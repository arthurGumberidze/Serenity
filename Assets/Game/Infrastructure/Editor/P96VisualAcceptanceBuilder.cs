using System;
using System.IO;
using System.Linq;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P96VisualAcceptanceBuilder
    {
        private const string SourceUmaPrefabPath =
            "Assets/Game/Art/ThirdParty/UMA3/Getting Started/UMADynamicCharacterAvatar-NoPhysics_NoLOD.prefab";

        [MenuItem("Serenity/P9.6/Build Clean UMA Avatar")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceUmaPrefabPath);
            if (source == null) throw new FileNotFoundException("Admitted UMA source prefab is missing.", SourceUmaPrefabPath);

            var directory = Path.GetDirectoryName(P4AppearanceFoundation.CleanUmaPrefabPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Clean UMA path has no directory.");
            Directory.CreateDirectory(directory);

            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate the admitted UMA prefab.");
            try
            {
                instance.name = "Serenity_UMA_Human30";
                var removed = 0;
                foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                if (removed == 0)
                    throw new InvalidDataException("Expected the excluded UMA sample walker to be removed from the clean prefab.");
                if (!PrefabUtility.SaveAsPrefabAsset(instance, P4AppearanceFoundation.CleanUmaPrefabPath, out var saved) || !saved)
                    throw new IOException("Could not save the clean UMA prefab.");
                Debug.Log($"P96_CLEAN_UMA_PREFAB removed_missing_scripts={removed}; path={P4AppearanceFoundation.CleanUmaPrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(U05LocalSceneBuilder.CharacterCatalogPath);
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.MalePrefabPath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.FemalePrefabPath);
            var cleanUma = AssetDatabase.LoadAssetAtPath<GameObject>(P4AppearanceFoundation.CleanUmaPrefabPath);
            if (catalog == null || male == null || female == null || cleanUma == null)
                throw new FileNotFoundException("P9.6 could not resolve the Character presentation assets.");
            catalog.Configure(male, female, cleanUma);
            EditorUtility.SetDirty(catalog);
            RegisterWardrobeAssets();
            AssetDatabase.SaveAssets();
        }

        private static void RegisterWardrobeAssets()
        {
            var indexerType = Type.GetType("UMA.UMAAssetIndexer, UMA_Core", true);
            var overlayType = Type.GetType("UMA.OverlayDataAsset, UMA_Core", true);
            var recipeType = Type.GetType("UMA.CharacterSystem.UMAWardrobeRecipe, UMA_Core", true);
            var indexer = indexerType.GetProperty("Instance")?.GetValue(null) ??
                throw new InvalidOperationException("UMA asset indexer is unavailable.");
            var overlay = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Game/Art/ThirdParty/UMA3/Wearables/Medieval/ChallengerTorsoArmorLeather.asset");
            var femaleRecipe = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Game/Art/Config/P96/F_StoneAgeHideTunic_Recipe.asset");
            var maleRecipe = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Game/Art/Config/P96/M_StoneAgeHideTunic_Recipe.asset");
            if (overlay == null || femaleRecipe == null || maleRecipe == null)
                throw new FileNotFoundException("P9.6 Stone Age UMA wardrobe assets are missing.");
            var add = indexerType.GetMethod("EvilAddAsset", new[] { typeof(Type), typeof(UnityEngine.Object) }) ??
                throw new MissingMethodException(indexerType.FullName, "EvilAddAsset");
            add.Invoke(indexer, new object[] { overlayType, overlay });
            add.Invoke(indexer, new object[] { recipeType, femaleRecipe });
            add.Invoke(indexer, new object[] { recipeType, maleRecipe });
            indexerType.GetMethod("ForceSave", Type.EmptyTypes)?.Invoke(indexer, null);
            var getAsset = indexerType.GetMethods().First(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "GetAsset" && method.IsGenericMethodDefinition && parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(bool) &&
                    parameters[2].ParameterType == typeof(bool);
            }).MakeGenericMethod(recipeType);
            if (getAsset.Invoke(indexer, new object[] { femaleRecipe.name, false, false }) == null ||
                getAsset.Invoke(indexer, new object[] { maleRecipe.name, false, false }) == null)
                throw new InvalidDataException("P9.6 Stone Age UMA wardrobe recipes were not indexed.");
        }
    }
}
