using System.Collections;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class P109CompactWorldPlayModeTests
    {
        [UnityTest]
        public IEnumerator EveryLakeSchoolHasItsOwnHitTargetAndNavigableDryBank()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Clock.Pause();
            var plan = root.WorldPopulation.Plan.Resources.Where(x => x.RecipeId == "fish_water").ToArray();
            Assert.That(plan.Length, Is.EqualTo(44));
            Assert.That(root.GetComponent<WorksitePresentation>().FishSchoolCount, Is.EqualTo(44));
            Physics.SyncTransforms();
            var views = Object.FindObjectsByType<ProductionSourcePresenter>();
            foreach (var node in plan)
            {
                var id = root.WorldPopulation.ResourceId(node);
                var view = views.Single(x => x.IsBound && x.Site.Id == id);
                Assert.That(view.GetComponentsInChildren<Transform>().Count(x => x.name.StartsWith("Swimming fish ")), Is.EqualTo(10));
                var hits = Physics.RaycastAll(view.transform.position + Vector3.up * 10,
                    Vector3.down, 20, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                Assert.That(hits.Any(x => x.collider.GetComponentInParent<ProductionSourcePresenter>() == view), Is.True, node.SourceZoneId);
                var bank = new Vector3(node.Position.X, node.Position.Y, node.Position.Z);
                if (!NavMesh.SamplePosition(bank, out _, 2f, NavMesh.AllAreas))
                {
                    var found = NavMesh.SamplePosition(bank, out var nearest, 100f, NavMesh.AllAreas);
                    Debug.Log("Fish bank navigation diagnostic: " + node.SourceZoneId + " bank=" + bank +
                        " nearest=" + (found ? nearest.position.ToString() : "none within 100m"));
                }
                Assert.That(NavMesh.SamplePosition(bank, out var hit, 2f, NavMesh.AllAreas), Is.True,
                    node.SourceZoneId + " " + bank);
                Assert.That(root.WorldHeight.IsWater(hit.position.x, hit.position.z), Is.False, node.SourceZoneId);
            }
            Assert.That(root.TiledWorld.LoadedCollisionTileCount, Is.EqualTo(16));
        }
    }
}
