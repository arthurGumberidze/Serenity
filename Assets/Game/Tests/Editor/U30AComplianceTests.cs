using System.Linq;
using System.Text;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.Campaign;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U30A")]
    [Category("U31")]
    public sealed class U30AComplianceTests
    {
        [Test]
        public void NewGameBootstrapCreatesExactlyTenStablePlayerCharactersAndValidDynasty()
        {
            var first = NewGameBootstrap.Create(new NewGameConfiguration(301001));
            var replay = NewGameBootstrap.Create(new NewGameConfiguration(301001));
            var other = NewGameBootstrap.Create(new NewGameConfiguration(301002));

            Assert.That(first.PlayerCharacters.Count, Is.EqualTo(10));
            Assert.That(first.PlayerCharacters.Select(x => x.Id).Distinct().Count(), Is.EqualTo(10));
            Assert.That(replay.PlayerCharacters.Select(x => x.Id),
                Is.EqualTo(first.PlayerCharacters.Select(x => x.Id)));
            Assert.That(other.PlayerCharacters.Select(x => x.Id),
                Is.Not.EqualTo(first.PlayerCharacters.Select(x => x.Id)));
            Assert.That(first.Dynasty.CurrentMonarchId, Is.EqualTo(first.MonarchId));
            Assert.That(first.Dynasty.MemberIds, Does.Contain(first.FirstHeirId));
            Assert.That(first.Dynasty.MemberIds, Does.Contain(first.SecondHeirId));
            Assert.That(first.Configuration.SessionId.IsValid, Is.True);
        }

        [Test]
        public void V15RoundTripPreservesCampaignAndConservedSupplyAuditState()
        {
            var unitId = new MilitaryUnitId(Id(301010));
            var memberA = Id(301011);
            var memberB = Id(301012);
            var supply = new MilitarySupplyState(
                new[] { new ResourceAmount(new ResourceId("berries"), new ResourceQuantity(7)) },
                new[] { new MilitarySupplyCheckpointState(MilitarySupplyCheckpointKind.Travel, 50, "travel:route-a") },
                5, true);
            var unit = new MilitaryUnitState(unitId, new MilitarySideId("player"), memberA,
                new[]
                {
                    new MilitaryUnitMemberState(memberA, MilitaryUnitMemberRole.Soldier,
                        MilitaryUnitMemberStatus.Active, 1),
                    new MilitaryUnitMemberState(memberB, MilitaryUnitMemberRole.Soldier,
                        MilitaryUnitMemberStatus.Active, 2)
                }, MilitaryUnitStatus.Ready, MilitaryUnitRepresentationMode.Aggregate, "enemy_settlement",
                925, 1000, MilitaryUnitOrderState.Idle(), null, 4, supply);
            var campaign = new CampaignProgressSaveState(301001, NewGameConfiguration.DefaultConfigurationId,
                new CombatSideId("player"), new Game.Domain.AI.AiStateId(Id(301013)),
                new Game.Domain.World.SettlementId(Id(301014)), true, 40, true, 80,
                new Game.Domain.AI.AiStateId(Id(301013)), new Game.Domain.World.SettlementId(Id(301014)),
                Id(301015), 2);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(301016),
                new GameTimeState(100, 200, 2, false), 400,
                military: new MilitaryUnitSaveState(new[] { unit }), campaign: campaign);

            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            var restored = decoded.Military.Units.Single();

            Assert.That(decoded.FormatVersion, Is.EqualTo(SaveSnapshot.CampaignVersion));
            Assert.That(restored.Supply.Initialized, Is.True);
            Assert.That(restored.Supply.Get(new ResourceId("berries")), Is.EqualTo(7));
            Assert.That(restored.Supply.ConsumedUnits, Is.EqualTo(5));
            Assert.That(restored.Supply.Checkpoints.Single().Key, Is.EqualTo("travel:route-a"));
            Assert.That(decoded.Campaign.EventSequence, Is.EqualTo(2));
            Assert.That(decoded.Campaign.MilitaryVictory, Is.True);
            Assert.That(decoded.Campaign.SourceBattleId, Is.EqualTo(Id(301015)));
        }

        [Test]
        public void V14LoadsWithSafeLegacyCampaignAndSupplyDefaults()
        {
            var member = Id(301021);
            var unit = new MilitaryUnitState(new MilitaryUnitId(Id(301020)), new MilitarySideId("player"), member,
                new[] { new MilitaryUnitMemberState(member, MilitaryUnitMemberRole.Soldier,
                    MilitaryUnitMemberStatus.Active, 1) }, MilitaryUnitStatus.Ready,
                MilitaryUnitRepresentationMode.Detailed, "local", 1000, 1000,
                MilitaryUnitOrderState.Idle(), null, 2,
                new MilitarySupplyState(new[]
                {
                    new ResourceAmount(new ResourceId("berries"), new ResourceQuantity(6))
                }, initialized: true));
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(new SaveSnapshot(SaveSnapshot.EventVersion,
                Id(301022), new GameTimeState(), 400,
                military: new MilitaryUnitSaveState(new[] { unit }))));

            Assert.That(decoded.Campaign, Is.Null);
            Assert.That(decoded.Military.Units.Single().Supply.Initialized, Is.False);
            Assert.That(decoded.Military.Units.Single().Supply.Amounts, Is.Empty);
        }

        [Test]
        public void InvalidCampaignAuditMarkersFailClosed()
        {
            Assert.Throws<System.ArgumentException>(() => new CampaignProgressSaveState(1,
                NewGameConfiguration.DefaultConfigurationId, new CombatSideId("player"),
                new Game.Domain.AI.AiStateId(Id(301030)), new Game.Domain.World.SettlementId(Id(301031)),
                false, null, true, 1, new Game.Domain.AI.AiStateId(Id(301030)),
                new Game.Domain.World.SettlementId(Id(301031)), Id(301032), 1));

            var valid = new CampaignProgressSaveState(1, NewGameConfiguration.DefaultConfigurationId,
                new CombatSideId("player"), new Game.Domain.AI.AiStateId(Id(301030)),
                new Game.Domain.World.SettlementId(Id(301031)), true, 1, false, null, null, null, null, 1);
            var codec = new SaveXmlCodec();
            var bytes = codec.Serialize(new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(301033),
                new GameTimeState(), 400, campaign: valid));
            var xml = Encoding.UTF8.GetString(bytes).Replace("<eventSequence>1</eventSequence>",
                "<eventSequence>2</eventSequence>");
            Assert.Throws<System.IO.InvalidDataException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(xml)));
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
