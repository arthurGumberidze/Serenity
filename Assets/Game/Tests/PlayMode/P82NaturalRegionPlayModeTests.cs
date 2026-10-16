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
        public IEnumerator StrategicViewKeepsAllSixPhysicalResourceFamiliesInCanonicalWorld()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.GlobalMapBridge.RequestEnterGlobal();
            var timeout = Time.realtimeSinceStartup + 15f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<Game.Presentation.Environment.P102ConnectedWorldPresentation>();
            Assert.That(controller, Is.Not.Null);
            var markers = root.PopulatedBiomeWorld.ResourceFormations;
            var required = new[] { "berries", "plant_fiber", "wood_log", "stone", "iron_ore", "gold_ore" };
            Assert.That(markers.Count, Is.EqualTo(24));
            Assert.That(required.All(id => root.Production.Sites.Any(x =>
                root.ProductionRecipes.Get(x.RecipeId).Outputs.Any(output => output.ResourceId.ToString() == id))), Is.True);
            foreach (var marker in markers)
            {
                Assert.That(marker.ClickableRoot.GetComponentInChildren<Collider>(true), Is.Not.Null);
            }
        }
    }
}
