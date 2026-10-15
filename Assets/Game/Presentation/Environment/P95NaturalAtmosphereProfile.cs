using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Time;
using Game.Domain.Weather;
using UnityEngine;

namespace Game.Presentation.Environment
{
    public enum P95MapLocationKind
    {
        Settlement = 0,
        Forest = 1,
        Ford = 2,
        Hills = 3,
        Field = 4
    }

    [Serializable]
    public sealed class P95MapLocationDefinition
    {
        [SerializeField] private string locationId;
        [SerializeField] private P95MapLocationKind kind;
        [SerializeField] private Color groundColor = Color.white;
        [SerializeField, Range(0.5f, 2f)] private float scale = 1f;
        [SerializeField, Range(0f, 4f)] private float relief = 1f;

        public string LocationId => locationId;
        public P95MapLocationKind Kind => kind;
        public Color GroundColor => groundColor;
        public float Scale => scale;
        public float Relief => relief;

        public P95MapLocationDefinition Configure(string id, P95MapLocationKind visualKind, Color color,
            float visualScale, float reliefHeight)
        {
            locationId = id;
            kind = visualKind;
            groundColor = color;
            scale = visualScale;
            relief = reliefHeight;
            return this;
        }
    }

    public readonly struct P95AtmosphereVisualState
    {
        public P95AtmosphereVisualState(long calendarTick, float solarHour, float daylight, bool night,
            Quaternion lightRotation, Color lightColor, float lightIntensity, float shadowStrength,
            Color ambientColor, float ambientIntensity, Color fogColor, float fogDensity,
            Color skyColor, Color waterColor, float waterSmoothness, float reflectionIntensity)
        {
            CalendarTick = calendarTick;
            SolarHour = solarHour;
            Daylight = daylight;
            IsNight = night;
            LightRotation = lightRotation;
            LightColor = lightColor;
            LightIntensity = lightIntensity;
            ShadowStrength = shadowStrength;
            AmbientColor = ambientColor;
            AmbientIntensity = ambientIntensity;
            FogColor = fogColor;
            FogDensity = fogDensity;
            SkyColor = skyColor;
            WaterColor = waterColor;
            WaterSmoothness = waterSmoothness;
            ReflectionIntensity = reflectionIntensity;
        }

        public long CalendarTick { get; }
        public float SolarHour { get; }
        public float Daylight { get; }
        public bool IsNight { get; }
        public Quaternion LightRotation { get; }
        public Color LightColor { get; }
        public float LightIntensity { get; }
        public float ShadowStrength { get; }
        public Color AmbientColor { get; }
        public float AmbientIntensity { get; }
        public Color FogColor { get; }
        public float FogDensity { get; }
        public Color SkyColor { get; }
        public Color WaterColor { get; }
        public float WaterSmoothness { get; }
        public float ReflectionIntensity { get; }
    }

    /// <summary>
    /// Immutable authoring data for P9.5 atmosphere and map art. Canonical time, season and weather remain in
    /// GameClock and WeatherService; this ScriptableObject contains definitions only.
    /// </summary>
    [CreateAssetMenu(menuName = "Serenity/P9.5 Natural Atmosphere", fileName = "P95_NaturalAtmosphere")]
    public sealed class P95NaturalAtmosphereProfile : ScriptableObject
    {
        [Header("Day and night")]
        [SerializeField] private Color daylightColor = new Color(1f, 0.91f, 0.75f);
        [SerializeField] private Color dawnColor = new Color(1f, 0.58f, 0.34f);
        [SerializeField] private Color moonlightColor = new Color(0.46f, 0.58f, 0.78f);
        [SerializeField] private Color dayAmbient = new Color(0.55f, 0.61f, 0.57f);
        [SerializeField] private Color nightAmbient = new Color(0.105f, 0.15f, 0.21f);
        [SerializeField] private Color daySky = new Color(0.47f, 0.66f, 0.78f);
        [SerializeField] private Color nightSky = new Color(0.025f, 0.045f, 0.085f);
        [SerializeField, Range(0f, 2f)] private float dayLightIntensity = 1.28f;
        [SerializeField, Range(0f, 1f)] private float nightLightIntensity = 0.16f;

