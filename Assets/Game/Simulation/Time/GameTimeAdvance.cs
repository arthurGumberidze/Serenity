using System;

namespace Game.Simulation.Time
{
    public readonly struct GameTimeAdvance
    {
        public TimeSpan CalendarDelta { get; }
        public TimeSpan BiologicalDelta { get; }

        public GameTimeAdvance(TimeSpan calendarDelta, TimeSpan biologicalDelta)
        {
            CalendarDelta = calendarDelta;
            BiologicalDelta = biologicalDelta;
        }
    }
}
