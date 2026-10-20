using System;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Presentation.Environment;
using Game.Simulation.Time;
using Game.Simulation.Weather;
using UnityEngine;

namespace Game.Presentation.Weather
{
    /// <summary>Disposable environment projection. Canonical weather remains in WeatherService.</summary>
    public sealed class WeatherPresentationController : MonoBehaviour
    {
        private WeatherService weather;
        private long displayedTimeBucket = -1;
        private Light sun;
        private ParticleSystem rainParticles;
        private ParticleSystem snowParticles;
        private AudioSource rainAudio;
        private Material rainMaterial;
        private AudioClip rainClip;
        private P5InhabitableRegionPresentation region;
        private P95NaturalAtmosphereProfile atmosphereProfile;
        private Material runtimeSkybox;
        private Material originalSkybox;
        private bool previewActive;
        private bool ownsRainMaterial;
        private bool ownsAtmosphereProfile;

        public P95AtmosphereVisualState CurrentVisualState { get; private set; }

        public bool IsRaining { get; private set; }
        public ParticleSystem RainParticles => rainParticles;
        public ParticleSystem SnowParticles => snowParticles;
        public AudioSource RainAudio => rainAudio;
        public bool IsSnowing { get; private set; }

        public void Initialize(WeatherService service, Material particleMaterial = null,
            P5InhabitableRegionPresentation inhabitableRegion = null,
            P95NaturalAtmosphereProfile profile = null)
        {
            weather = service ?? throw new ArgumentNullException(nameof(service));
            rainMaterial = particleMaterial;
            region = inhabitableRegion;
            atmosphereProfile = profile;
            if (atmosphereProfile == null)
            {
                atmosphereProfile = ScriptableObject.CreateInstance<P95NaturalAtmosphereProfile>();
                atmosphereProfile.ConfigureDefaults();
                ownsAtmosphereProfile = true;
            }
            atmosphereProfile.ValidateConfiguration();
            sun = FindDirectionalLight();
            CreateRainPresentation();
            CreateSnowPresentation();
        }

        public void Refresh(long calendarTick)
        {
            if (weather == null || previewActive) return;
            var state = weather.GetWeatherAtCalendarTick(calendarTick);
            var bucket = calendarTick / TimeSpan.TicksPerMinute;
            if (bucket == displayedTimeBucket) return;
            displayedTimeBucket = bucket;
            ApplyState(state, calendarTick);
        }

        /// <summary>Presentation-only preview used by visual validation. Canonical weather is never mutated.</summary>
        public void ShowPreview(WeatherState state, long? calendarTick = null)
        {
            previewActive = true;
            displayedTimeBucket = -1;
            var tick = calendarTick ?? checked(state.AbsoluteDay * TimeSpan.TicksPerDay + 12 * TimeSpan.TicksPerHour);
            ApplyState(state, tick);
        }

        public void ClearPreview(long calendarTick)
        {
            previewActive = false;
            displayedTimeBucket = -1;
            Refresh(calendarTick);
        }

