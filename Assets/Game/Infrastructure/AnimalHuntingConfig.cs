using System;
using System.Linq;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure
{
    [Serializable]
    public sealed class AnimalHuntingConfig
    {
        [Serializable] public sealed class Species { public string id; public int health; public float fleeSpeed = 3f, fleeDistance = 18f, fleeGameSeconds = 360f; }
        public int arrowDamage;
        public float drawGameSeconds, flightGameSeconds, rangeMeters;
        public int accuracyPermille;
        public int pastureCapacity, livestockMaturityDays, livestockBirthDays;
        public Species[] species;
        public string[] domesticSpecies;
        public static System.Collections.Generic.IReadOnlyDictionary<string, WildlifeEscapePolicy> LoadEscape()
        {
            var data = JsonUtility.FromJson<AnimalHuntingConfig>(Resources.Load<TextAsset>("World/AnimalHunting").text);
            return data.species.ToDictionary(x=>x.id,x=>new WildlifeEscapePolicy(x.fleeSpeed,x.fleeDistance,
                TimeSpan.FromSeconds(x.fleeGameSeconds).Ticks));
        }
        public static AnimalHuntingPolicy Load()
        {
            var asset = Resources.Load<TextAsset>("World/AnimalHunting");
            if (asset == null) throw new InvalidOperationException("Animal hunting configuration is missing.");
            var data = JsonUtility.FromJson<AnimalHuntingConfig>(asset.text);
            return new AnimalHuntingPolicy(data.arrowDamage, TimeSpan.FromSeconds(data.drawGameSeconds).Ticks,
                TimeSpan.FromSeconds(data.flightGameSeconds).Ticks, data.rangeMeters, data.accuracyPermille,
                data.species.ToDictionary(x => x.id, x => x.health), data.pastureCapacity,
                data.livestockMaturityDays, data.livestockBirthDays, data.domesticSpecies);
        }
    }
}
