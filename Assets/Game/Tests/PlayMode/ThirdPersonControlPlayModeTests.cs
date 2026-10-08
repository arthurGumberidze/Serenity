using System;
using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Control;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.AI;
using Game.Simulation.Control;
using Game.Simulation.Time;
using Game.Simulation.Tiers;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U22")]
    public sealed class ThirdPersonControlPlayModeTests
    {
        private LocalSceneCompositionRoot root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
            root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneStartsRtsWithOneReusableTpsRig()
        {
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.ThirdPersonControl.CameraRigCount, Is.EqualTo(1));
            Assert.That(root.ThirdPersonControl.SubscriptionCount, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<CinemachineCamera>().Length, Is.EqualTo(2));
            Assert.That(root.Input.RtsActionsEnabled, Is.True);
            Assert.That(root.Input.ThirdPersonActionsEnabled, Is.False);
            Assert.That(root.Input.GlobalTimeActionsEnabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterAndExitUseSameCharacterPresenterAndExclusiveMovementAuthority()
        {
            var character = root.MaleDemoCharacter;
            var presenter = root.DemoPresenters[0];
            root.SelectionProbe.SelectCharacters(new[] { presenter });
            var before = character.CaptureState();

            Assert.That(root.ThirdPersonControl.TryEnterSelected().IsEligible, Is.True);
            Assert.That(root.PlayerControl.State.ControlledCharacterId, Is.EqualTo(character.Id));
            Assert.That(root.ThirdPersonControl.MovementAuthority, Is.EqualTo(Tier1MovementAuthority.DirectPlayer));
            Assert.That(root.Input.RtsActionsEnabled, Is.False);
            Assert.That(root.Input.ThirdPersonActionsEnabled, Is.True);
            Assert.That(root.Input.GlobalTimeActionsEnabled, Is.True);
            Assert.That(root.CharacterPresentations.TryGet(character.Id, out var activePresenter), Is.True);
            Assert.That(activePresenter, Is.SameAs(presenter));

            root.ThirdPersonControl.Exit();
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.CharacterPresentations.TryGet(character.Id, out activePresenter), Is.True);
            Assert.That(activePresenter, Is.SameAs(presenter));
            Assert.That(character.CaptureState().Id, Is.EqualTo(before.Id));
            Assert.That(character.CaptureState().Health, Is.EqualTo(before.Health));
            Assert.That(root.ThirdPersonControl.MovementAuthority, Is.EqualTo(Tier1MovementAuthority.Autonomous));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidAndMultipleSelectionsAreRejectedExplicitly()
        {
            Assert.That(root.ThirdPersonControl.TryEnterSelected().IsEligible, Is.False);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters.Take(2));
            Assert.That(root.ThirdPersonControl.TryEnterSelected().IsEligible, Is.False);
            Assert.That(root.ThirdPersonControl.LastEntryMessage, Does.Contain("Multiple"));
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[1] });
            Assert.That(root.ThirdPersonControl.TryEnterSelected().Failure,
                Is.EqualTo(DirectControlEligibilityFailure.NotPlayerControlledRole));
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EligibilityRejectsDeadChildIncapacitatedAndEnemyCharacters()
        {
            Assert.That(root.PlayerControl.Evaluate(root.MaleDemoCharacter.Id).IsEligible, Is.True);
            Assert.That(root.PlayerControl.Evaluate(root.FirstDemoHeir.Id).Failure,
                Is.EqualTo(DirectControlEligibilityFailure.NotManaged));
            Assert.That(root.PlayerControl.Evaluate(root.FemaleDemoCharacter.Id).Failure,
                Is.EqualTo(DirectControlEligibilityFailure.NotPlayerControlledRole));

            var child = Character.CreateNew(new CharacterCreationData(new CharacterName("Young", "Stone"),
                CharacterSex.Female, root.Clock.State.BiologicalTicks,
                new[]
                {
                    new ParentLink(root.MaleDemoCharacter.Id, ParentRole.Father, ParentageKind.Biological, true),
                    new ParentLink(root.FemaleDemoCharacter.Id, ParentRole.Mother, ParentageKind.Biological, true)
                }, null, null, root.MaleDemoCharacter.DynastyId, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") }, null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
            root.Characters.Add(child);
            Assert.That(root.PlayerControl.Evaluate(child.Id).Failure,
                Is.EqualTo(DirectControlEligibilityFailure.Child));

            var calendarTick = root.Clock.State.CalendarTicks;
            var biologicalTick = root.Clock.State.BiologicalTicks;
            root.HealthFamily.ApplyCombatInjury(root.FemaleDemoCharacter.Id,
                new HealthConditionId("serious_wound"), calendarTick, biologicalTick, forceFatal: true);
            Assert.That(root.PlayerControl.Evaluate(root.FemaleDemoCharacter.Id).Failure,
                Is.EqualTo(DirectControlEligibilityFailure.Dead));

            root.HealthFamily.ApplyCombatInjury(root.MaleDemoCharacter.Id,
                new HealthConditionId("serious_wound"), calendarTick, biologicalTick);
            Assert.That(root.HealthFamily.GetCapabilities(root.MaleDemoCharacter.Id).IsIncapacitated, Is.True);
            Assert.That(root.PlayerControl.Evaluate(root.MaleDemoCharacter.Id).Failure,
                Is.EqualTo(DirectControlEligibilityFailure.Incapacitated));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MaterializableHeirAndPlayerCaptainUseExistingIdentities()
        {
            var heir = root.FirstDemoHeir;
            root.Tiers.Materialize(heir, CharacterSimulationTier.Tier3,
                new CharacterRuntimeState(heir.Id, new WorldPosition(12f, 0f, 12f), default, false, "local",
                    root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks));
            Assert.That(root.ThirdPersonControl.TryEnter(heir.Id).IsEligible, Is.True);
            Assert.That(root.Characters.Get(heir.Id), Is.SameAs(heir));
            Assert.That(root.CharacterPresentations.TryGet(heir.Id, out var heirPresenter), Is.True);
            Assert.That(heirPresenter.Character, Is.SameAs(heir));
            root.ThirdPersonControl.Exit();

            var captain = root.FemaleDemoCharacter;
            var unit = root.MilitaryUnits.Create(new MilitarySideId("player"), new[] { captain.Id }, captain.Id);
            Assert.That(root.ThirdPersonControl.TryEnter(captain.Id).IsEligible, Is.True);
            Assert.That(root.Characters.Get(captain.Id), Is.SameAs(captain));
            Assert.That(root.MilitaryUnits.Get(unit.Id).CaptainId, Is.EqualTo(captain.Id));
            Assert.That(root.MilitaryUnits.Get(unit.Id).ActiveMemberIds, Does.Contain(captain.Id));
            root.ThirdPersonControl.Exit();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OneHundredSwitchesDoNotDuplicateComponentsCallbacksOrState()
        {
            var presenter = root.DemoPresenters[0];
            var character = root.MaleDemoCharacter;
            var before = character.CaptureState();
            var presenterCount = root.CharacterPresentations.Count;
            for (var i = 0; i < 100; i++)
            {
                Assert.That(root.ThirdPersonControl.TryEnter(character.Id).IsEligible, Is.True, "enter " + i);
                root.ThirdPersonControl.Exit();
            }
            Assert.That(root.ThirdPersonControl.CameraRigCount, Is.EqualTo(1));
            Assert.That(root.ThirdPersonControl.SubscriptionCount, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<CinemachineCamera>().Length, Is.EqualTo(2));
            Assert.That(root.CharacterPresentations.Count, Is.EqualTo(presenterCount));
            Assert.That(root.CharacterPresentations.TryGet(character.Id, out var activePresenter), Is.True);
            Assert.That(activePresenter, Is.SameAs(presenter));
            Assert.That(character.CaptureState().Id, Is.EqualTo(before.Id));
            Assert.That(character.CaptureState().DynastyId, Is.EqualTo(before.DynastyId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RtsTpsRtsPreservesCanonicalStateAssignmentsMembershipAndOrder()
        {
            var character = root.MaleDemoCharacter;
            var characterBefore = character.CaptureState();
            var inventory = root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, character.Id));
            var inventoryBefore = inventory.CaptureState();
            var group = root.Work.CreateGroup("control-preservation", new[] { character.Id });
            var order = root.Work.CreateMoveJob(character.Id, new WorldPosition(4f, 0f, 4f),
                WorkPriority.High, group.Id);
            root.AiScheduler.Advance(new GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.Zero));
            Assert.That(root.Work.Get(order.Id).Status, Is.EqualTo(WorkJobStatus.Active));
            root.Research.AssignWorker(character.Id);
            var unit = root.MilitaryUnits.Create(new MilitarySideId("player"), new[] { character.Id }, character.Id);

            Assert.That(root.ThirdPersonControl.TryEnter(character.Id).IsEligible, Is.True);
            Assert.That(root.Work.Get(order.Id).Status, Is.EqualTo(WorkJobStatus.Assigned),
                "Suspending Utility AI must requeue, not lose, the active order.");
            root.ThirdPersonControl.Exit();

            Assert.That(root.Characters.Get(character.Id), Is.SameAs(character));
            Assert.That(character.CaptureState().Health, Is.EqualTo(characterBefore.Health));
            Assert.That(character.CaptureState().DynastyId, Is.EqualTo(characterBefore.DynastyId));
            Assert.That(character.CaptureState().Relationships, Is.EqualTo(characterBefore.Relationships));
            Assert.That(inventory.TotalUnits, Is.EqualTo(inventoryBefore.Amounts.Sum(x => x.Quantity.Units)));
            Assert.That(root.Research.IsAssigned(character.Id), Is.True);
            Assert.That(root.Work.Groups.Get(group.Id).Contains(character.Id), Is.True);
            Assert.That(root.Work.Get(order.Id).Status, Is.EqualTo(WorkJobStatus.Assigned));
            Assert.That(root.MilitaryUnits.Get(unit.Id).CaptainId, Is.EqualTo(character.Id));
            Assert.That(root.MilitaryUnits.Get(unit.Id).ActiveMemberIds, Does.Contain(character.Id));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveLoadRestoresValidControlAndFallsBackToRtsForInvalidIdentity()
        {
            var id = root.MaleDemoCharacter.Id;
            Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
            var snapshot = new Game.Domain.SaveSnapshot(Game.Domain.SaveSnapshot.CurrentVersion,
                Game.Domain.StableEntityId.NewId(), root.Clock.State, root.Clock.BiologicalMultiplier,
                playerControl: root.PlayerControl.CaptureState());
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));

            root.ThirdPersonControl.Exit();
            Assert.That(root.PlayerControl.RestoreState(restored.PlayerControl), Is.True);
            Assert.That(root.PlayerControl.State.ControlledCharacterId, Is.EqualTo(id));
            Assert.That(root.CharacterPresentations.TryGet(id, out var presenter), Is.True);
            Assert.That(root.ThirdPersonControl.ThirdPersonCamera.Follow.IsChildOf(presenter.transform), Is.True);
            Assert.That(root.ThirdPersonControl.CameraRigCount, Is.EqualTo(1));
            Assert.That(root.ThirdPersonControl.SubscriptionCount, Is.EqualTo(3));

            root.ThirdPersonControl.Exit();
            var invalid = new PlayerControlSaveState(PlayerControlMode.ThirdPerson, root.FemaleDemoCharacter.Id);
            Assert.That(root.PlayerControl.RestoreState(invalid), Is.False);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.PlayerControl.LastExitReason, Is.EqualTo(PlayerControlExitReason.InvalidAfterLoad));

            root.HealthFamily.ApplyCombatInjury(id, new HealthConditionId("serious_wound"),
                root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks, forceFatal: true);
            Assert.That(root.PlayerControl.RestoreState(restored.PlayerControl), Is.False);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.PlayerControl.LastExitReason, Is.EqualTo(PlayerControlExitReason.InvalidAfterLoad));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlledCharacterIncapacitationReturnsSafelyToRts()
        {
            var id = root.MaleDemoCharacter.Id;
            Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
            root.HealthFamily.ApplyCombatInjury(id, new HealthConditionId("serious_wound"),
                root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks);
            Assert.That(root.HealthFamily.GetCapabilities(id).IsIncapacitated, Is.True);
            root.ThirdPersonControl.Tick(0f);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.PlayerControl.LastExitReason, Is.EqualTo(PlayerControlExitReason.Incapacitated));
            Assert.That(root.ThirdPersonControl.MovementAuthority, Is.EqualTo(Tier1MovementAuthority.Autonomous));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlledCharacterIsTierOnePinnedUntilExit()
        {
            var id = root.MaleDemoCharacter.Id;
            Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
            Assert.That(root.Tiers.GetTier(id), Is.EqualTo(CharacterSimulationTier.Tier1));
            Assert.Throws<System.InvalidOperationException>(() =>
                root.Tiers.Transition(id, CharacterSimulationTier.Tier2));
            root.ThirdPersonControl.Exit();
            Assert.That(root.Tiers.Transition(id, CharacterSimulationTier.Tier2).Changed, Is.True);
            Assert.That(root.Tiers.Transition(id, CharacterSimulationTier.Tier1).Changed, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseStopsDirectMovementAndFasterSpeedUsesSameNavMeshAgent()
        {
            var id = root.MaleDemoCharacter.Id;
            Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
            Assert.That(root.AiRuntime.TryGetMovement(id, out var movement), Is.True);
            var agent = movement.Agent;
            var before = agent.transform.position;
            root.AiRuntime.SetPaused(true);
            movement.MoveDirect(Vector3.forward, 0.25f, false, root.Clock.IsPaused, 1, 1000);
            Assert.That(agent.transform.position, Is.EqualTo(before));
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.SetSpeed(GameSpeed.Faster);
            movement.MoveDirect(Vector3.forward, 0.25f, false, root.Clock.IsPaused, (int)root.Clock.Speed, 1000);
            yield return null;
            Assert.That(agent.transform.position.z, Is.GreaterThan(before.z));
            Assert.That(movement.Authority, Is.EqualTo(Tier1MovementAuthority.DirectPlayer));
            root.ThirdPersonControl.Exit();
        }

        [UnityTest]
        public IEnumerator ControlledMonarchDeathKeepsCombatCanonicalAndReturnsSafelyToRts()
        {
            var id = root.MaleDemoCharacter.Id;
            root.StartDemoCombat();
            Assert.That(root.Combat.TryGetCombatant(id, out _), Is.True);
            Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
            Assert.That(root.AiRuntime.TryGetMovement(id, out var movement), Is.True);
            Assert.That(movement.Agent.hasPath, Is.False);
            var calendarTick = root.Clock.State.CalendarTicks;
            var biologicalTick = root.Clock.State.BiologicalTicks;
            root.Combat.AdvanceTo(calendarTick, biologicalTick);
            Assert.That(movement.Agent.hasPath, Is.False,
                "U20 combat may keep attacking but must not acquire movement authority while TPS owns it.");
            root.HealthFamily.ApplyCombatInjury(id, new HealthConditionId("serious_wound"), calendarTick,
                biologicalTick,
                forceFatal: true);
            root.Combat.AdvanceTo(calendarTick, biologicalTick);
            root.ThirdPersonControl.Tick(0f);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(root.MaleDemoCharacter.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(root.PlayerRealm.CurrentRulerId, Is.Not.EqualTo(id));
            Assert.That(root.Combat.TryGetLatestCombatant(id, out var combatant), Is.True);
            Assert.That(combatant.State, Is.EqualTo(Game.Domain.Combat.CombatantState.Dead));
            yield return null;
        }
    }
}
