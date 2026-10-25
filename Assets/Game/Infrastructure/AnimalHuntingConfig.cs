using System;
using System.Linq;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure
{
    [Serializable]
    public sealed class AnimalHuntingConfig
    {
        [Serializable] public sealed class Species { public string id; public int health; }
        public int arrowDamage;
        public float drawGameSeconds, flightGameSeconds, rangeMeters;
        public int accuracyPermille;
        public int pastureCapacity, livestockMaturityDays, livestockBirthDays;
        public Species[] species;
        public static AnimalHuntingPolicy Load()
        {
            var asset = Resources.Load<TextAsset>("World/AnimalHunting");
            if (asset == null) throw new InvalidOperationException("Animal hunting configuration is missing.");
            var data = JsonUtility.FromJson<AnimalHuntingConfig>(asset.text);
            return new AnimalHuntingPolicy(data.arrowDamage, TimeSpan.FromSeconds(data.drawGameSeconds).Ticks,
                TimeSpan.FromSeconds(data.flightGameSeconds).Ticks, data.rangeMeters, data.accuracyPermille,
                data.species.ToDictionary(x => x.id, x => x.health), data.pastureCapacity,
                data.livestockMaturityDays, data.livestockBirthDays);
        }
    }
}
