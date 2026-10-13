using System.IO;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P3")]
    public sealed class P3ReferenceCampTests
    {
        [Test]
        public void LocalGameplayContainsAuthoredReferenceCampAndKeepsMenuFirst()
        {
            var buildScenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            Assert.That(buildScenes, Is.EqualTo(new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/LocalGameplay.unity",
                "Assets/Scenes/GlobalMap.unity"
            }));

            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            var camp = root != null ? root.GetComponent<StoneAgeReferenceCampPresentation>() : null;
            Assert.That(root, Is.Not.Null);
            Assert.That(camp, Is.Not.Null);
            Assert.That(camp.CampfireAnchor, Is.Not.Null);
            Assert.That(camp.WaterRenderer, Is.Not.Null);
            Assert.That(camp.RainParticleMaterial, Is.Not.Null);
            Assert.That(camp.ShelterSurfaceMaterial, Is.Not.Null);
            Assert.That(camp.StorageSurfaceMaterial, Is.Not.Null);
            Assert.That(camp.TreeCount, Is.GreaterThanOrEqualTo(14));
            Assert.That(camp.RockCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(camp.GrassClusterCount, Is.GreaterThanOrEqualTo(36));
            Assert.That(GameObject.Find("P3 Buildable Forest Clearing"), Is.Not.Null);
            Assert.That(GameObject.Find("P3 Curved Shore"), Is.Not.Null);
            Assert.That(GameObject.Find("P3 Water Edge"), Is.Not.Null);
            Assert.That(GameObject.Find("P3 Reference World"), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)
                .Where(x => x.enabled).All(x => x.sharedMaterial != null), Is.True);
        }

        [Test]
        public void ReferenceCampUsesOnlyApprovedRuntimeOrProjectOwnedArt()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var allowed = new[]
            {
                "Assets/Game/Art/Prefabs/Environment/Serenity_Tree.prefab",
                "Assets/Game/Art/Prefabs/Environment/Serenity_Rock.prefab",
                "Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab",
                "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Tree_01.fbx",
                "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Rock_01.fbx",
                "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Campfire_01.fbx"
            };
            var world = GameObject.Find("P3 Reference World");
            Assert.That(world, Is.Not.Null);
            var prefabPaths = world.GetComponentsInChildren<Transform>(true)
                .Select(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x.gameObject))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            Assert.That(prefabPaths, Is.SubsetOf(allowed));
            Assert.That(prefabPaths, Does.Contain(allowed[0]));
            Assert.That(prefabPaths, Does.Contain(allowed[1]));
            Assert.That(prefabPaths, Does.Contain(allowed[2]));

            var registry = File.ReadAllText(Path.GetFullPath("docs/ASSET_REGISTRY.md"));
            Assert.That(registry, Does.Contain("WIZARDHAT-STONEAGE-NATURE"));
            Assert.That(registry, Does.Contain("APPROVED_RUNTIME"));
            Assert.That(registry, Does.Contain("SERENITY-PRIMITIVE-PLACEHOLDERS"));
        }

        [Test]
        public void P3MeshesAndMaterialsAreReusableProjectAssets()
        {
            var required = new[]
            {
                "Assets/Game/Art/Meshes/P3/P3_Shoreline.asset",
                "Assets/Game/Art/Meshes/P3/P3_WaterPlane.asset",
                "Assets/Game/Art/Meshes/P3/P3_GrassCluster.asset",
                "Assets/Game/Art/Materials/P3/P3_Ground.mat",
                "Assets/Game/Art/Materials/P3/P3_Shore.mat",
                "Assets/Game/Art/Materials/P3/P3_Water.mat",
                "Assets/Game/Art/Materials/P3/P3_Grass.mat",
                "Assets/Game/Art/Materials/P3/P3_Skybox.mat",
                "Assets/Game/Art/Materials/P3/P3_Flame.mat",
                "Assets/Game/Art/Materials/P3/P3_Smoke.mat",
                "Assets/Game/Art/Materials/P3/P3_Rain.mat",
                "Assets/Game/Art/Materials/P3/P3_SoftParticle.asset",
                "Assets/Game/Art/Materials/P3/P3_ShelterHide.mat",
                "Assets/Game/Art/Materials/P3/P3_StorageWicker.mat"
            };
            foreach (var path in required)
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
            var water = AssetDatabase.LoadAssetAtPath<Material>(required[5]);
            Assert.That(water.shader.name, Does.Contain("Universal Render Pipeline/Lit"));
            Assert.That(water.renderQueue, Is.GreaterThanOrEqualTo(3000));
        }
    }
}