        [Header("Air and water")]
        [SerializeField] private Color clearFog = new Color(0.55f, 0.64f, 0.66f);
        [SerializeField] private Color rainFog = new Color(0.31f, 0.38f, 0.43f);
        [SerializeField] private Color snowFog = new Color(0.72f, 0.78f, 0.82f);
        [SerializeField] private Color dayWater = new Color(0.16f, 0.42f, 0.47f, 0.88f);
        [SerializeField] private Color nightWater = new Color(0.035f, 0.12f, 0.19f, 0.94f);
        [SerializeField, Range(0.0001f, 0.02f)] private float clearFogDensity = 0.0018f;
        [SerializeField, Range(0.0001f, 0.02f)] private float severeFogDensity = 0.0085f;

        [Header("Season accents")]
        [SerializeField] private Color springAccent = new Color(0.67f, 0.82f, 0.58f);
        [SerializeField] private Color summerAccent = new Color(0.72f, 0.78f, 0.48f);
        [SerializeField] private Color autumnAccent = new Color(0.72f, 0.49f, 0.27f);
        [SerializeField] private Color winterAccent = new Color(0.66f, 0.75f, 0.78f);

        [Header("Global map")]
        [SerializeField] private Color mapWater = new Color(0.055f, 0.19f, 0.23f);
        [SerializeField] private Color mapLand = new Color(0.29f, 0.38f, 0.22f);
        [SerializeField] private Color mapRoute = new Color(0.64f, 0.55f, 0.37f);
        [SerializeField] private P95MapLocationDefinition[] locations = Array.Empty<P95MapLocationDefinition>();

        public Color MapWater => mapWater;
        public Color MapLand => mapLand;
        public Color MapRoute => mapRoute;
        public IReadOnlyList<P95MapLocationDefinition> Locations => locations;

        public void ConfigureDefaults()
        {
            locations = new[]
            {
                new P95MapLocationDefinition().Configure("starter_settlement", P95MapLocationKind.Settlement,
                    new Color(0.39f, 0.36f, 0.22f), 1.15f, 0.75f),
                new P95MapLocationDefinition().Configure("enemy_settlement", P95MapLocationKind.Settlement,
                    new Color(0.35f, 0.29f, 0.2f), 1.1f, 0.95f),
                new P95MapLocationDefinition().Configure("north_forest", P95MapLocationKind.Forest,
                    new Color(0.16f, 0.34f, 0.18f), 1.2f, 1.35f),
                new P95MapLocationDefinition().Configure("river_ford", P95MapLocationKind.Ford,
                    new Color(0.38f, 0.46f, 0.35f), 0.95f, 0.35f),
                new P95MapLocationDefinition().Configure("east_hills", P95MapLocationKind.Hills,
                    new Color(0.38f, 0.4f, 0.34f), 1.25f, 2.35f),
                new P95MapLocationDefinition().Configure("south_field", P95MapLocationKind.Field,
                    new Color(0.5f, 0.49f, 0.27f), 1.2f, 0.45f)
            };
        }

        public void ValidateConfiguration()
        {
            var required = new[]
            {
                "starter_settlement", "enemy_settlement", "north_forest",
                "river_ford", "east_hills", "south_field"
            };
            if (locations == null || locations.Length != required.Length ||
                locations.Any(x => x == null || string.IsNullOrWhiteSpace(x.LocationId)) ||
                locations.Select(x => x.LocationId).Distinct(StringComparer.Ordinal).Count() != locations.Length ||
                required.Any(id => locations.All(x => !string.Equals(x.LocationId, id, StringComparison.Ordinal))))
                throw new InvalidOperationException("P9.5 atmosphere requires six unique canonical map visuals.");
            if (clearFogDensity <= 0f || severeFogDensity < clearFogDensity || dayLightIntensity <= 0f ||
                nightLightIntensity < 0.08f)
                throw new InvalidOperationException("P9.5 atmosphere readability settings are invalid.");
        }

        public P95MapLocationDefinition GetLocation(string locationId)
        {
            var result = locations.FirstOrDefault(x => string.Equals(x.LocationId, locationId,
                StringComparison.Ordinal));
            return result ?? throw new KeyNotFoundException("Unknown P9.5 location visual '" + locationId + "'.");
        }

