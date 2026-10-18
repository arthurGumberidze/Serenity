using System;
using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Infrastructure;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U16A")]
    public sealed class U16ADebugGameplayControlPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoHardcodedDemoAssignmentAndSelectedResearchCommandsReportRealActiveState()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            Assert.That(root.Research.AssignedWorkers, Is.Empty,
                "Aren and Mira must not be assigned by LocalGameplay bootstrap.");
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });

            Assert.That(root.ResearchDebug.AssignSelected(), Is.EqualTo(1));
            Assert.That(root.ResearchDebug.AssignSelected(), Is.Zero);
            Assert.That(root.ResearchDebug.CaptureWorkerStatus().AssignedCount, Is.EqualTo(1));
            Assert.That(root.ResearchDebug.CaptureWorkerStatus().ActiveCount, Is.Zero);
            Assert.That(root.ResearchDebug.StartSelected(), Is.True);
            AdvanceSchedulerUntilResearchActive(root);
            var active = root.ResearchDebug.CaptureWorkerStatus();
            Assert.That(active.ActiveCount, Is.EqualTo(1));
            Assert.That(active.ActiveWorkerIds.Single(), Is.EqualTo(root.MaleDemoCharacter.Id));

            Assert.That(root.ResearchDebug.RemoveSelected(), Is.EqualTo(1));
            Assert.That(root.ResearchDebug.CaptureWorkerStatus().AssignedCount, Is.Zero);
            Assert.That(root.ResearchDebug.CaptureWorkerStatus().ActiveCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingSelectorRejectsLockedCropAndRoutesUnlockedCropThroughExistingPlacement()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var cropId = new BuildingDefinitionId("primitive_crop_plot");
            var options = root.BuildingDebug.Options;
            CollectionAssert.AreEquivalent(new[] { "primitive_shelter", "storage_basket", "primitive_crop_plot", "military_barracks" },
                options.Select(option => option.Definition.Id.ToString()));
            var locked = options.Single(option => option.Definition.Id == cropId);
            Assert.That(locked.IsAvailable, Is.False);
            Assert.That(locked.LockReason, Does.Contain("Примитивное земледелие"));
            Assert.That(root.BuildingDebug.Select(cropId), Is.False);
            Assert.That(root.BuildingPlacementController.IsActive, Is.False);
            Assert.That(root.BuildingPlacementController.LastConstructionFailure,
                Is.EqualTo(Game.Simulation.Resources.ConstructionFailureReason.LockedByResearch));

            root.Research.AssignWorker(root.MaleDemoCharacter.Id);
            CompleteTechnology(root, "controlled_fire");
            CompleteTechnology(root, "fiber_working");
            CompleteTechnology(root, "stone_tools");
            CompleteTechnology(root, "primitive_agriculture");
            Assert.That(root.BuildingDebug.Options.Single(option => option.Definition.Id == cropId).IsAvailable, Is.True);
            Assert.That(root.BuildingDebug.Select(cropId), Is.True);
            Assert.That(root.BuildingPlacementController.ActiveDefinitionId, Is.EqualTo(cropId));
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(25f, 0f, 25f)), Is.True);
            var placed = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(placed, Is.Not.Null);
            Assert.That(placed.DefinitionId, Is.EqualTo(cropId));
            Assert.That(root.Production.TryGetSite(placed.Id, out var site), Is.True);
            Assert.That(site.Kind, Is.EqualTo(Game.Domain.Production.ProductionSiteKind.PrimitiveCropPlot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseStopsClockAiResearchAndProductionThenResumesWithoutUiClickThrough()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Research.AssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            AdvanceSchedulerUntilResearchActive(root);
            Assert.That(root.ResearchDebug.CaptureWorkerStatus().ActiveCount, Is.EqualTo(1));
            root.TimeDebug.SetSpeed(GameSpeed.Maximum);
            root.TimeDebug.TogglePause();
            Assert.That(root.Clock.IsPaused, Is.True);
            var clockTick = root.Clock.State.CalendarTicks;
            var researchHash = root.Research.ComputeStateHash();
            var productionHash = root.Production.ComputeStateHash();
            var aiUpdates = root.AiScheduler.Metrics.TotalActionUpdates;

            yield return null;
            yield return null;

            Assert.That(root.Clock.State.CalendarTicks, Is.EqualTo(clockTick));
            Assert.That(root.Research.ComputeStateHash(), Is.EqualTo(researchHash));
            Assert.That(root.Production.ComputeStateHash(), Is.EqualTo(productionHash));
            Assert.That(root.AiScheduler.Metrics.TotalActionUpdates, Is.EqualTo(aiUpdates));

            root.TimeDebug.TogglePause();
            Assert.That(root.Clock.IsPaused, Is.False);
            var timeout = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < timeout && root.Clock.State.CalendarTicks == clockTick) yield return null;
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(clockTick));
            Assert.That(root.Production.LastCalendarTick, Is.GreaterThan(clockTick));

            root.AiRuntime.SetPaused(true);
            root.Research.Cancel();
            CompleteTechnology(root, "shelter_construction");
            var shelterId = new BuildingDefinitionId("primitive_shelter");
            Assert.That(root.BuildingPlacementController.TryStartPlacement(shelterId), Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(30f, 0f, 30f)), Is.True);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            var beforeBuildings = root.Buildings.Count;
            Assert.That(root.TimeDebug.TryGetUiBlockingRect(out var timeRect), Is.True);

            yield return ClickScreenPoint(WorldPointerRaycaster.GuiToScreenPoint(
                new Vector2(timeRect.xMin + 4f, timeRect.yMin + 4f)));

            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings),
                "A UI click must not confirm the building behind the panel.");
            Assert.That(root.BuildingPlacementController.IsActive, Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds,
                Is.EqualTo(new[] { root.MaleDemoCharacter.Id }));
            root.BuildingPlacementController.CancelPlacement();
        }

        private static void AdvanceSchedulerUntilResearchActive(LocalSceneCompositionRoot root)
        {
            // Ten settlers no longer fit in one scheduler budget; identity sorting
            // must not require the monarch to be processed on the first iteration.
            for (var i = 0; i < root.AiAgents.Count && root.ResearchDebug.CaptureWorkerStatus().ActiveCount == 0; i++)
                root.AiScheduler.Advance(new GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.Zero));
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string technologyId)
        {
            var id = new TechnologyId(technologyId);
            if (root.Research.IsCompleted(id)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(id), Is.True, technologyId + " must be available.");
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                checked(root.Research.LastProcessedTick + root.Technologies.Get(id).ResearchWorkRequiredTicks), true);
            Assert.That(root.Research.IsCompleted(id), Is.True);
        }

        private static IEnumerator ClickScreenPoint(Vector2 screenPoint)
        {
            var input = UnityEngine.Object.FindAnyObjectByType<LocalGameplayInputSource>();
            var clickAction = input.Actions.FindActionMap("Pointer", true).FindAction("PrimaryClick", true);
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
                Assert.That(clickAction.controls.Select(control => control.device).OfType<Mouse>().FirstOrDefault(),
                    Is.Not.Null);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint }
                    .WithButton(MouseButton.Left));
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
    }
}
