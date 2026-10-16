using System;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Combat;
using Game.Simulation.Work;
using UnityEngine;

namespace Game.Presentation.Work
{
    /// <summary>Routes U05 secondary-click world intent into the pure U11 manual-move command boundary.</summary>
    public sealed class ManualMoveInputController : MonoBehaviour
    {
        private LocalGameplayInputSource input;
        private WorldPointerRaycaster raycaster;
        private SelectionProbe selection;
        private WorkDebugOverlay workOverlay;
        private BuildingPlacementController buildingPlacement;
        private ManualMoveCommandService commands;
        private CombatCommandService combatCommands;
        private Func<long> calendarTickProvider;

        public ManualMoveCommandResult LastResult { get; private set; }
        public CombatCommandResult LastCombatResult { get; private set; }
        public bool ContextMenusEnabled { get; private set; }

        public void EnableContextMenus() => ContextMenusEnabled = true;

        public void Initialize(LocalGameplayInputSource inputSource, WorldPointerRaycaster worldRaycaster,
            SelectionProbe selectionProbe, WorkDebugOverlay overlay, BuildingPlacementController placement,
            ManualMoveCommandService commandService, CombatCommandService combatCommandService = null,
            Func<long> currentCalendarTick = null)
        {
            Unsubscribe();
            input = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            workOverlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            buildingPlacement = placement ?? throw new ArgumentNullException(nameof(placement));
            commands = commandService ?? throw new ArgumentNullException(nameof(commandService));
            combatCommands = combatCommandService;
            calendarTickProvider = currentCalendarTick;
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (input == null) return;
            input.SecondaryClicked -= OnSecondaryClicked;
            input.SecondaryClicked += OnSecondaryClicked;
        }

        private void Unsubscribe()
        {
            if (input != null) input.SecondaryClicked -= OnSecondaryClicked;
        }

        private void OnSecondaryClicked()
        {
            if (buildingPlacement.IsActive)
            {
                buildingPlacement.CancelPlacement();
                return;
            }

            if (ContextMenusEnabled) return;

            if (!raycaster.TryGetWorldHit(input.PointerPosition, out var hit)) return;

            var selectedIds = selection.SelectedCharacterIds;
            var hostile = hit.collider.GetComponentInParent<CharacterPresenter>();
            if (hostile != null && hostile.IsBound && combatCommands != null && selectedIds.Count > 0)
            {
                LastCombatResult = combatCommands.IssueAttack(selectedIds, hostile.CharacterId,
                    calendarTickProvider == null ? 0L : calendarTickProvider());
                return;
            }
            if (hit.collider.GetComponentInParent<BuildableGround>() == null) return;

            var target = new WorldPosition(hit.point.x, hit.point.y, hit.point.z);
            if (selectedIds.Count > 0)
            {
                WorkGroupId? groupId = null;
                if (workOverlay.CurrentGroupId.HasValue)
                {
                    var group = ResolveGroup(workOverlay.CurrentGroupId.Value);
                    if (group != null && group.Members.SequenceEqual(selectedIds)) groupId = group.Id;
                }
                LastResult = commands.Issue(selectedIds, target, workOverlay.Priority, groupId);
                return;
            }

            if (workOverlay.CurrentGroupId.HasValue)
                LastResult = commands.IssueForGroup(workOverlay.CurrentGroupId.Value, target, workOverlay.Priority);
        }

        private WorkGroup ResolveGroup(WorkGroupId groupId) => workOverlay.TryGetGroup(groupId, out var group)
            ? group : null;
    }
}
