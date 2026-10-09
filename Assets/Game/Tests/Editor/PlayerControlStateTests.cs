using System;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Control;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Control;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U22")]
    public sealed class PlayerControlStateTests
    {
        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        [Test]
        public void StartsInRtsAndRoundTripKeepsControlledStableId()
        {
            var state = new PlayerControlState();
            Assert.That(state.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(state.ControlledCharacterId, Is.Null);
            state.EnterThirdPerson(Id(1));
            var saved = state.CaptureState();
            var restored = new PlayerControlState();
            restored.Restore(saved);
            Assert.That(restored.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));
            Assert.That(restored.ControlledCharacterId, Is.EqualTo(Id(1)));
            restored.ReturnToRts();
            Assert.That(restored.ControlledCharacterId, Is.Null);
        }

        [Test]
        public void InvalidModeAndIdentityCombinationsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new PlayerControlSaveState(PlayerControlMode.Rts, Id(1)));
            Assert.Throws<ArgumentException>(() => new PlayerControlSaveState(PlayerControlMode.ThirdPerson, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerControlSaveState((PlayerControlMode)99, null));
        }

        [Test]
        public void VersionNineXmlRoundTripPersistsModeAndIdentity()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.PlayerControlVersion, Id(2), new GameTimeState(), 400,
                playerControl: new PlayerControlSaveState(PlayerControlMode.ThirdPerson, Id(3)));
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(9));
            Assert.That(restored.PlayerControl.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));
            Assert.That(restored.PlayerControl.ControlledCharacterId, Is.EqualTo(Id(3)));
        }

        [Test]
        public void LegacyVersionEightRemainsReadableWithoutControlState()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.MilitaryVersion, Id(4), new GameTimeState(), 400);
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(8));
            Assert.That(restored.PlayerControl, Is.Null);
        }

        [Test]
        public void ActivityOwnerSetUsesAnyOwner()
        {
            var id = Id(5);
            var owners = new CharacterActivityOwnerSet(new Owner(false), new Owner(true));
            Assert.That(owners.IsExternallyControlled(id), Is.True);
        }

        [Test]
        public void TierGuardSetRequiresEveryGuard()
        {
            var id = Id(6);
            var guards = new CharacterTierTransitionGuardSet(new Guard(true), new Guard(false));
            Assert.That(guards.CanTransition(id, CharacterSimulationTier.Tier2), Is.False);
            Assert.That(new CharacterTierTransitionGuardSet(new Guard(true), new Guard(true))
                .CanTransition(id, CharacterSimulationTier.Tier2), Is.True);
        }

        [Test]
        public void SnapshotCurrentVersionAdvancesBeyondPlayerControlVersion()
        {
            Assert.That(SaveSnapshot.PlayerControlVersion, Is.EqualTo(9));
            Assert.That(SaveSnapshot.BattleVersion, Is.EqualTo(10));
            Assert.That(SaveSnapshot.BuildingVersion, Is.EqualTo(11));
            Assert.That(SaveSnapshot.GlobalMapVersion, Is.EqualTo(12));
            Assert.That(SaveSnapshot.StrategicAiVersion, Is.EqualTo(13));
            Assert.That(SaveSnapshot.EventVersion, Is.EqualTo(14));
            Assert.That(SaveSnapshot.CurrentVersion, Is.EqualTo(SaveSnapshot.EventVersion));
        }

        private sealed class Owner : IExternalCharacterActivityOwner
        {
            private readonly bool value;
            public Owner(bool controlled) => value = controlled;
            public bool IsExternallyControlled(StableEntityId characterId) => value;
        }

        private sealed class Guard : ICharacterTierTransitionGuard
        {
            private readonly bool value;
            public Guard(bool allowed) => value = allowed;
            public bool CanTransition(StableEntityId characterId, CharacterSimulationTier targetTier) => value;
        }
    }
}
