using System;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Time;
using Game.Simulation.Weather;
using UnityEngine;

namespace Game.Presentation.Weather
{
    /// <summary>Disposable environment projection. Canonical weather remains in WeatherService.</summary>
    public sealed class WeatherPresentationController : MonoBehaviour
    {
        private WeatherService weather;
        private long displayedDay = -1;
        private Light sun;
        private ParticleSystem rainParticles;
        private AudioSource rainAudio;
        private Material rainMaterial;
        private AudioClip rainClip;
        private bool previewActive;
        private bool ownsRainMaterial;

        public bool IsRaining { get; private set; }
        public ParticleSystem RainParticles => rainParticles;
        public AudioSource RainAudio => rainAudio;

        public void Initialize(WeatherService service, Material particleMaterial = null)
        {
            weather = service ?? throw new ArgumentNullException(nameof(service));
            rainMaterial = particleMaterial;
            sun = FindDirectionalLight();
            CreateRainPresentation();
        }

        public void Refresh(long calendarTick)
        {
            if (weather == null || previewActive) return;
            var state = weather.GetWeatherAtCalendarTick(calendarTick);
            if (state.AbsoluteDay == displayedDay) return;
            displayedDay = state.AbsoluteDay;
            ApplyState(state);
        }

        /// <summary>Presentation-only preview used by visual validation. Canonical weather is never mutated.</summary>
        public void ShowPreview(WeatherState state)
        {
            previewActive = true;
            displayedDay = -1;
            ApplyState(state);
        }

        public void ClearPreview(long calendarTick)
        {
            previewActive = false;
            displayedDay = -1;
            Refresh(calendarTick);
        }

        private void ApplyState(WeatherState state)
        {
            var overcast = state.Type == WeatherType.Cloudy || state.Precipitation != PrecipitationType.None;
            RenderSettings.fog = overcast;
            RenderSettings.fogColor = state.Snowing ? new Color(0.72f, 0.77f, 0.82f) : new Color(0.36f, 0.42f, 0.48f);
            RenderSettings.fogDensity = state.Type == WeatherType.HeavyRain ? 0.012f : overcast ? 0.006f : 0.002f;
            RenderSettings.ambientLight = overcast ? new Color(0.48f, 0.5f, 0.54f) : new Color(0.68f, 0.66f, 0.58f);
            if (sun != null)
            {
                sun.intensity = state.Type == WeatherType.Clear || state.Type == WeatherType.HeatWave ? 1.05f :
                    state.Type == WeatherType.HeavyRain || state.Type == WeatherType.Snow ? 0.55f : 0.78f;
                sun.color = state.Type == WeatherType.ColdSnap || state.Type == WeatherType.Snow
                    ? new Color(0.78f, 0.86f, 1f) : new Color(1f, 0.94f, 0.82f);
            }
            IsRaining = state.Precipitation == PrecipitationType.Rain;
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = state.Type == WeatherType.HeavyRain ? 1450f : 680f;
                rainParticles.gameObject.SetActive(IsRaining);
                if (IsRaining && !rainParticles.isPlaying) rainParticles.Play();
            }
            if (rainAudio != null)
            {
                rainAudio.volume = state.Type == WeatherType.HeavyRain ? 0.24f : 0.14f;
                if (IsRaining && !rainAudio.isPlaying) rainAudio.Play();
                else if (!IsRaining && rainAudio.isPlaying) rainAudio.Stop();
            }
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
            if (!IsRaining || rainParticles == null || UnityEngine.Camera.main == null) return;
            rainParticles.transform.position = UnityEngine.Camera.main.transform.position + Vector3.up * 13f;
        }

        private void OnDestroy()
        {
            if (ownsRainMaterial && rainMaterial != null) Destroy(rainMaterial);
            if (rainClip != null) Destroy(rainClip);
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
