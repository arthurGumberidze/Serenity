using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.World
{
    public readonly struct WorldMapIntRange
    {
        public WorldMapIntRange(int minimum, int maximum)
        {
            if (minimum < 0 || maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
        }

        public int Minimum { get; }
        public int Maximum { get; }
        public int Midpoint => Minimum + (Maximum - Minimum) / 2;
        public bool Contains(int value) => value >= Minimum && value <= Maximum;
    }

    public sealed class WorldMapWildlifeSpeciesDefinition
    {
        public WorldMapWildlifeSpeciesDefinition(string id, string name, IEnumerable<string> suitableBiomes,
            WorldMapIntRange targetPopulation, WorldMapIntRange groupSize, string activity)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(activity) || targetPopulation.Minimum <= 0 || groupSize.Minimum <= 0 ||
                groupSize.Maximum > targetPopulation.Maximum)
                throw new ArgumentException(nameof(id));
            var biomes = (suitableBiomes ?? throw new ArgumentNullException(nameof(suitableBiomes))).ToArray();
            if (biomes.Length == 0 || biomes.Any(string.IsNullOrWhiteSpace) ||
                biomes.Distinct(StringComparer.Ordinal).Count() != biomes.Length)
                throw new ArgumentException("Suitable biome IDs must be non-empty and unique.", nameof(suitableBiomes));
            Id = id;
            Name = name;
            SuitableBiomes = Array.AsReadOnly(biomes);
            TargetPopulation = targetPopulation;
            GroupSize = groupSize;
            Activity = activity;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> SuitableBiomes { get; }
        public WorldMapIntRange TargetPopulation { get; }
        public WorldMapIntRange GroupSize { get; }
        public string Activity { get; }
    }

    /// <summary>Immutable authored wildlife definition. Mutable populations live in WildlifePopulationService.</summary>
    public sealed class WorldMapWildlifeDefinition
    {
        public WorldMapWildlifeDefinition(IEnumerable<WorldMapWildlifeSpeciesDefinition> species,
            IEnumerable<WorldMapMask> concentrationZones, string globalRule, string movement,
            string migration, string reproduction, string huntingPressure, bool timeOfDay,
            WorldMapIntRange canonicalPopulationTarget, WorldMapIntRange tier1VisibleCap,
            WorldMapIntRange tier2ActiveCap)
        {
            var speciesValues = (species ?? throw new ArgumentNullException(nameof(species))).ToArray();
            var zones = (concentrationZones ?? throw new ArgumentNullException(nameof(concentrationZones))).ToArray();
            if (speciesValues.Length == 0 || speciesValues.Any(value => value == null) ||
                speciesValues.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != speciesValues.Length)
                throw new ArgumentException("Wildlife species require unique stable IDs.", nameof(species));
            if (zones.Any(value => value == null) ||
                zones.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != zones.Length)
                throw new ArgumentException("Wildlife zones require unique stable IDs.", nameof(concentrationZones));
            if (zones.Any(zone => zone.References.Any(reference => speciesValues.All(value => value.Id != reference))))
                throw new ArgumentException("Wildlife zones may reference only authored species.", nameof(concentrationZones));
            if (string.IsNullOrWhiteSpace(globalRule) || movement != "habitat_graph_between_feeding_rest_water_nodes" ||
                migration != "season_and_pressure_weighted" ||
                reproduction != "population_tick_with_carrying_capacity" ||
                huntingPressure != "reduces_local_population_and_increases_avoidance" || !timeOfDay)
                throw new ArgumentException("Wildlife simulation contract is incomplete.");
            Species = Array.AsReadOnly(speciesValues);
            ConcentrationZones = Array.AsReadOnly(zones);
            GlobalRule = globalRule;
            Movement = movement;
            Migration = migration;
            Reproduction = reproduction;
            HuntingPressure = huntingPressure;
            TimeOfDay = timeOfDay;
            CanonicalPopulationTarget = canonicalPopulationTarget;
            Tier1VisibleCap = tier1VisibleCap;
            Tier2ActiveCap = tier2ActiveCap;
        }

        public IReadOnlyList<WorldMapWildlifeSpeciesDefinition> Species { get; }
        public IReadOnlyList<WorldMapMask> ConcentrationZones { get; }
        public string GlobalRule { get; }
        public string Movement { get; }
        public string Migration { get; }
        public string Reproduction { get; }
        public string HuntingPressure { get; }
        public bool TimeOfDay { get; }
        public WorldMapIntRange CanonicalPopulationTarget { get; }
        public WorldMapIntRange Tier1VisibleCap { get; }
        public WorldMapIntRange Tier2ActiveCap { get; }

        public WorldMapWildlifeSpeciesDefinition SpeciesById(string id) =>
            Species.FirstOrDefault(value => string.Equals(value.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException("Unknown wildlife species: " + id);
    }
}
