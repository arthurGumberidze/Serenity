using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Simulation.Health;
using Game.Simulation.Tiers;

namespace Game.Simulation.Weather
{
    public readonly struct WeatherHealthAdvanceResult
    {
        public WeatherHealthAdvanceResult(int processedPersonDays, int conditionsApplied, ulong stateHash)
        {
            ProcessedPersonDays = processedPersonDays;
            ConditionsApplied = conditionsApplied;
            StateHash = stateHash;
        }
        public int ProcessedPersonDays { get; }
        public int ConditionsApplied { get; }
        public ulong StateHash { get; }
    }

    /// <summary>Stateless daily weather generation plus persisted exactly-once health exposure checkpoints.</summary>
    public sealed class WeatherService : IWeatherModifierProvider
    {
        public const uint WeatherTypeStream = 0x55313901u;
        public const uint TemperatureStream = 0x55313902u;
        public const uint PrecipitationStream = 0x55313903u;
        public const uint WindStream = 0x55313904u;
        public const uint HealthExposureStream = 0x55313905u;

        private readonly long worldSeed;
        private readonly ClimateProfile climate;
        private readonly HealthAndFamilyService health;
        private readonly Dictionary<StableEntityId, long> exposureDays = new Dictionary<StableEntityId, long>();
        private readonly ulong climateKey;

        public WeatherService(long simulationWorldSeed, ClimateProfile climateProfile,
            HealthAndFamilyService healthService = null)
        {
            worldSeed = simulationWorldSeed;
            climate = climateProfile ?? throw new ArgumentNullException(nameof(climateProfile));
            health = healthService;
            climateKey = TextHash64(climate.Id.ToString());
        }

        public long WorldSeed => worldSeed;
        public ClimateProfile Climate => climate;
        public IReadOnlyList<WeatherExposureCheckpoint> ExposureCheckpoints => exposureDays
            .OrderBy(x => x.Key).Select(x => new WeatherExposureCheckpoint(x.Key, x.Value)).ToArray();

        public WeatherState GetWeatherAtCalendarTick(long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            return GetWeatherForDay(GameCalendar.CompletedDays(calendarTick));
        }

        public WeatherState GetWeatherForDay(long absoluteDay)
        {
            if (absoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(absoluteDay));
            var dayTick = checked(absoluteDay * TimeSpan.TicksPerDay);
            var season = climate.SeasonCalendar.GetSeason(dayTick);
            var seasonal = climate.GetSeason(season);
            var typeSample = Sample(absoluteDay, WeatherTypeStream);
            var selection = (int)(typeSample % (ulong)seasonal.TotalWeight);
            var type = WeatherType.Clear;
            foreach (var weight in seasonal.WeatherWeights)
            {
                if (selection < weight.Weight) { type = weight.Type; break; }
                selection -= weight.Weight;
            }

            var span = seasonal.TemperatureVariationDeciCelsius;
            var range = checked(span * 2 + 1);
            var temperature = seasonal.BaseTemperatureDeciCelsius +
                (int)(Sample(absoluteDay, TemperatureStream) % (ulong)range) - span;
            temperature += climate.GetEffect(type).TemperatureAnomalyDeciCelsius;

            if ((type == WeatherType.Rain || type == WeatherType.HeavyRain) && temperature <= climate.SnowThresholdDeciCelsius)
                type = WeatherType.Snow;
            else if (type == WeatherType.Snow && temperature > climate.SnowThresholdDeciCelsius)
                type = WeatherType.Rain;

            var effect = climate.GetEffect(type);
            var precipitation = effect.Precipitation;
            var precipitationPermille = SampleRange(absoluteDay, PrecipitationStream,
                effect.PrecipitationMinimumPermille, effect.PrecipitationMaximumPermille);
            var wind = SampleRange(absoluteDay, WindStream, effect.WindMinimumPermille, effect.WindMaximumPermille);

            var crop = Multiply(seasonal.CropModifierPerThousand, effect.CropModifierPerThousand);
            var work = effect.OutdoorWorkModifierPerThousand;
            var movement = effect.MovementModifierPerThousand;
            var exposureKind = temperature <= climate.SevereColdDeciCelsius || type == WeatherType.ColdSnap
                ? WeatherExposureKind.Cold : temperature >= climate.SevereHeatDeciCelsius || type == WeatherType.HeatWave
                    ? WeatherExposureKind.Heat : WeatherExposureKind.None;
            var exposureRisk = exposureKind == WeatherExposureKind.None ? 0 :
                Math.Min(1000, 120 + Math.Abs(temperature - (exposureKind == WeatherExposureKind.Cold
                    ? climate.SevereColdDeciCelsius : climate.SevereHeatDeciCelsius)) * 2);
            return new WeatherState(absoluteDay, season, type, temperature, precipitation, precipitationPermille, wind,
                exposureKind, new WeatherModifiers(crop, work, movement, exposureRisk,
                    seasonal.WildFoodModifierPerThousand));
        }

