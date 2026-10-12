using System;
using Game.Domain.Time;
using Game.Presentation.AI;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.Input
{
    /// <summary>Development-only command/read surface over the one canonical GameClock.</summary>
    public sealed class TimeDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private static readonly GameSpeed[] OrderedSpeeds =
        {
            GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Faster, GameSpeed.VeryFast, GameSpeed.Maximum
        };

        private LocalGameplayInputSource input;
        private Tier1AiRuntimeDriver runtime;
        private WorldPointerRaycaster raycaster;

        public GameClock Clock => runtime?.Clock;
        public static System.Collections.Generic.IReadOnlyList<GameSpeed> SupportedSpeeds => OrderedSpeeds;

        public void Initialize(LocalGameplayInputSource inputSource, Tier1AiRuntimeDriver runtimeDriver,
            WorldPointerRaycaster worldRaycaster)
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
            input = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            runtime = runtimeDriver ?? throw new ArgumentNullException(nameof(runtimeDriver));
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            Subscribe();
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = new Rect(12f, Mathf.Max(12f, Screen.height - 100f), 430f, 88f);
            return Clock != null && (Application.isEditor || Debug.isDebugBuild);
        }

        public void TogglePause() => runtime.SetPaused(!Clock.IsPaused);
        public void SetSpeed(GameSpeed speed) => runtime.SetSpeed(speed);
        public void StepSlower() => SetSpeed(PreviousSpeed(Clock.Speed));
        public void StepFaster() => SetSpeed(NextSpeed(Clock.Speed));

        public static GameSpeed PreviousSpeed(GameSpeed current)
        {
            var index = Array.IndexOf(OrderedSpeeds, current);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(current));
            return OrderedSpeeds[Math.Max(0, index - 1)];
        }

        public static GameSpeed NextSpeed(GameSpeed current)
        {
            var index = Array.IndexOf(OrderedSpeeds, current);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(current));
            return OrderedSpeeds[Math.Min(OrderedSpeeds.Length - 1, index + 1)];
        }

        private void OnEnable()
        {
            Subscribe();
            raycaster?.RegisterUiBlocker(this);
        }

        private void OnDisable()
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
        }

        private void Subscribe()
        {
            if (input == null) return;
            input.PauseToggled -= TogglePause;
            input.PauseToggled += TogglePause;
            input.TimeSlowerRequested -= StepSlower;
            input.TimeSlowerRequested += StepSlower;
            input.TimeFasterRequested -= StepFaster;
            input.TimeFasterRequested += StepFaster;
        }

        private void Unsubscribe()
        {
            if (input == null) return;
            input.PauseToggled -= TogglePause;
            input.TimeSlowerRequested -= StepSlower;
            input.TimeFasterRequested -= StepFaster;
        }

        private void OnGUI()
        {
            if (!TryGetUiBlockingRect(out var panelRect)) return;
            GUI.Box(panelRect, GUIContent.none);
            GUI.BeginGroup(panelRect);
            var ticks = Clock.State.CalendarTicks;
            GUI.Label(new Rect(8f, 6f, 414f, 20f), RussianTextCatalog.Format("debug.time.date",
                GameCalendar.Year(ticks), GameCalendar.DayOfYear(ticks), GameCalendar.HourOfDay(ticks), GameCalendar.MinuteOfHour(ticks)));
            GUI.Label(new Rect(8f, 27f, 190f, 20f), RussianTextCatalog.Format("debug.time.speed", (int)Clock.Speed,
                Clock.IsPaused ? RussianTextCatalog.Get("common.yes") : RussianTextCatalog.Get("common.no")));
            if (GUI.Button(new Rect(202f, 25f, 68f, 23f), RussianTextCatalog.Get(Clock.IsPaused ? "debug.time.resume" : "debug.time.pause"))) TogglePause();
            if (GUI.Button(new Rect(276f, 25f, 68f, 23f), RussianTextCatalog.Get("debug.time.slower"))) StepSlower();
            if (GUI.Button(new Rect(350f, 25f, 72f, 23f), RussianTextCatalog.Get("debug.time.faster"))) StepFaster();
            GUI.Label(new Rect(8f, 53f, 414f, 20f), RussianTextCatalog.Get("debug.time.help"));
            GUI.EndGroup();
        }
    }
}
