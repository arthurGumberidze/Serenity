using System.IO;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P4AppearanceFoundation
    {
        [MenuItem("Serenity/P4/Build Ruler Editor")]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                U05LocalSceneBuilder.CharacterCatalogPath);
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.MalePrefabPath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(U07PresentationFoundation.FemalePrefabPath);
            if (catalog == null || male == null || female == null)
                throw new FileNotFoundException("P4 requires the approved U07 presentation catalog and both wrapper prefabs.");
            catalog.Configure(male, female);
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
                !definitions.TryGet(StoneAgeAppearanceProfiles.AdultA, out _) ||
                !definitions.TryGet(StoneAgeAppearanceProfiles.AdultB, out _))
                throw new InvalidDataException("P4 requires exactly the two approved integrated appearance profiles.");
        }
    }
}