        public int GetMovementModifierForInterval(long startCalendarTick, long endCalendarTick)
        {
            if (startCalendarTick < 0 || endCalendarTick < startCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(startCalendarTick));
            if (startCalendarTick == endCalendarTick) return GetWeatherAtCalendarTick(endCalendarTick).Modifiers.MovementPerThousand;
            decimal weighted = 0;
            var cursor = startCalendarTick;
            while (cursor < endCalendarTick)
            {
                var day = cursor / TimeSpan.TicksPerDay;
                var boundary = Math.Min(endCalendarTick, checked((day + 1) * TimeSpan.TicksPerDay));
                weighted += (decimal)(boundary - cursor) * GetWeatherForDay(day).Modifiers.MovementPerThousand;
                cursor = boundary;
            }
            return (int)(weighted / (endCalendarTick - startCalendarTick));
        }

        public WeatherHealthAdvanceResult AdvanceHealthExposureTo(long targetCalendarTick,
            IEnumerable<StableEntityId> outdoorCharacters)
        {
            if (targetCalendarTick < 0) throw new ArgumentOutOfRangeException(nameof(targetCalendarTick));
            var ids = (outdoorCharacters ?? throw new ArgumentNullException(nameof(outdoorCharacters)))
                .Distinct().OrderBy(x => x).ToArray();
            if (ids.Any(x => !x.IsValid)) throw new ArgumentException("Outdoor character IDs must be valid.", nameof(outdoorCharacters));
            var completedDays = targetCalendarTick / TimeSpan.TicksPerDay;
            var finalDay = completedDays - 1;
            var processed = 0;
            var applied = 0;
            foreach (var id in ids)
            {
                exposureDays.TryGetValue(id, out var savedDay);
                var firstDay = exposureDays.ContainsKey(id) ? savedDay + 1 : 0;
                for (var day = firstDay; day <= finalDay; day++)
                {
                    processed++;
                    var weather = GetWeatherForDay(day);
                    if (health != null && weather.ExposureKind != WeatherExposureKind.None &&
                        SampleForCharacter(id, day, HealthExposureStream) % 1000UL < (ulong)weather.Modifiers.HealthExposureRiskPermille)
                    {
                        var definitionId = new HealthConditionId(weather.ExposureKind == WeatherExposureKind.Cold
                            ? "cold_stress" : "heat_stress");
                        var active = health.Conditions.Any(x => x.TargetId == id && x.DefinitionId.Equals(definitionId) &&
                            (x.State == HealthConditionState.Active || x.State == HealthConditionState.Improving ||
                             x.State == HealthConditionState.Worsening || x.State == HealthConditionState.Critical));
                        if (!active)
                        {
                            health.ApplyCondition(id, definitionId, checked((day + 1) * TimeSpan.TicksPerDay));
                            applied++;
                        }
                    }
                    exposureDays[id] = day;
                }
            }
            return new WeatherHealthAdvanceResult(processed, applied, ComputeStateHash());
        }

        public WeatherSaveState CaptureState() => new WeatherSaveState(worldSeed, climate.Id, ExposureCheckpoints);

        public void RestoreState(WeatherSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.WorldSeed != worldSeed) throw new InvalidOperationException("Weather save world seed does not match this session.");
            if (state.ClimateProfileId != climate.Id) throw new InvalidOperationException("Weather save climate profile does not match this session.");
            exposureDays.Clear();
            foreach (var checkpoint in state.ExposureCheckpoints) exposureDays.Add(checkpoint.CharacterId, checkpoint.LastProcessedDay);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)worldSeed));
            DeterministicKeyedRandom.Add(ref hash, climateKey);
            foreach (var checkpoint in ExposureCheckpoints)
            {
                foreach (var value in checkpoint.CharacterId.ToGuid().ToByteArray())
                    DeterministicKeyedRandom.AddByte(ref hash, value);
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)checkpoint.LastProcessedDay));
            }
            return hash;
        }

        private ulong Sample(long day, uint stream) => DeterministicKeyedRandom.Sample64(worldSeed, climateKey,
            unchecked((ulong)day), stream);
        private int SampleRange(long day, uint stream, int minimum, int maximum) => minimum == maximum ? minimum :
            minimum + (int)(Sample(day, stream) % (ulong)(maximum - minimum + 1));
        private ulong SampleForCharacter(StableEntityId id, long day, uint stream) =>
            DeterministicKeyedRandom.Sample64(worldSeed, id, unchecked((ulong)day), stream);
        private static int Multiply(int left, int right) => left * right / 1000;
        private static ulong TextHash64(string value)
        {
            var hash = 14695981039346656037UL;
            for (var i = 0; i < value.Length; i++) DeterministicKeyedRandom.Add(ref hash, value[i]);
            return hash;
        }
    }
}
