using System;
using Game.Domain.Time;

namespace Game.Simulation.Time
{
    /// <summary>
    /// Central clock advanced by a session-level driver. It has no dependency on Unity frame time.
    /// </summary>
    public sealed class GameClock
    {
        private const int CalendarTicksPerRealTickAtNormalSpeed = 60;
        private readonly GameTimeSettings settings;

        public GameTimeState State { get; }
        public GameSpeed Speed => (GameSpeed)State.SpeedMultiplier;
        public bool IsPaused => State.IsPaused;
        public TimeSpan CalendarElapsed => TimeSpan.FromTicks(State.CalendarTicks);
        public TimeSpan BiologicalElapsed => TimeSpan.FromTicks(State.BiologicalTicks);

        public GameClock(GameTimeState state, GameTimeSettings settings = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.settings = settings ?? new GameTimeSettings();
            ValidateState(state);
        }

        public void SetSpeed(GameSpeed speed)
        {
            ValidateSpeed((int)speed);
            State.SpeedMultiplier = (int)speed;
        }

        public void Pause() => State.IsPaused = true;

        public void Resume() => State.IsPaused = false;

        public GameTimeAdvance Advance(TimeSpan realDelta)
        {
            if (realDelta < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(realDelta));
            ValidateState(State);
            if (State.IsPaused || realDelta == TimeSpan.Zero)
                return new GameTimeAdvance(TimeSpan.Zero, TimeSpan.Zero);

            long calendarDeltaTicks;
            long biologicalDeltaTicks;
            long nextCalendarTicks;
            long nextBiologicalTicks;

            checked
            {
                var scaledRealTicks = realDelta.Ticks * State.SpeedMultiplier;
                calendarDeltaTicks = scaledRealTicks * CalendarTicksPerRealTickAtNormalSpeed;
                biologicalDeltaTicks = calendarDeltaTicks * settings.BiologicalMultiplier;
                nextCalendarTicks = State.CalendarTicks + calendarDeltaTicks;
                nextBiologicalTicks = State.BiologicalTicks + biologicalDeltaTicks;
            }

            State.CalendarTicks = nextCalendarTicks;
            State.BiologicalTicks = nextBiologicalTicks;
            return new GameTimeAdvance(
                TimeSpan.FromTicks(calendarDeltaTicks),
                TimeSpan.FromTicks(biologicalDeltaTicks));
        }

        private static void ValidateState(GameTimeState state)
        {
            if (state.CalendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(state.CalendarTicks));
            if (state.BiologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(state.BiologicalTicks));
            ValidateSpeed(state.SpeedMultiplier);
        }

        private static void ValidateSpeed(int speed)
        {
            if (speed != 1 && speed != 2 && speed != 3 && speed != 5 && speed != 10)
                throw new ArgumentOutOfRangeException(nameof(speed), "Supported speeds are x1, x2, x3, x5 and x10.");
        }
    }
}
