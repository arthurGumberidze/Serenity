using System.Collections;
using System.Linq;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P109")]
    public sealed class P109FunctionalIntegrationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StarterNaturalSourcesOfferReachablePhysicalCommands()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var resident = Object.FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude)
                .Where(x => x.IsBound).OrderBy(x => x.CharacterId).First();
            root.SelectionProbe.SelectCharacters(new[] { resident });

            var sources = root.PopulatedBiomeWorld.ResourceFormations.OrderBy(x => x.StableIndex).Take(4).ToArray();
            Assert.That(sources, Has.Length.EqualTo(4));
            var enabledCommands = 0;
            var berryCommandEnabled = false;
            foreach (var source in sources)
            {
                var site = source.ClickableRoot.GetComponent<Game.Presentation.Production.ProductionSourcePresenter>().Site;
                Assert.That(Game.Presentation.AI.ReachableNavMeshPoint.TryFind(resident.transform.position,
                    source.ClickableRoot.position, out var destination), Is.True, site.Id.ToString());
                Assert.That(Vector3.Distance(destination, source.ClickableRoot.position), Is.LessThanOrEqualTo(3.1f));
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(resident.transform.position, destination,
                    NavMesh.AllAreas, path), Is.True, site.Id.ToString());
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), site.Id.ToString());

                var output = root.Production.Recipes.Get(site.RecipeId).Outputs[0].ResourceId;
                var resourceClass = output.ToString() == "wood_log" ? ContextResourceClass.Choppable :
                    output.ToString() == "stone" || output.ToString() == "iron_ore" || output.ToString() == "gold_ore"
                        ? ContextResourceClass.Mineable : ContextResourceClass.Gatherable;
                var action = resourceClass == ContextResourceClass.Choppable ? ContextActionKind.Chop :
                    resourceClass == ContextResourceClass.Mineable ? ContextActionKind.Mine : ContextActionKind.Gather;
                root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,
                    site.Position, site.Id, output, resourceClass), new Vector2(640f, 360f));
                yield return null;
                var state = root.GameplayUi.ContextActionStates[action];
                if (state.Enabled) enabledCommands++;
                else Assert.That(state.Reason, Is.EqualTo(ContextUnavailableReason.MissingTechnology),
                    site.Id + ": " + state.Reason);
                if (output.ToString() == "berries") berryCommandEnabled = state.Enabled;
            }
            Assert.That(enabledCommands, Is.GreaterThanOrEqualTo(1));
            Assert.That(berryCommandEnabled, Is.True);
        }
    }
}
