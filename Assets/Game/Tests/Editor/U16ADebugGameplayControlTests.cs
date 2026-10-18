using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Presentation.Buildings;
using Game.Presentation.Input;
using Game.Simulation.Production;
using Game.Simulation.Research;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Editor
{
    [Category("U16A")]
    public sealed class U16ADebugGameplayControlTests
    {
        private static readonly StableEntityId WorkerA = StableEntityId.Parse("000000000000000000000000000000a1");
        private static readonly StableEntityId WorkerB = StableEntityId.Parse("000000000000000000000000000000b2");

        [Test]
        public void AssignSelectedResearchTests_AssignIsIdempotentAndUnassigns()
        {
            var research = CreateResearch(out _);

            Assert.That(research.AssignWorkers(new[] { WorkerB, WorkerA, WorkerA }), Is.EqualTo(2));
            Assert.That(research.AssignWorkers(new[] { WorkerA, WorkerB }), Is.Zero);
            Assert.That(research.AssignedWorkers, Is.EqualTo(new[] { WorkerA, WorkerB }));
            Assert.That(research.UnassignWorkers(new[] { WorkerA, WorkerA }), Is.EqualTo(1));
            Assert.That(research.AssignedWorkers, Is.EqualTo(new[] { WorkerB }));
        }

        [Test]
        public void ResearchWorkerStatusTests_OnlyAssignedResearchingAiIsActive()
        {
            var research = CreateResearch(out _);
            research.AssignWorkers(new[] { WorkerA, WorkerB });
            Assert.That(research.Start(new TechnologyId("controlled_fire")), Is.True);
            var agents = new Tier1AiAgentRegistry();
            var idle = new Tier1AiAgentState(WorkerA, default);
            var researching = new Tier1AiAgentState(WorkerB, default);
            researching.SetAction(UtilityActionKind.Research, AiActionPhase.Researching);
            agents.Add(idle);
            agents.Add(researching);

            var status = ResearchWorkerStatusQuery.Capture(research, agents);

            Assert.That(status.AssignedCount, Is.EqualTo(2));
            Assert.That(status.ActiveCount, Is.EqualTo(1));
            Assert.That(status.ActiveWorkerIds, Is.EqualTo(new[] { WorkerB }));
            Assert.That(status.ActiveCount, Is.LessThanOrEqualTo(status.AssignedCount));
        }

        [Test]
        public void BuildingSelectorTests_UsesCanonicalCatalogLocksAndUnlockReasons()
        {
            var research = CreateResearch(out var catalog);
            research.AssignWorker(WorkerA);

            var initial = BuildingDebugCatalogView.Capture(catalog, research);

            CollectionAssert.AreEquivalent(new[] { "primitive_shelter", "storage_basket", "primitive_crop_plot", "military_barracks" },
                initial.Select(option => option.Definition.Id.ToString()));
            var shelter = initial.Single(option => option.Definition.Id == new BuildingDefinitionId("primitive_shelter"));
            Assert.That(shelter.IsAvailable,
                Is.EqualTo(research.IsBuildingUnlocked(new BuildingDefinitionId("primitive_shelter"))));
            var crop = initial.Single(option => option.Definition.Id == new BuildingDefinitionId("primitive_crop_plot"));
            Assert.That(crop.IsAvailable, Is.False);
            Assert.That(crop.LockReason, Does.Contain("Примитивное земледелие"));

            Complete(research, "controlled_fire");
            Complete(research, "fiber_working");
            Complete(research, "stone_tools");
            Complete(research, "primitive_agriculture");
            crop = BuildingDebugCatalogView.Capture(catalog, research)
                .Single(option => option.Definition.Id == new BuildingDefinitionId("primitive_crop_plot"));
            Assert.That(crop.IsAvailable, Is.True);
            Assert.That(crop.LockReason, Is.Empty);
        }

        [Test]
        public void TimeControlTests_StepSequenceCoversEverySupportedSpeedWithoutDigitHotkeys()
        {
            Assert.That(TimeDebugOverlay.SupportedSpeeds.Select(speed => (int)speed),
                Is.EqualTo(new[] { 1, 2, 3, 5, 10 }));
            Assert.That(TimeDebugOverlay.NextSpeed(Game.Simulation.Time.GameSpeed.Normal),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.Fast));
            Assert.That(TimeDebugOverlay.NextSpeed(Game.Simulation.Time.GameSpeed.Fast),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.Faster));
            Assert.That(TimeDebugOverlay.NextSpeed(Game.Simulation.Time.GameSpeed.Faster),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.VeryFast));
            Assert.That(TimeDebugOverlay.NextSpeed(Game.Simulation.Time.GameSpeed.VeryFast),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.Maximum));
            Assert.That(TimeDebugOverlay.NextSpeed(Game.Simulation.Time.GameSpeed.Maximum),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.Maximum));
            Assert.That(TimeDebugOverlay.PreviousSpeed(Game.Simulation.Time.GameSpeed.Normal),
                Is.EqualTo(Game.Simulation.Time.GameSpeed.Normal));
        }

        private static ResearchService CreateResearch(out BuildingPresentationCatalog buildingCatalog)
        {
            buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            Assert.That(buildingCatalog, Is.Not.Null);
            var resources = buildingCatalog.ResourceCatalog.CreateCatalog();
            var production = ProductionCatalog.CreateStoneAge(resources);
            var technologies = TechnologyCatalog.CreateStoneAge(production,
                buildingCatalog.Entries.Select(entry => new BuildingDefinitionId(entry.DefinitionId)));
            return new ResearchService(technologies);
        }

        private static void Complete(ResearchService research, string technologyId)
        {
            var id = new TechnologyId(technologyId);
            Assert.That(research.Start(id), Is.True, technologyId + " must be available.");
            research.AdvanceWorkerTo(WorkerA,
                checked(research.LastProcessedTick + research.Catalog.Get(id).ResearchWorkRequiredTicks), true);
            Assert.That(research.IsCompleted(id), Is.True);
        }
    }
}
