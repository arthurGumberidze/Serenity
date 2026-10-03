using System;
using Game.Domain.Time;

namespace Game.Domain
{
    /// <summary>Immutable data-only checkpoint. Future sections require explicit schema evolution.</summary>
    public sealed class SaveSnapshot
    {
        public const int CurrentVersion = 1;
        public int FormatVersion { get; }
        public StableEntityId SessionId { get; }
        public long CalendarTicks { get; }
        public long BiologicalTicks { get; }
        public int SpeedMultiplier { get; }
        public bool IsPaused { get; }
        public int BiologicalMultiplier { get; }

        public SaveSnapshot(int formatVersion, StableEntityId sessionId, GameTimeState time, int biologicalMultiplier)
        {
            if (formatVersion != CurrentVersion) throw new NotSupportedException("Unsupported save format version: " + formatVersion);
            if (!sessionId.IsValid) throw new ArgumentException("Session ID must be valid.", nameof(sessionId));
            if (time == null) throw new ArgumentNullException(nameof(time));
            if (time.CalendarTicks < 0 || time.BiologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (biologicalMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(biologicalMultiplier));
            FormatVersion = formatVersion;
            SessionId = sessionId;
            CalendarTicks = time.CalendarTicks;
            BiologicalTicks = time.BiologicalTicks;
            SpeedMultiplier = time.SpeedMultiplier;
            IsPaused = time.IsPaused;
            BiologicalMultiplier = biologicalMultiplier;
        }
        public GameTimeState CreateTimeState() => new GameTimeState(CalendarTicks, BiologicalTicks, SpeedMultiplier, IsPaused);
    }
}
