using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace Game.Tests
{
    public class PackageConfigurationTests
    {
        [TestCase("com.unity.entities", "6.6.0")]
        [TestCase("com.unity.burst", "2.0.0")]
        [TestCase("com.unity.collections", "6.6.0")]
        [TestCase("com.unity.mathematics", "1.4.0")]
        [TestCase("com.unity.cinemachine", "6.6.0")]
        [TestCase("com.unity.addressables", "2.11.2")]
        [TestCase("com.unity.inputsystem", "1.20.0")]
        [TestCase("com.unity.ai.navigation", "2.0.12")]
        [TestCase("com.unity.render-pipelines.universal", "17.6.0")]
        [TestCase("com.unity.test-framework", "1.8.0")]
        public void RequiredPackageIsInstalledAtPinnedVersion(string name, string version)
        {
            var package = PackageInfo.GetAllRegisteredPackages().Single(p => p.name == name);
            Assert.That(package.version, Is.EqualTo(version), name);
            Assert.That(package.isDirectDependency, Is.True, name);
            Assert.That(Directory.Exists(package.resolvedPath), Is.True, name);
        }

        [Test]
        public void RequiredRuntimePackageAssembliesParticipateInPlayerCompilation()
        {
            var names = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToArray();
            foreach (var name in new[] { "Unity.Entities", "Unity.Collections", "Unity.Cinemachine",
                "Unity.Addressables", "Unity.ResourceManager", "Unity.InputSystem", "Unity.AI.Navigation" })
                Assert.That(names, Does.Contain(name));
            // Jobs, Mathematics and Burst APIs ship in Editor engine modules in Unity 6.6.
            Assert.That(typeof(Unity.Jobs.JobHandle).Assembly, Is.Not.Null);
            Assert.That(typeof(Unity.Mathematics.float3).Assembly, Is.Not.Null);
            Assert.That(typeof(Unity.Burst.BurstCompileAttribute).Assembly, Is.Not.Null);
        }

        [Test]
        public void BaselineSettingsMatchWindowsHybridProfile()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone),
                Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            Assert.That(settings.FindProperty("activeInputHandler").intValue, Is.EqualTo(1), "Input System only");
        }
    }
}
