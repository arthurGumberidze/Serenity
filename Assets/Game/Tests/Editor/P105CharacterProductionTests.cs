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
            Assert.That(a.Select(x => x.AgeId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(a.Any(x => x.ClothingId == StoneAgeAppearanceProfiles.WorkerClothing), Is.True);
            Assert.That(a.Any(x => x.ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing), Is.True);
            Assert.That(a.All(x => x.Parameters.Count == 9), Is.True);
        }

        [Test]
        public void DataDrivenWardrobeHookPreservesCanonicalMilitaryIdThroughV22RoundTrip()
        {
            var definitions = StoneAgeClothingDefinitions.CreateCatalog();
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.CivilianClothing).VisualContentAvailable, Is.True);
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.MilitaryClothing).Role,
                Is.EqualTo(CharacterClothingRole.Military));
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.WorkerClothing).VisualContentAvailable, Is.True);
            Assert.That(definitions.Get(StoneAgeAppearanceProfiles.MilitaryClothing).VisualContentAvailable, Is.True);

            var presentations = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                "Assets/Game/Art/Config/CharacterPresentationCatalog.asset");
            Assert.That(presentations.TryResolveRoleVisual(CharacterSex.Male,
                StoneAgeAppearanceProfiles.WorkerClothing, out var worker), Is.True);
            Assert.That(worker.GetComponentInChildren<Animator>(true).isHuman, Is.True);
            Assert.That(presentations.TryResolveRoleVisual(CharacterSex.Female,
                StoneAgeAppearanceProfiles.MilitaryClothing, out var military), Is.True);
            Assert.That(military.GetComponentInChildren<Animator>(true).isHuman, Is.True);

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
        public void AuthoredControllerCoversWorkCombatAndSleepIntents()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Game/Art/Animation/CharacterTier1.controller");
            Assert.That(controller, Is.Not.Null);
            var names = controller.animationClips.Select(x => x.name).ToArray();
            foreach (var required in new[] { "Walk_Carry_Loop", "Farm_Harvest", "TreeChopping_Loop",
                         "Fixing_Kneeling", "Idle_Rail_Loop", "OverhandThrow", "Sword_Regular_Combo", "LayToIdle" })
                Assert.That(names.Any(x => x.Contains(required)), Is.True, required);
        }

        [Test]
        public void CharacterLodMeshCarriesIdentityChannelsAndUsesHysteresisThresholds()
        {
            Assert.That(Tier1AiRuntimeDriver.CharacterLodNearDistance,
                Is.LessThan(Tier1AiRuntimeDriver.CharacterLodFarDistance));
            Assert.That(Tier1AiRuntimeDriver.CharacterLodFarDistance,
                Is.LessThan(Tier1AiRuntimeDriver.CharacterLodImpostorDistance));
            Assert.That(Shader.Find("Serenity/CharacterLod"), Is.Not.Null);
            var mesh = Tier1AiRuntimeDriver.BuildLightweightHumanoidMesh();
            var farMesh = Tier1AiRuntimeDriver.BuildLightweightHumanoidMesh(true);
            try
            {
                Assert.That(mesh.colors.Any(x => x.r > 0.9f), Is.True, "Skin identity channel missing.");
                Assert.That(mesh.colors.Any(x => x.g > 0.9f), Is.True, "Hair identity channel missing.");
                Assert.That(mesh.colors.Any(x => x.b > 0.9f), Is.True, "Clothing identity channel missing.");
                Assert.That(mesh.bounds.size.y, Is.InRange(1.75f, 1.9f));
                Assert.That(farMesh.triangles.Length, Is.LessThan(mesh.triangles.Length));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(farMesh); }
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
