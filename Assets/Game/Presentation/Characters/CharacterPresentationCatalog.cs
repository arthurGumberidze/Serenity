using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    [CreateAssetMenu(menuName = "Serenity/Character Presentation Catalog", fileName = "CharacterPresentationCatalog")]
    public sealed class CharacterPresentationCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string ProfileId;
            public CharacterSex Sex;
            public string BodyId;
            public string FaceId;
            public string SkinId;
            public string HairId;
            public string BeardId;
            public string HairColorId;
            public string ClothingId;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private GameObject umaAvatarPrefab;
        private readonly HashSet<string> reportedFallbacks = new HashSet<string>(StringComparer.Ordinal);

        public void Configure(GameObject malePrefab, GameObject femalePrefab, GameObject umaPrefab = null)
        {
            var definitions = StoneAgeAppearanceProfiles.CreateCatalog();
            entries = new[]
            {
                EntryFor(definitions.Get(StoneAgeAppearanceProfiles.UmaMale), malePrefab),
                EntryFor(definitions.Get(StoneAgeAppearanceProfiles.UmaFemale), femalePrefab)
            };
            umaAvatarPrefab = umaPrefab;
        }

        public AppearanceDefinitionCatalog CreateDefinitionCatalog() => StoneAgeAppearanceProfiles.CreateCatalog();
        public GameObject UmaAvatarPrefab => umaAvatarPrefab;

        public GameObject Resolve(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            return Resolve(character.Sex);
        }

        public GameObject Resolve(Character character, AppearanceState appearance, out AppearanceState resolvedAppearance)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            return Resolve(character.Sex, appearance, out resolvedAppearance);
        }

        public GameObject Resolve(CharacterSex sex, AppearanceState appearance, out AppearanceState resolvedAppearance)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            var catalog = CreateDefinitionCatalog();
            if (catalog.TryGet(appearance.ProfileId, out var requested) && requested.Sex == sex)
            {
                var exact = FindEntry(requested.Id);
                ValidatePrefab(exact.Prefab, exact.ProfileId);
                resolvedAppearance = appearance;
                return exact.Prefab;
            }

            var fallback = catalog.DefaultFor(sex);
            var fallbackEntry = FindEntry(fallback.Id);
            ValidatePrefab(fallbackEntry.Prefab, fallbackEntry.ProfileId);
            resolvedAppearance = fallback.CreateState(appearance.CharacterId);
            var key = appearance.ProfileId + "->" + fallback.Id;
            if (reportedFallbacks.Add(key))
                Debug.LogWarning("Appearance profile '" + appearance.ProfileId + "' is unavailable or incompatible; using '" +
                                 fallback.Id + "'. Canonical saved IDs were preserved.");
            return fallbackEntry.Prefab;
        }

        public GameObject Resolve(CharacterSex sex)
        {
            if (!Enum.IsDefined(typeof(CharacterSex), sex))
                throw new ArgumentOutOfRangeException(nameof(sex), "Unsupported character sex for Tier 1 presentation.");
            if (entries == null) throw new InvalidOperationException("Character presentation catalog is not configured.");

            var matchCount = 0;
            GameObject match = null;
            foreach (var entry in entries)
            {
                if (entry.Sex != sex) continue;
                matchCount++;
                match = entry.Prefab;
            }
            if (matchCount != 1)
                throw new InvalidOperationException("Character presentation catalog must contain exactly one entry for " + sex + ".");
            if (match == null)
                throw new InvalidOperationException("Character presentation prefab is missing for " + sex + ".");
            if (match.GetComponent<CharacterPresenter>() == null)
                throw new InvalidOperationException("Character presentation prefab has no CharacterPresenter: " + match.name + ".");
            return match;
        }

        private Entry FindEntry(AppearanceProfileId profileId)
        {
            var matches = entries.Where(x => string.Equals(x.ProfileId, profileId.Value, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Character presentation catalog must contain exactly one entry for " + profileId + ".");
            return matches[0];
        }

        private static Entry EntryFor(AppearanceProfileDefinition definition, GameObject prefab) => new Entry
        {
            ProfileId = definition.Id.Value,
            Sex = definition.Sex,
            BodyId = definition.BodyId.Value,
            FaceId = definition.FaceId.Value,
            SkinId = definition.SkinId.Value,
            HairId = definition.HairId.Value,
            BeardId = definition.BeardId.Value,
            HairColorId = definition.HairColorId.Value,
            ClothingId = definition.ClothingId.Value,
            Prefab = prefab
        };

        private static AppearanceProfileDefinition Definition(Entry entry) => new AppearanceProfileDefinition(
            new AppearanceProfileId(entry.ProfileId), entry.Sex, new AppearancePartId(entry.BodyId),
            new AppearancePartId(entry.FaceId), new AppearancePartId(entry.SkinId),
            new AppearancePartId(entry.HairId), new AppearancePartId(entry.BeardId),
            new AppearancePartId(entry.HairColorId), new AppearancePartId(entry.ClothingId));

        private static void ValidatePrefab(GameObject prefab, string profileId)
        {
            if (prefab == null) throw new InvalidOperationException("Character presentation prefab is missing for " + profileId + ".");
            if (prefab.GetComponent<CharacterPresenter>() == null)
                throw new InvalidOperationException("Character presentation prefab has no CharacterPresenter: " + prefab.name + ".");
        }
    }
}
