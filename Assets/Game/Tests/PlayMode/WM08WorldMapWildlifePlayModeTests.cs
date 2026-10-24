using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.ECS.Tier2;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM08")]
    public sealed class WM08WorldMapWildlifePlayModeTests
    {
        [UnityTest]
        public IEnumerator EverySpeciesCanBePickedThroughItsActualPresentationBounds()
        {
            var catalog = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
            var host = new GameObject("wildlife picking test");
            var camera = host.AddComponent<Camera>();
            var raycaster = host.AddComponent<WorldPointerRaycaster>();
            raycaster.Configure(camera, ~0);
            var selection = host.AddComponent<SelectionProbe>();
            selection.Configure(null, raycaster);
            var index = 0;
            foreach (var entry in catalog.Entries)
            {
                var animal = new GameObject(entry.SpeciesId);
                animal.transform.localScale = Vector3.one * entry.Scale;
                WorldMapScenery.Instantiate(entry.Prefab, animal.transform);
                var view = animal.AddComponent<WorldMapWildlifeView>();
                view.Bind(DeterministicStableId.Create(15008, "pick", index++), entry.SpeciesId);
                var center = animal.GetComponent<Collider>().bounds.center;
                camera.transform.position = center + new Vector3(0, 3, -6);
                camera.transform.LookAt(center);
                yield return null;
                Physics.SyncTransforms();
                var point = camera.WorldToScreenPoint(center);
                Assert.That(selection.TrySelectAt(new Vector2(point.x, point.y), false), Is.True, entry.SpeciesId);
                Assert.That(selection.SelectedWildlife, Is.EqualTo(view), entry.SpeciesId);
                selection.ClearSelection();
                Object.Destroy(animal);
                yield return null;
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierOneTwoThreeTransitionsKeepOneCanonicalAnimalAndStableId()
        {
            const long seed = 20261023L;
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, seed);
            var plan = new WorldMapWildlifePlanner(world, heights, seed).Create();
            var service = new WildlifePopulationService(plan, seed);
            var catalog = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
            Assert.That(catalog, Is.Not.Null);

            var root = new GameObject("WM08 wildlife tier smoke");
            var presenter = root.AddComponent<WorldMapWildlifePresenter>();
            using (var tier2 = new Tier2Runtime("WM08 wildlife tier smoke"))
            {
                presenter.Initialize(service, tier2, catalog, world.PlayerStart);
                Assert.That(service.Plan.Definition.CanonicalPopulationTarget.Contains(service.Count), Is.True);
                Assert.That(presenter.Tier1ViewCount, Is.EqualTo(35));
                Assert.That(presenter.Tier2EntityCount, Is.EqualTo(80));
                Assert.That(presenter.Tier3Count, Is.EqualTo(service.Count - 115));
                Assert.That(service.Animals.Select(value => value.Id).Distinct().Count(), Is.EqualTo(service.Count));
                foreach (var animal in service.Animals)
                    Assert.That(presenter.ActiveProjectionCount(animal.Id),
                        Is.EqualTo(animal.Tier == CharacterSimulationTier.Tier3 ? 0 : 1), animal.Id.ToString());

                var id = presenter.Tier1Ids[0];
                presenter.Transition(id, CharacterSimulationTier.Tier2);
                Assert.That(service.Get(id).Id, Is.EqualTo(id));
                Assert.That(presenter.ActiveProjectionCount(id), Is.EqualTo(1));
                Assert.That(tier2.Wildlife.IsActive(id), Is.True);
                presenter.Transition(id, CharacterSimulationTier.Tier3);
                Assert.That(service.Get(id).Id, Is.EqualTo(id));
                Assert.That(presenter.ActiveProjectionCount(id), Is.Zero);
                presenter.Transition(id, CharacterSimulationTier.Tier1);
                Assert.That(service.Get(id).Id, Is.EqualTo(id));
                Assert.That(presenter.ActiveProjectionCount(id), Is.EqualTo(1));
                Assert.That(service.Plan.Definition.CanonicalPopulationTarget.Contains(service.Count), Is.True);
                presenter.Shutdown();
                Assert.That(tier2.Wildlife.EntityCount, Is.Zero);
            }
            Object.Destroy(root);
            yield return null;
        }
    }
}
