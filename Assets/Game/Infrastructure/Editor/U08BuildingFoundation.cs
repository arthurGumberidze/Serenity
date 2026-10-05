using System;
using System.IO;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class U08BuildingFoundation
    {
        public const string CatalogPath = U05LocalSceneBuilder.BuildingCatalogPath;
        public const string ShelterPrefabPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveShelter.prefab";
        public const string StoragePrefabPath = "Assets/Game/Art/Prefabs/Props/Serenity_StorageBasket.prefab";

        [MenuItem("Serenity/U08/Build Building Foundation")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(U05LocalSceneBuilder.ActionsPath, ImportAssetOptions.ForceUpdate);
            RefreshCatalogAndPrefabs();
            U05LocalSceneBuilder.Build();
            Validate();
            Debug.Log("U08 building foundation generated successfully.");
        }

        public static void RefreshCatalogAndPrefabs()
        {
            AddPresentationFoundation(ShelterPrefabPath, new Vector3(4f, 3.2f, 3f), new Vector3(0f, 1.6f, 0f));
            AddPresentationFoundation(StoragePrefabPath, new Vector3(1.1f, 0.9f, 0.8f), new Vector3(0f, 0.45f, 0f));

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildingPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var shelter = AssetDatabase.LoadAssetAtPath<GameObject>(ShelterPrefabPath);
            var storage = AssetDatabase.LoadAssetAtPath<GameObject>(StoragePrefabPath);
            catalog.Configure(new[]
            {
                new BuildingPresentationCatalog.Entry().Configure("primitive_shelter", "Primitive Shelter",
                    BuildingCategory.Shelter, 4, 3, shelter),
                new BuildingPresentationCatalog.Entry().Configure("storage_basket", "Storage Basket",
                    BuildingCategory.Storage, 2, 1, storage)
            }, "primitive_shelter");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(CatalogPath);
            if (catalog == null) throw new FileNotFoundException("U08 building catalog is missing.", CatalogPath);
            var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            if (characterCatalog == null) throw new FileNotFoundException("U07 character catalog is missing.");
            catalog.Validate();
            foreach (var path in new[] { ShelterPrefabPath, StoragePrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new FileNotFoundException("U08 building prefab is missing.", path);
                if (prefab.GetComponent<BuildingPresenter>() == null)
                    throw new InvalidDataException("U08 building prefab has no BuildingPresenter: " + path);
                if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidDataException("U08 building prefab has no renderer: " + path);
                if (prefab.GetComponent<Collider>() == null)
                    throw new InvalidDataException("U08 building prefab has no root collider: " + path);
            }
        }

        [MenuItem("Serenity/U08/Capture Building Validation")]
        public static void CaptureValidationScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(U05LocalSceneBuilder.ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(CatalogPath);
            if (catalog == null) throw new FileNotFoundException("U08 building catalog is missing.", CatalogPath);
            var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            if (characterCatalog == null) throw new FileNotFoundException("U07 character catalog is missing.");
            var cameraObject = new GameObject("U08 Validation Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(26f, 23f, -18f);
            camera.transform.LookAt(new Vector3(13f, 0f, 12f));
            camera.fieldOfView = 48f;
            var shelter = UnityEngine.Object.Instantiate(catalog.ResolvePrefab(new BuildingDefinitionId("primitive_shelter")),
                new Vector3(8.5f, 0f, 10f), Quaternion.identity);
            shelter.name = "Completed Primitive Shelter";
            var valid = UnityEngine.Object.Instantiate(catalog.ResolvePrefab(new BuildingDefinitionId("storage_basket")),
                new Vector3(14.5f, 0f, 11f), Quaternion.identity);
            valid.name = "Valid Storage Preview";
            var invalid = UnityEngine.Object.Instantiate(catalog.ResolvePrefab(new BuildingDefinitionId("storage_basket")),
                new Vector3(17.5f, 0f, 11f), Quaternion.Euler(0f, 90f, 0f));
            invalid.name = "Invalid Storage Preview";
            var male = UnityEngine.Object.Instantiate(characterCatalog.Resolve(CharacterSex.Male),
                new Vector3(2f, 0f, 8f), Quaternion.identity);
            var female = UnityEngine.Object.Instantiate(characterCatalog.Resolve(CharacterSex.Female),
                new Vector3(5f, 0f, 13f), Quaternion.identity);
            Tint(valid, new Color(0.25f, 1f, 0.35f, 1f));
            Tint(invalid, new Color(1f, 0.2f, 0.2f, 1f));

            Directory.CreateDirectory("Logs");
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/U08-building-validation.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(shelter);
                UnityEngine.Object.DestroyImmediate(valid);
                UnityEngine.Object.DestroyImmediate(invalid);
                UnityEngine.Object.DestroyImmediate(male);
                UnityEngine.Object.DestroyImmediate(female);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("U08 building validation captured to Logs/U08-building-validation.png.");
        }

        private static void Tint(GameObject target, Color color)
        {
            var propertyBlock = new MaterialPropertyBlock();
            var property = Shader.PropertyToID("_BaseColor");
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(property, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void AddPresentationFoundation(string path, Vector3 colliderSize, Vector3 colliderCenter)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<BuildingPresenter>() == null) root.AddComponent<BuildingPresenter>();
                var collider = root.GetComponent<BoxCollider>();
                if (collider == null) collider = root.AddComponent<BoxCollider>();
                collider.size = colliderSize;
                collider.center = colliderCenter;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
