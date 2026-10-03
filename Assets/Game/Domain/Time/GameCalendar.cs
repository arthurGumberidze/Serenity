using System;

namespace Game.Domain.Time
{
    public static class GameCalendar
    {
        public const int HoursPerDay = 24;
        public const int MinutesPerHour = 60;
        public const int DaysPerYear = 365;

        public static TimeSpan Elapsed(long calendarTicks)
        {
            if (calendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(calendarTicks));
            return TimeSpan.FromTicks(calendarTicks);
        }

        public static long CompletedDays(long calendarTicks)
        {
            return Elapsed(calendarTicks).Ticks / TimeSpan.TicksPerDay;
        }

        public static long Year(long calendarTicks)
        {
            return checked(CompletedDays(calendarTicks) / DaysPerYear + 1);
        }

        public static int DayOfYear(long calendarTicks)
        {
            return (int)(CompletedDays(calendarTicks) % DaysPerYear) + 1;
        }

        public static int HourOfDay(long calendarTicks)
        {
            return Elapsed(calendarTicks).Hours;
        }

        public static int MinuteOfHour(long calendarTicks)
        {
            return Elapsed(calendarTicks).Minutes;
        }
    }
}
