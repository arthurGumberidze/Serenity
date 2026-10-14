using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Infrastructure;
using Game.Simulation.Campaign;
using Game.Simulation.Tiers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("P84")]
    public sealed class P84AppearanceEditorPlayModeTests
    {
        private string directory;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-p84-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator EditorShowsEverySupportedControlAndEditsCanonicalState()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenNewGame();
            yield return null;
            foreach (var name in new[] { "appearance-skin", "appearance-hair", "appearance-beard",
                         "appearance-hair-color", "appearance-eye-color", "appearance-age", "appearance-clothing" })
                Assert.That(menu.RootVisual.Q<DropdownField>(name), Is.Not.Null, name);
            foreach (var id in menu.RulerDraft.Profile.Parameters)
                Assert.That(menu.RootVisual.Q<Slider>("appearance-" + id.Value), Is.Not.Null, id.Value);
            Assert.That(menu.SetAppearanceOption("hair", "hair_messy"), Is.True);
            Assert.That(menu.SetAppearanceOption("eye_color", "eye_green"), Is.True);
            Assert.That(menu.SetAppearanceParameter(StoneAgeAppearanceProfiles.Nose, 830), Is.True);
            Assert.That(menu.RulerDraft.Appearance.HairId.Value, Is.EqualTo("hair_messy"));
            Assert.That(menu.RulerDraft.Appearance.EyeColorId.Value, Is.EqualTo("eye_green"));
            Assert.That(menu.RulerDraft.Appearance.Parameter(StoneAgeAppearanceProfiles.Nose), Is.EqualTo(830));
        }

        [UnityTest]
        public IEnumerator EditedStateSurvivesWorldTierRoundTripAndSaveLoad()
        {
            var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
            var profile = catalog.Get(StoneAgeAppearanceProfiles.UmaMale);
            var edited = profile.CreateDetailedState(DeterministicStableId.Create(84003, "player_character", 0))
                .With(skinId: new AppearancePartId("skin_deep"), hairId: new AppearancePartId("hair_bun"),
                    beardId: new AppearancePartId("beard_goatee"), eyeColorId: new AppearancePartId("eye_blue"),
                    ageId: new AppearancePartId("age_mature"), parameters: profile.Parameters.Select(id =>
                        new AppearanceParameterValue(id, id == StoneAgeAppearanceProfiles.Height ? 740 : 420)));
            var configuration = new NewGameConfiguration(84003, rulerName: "Арен", dynastyName: "Дом Серенити",
                rulerSex: CharacterSex.Male, rulerAppearanceProfileId: profile.Id, rulerAppearance: edited);
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.StartNewGame(configuration);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null; yield return null;
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            AssertState(root.Appearances.Get(root.MaleDemoCharacter.Id));
            AssertState(root.DemoPresenters[0].Appearance);
            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            Assert.That(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var rematerialized), Is.True);
            AssertState(rematerialized.Appearance);
            root.GameplayUi.SaveSlot(1);
            root.Appearances.Set(profile.CreateState(root.MaleDemoCharacter.Id));
            root.GameplayUi.ConfirmLoadSlot(1);
            yield return null;
            AssertState(root.Appearances.Get(root.MaleDemoCharacter.Id));
        }

        private static void AssertState(AppearanceState state)
        {
            Assert.That(state.SkinId.Value, Is.EqualTo("skin_deep"));
            Assert.That(state.HairId.Value, Is.EqualTo("hair_bun"));
            Assert.That(state.BeardId.Value, Is.EqualTo("beard_goatee"));
            Assert.That(state.EyeColorId.Value, Is.EqualTo("eye_blue"));
            Assert.That(state.AgeId.Value, Is.EqualTo("age_mature"));
            Assert.That(state.Parameter(StoneAgeAppearanceProfiles.Height), Is.EqualTo(740));
        }
    }
}
