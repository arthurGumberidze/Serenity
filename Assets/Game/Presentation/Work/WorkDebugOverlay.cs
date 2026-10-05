using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Characters;
using Game.Presentation.Interaction;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Work
{
    /// <summary>Development-only command surface. It submits canonical work orders and owns no work state.</summary>
    public sealed class WorkDebugOverlay : MonoBehaviour
    {
        private WorkManager work;
        private SelectionProbe selection;
        private CharacterPresentationRegistry presentations;
        private WorldPileService piles;
        private ResourceInventoryRegistry inventories;
        private BuildingRegistry buildings;
        private int nextGroupNumber = 1;
        private WorkPriority priority = WorkPriority.High;
        private readonly WorkGroupId?[] hotkeyGroups = new WorkGroupId?[9];

        public WorkGroupId? CurrentGroupId { get; private set; }
        public int SelectionCount => selection?.SelectedCharacterIds.Count ?? 0;
        public WorkPriority Priority => priority;

        private void OnEnable() => InputSystem.onAfterUpdate += HandleGroupHotkeys;
        private void OnDisable() => InputSystem.onAfterUpdate -= HandleGroupHotkeys;

        public void Initialize(WorkManager workManager, SelectionProbe selectionProbe,
            CharacterPresentationRegistry presentationRegistry, WorldPileService worldPiles,
            ResourceInventoryRegistry inventoryRegistry, BuildingRegistry buildingRegistry)
        {
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            presentations = presentationRegistry ?? throw new ArgumentNullException(nameof(presentationRegistry));
            piles = worldPiles ?? throw new ArgumentNullException(nameof(worldPiles));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
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
            return work.CreateGroupMoveJobs(group.Id, target, selectedPriority);
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

        private void OnGUI()
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || work == null || selection == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 410f, 12f, 398f, 420f), GUI.skin.box);
            GUILayout.Label($"U11 Work — selected NPCs: {SelectionCount}");
            GUILayout.Label(CurrentGroupId.HasValue
                ? $"Group: {CurrentGroup().DisplayName} ({CurrentGroup().MemberCount})"
                : "Group: none");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create from selection")) TryUi(() => CreateGroupFromSelection());
            if (GUILayout.Button("Select group")) TryUi(SelectCurrentGroup);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add selected")) TryUi(AddSelectionToCurrentGroup);
            if (GUILayout.Button("Remove selected")) TryUi(RemoveSelectionFromCurrentGroup);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Priority: " + priority, GUILayout.Width(150f));
            if (GUILayout.Button("Cycle priority"))
                priority = (WorkPriority)(((int)priority + 1) % Enum.GetValues(typeof(WorkPriority)).Length);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Assign group Move to demo point"))
                TryUi(() => AssignCurrentGroupMove(new WorldPosition(0f, 0f, 10f), priority));
            if (GUILayout.Button("Assign group Haul x1"))
                TryUi(() =>
                {
                    if (!TryFindDemoHaul(out var source, out var destination, out var resourceId))
                        throw new InvalidOperationException("No valid pile/storage pair.");
                    AssignCurrentGroupHaul(source, destination, resourceId, new ResourceQuantity(1), priority);
                });
            if (GUILayout.Button("Cancel current group jobs")) TryUi(() => CancelCurrentGroupJobs());

            foreach (var job in work.Jobs.OrderByDescending(x => x.Id.Value).Take(8))
                GUILayout.Label($"#{job.Id} {job.Type} {job.Priority} {job.Status} claim={job.ClaimState}");
            GUILayout.EndArea();
        }

        private static void TryUi(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogWarning("U11 work command rejected: " + exception.Message); }
        }
    }
}
