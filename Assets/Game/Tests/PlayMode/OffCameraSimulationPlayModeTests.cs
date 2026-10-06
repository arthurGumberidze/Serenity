using System;
using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.Tiers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [TestFixture]
    [Category("U14")]
    public sealed class OffCameraSimulationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tier3CatchUpReturnsLatestStateWithoutChangingCanonicalOwnership()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            root.AiRuntime.SetPaused(true);
            var character = root.MaleDemoCharacter;
            var id = character.Id;
            var canonicalBefore = character.CaptureState();
            var owner = new InventoryOwner(InventoryOwnerKind.Character, id);
            var inventory = root.Inventories.Get(owner);
            var wood = new ResourceId("wood_log");
            var woodBefore = inventory.GetAmount(wood);
            var group = root.Work.CreateGroup("U14 continuity", new[] { id });
            var order = root.Work.CreateMoveJob(id, new WorldPosition(8f, 0f, 8f), WorkPriority.High, group.Id);

            root.Tiers.Transition(id, CharacterSimulationTier.Tier3);
            var needsBefore = root.Tier3Characters.Get(id).State;
            root.Clock.Resume();
            root.Clock.Advance(TimeSpan.FromMinutes(240)); // ten calendar days at x1
            root.Tier3Adapter.AdvanceAllToCurrent();
            var remote = root.Tier3Characters.Get(id).State;
            Assert.That(remote.OffCameraStepCount, Is.EqualTo(10UL));
            Assert.That(remote.AbstractActivityProgress, Is.GreaterThan(0));
            Assert.That(remote.Hunger, Is.EqualTo(needsBefore.Hunger), "Tier 3 does not run Tier 1 Utility AI.");
            Assert.That(remote.Energy, Is.EqualTo(needsBefore.Energy));

            root.Tiers.Transition(id, CharacterSimulationTier.Tier1);
            var restored = root.Tiers.GetRuntimeState(id);
            Assert.That(restored.LastSimulationCalendarTick, Is.EqualTo(root.Clock.State.CalendarTicks));
            Assert.That(restored.OffCameraStepCount, Is.EqualTo(remote.OffCameraStepCount));
            Assert.That(character.CaptureState().Health, Is.EqualTo(canonicalBefore.Health));
            Assert.That(character.CaptureState().Parents, Is.EquivalentTo(canonicalBefore.Parents));
            Assert.That(root.Inventories.Get(owner), Is.SameAs(inventory));
            Assert.That(inventory.GetAmount(wood), Is.EqualTo(woodBefore));
            Assert.That(root.Work.Groups.TryGetForMember(id, out var restoredGroup), Is.True);
            Assert.That(restoredGroup, Is.SameAs(group));
            Assert.That(order.Status, Is.EqualTo(WorkJobStatus.Assigned));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThousandTier3RecordsAddNoGameObjectsEntitiesOrPerCharacterBehaviours()
        {
            var before = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            var records = Enumerable.Range(1, 1000).Select(i => new Tier3CharacterRecord(
                new CharacterRuntimeState(Game.Domain.StableEntityId.Parse(i.ToString("x32")),
                    default, default, false))).ToArray();
            var simulation = new OffCameraSimulationService(OffCameraSimulationSettings.ForWorld(7714));
            var result = simulation.Advance(records, new SimulationTimePoint(
                TimeSpan.FromDays(30).Ticks, TimeSpan.FromDays(12000).Ticks));
            var after = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;

            Assert.That(result.ProcessedEntities, Is.EqualTo(1000));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(records.All(x => x.GetType().BaseType == typeof(object)), Is.True);
            Assert.That(records.Select(x => x.CharacterId).Distinct().Count(), Is.EqualTo(1000));
            yield return null;
        }
    }
}