        public P95AtmosphereVisualState Evaluate(long calendarTick, WeatherState weather)
        {
            ValidateConfiguration();
            var hour = GameCalendar.HourOfDay(calendarTick) + GameCalendar.MinuteOfHour(calendarTick) / 60f;
            var solarWave = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f);
            var daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.16f, 0.72f, solarWave));
            var dawn = 1f - Mathf.Clamp01(Mathf.Abs(hour - 6.5f) / 2.25f);
            var dusk = 1f - Mathf.Clamp01(Mathf.Abs(hour - 18.5f) / 2.25f);
            var warmEdge = Mathf.Max(dawn, dusk);
            var overcast = weather.Type == WeatherType.Cloudy || weather.Precipitation != PrecipitationType.None;
            var heavy = weather.Type == WeatherType.HeavyRain || weather.Type == WeatherType.Snow;
            var cloudLight = overcast ? (heavy ? 0.56f : 0.76f) : 1f;
            var lightColor = Color.Lerp(moonlightColor, Color.Lerp(daylightColor, dawnColor, warmEdge * 0.72f), daylight);
            var lightIntensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity * cloudLight, daylight);
            var seasonAccent = SeasonAccent(weather.Season);
            var ambient = Color.Lerp(nightAmbient, Color.Lerp(dayAmbient, seasonAccent, 0.12f), daylight);
            if (overcast) ambient = Color.Lerp(ambient, rainFog, heavy ? 0.34f : 0.2f);
            var fogTarget = weather.Snowing ? snowFog : overcast ? rainFog : clearFog;
            var fog = Color.Lerp(Color.Lerp(nightSky, fogTarget, 0.56f), fogTarget, daylight);
            var weatherWeight = heavy ? 1f : overcast ? 0.5f : 0f;
            var fogDensity = Mathf.Lerp(clearFogDensity, severeFogDensity, weatherWeight);
            fogDensity *= Mathf.Lerp(1.18f, 1f, daylight);
            var sky = Color.Lerp(nightSky, Color.Lerp(daySky, seasonAccent, 0.08f), daylight);
            if (overcast) sky = Color.Lerp(sky, fogTarget, heavy ? 0.62f : 0.42f);
            var water = Color.Lerp(nightWater, Color.Lerp(dayWater, seasonAccent, 0.09f), daylight);
            if (overcast) water = Color.Lerp(water, rainFog, 0.24f);
            var yaw = 112f + hour * 7.5f;
            var pitch = daylight > 0.12f ? Mathf.Lerp(12f, 58f, Mathf.Clamp01(solarWave)) : 32f;
            if (daylight <= 0.12f) yaw += 180f;
            return new P95AtmosphereVisualState(calendarTick, hour, daylight, daylight < 0.18f,
                Quaternion.Euler(pitch, yaw, 0f), lightColor, lightIntensity,
                Mathf.Lerp(0.42f, 0.82f, daylight), ambient, Mathf.Lerp(0.42f, 0.9f, daylight),
                fog, fogDensity, sky, water, overcast ? 0.72f : 0.9f,
                Mathf.Lerp(0.34f, overcast ? 0.55f : 0.78f, daylight));
        }

        public string ComputeLayoutHash()
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                foreach (var location in locations.OrderBy(x => x.LocationId, StringComparer.Ordinal))
                {
                    foreach (var character in location.LocationId) { hash ^= character; hash *= 1099511628211UL; }
                    hash ^= (ulong)location.Kind; hash *= 1099511628211UL;
                    hash ^= (ulong)Mathf.RoundToInt(location.Scale * 1000f); hash *= 1099511628211UL;
                    hash ^= (ulong)Mathf.RoundToInt(location.Relief * 1000f); hash *= 1099511628211UL;
                }
                return hash.ToString("X16");
            }
        }

        private Color SeasonAccent(Season season) => season switch
        {
            Season.Spring => springAccent,
            Season.Summer => summerAccent,
            Season.Autumn => autumnAccent,
            _ => winterAccent
        };
    }
}
