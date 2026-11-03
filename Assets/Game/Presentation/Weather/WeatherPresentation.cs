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
        private LineRenderer lightning;
        private Light lightningFlash;
        private Material lightningMaterial;
        private float lightningVisibleUntil;
        private long lastLightningMinute = long.MinValue;
        private P5InhabitableRegionPresentation region;
        private P95NaturalAtmosphereProfile atmosphereProfile;
        private Material runtimeSkybox;
        private Material originalSkybox;
        private bool previewActive;
        private bool ownsRainMaterial;
        private bool ownsAtmosphereProfile;
        private WorldMapArtDirection mapArt;
        private Vector4 mapTransform;

        public void ConfigureMapArt(WorldMapArtDirection art, Vector4 worldToDesign)
        { mapArt = art; mapTransform = worldToDesign; }

        private void ApplyLocalFog()
        {
            if (mapArt == null || Camera.main == null) return;
            var point = Camera.main.transform.position;
            var haze = mapArt.Haze(new Vector2(point.x * mapTransform.z + mapTransform.x,
                point.z * mapTransform.w + mapTransform.y));
            RenderSettings.fogDensity = CurrentVisualState.FogDensity * haze;
        }

        public P95AtmosphereVisualState CurrentVisualState { get; private set; }

        public bool IsRaining { get; private set; }
        public ParticleSystem RainParticles => rainParticles;
        public ParticleSystem SnowParticles => snowParticles;
        public AudioSource RainAudio => rainAudio;
        public bool IsSnowing { get; private set; }
        public bool IsLightningReady => lightning != null && lightning.sharedMaterial != null;

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
            CreateLightningPresentation();
        }

        public void Refresh(long calendarTick)
        {
            if (weather == null || previewActive) return;
            var state = weather.GetWeatherAtCalendarTick(calendarTick);
            var bucket = calendarTick / TimeSpan.TicksPerMinute;
            if (bucket == displayedTimeBucket) { ApplyLocalFog(); return; }
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
            RenderSettings.ambientLight = Color.Lerp(CurrentVisualState.AmbientColor, Color.white,
                0.18f * CurrentVisualState.Daylight);
            RenderSettings.ambientIntensity = Mathf.Max(0.16f, CurrentVisualState.AmbientIntensity);
            RenderSettings.reflectionIntensity = CurrentVisualState.ReflectionIntensity;
            ApplySkybox(CurrentVisualState.SkyColor);
            if (sun != null)
            {
                sun.transform.rotation = CurrentVisualState.LightRotation;
                sun.intensity = Mathf.Max(0.05f, CurrentVisualState.LightIntensity);
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
            var storming = state.Type == WeatherType.HeavyRain;
            var minute = calendarTick / TimeSpan.TicksPerMinute;
            if (storming && (lastLightningMinute == long.MinValue || minute - lastLightningMinute >= 7))
            {
                TriggerLightning(minute);
                lastLightningMinute = minute;
            }
            else if (!storming)
            {
                lastLightningMinute = long.MinValue;
                HideLightning();
            }
            region?.ApplySeason(state.Season, state.Snowing);
            Shader.SetGlobalFloat("_WorldMapSeason", state.Season == Season.Spring ? 0 :
                state.Season == Season.Summer ? 1 : state.Season == Season.Autumn ? 2 : 3);
            ApplyLocalFog();
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
            var imported = WorldPropCatalog.Current != null && WorldPropCatalog.Current.Find("rain") != null;
            var rainObject = imported ? WorldPropCatalog.Create("rain", transform) : new GameObject("P3 Rain Presentation");
            rainObject.transform.SetParent(transform, false);
            rainParticles = rainObject.GetComponent<ParticleSystem>() ?? rainObject.AddComponent<ParticleSystem>();
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rainParticles.main;
            main.loop = true;
            main.prewarm = true;
            main.useUnscaledTime = true;
            main.simulationSpeed = 1f;
            main.startDelay = 0f;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = new Color(0.68f, 0.79f, 0.9f, 0.6f);
            main.maxParticles = 2600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.4f;
            var emission = rainParticles.emission;
            emission.enabled = true;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
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
            if (!imported) renderer.sharedMaterial = rainMaterial;

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
            var imported = WorldPropCatalog.Current != null && WorldPropCatalog.Current.Find("snow") != null;
            var snowObject = imported ? WorldPropCatalog.Create("snow", transform) : new GameObject("P9.5 Snow Presentation");
            snowObject.transform.SetParent(transform, false);
            snowParticles = snowObject.GetComponent<ParticleSystem>() ?? snowObject.AddComponent<ParticleSystem>();
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            if (!imported) renderer.sharedMaterial = rainMaterial;
            snowObject.SetActive(false);
        }

        private void CreateLightningPresentation()
        {
            if (lightning != null) return;
            var lightningObject = new GameObject("P10.9 Storm Lightning");
            lightningObject.transform.SetParent(transform, false);
            lightning = lightningObject.AddComponent<LineRenderer>();
            lightning.useWorldSpace = true;
            lightning.alignment = LineAlignment.View;
            lightning.textureMode = LineTextureMode.Tile;
            lightning.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(0.75f, 0.09f),
                new Keyframe(1f, 0.025f));
            lightning.numCornerVertices = 2;
            lightning.numCapVertices = 2;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("A lightning shader is required.");
            lightningMaterial = new Material(shader) { name = "P10.9 Runtime Lightning" };
            var art = UnityEngine.Resources.Load<CompactWorldArtCatalog>("World/CompactWorldArt");
            var texture = art != null ? art.LightningTexture : null;
            if (lightningMaterial.HasProperty("_BaseMap")) lightningMaterial.SetTexture("_BaseMap", texture);
            if (lightningMaterial.HasProperty("_MainTex")) lightningMaterial.SetTexture("_MainTex", texture);
            if (lightningMaterial.HasProperty("_BaseColor"))
                lightningMaterial.SetColor("_BaseColor", new Color(0.72f, 0.86f, 1f, 0.95f));
            if (lightningMaterial.HasProperty("_Color"))
                lightningMaterial.SetColor("_Color", new Color(0.72f, 0.86f, 1f, 0.95f));
            if (lightningMaterial.HasProperty("_Surface")) lightningMaterial.SetFloat("_Surface", 1f);
            if (lightningMaterial.HasProperty("_Blend")) lightningMaterial.SetFloat("_Blend", 2f);
            if (lightningMaterial.HasProperty("_SrcBlend"))
                lightningMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (lightningMaterial.HasProperty("_DstBlend"))
                lightningMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (lightningMaterial.HasProperty("_ZWrite")) lightningMaterial.SetFloat("_ZWrite", 0f);
            lightningMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            lightningMaterial.SetOverrideTag("RenderType", "Transparent");
            lightningMaterial.renderQueue = 3100;
            lightning.sharedMaterial = lightningMaterial;
            lightningFlash = lightningObject.AddComponent<Light>();
            lightningFlash.type = LightType.Point;
            lightningFlash.color = new Color(0.72f, 0.84f, 1f);
            lightningFlash.range = 70f;
            lightningFlash.intensity = 2.8f;
            lightningFlash.shadows = LightShadows.None;
            lightningObject.SetActive(false);
        }

        private void TriggerLightning(long minute)
        {
            if (lightning == null || Camera.main == null) return;
            var random = new System.Random(unchecked((int)(minute ^ 0x51A7B01L)));
            var camera = Camera.main;
            var focus = camera.transform.position + camera.transform.forward * 18f;
            var end = new Vector3(focus.x + (float)(random.NextDouble() * 24d - 12d), focus.y,
                focus.z + (float)(random.NextDouble() * 24d - 12d));
            if (Physics.Raycast(new Vector3(end.x, camera.transform.position.y + 120f, end.z), Vector3.down,
                    out var hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) end = hit.point;
            else end.y = Mathf.Max(0f, focus.y - 20f);
            var start = end + new Vector3((float)(random.NextDouble() * 16d - 8d), 42f,
                (float)(random.NextDouble() * 16d - 8d));
            const int points = 13;
            lightning.positionCount = points;
            for (var index = 0; index < points; index++)
            {
                var t = index / (points - 1f);
                var point = Vector3.Lerp(start, end, t);
                if (index > 0 && index < points - 1)
                    point += new Vector3((float)(random.NextDouble() * 3d - 1.5d), 0f,
                        (float)(random.NextDouble() * 3d - 1.5d)) * (1f - t * 0.65f);
                lightning.SetPosition(index, point);
            }
            lightning.gameObject.SetActive(true);
            lightningFlash.transform.position = end + Vector3.up * 8f;
            lightningFlash.enabled = true;
            lightningVisibleUntil = Time.unscaledTime + 0.16f;
        }

        private void HideLightning()
        {
            if (lightning != null) lightning.gameObject.SetActive(false);
            if (lightningFlash != null) lightningFlash.enabled = false;
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
            if (lightning != null && lightning.gameObject.activeSelf && Time.unscaledTime >= lightningVisibleUntil)
                HideLightning();
            if (UnityEngine.Camera.main == null) return;
            var precipitationPosition = UnityEngine.Camera.main.transform.position + Vector3.up * 13f;
            if (IsRaining && rainParticles != null) rainParticles.transform.position = precipitationPosition;
            if (IsSnowing && snowParticles != null) snowParticles.transform.position = precipitationPosition;
        }

        private void OnDestroy()
        {
            if (ownsRainMaterial && rainMaterial != null) Destroy(rainMaterial);
            if (rainClip != null) Destroy(rainClip);
            if (lightningMaterial != null) Destroy(lightningMaterial);
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
