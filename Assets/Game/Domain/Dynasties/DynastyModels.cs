using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.Characters;

namespace Game.Domain.Dynasties
{
    public enum DynastyStatus
    {
        Active = 1,
        Extinct = 2
    }

    public enum SuccessionReason
    {
        MonarchDeath = 1,
        DynastyExtinction = 2
    }

    public enum GameRunStatus
    {
        Running = 1,
        GameOver = 2
    }

    public enum GameOverReason
    {
        None = 0,
        DynastyExtinction = 1
    }

    public static class SuccessionRuleIds
    {
        public const string AgnaticPrimogenitureV1 = "agnatic_primogeniture_v1";

        public static bool IsSupported(string value) =>
            string.Equals(value, AgnaticPrimogenitureV1, StringComparison.Ordinal);
    }

    public readonly struct SuccessionHistoryEntry : IEquatable<SuccessionHistoryEntry>
    {
        public long SequenceNumber { get; }
        public StableEntityId PreviousMonarchId { get; }
        public StableEntityId? NewMonarchId { get; }
        public long CalendarTick { get; }
        public SuccessionReason Reason { get; }

        public SuccessionHistoryEntry(long sequenceNumber, StableEntityId previousMonarchId,
            StableEntityId? newMonarchId, long calendarTick, SuccessionReason reason)
        {
            if (sequenceNumber <= 0) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
            if (!previousMonarchId.IsValid) throw new ArgumentException("Previous monarch ID must be valid.", nameof(previousMonarchId));
            if (newMonarchId.HasValue && !newMonarchId.Value.IsValid)
                throw new ArgumentException("New monarch ID must be valid when present.", nameof(newMonarchId));
            if (newMonarchId == previousMonarchId)
                throw new ArgumentException("Succession cannot retain the deceased monarch.", nameof(newMonarchId));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!Enum.IsDefined(typeof(SuccessionReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
            if ((reason == SuccessionReason.MonarchDeath) != newMonarchId.HasValue)
                throw new ArgumentException("Succession reason and new monarch presence do not agree.", nameof(reason));
            SequenceNumber = sequenceNumber;
            PreviousMonarchId = previousMonarchId;
            NewMonarchId = newMonarchId;
            CalendarTick = calendarTick;
            Reason = reason;
        }

        public bool Equals(SuccessionHistoryEntry other) => SequenceNumber == other.SequenceNumber &&
            PreviousMonarchId == other.PreviousMonarchId && NewMonarchId == other.NewMonarchId &&
            CalendarTick == other.CalendarTick && Reason == other.Reason;
        public override bool Equals(object obj) => obj is SuccessionHistoryEntry other && Equals(other);
        public override int GetHashCode() => SequenceNumber.GetHashCode();
    }

    public sealed class DynastyStateSnapshot
    {
        public StableEntityId Id { get; }
        public string Name { get; }
        public StableEntityId FounderId { get; }
        public StableEntityId? CurrentMonarchId { get; }
        public string SuccessionRuleId { get; }
        public DynastyStatus Status { get; }
        public IReadOnlyList<StableEntityId> MemberIds { get; }
        public IReadOnlyList<StableEntityId> ProcessedMonarchDeathIds { get; }
        public IReadOnlyList<SuccessionHistoryEntry> History { get; }

        public DynastyStateSnapshot(StableEntityId id, string name, StableEntityId founderId,
            StableEntityId? currentMonarchId, string successionRuleId, DynastyStatus status,
            IEnumerable<StableEntityId> memberIds, IEnumerable<StableEntityId> processedMonarchDeathIds,
            IEnumerable<SuccessionHistoryEntry> history)
        {
            Id = id;
            Name = name;
            FounderId = founderId;
            CurrentMonarchId = currentMonarchId;
            SuccessionRuleId = successionRuleId;
            Status = status;
            MemberIds = Snapshot(memberIds);
            ProcessedMonarchDeathIds = Snapshot(processedMonarchDeathIds);
            History = Snapshot(history);
        }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> values) =>
            new ReadOnlyCollection<T>((values ?? Array.Empty<T>()).ToArray());
    }

    /// <summary>Canonical dynasty state. Character biography and family links remain on Character.</summary>
    public sealed class DynastyState
    {
        private readonly HashSet<StableEntityId> members = new HashSet<StableEntityId>();
        private readonly HashSet<StableEntityId> processedDeaths = new HashSet<StableEntityId>();
        private readonly List<SuccessionHistoryEntry> history = new List<SuccessionHistoryEntry>();

        public StableEntityId Id { get; private set; }
        public string Name { get; private set; }
        public StableEntityId FounderId { get; private set; }
        public StableEntityId? CurrentMonarchId { get; private set; }
        public string SuccessionRuleId { get; private set; }
        public DynastyStatus Status { get; private set; }
        public IReadOnlyList<StableEntityId> MemberIds => members.OrderBy(IdText, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<StableEntityId> ProcessedMonarchDeathIds =>
            processedDeaths.OrderBy(IdText, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<SuccessionHistoryEntry> History => history.AsReadOnly();

        private DynastyState(DynastyStateSnapshot snapshot) => RestoreFrom(snapshot);

        public static DynastyState Create(StableEntityId id, string name, StableEntityId founderId,
            StableEntityId currentMonarchId, IEnumerable<StableEntityId> memberIds,
            string successionRuleId = SuccessionRuleIds.AgnaticPrimogenitureV1) =>
            new DynastyState(new DynastyStateSnapshot(id, name, founderId, currentMonarchId,
                successionRuleId, DynastyStatus.Active, memberIds, null, null));

        public static DynastyState Restore(DynastyStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new DynastyState(snapshot);
        }

        public bool Contains(StableEntityId characterId) => members.Contains(characterId);
        public bool WasMonarchDeathProcessed(StableEntityId characterId) => processedDeaths.Contains(characterId);

        public bool AddMember(StableEntityId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Dynasty member ID must be valid.", nameof(characterId));
            if (Status != DynastyStatus.Active) throw new InvalidOperationException("Cannot add a member to an extinct dynasty.");
            return members.Add(characterId);
        }

        public SuccessionHistoryEntry ApplySuccession(StableEntityId deceasedMonarchId,
            StableEntityId? newMonarchId, long calendarTick)
        {
            if (Status != DynastyStatus.Active || CurrentMonarchId != deceasedMonarchId)
                throw new InvalidOperationException("Only the active current monarch can trigger succession.");
            if (!members.Contains(deceasedMonarchId)) throw new InvalidOperationException("Current monarch is not a dynasty member.");
            if (processedDeaths.Contains(deceasedMonarchId))
                throw new InvalidOperationException("This monarch death was already processed.");
            if (newMonarchId.HasValue && (!members.Contains(newMonarchId.Value) || newMonarchId == deceasedMonarchId))
                throw new InvalidOperationException("The new monarch must be a different dynasty member.");

            var reason = newMonarchId.HasValue ? SuccessionReason.MonarchDeath : SuccessionReason.DynastyExtinction;
            var entry = new SuccessionHistoryEntry(history.Count + 1L, deceasedMonarchId, newMonarchId,
                calendarTick, reason);
            processedDeaths.Add(deceasedMonarchId);
            history.Add(entry);
            CurrentMonarchId = newMonarchId;
            Status = newMonarchId.HasValue ? DynastyStatus.Active : DynastyStatus.Extinct;
            return entry;
        }

        public DynastyStateSnapshot CaptureState() => new DynastyStateSnapshot(Id, Name, FounderId,
            CurrentMonarchId, SuccessionRuleId, Status, MemberIds, ProcessedMonarchDeathIds, history);

        public void RestoreFrom(DynastyStateSnapshot snapshot)
        {
            Validate(snapshot);
            Id = snapshot.Id;
            Name = snapshot.Name;
            FounderId = snapshot.FounderId;
            CurrentMonarchId = snapshot.CurrentMonarchId;
            SuccessionRuleId = snapshot.SuccessionRuleId;
            Status = snapshot.Status;
            members.Clear();
            foreach (var id in snapshot.MemberIds) members.Add(id);
            processedDeaths.Clear();
            foreach (var id in snapshot.ProcessedMonarchDeathIds) processedDeaths.Add(id);
            history.Clear();
            history.AddRange(snapshot.History);
        }

        private static void Validate(DynastyStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.Id.IsValid) throw new ArgumentException("Dynasty ID must be valid.", nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Name.Length > 64 ||
                !string.Equals(snapshot.Name, snapshot.Name.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Dynasty name must be canonical printable text.", nameof(snapshot));
            if (!snapshot.FounderId.IsValid) throw new ArgumentException("Founder ID must be valid.", nameof(snapshot));
            if (!SuccessionRuleIds.IsSupported(snapshot.SuccessionRuleId))
                throw new NotSupportedException("Unsupported succession rule: " + snapshot.SuccessionRuleId);
            if (!Enum.IsDefined(typeof(DynastyStatus), snapshot.Status)) throw new ArgumentOutOfRangeException(nameof(snapshot));

            var memberSet = new HashSet<StableEntityId>();
            foreach (var id in snapshot.MemberIds)
                if (!id.IsValid || !memberSet.Add(id))
                    throw new ArgumentException("Dynasty member IDs must be valid and unique.", nameof(snapshot));
            if (!memberSet.Contains(snapshot.FounderId)) throw new ArgumentException("Founder must be a dynasty member.", nameof(snapshot));
            if (snapshot.CurrentMonarchId.HasValue && !memberSet.Contains(snapshot.CurrentMonarchId.Value))
                throw new ArgumentException("Current monarch must be a dynasty member.", nameof(snapshot));
            if ((snapshot.Status == DynastyStatus.Active) != snapshot.CurrentMonarchId.HasValue)
                throw new ArgumentException("Active dynasty status requires exactly one current monarch.", nameof(snapshot));

            var processedSet = new HashSet<StableEntityId>();
            foreach (var id in snapshot.ProcessedMonarchDeathIds)
                if (!id.IsValid || !memberSet.Contains(id) || !processedSet.Add(id))
                    throw new ArgumentException("Processed death markers must be unique dynasty members.", nameof(snapshot));
            for (var i = 0; i < snapshot.History.Count; i++)
            {
                var entry = snapshot.History[i];
                if (entry.SequenceNumber != i + 1L || !memberSet.Contains(entry.PreviousMonarchId) ||
                    (entry.NewMonarchId.HasValue && !memberSet.Contains(entry.NewMonarchId.Value)))
                    throw new ArgumentException("Succession history is invalid.", nameof(snapshot));
            }
            if (!processedSet.SetEquals(snapshot.History.Select(x => x.PreviousMonarchId)))
                throw new ArgumentException("Processed death markers must match succession history.", nameof(snapshot));
            if (snapshot.Status == DynastyStatus.Extinct &&
                (snapshot.History.Count == 0 || snapshot.History[snapshot.History.Count - 1].Reason != SuccessionReason.DynastyExtinction))
                throw new ArgumentException("An extinct dynasty requires an extinction history entry.", nameof(snapshot));
        }

        private static string IdText(StableEntityId id) => id.ToString();
    }

    public sealed class PlayerRealmState
    {
        public StableEntityId? CurrentRulerId { get; private set; }
        public PlayerRealmState(StableEntityId? currentRulerId) => Restore(currentRulerId);
        public void TransferTo(StableEntityId newRulerId)
        {
            if (!newRulerId.IsValid) throw new ArgumentException("Ruler ID must be valid.", nameof(newRulerId));
            CurrentRulerId = newRulerId;
        }
        public void ClearForExtinction() => CurrentRulerId = null;
        public void Restore(StableEntityId? rulerId)
        {
            if (rulerId.HasValue && !rulerId.Value.IsValid) throw new ArgumentException("Ruler ID must be valid.", nameof(rulerId));
            CurrentRulerId = rulerId;
        }
    }

    public sealed class GameRunState
    {
        public GameRunStatus Status { get; private set; } = GameRunStatus.Running;
        public GameOverReason GameOverReason { get; private set; } = GameOverReason.None;

        public void End(GameOverReason reason)
        {
            if (reason == GameOverReason.None || !Enum.IsDefined(typeof(GameOverReason), reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (Status == GameRunStatus.GameOver)
            {
                if (GameOverReason != reason) throw new InvalidOperationException("Game is already over for another reason.");
                return;
            }
            Status = GameRunStatus.GameOver;
            GameOverReason = reason;
        }

        public void Restore(GameRunStatus status, GameOverReason reason)
        {
            if (!Enum.IsDefined(typeof(GameRunStatus), status) || !Enum.IsDefined(typeof(GameOverReason), reason))
                throw new ArgumentOutOfRangeException();
            if ((status == GameRunStatus.Running) != (reason == GameOverReason.None))
                throw new ArgumentException("Running state and game-over reason do not agree.");
            Status = status;
            GameOverReason = reason;
        }
    }

    public sealed class DynastyWorldSaveState
    {
        public IReadOnlyList<CharacterState> Characters { get; }
        public DynastyStateSnapshot Dynasty { get; }
        public StableEntityId? CurrentRulerId { get; }
        public GameRunStatus RunStatus { get; }
        public GameOverReason GameOverReason { get; }

        public DynastyWorldSaveState(IEnumerable<CharacterState> characters, DynastyStateSnapshot dynasty,
            StableEntityId? currentRulerId, GameRunStatus runStatus, GameOverReason gameOverReason)
        {
            Characters = new ReadOnlyCollection<CharacterState>((characters ?? Array.Empty<CharacterState>()).ToArray());
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            CurrentRulerId = currentRulerId;
            RunStatus = runStatus;
            GameOverReason = gameOverReason;
            var ids = new HashSet<StableEntityId>();
            var byId = new Dictionary<StableEntityId, CharacterState>();
            foreach (var state in Characters)
            {
                if (state == null) throw new ArgumentException("Character states cannot contain null.", nameof(characters));
                Character.Restore(state);
                if (!ids.Add(state.Id)) throw new ArgumentException("Character state IDs must be unique.", nameof(characters));
                byId.Add(state.Id, state);
            }
            DynastyState.Restore(dynasty);
            foreach (var memberId in dynasty.MemberIds)
            {
                if (!byId.TryGetValue(memberId, out var member))
                    throw new ArgumentException("Dynasty references a missing saved character.", nameof(dynasty));
                if (member.DynastyId != dynasty.Id)
                    throw new ArgumentException("Saved dynasty member has a different dynasty reference.", nameof(dynasty));
            }
            foreach (var state in Characters)
            {
                foreach (var parent in state.Parents)
                    if (!byId.ContainsKey(parent.ParentId))
                        throw new ArgumentException("Saved character references a missing parent.", nameof(characters));
                if (state.SpouseId.HasValue)
                {
                    if (!byId.TryGetValue(state.SpouseId.Value, out var spouse))
                        throw new ArgumentException("Saved character references a missing spouse.", nameof(characters));
                    if (spouse.SpouseId != state.Id)
                        throw new ArgumentException("Saved spouse references must be symmetric.", nameof(characters));
                }
            }
            var colors = new Dictionary<StableEntityId, int>();
            foreach (var state in Characters) ValidateParentCycles(state, byId, colors);
            if (currentRulerId.HasValue && !ids.Contains(currentRulerId.Value))
                throw new ArgumentException("Player ruler must reference a saved character.", nameof(currentRulerId));
            var run = new GameRunState();
            run.Restore(runStatus, gameOverReason);
            if (dynasty.Status == DynastyStatus.Active && currentRulerId != dynasty.CurrentMonarchId)
                throw new ArgumentException("Player ruler must match the active monarch.", nameof(currentRulerId));
            if (dynasty.Status == DynastyStatus.Active &&
                (runStatus != GameRunStatus.Running || gameOverReason != GameOverReason.None))
                throw new ArgumentException("An active dynasty requires a running game.");
            if (dynasty.Status == DynastyStatus.Extinct &&
                (currentRulerId.HasValue || runStatus != GameRunStatus.GameOver || gameOverReason != GameOverReason.DynastyExtinction))
                throw new ArgumentException("Extinct dynasty must produce dynasty-extinction Game Over.");
        }

        private static void ValidateParentCycles(CharacterState state,
            IReadOnlyDictionary<StableEntityId, CharacterState> byId, IDictionary<StableEntityId, int> colors)
        {
            if (colors.TryGetValue(state.Id, out var color))
            {
                if (color == 1) throw new ArgumentException("Cyclic saved parent references are not allowed.");
                if (color == 2) return;
            }
            colors[state.Id] = 1;
            foreach (var parent in state.Parents) ValidateParentCycles(byId[parent.ParentId], byId, colors);
            colors[state.Id] = 2;
        }
    }
}
