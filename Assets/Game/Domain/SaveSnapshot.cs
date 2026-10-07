using System;
using Game.Domain.Time;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Dynasties;
using Game.Domain.Health;

namespace Game.Domain
{
    /// <summary>Immutable data-only checkpoint. Future sections require explicit schema evolution.</summary>
    public sealed class SaveSnapshot
    {
        public const int LegacyVersion = 1;
        public const int ProductionVersion = 2;
        public const int ResearchVersion = 3;
        public const int DynastyVersion = 4;
        public const int CurrentVersion = 5;
        public int FormatVersion { get; }
        public StableEntityId SessionId { get; }
        public long CalendarTicks { get; }
        public long BiologicalTicks { get; }
        public int SpeedMultiplier { get; }
        public bool IsPaused { get; }
        public int BiologicalMultiplier { get; }
        public ProductionSaveState Production { get; }
        public ResearchSaveState Research { get; }
        public DynastyWorldSaveState DynastyWorld { get; }
        public HealthFamilySaveState HealthFamily { get; }

        public SaveSnapshot(int formatVersion, StableEntityId sessionId, GameTimeState time, int biologicalMultiplier,
            ProductionSaveState production = null, ResearchSaveState research = null,
            DynastyWorldSaveState dynastyWorld = null, HealthFamilySaveState healthFamily = null)
        {
            if (formatVersion != LegacyVersion && formatVersion != ProductionVersion &&
                formatVersion != ResearchVersion && formatVersion != DynastyVersion && formatVersion != CurrentVersion)
                throw new NotSupportedException("Unsupported save format version: " + formatVersion);
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
            Production = production;
            Research = research;
            DynastyWorld = dynastyWorld;
            HealthFamily = healthFamily;
        }
        public GameTimeState CreateTimeState() => new GameTimeState(CalendarTicks, BiologicalTicks, SpeedMultiplier, IsPaused);
    }
}
