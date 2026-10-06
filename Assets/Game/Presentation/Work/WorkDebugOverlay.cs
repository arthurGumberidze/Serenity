using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Work
{
    public enum WorkDebugCommand
    {
        CreateFromSelection = 0,
        SelectGroup = 1,
        AddSelected = 2,
        RemoveSelected = 3,
        CyclePriority = 4,
        AssignMove = 5,
        AssignHaul = 6,
        CancelJobs = 7
    }

    /// <summary>Development-only command surface. It submits canonical work orders and owns no work state.</summary>
    public sealed class WorkDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 398f;
        private const float PanelHeight = 420f;
        private WorkManager work;
        private SelectionProbe selection;
        private CharacterPresentationRegistry presentations;
        private WorldPileService piles;
        private ResourceInventoryRegistry inventories;
        private BuildingRegistry buildings;
        private LocalGameplayInputSource inputSource;
        private WorldPointerRaycaster raycaster;
        private ManualMoveCommandService manualMoveCommands;
        private int nextGroupNumber = 1;
        private WorkPriority priority = WorkPriority.High;
        private readonly WorkGroupId?[] hotkeyGroups = new WorkGroupId?[9];

        public WorkGroupId? CurrentGroupId { get; private set; }
        public int SelectionCount => selection?.SelectedCharacterIds.Count ?? 0;
        public WorkPriority Priority => priority;

        private void OnEnable()
        {
            InputSystem.onAfterUpdate += HandleGroupHotkeys;
            SubscribePrimaryClick();
            raycaster?.RegisterUiBlocker(this);
        }

        private void OnDisable()
        {
            InputSystem.onAfterUpdate -= HandleGroupHotkeys;
            UnsubscribePrimaryClick();
            raycaster?.UnregisterUiBlocker(this);
        }

        public void Initialize(WorkManager workManager, SelectionProbe selectionProbe,
            CharacterPresentationRegistry presentationRegistry, WorldPileService worldPiles,
            ResourceInventoryRegistry inventoryRegistry, BuildingRegistry buildingRegistry,
            LocalGameplayInputSource source, WorldPointerRaycaster worldRaycaster,
            ManualMoveCommandService moveCommands)
        {
            UnsubscribePrimaryClick();
            raycaster?.UnregisterUiBlocker(this);
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            presentations = presentationRegistry ?? throw new ArgumentNullException(nameof(presentationRegistry));
            piles = worldPiles ?? throw new ArgumentNullException(nameof(worldPiles));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            inputSource = source ?? throw new ArgumentNullException(nameof(source));
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            manualMoveCommands = moveCommands ?? throw new ArgumentNullException(nameof(moveCommands));
            SubscribePrimaryClick();
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = new Rect(Screen.width - 410f, 12f, PanelWidth, PanelHeight);
            return work != null && selection != null && (Application.isEditor || Debug.isDebugBuild);
        }

        public bool TryGetCommandGuiRect(WorkDebugCommand command, out Rect guiRect)
        {
            guiRect = default;
            if (!TryGetUiBlockingRect(out var panelRect)) return false;
            var localRect = GetCommandLocalRect(command);
            localRect.position += panelRect.position;
            guiRect = localRect;
            return true;
        }

        public WorkGroup CreateGroupFromSelection(string displayName = null)
        {
            RequireInitialized();
            var ids = selection.SelectedCharacterIds;
            if (ids.Count == 0) throw new InvalidOperationException("Select at least one character.");
            var group = work.CreateGroup(displayName ?? "Work Group " + nextGroupNumber++, ids);
            CurrentGroupId = group.Id;
            return group;
        }

        public void AddSelectionToCurrentGroup()
        {
            var group = CurrentGroup();
            foreach (var id in selection.SelectedCharacterIds)
                if (!group.Contains(id)) work.AddMember(group.Id, id);
        }

        public void RemoveSelectionFromCurrentGroup()
        {
            var group = CurrentGroup();
            foreach (var id in selection.SelectedCharacterIds.ToArray()) work.RemoveMember(group.Id, id);
        }

        public void SelectCurrentGroup()
        {
            var group = CurrentGroup();
            var active = new List<CharacterPresenter>();
            foreach (var id in group.Members)
                if (presentations.TryGet(id, out var presenter)) active.Add(presenter);
            selection.SelectCharacters(active);
        }

        public IReadOnlyList<WorkOrder> AssignCurrentGroupMove(WorldPosition target, WorkPriority selectedPriority)
        {
            var group = CurrentGroup();
            return manualMoveCommands.IssueForGroup(group.Id, target, selectedPriority).Jobs;
        }

        public bool TryGetGroup(WorkGroupId groupId, out WorkGroup group)
        {
            group = null;
            return work != null && work.Groups.TryGet(groupId, out group);
        }

        public IReadOnlyList<WorkOrder> AssignCurrentGroupHaul(InventoryOwner source, InventoryOwner destination,
            ResourceId resourceId, ResourceQuantity quantityPerWorker, WorkPriority selectedPriority)
        {
            var group = CurrentGroup();
            return work.CreateGroupHaulJobs(group.Id, source, destination, resourceId, quantityPerWorker,
                selectedPriority);
        }

        public int CancelCurrentGroupJobs()
        {
            var group = CurrentGroup();
            var count = 0;
            foreach (var job in work.Jobs.Where(x => x.GroupId == group.Id && !x.IsTerminal))
                if (work.Cancel(job.Id)) count++;
            return count;
        }

        public void SetPriority(WorkPriority value)
        {
            if (!Enum.IsDefined(typeof(WorkPriority), value)) throw new ArgumentOutOfRangeException(nameof(value));
            priority = value;
        }

        public void AssignHotkey(int slot, WorkGroupId groupId)
        {
            if (slot < 1 || slot > 9) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!work.Groups.TryGet(groupId, out _)) throw new InvalidOperationException("Work group does not exist.");
            hotkeyGroups[slot - 1] = groupId;
        }

        public bool RecallHotkey(int slot)
        {
            if (slot < 1 || slot > 9) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!hotkeyGroups[slot - 1].HasValue) return false;
            CurrentGroupId = hotkeyGroups[slot - 1];
            SelectCurrentGroup();
            return true;
        }

        private WorkGroup CurrentGroup()
        {
            RequireInitialized();
            if (!CurrentGroupId.HasValue) throw new InvalidOperationException("No work group is selected.");
            return work.Groups.Get(CurrentGroupId.Value);
        }

        private void RequireInitialized()
        {
            if (work == null) throw new InvalidOperationException("Work debug overlay is not initialized.");
        }

        private bool TryFindDemoHaul(out InventoryOwner source, out InventoryOwner destination,
            out ResourceId resourceId)
        {
            source = default;
            destination = default;
            resourceId = default;
            var pile = piles.All.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .FirstOrDefault(x => inventories.Get(x.InventoryOwner).GetAmount(x.ResourceId).Units > 0);
            var storage = inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                    buildings.TryGet(x.Owner.Id, out var building) &&
                    building.ConstructionState == ConstructionState.Completed)
                .OrderBy(x => x.Owner.Id.ToString(), StringComparer.Ordinal).FirstOrDefault();
            if (pile == null || storage == null) return false;
            source = pile.InventoryOwner;
            destination = storage.Owner;
            resourceId = pile.ResourceId;
            return true;
        }

        private void HandleGroupHotkeys()
        {
            if (work == null || Keyboard.current == null) return;
            var keys = new[]
            {
                Keyboard.current.digit1Key, Keyboard.current.digit2Key, Keyboard.current.digit3Key,
                Keyboard.current.digit4Key, Keyboard.current.digit5Key, Keyboard.current.digit6Key,
                Keyboard.current.digit7Key, Keyboard.current.digit8Key, Keyboard.current.digit9Key
            };
            for (var i = 0; i < keys.Length; i++)
            {
                if (!keys[i].wasPressedThisFrame) continue;
                var control = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                if (control && CurrentGroupId.HasValue) AssignHotkey(i + 1, CurrentGroupId.Value);
                else RecallHotkey(i + 1);
            }
        }

        private void SubscribePrimaryClick()
        {
            if (inputSource == null) return;
            inputSource.PrimaryClicked -= HandlePrimaryClick;
            inputSource.PrimaryClicked += HandlePrimaryClick;
        }

        private void UnsubscribePrimaryClick()
        {
            if (inputSource != null) inputSource.PrimaryClicked -= HandlePrimaryClick;
        }

        private void HandlePrimaryClick()
        {
            if (inputSource == null || !TryGetUiBlockingRect(out var panelRect)) return;
            var guiPosition = WorldPointerRaycaster.ScreenToGuiPoint(inputSource.PointerPosition);
            if (!panelRect.Contains(guiPosition)) return;
            foreach (WorkDebugCommand command in Enum.GetValues(typeof(WorkDebugCommand)))
            {
                if (!TryGetCommandGuiRect(command, out var commandRect) || !commandRect.Contains(guiPosition))
                    continue;
                ExecuteCommand(command);
                return;
            }
        }

        private void ExecuteCommand(WorkDebugCommand command)
        {
            switch (command)
            {
                case WorkDebugCommand.CreateFromSelection:
                    TryUi(() => CreateGroupFromSelection());
                    break;
                case WorkDebugCommand.SelectGroup:
                    TryUi(SelectCurrentGroup);
                    break;
                case WorkDebugCommand.AddSelected:
                    TryUi(AddSelectionToCurrentGroup);
                    break;
                case WorkDebugCommand.RemoveSelected:
                    TryUi(RemoveSelectionFromCurrentGroup);
                    break;
                case WorkDebugCommand.CyclePriority:
                    priority = (WorkPriority)(((int)priority + 1) % Enum.GetValues(typeof(WorkPriority)).Length);
                    break;
                case WorkDebugCommand.AssignMove:
                    TryUi(() => AssignCurrentGroupMove(new WorldPosition(0f, 0f, 10f), priority));
                    break;
                case WorkDebugCommand.AssignHaul:
                    TryUi(() =>
                    {
                        if (!TryFindDemoHaul(out var source, out var destination, out var resourceId))
                            throw new InvalidOperationException("No valid pile/storage pair.");
                        AssignCurrentGroupHaul(source, destination, resourceId, new ResourceQuantity(1), priority);
                    });
                    break;
                case WorkDebugCommand.CancelJobs:
                    TryUi(() => CancelCurrentGroupJobs());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private void OnGUI()
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || work == null || selection == null) return;
            TryGetUiBlockingRect(out var panelRect);
            GUI.Box(panelRect, GUIContent.none);
            GUI.BeginGroup(panelRect);
            GUI.Label(new Rect(8f, 8f, PanelWidth - 16f, 20f), $"U11 Work — selected NPCs: {SelectionCount}");
            GUI.Label(new Rect(8f, 30f, PanelWidth - 16f, 20f), CurrentGroupId.HasValue
                ? $"Group: {CurrentGroup().DisplayName} ({CurrentGroup().MemberCount})"
                : "Group: none");
            DrawCommandButton("Create from selection", WorkDebugCommand.CreateFromSelection);
            DrawCommandButton("Select group", WorkDebugCommand.SelectGroup);
            DrawCommandButton("Add selected", WorkDebugCommand.AddSelected);
            DrawCommandButton("Remove selected", WorkDebugCommand.RemoveSelected);
            GUI.Label(new Rect(8f, 106f, 150f, 22f), "Priority: " + priority);
            DrawCommandButton("Cycle priority", WorkDebugCommand.CyclePriority);
            DrawCommandButton("Assign group Move to demo point", WorkDebugCommand.AssignMove);
            DrawCommandButton("Assign group Haul x1", WorkDebugCommand.AssignHaul);
            DrawCommandButton("Cancel current group jobs", WorkDebugCommand.CancelJobs);

            var jobY = 216f;
            foreach (var job in work.Jobs.OrderByDescending(x => x.Id.Value).Take(8))
            {
                GUI.Label(new Rect(8f, jobY, PanelWidth - 16f, 20f),
                    $"#{job.Id} {job.Type} {job.Priority} {job.Status} claim={job.ClaimState}");
                jobY += 20f;
            }
            GUI.EndGroup();
        }

        private static void DrawCommandButton(string label, WorkDebugCommand command)
        {
            GUI.Button(GetCommandLocalRect(command), label);
        }

        private static Rect GetCommandLocalRect(WorkDebugCommand command)
        {
            const float left = 8f;
            const float gap = 6f;
            const float fullWidth = PanelWidth - left * 2f;
            const float halfWidth = (fullWidth - gap) * 0.5f;
            switch (command)
            {
                case WorkDebugCommand.CreateFromSelection: return new Rect(left, 54f, halfWidth, 22f);
                case WorkDebugCommand.SelectGroup: return new Rect(left + halfWidth + gap, 54f, halfWidth, 22f);
                case WorkDebugCommand.AddSelected: return new Rect(left, 80f, halfWidth, 22f);
                case WorkDebugCommand.RemoveSelected: return new Rect(left + halfWidth + gap, 80f, halfWidth, 22f);
                case WorkDebugCommand.CyclePriority: return new Rect(158f, 106f, fullWidth - 150f, 22f);
                case WorkDebugCommand.AssignMove: return new Rect(left, 132f, fullWidth, 22f);
                case WorkDebugCommand.AssignHaul: return new Rect(left, 158f, fullWidth, 22f);
                case WorkDebugCommand.CancelJobs: return new Rect(left, 184f, fullWidth, 22f);
                default: throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private static void TryUi(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogWarning("U11 work command rejected: " + exception.Message); }
        }
    }
}
