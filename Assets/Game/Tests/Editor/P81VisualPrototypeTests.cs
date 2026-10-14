using System.IO;
using System.Linq;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P81")]
    public sealed class P81VisualPrototypeTests
    {
        [Test]
        public void ProductionSceneSupersedesScopedBenchmarkWithOfficialNaturalPbrTerrain()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var production = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(Object.FindAnyObjectByType<P81VisualPrototypePresentation>(), Is.Null);
            Assert.That(production, Is.Not.Null);
            Assert.DoesNotThrow(production.ValidateConfiguration);
            Assert.That(production.Trees.Count, Is.GreaterThanOrEqualTo(40));
            Assert.That(production.Rocks.Count, Is.GreaterThanOrEqualTo(22));
            Assert.That(production.Trees.Concat(production.Rocks).SelectMany(x =>
                x.GetComponentsInChildren<Renderer>(true)).All(x => x.sharedMaterial != null &&
                x.sharedMaterial.shader.name != "Hidden/InternalErrorShader"), Is.True);
        }

        [Test]
        public void AppearanceIsStableDataAndVisualProviderHasFallback()
        {
            var guid = AssetDatabase.FindAssets("t:CharacterPresentationCatalog").Single();
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.CreateDefinitionCatalog().Get(StoneAgeAppearanceProfiles.UmaMale).Id.Value,
                Is.EqualTo("uma_human_male_30"));
            Assert.That(catalog.Resolve(CharacterSex.Male), Is.Not.Null);
            Assert.That(catalog.UmaAvatarPrefab, Is.Not.Null);
        }

        [Test]
        public void OfficialUmaDependencyAndSelectiveContentArePinned()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.umasteeringgroup.uma/package.json");
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.umasteeringgroup.uma"));
            Assert.That(package.source, Is.EqualTo(PackageSource.Git));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Art/ThirdParty/UMA3/Getting Started/UMADynamicCharacterAvatar-NoPhysics_NoLOD.prefab"),
                Is.Not.Null);
            Assert.That(File.Exists("Assets/Game/Art/ThirdParty/UMA3/SOURCE.md"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Game/Art/ThirdParty/UMA3/Scenes"), Is.False,
                "Demo scenes must stay outside the selective import.");
        }

        [Test]
        public void GeneratedMaterialAndMeshAssetsAreReusable()
        {
            foreach (var path in new[]
            {
                "Assets/Game/Art/Materials/P81/P81_Meadow.mat",
                "Assets/Game/Art/Materials/P81/P81_Shore.mat",
                "Assets/Game/Art/Materials/P81/P81_BrownBark.mat",
                "Assets/Game/Art/Materials/P81/P81_Stone.mat",
                "Assets/Game/Art/Meshes/P81/P81_Meadow.asset",
                "Assets/Game/Art/Meshes/P81/P81_Shore.asset",
                "Assets/Game/Art/Textures/P81/P81_Meadow_Albedo.asset",
                "Assets/Game/Art/Textures/P81/P81_Meadow_Normal.asset"
            }) Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
        }
    }
}
