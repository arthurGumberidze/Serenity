using System;
using System.Collections;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U15")]
    public sealed class ProductionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tier1WalksWorksAndCommitsOneCanonicalProductionOutput()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Production, Is.Not.Null);
            Assert.That(root.ProductionRecipes.All.Count, Is.EqualTo(5));
            Assert.That(root.Production.Sites.Count, Is.EqualTo(5));
            Assert.That(root.DemoCropPlotBuilding.DefinitionId.ToString(), Is.EqualTo("primitive_crop_plot"));
            Assert.That(root.DemoCropPlotBuilding.ConstructionState, Is.EqualTo(Game.Domain.Buildings.ConstructionState.Completed));
            Assert.That(root.DemoCropPlotPresenter.IsBound, Is.True);
            Assert.That(root.DemoCropPlotPresenter.GetComponent<Game.Presentation.Production.ProductionSourcePresenter>(), Is.Not.Null);

            var initialRemaining = root.Production.Sites.Where(x => x.Kind == ProductionSiteKind.NaturalSource)
                .Sum(x => x.RemainingAmount);
            var initialPositions = root.DemoPresenters.Select(x => x.transform.position).ToArray();
            var sawProductionAction = false;
            var sawWorking = false;
            var moved = false;
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
            var timeout = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < timeout &&
                   root.Resources.All.Sum(x => root.Production.GetProducedTotal(x.Id)) == 0)
            {
                foreach (var agent in root.AiAgents.All)
                {
                    sawProductionAction |= agent.CurrentAction == Game.Domain.AI.UtilityActionKind.Produce;
                    sawWorking |= agent.Phase == Game.Domain.AI.AiActionPhase.Working;
                }
                for (var i = 0; i < initialPositions.Length; i++)
                    moved |= Vector3.Distance(initialPositions[i], root.DemoPresenters[i].transform.position) > 0.4f;
                if (sawWorking && root.Production.ActiveTasks.Count > 0)
                    root.Production.AdvanceTask(root.Production.ActiveTasks[0].Id, TimeSpan.FromDays(1));
                yield return null;
            }

            var produced = root.Resources.All.Sum(x => root.Production.GetProducedTotal(x.Id));
            Assert.That(sawProductionAction, Is.True, "No Tier 1 worker selected a production task.");
            Assert.That(sawWorking, Is.True, "No Tier 1 worker reached the source and entered simulation-owned work.");
            Assert.That(moved, Is.True, "The Tier 1 producer never traversed the existing NavMesh bridge.");
            Assert.That(produced, Is.GreaterThan(0), "No declared recipe output was committed.");
            Assert.That(root.Production.Sites.Where(x => x.Kind == ProductionSiteKind.NaturalSource)
                .Sum(x => x.RemainingAmount), Is.LessThan(initialRemaining));
            foreach (var definition in root.Resources.All)
                Assert.That(root.Production.GetCanonicalTotal(definition.Id),
                    Is.EqualTo(root.Inventories.All.Sum(x => x.GetAmount(definition.Id).Units)));
        }

        [UnityTest]
        public IEnumerator Tier2AndTier3UseBatchedLogicalProductionWithoutAddingGameObjects()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var first = root.MaleDemoCharacter.Id;
            var second = root.FemaleDemoCharacter.Id;
            root.Tiers.Transition(first, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(second, CharacterSimulationTier.Tier3);
            var gameObjectsBefore = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            root.Clock.State.CalendarTicks = TimeSpan.FromDays(1).Ticks;
            root.Production.AdvanceTo(TimeSpan.FromDays(1).Ticks, new[] { second, first });
            var gameObjectsAfter = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;

            Assert.That(root.Production.GetProducedTotal(new ResourceId("berries")) +
                root.Production.GetProducedTotal(new ResourceId("stone")), Is.GreaterThan(0));
            Assert.That(gameObjectsAfter, Is.EqualTo(gameObjectsBefore));
            Assert.That(root.Tiers.GetTier(first), Is.EqualTo(CharacterSimulationTier.Tier2));
            Assert.That(root.Tiers.GetTier(second), Is.EqualTo(CharacterSimulationTier.Tier3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CropPlotPlacementUsesU08OccupancyConstructionAndActivation()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var beforeSites = root.Production.Sites.Count;
            root.BuildingPlacementController.StartPlacement(new Game.Domain.Buildings.BuildingDefinitionId("primitive_crop_plot"));
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(15f, 0f, 15f)), Is.True);
            var building = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            Assert.That(building.ConstructionState, Is.EqualTo(Game.Domain.Buildings.ConstructionState.Completed));
            Assert.That(root.BuildingOccupancy.IsOccupied(building.Coordinate), Is.True);
            Assert.That(root.Production.TryGetSite(building.Id, out var site), Is.True);
            Assert.That(site.Kind, Is.EqualTo(ProductionSiteKind.PrimitiveCropPlot));
            Assert.That(root.Production.Sites.Count, Is.EqualTo(beforeSites + 1));
            yield return null;
        }
    }
}
