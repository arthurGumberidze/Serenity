using System.IO;
using Game.Presentation.CameraControl;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Editor
{
    public static class U05LocalSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        public const string ActionsPath = "Assets/Game/Presentation/Input/LocalGameplay.inputactions";

        public static void Build()
        {
            var actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actionAsset == null)
                throw new FileNotFoundException("Local gameplay input actions were not imported.", ActionsPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LocalGameplay";

            var compositionObject = new GameObject("Local Scene Composition");
            var input = compositionObject.AddComponent<LocalGameplayInputSource>();
            input.Configure(actionAsset);
            var raycaster = compositionObject.AddComponent<WorldPointerRaycaster>();
            var selection = compositionObject.AddComponent<SelectionProbe>();
            var composition = compositionObject.AddComponent<LocalSceneCompositionRoot>();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground Placeholder";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            CreateMarker(new Vector3(-8f, 1f, 2f), new Vector3(2f, 2f, 2f), "Selection Marker A");
            CreateMarker(new Vector3(0f, 1.5f, 8f), new Vector3(2.5f, 3f, 2.5f), "Selection Marker B");
            CreateMarker(new Vector3(9f, 1f, -4f), new Vector3(2f, 2f, 2f), "Selection Marker C");

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = light;

            var outputCameraObject = new GameObject("Main Camera");
            outputCameraObject.tag = "MainCamera";
            var outputCamera = outputCameraObject.AddComponent<UnityEngine.Camera>();
            outputCamera.nearClipPlane = 0.1f;
            outputCamera.farClipPlane = 1000f;
            outputCameraObject.AddComponent<AudioListener>();
            outputCameraObject.AddComponent<CinemachineBrain>();

            var rig = new GameObject("RTS Camera Rig");
            rig.transform.position = new Vector3(0f, 0f, 0f);
            var controller = rig.AddComponent<RtsCameraController>();
            var virtualCameraObject = new GameObject("RTS Cinemachine Camera");
            virtualCameraObject.transform.SetParent(rig.transform, false);
            var virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Lens.FieldOfView = 55f;
            var settings = new RtsCameraSettings
            {
                MoveSpeed = 18f,
                EdgeScrollSpeed = 18f,
                EdgeScrollEnabled = true,
                EdgeWidthNormalized = 0.02f,
                DragPanSpeed = 0.8f,
                RotationSpeed = 90f,
                ZoomSpeed = 8f,
                ZoomSmoothing = 35f,
                MinZoom = 8f,
                MaxZoom = 45f,
                PitchDegrees = 50f,
                BoundsMin = new Vector2(-45f, -45f),
                BoundsMax = new Vector2(45f, 45f)
            };
            controller.Configure(input, virtualCameraObject.transform, settings, 25f);

            raycaster.Configure(outputCamera, Physics.DefaultRaycastLayers);
            selection.Configure(input, raycaster);
            composition.Configure(input, controller, raycaster, selection);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("U05 LocalGameplay scene generated.");
        }

        private static void CreateMarker(Vector3 position, Vector3 scale, string name)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = scale;
            marker.AddComponent<SelectableMarker>();
        }
    }
}
