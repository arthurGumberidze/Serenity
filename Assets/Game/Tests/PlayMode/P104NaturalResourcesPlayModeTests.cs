using System.Collections;
using System.Linq;
using Game.Domain.Production;
using Game.Infrastructure;
using Game.Presentation.Interaction;
using Game.Presentation.Production;
using Game.Presentation.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P104")]
    public sealed class P104NaturalResourcesPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllVisibleResourceAffordancesAreCanonicalSelectableAndInspectable()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.Resources.All.Select(x => x.Id.ToString()), Is.EquivalentTo(new[]
            {
                "wood_log", "stone", "plant_fiber", "hide", "berries", "iron_ore", "gold_ore", "meat", "fish"
            }));
            Assert.That(root.Production.Sites.Where(x => x.Kind == ProductionSiteKind.NaturalSource)
                .All(x => x.SourceCapacity >= 20 && x.SourceCapacity <= 50), Is.True);

            var resourceObjects = root.PopulatedBiomeWorld.CanopyTrees
                .Concat(root.PopulatedBiomeWorld.MajorRocks)
                .Concat(root.PopulatedBiomeWorld.ResourceFormations.SelectMany(x => x.DecorativeSatellites))
                .ToArray();
            Assert.That(resourceObjects, Is.Not.Empty);
            foreach (var resourceObject in resourceObjects)
            {
                var presenter = resourceObject.GetComponent<ProductionSourcePresenter>();
                Assert.That(presenter, Is.Not.Null, resourceObject.name);
                Assert.That(presenter.IsBound, Is.True, resourceObject.name);
                Assert.That(resourceObject.GetComponentInChildren<Collider>(true), Is.Not.Null, resourceObject.name);
                var proxy = resourceObject.GetComponent<StableWorldHitProxy>();
                Assert.That(proxy, Is.Not.Null, resourceObject.name);
                Assert.That(proxy.EntityId, Is.EqualTo(presenter.Site.Id), resourceObject.name);
            }

            foreach (var zone in root.Industry.Zones)
            {
                var pile = Object.FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include)
                    .Single(x => x.PileId == zone.Id);
                Assert.That(pile.GetComponent<Collider>(), Is.Not.Null, zone.DefinitionId);
                Assert.That(pile.GetComponent<StableWorldHitProxy>()?.IsBound, Is.True, zone.DefinitionId);
                root.SelectionProbe.SelectWorldResourcePile(pile);
                Assert.That(root.SelectionProbe.SelectedWorldResourcePilePresenter, Is.SameAs(pile));
            }
            Assert.That(Object.FindAnyObjectByType<P104ResourceHoverLabel>(), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimalLakeAndIndustrialZonesHaveStableAppendOnlyIdentityAndSaveState()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.Production.Sites.Count(x => x.RecipeId == new ProductionRecipeId("hunt_animal")),
                Is.EqualTo(1));
            Assert.That(root.Production.Sites.Count(x => x.RecipeId == new ProductionRecipeId("fish_water")),
                Is.EqualTo(1));
            Assert.That(root.Industry.Zones.Count, Is.EqualTo(6));
            Assert.That(root.Industry.Zones.Select(x => x.Id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(root.Production.CaptureState().NaturalLifecycles.Count,
                Is.EqualTo(root.Production.Sites.Count(x => x.Kind == ProductionSiteKind.NaturalSource)));
            Assert.That(root.Production.CaptureState().Industry.Zones.Count, Is.EqualTo(6));

            root.PlayerSaves.Save(3);
            root.PlayerSaves.Load(3);
            Assert.That(root.Production.CaptureState().Industry.Zones.Count, Is.EqualTo(6));
            Assert.That(root.Production.CaptureState().NaturalLifecycles.Count,
                Is.EqualTo(root.Production.Sites.Count(x => x.Kind == ProductionSiteKind.NaturalSource)));
            yield return null;
        }
    }
}
