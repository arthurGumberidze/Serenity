using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests.Editor
{
    public sealed class P109SyntyImportTests
    {
        private const string Root = "Assets/PolygonNatureBiomes";

        [Test]
        public void ApprovedForestModelsAndPrefabsAreAvailableWithoutExecutableCode()
        {
            Assert.That(AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Length, Is.GreaterThanOrEqualTo(190));
            Assert.That(Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories).Length, Is.EqualTo(226));
            Assert.That(Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetFiles(Root, "*.dll", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.Exists(Root + "/PNB_Tropical_Jungle"), Is.False);
            Assert.That(Directory.Exists(Root + "/PNB_Swamp_Marshland"), Is.False);
        }

        [Test]
        public void MaterialsResolveShadersAndVendorShadersUseUrp()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader, Is.Not.Null, path);
                Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), path);
                // EditMode may not have an active render pipeline; Material.GetTag
                // then inspects the fallback. Inspect the declared subshaders instead.
                Assert.That(Enumerable.Range(0, material.shader.subshaderCount).Any(index =>
                    material.shader.FindSubshaderTagValue(index, new ShaderTagId("RenderPipeline")).name ==
                    "UniversalPipeline"), Is.True, path);
            }
            foreach (var path in Directory.GetFiles(Root, "*.shader", SearchOption.AllDirectories))
            {
                Assert.That(File.ReadAllText(path), Does.Contain("com.unity.render-pipelines.universal"), path);
                Assert.That(ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path)), Is.False, path);
            }
        }

        [Test]
        public void DemoIsAvailableButDoesNotReplaceGameplayBuildScenes()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/PNB_Meadow_Forest/Scene/Demo.unity"), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.enabled && x.path.StartsWith(Root)), Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.enabled && x.path.EndsWith("LocalGameplay.unity")), Is.True);
        }
    }
}
