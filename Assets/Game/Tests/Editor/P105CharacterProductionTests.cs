using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Presentation.AI;
using Game.Presentation.Characters;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P105")]
    public sealed class P105CharacterProductionTests
    {
        [Test]
        public void GeneratedPopulationAppearanceIsDeterministicDiverseAndStableIdOwned()
        {
            var first = NewGameBootstrap.Create(new NewGameConfiguration(10501));
            var second = NewGameBootstrap.Create(new NewGameConfiguration(10501));
            var a = first.Appearances.Characters;
            var b = second.Appearances.Characters;

            Assert.That(a.Select(Signature), Is.EqualTo(b.Select(Signature)));
            Assert.That(a.Select(x => x.CharacterId).Distinct().Count(), Is.EqualTo(10));
            Assert.That(a.Select(x => x.SkinId).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            Assert.That(a.Select(x => x.HairId).Distinct().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(a.Select(x => x.Parameter(StoneAgeAppearanceProfiles.Build)).Distinct().Count(),
                Is.GreaterThanOrEqualTo(6));
            Assert.That(a.All(x => x.Parameters.Count == 9), Is.True);
        }

        [Test]
        public void DataDrivenWardrobeHookPreservesCanonicalMilitaryIdThroughV22RoundTrip()
        {
            var definitions = StoneAgeClothingDefinitions.CreateCatalog();
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.CivilianClothing).VisualContentAvailable, Is.True);
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.MilitaryClothing).Role,
                Is.EqualTo(CharacterClothingRole.Military));
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.MilitaryClothing).VisualContentAvailable, Is.False,
                "The future P10.7 hook must not pretend that a military outfit asset already exists.");

            var bootstrap = NewGameBootstrap.Create(new NewGameConfiguration(10502));
            var registry = new CharacterRegistry();
            registry.RestoreStates(bootstrap.PlayerCharacters);
            var service = new CharacterAppearanceService(registry, StoneAgeAppearanceProfiles.CreateCatalog(), 10502);
            var characterId = bootstrap.MonarchId;
            service.SetClothing(characterId, StoneAgeAppearanceProfiles.MilitaryClothing);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, appearance: service.CaptureState());
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(restored.Appearance.Characters.Single(x => x.CharacterId == characterId).ClothingId,
                Is.EqualTo(StoneAgeAppearanceProfiles.MilitaryClothing));
        }

        [Test]
        public void CharacterLodMeshCarriesIdentityChannelsAndUsesHysteresisThresholds()
        {
            Assert.That(Tier1AiRuntimeDriver.CharacterLodNearDistance,
                Is.LessThan(Tier1AiRuntimeDriver.CharacterLodFarDistance));
            Assert.That(Shader.Find("Serenity/CharacterLod"), Is.Not.Null);
            var mesh = Tier1AiRuntimeDriver.BuildLightweightHumanoidMesh();
            try
            {
                Assert.That(mesh.colors.Any(x => x.r > 0.9f), Is.True, "Skin identity channel missing.");
                Assert.That(mesh.colors.Any(x => x.g > 0.9f), Is.True, "Hair identity channel missing.");
                Assert.That(mesh.colors.Any(x => x.b > 0.9f), Is.True, "Clothing identity channel missing.");
                Assert.That(mesh.bounds.size.y, Is.InRange(1.75f, 1.9f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ApprovedTreeSourcesUseMeshLodsWithoutBillboardRendererComponents()
        {
            var paths = new[]
            {
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Pine_A.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Conifer/Conifer.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Cypress/Cypress_Forest_Desktop.prefab"
            };
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponentInChildren<LODGroup>(true), Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<BillboardRenderer>(true), Is.Empty, path);
            }
        }

        private static string Signature(AppearanceState value) => string.Join("|",
            value.CharacterId, value.ProfileId, value.SkinId, value.HairId, value.BeardId,
            value.HairColorId, value.EyeColorId, value.ClothingId,
            string.Join(",", value.Parameters.Select(x => x.Id + "=" + x.Value)));
    }
}
