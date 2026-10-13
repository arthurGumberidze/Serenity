using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Infrastructure;
using Game.Simulation.Tiers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("P4")]
    public sealed class P4RulerCreationPlayModeTests
    {
        private string directory;
        private string oldSaveDirectory;
        private string oldSettingsPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-p4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            oldSaveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            oldSettingsPath = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", oldSaveDirectory);
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", oldSettingsPath);
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator EditorShowsRussianIdentityStablePartsAndControllableLivePreview()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenNewGame();
            yield return null;

            Assert.That(menu.RootVisual.Q<TextField>("ruler-name"), Is.Not.Null);
            Assert.That(menu.RootVisual.Q<TextField>("dynasty-name"), Is.Not.Null);
            Assert.That(menu.RootVisual.Q<DropdownField>("appearance-profile").choices.Count, Is.EqualTo(2));
            Assert.That(menu.RootVisual.Q<VisualElement>("appearance-parts").Query<Label>().ToList().Count,
                Is.GreaterThanOrEqualTo(8));
            Assert.That(menu.HasLiveRulerPreview,
                Is.EqualTo(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null));

            var before = menu.RulerDraft.Profile.Id;
            menu.RotatePreview(25f);
            menu.SetPreviewZoom(4.4f);
            menu.RandomizeRuler();
            Assert.That(menu.RulerDraft.Profile.Id.IsValid, Is.True);
            menu.ResetRuler();
            Assert.That(menu.RulerDraft.Profile.Id, Is.EqualTo(StoneAgeAppearanceProfiles.AdultA));
            Assert.That(before, Is.EqualTo(StoneAgeAppearanceProfiles.AdultA));
        }

        [UnityTest]
        public IEnumerator ConfirmedFemaleRulerSurvivesTierRoundTripAndSaveLoad()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.StartNewGame(new NewGameConfiguration(44008, rulerName: "Эльда",
                dynastyName: "Дом Северной Сосны", rulerSex: CharacterSex.Female,
                rulerAppearanceProfileId: StoneAgeAppearanceProfiles.AdultB));
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null;
            yield return null;

            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.MaleDemoCharacter.Name.GivenName, Is.EqualTo("Эльда"));
            Assert.That(root.MaleDemoCharacter.Sex, Is.EqualTo(CharacterSex.Female));
            Assert.That(root.Dynasty.Name, Is.EqualTo("Дом Северной Сосны"));
            Assert.That(root.Appearances.Get(root.MaleDemoCharacter.Id).ProfileId,
                Is.EqualTo(StoneAgeAppearanceProfiles.AdultB));
            Assert.That(root.DemoPresenters[0].Appearance.ProfileId, Is.EqualTo(StoneAgeAppearanceProfiles.AdultB));

            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            Assert.That(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var rematerialized), Is.True);
            Assert.That(rematerialized.Appearance.ProfileId, Is.EqualTo(StoneAgeAppearanceProfiles.AdultB));

            root.GameplayUi.SaveSlot(1);
            root.MaleDemoCharacter.Rename(new CharacterName("Временно"));
            root.GameplayUi.ConfirmLoadSlot(1);
            yield return null;
            Assert.That(root.MaleDemoCharacter.Name.GivenName, Is.EqualTo("Эльда"));
            Assert.That(root.Appearances.Get(root.MaleDemoCharacter.Id).ProfileId,
                Is.EqualTo(StoneAgeAppearanceProfiles.AdultB));
            Assert.That(root.ReferenceCamp.EquippedToolCount, Is.EqualTo(2));
        }
    }
}
