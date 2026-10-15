using System.IO;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P4AppearanceFoundation
    {
        public const string CleanUmaPrefabPath = "Assets/Game/Art/Prefabs/Characters/Serenity_UMA_Human30.prefab";

        [MenuItem("Serenity/P4/Build Ruler Editor")]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.MalePrefabPath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.FemalePrefabPath);
            var uma = AssetDatabase.LoadAssetAtPath<GameObject>(CleanUmaPrefabPath) ??
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Art/ThirdParty/UMA3/Getting Started/UMADynamicCharacterAvatar-NoPhysics_NoLOD.prefab");
            if (catalog == null || male == null || female == null || uma == null)
                throw new FileNotFoundException("P8.4 requires the presentation catalog, fallback wrappers, and admitted UMA prefab.");
            catalog.Configure(male, female, uma);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            P2StartupSceneBuilder.Build();
            Validate();
            Debug.Log("P4 ruler editor foundation generated successfully.");
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            if (catalog == null) throw new FileNotFoundException("P4 character catalog is missing.");
            var definitions = catalog.CreateDefinitionCatalog();
            if (definitions.Profiles.Count != 2 ||
                !definitions.TryGet(StoneAgeAppearanceProfiles.UmaMale, out _) ||
                !definitions.TryGet(StoneAgeAppearanceProfiles.UmaFemale, out _) || catalog.UmaAvatarPrefab == null)
                throw new InvalidDataException("P8.4 requires two editable UMA profiles and the admitted UMA prefab.");
        }
    }
}
