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

        public void Initialize(WeatherService service)
        {
            weather = service ?? throw new ArgumentNullException(nameof(service));
            sun = FindDirectionalLight();
        }

        public void Refresh(long calendarTick)
        {
            if (weather == null) return;
            var state = weather.GetWeatherAtCalendarTick(calendarTick);
            if (state.AbsoluteDay == displayedDay) return;
            displayedDay = state.AbsoluteDay;
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
