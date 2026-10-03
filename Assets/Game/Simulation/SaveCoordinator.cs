using System;
using Game.Domain;
using Game.Simulation.Time;

namespace Game.Simulation
{
    /// <summary>Call on the simulation owner thread at a committed tick barrier. No concurrent ticks or jobs.</summary>
    public sealed class SaveCoordinator
    {
        private readonly ISaveStore store;
        public StableEntityId SessionId { get; private set; }
        public GameClock Clock { get; private set; }

        public SaveCoordinator(StableEntityId sessionId, GameClock clock, ISaveStore store)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Invalid session ID.", nameof(sessionId));
            SessionId = sessionId;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }
        public SaveSnapshot Capture()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, SessionId, Clock.State, Clock.BiologicalMultiplier);
            CreateClock(snapshot); // Validate all mutable U03 state before exposing a checkpoint.
            return snapshot;
        }
        public void Save(StableEntityId slotId) => store.Save(slotId, Capture());
        public void Load(StableEntityId slotId)
        {
            var snapshot = store.Load(slotId);
            var restoredClock = CreateClock(snapshot);
            // Commit only after all reads, parsing and validation succeed. Consumers rebind after Load.
            Clock = restoredClock;
            SessionId = snapshot.SessionId;
        }
        public static GameClock CreateClock(SaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new GameClock(snapshot.CreateTimeState(), new GameTimeSettings(snapshot.BiologicalMultiplier));
        }
    }
}
