using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Resources;
using Game.Domain.Weather;
using Game.Simulation.Combat;
using Game.Simulation.Production;
using Game.Simulation.Resources;

namespace Game.Simulation.Buildings
{
    public sealed class BuildingFireWeatherEffect
    {
        public BuildingFireWeatherEffect(WeatherType weatherType, int damagePerThousand, int intensityDelta)
        {
            if (!Enum.IsDefined(typeof(WeatherType), weatherType)) throw new ArgumentOutOfRangeException(nameof(weatherType));
            if (damagePerThousand <= 0 || damagePerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(damagePerThousand));
            if (intensityDelta < -1000 || intensityDelta > 1000)
                throw new ArgumentOutOfRangeException(nameof(intensityDelta));
            WeatherType = weatherType;
            DamagePerThousand = damagePerThousand;
            IntensityDelta = intensityDelta;
        }

        public WeatherType WeatherType { get; }
        public int DamagePerThousand { get; }
        public int IntensityDelta { get; }
    }

    public sealed class BuildingFireSettings
    {
        private readonly IReadOnlyDictionary<WeatherType, BuildingFireWeatherEffect> effects;

        public BuildingFireSettings(long stepTicks, int baseIntensityDecay, int baseDamageDivisor,
            IEnumerable<BuildingFireWeatherEffect> weatherEffects = null)
        {
            if (stepTicks <= 0) throw new ArgumentOutOfRangeException(nameof(stepTicks));
            if (baseIntensityDecay <= 0 || baseIntensityDecay > 1000)
                throw new ArgumentOutOfRangeException(nameof(baseIntensityDecay));
            if (baseDamageDivisor <= 0) throw new ArgumentOutOfRangeException(nameof(baseDamageDivisor));
            var configured = (weatherEffects ?? DefaultWeatherEffects(baseIntensityDecay)).ToArray();
            if (configured.Any(x => x == null) || configured.Select(x => x.WeatherType).Distinct().Count() != configured.Length ||
                configured.Length != Enum.GetValues(typeof(WeatherType)).Length)
                throw new ArgumentException("Fire settings require one effect for every weather type.", nameof(weatherEffects));
            effects = configured.ToDictionary(x => x.WeatherType);
            StepTicks = stepTicks; BaseIntensityDecay = baseIntensityDecay; BaseDamageDivisor = baseDamageDivisor;
        }
        public long StepTicks { get; }
        public int BaseIntensityDecay { get; }
        public int BaseDamageDivisor { get; }
        public static BuildingFireSettings Default => new BuildingFireSettings(TimeSpan.FromHours(1).Ticks, 25, 20);

        public int DamageModifier(WeatherType type) => Effect(type).DamagePerThousand;
        public int IntensityDelta(WeatherType type) => Effect(type).IntensityDelta;

        private BuildingFireWeatherEffect Effect(WeatherType type) => effects.TryGetValue(type, out var effect)
            ? effect : throw new ArgumentOutOfRangeException(nameof(type));

        private static IEnumerable<BuildingFireWeatherEffect> DefaultWeatherEffects(int baseDecay)
        {
            yield return new BuildingFireWeatherEffect(WeatherType.Clear, 1000, -baseDecay);
            yield return new BuildingFireWeatherEffect(WeatherType.Cloudy, 1000, -baseDecay);
            yield return new BuildingFireWeatherEffect(WeatherType.Rain, 600, -100);
            yield return new BuildingFireWeatherEffect(WeatherType.HeavyRain, 300, -250);
            yield return new BuildingFireWeatherEffect(WeatherType.Snow, 200, -300);
            yield return new BuildingFireWeatherEffect(WeatherType.Windy, 1000, -baseDecay);
            yield return new BuildingFireWeatherEffect(WeatherType.ColdSnap, 1000, -baseDecay);
            yield return new BuildingFireWeatherEffect(WeatherType.HeatWave, 1200, 25);
        }
    }

    /// <summary>One engine-free authority for structural damage, fire, destruction side effects and capture.</summary>
    public sealed class BuildingDamageService
    {
        private const uint FireDamageStream = 0x55323401u;
        private readonly BuildingRegistry buildings;
        private readonly Dictionary<BuildingDefinitionId, BuildingDefinition> definitions;
        private readonly ProductionService production;
        private readonly ResourceInventoryRegistry inventories;
        private readonly WorldPileService worldPiles;
        private readonly IWeatherModifierProvider weather;
        private readonly CombatService combat;
        private readonly BuildingOccupancyGrid occupancy;
        private readonly BuildingFireSettings fireSettings;

