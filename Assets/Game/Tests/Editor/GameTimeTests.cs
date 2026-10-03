using System;
using Game.Domain.Time;
using Game.Simulation.Time;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class GameTimeTests
    {
        [Test]
        public void X1_AdvancesOneCalendarDayInTwentyFourRealMinutes()
        {
            var clock = CreateClock();

            clock.Advance(TimeSpan.FromMinutes(24));

            Assert.That(clock.CalendarElapsed, Is.EqualTo(TimeSpan.FromDays(1)));
            Assert.That(GameCalendar.CompletedDays(clock.State.CalendarTicks), Is.EqualTo(1));
        }

        [TestCase(GameSpeed.Normal, 1)]
        [TestCase(GameSpeed.Fast, 2)]
        [TestCase(GameSpeed.Faster, 3)]
        [TestCase(GameSpeed.VeryFast, 5)]
        [TestCase(GameSpeed.Maximum, 10)]
        public void SupportedSpeed_MultipliesCalendarAdvance(GameSpeed speed, int expectedHours)
        {
            var clock = CreateClock();
            clock.SetSpeed(speed);

            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.That(clock.CalendarElapsed, Is.EqualTo(TimeSpan.FromHours(expectedHours)));
        }

        [Test]
        public void Pause_PreventsBothClocksFromAdvancing()
        {
            var clock = CreateClock();
            clock.SetSpeed(GameSpeed.Maximum);
            clock.Pause();

            var advance = clock.Advance(TimeSpan.FromHours(10));

            Assert.That(advance.CalendarDelta, Is.EqualTo(TimeSpan.Zero));
            Assert.That(advance.BiologicalDelta, Is.EqualTo(TimeSpan.Zero));
            Assert.That(clock.State.CalendarTicks, Is.Zero);
            Assert.That(clock.State.BiologicalTicks, Is.Zero);
        }

        [Test]
        public void Resume_ContinuesFromPreservedStateAndSpeed()
        {
            var clock = CreateClock();
            clock.SetSpeed(GameSpeed.Faster);
            clock.Advance(TimeSpan.FromMinutes(1));
            clock.Pause();
            clock.Advance(TimeSpan.FromMinutes(20));

            clock.Resume();
            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.That(clock.Speed, Is.EqualTo(GameSpeed.Faster));
            Assert.That(clock.CalendarElapsed, Is.EqualTo(TimeSpan.FromHours(6)));
        }

        [Test]
        public void SwitchingSpeed_AffectsOnlySubsequentAdvance()
        {
            var clock = CreateClock();
            clock.Advance(TimeSpan.FromMinutes(1));
            clock.SetSpeed(GameSpeed.Maximum);

            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.That(clock.CalendarElapsed, Is.EqualTo(TimeSpan.FromHours(11)));
        }

        [Test]
        public void BiologicalClock_IsSeparateAndUsesConfiguredMultiplier()
        {
            var state = new GameTimeState();
            var clock = new GameClock(state, new GameTimeSettings(biologicalMultiplier: 400));

            var advance = clock.Advance(TimeSpan.FromSeconds(1));

            Assert.That(advance.CalendarDelta, Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(advance.BiologicalDelta, Is.EqualTo(TimeSpan.FromMinutes(400)));
            Assert.That(state.BiologicalTicks, Is.Not.EqualTo(state.CalendarTicks));
        }

        [Test]
        public void LargeAdvance_EqualsManySmallAdvances()
        {
            var oneStep = CreateClock();
            var manySteps = CreateClock();
            oneStep.SetSpeed(GameSpeed.VeryFast);
            manySteps.SetSpeed(GameSpeed.VeryFast);

            oneStep.Advance(TimeSpan.FromHours(8));
            for (var i = 0; i < 480; i++)
                manySteps.Advance(TimeSpan.FromMinutes(1));

            Assert.That(manySteps.State.CalendarTicks, Is.EqualTo(oneStep.State.CalendarTicks));
            Assert.That(manySteps.State.BiologicalTicks, Is.EqualTo(oneStep.State.BiologicalTicks));
        }

        [Test]
        public void RestoredState_ContinuesDeterministically()
        {
            var original = CreateClock();
            original.SetSpeed(GameSpeed.Fast);
            original.Advance(TimeSpan.FromMinutes(7));
            original.Pause();

            var restoredState = new GameTimeState(
                original.State.CalendarTicks,
                original.State.BiologicalTicks,
                original.State.SpeedMultiplier,
                original.State.IsPaused);
            var restored = new GameClock(restoredState);
            restored.Resume();
            restored.Advance(TimeSpan.FromMinutes(2));

            original.Resume();
            original.Advance(TimeSpan.FromMinutes(2));
            Assert.That(restored.State.CalendarTicks, Is.EqualTo(original.State.CalendarTicks));
            Assert.That(restored.State.BiologicalTicks, Is.EqualTo(original.State.BiologicalTicks));
        }

        [Test]
        public void Calendar_ReportsDayAndYearRollover()
        {
            var lastMinuteOfYear = TimeSpan.FromDays(365).Ticks - TimeSpan.TicksPerMinute;
            Assert.That(GameCalendar.Year(lastMinuteOfYear), Is.EqualTo(1));
            Assert.That(GameCalendar.DayOfYear(lastMinuteOfYear), Is.EqualTo(365));
            Assert.That(GameCalendar.HourOfDay(lastMinuteOfYear), Is.EqualTo(23));
            Assert.That(GameCalendar.MinuteOfHour(lastMinuteOfYear), Is.EqualTo(59));

            Assert.That(GameCalendar.Year(TimeSpan.FromDays(365).Ticks), Is.EqualTo(2));
            Assert.That(GameCalendar.DayOfYear(TimeSpan.FromDays(365).Ticks), Is.EqualTo(1));
        }

        [Test]
        public void InvalidSpeedAndNegativeDelta_AreRejectedWithoutMutation()
        {
            var clock = CreateClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed((GameSpeed)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
            Assert.That(clock.State.CalendarTicks, Is.Zero);
            Assert.That(clock.State.BiologicalTicks, Is.Zero);
        }

        [Test]
        public void Overflow_IsRejectedWithoutPartialMutation()
        {
            var state = new GameTimeState(long.MaxValue, long.MaxValue, 10, false);
            var clock = new GameClock(state);

            Assert.Throws<OverflowException>(() => clock.Advance(TimeSpan.FromTicks(1)));
            Assert.That(state.CalendarTicks, Is.EqualTo(long.MaxValue));
            Assert.That(state.BiologicalTicks, Is.EqualTo(long.MaxValue));
        }

        private static GameClock CreateClock()
        {
            return new GameClock(new GameTimeState());
        }
    }
}
