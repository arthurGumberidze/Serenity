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

            var resourceObjects = Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Where(x => x.name.StartsWith("P10.9 ")).ToArray();
            Assert.That(resourceObjects, Is.Not.Empty);
            Assert.That(resourceObjects.Select(x => x.Site.Id).Distinct().Count(), Is.EqualTo(resourceObjects.Length));
            foreach (var presenter in resourceObjects)
            {
                Assert.That(presenter.IsBound, Is.True, presenter.name);
                Assert.That(presenter.GetComponentInChildren<Collider>(true), Is.Not.Null, presenter.name);
                var proxy = presenter.GetComponent<StableWorldHitProxy>();
                Assert.That(proxy, Is.Not.Null, presenter.name);
                Assert.That(proxy.EntityId, Is.EqualTo(presenter.Site.Id), presenter.name);
            }

            foreach (var pile in Object.FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None).Where(x => x.name.Contains("p109_world_industry")))
            {
                Assert.That(pile.GetComponent<Collider>(), Is.Not.Null, pile.name);
                Assert.That(pile.GetComponent<StableWorldHitProxy>()?.IsBound, Is.True, pile.name);
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
                Is.GreaterThan(1));
            Assert.That(root.Production.Sites.Count(x => x.RecipeId == new ProductionRecipeId("fish_water")),
                Is.GreaterThan(1));
            Assert.That(root.Industry.Zones.Count, Is.EqualTo(6 + root.WorldPopulation.Plan.Industry.Count));
            Assert.That(root.Industry.Zones.Select(x => x.Id).Distinct().Count(), Is.EqualTo(root.Industry.Zones.Count));
            Assert.That(root.Production.CaptureState().NaturalLifecycles.Count,
                Is.EqualTo(root.Production.Sites.Count(x => x.Kind == ProductionSiteKind.NaturalSource)));
            Assert.That(root.Production.CaptureState().Industry.Zones.Count, Is.EqualTo(root.Industry.Zones.Count));

            root.PlayerSaves.Save(3);
            root.PlayerSaves.Load(3);
            Assert.That(root.Production.CaptureState().Industry.Zones.Count, Is.EqualTo(root.Industry.Zones.Count));
            Assert.That(root.Production.CaptureState().NaturalLifecycles.Count,
                Is.EqualTo(root.Production.Sites.Count(x => x.Kind == ProductionSiteKind.NaturalSource)));
            yield return null;
        }
    }
}
