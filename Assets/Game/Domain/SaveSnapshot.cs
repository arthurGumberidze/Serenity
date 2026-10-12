using System;
using Game.Domain.Time;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Weather;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Control;
using Game.Domain.Buildings;
using Game.Domain.World;
using Game.Domain.AI;
using Game.Domain.Events;
using Game.Domain.Campaign;

namespace Game.Domain
{
    /// <summary>Immutable data-only checkpoint. Future sections require explicit schema evolution.</summary>
    public sealed class SaveSnapshot
    {
        public const int LegacyVersion = 1;
        public const int ProductionVersion = 2;
        public const int ResearchVersion = 3;
        public const int DynastyVersion = 4;
        public const int HealthVersion = 5;
        public const int WeatherVersion = 6;
        public const int CombatVersion = 7;
        public const int MilitaryVersion = 8;
        public const int PlayerControlVersion = 9;
        public const int BattleVersion = 10;
        public const int BuildingVersion = 11;
        public const int GlobalMapVersion = 12;
        public const int StrategicAiVersion = 13;
        public const int EventVersion = 14;
        public const int CampaignVersion = 15;
        public const int CurrentVersion = CampaignVersion;
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
        public WeatherSaveState Weather { get; }
        public CombatSaveState Combat { get; }
        public MilitaryUnitSaveState Military { get; }
        public PlayerControlSaveState PlayerControl { get; }
        public BuildingWorldSaveState BuildingWorld { get; }
        public GlobalMapSaveState GlobalMap { get; }
        public AiStrategicSaveState StrategicAi { get; }
        public EventSaveState Events { get; }
        public CampaignProgressSaveState Campaign { get; }

        public SaveSnapshot(int formatVersion, StableEntityId sessionId, GameTimeState time, int biologicalMultiplier,
            ProductionSaveState production = null, ResearchSaveState research = null,
            DynastyWorldSaveState dynastyWorld = null, HealthFamilySaveState healthFamily = null,
            WeatherSaveState weather = null, CombatSaveState combat = null,
            MilitaryUnitSaveState military = null, PlayerControlSaveState playerControl = null,
            BuildingWorldSaveState buildingWorld = null, GlobalMapSaveState globalMap = null,
            AiStrategicSaveState strategicAi = null, EventSaveState events = null,
            CampaignProgressSaveState campaign = null)
        {
            if (formatVersion != LegacyVersion && formatVersion != ProductionVersion &&
                formatVersion != ResearchVersion && formatVersion != DynastyVersion && formatVersion != HealthVersion &&
                formatVersion != WeatherVersion && formatVersion != CombatVersion &&
                formatVersion != MilitaryVersion && formatVersion != PlayerControlVersion &&
                formatVersion != BattleVersion && formatVersion != BuildingVersion &&
                formatVersion != GlobalMapVersion && formatVersion != StrategicAiVersion &&
                formatVersion != EventVersion && formatVersion != CurrentVersion)
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
            Weather = weather;
            Combat = combat;
            Military = military;
            PlayerControl = playerControl;
            BuildingWorld = buildingWorld;
            GlobalMap = globalMap;
            StrategicAi = strategicAi;
            Events = events;
            Campaign = campaign;
        }
        public GameTimeState CreateTimeState() => new GameTimeState(CalendarTicks, BiologicalTicks, SpeedMultiplier, IsPaused);
    }
}
