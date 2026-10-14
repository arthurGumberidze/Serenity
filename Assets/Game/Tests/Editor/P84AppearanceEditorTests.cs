using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.Campaign;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P84")]
    public sealed class P84AppearanceEditorTests
    {
        [Test]
        public void CatalogExposesHonestBoundedOptionsForBothUmaRaces()
        {
            var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
            Assert.That(catalog.Profiles.Count, Is.EqualTo(2));
            foreach (var profile in catalog.Profiles)
            {
                Assert.That(profile.Parameters.Count, Is.EqualTo(9));
                Assert.That(profile.Skins.Count, Is.EqualTo(3));
                Assert.That(profile.Hairs.Count, Is.EqualTo(4));
                Assert.That(profile.HairColors.Count, Is.EqualTo(5));
                Assert.That(profile.EyeColors.Count, Is.EqualTo(5));
                Assert.That(profile.Ages.Count, Is.EqualTo(3));
                Assert.That(profile.CreateDetailedState(StableEntityId.NewId()).Parameters.All(x => x.Value == 500), Is.True);
            }
            Assert.That(catalog.Get(StoneAgeAppearanceProfiles.UmaFemale).Beards.Single().Value,
                Is.EqualTo("beard_none"));
        }

        [Test]
        public void RandomizeAndResetAreDeterministicAndPreserveFullStateInNewGame()
        {
            var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
            var first = RulerCreationService.Randomize(84001, 4, catalog);
            var replay = RulerCreationService.Randomize(84001, 4, catalog);
            Assert.That(Signature(first.Appearance), Is.EqualTo(Signature(replay.Appearance)));
            Assert.That(first.Appearance.Parameters.All(x => x.Value >= 0 && x.Value <= 1000), Is.True);

            var bootstrap = NewGameBootstrap.Create(first.ToConfiguration(), catalog);
            var ruler = bootstrap.Appearances.Characters.Single(x => x.CharacterId == bootstrap.MonarchId);
            Assert.That(Signature(ruler), Is.EqualTo(Signature(first.Appearance.With(characterId: bootstrap.MonarchId))));

            var reset = RulerCreationService.CreateDefault(84001, catalog);
            Assert.That(reset.Appearance.Parameters.All(x => x.Value == 500), Is.True);
            Assert.That(reset.Appearance.ProfileId, Is.EqualTo(StoneAgeAppearanceProfiles.UmaMale));
        }

        [Test]
        public void V19RoundTripKeepsEveryEditorFieldAndV18StillLoads()
        {
            var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
            var edited = RulerCreationService.Randomize(84002, 9, catalog).Appearance;
            var state = new AppearanceWorldSaveState(new[] { edited });
            var codec = new SaveXmlCodec();
            var v19 = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, appearance: state);
            var restored = codec.Deserialize(codec.Serialize(v19)).Appearance.Characters.Single();
            Assert.That(Signature(restored), Is.EqualTo(Signature(edited)));

            var v18 = new SaveSnapshot(SaveSnapshot.WorkVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, appearance: state);
            var legacy = codec.Deserialize(codec.Serialize(v18)).Appearance.Characters.Single();
            Assert.That(legacy.ProfileId, Is.EqualTo(edited.ProfileId));
            Assert.That(legacy.SkinId, Is.EqualTo(edited.SkinId));
            Assert.That(legacy.EyeColorId.Value, Is.EqualTo("eye_brown"));
            Assert.That(legacy.AgeId.Value, Is.EqualTo("age_adult"));
            Assert.That(legacy.Parameters, Is.Empty);
        }

        [Test]
        public void UnsupportedValuesAndOutOfRangeParametersFailFast()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AppearanceParameterValue(
                StoneAgeAppearanceProfiles.Nose, 1001));
            var profile = StoneAgeAppearanceProfiles.CreateCatalog().Get(StoneAgeAppearanceProfiles.UmaMale);
            var invalid = profile.CreateState(StableEntityId.NewId()).With(hairId: new AppearancePartId("missing_hair"));
            Assert.That(profile.Supports(invalid), Is.False);
        }

        private static string Signature(AppearanceState value) => string.Join("|", new[]
        {
            value.ProfileId.Value, value.BodyId.Value, value.FaceId.Value, value.SkinId.Value,
            value.HairId.Value, value.BeardId.Value, value.HairColorId.Value, value.EyeColorId.Value,
            value.AgeId.Value, value.ClothingId.Value,
            string.Join(",", value.Parameters.Select(x => x.Id.Value + "=" + x.Value))
        });
    }
}
