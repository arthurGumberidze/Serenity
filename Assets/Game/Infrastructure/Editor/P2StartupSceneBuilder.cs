using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Game.Presentation.Characters;

namespace Game.Infrastructure.Editor
{
    public static class P2StartupSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Serenity/P2/Build Startup Scene")]
        public static void Build()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(U05LocalSceneBuilder.ActionsPath);
            if (actions == null) throw new FileNotFoundException("Local gameplay input actions are required.",
                U05LocalSceneBuilder.ActionsPath);
            if (!File.Exists(U05LocalSceneBuilder.ScenePath))
                throw new FileNotFoundException("Local gameplay scene is required.", U05LocalSceneBuilder.ScenePath);
            if (!File.Exists(U25GlobalMapSceneBuilder.ScenePath))
                throw new FileNotFoundException("Global map scene is required.", U25GlobalMapSceneBuilder.ScenePath);
            var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            if (characterCatalog == null)
                throw new FileNotFoundException("Character presentation catalog is required.",
                    U05LocalSceneBuilder.CharacterCatalogPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";
            var menuObject = new GameObject("Main Menu");
            menuObject.AddComponent<MainMenuController>().Configure(actions, characterCatalog);

            var cameraObject = new GameObject("Menu Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.035f, 1f);
            cameraObject.AddComponent<AudioListener>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(U05LocalSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(U25GlobalMapSceneBuilder.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("P2 MainMenu scene generated and set as the startup scene.");
        }
    }
}
