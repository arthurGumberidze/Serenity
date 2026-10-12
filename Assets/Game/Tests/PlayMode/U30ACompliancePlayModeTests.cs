using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Combat;
using Game.Simulation.World;
using Game.Presentation.UI;
using Game.Presentation.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U30A")]
    [Category("U31")]
    public sealed class U30ACompliancePlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-u30a-play-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator NormalNewGameHasTenPlayerCharactersIndependentAiAndNoDuplicateAuthorities()
        {
            var root = Root();
            Assert.That(root.PlayerCharacterIds.Count, Is.EqualTo(10));
            Assert.That(root.PlayerCharacterIds.Distinct().Count(), Is.EqualTo(10));
            Assert.That(root.StarterEnemyAi.Residents.Count, Is.EqualTo(6));
            Assert.That(root.PlayerCharacterIds.Intersect(root.StarterEnemyAi.Residents), Is.Empty);
            Assert.That(root.Dynasty.CurrentMonarchId, Is.EqualTo(root.NewGame.MonarchId));
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.False);
            root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
            root.GameplayUi.RefreshNow();
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("new-game-button"), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<LocalSceneCompositionRoot>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length,
                Is.EqualTo(1));

            const long requestedSeed = 301101;
            root.StartNewGame(requestedSeed);
            yield return null;
            yield return null;
            root = Root();
            Assert.That(root.ActiveNewGameConfiguration.WorldSeed, Is.EqualTo(requestedSeed));
            Assert.That(root.PlayerCharacterIds.Count, Is.EqualTo(10));
            Assert.That(Object.FindObjectsByType<LocalSceneCompositionRoot>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length,
                Is.EqualTo(1));

            root.PlayerSaves.Save(1);
            root.PlayerSaves.Load(1);
            Assert.That(root.PlayerCharacterIds.Count, Is.EqualTo(10));
            Assert.That(Object.FindObjectsByType<LocalSceneCompositionRoot>().Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SupplyLoadsConservativelyAndTravelCombatCheckpointsConsumeExactlyOnce()
        {
            var root = Root();
            var members = root.PlayerCharacterIds.Take(2).ToArray();
            var unit = root.MilitaryUnits.Create(MilitaryUnitId.NewId(), new MilitarySideId("player"),
                members, members[0], MilitaryUnitRepresentationMode.Detailed, "starter_settlement");
            var owners = root.Inventories.All.Select(x => x.Owner).ToArray();
            var berries = new ResourceId("berries");
            var inventoryBefore = root.Inventories.All.Sum(x => x.GetAmount(berries).Units);

            var loaded = root.MilitaryUnits.LoadInitialSupply(unit.Id, root.Inventories, owners);
            var supplied = root.MilitaryUnits.Get(unit.Id);
            var inventoryAfter = root.Inventories.All.Sum(x => x.GetAmount(berries).Units);
            Assert.That(loaded, Is.EqualTo(12));
            Assert.That(supplied.Supply.Get(berries), Is.EqualTo(12));
            Assert.That(inventoryAfter + supplied.Supply.Get(berries), Is.EqualTo(inventoryBefore));

            var supplyBeforeTransition = supplied.Supply.Get(berries);
            root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Detailed);
            Assert.That(root.MilitaryUnits.Get(unit.Id).Supply.Get(berries), Is.EqualTo(supplyBeforeTransition));
            root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            var origin = new WorldLocationId(root.MilitaryUnits.Get(unit.Id).AbstractLocationId);
            var route = root.GlobalMap.GetRoutes(origin).First();
            var destination = route.From == origin ? route.To : route.From;
            var travel = root.GlobalMap.IssueTravelOrder(unit.Id, destination, root.Clock.State.CalendarTicks);
            var duplicate = root.GlobalMap.IssueTravelOrder(unit.Id, destination, root.Clock.State.CalendarTicks);
            Assert.That(travel.Status, Is.EqualTo(TravelOrderStatus.Issued));
            Assert.That(duplicate.Status, Is.EqualTo(TravelOrderStatus.AlreadyTravelling));
            Assert.That(root.MilitaryUnits.Get(unit.Id).Supply.Get(berries), Is.EqualTo(10));

            for (var hour = 0; hour < 5; hour++)
                Assert.That(root.MilitaryUnits.ConsumeCombatSupplyForCharacter(members[0],
                    hour * TimeSpan.TicksPerHour), Is.EqualTo(1000));
            Assert.That(root.MilitaryUnits.ConsumeCombatSupplyForCharacter(members[1],
                4 * TimeSpan.TicksPerHour), Is.EqualTo(1000),
                "Every member must observe the persisted result of the unit's exactly-once combat checkpoint.");
            var shortage = root.MilitaryUnits.ConsumeCombatSupplyForCharacter(members[0],
                5 * TimeSpan.TicksPerHour);
            Assert.That(root.MilitaryUnits.ConsumeCombatSupplyForCharacter(members[1],
                5 * TimeSpan.TicksPerHour), Is.EqualTo(700));
            supplied = root.MilitaryUnits.Get(unit.Id);
            Assert.That(shortage, Is.EqualTo(700));
            Assert.That(root.MilitaryUnits.GetTravelDurationModifier(unit.Id), Is.EqualTo(1500));
            Assert.That(supplied.Supply.Get(berries), Is.Zero);
            Assert.That(supplied.Supply.ConsumedUnits, Is.EqualTo(12));
            Assert.That(supplied.Supply.Checkpoints.Count, Is.EqualTo(7));
            Assert.That(supplied.MoraleSummary, Is.EqualTo(925));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanonicalBattleCaptureRecordsVictoryOnceAndSandboxContinues()
        {
            var root = Root();
            var battle = root.StartDemoUnitBattle(BattleMode.Abstract);
            root.MilitaryUnits.SetAbstractLocation(battle.SideAUnitId.Value, "enemy_settlement");
            root.MilitaryUnits.SetAbstractLocation(battle.SideBUnitId.Value, "enemy_settlement");
            foreach (var enemy in root.MilitaryUnits.Get(battle.SideBUnitId.Value).ActiveMemberIds)
                root.HealthFamily.ApplyCombatInjury(enemy, new Game.Domain.Health.HealthConditionId("serious_wound"),
                    root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks, true);

            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
            yield return null;
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).Outcome,
                Is.EqualTo(CombatOutcome.SideAVictory));
            Assert.That(root.GlobalMap.Settlements.Single(x => x.Id == root.CampaignProgress.State.RivalHomeSettlementId)
                .OwnerSideId, Is.EqualTo(new CombatSideId("player")));
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.CampaignProgress.State.SourceBattleId, Is.EqualTo(battle.BattleId));
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(2));

            var victoryTick = root.CampaignProgress.State.VictoryTick;
            var beforeContinuation = root.Clock.State.CalendarTicks;
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
            yield return null;
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(beforeContinuation));
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(2));
            Assert.That(root.CampaignProgress.State.VictoryTick, Is.EqualTo(victoryTick));
            root.GameplayUi.RefreshNow();
            Assert.That(string.Join("\n", root.GameplayUi.RootVisual.Query<Label>().ToList().Select(x => x.text)),
                Does.Contain("СОПЕРНИК ПОБЕЖДЁН").Or.Contain("Военная победа"));

            root.PlayerSaves.Save(1);
            root.PlayerSaves.Load(1);
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RivalDiscoveryIsCanonicalExactlyOnceAndPersistsThroughPlayerSaveLoad()
        {
            var root = Root();
            var member = root.PlayerCharacterIds[0];
            var unit = root.MilitaryUnits.Create(MilitaryUnitId.NewId(), new MilitarySideId("player"),
                new[] { member }, member,
                MilitaryUnitRepresentationMode.Aggregate, "starter_settlement");
            Assert.That(root.CampaignProgress.State.EventSequence, Is.Zero);

            root.MilitaryUnits.SetAbstractLocation(unit.Id, "enemy_settlement");
            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
            var discoveryTick = root.CampaignProgress.State.DiscoveredTick;
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.True);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(1));

            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks + 1);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(1));
            Assert.That(root.CampaignProgress.State.DiscoveredTick, Is.EqualTo(discoveryTick));

            root.PlayerSaves.Save(1);
            root.CampaignProgress.RestoreLegacyDefault();
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.False);
            root.PlayerSaves.Load(1);
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.True);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(1));
            Assert.That(root.CampaignProgress.State.DiscoveredTick, Is.EqualTo(discoveryTick));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TpsAttackUsesU20EncounterAndOneRequestCannotDoubleHit()
        {
            var root = Root();
            var battle = root.StartDemoUnitBattle(BattleMode.Physical);
            var attacker = root.MilitaryUnits.Get(battle.SideAUnitId.Value).CaptainId.Value;
            var target = battle.Combatants.First(x => x.SideId == battle.SideB).CharacterId;
            Assert.That(root.CharacterPresentations.TryGet(attacker, out var attackerPresenter), Is.True);
            Assert.That(root.CharacterPresentations.TryGet(target, out var targetPresenter), Is.True);
            targetPresenter.transform.position = attackerPresenter.transform.position + attackerPresenter.transform.forward;
            Physics.SyncTransforms();
            Assert.That(root.ThirdPersonControl.TryEnter(attacker).IsEligible, Is.True);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));

            var before = root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).AttackHistory.Count;
            root.AiRuntime.SetPaused(true);
            Assert.That(root.ThirdPersonControl.TryAttack(target), Is.EqualTo(ManualCombatAttackStatus.Invalid));
            root.AiRuntime.SetPaused(false);
            Assert.That(root.ThirdPersonControl.TryAttack(target), Is.EqualTo(ManualCombatAttackStatus.Hit));
            var afterOne = root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).AttackHistory.Count;
            Assert.That(afterOne, Is.EqualTo(before + 1));
            Assert.That(root.ThirdPersonControl.TryAttack(target), Is.EqualTo(ManualCombatAttackStatus.Cooldown));
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).AttackHistory.Count,
                Is.EqualTo(afterOne));
            var playerUnit = root.MilitaryUnits.Get(battle.SideAUnitId.Value);
            root.ThirdPersonControl.Exit();
            Assert.That(root.ThirdPersonControl.TryEnter(attacker).IsEligible, Is.True);
            Assert.That(root.MilitaryUnits.Get(battle.SideAUnitId.Value).CaptainId, Is.EqualTo(playerUnit.CaptainId));
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).AttackHistory.Count,
                Is.EqualTo(afterOne));
            Assert.That(root.ThirdPersonControl.TryAttack(target), Is.EqualTo(ManualCombatAttackStatus.Cooldown));
            root.ThirdPersonControl.Exit();
            yield return null;
        }

        private static LocalSceneCompositionRoot Root()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            return root;
        }
    }
}
