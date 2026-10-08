using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.Time;

namespace Game.Domain.Weather
{
    public enum Season { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }
    public enum WeatherType { Clear = 0, Cloudy = 1, Rain = 2, HeavyRain = 3, Snow = 4, Windy = 5, ColdSnap = 6, HeatWave = 7 }
    public enum PrecipitationType { None = 0, Rain = 1, Snow = 2 }
    public enum WeatherExposureKind { None = 0, Cold = 1, Heat = 2 }

    public readonly struct ClimateProfileId : IEquatable<ClimateProfileId>, IComparable<ClimateProfileId>
    {
        private readonly string value;

        public ClimateProfileId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value != value.Trim() ||
                value.Any(c => !(char.IsLower(c) || char.IsDigit(c) || c == '_')))
                throw new ArgumentException("Climate profile ID must be canonical lowercase text.", nameof(value));
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        public int CompareTo(ClimateProfileId other) => string.Compare(value, other.value, StringComparison.Ordinal);
        public bool Equals(ClimateProfileId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ClimateProfileId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(ClimateProfileId left, ClimateProfileId right) => left.Equals(right);
        public static bool operator !=(ClimateProfileId left, ClimateProfileId right) => !left.Equals(right);
    }

    public sealed class SeasonCalendar
    {
        private readonly int[] startDays;
        private readonly IReadOnlyList<int> readOnlyStartDays;

        public SeasonCalendar(int springStartDay, int summerStartDay, int autumnStartDay, int winterStartDay)
        {
            startDays = new[] { springStartDay, summerStartDay, autumnStartDay, winterStartDay };
            if (springStartDay != 1 || startDays.Any(x => x < 1 || x > GameCalendar.DaysPerYear) ||
                startDays.Distinct().Count() != 4 || !startDays.SequenceEqual(startDays.OrderBy(x => x)))
                throw new ArgumentException("Season starts must be unique, ordered days with spring beginning on day 1.");
            readOnlyStartDays = Array.AsReadOnly(startDays);
        }

        public IReadOnlyList<int> StartDays => readOnlyStartDays;
        public Season GetSeason(long calendarTicks) => GetSeasonForDayOfYear(GameCalendar.DayOfYear(calendarTicks));

        public Season GetSeasonForDayOfYear(int dayOfYear)
        {
            if (dayOfYear < 1 || dayOfYear > GameCalendar.DaysPerYear)
                throw new ArgumentOutOfRangeException(nameof(dayOfYear));
            if (dayOfYear >= startDays[3]) return Season.Winter;
            if (dayOfYear >= startDays[2]) return Season.Autumn;
            if (dayOfYear >= startDays[1]) return Season.Summer;
            return Season.Spring;
        }

        public static SeasonCalendar EqualSegments365 => new SeasonCalendar(1, 92, 183, 274);
    }

