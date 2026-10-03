using System;

namespace Game.Domain.Time
{
    /// <summary>Canonical, persistence-ready state of the game clocks.</summary>
    [Serializable]
    public sealed class GameTimeState
    {
        public long CalendarTicks;
        public long BiologicalTicks;
        public int SpeedMultiplier = 1;
        public bool IsPaused;

        public GameTimeState()
        {
        }

        public GameTimeState(long calendarTicks, long biologicalTicks, int speedMultiplier, bool isPaused)
        {
            if (calendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(calendarTicks));
            if (biologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(biologicalTicks));

            CalendarTicks = calendarTicks;
            BiologicalTicks = biologicalTicks;
            SpeedMultiplier = speedMultiplier;
            IsPaused = isPaused;
        }
    }
}
