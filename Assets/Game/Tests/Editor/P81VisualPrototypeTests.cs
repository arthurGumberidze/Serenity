using System.IO;
using System.Linq;
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
        public void SceneContainsScopedNaturalPbrBenchmark()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var prototype = Object.FindAnyObjectByType<P81VisualPrototypePresentation>();
            Assert.That(prototype, Is.Not.Null);
            Assert.DoesNotThrow(prototype.ValidateConfiguration);
            Assert.That(prototype.EnvironmentRenderers.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(prototype.TrunkRenderers.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(prototype.EnvironmentRenderers.All(x => x.sharedMaterial != null &&
                x.sharedMaterial.shader.name.Contains("Lit")), Is.True);
            Assert.That(prototype.TrunkRenderers.All(x => x.sharedMaterial.color.r >
                x.sharedMaterial.color.b * 2f), Is.True);
        }

        [Test]
        public void AppearanceIsStableDataAndVisualProviderHasFallback()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var prototype = Object.FindAnyObjectByType<P81VisualPrototypePresentation>();
            var appearance = prototype.CanonicalAppearance;
            Assert.That(appearance.CharacterId.ToString(), Is.EqualTo("81000000000000000000000000000001"));
            Assert.That(appearance.ProfileId.Value, Is.EqualTo("uma_human_male_30"));
            Assert.That(prototype.HasVisibleCharacter, Is.True);
            Assert.That(GameObject.Find("P81 Project Character Fallback"), Is.Not.Null);
            Assert.That(GameObject.Find("P81 UMA Character Prototype"), Is.Not.Null);
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
