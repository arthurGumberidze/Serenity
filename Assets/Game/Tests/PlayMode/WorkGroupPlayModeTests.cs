using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.AI;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Presentation.Resources;
using Game.Presentation.Work;
using Game.Simulation.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U11")]
    public sealed class WorkGroupPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftMultiSelectionCreatesGroupAndSurvivesPresenterRespawn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            SelectFromAbove(root, root.DemoPresenters[0], false);
            SelectFromAbove(root, root.DemoPresenters[1], true);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));
            Assert.That(root.WorkDebug.SelectionCount, Is.EqualTo(2));
            var group = root.WorkDebug.CreateGroupFromSelection("Founders");
            Assert.That(group.MemberCount, Is.EqualTo(2));
            root.WorkDebug.AssignHotkey(1, group.Id);
            SelectFromAbove(root, root.DemoPilePresenters[0], false);
            Assert.That(root.WorkDebug.RecallHotkey(1), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));

            var character = root.MaleDemoCharacter;
            var oldPresenter = root.DemoPresenters.Single(x => x.CharacterId == character.Id);
            root.CharacterSpawner.Despawn(oldPresenter);
            yield return null;
            var replacement = root.CharacterSpawner.Spawn(character, new Vector3(-3f, 0f, 0f), Quaternion.identity);
            root.AiRuntime.Register(character, replacement);
            root.WorkDebug.SelectCurrentGroup();

            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(character.Id));
            Assert.That(root.Work.Groups.Get(group.Id).Contains(character.Id), Is.True);
            root.AiRuntime.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator CreateFromSelectionUiClickUsesCurrentSelectionWithoutClickThrough()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            var selectedId = root.DemoPresenters[0].CharacterId;
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { selectedId }));
            Assert.That(root.Input.Actions.FindActionMap("Pointer", true).enabled, Is.True,
                "Pointer action map must be enabled for the real input path.");
            yield return WaitForCommandRect(root.WorkDebug, WorkDebugCommand.CreateFromSelection);
            var primaryClicks = 0;
            var pointerAtClick = Vector2.zero;
            root.Input.PrimaryClicked += () =>
            {
                primaryClicks++;
                pointerAtClick = root.Input.PointerPosition;
            };

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.CreateFromSelection);

            Assert.That(primaryClicks, Is.EqualTo(1), "Synthetic mouse press did not reach LocalGameplayInputSource.");
            Assert.That(root.WorkDebug.TryGetCommandGuiRect(WorkDebugCommand.CreateFromSelection, out var commandRect),
                Is.True);
            var expectedPointer = WorldPointerRaycaster.GuiToScreenPoint(commandRect.center);
            Assert.That(Vector2.Distance(pointerAtClick, expectedPointer), Is.LessThan(0.5f),
                $"Pointer at click was {pointerAtClick}, expected {expectedPointer}.");
            Assert.That(root.WorkDebug.CurrentGroupId.HasValue, Is.True);
            var group = root.Work.Groups.Get(root.WorkDebug.CurrentGroupId.Value);
            Assert.That(group.MemberCount, Is.EqualTo(1));
            Assert.That(group.Contains(selectedId), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { selectedId }));
        }

        [UnityTest]
        public IEnumerator ClickInsideEveryDevelopmentPanelDoesNotClearSelection()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            var selectedId = root.DemoPresenters[0].CharacterId;
            yield return null;

            var blockers = new IWorldPointerUiBlocker[]
            {
                Object.FindAnyObjectByType<ResourceDebugOverlay>(),
                Object.FindAnyObjectByType<Tier1AiDebugOverlay>(),
                root.WorkDebug
            };
            foreach (var blocker in blockers)
            {
                Assert.That(blocker, Is.Not.Null);
                Assert.That(blocker.TryGetUiBlockingRect(out var panelRect), Is.True);
                var guiPoint = new Vector2(panelRect.xMin + 4f, panelRect.yMin + 4f);
                yield return ClickScreenPoint(WorldPointerRaycaster.GuiToScreenPoint(guiPoint));
                Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { selectedId }));
            }
        }

        [UnityTest]
        public IEnumerator RemainingWorkPanelButtonsUseInputPathWithoutSelectionClickThrough()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            yield return WaitForAllCommandRects(root.WorkDebug);

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.CreateFromSelection);
            var groupId = root.WorkDebug.CurrentGroupId.Value;
            var firstId = root.DemoPresenters[0].CharacterId;
            var secondId = root.DemoPresenters[1].CharacterId;

            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[1] });
            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.AddSelected);
            Assert.That(root.Work.Groups.Get(groupId).Contains(secondId), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { secondId }));

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.RemoveSelected);
            Assert.That(root.Work.Groups.Get(groupId).Contains(secondId), Is.False);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { secondId }));

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.SelectGroup);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { firstId }));

            var oldPriority = root.WorkDebug.Priority;
            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.CyclePriority);
            Assert.That(root.WorkDebug.Priority, Is.Not.EqualTo(oldPriority));
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { firstId }));

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.AssignMove);
            Assert.That(root.Work.Jobs.Any(x => x.GroupId == groupId && x.Type == WorkJobType.Move), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { firstId }));
            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.CancelJobs);
            Assert.That(root.Work.Jobs.Where(x => x.GroupId == groupId).All(x => x.IsTerminal), Is.True);

            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.AssignHaul);
            Assert.That(root.Work.Jobs.Any(x => x.GroupId == groupId && x.Type == WorkJobType.Haul), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { firstId }));
            yield return ClickWorkCommand(root.WorkDebug, WorkDebugCommand.CancelJobs);
            Assert.That(root.Work.Jobs.Where(x => x.GroupId == groupId).All(x => x.IsTerminal), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { firstId }));
        }

        [UnityTest]
        public IEnumerator GroupMoveCommandDrivesPhysicalNavMeshMovement()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            var group = root.WorkDebug.CreateGroupFromSelection("Scouts");
            var start = root.DemoPresenters.ToDictionary(x => x.CharacterId, x => x.transform.position);
            var jobs = root.WorkDebug.AssignCurrentGroupMove(new WorldPosition(0f, 0f, 9f), WorkPriority.Urgent);
            Assert.That(jobs.Count, Is.EqualTo(2));
            root.AiRuntime.SetPaused(false);

            var timeout = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < timeout && jobs.Any(x => !x.IsTerminal)) yield return null;
            Assert.That(jobs.All(x => x.Status == WorkJobStatus.Completed), Is.True);
            Assert.That(root.DemoPresenters.Any(x => Vector3.Distance(start[x.CharacterId], x.transform.position) > 2f), Is.True);
            Assert.That(root.Work.Groups.Get(group.Id).MemberCount, Is.EqualTo(2));
            Assert.That(root.AiRuntime.PerAgentAiUpdateCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RightClickGroundMovesTwoSelectedNpcWithFormationAndReturnsToUtilityAi()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2),
                "The established multi-selection must feed the command path.");
            var ids = root.DemoPresenters.Select(x => x.CharacterId).ToArray();
            var start = root.DemoPresenters.ToDictionary(x => x.CharacterId, x => x.transform.position);
            var screenPoint = FindGroundScreenPoint(root, out var clickedTarget);

            yield return ClickScreenPoint(screenPoint, MouseButton.Right);

            var result = root.ManualMoveInput.LastResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.CreatedCount, Is.EqualTo(2));
            Assert.That(result.Jobs.All(x => x.Type == WorkJobType.Move &&
                x.Status == WorkJobStatus.Assigned), Is.True);
            Assert.That(result.Jobs.Select(x => x.Target.Position).Distinct().Count(), Is.EqualTo(2),
                "Formation slots must not stack agents on one destination.");
            var commandCenter = new Vector3(
                result.Jobs.Average(x => x.Target.Position.X),
                result.Jobs.Average(x => x.Target.Position.Y),
                result.Jobs.Average(x => x.Target.Position.Z));
            Assert.That(Vector3.Distance(clickedTarget, commandCenter), Is.LessThan(0.5f),
                $"Pointer raycast produced {commandCenter} for requested ground point {clickedTarget}.");
            root.AiRuntime.SetPaused(false);

            var timeout = Time.realtimeSinceStartup + 15f;
            var arrivalPositions = new System.Collections.Generic.Dictionary<Game.Domain.StableEntityId, Vector3>();
            while (Time.realtimeSinceStartup < timeout && result.Jobs.Any(x => !x.IsTerminal))
            {
                foreach (var job in result.Jobs.Where(x => x.IsTerminal && x.AssigneeId.HasValue))
                    if (!arrivalPositions.ContainsKey(job.AssigneeId.Value))
                        arrivalPositions.Add(job.AssigneeId.Value,
                            root.DemoPresenters.Single(x => x.CharacterId == job.AssigneeId.Value).transform.position);
                yield return null;
            }
            foreach (var job in result.Jobs.Where(x => x.IsTerminal && x.AssigneeId.HasValue))
                if (!arrivalPositions.ContainsKey(job.AssigneeId.Value))
                    arrivalPositions.Add(job.AssigneeId.Value,
                        root.DemoPresenters.Single(x => x.CharacterId == job.AssigneeId.Value).transform.position);
            Assert.That(result.Jobs.All(x => x.Status == WorkJobStatus.Completed), Is.True);
            foreach (var presenter in root.DemoPresenters)
            {
                Assert.That(Vector3.Distance(start[presenter.CharacterId], presenter.transform.position),
                    Is.GreaterThan(2f));
                Assert.That(Vector3.Distance(clickedTarget, arrivalPositions[presenter.CharacterId]),
                    Is.LessThan(2.5f));
            }
            Assert.That(root.DemoPresenters.Select(x => x.CharacterId), Is.EquivalentTo(ids));

            timeout = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < timeout && root.AiAgents.All.Any(x =>
                       x.CurrentAction == UtilityActionKind.ManualMove)) yield return null;
            Assert.That(root.AiAgents.All.All(x => x.CurrentAction != UtilityActionKind.ManualMove), Is.True);
            Assert.That(ids.All(x => root.Tiers.GetTier(x) == CharacterSimulationTier.Tier1), Is.True);
        }

        [UnityTest]
        public IEnumerator ActiveWorkGroupRightClickReportsRemoteUnavailableWithoutTierOrOffCameraMutation()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            var group = root.WorkDebug.CreateGroupFromSelection("Mixed tiers");
            var remoteId = root.FemaleDemoCharacter.Id;
            root.DemoPresenters.Single(x => x.CharacterId == remoteId).transform.position =
                new Vector3(100f, 0f, 100f);
            Physics.SyncTransforms();
            root.Tiers.Transition(remoteId, CharacterSimulationTier.Tier3);
            root.SelectionProbe.SelectCharacters(System.Array.Empty<Game.Presentation.Characters.CharacterPresenter>());
            var remoteBefore = root.Tier3Characters.Get(remoteId).State;
            var hashBefore = root.OffCameraSimulation.ComputeStateHash(new[] { remoteBefore });
            var screenPoint = FindGroundScreenPoint(root, out _);

            yield return ClickScreenPoint(screenPoint, MouseButton.Right);

            var result = root.ManualMoveInput.LastResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.Single(x => x.CharacterId == remoteId).Status,
                Is.EqualTo(ManualMoveCommandStatus.TierUnavailable));
            Assert.That(root.Work.Jobs.Any(x => x.AssigneeId == remoteId), Is.False);
            Assert.That(root.Tiers.GetTier(remoteId), Is.EqualTo(CharacterSimulationTier.Tier3));
            Assert.That(root.OffCameraSimulation.ComputeStateHash(new[]
                { root.Tier3Characters.Get(remoteId).State }), Is.EqualTo(hashBefore));
            Assert.That(root.Work.Groups.Get(group.Id).Contains(remoteId), Is.True);
        }

        [UnityTest]
        public IEnumerator RightClickInsideUiDoesNotCreateMove()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            Assert.That(root.WorkDebug.TryGetUiBlockingRect(out var panelRect), Is.True);
            var before = root.Work.JobCount;

            yield return ClickScreenPoint(WorldPointerRaycaster.GuiToScreenPoint(panelRect.center), MouseButton.Right);

            Assert.That(root.Work.JobCount, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RightClickDuringBuildingPlacementCancelsWithoutCreatingMove()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            root.BuildingPlacementController.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(root.BuildingPlacementController.IsActive, Is.True);
            var before = root.Work.JobCount;
            var screenPoint = FindGroundScreenPoint(root, out _);

            yield return ClickScreenPoint(screenPoint, MouseButton.Right);

            Assert.That(root.BuildingPlacementController.IsActive, Is.False);
            Assert.That(root.Work.JobCount, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator GroupHaulUsesCanonicalInventoryThenAgentsReturnToAutonomousAi()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            root.WorkDebug.CreateGroupFromSelection("Haulers");
            var pile = root.DemoPilePresenters.OrderBy(x => x.PileId.ToString()).First();
            var source = new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId);
            var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage, root.DemoStorageBuilding.Id);
            var initialTotal = root.Inventories.All.Sum(x => x.GetAmount(pile.ResourceId).Units);
            var initialSource = root.Inventories.Get(source).GetAmount(pile.ResourceId).Units;
            var initialStorage = root.Inventories.Get(destination).GetAmount(pile.ResourceId).Units;
            var jobs = root.WorkDebug.AssignCurrentGroupHaul(source, destination, pile.ResourceId,
                new ResourceQuantity(1), WorkPriority.Urgent);
            Assert.That(jobs.Count, Is.EqualTo(2));
            root.AiRuntime.SetPaused(false);

            var timeout = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < timeout && jobs.Any(x => !x.IsTerminal)) yield return null;
            Assert.That(jobs.All(x => x.Status == WorkJobStatus.Completed), Is.True);
            Assert.That(root.Inventories.Get(source).GetAmount(pile.ResourceId).Units, Is.EqualTo(initialSource - 2));
            Assert.That(root.Inventories.Get(destination).GetAmount(pile.ResourceId).Units, Is.EqualTo(initialStorage + 2));
            Assert.That(root.Inventories.All.Sum(x => x.GetAmount(pile.ResourceId).Units), Is.EqualTo(initialTotal));
            Assert.That(jobs.All(x => x.ClaimState == WorkClaimState.Released), Is.True);

            timeout = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < timeout && root.AiAgents.All.Any(x =>
                       x.CurrentAction == UtilityActionKind.ManualHaul || x.CurrentAction == UtilityActionKind.ManualMove))
                yield return null;
            Assert.That(root.AiAgents.All.All(x => x.CurrentAction != UtilityActionKind.ManualHaul &&
                x.CurrentAction != UtilityActionKind.ManualMove), Is.True);
            Assert.That(root.AiAgents.All.Any(x => x.CurrentAction == UtilityActionKind.Haul ||
                x.CurrentAction == UtilityActionKind.Rest || x.CurrentAction == UtilityActionKind.Idle), Is.True);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(root.PointerRaycaster.WorldCamera);
        }

        private static void SelectFromAbove(LocalSceneCompositionRoot root, Component target, bool additive)
        {
            var collider = target.GetComponentInChildren<Collider>(true);
            Assert.That(collider, Is.Not.Null);
            var center = collider.bounds.center;
            var point = PointerSelectionTestPoint.AimCameraAtUnblockedPoint(root, center);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(point), Is.False);
            Assert.That(root.SelectionProbe.TrySelectAt(point, additive), Is.True);
        }

        private static Vector2 FindGroundScreenPoint(LocalSceneCompositionRoot root, out Vector3 worldPoint)
        {
            const int samples = 20;
            var bestPoint = default(Vector2);
            worldPoint = default;
            var bestDistance = float.PositiveInfinity;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            for (var y = 0; y < samples; y++)
            for (var x = 0; x < samples; x++)
            {
                var point = new Vector2(
                    Mathf.Lerp(24f, Mathf.Max(24f, Screen.width - 24f), x / (samples - 1f)),
                    Mathf.Lerp(24f, Mathf.Max(24f, Screen.height - 24f), y / (samples - 1f)));
                if (root.PointerRaycaster.IsPointerBlockedByUi(point)) continue;
                if (!root.PointerRaycaster.TryGetWorldHit(point, out var hit) ||
                    hit.collider.GetComponentInParent<BuildableGround>() == null) continue;
                var distance = (point - center).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestPoint = point;
                worldPoint = hit.point;
                bestDistance = distance;
            }
            Assert.That(float.IsPositiveInfinity(bestDistance), Is.False,
                "No unblocked buildable-ground point is visible to the RTS camera.");
            return bestPoint;
        }

        private static IEnumerator WaitForCommandRect(WorkDebugOverlay overlay, WorkDebugCommand command)
        {
            for (var frame = 0; frame < 20; frame++)
            {
                if (overlay.TryGetCommandGuiRect(command, out var rect) && rect.width > 0f) yield break;
                yield return null;
            }
            Assert.Fail("Work debug command rect was not produced for " + command + ".");
        }

        private static IEnumerator WaitForAllCommandRects(WorkDebugOverlay overlay)
        {
            foreach (WorkDebugCommand command in System.Enum.GetValues(typeof(WorkDebugCommand)))
                yield return WaitForCommandRect(overlay, command);
        }

        private static IEnumerator ClickWorkCommand(WorkDebugOverlay overlay, WorkDebugCommand command)
        {
            Assert.That(overlay.TryGetCommandGuiRect(command, out var guiRect), Is.True);
            yield return ClickScreenPoint(WorldPointerRaycaster.GuiToScreenPoint(guiRect.center));
        }

        private static IEnumerator ClickScreenPoint(Vector2 screenPoint, MouseButton button = MouseButton.Left)
        {
            var input = Object.FindAnyObjectByType<LocalGameplayInputSource>();
            Assert.That(input, Is.Not.Null);
            var actionName = button == MouseButton.Right ? "SecondaryClick" : "PrimaryClick";
            var clickAction = input.Actions.FindActionMap("Pointer", true).FindAction(actionName, true);
            var oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            var oldEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.EnableDevice(mouse);
            input.Actions.devices = new InputDevice[] { mouse };
            try
            {
                Assert.That(clickAction.controls.Select(x => x.device).OfType<Mouse>().FirstOrDefault(), Is.Not.Null,
                    actionName + " must resolve to the synthetic Mouse device.");
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
                yield return null;
                InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = screenPoint }.WithButton(button));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
                yield return null;
            }
            finally
            {
                input.Actions.devices = null;
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInputBehavior;
            }
        }

        private static void Capture(Camera camera)
        {
            camera.transform.position = new Vector3(-2f, 14f, -18f);
            camera.transform.LookAt(new Vector3(-1f, 0.5f, 1f));
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/U11-work-groups.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
