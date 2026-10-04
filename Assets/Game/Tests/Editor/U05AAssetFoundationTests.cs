using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    public class U05AAssetFoundationTests
    {
        private const string ValidationScenePath = "Assets/Scenes/Development/StoneAgeAssetGallery.unity";

        private static readonly string[] RequiredPrefabPaths =
        {
            "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab",
            "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_B_Tier1.prefab",
            "Assets/Game/Art/Prefabs/Characters/Serenity_Humanoid_Tier2.prefab",
            "Assets/Game/Art/Prefabs/Animals/Serenity_Boar.prefab",
            "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveShelter.prefab",
            "Assets/Game/Art/Prefabs/Environment/Serenity_Tree.prefab",
            "Assets/Game/Art/Prefabs/Environment/Serenity_Rock.prefab",
            "Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab",
            "Assets/Game/Art/Prefabs/Props/Serenity_StorageBasket.prefab",
            "Assets/Game/Art/Prefabs/Tools/Serenity_Torch.prefab",
            "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneAxe.prefab",
            "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneSpear.prefab"
        };

        [Test]
        public void RequiredSerenityPrefabsLoadOutsideThirdPartyFolders()
        {
            foreach (var path in RequiredPrefabPaths)
            {
                Assert.That(path, Does.StartWith("Assets/Game/Art/Prefabs/"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, path);
            }
        }

        [Test]
        public void RuntimePrefabsHaveRenderersMaterialsAndNoMissingScripts()
        {
            foreach (var path in RequiredPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials), Has.None.Null, path);
                var missing = prefab.GetComponentsInChildren<Transform>(true)
                    .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
                Assert.That(missing, Is.Zero, path);
            }
        }

        [Test]
        public void SelectedHumanoidsHaveValidHumanoidAvatarsAndCoreAnimationsExist()
        {
            foreach (var path in RequiredPrefabPaths.Take(2))
            {
                var animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, path);
                Assert.That(animator.avatar, Is.Not.Null, path);
                Assert.That(animator.avatar.isValid, Is.True, path);
                Assert.That(animator.avatar.isHuman, Is.True, path);
            }

            var root = "Assets/Hodaart/HodaartLowPolyCharacterCollection3/Animations/";
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "Idle.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "Walking.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "Running.anim"), Is.Not.Null);

            var boar = AssetDatabase.LoadAssetAtPath<GameObject>(RequiredPrefabPaths[3]);
            var boarAnimator = boar.GetComponentInChildren<Animator>(true);
            Assert.That(boarAnimator, Is.Not.Null);
            Assert.That(boarAnimator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(boarAnimator.runtimeAnimatorController.animationClips.Length, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void SelectedThirdPartyModelsUseScopedImportPolicy()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Game/Art/ThirdParty/WizardHatStudio" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length, Is.EqualTo(7));
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), path);
                Assert.That(importer.addCollider, Is.False, path);
                Assert.That(importer.isReadable, Is.False, path);
            }
        }

        [Test]
        public void ValidationSceneIsDevelopmentOnlyAndLocalGameplayRemainsBuildScene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ValidationScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(new[] { "Assets/Scenes/LocalGameplay.unity" }));
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path == ValidationScenePath), Is.False);
        }

        [Test]
        public void RegistryCoversEveryRuntimeThirdPartyPackageAndStagingIsOutsideAssets()
        {
            Assert.That(AssetDatabase.IsValidFolder("Assets/serenity_games_assets"), Is.False);
            var registry = File.ReadAllText("docs/ASSET_REGISTRY.md");
            Assert.That(registry, Does.Contain("HODAART-LPCC3"));
            Assert.That(registry, Does.Contain("WIZARDHAT-STONEAGE-NATURE"));
            Assert.That(registry, Does.Contain("WIZARDHAT-STONEAGE-WEAPONS"));
            Assert.That(registry, Does.Contain("WIZARDHAT-STONEAGE-WILD-HUNT"));
            Assert.That(registry, Does.Contain("LICENSE_REVIEW_REQUIRED"));
        }
    }
}