    public readonly struct WeatherWeight
    {
        public WeatherWeight(WeatherType type, int weight)
        {
            if (!Enum.IsDefined(typeof(WeatherType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Type = type;
            Weight = weight;
        }
        public WeatherType Type { get; }
        public int Weight { get; }
    }

    public readonly struct WeatherEffect
    {
        public WeatherEffect(WeatherType type, int temperatureAnomalyDeciCelsius, PrecipitationType precipitation,
            int precipitationMinimumPermille, int precipitationMaximumPermille,
            int windMinimumPermille, int windMaximumPermille,
            int cropModifierPerThousand, int outdoorWorkModifierPerThousand, int movementModifierPerThousand)
        {
            if (!Enum.IsDefined(typeof(WeatherType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(PrecipitationType), precipitation))
                throw new ArgumentOutOfRangeException(nameof(precipitation));
            if (temperatureAnomalyDeciCelsius < -500 || temperatureAnomalyDeciCelsius > 500)
                throw new ArgumentOutOfRangeException(nameof(temperatureAnomalyDeciCelsius));
            if (precipitationMinimumPermille < 0 || precipitationMaximumPermille < precipitationMinimumPermille ||
                precipitationMaximumPermille > 1000 ||
                (precipitation == PrecipitationType.None && precipitationMaximumPermille != 0) ||
                (precipitation != PrecipitationType.None && precipitationMinimumPermille == 0))
                throw new ArgumentOutOfRangeException(nameof(precipitationMinimumPermille));
            if (windMinimumPermille < 0 || windMaximumPermille < windMinimumPermille || windMaximumPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(windMinimumPermille));
            ValidateModifier(cropModifierPerThousand, nameof(cropModifierPerThousand));
            ValidateModifier(outdoorWorkModifierPerThousand, nameof(outdoorWorkModifierPerThousand));
            ValidateModifier(movementModifierPerThousand, nameof(movementModifierPerThousand));
            Type = type;
            TemperatureAnomalyDeciCelsius = temperatureAnomalyDeciCelsius;
            Precipitation = precipitation;
            PrecipitationMinimumPermille = precipitationMinimumPermille;
            PrecipitationMaximumPermille = precipitationMaximumPermille;
            WindMinimumPermille = windMinimumPermille;
            WindMaximumPermille = windMaximumPermille;
            CropModifierPerThousand = cropModifierPerThousand;
            OutdoorWorkModifierPerThousand = outdoorWorkModifierPerThousand;
            MovementModifierPerThousand = movementModifierPerThousand;
        }

        public WeatherType Type { get; }
        public int TemperatureAnomalyDeciCelsius { get; }
        public PrecipitationType Precipitation { get; }
        public int PrecipitationMinimumPermille { get; }
        public int PrecipitationMaximumPermille { get; }
        public int WindMinimumPermille { get; }
        public int WindMaximumPermille { get; }
        public int CropModifierPerThousand { get; }
        public int OutdoorWorkModifierPerThousand { get; }
        public int MovementModifierPerThousand { get; }

        private static void ValidateModifier(int value, string name)
        {
            if (value < 0 || value > 2000) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class SeasonalClimate
    {
        private readonly WeatherWeight[] weights;
        private readonly IReadOnlyList<WeatherWeight> readOnlyWeights;

        public SeasonalClimate(Season season, int baseTemperatureDeciCelsius, int temperatureVariationDeciCelsius,
            int cropModifierPerThousand, int wildFoodModifierPerThousand, IEnumerable<WeatherWeight> weatherWeights)
        {
            if (!Enum.IsDefined(typeof(Season), season)) throw new ArgumentOutOfRangeException(nameof(season));
            if (baseTemperatureDeciCelsius < -800 || baseTemperatureDeciCelsius > 700)
                throw new ArgumentOutOfRangeException(nameof(baseTemperatureDeciCelsius));
            if (temperatureVariationDeciCelsius < 0 || temperatureVariationDeciCelsius > 500)
                throw new ArgumentOutOfRangeException(nameof(temperatureVariationDeciCelsius));
            ValidateModifier(cropModifierPerThousand, nameof(cropModifierPerThousand));
            ValidateModifier(wildFoodModifierPerThousand, nameof(wildFoodModifierPerThousand));
            weights = (weatherWeights ?? throw new ArgumentNullException(nameof(weatherWeights))).ToArray();
            if (weights.Length == 0 || weights.All(x => x.Weight == 0) || weights.Select(x => x.Type).Distinct().Count() != weights.Length)
                throw new ArgumentException("A season requires unique weather entries with positive total weight.", nameof(weatherWeights));
            readOnlyWeights = Array.AsReadOnly(weights);
            Season = season;
            BaseTemperatureDeciCelsius = baseTemperatureDeciCelsius;
            TemperatureVariationDeciCelsius = temperatureVariationDeciCelsius;
            CropModifierPerThousand = cropModifierPerThousand;
            WildFoodModifierPerThousand = wildFoodModifierPerThousand;
        }

        public Season Season { get; }
        public int BaseTemperatureDeciCelsius { get; }
        public int TemperatureVariationDeciCelsius { get; }
        public int CropModifierPerThousand { get; }
        public int WildFoodModifierPerThousand { get; }
        public IReadOnlyList<WeatherWeight> WeatherWeights => readOnlyWeights;
        public int TotalWeight => weights.Sum(x => x.Weight);

        private static void ValidateModifier(int value, string name)
        {
            if (value < 0 || value > 2000) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class ClimateProfile
    {
        private readonly Dictionary<Season, SeasonalClimate> seasons;
        private readonly Dictionary<WeatherType, WeatherEffect> effects;

        public ClimateProfile(ClimateProfileId id, SeasonCalendar seasonCalendar, IEnumerable<SeasonalClimate> values,
            int snowThresholdDeciCelsius, int severeColdDeciCelsius, int severeHeatDeciCelsius,
            IEnumerable<WeatherEffect> weatherEffects = null)
        {
            if (!id.IsValid) throw new ArgumentException("Climate profile ID required.", nameof(id));
            SeasonCalendar = seasonCalendar ?? throw new ArgumentNullException(nameof(seasonCalendar));
            if (snowThresholdDeciCelsius < -500 || snowThresholdDeciCelsius > 100)
                throw new ArgumentOutOfRangeException(nameof(snowThresholdDeciCelsius));
            if (severeColdDeciCelsius >= snowThresholdDeciCelsius)
                throw new ArgumentException("Severe cold threshold must be below the snow threshold.");
            if (severeHeatDeciCelsius <= 200 || severeHeatDeciCelsius > 700)
                throw new ArgumentOutOfRangeException(nameof(severeHeatDeciCelsius));
            seasons = new Dictionary<Season, SeasonalClimate>();
            foreach (var value in values ?? throw new ArgumentNullException(nameof(values)))
            {
                if (value == null || !seasons.TryAdd(value.Season, value))
                    throw new ArgumentException("Climate seasons must be non-null and unique.", nameof(values));
            }
            if (seasons.Count != 4 || Enum.GetValues(typeof(Season)).Cast<Season>().Any(x => !seasons.ContainsKey(x)))
                throw new ArgumentException("Climate profile must define all four seasons.", nameof(values));
            effects = new Dictionary<WeatherType, WeatherEffect>();
            foreach (var effect in weatherEffects ?? TemperateWeatherEffects())
                if (!effects.TryAdd(effect.Type, effect))
                    throw new ArgumentException("Climate weather effects must have unique weather types.", nameof(weatherEffects));
            if (effects.Count != Enum.GetValues(typeof(WeatherType)).Length ||
                Enum.GetValues(typeof(WeatherType)).Cast<WeatherType>().Any(x => !effects.ContainsKey(x)))
                throw new ArgumentException("Climate profile must define every weather type effect.", nameof(weatherEffects));
            Id = id;
            SnowThresholdDeciCelsius = snowThresholdDeciCelsius;
            SevereColdDeciCelsius = severeColdDeciCelsius;
            SevereHeatDeciCelsius = severeHeatDeciCelsius;
        }

        public ClimateProfileId Id { get; }
        public SeasonCalendar SeasonCalendar { get; }
        public int SnowThresholdDeciCelsius { get; }
        public int SevereColdDeciCelsius { get; }
        public int SevereHeatDeciCelsius { get; }
        public SeasonalClimate GetSeason(Season season) => seasons[season];
        public WeatherEffect GetEffect(WeatherType type) => effects[type];

        public static ClimateProfile Temperate => new ClimateProfile(new ClimateProfileId("temperate"),
            SeasonCalendar.EqualSegments365, new[]
            {
                Seasonal(Season.Spring, 110, 100, 1000, 1000, 34, 23, 22, 7, 0, 8, 4, 2),
                Seasonal(Season.Summer, 230, 110, 1100, 1150, 42, 20, 14, 5, 0, 8, 2, 9),
                Seasonal(Season.Autumn, 100, 110, 800, 850, 28, 25, 24, 9, 1, 8, 4, 1),
                Seasonal(Season.Winter, -40, 120, 0, 300, 24, 25, 5, 1, 28, 8, 9, 0)
            }, 0, -100, 350, TemperateWeatherEffects());

        private static SeasonalClimate Seasonal(Season season, int temperature, int variation, int crop, int wild,
            int clear, int cloudy, int rain, int heavyRain, int snow, int windy, int coldSnap, int heatWave) =>
            new SeasonalClimate(season, temperature, variation, crop, wild, new[]
            {
                new WeatherWeight(WeatherType.Clear, clear), new WeatherWeight(WeatherType.Cloudy, cloudy),
                new WeatherWeight(WeatherType.Rain, rain), new WeatherWeight(WeatherType.HeavyRain, heavyRain),
                new WeatherWeight(WeatherType.Snow, snow), new WeatherWeight(WeatherType.Windy, windy),
                new WeatherWeight(WeatherType.ColdSnap, coldSnap), new WeatherWeight(WeatherType.HeatWave, heatWave)
            });

        private static WeatherEffect[] TemperateWeatherEffects() => new[]
        {
            Effect(WeatherType.Clear, 0, PrecipitationType.None, 0, 0, 80, 380, 1000, 1000, 1000),
            Effect(WeatherType.Cloudy, 0, PrecipitationType.None, 0, 0, 80, 380, 1000, 1000, 1000),
            Effect(WeatherType.Rain, 0, PrecipitationType.Rain, 300, 700, 80, 380, 1100, 920, 950),
            Effect(WeatherType.HeavyRain, 0, PrecipitationType.Rain, 750, 1000, 350, 650, 950, 850, 900),
            Effect(WeatherType.Snow, 0, PrecipitationType.Snow, 300, 700, 350, 650, 0, 750, 750),
            Effect(WeatherType.Windy, 0, PrecipitationType.None, 0, 0, 650, 1000, 1000, 850, 900),
            Effect(WeatherType.ColdSnap, -120, PrecipitationType.None, 0, 0, 80, 380, 700, 800, 900),
            Effect(WeatherType.HeatWave, 120, PrecipitationType.None, 0, 0, 80, 380, 800, 850, 950)
        };

        private static WeatherEffect Effect(WeatherType type, int anomaly, PrecipitationType precipitation,
            int precipitationMinimum, int precipitationMaximum, int windMinimum, int windMaximum,
            int crop, int work, int movement) => new WeatherEffect(type, anomaly, precipitation,
                precipitationMinimum, precipitationMaximum, windMinimum, windMaximum, crop, work, movement);
    }

    public sealed class ClimateProfileCatalog
    {
        private readonly Dictionary<ClimateProfileId, ClimateProfile> profiles;

        public ClimateProfileCatalog(IEnumerable<ClimateProfile> values)
        {
            profiles = new Dictionary<ClimateProfileId, ClimateProfile>();
            foreach (var profile in values ?? throw new ArgumentNullException(nameof(values)))
                if (profile == null || !profiles.TryAdd(profile.Id, profile))
                    throw new ArgumentException("Climate profiles must be non-null and have unique IDs.", nameof(values));
            if (profiles.Count == 0) throw new ArgumentException("At least one climate profile is required.", nameof(values));
        }

        public ClimateProfile Get(ClimateProfileId id) => profiles.TryGetValue(id, out var profile)
            ? profile : throw new KeyNotFoundException($"Unknown climate profile '{id}'.");
    }

    public readonly struct WeatherModifiers
    {
        public WeatherModifiers(int crop, int outdoorWork, int movement, int healthExposureRisk, int wildFood)
        {
            Validate(crop, nameof(crop)); Validate(outdoorWork, nameof(outdoorWork));
            Validate(movement, nameof(movement)); Validate(healthExposureRisk, nameof(healthExposureRisk));
            Validate(wildFood, nameof(wildFood));
            CropGrowthPerThousand = crop;
            OutdoorWorkPerThousand = outdoorWork;
            MovementPerThousand = movement;
            HealthExposureRiskPermille = healthExposureRisk;
            WildFoodGatheringPerThousand = wildFood;
        }
        public int CropGrowthPerThousand { get; }
        public int OutdoorWorkPerThousand { get; }
        public int MovementPerThousand { get; }
        public int HealthExposureRiskPermille { get; }
        public int WildFoodGatheringPerThousand { get; }
        private static void Validate(int value, string name)
        {
            if (value < 0 || value > 2000) throw new ArgumentOutOfRangeException(name);
        }
    }

    public readonly struct WeatherState
    {
        public WeatherState(long absoluteDay, Season season, WeatherType type, int temperatureDeciCelsius,
            PrecipitationType precipitation, int precipitationPermille, int windPermille,
            WeatherExposureKind exposureKind, WeatherModifiers modifiers)
        {
            if (absoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(absoluteDay));
            if (!Enum.IsDefined(typeof(Season), season) || !Enum.IsDefined(typeof(WeatherType), type) ||
                !Enum.IsDefined(typeof(PrecipitationType), precipitation) || !Enum.IsDefined(typeof(WeatherExposureKind), exposureKind))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (temperatureDeciCelsius < -1000 || temperatureDeciCelsius > 1000)
                throw new ArgumentOutOfRangeException(nameof(temperatureDeciCelsius));
            if (precipitationPermille < 0 || precipitationPermille > 1000 || windPermille < 0 || windPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(precipitationPermille));
            if (precipitation == PrecipitationType.None && precipitationPermille != 0)
                throw new ArgumentException("Dry weather cannot have precipitation intensity.");
            AbsoluteDay = absoluteDay; Season = season; Type = type; TemperatureDeciCelsius = temperatureDeciCelsius;
            Precipitation = precipitation; PrecipitationPermille = precipitationPermille; WindPermille = windPermille;
            ExposureKind = exposureKind; Modifiers = modifiers;
        }
        public long AbsoluteDay { get; }
        public Season Season { get; }
        public WeatherType Type { get; }
        public int TemperatureDeciCelsius { get; }
        public double TemperatureCelsius => TemperatureDeciCelsius / 10d;
        public PrecipitationType Precipitation { get; }
        public int PrecipitationPermille { get; }
        public int WindPermille { get; }
        public bool Snowing => Precipitation == PrecipitationType.Snow;
        public WeatherExposureKind ExposureKind { get; }
        public WeatherModifiers Modifiers { get; }
    }

    public interface IWeatherModifierProvider
    {
        WeatherState GetWeatherAtCalendarTick(long calendarTick);
    }

    public readonly struct WeatherExposureCheckpoint
    {
        public WeatherExposureCheckpoint(StableEntityId characterId, long lastProcessedDay)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            if (lastProcessedDay < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedDay));
            CharacterId = characterId; LastProcessedDay = lastProcessedDay;
        }
        public StableEntityId CharacterId { get; }
        public long LastProcessedDay { get; }
    }

    public sealed class WeatherSaveState
    {
        public WeatherSaveState(long worldSeed, ClimateProfileId climateProfileId,
            IEnumerable<WeatherExposureCheckpoint> exposureCheckpoints)
        {
            if (!climateProfileId.IsValid) throw new ArgumentException("Climate profile ID required.", nameof(climateProfileId));
            var values = (exposureCheckpoints ?? Array.Empty<WeatherExposureCheckpoint>())
                .OrderBy(x => x.CharacterId).ToArray();
            if (values.Select(x => x.CharacterId).Distinct().Count() != values.Length)
                throw new ArgumentException("Weather exposure checkpoints must have unique character IDs.", nameof(exposureCheckpoints));
            WorldSeed = worldSeed; ClimateProfileId = climateProfileId;
            ExposureCheckpoints = new ReadOnlyCollection<WeatherExposureCheckpoint>(values);
        }
        public long WorldSeed { get; }
        public ClimateProfileId ClimateProfileId { get; }
        public IReadOnlyList<WeatherExposureCheckpoint> ExposureCheckpoints { get; }
    }
}
