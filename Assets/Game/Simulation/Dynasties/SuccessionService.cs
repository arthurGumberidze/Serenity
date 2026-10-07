using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Dynasties;

namespace Game.Simulation.Dynasties
{
    public enum SuccessionOutcome
    {
        NoChange = 0,
        NewMonarch = 1,
        DynastyExtinct = 2
    }

    public readonly struct SuccessionResult
    {
        public bool Applied { get; }
        public SuccessionOutcome Outcome { get; }
        public StableEntityId PreviousMonarchId { get; }
        public StableEntityId? NewMonarchId { get; }
        public SuccessionHistoryEntry? HistoryEntry { get; }

        public SuccessionResult(bool applied, SuccessionOutcome outcome, StableEntityId previousMonarchId,
            StableEntityId? newMonarchId, SuccessionHistoryEntry? historyEntry)
        {
            Applied = applied;
            Outcome = outcome;
            PreviousMonarchId = previousMonarchId;
            NewMonarchId = newMonarchId;
            HistoryEntry = historyEntry;
        }
    }

    /// <summary>Event-driven authoritative death-to-succession boundary.</summary>
    public sealed class SuccessionService
    {
        private readonly CharacterRegistry characters;
        private readonly FamilyTreeQuery family;

        public DynastyState Dynasty { get; }
        public PlayerRealmState PlayerRealm { get; }
        public GameRunState GameRun { get; }
        public event Action<SuccessionResult> SuccessionResolved;

        public SuccessionService(CharacterRegistry characterRegistry, DynastyState dynasty,
            PlayerRealmState playerRealm, GameRunState gameRun)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            PlayerRealm = playerRealm ?? throw new ArgumentNullException(nameof(playerRealm));
            GameRun = gameRun ?? throw new ArgumentNullException(nameof(gameRun));
            family = new FamilyTreeQuery(characters);
            ValidateCoherence();
        }

        public FamilyTreeQuery Family => family;

        public SuccessionResult ProcessDeath(StableEntityId characterId, long biologicalDeathTick, long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var character = characters.Get(characterId);
            if (character.LifeState == CharacterLifeState.Alive)
                character.MarkDead(biologicalDeathTick);

            if (Dynasty.Status != DynastyStatus.Active || Dynasty.CurrentMonarchId != characterId)
                return new SuccessionResult(false, SuccessionOutcome.NoChange, characterId, null, null);
            if (Dynasty.WasMonarchDeathProcessed(characterId))
            {
                var existing = Dynasty.History.Last(x => x.PreviousMonarchId == characterId);
                return new SuccessionResult(false, existing.NewMonarchId.HasValue
                        ? SuccessionOutcome.NewMonarch : SuccessionOutcome.DynastyExtinct,
                    characterId, existing.NewMonarchId, existing);
            }

            var next = family.GetOrderedSuccessionCandidates(Dynasty, characterId).FirstOrDefault();
            var nextId = next?.Id;
            var entry = Dynasty.ApplySuccession(characterId, nextId, calendarTick);
            SuccessionResult result;
            if (nextId.HasValue)
            {
                PlayerRealm.TransferTo(nextId.Value);
                result = new SuccessionResult(true, SuccessionOutcome.NewMonarch, characterId, nextId, entry);
            }
            else
            {
                PlayerRealm.ClearForExtinction();
                GameRun.End(GameOverReason.DynastyExtinction);
                result = new SuccessionResult(true, SuccessionOutcome.DynastyExtinct, characterId, null, entry);
            }
            SuccessionResolved?.Invoke(result);
            return result;
        }

        public DynastyWorldSaveState CaptureState() => new DynastyWorldSaveState(characters.CaptureStates(),
            Dynasty.CaptureState(), PlayerRealm.CurrentRulerId, GameRun.Status, GameRun.GameOverReason);

        public void RestoreState(DynastyWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var validationRegistry = new CharacterRegistry();
            validationRegistry.RestoreStates(state.Characters);
            var validationDynasty = DynastyState.Restore(state.Dynasty);
            var validationFamily = new FamilyTreeQuery(validationRegistry);
            validationFamily.ValidateDynasty(validationDynasty);
            if (validationDynasty.Status == DynastyStatus.Active)
            {
                var monarch = validationRegistry.Get(validationDynasty.CurrentMonarchId.Value);
                if (monarch.LifeState != CharacterLifeState.Alive)
                    throw new InvalidOperationException("An active restored dynasty cannot have a dead current monarch.");
            }

            characters.RestoreStates(state.Characters);
            Dynasty.RestoreFrom(state.Dynasty);
            PlayerRealm.Restore(state.CurrentRulerId);
            GameRun.Restore(state.RunStatus, state.GameOverReason);
            ValidateCoherence();
        }

        private void ValidateCoherence()
        {
            family.ValidateDynasty(Dynasty);
            if (Dynasty.Status == DynastyStatus.Active)
            {
                if (PlayerRealm.CurrentRulerId != Dynasty.CurrentMonarchId)
                    throw new InvalidOperationException("Player ruler must match the active monarch.");
                if (GameRun.Status != GameRunStatus.Running)
                    throw new InvalidOperationException("An active dynasty requires a running game.");
                var monarch = characters.Get(Dynasty.CurrentMonarchId.Value);
                if (monarch.LifeState != CharacterLifeState.Alive)
                    throw new InvalidOperationException("Active current monarch must be alive.");
            }
            else if (PlayerRealm.CurrentRulerId.HasValue || GameRun.Status != GameRunStatus.GameOver ||
                     GameRun.GameOverReason != GameOverReason.DynastyExtinction)
                throw new InvalidOperationException("Dynasty extinction and game-over state are inconsistent.");
        }
    }
}
