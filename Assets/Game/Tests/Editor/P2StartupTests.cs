using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Time;
using Game.Domain.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("P2")]
    public sealed class P2StartupTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-p2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void UserSettingsRoundTripOutsideWorldSaveAndValidateUniquePrimaryKeys()
        {
            var path = Path.Combine(directory, "preferences", "user-settings.json");
            var store = new UserSettingsStore(path);
            var settings = new UserSettingsData
            {
                masterVolume = 0.35f,
                resolutionWidth = 1920,
                resolutionHeight = 1080,
                uiScale = 1.25f,
                cameraSpeed = 1.4f,
                cameraUp = "upArrow",
                cameraDown = "downArrow",
                cameraLeft = "leftArrow",
                cameraRight = "rightArrow"
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.That(loaded.WasCorrupt, Is.False);
            Assert.That(loaded.Settings.masterVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(loaded.Settings.resolutionWidth, Is.EqualTo(1920));
            Assert.That(loaded.Settings.uiScale, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(loaded.Settings.cameraUp, Is.EqualTo("upArrow"));
            Assert.That(path, Does.Not.Contain("Saves"));

            loaded.Settings.pause = loaded.Settings.build;
            Assert.Throws<InvalidDataException>(() => loaded.Settings.Validate());
        }

        [Test]
        public void CorruptPreferencesReturnSafeDefaultsWithoutReplacingSource()
        {
            var path = Path.Combine(directory, "user-settings.json");
            File.WriteAllText(path, "{ this is not valid json");
            var before = File.ReadAllText(path);

            var loaded = new UserSettingsStore(path).Load();

            Assert.That(loaded.WasCorrupt, Is.True);
            Assert.That(loaded.Settings.version, Is.EqualTo(UserSettingsData.CurrentVersion));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test]
        public void StartupCatalogSeparatesEmptyCorruptAndUsableSlots()
        {
            var validSlot = LocalGameplaySaveFacade.SlotId(2);
            var catalog = new StartupSaveCatalog(new FakeCatalog(new[]
            {
                new SaveSlotMetadata(LocalGameplaySaveFacade.SlotId(1), null, null, null,
                    DateTime.UtcNow.AddMinutes(-1), true, "corrupt"),
                new SaveSlotMetadata(validSlot, StableEntityId.NewId(), 1440, GameWorldMode.Local,
                    DateTime.UtcNow, false, string.Empty, 12345)
            }));
            var slots = catalog.ListSlots();

            Assert.That(slots.Single(x => x.Index == 1).IsCorrupt, Is.True);
            Assert.That(slots.Single(x => x.Index == 2).Exists, Is.True);
            Assert.That(slots.Single(x => x.Index == 2).IsCorrupt, Is.False);
            Assert.That(slots.Single(x => x.Index == 3).Exists, Is.False);
            Assert.That(catalog.LatestUsableSlot(), Is.EqualTo(2));
        }

        private sealed class FakeCatalog : ISaveSlotCatalog
        {
            private readonly IReadOnlyList<SaveSlotMetadata> slots;
            public FakeCatalog(IReadOnlyList<SaveSlotMetadata> values) => slots = values;
            public IReadOnlyList<SaveSlotMetadata> ListSlots() => slots;
        }

        [Test]
        public void PrimaryKeyOverridesApplyToExistingInputAuthority()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Game/Presentation/Input/LocalGameplay.inputactions");
            Assert.That(source, Is.Not.Null);
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var settings = new UserSettingsData { cameraUp = "upArrow", pause = "enter" };
                UserSettingsRuntime.ApplyBindings(settings, copy);

                var up = copy.FindActionMap("Camera", true).FindAction("Move", true).bindings
                    .First(x => x.isPartOfComposite && x.name == "up" && x.path == "<Keyboard>/w");
                Assert.That(up.overridePath, Is.EqualTo("<Keyboard>/upArrow"));
                var pause = copy.FindActionMap("Time", true).FindAction("TogglePause", true).bindings
                    .First(x => x.path.StartsWith("<Keyboard>"));
                Assert.That(pause.overridePath, Is.EqualTo("<Keyboard>/enter"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
    }
}
