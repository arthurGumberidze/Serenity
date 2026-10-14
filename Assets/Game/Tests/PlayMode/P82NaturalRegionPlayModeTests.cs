using System.Collections;
using System.Linq;
using Game.Infrastructure;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Presentation.Production;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P82")]
    public sealed class P82NaturalRegionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryLocalResourceCanBeSelectedAndMapsToCanonicalDomainState()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var presenters = Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                .Where(x => x.IsBound).OrderBy(x => x.Site.Id).ToArray();
            Assert.That(presenters, Has.Length.EqualTo(24));
            foreach (var presenter in presenters)
            {
                root.SelectionProbe.SelectProductionSource(presenter);
                Assert.That(root.SelectionProbe.SelectedProductionSourcePresenter, Is.SameAs(presenter));
                Assert.That(presenter.IsSelected, Is.True);
                Assert.That(root.Production.TryGetSite(presenter.Site.Id, out var site), Is.True);
                Assert.That(site, Is.SameAs(presenter.Site));
                Assert.That(presenter.GetComponentInChildren<Collider>(), Is.Not.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IronAndGoldStableIdentitySurvivesSaveLoad()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var anchors = root.InhabitableRegion.ResourceAnchors.Where(x => x.RecipeId == "gather_iron" || x.RecipeId == "gather_gold").ToArray();
            var ids = anchors.Select(x => DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                "player_source", x.StableIndex)).ToArray();
            root.PlayerSaves.Save(1);
            root.PlayerSaves.Load(1);
            foreach (var id in ids)
            {
                Assert.That(root.Production.TryGetSite(id, out var site), Is.True);
                var presenter = Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                    .Single(x => x.IsBound && x.Site.Id == id);
                Assert.That(presenter.Site, Is.SameAs(site));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalMapProjectsAndSelectsAllSixResourceFamilies()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.GlobalMapBridge.RequestEnterGlobal();
            var timeout = Time.realtimeSinceStartup + 15f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GlobalMapPresentationController>();
            Assert.That(controller, Is.Not.Null);
            var markers = controller.GetComponentsInChildren<GlobalMapPresentationController.GlobalResourceMarker>(true);
            var required = new[] { "berries", "plant_fiber", "wood_log", "stone", "iron_ore", "gold_ore" };
            Assert.That(markers, Has.Length.EqualTo(6));
            Assert.That(required.All(id => markers.Any(x => x.ResourceId.ToString() == id)), Is.True);
            foreach (var marker in markers)
            {
                controller.SelectResourceMarker(marker);
                Assert.That(controller.SelectedResourceMarker, Is.SameAs(marker));
                Assert.That(marker.SiteIds.All(id => root.Production.TryGetSite(id, out _)), Is.True);
                Assert.That(marker.GetComponent<Collider>(), Is.Not.Null);
            }
        }
    }
}
