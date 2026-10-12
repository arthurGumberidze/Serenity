using System;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Military;
using Game.Domain.Weather;
using Game.Presentation.Localization;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests
{
    [Category("P1")]
    public sealed class P1RussianLocalizationTests
    {
        [SetUp]
        public void ResetDiagnostics() => RussianTextCatalog.ResetMissingDiagnosticsForTests();

        [Test]
        public void RussianIsDefaultAndCoreGlossaryUsesStableKeys()
        {
            Assert.That(RussianTextCatalog.Locale, Is.EqualTo("ru-RU"));
            Assert.That(RussianTextCatalog.Get("ui.nav.build"), Does.Contain("Строительство"));
            Assert.That(RussianTextCatalog.Get("ui.nav.research"), Does.Contain("Исследования"));
            Assert.That(RussianTextCatalog.Content("resource", "wood_log"), Is.EqualTo("Брёвна"));
            Assert.That(RussianTextCatalog.Content("technology", "shelter_construction"), Is.EqualTo("Строительство укрытий"));
            Assert.That(RussianTextCatalog.PersonName("Aren"), Is.EqualTo("Арен"));
            Assert.That(RussianTextCatalog.DynastyName("House Varek"), Is.EqualTo("Дом Варека"));
            Assert.That(RussianTextCatalog.MissingKeys, Is.Empty);
        }

        [Test]
        public void VisibleEnumsHaveRussianNamesInsteadOfRawIdentifiers()
        {
            Enum[] values =
            {
                Season.Winter, WeatherType.HeavyRain, PrecipitationType.Snow, WeatherExposureKind.Cold,
                CharacterLifeState.Alive, CharacterSex.Female, PlayerControlMode.ThirdPerson,
                MilitaryUnitRepresentationMode.Aggregate, MilitaryUnitStatus.Engaged,
                BattleMode.Abstract, CombatEncounterStatus.Active, CombatantState.Retreating,
                BuildingStructuralStatus.Critical, BuildingFireStatus.Burning
            };
            foreach (var value in values)
            {
                var text = RussianTextCatalog.Enum(value);
                Assert.That(text, Is.Not.EqualTo(value.ToString()), value.GetType().Name + "." + value);
                Assert.That(text.Any(c => c >= 'А' && c <= 'я'), Is.True, text);
            }
            Assert.That(RussianTextCatalog.MissingKeys, Is.Empty);
        }

        [TestCase(1, "день")]
        [TestCase(2, "дня")]
        [TestCase(5, "дней")]
        [TestCase(11, "дней")]
        [TestCase(21, "день")]
        public void RussianPluralRulesCoverImportantNumberForms(int value, string expected) =>
            Assert.That(RussianTextCatalog.Plural(value, "день", "дня", "дней"), Is.EqualTo(expected));

        [Test]
        public void MissingKeyIsDiagnosedWithoutExposingTechnicalKeyToPlayer()
        {
            LogAssert.Expect(UnityEngine.LogType.Warning, "Missing ru-RU translation key: test.missing.key");
            Assert.That(RussianTextCatalog.Get("test.missing.key"), Is.EqualTo(RussianTextCatalog.MissingText));
            Assert.That(RussianTextCatalog.Get("test.missing.key"), Is.EqualTo(RussianTextCatalog.MissingText));
            Assert.That(RussianTextCatalog.MissingKeys, Does.Contain("test.missing.key"));
        }
    }
}
