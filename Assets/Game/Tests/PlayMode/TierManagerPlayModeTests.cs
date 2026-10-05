using System;
using System.Collections;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [TestFixture]
    [Category("U13")]
    public sealed class TierManagerPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalRuntimeRoundTripPreservesCanonicalStateInventoryGroupJobAndPosition()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            root.AiRuntime.SetPaused(true);
            var character = root.MaleDemoCharacter;
            var id = character.Id;
            var before = character.CaptureState();
            Assert.That(root.CharacterPresentations.TryGet(id, out var originalPresenter), Is.True);
            var originalPresentationName = originalPresenter.gameObject.name;
            var inventoryOwner = new InventoryOwner(InventoryOwnerKind.Character, id);
            var inventory = root.Inventories.Get(inventoryOwner);
            var wood = new ResourceId("wood_log");
            var beforeWood = inventory.GetAmount(wood).Units;
            if (inventory.Capacity - inventory.TotalUnits >= 2)
                Assert.That(inventory.TryAdd(wood, new ResourceQuantity(2)), Is.True);
            var carriedWood = inventory.GetAmount(wood).Units;
            var group = root.Work.CreateGroup("U13 continuity", new[] { id });
            var job = root.Work.CreateMoveJob(id, new WorldPosition(0f, 0f, 8f), WorkPriority.Urgent, group.Id);
            for (var i = 0; i < 4 && job.Status != WorkJobStatus.Active; i++)
                root.AiScheduler.Advance(new GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.Zero));
            Assert.That(job.Status, Is.EqualTo(WorkJobStatus.Active));
            Assert.That(root.AiRuntime.TryGetMovement(id, out var movement), Is.True);
            Assert.That(movement.Agent.Warp(new Vector3(7f, 0f, 4f)), Is.True);
            var expectedPosition = movement.CurrentPosition;

            Assert.That(root.Tiers.GetTier(id), Is.EqualTo(CharacterSimulationTier.Tier1));
            root.Tiers.Transition(id, CharacterSimulationTier.Tier2);
            Assert.That(root.CharacterPresentations.TryGet(id, out _), Is.False);
            Assert.That(root.Tier2Runtime.Materializer.TryGetEntity(id, out _), Is.True);
            Assert.That(root.AiAgents.TryGet(id, out _), Is.False);
            Assert.That(job.Status, Is.EqualTo(WorkJobStatus.Assigned), "Tier 1-only path execution is requeued.");
            Assert.That(root.HaulClaims.TryGetForClaimant(id, out _), Is.False);
            Assert.That(root.Tiers.GetRuntimeState(id).Position, Is.EqualTo(expectedPosition));

            root.Tiers.Transition(id, CharacterSimulationTier.Tier3);
            Assert.That(root.Tier2Runtime.Materializer.TryGetEntity(id, out _), Is.False);
            Assert.That(root.Tier3Characters.TryGet(id, out var tier3), Is.True);
            Assert.That(tier3.State.Position, Is.EqualTo(expectedPosition));

            root.Tiers.Transition(id, CharacterSimulationTier.Tier1);
            Assert.That(root.Tiers.CountActiveRepresentations(id), Is.EqualTo(1));
            Assert.That(root.CharacterPresentations.TryGet(id, out var restoredPresenter), Is.True);
            Assert.That(restoredPresenter.Character, Is.SameAs(character));
            Assert.That(restoredPresenter.CharacterId, Is.EqualTo(id));
            Assert.That(restoredPresenter.gameObject.name, Is.EqualTo(originalPresentationName),
                "Tier round trips must reuse the deterministic presentation mapping.");
            Assert.That(restoredPresenter.transform.position.x, Is.EqualTo(expectedPosition.X).Within(0.05f));
            Assert.That(restoredPresenter.transform.position.z, Is.EqualTo(expectedPosition.Z).Within(0.05f));
            Assert.That(root.Inventories.Get(inventoryOwner), Is.SameAs(inventory));
            Assert.That(inventory.GetAmount(wood).Units, Is.EqualTo(carriedWood));
            Assert.That(carriedWood, Is.GreaterThanOrEqualTo(beforeWood));
            Assert.That(root.Work.Groups.TryGetForMember(id, out var restoredGroup), Is.True);
            Assert.That(restoredGroup, Is.SameAs(group));
            AssertCanonicalState(before, character.CaptureState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameTierRequestsDoNotCreateDuplicatePresenterOrEntity()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var id = root.FemaleDemoCharacter.Id;
            Assert.That(root.Tiers.Transition(id, CharacterSimulationTier.Tier1).Changed, Is.False);
            Assert.That(root.CharacterPresentations.Count, Is.EqualTo(2));
            root.Tiers.Transition(id, CharacterSimulationTier.Tier2);
            Assert.That(root.Tiers.Transition(id, CharacterSimulationTier.Tier2).Changed, Is.False);
            Assert.That(root.Tier2Runtime.Materializer.EntityCount, Is.EqualTo(1));
            root.Tiers.Transition(id, CharacterSimulationTier.Tier3);
            Assert.That(root.Tiers.Transition(id, CharacterSimulationTier.Tier3).Changed, Is.False);
            Assert.That(root.Tier3Characters.Count, Is.EqualTo(1));
            yield return null;
        }

        private static void AssertCanonicalState(CharacterState expected, CharacterState actual)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Sex, Is.EqualTo(expected.Sex));
            Assert.That(actual.Parents, Is.EquivalentTo(expected.Parents));
            Assert.That(actual.SpouseId, Is.EqualTo(expected.SpouseId));
            Assert.That(actual.FamilyId, Is.EqualTo(expected.FamilyId));
            Assert.That(actual.DynastyId, Is.EqualTo(expected.DynastyId));
            Assert.That(actual.Health, Is.EqualTo(expected.Health));
            Assert.That(actual.Traits, Is.EquivalentTo(expected.Traits));
            Assert.That(actual.Skills, Is.EquivalentTo(expected.Skills));
            Assert.That(actual.ProfessionId, Is.EqualTo(expected.ProfessionId));
            Assert.That(actual.Relationships, Is.EquivalentTo(expected.Relationships));
        }
    }
}
