using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Diagnostics;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Resources;
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
    [TestFixture, Category("P10.8")]
    public sealed class P108DiagnosticsPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-p108-play-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator PauseCodesRouteRunsAllCanonicalEffectsRepeatsAndPersistsIrreversibleFlag()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            root.AiRuntime.SetPaused(false);
            root.GameplayUi.SetPanel(GameplayPanel.Codes);
            Assert.That(root.GameplayUi.ActivePanel, Is.Not.EqualTo(GameplayPanel.Codes));
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("nav-codes").enabledSelf, Is.False);

            root.PlayerSaves.Save(2); // A clean v25 party checkpoint used to prove the flag cannot be undone by load.
            root.AiRuntime.SetPaused(true);
            root.GameplayUi.RefreshNow();
            root.GameplayUi.SetPanel(GameplayPanel.Codes);
            yield return null;
            Assert.That(root.GameplayUi.ActivePanel, Is.EqualTo(GameplayPanel.Codes));
            Assert.That(root.GameplayUi.RootVisual.Q<TextField>("codes-input"), Is.Not.Null);
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("codes-apply"), Is.Not.Null);
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("unknown"), Is.False);
            Assert.That(root.Diagnostics.CheatsUsed, Is.False);

            var totalCells = root.ConnectedWorld.Layout.Columns * root.ConnectedWorld.Layout.Rows;
            Assert.That(root.ConnectedWorld.RevealedCellCount, Is.LessThan(totalCells));
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("map_serenity"), Is.True);
            Assert.That(root.ConnectedWorld.RevealedCellCount, Is.EqualTo(totalCells));
            Assert.That(root.GameplayUi.LastDiagnosticResult.AffectedCount, Is.GreaterThan(0));
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("map_serenity"), Is.True);
            Assert.That(root.GameplayUi.LastDiagnosticResult.AffectedCount, Is.Zero);

            Assert.That(root.GameplayUi.SubmitDiagnosticCode("serenity"), Is.True);
            Assert.That(root.Research.CompletedTechnologies.Count, Is.EqualTo(root.Technologies.All.Count));
            Assert.That(root.Diagnostics.CheatsUsed, Is.True);
            Assert.That(root.Diagnostics.AchievementsEnabled, Is.False);

            var resourceBefore = root.Resources.All.ToDictionary(x => x.Id, x => Total(root, x.Id));
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("res_serenity"), Is.True);
            foreach (var definition in root.Resources.All)
                Assert.That(Total(root, definition.Id), Is.EqualTo(resourceBefore[definition.Id] + 10000),
                    definition.Id.ToString());
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("res_serenity"), Is.True);
            foreach (var definition in root.Resources.All)
                Assert.That(Total(root, definition.Id), Is.EqualTo(resourceBefore[definition.Id] + 20000),
                    definition.Id + " repeat");

            var trainingBefore = root.MilitaryTraining.Records.Count;
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("army_serenity"), Is.True,
                root.Diagnostics.LastFailureDetail);
            var firstArmy = root.MilitaryTraining.Records.Skip(trainingBefore).ToArray();
            Assert.That(firstArmy.Length, Is.EqualTo(root.MilitaryTraining.Catalog.All.Count));
            CollectionAssert.AreEquivalent(root.MilitaryTraining.Catalog.All.Select(x => x.Rank),
                firstArmy.Select(x => x.Rank));
            Assert.That(firstArmy.Select(x => x.CharacterId).Distinct().Count(), Is.EqualTo(firstArmy.Length));
            foreach (var record in firstArmy)
            {
                Assert.That(record.Completed, Is.True);
                Assert.That(root.MilitaryUnits.TryGetForMember(record.CharacterId, out var unit), Is.True);
                Assert.That(unit.OwnerSideId.Value, Is.EqualTo("player"));
                Assert.That(root.Appearances.Get(record.CharacterId).ClothingId,
                    Is.EqualTo(StoneAgeAppearanceProfiles.MilitaryClothing));
                Assert.That(root.Characters.Get(record.CharacterId).TryGetSkill(
                    Game.Simulation.Military.MilitaryTrainingService.CombatSkillId, out var skill), Is.True);
                Assert.That(skill.Level.Value, Is.GreaterThanOrEqualTo(20));
            }
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("army_serenity"), Is.True);
            var allDiagnosticSoldiers = root.MilitaryTraining.Records.Skip(trainingBefore).ToArray();
            Assert.That(allDiagnosticSoldiers.Length, Is.EqualTo(firstArmy.Length * 2));
            Assert.That(allDiagnosticSoldiers.Select(x => x.CharacterId).Distinct().Count(),
                Is.EqualTo(allDiagnosticSoldiers.Length));

            Assert.That(root.GameplayUi.RunFamilyDiagnostic(), Is.True);
            var family = root.GameplayUi.LastDiagnosticResult;
            var first = root.Characters.Get(family.FirstCharacterId.Value);
            var second = root.Characters.Get(family.SecondCharacterId.Value);
            Assert.That(first.AgeInCompletedYears(root.Clock.State.BiologicalTicks), Is.GreaterThanOrEqualTo(18));
            Assert.That(second.AgeInCompletedYears(root.Clock.State.BiologicalTicks), Is.GreaterThanOrEqualTo(18));
            Assert.That(first.SpouseId, Is.EqualTo(second.Id));
            Assert.That(second.SpouseId, Is.EqualTo(first.Id));
            Assert.That(root.Domestic.Beds.Any(x => x.Id == family.BedId.Value && x.AssignedResidentId == second.Id), Is.True);
            Assert.That(root.Domestic.Consents.Any(x => x.IsMutual &&
                (x.FirstId == first.Id || x.SecondId == first.Id)), Is.True);
            Assert.That(root.HealthFamily.Pregnancies.Any(x => x.PregnancyId == family.PregnancyId.Value), Is.True);
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("family-diagnostic-skip"), Is.Not.Null);

            var expectedCharacters = root.Characters.Count;
            var expectedResources = root.Resources.All.ToDictionary(x => x.Id, x => Total(root, x.Id));
            root.PlayerSaves.Save(1);
            var marked = root.PlayerSaves.ListSlots().Single(x => x.Index == 1);
            Assert.That(marked.CheatsUsed, Is.True);
            Assert.That(marked.Detail, Does.Contain("достижения отключены"));
            Assert.That(root.Saves.Capture().FormatVersion, Is.EqualTo(Game.Domain.SaveSnapshot.DiagnosticVersion));

            root.PlayerSaves.Load(2);
            yield return null;
            Assert.That(root.Diagnostics.CheatsUsed, Is.True, "Loading a clean checkpoint cannot re-enable achievements.");
            root.PlayerSaves.Load(1);
            yield return null;
            Assert.That(root.Characters.Count, Is.EqualTo(expectedCharacters));
            foreach (var amount in expectedResources)
                Assert.That(Total(root, amount.Key), Is.EqualTo(amount.Value));
            Assert.That(root.HealthFamily.Pregnancies.Any(x => x.PregnancyId == family.PregnancyId.Value), Is.True);
        }

        [UnityTest]
        public IEnumerator CorruptSlotIsBlockedAndDoesNotChangeCheatEvidence()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            Assert.That(root.GameplayUi.SubmitDiagnosticCode("serenity"), Is.True);
            var path = Path.Combine(saveDirectory, LocalGameplaySaveFacade.SlotId(3) + ".xml");
            Directory.CreateDirectory(saveDirectory);
            File.WriteAllText(path, "<serenitySave><version>25</version><diagnostics>");
            var slot = root.PlayerSaves.ListSlots().Single(x => x.Index == 3);
            Assert.That(slot.IsCorrupt, Is.True);
            Assert.Throws<InvalidDataException>(() => root.PlayerSaves.Load(3));
            Assert.That(root.Diagnostics.CheatsUsed, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingCanonicalStorageFailsAtomicallyAndDoesNotMarkParty()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var before = root.Resources.All.ToDictionary(x => x.Id, x => Total(root, x.Id));
            foreach (var inventory in root.Inventories.All
                         .Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage).ToArray())
                if (root.Buildings.TryGet(inventory.Owner.Id, out var building) && building.IsOperational &&
                    building.OwnerSideId.Value == "player")
                    building.ApplyDamage(Game.Domain.StableEntityId.NewId(), building.CurrentIntegrity,
                        BuildingDamageType.Combat, root.Clock.State.CalendarTicks);

            Assert.That(root.GameplayUi.SubmitDiagnosticCode("res_serenity"), Is.False);
            Assert.That(root.GameplayUi.LastDiagnosticResult.Failure, Is.EqualTo(DiagnosticFailure.MissingStorage));
            Assert.That(root.Diagnostics.CheatsUsed, Is.False);
            foreach (var amount in before) Assert.That(Total(root, amount.Key), Is.EqualTo(amount.Value));
            yield return null;
        }

        private static long Total(LocalSceneCompositionRoot root, ResourceId id) =>
            root.Inventories.All.Sum(x => x.GetAmount(id).Units);
    }
}
