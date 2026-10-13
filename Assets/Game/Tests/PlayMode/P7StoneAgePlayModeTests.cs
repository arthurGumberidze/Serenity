using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("P7")]
    public sealed class P7StoneAgePlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-p7-play-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator UndefendedPlayerOccupationPersistsWinsOnceAndCampaignContinues()
        {
            var root = Root();
            ReserveEnemyNonCombatants(root, "p7_test_victory_reserve");
            var member = root.PlayerCharacterIds[0];
            var unit = root.MilitaryUnits.Create(MilitaryUnitId.NewId(), new MilitarySideId("player"),
                new[] { member }, member, MilitaryUnitRepresentationMode.Aggregate, "enemy_settlement");
            root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
            var occupation = root.GlobalMap.Occupations.Single(x => x.ClaimantUnitId == unit.Id);
            Assert.That(root.Battles.Battles, Is.Empty);

            AdvanceSimulation(root, TimeSpan.FromMinutes(12));
            var halfway = root.GlobalMap.Occupations.Single();
            Assert.That(halfway.ProgressTicks, Is.EqualTo(TimeSpan.FromHours(12).Ticks));
            root.PlayerSaves.Save(1);
            AdvanceSimulation(root, TimeSpan.FromMinutes(1));
            root.PlayerSaves.Load(1);
            Assert.That(root.GlobalMap.Occupations.Single().OccupationId, Is.EqualTo(occupation.OccupationId));
            Assert.That(root.GlobalMap.Occupations.Single().ProgressTicks, Is.EqualTo(TimeSpan.FromHours(12).Ticks));

            AdvanceSimulation(root, TimeSpan.FromMinutes(12));
            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
            var rival = root.GlobalMap.Settlements.Single(x => x.Id == root.CampaignProgress.State.RivalHomeSettlementId);
            Assert.That(rival.OwnerSideId, Is.EqualTo(new CombatSideId("player")));
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.CampaignProgress.State.SourceOccupationId, Is.EqualTo(occupation.OccupationId));
            Assert.That(root.CampaignProgress.State.SourceBattleId, Is.Null);
            Assert.That(root.GameRun.Status, Is.EqualTo(GameRunStatus.Running));
            var victorySequence = root.CampaignProgress.State.EventSequence;
            AdvanceSimulation(root, TimeSpan.FromSeconds(1));
            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(victorySequence));
            Assert.That(root.GlobalMap.OccupationEvents.Count(x => x.Kind == SettlementOccupationEventKind.Completed),
                Is.EqualTo(1));
            root.GameplayUi.RefreshNow();
            Assert.That(AllUiText(root.GameplayUi), Does.Contain("СОПЕРНИК ПОБЕЖДЁН"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyOccupationDefeatsPlayerAndLoadingPreDefeatSaveRestoresRun()
        {
            var root = Root();
            root.PlayerSaves.Save(1);
            var member = root.StarterEnemyAi.Residents[0];
            var unit = root.MilitaryUnits.Create(MilitaryUnitId.NewId(), new MilitarySideId("enemy"),
                new[] { member }, member, MilitaryUnitRepresentationMode.Aggregate, "starter_settlement");
            root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
            Assert.That(root.GlobalMap.Occupations.Single().ClaimantUnitId, Is.EqualTo(unit.Id));
            AdvanceSimulation(root, TimeSpan.FromMinutes(24));
            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
            Assert.That(root.GameRun.Status, Is.EqualTo(GameRunStatus.GameOver));
            Assert.That(root.GameRun.GameOverReason, Is.EqualTo(GameOverReason.NoControlledSettlement));
            root.GameplayUi.RefreshNow();
            Assert.That(AllUiText(root.GameplayUi), Does.Contain("ПОРАЖЕНИЕ")
                .And.Contain("не осталось подконтрольных поселений"));

            root.PlayerSaves.Load(1);
            Assert.That(root.GameRun.Status, Is.EqualTo(GameRunStatus.Running));
            Assert.That(root.GlobalMap.Settlements.Any(x => x.OwnerSideId == new CombatSideId("player")), Is.True);
            yield return null;
        }

        private static void ReserveEnemyNonCombatants(LocalSceneCompositionRoot root, string key)
        {
            var roster = root.StarterEnemyAi.Residents.Skip(1).ToArray();
            root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(
                    root.ActiveNewGameConfiguration.WorldSeed, key)), new MilitarySideId("enemy"),
                roster, roster[0], MilitaryUnitRepresentationMode.Aggregate, "local");
        }

        private static void AdvanceSimulation(LocalSceneCompositionRoot root, TimeSpan realDelta)
        {
            root.Clock.Resume();
            root.AiRuntime.AdvanceForDebug(realDelta);
            root.Clock.Pause();
        }

        private static string AllUiText(GameplayUiController ui) => string.Join("\n",
            ui.RootVisual.Query<Label>().ToList().Select(x => x.text));

        private static LocalSceneCompositionRoot Root()
        {
            var value = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(value, Is.Not.Null);
            return value;
        }
    }
}
