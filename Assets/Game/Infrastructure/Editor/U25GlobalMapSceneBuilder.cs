using System.IO;
using System.Linq;
using Game.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Editor
{
    public static class U25GlobalMapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GlobalMap.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GlobalMap";

            var root = new GameObject("Global Map Presentation");
            var markerRoot = new GameObject("Map Markers").transform;
            markerRoot.SetParent(root.transform, false);
            var controller = root.AddComponent<GlobalMapPresentationController>();

            var mapPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            mapPlane.name = "Schematic Map Plane";
            mapPlane.transform.SetParent(root.transform, false);
            mapPlane.transform.position = new Vector3(0f, 100f, 0f);
            mapPlane.transform.localScale = new Vector3(9f, 1f, 7f);
            var planeRenderer = mapPlane.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.12f, 0.2f, 0.16f));
            block.SetColor("_Color", new Color(0.12f, 0.2f, 0.16f));
            planeRenderer.SetPropertyBlock(block);

            var cameraObject = new GameObject("Global Map Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.07f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 90f;
            camera.transform.position = new Vector3(0f, 145f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            controller.Configure(camera, markerRoot);

            var lightObject = new GameObject("Global Map Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(x => x.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else
                scenes = scenes.Select(x => x.path == ScenePath ? new EditorBuildSettingsScene(ScenePath, true) : x).ToList();
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("U25 GlobalMap scene generated.");
        }
    }
}
