using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Presentation.Characters;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P4")]
    public sealed class P4AppearanceTests
    {
        [Test]
        public void StableAppearanceIdsAndRandomizeAreDeterministicAndBounded()
        {
            var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
            var first = RulerCreationService.Randomize(44004, 7, catalog);
            var second = RulerCreationService.Randomize(44004, 7, catalog);

            Assert.That(first.Profile.Id, Is.EqualTo(second.Profile.Id));
            Assert.That(first.RulerName, Is.EqualTo(second.RulerName));
            Assert.That(first.DynastyName, Is.EqualTo(second.DynastyName));
            Assert.That(catalog.Profiles.Select(x => x.Id), Does.Contain(first.Profile.Id));
            Assert.That(first.Appearance.CharacterId,
                Is.EqualTo(DeterministicStableId.Create(44004, "player_character", 0)));
            Assert.Throws<System.ArgumentException>(() => new AppearanceProfileId("Not Stable"));
        }

        [Test]
        public void FemaleRulerConfigurationPreservesFamilyDynastyAndSuccessionAuthority()
        {
            var configuration = new NewGameConfiguration(44005, rulerName: "Нера",
                dynastyName: "Дом Ясного Огня", rulerSex: CharacterSex.Female,
                rulerAppearanceProfileId: StoneAgeAppearanceProfiles.AdultB);
            var result = NewGameBootstrap.Create(configuration, StoneAgeAppearanceProfiles.CreateCatalog());
            var ruler = result.PlayerCharacters.Single(x => x.Id == result.MonarchId);
            var spouse = result.PlayerCharacters.Single(x => x.Id == result.SpouseId);
            var firstHeir = result.PlayerCharacters.Single(x => x.Id == result.FirstHeirId);

            Assert.That(ruler.Name.GivenName, Is.EqualTo("Нера"));
            Assert.That(ruler.Sex, Is.EqualTo(CharacterSex.Female));
            Assert.That(spouse.Sex, Is.EqualTo(CharacterSex.Male));
            Assert.That(result.Dynasty.Name, Is.EqualTo("Дом Ясного Огня"));
            Assert.That(result.Dynasty.CurrentMonarchId, Is.EqualTo(result.MonarchId));
            Assert.That(result.Dynasty.MemberIds, Does.Contain(result.FirstHeirId));
            Assert.That(firstHeir.Parents.Single(x => x.Role == ParentRole.Mother).ParentId,
                Is.EqualTo(result.MonarchId));
            Assert.That(result.Appearances.Characters.Single(x => x.CharacterId == result.MonarchId).ProfileId,
                Is.EqualTo(StoneAgeAppearanceProfiles.AdultB));
        }

        [Test]
        public void AppearanceVersionRoundTripsEveryStablePartIdAndLegacyDefaultsAreDeterministic()
        {
            var bootstrap = NewGameBootstrap.Create(new NewGameConfiguration(44006));
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, appearance: bootstrap.Appearances);
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var expected = bootstrap.Appearances.Characters.OrderBy(x => x.CharacterId.ToString()).First();
            var actual = restored.Appearance.Characters.Single(x => x.CharacterId == expected.CharacterId);

            Assert.That(actual.ProfileId, Is.EqualTo(expected.ProfileId));
            Assert.That(actual.BodyId, Is.EqualTo(expected.BodyId));
            Assert.That(actual.FaceId, Is.EqualTo(expected.FaceId));
            Assert.That(actual.SkinId, Is.EqualTo(expected.SkinId));
            Assert.That(actual.HairId, Is.EqualTo(expected.HairId));
            Assert.That(actual.BeardId, Is.EqualTo(expected.BeardId));
            Assert.That(actual.HairColorId, Is.EqualTo(expected.HairColorId));
            Assert.That(actual.ClothingId, Is.EqualTo(expected.ClothingId));

            var registry = new CharacterRegistry();
            registry.RestoreStates(bootstrap.PlayerCharacters);
            var appearances = new CharacterAppearanceService(registry, StoneAgeAppearanceProfiles.CreateCatalog(), 44006);
            appearances.RestoreLegacyDefaults();
            Assert.That(appearances.CaptureState().Characters.Count, Is.EqualTo(10));
            Assert.That(appearances.Get(bootstrap.MonarchId).ProfileId, Is.EqualTo(StoneAgeAppearanceProfiles.AdultA));

            var legacy = new SaveSnapshot(SaveSnapshot.CampaignVersion, StableEntityId.NewId(),
                new GameTimeState(), 400);
            Assert.That(new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(legacy)).Appearance, Is.Null);
        }

        [Test]
        public void MissingSavedContentFallsBackOnlyInPresentationWithoutMutatingCanonicalIds()
        {
            var bootstrap = NewGameBootstrap.Create(new NewGameConfiguration(44007));
            var registry = new CharacterRegistry();
            registry.RestoreStates(bootstrap.PlayerCharacters);
            var ruler = registry.Get(bootstrap.MonarchId);
            var unknown = new AppearanceState(ruler.Id, new AppearanceProfileId("removed_profile"),
                new AppearancePartId("removed_body"), new AppearancePartId("removed_face"),
                new AppearancePartId("removed_skin"), new AppearancePartId("removed_hair"),
                new AppearancePartId("removed_beard"), new AppearancePartId("removed_hair_color"),
                new AppearancePartId("removed_clothing"));
            var service = new CharacterAppearanceService(registry, StoneAgeAppearanceProfiles.CreateCatalog(), 44007);
            service.RestoreState(new AppearanceWorldSaveState(new[] { unknown }));
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                "Assets/Game/Art/Config/CharacterPresentationCatalog.asset");

            var prefab = catalog.Resolve(ruler, service.Get(ruler.Id), out var projected);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(projected.ProfileId, Is.EqualTo(StoneAgeAppearanceProfiles.AdultA));
            Assert.That(service.Get(ruler.Id).ProfileId.Value, Is.EqualTo("removed_profile"));
        }
    }
}
