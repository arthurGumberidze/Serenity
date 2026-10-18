using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.World;

namespace Game.Simulation
{
    /// <summary>Synchronous checkpoint port. Writes replace a slot atomically or throw; reads never mutate runtime.</summary>
    public interface ISaveStore
    {
        void Save(StableEntityId slotId, SaveSnapshot snapshot);
        SaveSnapshot Load(StableEntityId slotId);
    }

    public sealed class SaveSlotMetadata
    {
        public SaveSlotMetadata(StableEntityId slotId, StableEntityId? sessionId, long? calendarTicks,
            GameWorldMode? mode, DateTime lastWriteUtc, bool isCorrupt, string error, long? worldSeed = null,
            int? formatVersion = null, string worldGenerationId = null)
        {
            if (!slotId.IsValid) throw new ArgumentException("Slot ID is required.", nameof(slotId));
            SlotId = slotId;
            SessionId = sessionId;
            CalendarTicks = calendarTicks;
            Mode = mode;
            LastWriteUtc = DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc);
            IsCorrupt = isCorrupt;
            Error = error ?? string.Empty;
            WorldSeed = worldSeed;
            FormatVersion = formatVersion;
            WorldGenerationId = worldGenerationId ?? string.Empty;
        }

        public StableEntityId SlotId { get; }
        public StableEntityId? SessionId { get; }
        public long? CalendarTicks { get; }
        public GameWorldMode? Mode { get; }
        public DateTime LastWriteUtc { get; }
        public bool IsCorrupt { get; }
        public string Error { get; }
        public long? WorldSeed { get; }
        public int? FormatVersion { get; }
        public string WorldGenerationId { get; }
    }

    /// <summary>Optional storage capability used by player-facing slot browsers.</summary>
    public interface ISaveSlotCatalog
    {
        IReadOnlyList<SaveSlotMetadata> ListSlots();
    }
}