        private void ApplyState(WeatherState state, long calendarTick)
        {
            CurrentVisualState = atmosphereProfile.Evaluate(calendarTick, state);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = CurrentVisualState.FogColor;
            RenderSettings.fogDensity = CurrentVisualState.FogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // Preserve weather/night colour, but keep units and interaction targets readable.
            RenderSettings.ambientLight = Color.Lerp(CurrentVisualState.AmbientColor, Color.white, 0.18f);
            RenderSettings.ambientIntensity = Mathf.Max(0.7f, CurrentVisualState.AmbientIntensity);
            RenderSettings.reflectionIntensity = CurrentVisualState.ReflectionIntensity;
            ApplySkybox(CurrentVisualState.SkyColor);
            if (sun != null)
            {
                sun.transform.rotation = CurrentVisualState.LightRotation;
                sun.intensity = Mathf.Max(0.45f, CurrentVisualState.LightIntensity);
                sun.color = CurrentVisualState.LightColor;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = CurrentVisualState.ShadowStrength;
            }
            IsRaining = state.Precipitation == PrecipitationType.Rain;
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = state.Type == WeatherType.HeavyRain ? 1450f : 680f;
                rainParticles.gameObject.SetActive(IsRaining);
                if (IsRaining && !rainParticles.isPlaying) rainParticles.Play();
            }
            IsSnowing = state.Precipitation == PrecipitationType.Snow;
            if (snowParticles != null)
            {
                var emission = snowParticles.emission;
                emission.rateOverTime = state.PrecipitationPermille > 650 ? 520f : 260f;
                snowParticles.gameObject.SetActive(IsSnowing);
                if (IsSnowing && !snowParticles.isPlaying) snowParticles.Play();
            }
            if (rainAudio != null)
            {
                rainAudio.volume = state.Type == WeatherType.HeavyRain ? 0.24f : 0.14f;
                if (IsRaining && !rainAudio.isPlaying) rainAudio.Play();
                else if (!IsRaining && rainAudio.isPlaying) rainAudio.Stop();
            }
            region?.ApplySeason(state.Season, state.Snowing);
            ApplyWater(CurrentVisualState);
        }