        public BuildingDamageService(BuildingRegistry buildingRegistry, IEnumerable<BuildingDefinition> buildingDefinitions,
            ProductionService productionService = null, ResourceInventoryRegistry inventoryRegistry = null,
            WorldPileService pileService = null, IWeatherModifierProvider weatherProvider = null,
            CombatService combatService = null, BuildingOccupancyGrid occupancyGrid = null,
            BuildingFireSettings settings = null)
        {
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            definitions = new Dictionary<BuildingDefinitionId, BuildingDefinition>();
            foreach (var definition in buildingDefinitions ?? throw new ArgumentNullException(nameof(buildingDefinitions)))
                if (definition == null || !definitions.TryAdd(definition.Id, definition))
                    throw new ArgumentException("Building definitions must be non-null and unique.", nameof(buildingDefinitions));
            if (definitions.Count == 0) throw new ArgumentException("At least one building definition is required.", nameof(buildingDefinitions));
            production = productionService;
            inventories = inventoryRegistry;
            worldPiles = pileService;
            if ((inventories == null) != (worldPiles == null))
                throw new ArgumentException("Inventory and world-pile services must be supplied together.");
            weather = weatherProvider;
            combat = combatService;
            occupancy = occupancyGrid;
            fireSettings = settings ?? BuildingFireSettings.Default;
        }

        public long LastFireTick { get; private set; }
        public event Action<Building> BuildingChanged;

