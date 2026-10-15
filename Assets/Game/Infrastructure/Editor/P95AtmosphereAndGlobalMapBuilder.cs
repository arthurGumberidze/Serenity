using System;
using System.IO;
using Game.Presentation.Environment;
using Game.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    /// <summary>Authors the P9.5 definition asset and connects both disposable presentation scenes.</summary>
    public static class P95AtmosphereAndGlobalMapBuilder
    {
        public const string ProfilePath = "Assets/Game/Art/Environment/P95_NaturalAtmosphere.asset";
        private const string LocalScenePath = "Assets/Scenes/LocalGameplay.unity";

        [MenuItem("Serenity/P9.5/Build Atmosphere And Natural Global Map")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = AssetDatabase.LoadAssetAtPath<P95NaturalAtmosphereProfile>(ProfilePath);
            if (profile == null)
            {
                if (File.Exists(ProfilePath)) AssetDatabase.DeleteAsset(ProfilePath);
                profile = ScriptableObject.CreateInstance<P95NaturalAtmosphereProfile>();
                profile.name = "P95 Natural Atmosphere";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.ConfigureDefaults();
            profile.ValidateConfiguration();
            EditorUtility.SetDirty(profile);

            var local = EditorSceneManager.OpenScene(LocalScenePath, OpenSceneMode.Single);
            var root = UnityEngine.Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            if (root == null) throw new InvalidOperationException("LocalGameplay requires a composition root.");
            var serializedRoot = new SerializedObject(root);
            serializedRoot.FindProperty("naturalAtmosphereProfile").objectReferenceValue = profile;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(local);
            EditorSceneManager.SaveScene(local, LocalScenePath);

            var global = EditorSceneManager.OpenScene(U25GlobalMapSceneBuilder.ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<GlobalMapPresentationController>();
            if (controller == null) throw new InvalidOperationException("GlobalMap requires a presentation controller.");
            var camera = controller.MapCamera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .2f, .24f);
            camera.fieldOfView = 39f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 260f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 165f, -38f), Quaternion.Euler(60f, 0f, 0f));
            var plane = GameObject.Find("Schematic Map Plane");
            if (plane != null)
            {
                plane.transform.position = new Vector3(0f, 99.45f, 0f);
                plane.transform.localScale = new Vector3(9f, 1f, 7f);
            }
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .72f;
            }
            EditorSceneManager.MarkSceneDirty(global);
            EditorSceneManager.SaveScene(global, U25GlobalMapSceneBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("P95_ATMOSPHERE_GLOBAL_MAP_BUILT profile=" + profile.ComputeLayoutHash());
        }
    }
}