        private void ApplySkybox(Color color)
        {
            var source = RenderSettings.skybox;
            if (source == null) return;
            if (runtimeSkybox == null)
            {
                originalSkybox = source;
                runtimeSkybox = new Material(source) { name = "P9.5 Runtime Skybox" };
                RenderSettings.skybox = runtimeSkybox;
            }
            if (runtimeSkybox.HasProperty("_SkyTint")) runtimeSkybox.SetColor("_SkyTint", color);
            if (runtimeSkybox.HasProperty("_Tint")) runtimeSkybox.SetColor("_Tint", color);
            if (runtimeSkybox.HasProperty("_Exposure"))
                runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(0.34f, 1.05f, CurrentVisualState.Daylight));
        }

        private void ApplyWater(P95AtmosphereVisualState visual)
        {
            if (region == null || region.WaterRenderer == null) return;
            var block = new MaterialPropertyBlock();
            region.WaterRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", visual.WaterColor);
            block.SetColor("_Color", visual.WaterColor);
            block.SetFloat("_Smoothness", visual.WaterSmoothness);
            region.WaterRenderer.SetPropertyBlock(block);
        }

        private void CreateRainPresentation()
        {
            if (rainParticles != null) return;
            var rainObject = new GameObject("P3 Rain Presentation");
            rainObject.transform.SetParent(transform, false);
            rainParticles = rainObject.AddComponent<ParticleSystem>();
            var main = rainParticles.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = new Color(0.68f, 0.79f, 0.9f, 0.6f);
            main.maxParticles = 2600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.4f;
            var emission = rainParticles.emission;
            emission.rateOverTime = 680f;
            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 1f, 32f);
            var renderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 4.5f;
            if (rainMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("A rain particle shader is required.");
                rainMaterial = new Material(shader) { name = "P3 Rain Runtime Material" };
                rainMaterial.color = new Color(0.68f, 0.79f, 0.9f, 0.52f);
                ownsRainMaterial = true;
            }
            renderer.sharedMaterial = rainMaterial;

            rainAudio = rainObject.AddComponent<AudioSource>();
            rainAudio.loop = true;
            rainAudio.playOnAwake = false;
            rainAudio.spatialBlend = 0f;
            rainAudio.volume = 0.14f;
            rainClip = CreateRainLoop();
            rainAudio.clip = rainClip;
            rainObject.SetActive(false);
        }

        private void CreateSnowPresentation()
        {
            if (snowParticles != null) return;
            var snowObject = new GameObject("P9.5 Snow Presentation");
            snowObject.transform.SetParent(transform, false);
            snowParticles = snowObject.AddComponent<ParticleSystem>();
            var main = snowParticles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.14f);
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.82f);
            main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.035f;
            var emission = snowParticles.emission;
            emission.rateOverTime = 260f;
            var shape = snowParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 1f, 34f);
            var noise = snowParticles.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.32f;
            var renderer = snowParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = rainMaterial;
            snowObject.SetActive(false);
        }

        private static AudioClip CreateRainLoop()
        {
            const int sampleRate = 22050;
            const int seconds = 4;
            var samples = new float[sampleRate * seconds];
            var random = new System.Random(3019);
            var filtered = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var noise = (float)(random.NextDouble() * 2d - 1d);
                filtered = filtered * 0.74f + noise * 0.26f;
                samples[i] = filtered * 0.13f;
            }
            var clip = AudioClip.Create("P3 Rain Loop", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void LateUpdate()
        {
            if (UnityEngine.Camera.main == null) return;
            var precipitationPosition = UnityEngine.Camera.main.transform.position + Vector3.up * 13f;
            if (IsRaining && rainParticles != null) rainParticles.transform.position = precipitationPosition;
            if (IsSnowing && snowParticles != null) snowParticles.transform.position = precipitationPosition;
        }

        private void OnDestroy()
        {
            if (ownsRainMaterial && rainMaterial != null) Destroy(rainMaterial);
            if (rainClip != null) Destroy(rainClip);
            if (runtimeSkybox != null)
            {
                if (RenderSettings.skybox == runtimeSkybox) RenderSettings.skybox = originalSkybox;
                Destroy(runtimeSkybox);
            }
            if (ownsAtmosphereProfile && atmosphereProfile != null) Destroy(atmosphereProfile);
        }

        private static Light FindDirectionalLight()
        {
            foreach (var candidate in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (candidate.type == LightType.Directional) return candidate;
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WeatherDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 330f;
        private const float PanelHeight = 224f;
        private WeatherService weather;
        private GameClock clock;
        private WorldPointerRaycaster raycaster;

        public void Initialize(WeatherService service, GameClock gameClock, WorldPointerRaycaster worldRaycaster)
        {
            weather = service ?? throw new ArgumentNullException(nameof(service));
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return isActiveAndEnabled && weather != null && clock != null;
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);
        private void OnDestroy() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (weather == null || clock == null) return;
            var state = weather.GetWeatherAtCalendarTick(clock.State.CalendarTicks);
            var next = weather.GetWeatherForDay(state.AbsoluteDay + 1);
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label(RussianTextCatalog.Get("debug.weather.title"));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.date", GameCalendar.Year(clock.State.CalendarTicks),
                GameCalendar.DayOfYear(clock.State.CalendarTicks), GameCalendar.HourOfDay(clock.State.CalendarTicks),
                GameCalendar.MinuteOfHour(clock.State.CalendarTicks)));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.summary", RussianTextCatalog.Enum(state.Season),
                RussianTextCatalog.Enum(state.Type), RussianTextCatalog.Decimal(state.TemperatureCelsius)));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.precipitation", RussianTextCatalog.Enum(state.Precipitation),
                RussianTextCatalog.Decimal(state.PrecipitationPermille / 10f, "0"), RussianTextCatalog.Decimal(state.WindPermille / 10f, "0")));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.crop", Percent(state.Modifiers.CropGrowthPerThousand),
                Percent(state.Modifiers.OutdoorWorkPerThousand)));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.movement", Percent(state.Modifiers.MovementPerThousand),
                Percent(state.Modifiers.WildFoodGatheringPerThousand)));
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.health", RussianTextCatalog.Enum(state.ExposureKind),
                RussianTextCatalog.Decimal(state.Modifiers.HealthExposureRiskPermille / 10f, "0")));
            GUILayout.Space(4f);
            GUILayout.Label(RussianTextCatalog.Format("debug.weather.tomorrow", RussianTextCatalog.Enum(next.Season),
                RussianTextCatalog.Enum(next.Type), RussianTextCatalog.Decimal(next.TemperatureCelsius)));
            GUILayout.Label(RussianTextCatalog.Get("debug.weather.note"));
            GUILayout.EndArea();
        }

        private static Rect GetPanelRect() => new Rect(Mathf.Max(850f, Screen.width - 750f), 12f, PanelWidth, PanelHeight);
        private static string Percent(int perThousand) => (perThousand / 10f).ToString("0") + "%";
    }
}
