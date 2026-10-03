using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EnvironmentTests
{
    [Test]
    public void EditorAndWindowsSupportMatchTarget()
    {
        Assert.That(Application.unityVersion, Is.EqualTo("6000.6.4f1"));
        Assert.That(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows64), Is.True);
    }

    [Test]
    public void DefaultAndQualityPipelinesUseUrp()
    {
        Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
        for (var index = 0; index < QualitySettings.names.Length; index++)
        {
            var pipeline = QualitySettings.GetRenderPipelineAssetAt(index);
            if (pipeline != null)
                Assert.That(pipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
        }
    }

    [Test]
    public void EnabledScenesLoadWithCameraAndWithoutMissingScripts()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Assert.That(scenes, Is.Not.Empty);
        foreach (var entry in scenes)
        {
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Assert.That(transforms.Any(t => t.GetComponent<Camera>() != null), Is.True, entry.path);
            foreach (var transform in transforms)
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                    Is.Zero, entry.path + ": " + transform.name);
        }
    }
}