        public BuildingDamageResult ApplyDamage(StableEntityId buildingId, StableEntityId damageEventId,
            int amount, BuildingDamageType type, long calendarTick)
        {
            if (amount <= 0) return new BuildingDamageResult(BuildingDamageFailure.InvalidAmount, amount, 0, 0, false);
            if (!Enum.IsDefined(typeof(BuildingDamageType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!damageEventId.IsValid) throw new ArgumentException("Damage event identity required.", nameof(damageEventId));
            if (!buildings.TryGet(buildingId, out var building))
                return new BuildingDamageResult(BuildingDamageFailure.MissingBuilding, amount, 0, 0, false);
            if (building.IsDestroyed)
                return new BuildingDamageResult(BuildingDamageFailure.AlreadyDestroyed, amount, 0, 0, false);
            if (building.HasProcessedDamage(damageEventId))
                return new BuildingDamageResult(BuildingDamageFailure.DuplicateEvent, amount, 0, building.CurrentIntegrity, false);
            var definition = Definition(building);
            var multiplier = type == BuildingDamageType.Fire ? definition.FireDamagePerThousand :
                type == BuildingDamageType.Combat ? definition.CombatDamagePerThousand : 1000;
            var scaledLong = Math.Max(1L, checked((long)amount * multiplier) / 1000L);
            var scaled = (int)Math.Min(int.MaxValue, scaledLong);
            var destroys = scaled >= building.CurrentIntegrity;
            if (destroys)
            {
                if (inventories != null && worldPiles != null)
                    worldPiles.SpillStorage(building.Id, building.Coordinate);
                production?.CancelTasksForSite(building.Id);
            }
            var applied = building.ApplyDamage(damageEventId, scaled, type, calendarTick);
            if (destroys) building.MarkStorageSpilled();
            BuildingChanged?.Invoke(building);
            return new BuildingDamageResult(BuildingDamageFailure.None, amount, applied,
                building.CurrentIntegrity, destroys);
        }

        public BuildingDamageResult ApplyCombatDamage(StableEntityId buildingId, StableEntityId battleId,
            ulong attackSequence, int amount, BattleMode mode, long calendarTick)
        {
            if (!battleId.IsValid) throw new ArgumentException("Battle identity required.", nameof(battleId));
            if (attackSequence == 0) throw new ArgumentOutOfRangeException(nameof(attackSequence));
            if (!Enum.IsDefined(typeof(BattleMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return ApplyDamage(buildingId, DerivedEventId(buildingId, battleId, attackSequence, (uint)mode),
                amount, BuildingDamageType.Combat, calendarTick);
        }

        public BuildingFireResult IgniteBuilding(StableEntityId buildingId, int intensity, long calendarTick)
        {
            if (calendarTick < LastFireTick)
                throw new ArgumentOutOfRangeException(nameof(calendarTick), "Fire time cannot move backwards.");
            if (!buildings.TryGet(buildingId, out var building))
                return new BuildingFireResult(BuildingFireFailure.MissingBuilding, BuildingFireStatus.None);
            if (intensity <= 0 || intensity > 1000)
                return new BuildingFireResult(BuildingFireFailure.InvalidIntensity, building.FireStatus);
            if (building.IsDestroyed)
                return new BuildingFireResult(BuildingFireFailure.AlreadyDestroyed, building.FireStatus);
            if (building.FireStatus == BuildingFireStatus.Burning)
                return new BuildingFireResult(BuildingFireFailure.AlreadyBurning, building.FireStatus);
            building.Ignite(intensity, calendarTick);
            if (calendarTick > LastFireTick) LastFireTick = calendarTick;
            BuildingChanged?.Invoke(building);
            return new BuildingFireResult(BuildingFireFailure.None, building.FireStatus);
        }

        public BuildingFireResult ExtinguishBuilding(StableEntityId buildingId, long calendarTick)
        {
            if (calendarTick < LastFireTick)
                throw new ArgumentOutOfRangeException(nameof(calendarTick), "Fire time cannot move backwards.");
            if (!buildings.TryGet(buildingId, out var building))
                return new BuildingFireResult(BuildingFireFailure.MissingBuilding, BuildingFireStatus.None);
            if (building.FireStatus != BuildingFireStatus.Burning)
                return new BuildingFireResult(BuildingFireFailure.NotBurning, building.FireStatus);
            building.Extinguish(calendarTick);
            if (calendarTick > LastFireTick) LastFireTick = calendarTick;
            BuildingChanged?.Invoke(building);
            return new BuildingFireResult(BuildingFireFailure.None, building.FireStatus);
        }

        public void AdvanceFireTo(long targetCalendarTick)
        {
            if (targetCalendarTick < LastFireTick)
                throw new ArgumentOutOfRangeException(nameof(targetCalendarTick), "Fire time cannot move backwards.");
            foreach (var building in buildings.All.Where(x => x.FireStatus == BuildingFireStatus.Burning)
                         .OrderBy(x => x.Id).ToArray())
            {
                var next = checked(building.LastFireProcessedTick + fireSettings.StepTicks);
                while (next <= targetCalendarTick && building.FireStatus == BuildingFireStatus.Burning)
                {
                    var type = weather == null ? WeatherType.Clear : weather.GetWeatherAtCalendarTick(next).Type;
                    var damage = Math.Max(1, building.FireIntensity / fireSettings.BaseDamageDivisor);
                    damage = Math.Max(1, checked(damage * fireSettings.DamageModifier(type) / 1000));
                    ApplyDamage(building.Id, DerivedEventId(building.Id, building.Id,
                        unchecked((ulong)next), FireDamageStream), damage, BuildingDamageType.Fire, next);
                    var nextIntensity = Math.Max(0, Math.Min(1000,
                        checked(building.FireIntensity + fireSettings.IntensityDelta(type))));
                    building.AdvanceFireCheckpoint(next, nextIntensity);
                    BuildingChanged?.Invoke(building);
                    next = checked(next + fireSettings.StepTicks);
                }
            }
            LastFireTick = targetCalendarTick;
        }

        public BuildingCaptureResult CaptureBuilding(StableEntityId buildingId, CombatSideId newOwnerSideId,
            StableEntityId battleId, long calendarTick)
        {
            if (!newOwnerSideId.IsValid) return new BuildingCaptureResult(BuildingCaptureFailure.InvalidOwner, default);
            if (!buildings.TryGet(buildingId, out var building))
                return new BuildingCaptureResult(BuildingCaptureFailure.MissingBuilding, default);
            if (building.OwnerSideId == newOwnerSideId)
                return new BuildingCaptureResult(BuildingCaptureFailure.SameOwner, building.OwnerSideId);
            if (building.IsDestroyed)
                return new BuildingCaptureResult(BuildingCaptureFailure.Destroyed, building.OwnerSideId);
            if (!Definition(building).Capturable)
                return new BuildingCaptureResult(BuildingCaptureFailure.NotCapturable, building.OwnerSideId);
            if (combat == null || !combat.TryGetEncounter(battleId, out var encounter))
                return new BuildingCaptureResult(BuildingCaptureFailure.MissingBattle, building.OwnerSideId);
            return CaptureBuilding(buildingId, newOwnerSideId, encounter, calendarTick);
        }

        public BuildingCaptureResult CaptureBuilding(StableEntityId buildingId, CombatSideId newOwnerSideId,
            CombatEncounterState encounter, long calendarTick)
        {
            if (!newOwnerSideId.IsValid) return new BuildingCaptureResult(BuildingCaptureFailure.InvalidOwner, default);
            if (!buildings.TryGet(buildingId, out var building))
                return new BuildingCaptureResult(BuildingCaptureFailure.MissingBuilding, default);
            if (building.OwnerSideId == newOwnerSideId)
                return new BuildingCaptureResult(BuildingCaptureFailure.SameOwner, building.OwnerSideId);
            if (building.IsDestroyed)
                return new BuildingCaptureResult(BuildingCaptureFailure.Destroyed, building.OwnerSideId);
            if (!Definition(building).Capturable)
                return new BuildingCaptureResult(BuildingCaptureFailure.NotCapturable, building.OwnerSideId);
            if (encounter == null)
                return new BuildingCaptureResult(BuildingCaptureFailure.MissingBattle, building.OwnerSideId);
            if (encounter.Status != CombatEncounterStatus.Finished)
                return new BuildingCaptureResult(BuildingCaptureFailure.BattleNotFinished, building.OwnerSideId);
            var victor = encounter.Outcome == CombatOutcome.SideAVictory ? encounter.SideA :
                encounter.Outcome == CombatOutcome.SideBVictory ? encounter.SideB : default;
            if (!victor.IsValid || victor != newOwnerSideId || victor == building.OwnerSideId)
                return new BuildingCaptureResult(BuildingCaptureFailure.NotVictoriousSide, building.OwnerSideId);
            production?.CancelTasksForSite(building.Id);
            building.Capture(newOwnerSideId, calendarTick);
            BuildingChanged?.Invoke(building);
            return new BuildingCaptureResult(BuildingCaptureFailure.None, building.OwnerSideId);
        }

        public BuildingWorldSaveState CaptureState() => new BuildingWorldSaveState(
            buildings.All.Select(x => x.CaptureState()), worldPiles?.CaptureStates(), LastFireTick);

        public void RestoreState(BuildingWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var value in state.Buildings)
            {
                var definition = definitions.TryGetValue(value.DefinitionId, out var found) ? found :
                    throw new ArgumentException("Saved building definition is unavailable.", nameof(state));
                if (definition.MaxIntegrity != value.MaxIntegrity)
                    throw new ArgumentException("Saved building integrity definition changed.", nameof(state));
                if (value.LastFireProcessedTick > state.LastFireTick)
                    throw new ArgumentException("Saved fire checkpoint is ahead of building-world time.", nameof(state));
            }
            buildings.ReplaceAll(state.Buildings);
            if (occupancy != null)
            {
                occupancy.Clear();
                foreach (var building in buildings.All.OrderBy(x => x.Id))
                {
                    var definition = Definition(building);
                    occupancy.Reserve(definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation),
                        building.Id, definition.AllowsOverlap);
                }
            }
            if (worldPiles != null) worldPiles.RestoreStates(state.WorldPiles);
            else if (state.WorldPiles.Count != 0) throw new InvalidOperationException("Save contains world piles but no pile service is attached.");
            LastFireTick = state.LastFireTick;
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var building in buildings.All.OrderBy(x => x.Id))
            {
                AddId(ref hash, building.Id); Add(ref hash, unchecked((ulong)building.CurrentIntegrity));
                Add(ref hash, (ulong)building.StructuralStatus); Add(ref hash, (ulong)building.FireStatus);
                Add(ref hash, unchecked((ulong)building.FireIntensity)); Add(ref hash, unchecked((ulong)building.LastFireProcessedTick));
                foreach (var value in building.OwnerSideId.ToString()) AddByte(ref hash, (byte)value);
            }
            return hash;
        }

        private BuildingDefinition Definition(Building building) => definitions.TryGetValue(building.DefinitionId, out var definition)
            ? definition : throw new InvalidOperationException("Building definition is unavailable: " + building.DefinitionId);

        private static StableEntityId DerivedEventId(StableEntityId buildingId, StableEntityId sourceId,
            ulong sequence, uint stream)
        {
            var first = 14695981039346656037UL;
            foreach (var value in buildingId.ToGuid().ToByteArray()) AddByte(ref first, value);
            foreach (var value in sourceId.ToGuid().ToByteArray()) AddByte(ref first, value);
            Add(ref first, sequence); Add(ref first, stream);
            var second = first ^ 0x9e3779b97f4a7c15UL;
            Add(ref second, sequence ^ ((ulong)stream << 32));
            return StableEntityId.FromDeterministicHash(first, second);
        }

        private static void AddId(ref ulong hash, StableEntityId id)
        { foreach (var value in id.ToGuid().ToByteArray()) AddByte(ref hash, value); }
        private static void Add(ref ulong hash, ulong value)
        { for (var shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(value >> shift)); }
        private static void AddByte(ref ulong hash, byte value) { hash ^= value; hash *= 1099511628211UL; }
    }
}
