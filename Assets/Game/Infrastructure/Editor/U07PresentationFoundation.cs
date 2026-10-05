using System;
using System.IO;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class U07PresentationFoundation
    {
        public const string CatalogPath = U05LocalSceneBuilder.CharacterCatalogPath;
        public const string MalePrefabPath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab";
        public const string FemalePrefabPath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_B_Tier1.prefab";

        [MenuItem("Serenity/U07/Build Tier 1 Presentation")]
        public static void Build()
        {
            U05AAssetFoundation.Build();
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePrefabPath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePrefabPath);
            if (male == null || female == null)
                throw new FileNotFoundException("U07 character wrapper prefabs are missing.");
            catalog.Configure(male, female);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U05LocalSceneBuilder.BuildingCatalogPath) != null)
                U08BuildingFoundation.RefreshCatalogAndPrefabs();

            U05LocalSceneBuilder.Build();
            Validate();
            Debug.Log("U07 Tier 1 presentation generated successfully.");
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(CatalogPath);
            if (catalog == null) throw new FileNotFoundException("U07 catalog is missing.", CatalogPath);
            foreach (var path in new[] { MalePrefabPath, FemalePrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new FileNotFoundException("U07 prefab is missing.", path);
                var presenter = prefab.GetComponent<CharacterPresenter>();
                if (presenter == null) throw new InvalidDataException("U07 prefab has no CharacterPresenter: " + path);
                if (presenter.Animator == null || presenter.Animator.avatar == null || !presenter.Animator.avatar.isValid)
                    throw new InvalidDataException("U07 prefab has no valid Animator/Avatar: " + path);
                if (prefab.GetComponent<CapsuleCollider>() == null)
                    throw new InvalidDataException("U07 prefab has no capsule collider: " + path);
                if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidDataException("U07 prefab has no renderer: " + path);
            }
        }

    }
}
