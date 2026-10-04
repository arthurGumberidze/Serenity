using System;
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
            public CharacterSex Sex;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public void Configure(GameObject malePrefab, GameObject femalePrefab)
        {
            entries = new[]
            {
                new Entry { Sex = CharacterSex.Male, Prefab = malePrefab },
                new Entry { Sex = CharacterSex.Female, Prefab = femalePrefab }
            };
        }

        public GameObject Resolve(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            return Resolve(character.Sex);
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
    }
}
