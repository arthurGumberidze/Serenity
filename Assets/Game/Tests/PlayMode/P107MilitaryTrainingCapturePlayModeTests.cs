using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("P10.7")]
    public sealed class P107MilitaryTrainingCapturePlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-p107-play-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MilitaryPanelTrainsSameResidentShowsClothingAndSavesV24Continuity()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var id = root.MaleDemoCharacter.Id;
            root.SelectionProbe.SelectCharacterIds(new[] { id });
            root.GameplayUi.SetPanel(GameplayPanel.Military);
            root.GameplayUi.RefreshNow();
            yield return null;
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("military-train"), Is.Not.Null);
            var count = root.Characters.Count;
            var start = root.MilitaryTraining.TryStart(id, root.DemoBarracksBuilding.Id,
                new CombatSideId("player"), root.Clock.State.CalendarTicks,
                root.Inventories.All.Select(x => x.Owner));
            Assert.That(start.Started, Is.True);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(4));
            yield return null;
            Assert.That(root.Characters.Count, Is.EqualTo(count));
            Assert.That(root.Characters.Get(id), Is.SameAs(root.MaleDemoCharacter));
            Assert.That(root.MilitaryTraining.GetRank(id), Is.EqualTo(MilitaryRank.Recruit));
            Assert.That(root.Appearances.Get(id).ClothingId,
                Is.EqualTo(Game.Domain.Characters.StoneAgeAppearanceProfiles.MilitaryClothing));
            Assert.That(root.MilitaryUnits.TryGetForMember(id, out var unit), Is.True);
            Assert.That(unit.ActiveMemberIds, Does.Contain(id));

            root.PlayerSaves.Save(3);
            var snapshot = root.Saves.Capture();
            Assert.That(snapshot.FormatVersion, Is.EqualTo(SaveSnapshot.MilitaryCampaignVersion));
            Assert.That(snapshot.MilitaryCampaign.Training.Records.Any(x => x.CharacterId == id && x.Completed), Is.True);
            root.PlayerSaves.Load(3);
            yield return null;
            Assert.That(root.MilitaryTraining.GetRank(id), Is.EqualTo(MilitaryRank.Recruit));
            Assert.That(root.Appearances.Get(id).ClothingId,
                Is.EqualTo(Game.Domain.Characters.StoneAgeAppearanceProfiles.MilitaryClothing));
        }

        [UnityTest]
        public IEnumerator RmbAttackMatrixPhysicalMoveStrategicButtonAndFlagClockArePlayerFacing()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var id = root.MaleDemoCharacter.Id;
            root.SelectionProbe.SelectCharacterIds(new[] { id });
            var ground = new Game.Domain.Work.ContextCommandTarget(Game.Domain.Work.ContextTargetKind.Ground,
                new WorldPosition(4, 0, 4));
            root.GameplayUi.OpenContextMenu(ground, new Vector2(800, 450));
            Assert.That(root.GameplayUi.ContextActionStates[Game.Domain.Work.ContextActionKind.Attack].Enabled, Is.False);
            root.GameplayUi.CloseContextMenu();

            var training = root.MilitaryTraining.TryStart(id, root.DemoBarracksBuilding.Id,
                new CombatSideId("player"), root.Clock.State.CalendarTicks,
                root.Inventories.All.Select(x => x.Owner));
            Assert.That(training.Started, Is.True);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(4));
            Assert.That(root.AiAgents.TryGet(id, out var agent), Is.True);
            var desired = new Vector3(agent.Position.X + 8f, agent.Position.Y, agent.Position.Z + 4f);
            Assert.That(NavMesh.SamplePosition(desired, out var navHit, 24f, NavMesh.AllAreas), Is.True);
            ground = new Game.Domain.Work.ContextCommandTarget(Game.Domain.Work.ContextTargetKind.Ground,
                new WorldPosition(navHit.position.x, navHit.position.y, navHit.position.z));
            root.SelectionProbe.SelectCharacterIds(new[] { id });
            root.GameplayUi.OpenContextMenu(ground, new Vector2(800, 450));
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("context-attack"), Is.Not.Null);
            Assert.That(root.GameplayUi.ContextActionStates[Game.Domain.Work.ContextActionKind.Attack].Enabled, Is.True);
            var issued = root.GameplayUi.ExecuteMilitaryAttack();
            Assert.That(issued.Issued, Is.True);
            Assert.That(root.MilitaryUnits.Get(issued.Order.AttackerUnitId).Order.Kind,
                Is.EqualTo(MilitaryUnitOrderKind.Move));
            Assert.That(root.Work.HasOpenJob(id), Is.True, "Physical attack must queue walking, not teleport.");

            root.GlobalMapBridge.RequestEnterGlobal();
            yield return null;
            root.GameplayUi.SetPanel(GameplayPanel.Global);
            root.GameplayUi.RefreshNow();
            yield return null;
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("strategic-attack"), Is.Not.Null);

            var capture = root.SettlementCapture.Get(root.StarterSettlementId);
            var enemyId = root.StarterEnemyAi.Residents.First(x => !root.MilitaryUnits.TryGetForMember(x, out _));
            var enemy = root.MilitaryUnits.Create(new MilitarySideId("enemy"), new[] { enemyId }, enemyId);
            Assert.That(root.SettlementCapture.TryClaimFlag(root.StarterSettlementId,
                capture.MainBuildingId, enemy.Id, root.Clock.State.CalendarTicks), Is.True);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
            Assert.That(root.SettlementCapture.Get(root.StarterSettlementId).ProgressTicks, Is.Zero,
                "Both active flags are required.");
            Assert.That(root.SettlementCapture.TryClaimFlag(root.StarterSettlementId,
                root.DemoBarracksBuilding.Id, enemy.Id, root.Clock.State.CalendarTicks), Is.True);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
            var progressing = root.SettlementCapture.Get(root.StarterSettlementId);
            Assert.That(progressing.ProgressTicks, Is.EqualTo(TimeSpan.FromHours(1).Ticks));
            Assert.That(progressing.Status, Is.EqualTo(SettlementCaptureClockStatus.Advancing));
        }
    }
}
