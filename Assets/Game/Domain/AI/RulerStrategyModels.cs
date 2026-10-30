using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.AI
{
    public enum RulerStrategyProviderKind
    {
        Deterministic = 0,
        Ollama = 1,
        CloudApi = 2
    }

    public enum RulerStrategyTrigger
    {
        Annual = 1,
        Trade = 2,
        War = 3,
        Crisis = 4,
        Dialogue = 5,
        Manual = 6
    }

    public enum RulerDiplomaticPosture
    {
        Isolationist = 1,
        Neutral = 2,
        Cooperative = 3,
        Expansionist = 4
    }

    public enum RulerStrategyRequestStatus
    {
        Accepted = 1,
        Rejected = 2,
        Unavailable = 3,
        TimedOut = 4
    }

    /// <summary>
    /// Aggregate intent only. Canonical gameplay services remain the sole authority for every action.
    /// </summary>
    public sealed class RulerStrategyPlan
    {
        public RulerStrategyPlan(long requestSequence, long generatedTick, long validUntilTick,
            RulerStrategyProviderKind provider, AiStrategicGoal preferredGoal, int foodTarget,
            int materialTarget, int minimumArmySize, int aggression,
            RulerDiplomaticPosture diplomaticPosture, int foodWorkforcePercent,
            int materialsWorkforcePercent, int constructionWorkforcePercent,
            int researchWorkforcePercent, int militaryWorkforcePercent,
            IEnumerable<string> buildingPriorities, string summary)
        {
            if (requestSequence <= 0 || generatedTick < 0 || validUntilTick <= generatedTick)
                throw new ArgumentOutOfRangeException();
            if (!Enum.IsDefined(typeof(RulerStrategyProviderKind), provider) ||
                provider == RulerStrategyProviderKind.Deterministic ||
                !Enum.IsDefined(typeof(AiStrategicGoal), preferredGoal) ||
                preferredGoal == AiStrategicGoal.Defeated ||
                !Enum.IsDefined(typeof(RulerDiplomaticPosture), diplomaticPosture))
                throw new ArgumentException("Invalid ruler strategy enum value.");
            if (foodTarget < 0 || foodTarget > 10000 || materialTarget < 0 || materialTarget > 10000 ||
                minimumArmySize < 1 || minimumArmySize > 2000 || aggression < 0 || aggression > 100)
                throw new ArgumentOutOfRangeException("Ruler strategy targets are outside safe bounds.");

            var shares = new[] { foodWorkforcePercent, materialsWorkforcePercent,
                constructionWorkforcePercent, researchWorkforcePercent, militaryWorkforcePercent };
            if (shares.Any(value => value < 0 || value > 100) || shares.Sum() != 100)
                throw new ArgumentException("Ruler strategy workforce shares must total 100 percent.");
            var priorities = (buildingPriorities ?? Array.Empty<string>()).ToArray();
            if (priorities.Length > 8 || priorities.Any(value => string.IsNullOrWhiteSpace(value) ||
                    value != value.Trim() || value.Length > 64) ||
                priorities.Distinct(StringComparer.Ordinal).Count() != priorities.Length)
                throw new ArgumentException("Ruler strategy building priorities are invalid.", nameof(buildingPriorities));
            if (summary == null || summary.Length > 512)
                throw new ArgumentOutOfRangeException(nameof(summary));

            RequestSequence = requestSequence;
            GeneratedTick = generatedTick;
            ValidUntilTick = validUntilTick;
            Provider = provider;
            PreferredGoal = preferredGoal;
            FoodTarget = foodTarget;
            MaterialTarget = materialTarget;
            MinimumArmySize = minimumArmySize;
            Aggression = aggression;
            DiplomaticPosture = diplomaticPosture;
            FoodWorkforcePercent = foodWorkforcePercent;
            MaterialsWorkforcePercent = materialsWorkforcePercent;
            ConstructionWorkforcePercent = constructionWorkforcePercent;
            ResearchWorkforcePercent = researchWorkforcePercent;
            MilitaryWorkforcePercent = militaryWorkforcePercent;
            BuildingPriorities = Array.AsReadOnly(priorities);
            Summary = summary;
        }

        public long RequestSequence { get; }
        public long GeneratedTick { get; }
        public long ValidUntilTick { get; }
        public RulerStrategyProviderKind Provider { get; }
        public AiStrategicGoal PreferredGoal { get; }
        public int FoodTarget { get; }
        public int MaterialTarget { get; }
        public int MinimumArmySize { get; }
        public int Aggression { get; }
        public RulerDiplomaticPosture DiplomaticPosture { get; }
        public int FoodWorkforcePercent { get; }
        public int MaterialsWorkforcePercent { get; }
        public int ConstructionWorkforcePercent { get; }
        public int ResearchWorkforcePercent { get; }
        public int MilitaryWorkforcePercent { get; }
        public IReadOnlyList<string> BuildingPriorities { get; }
        public string Summary { get; }
        public bool IsActiveAt(long tick) => tick >= GeneratedTick && tick < ValidUntilTick;
    }

    public readonly struct RulerStrategyPendingRequest
    {
        public RulerStrategyPendingRequest(long sequence, long requestedTick, RulerStrategyTrigger trigger,
            string subject)
        {
            if (sequence <= 0 || requestedTick < 0 || !Enum.IsDefined(typeof(RulerStrategyTrigger), trigger) ||
                subject == null || subject.Length > 512)
                throw new ArgumentException("Invalid ruler strategy request.");
            Sequence = sequence;
            RequestedTick = requestedTick;
            Trigger = trigger;
            Subject = subject;
        }

        public long Sequence { get; }
        public long RequestedTick { get; }
        public RulerStrategyTrigger Trigger { get; }
        public string Subject { get; }
    }

    public readonly struct RulerStrategyJournalEntry
    {
        public RulerStrategyJournalEntry(long requestSequence, long requestedTick, long completedTick,
            RulerStrategyTrigger trigger, RulerStrategyProviderKind provider,
            RulerStrategyRequestStatus status, string note)
        {
            if (requestSequence <= 0 || requestedTick < 0 || completedTick < requestedTick ||
                !Enum.IsDefined(typeof(RulerStrategyTrigger), trigger) ||
                !Enum.IsDefined(typeof(RulerStrategyProviderKind), provider) ||
                !Enum.IsDefined(typeof(RulerStrategyRequestStatus), status) ||
                note == null || note.Length > 512)
                throw new ArgumentException("Invalid ruler strategy journal entry.");
            RequestSequence = requestSequence;
            RequestedTick = requestedTick;
            CompletedTick = completedTick;
            Trigger = trigger;
            Provider = provider;
            Status = status;
            Note = note;
        }

        public long RequestSequence { get; }
        public long RequestedTick { get; }
        public long CompletedTick { get; }
        public RulerStrategyTrigger Trigger { get; }
        public RulerStrategyProviderKind Provider { get; }
        public RulerStrategyRequestStatus Status { get; }
        public string Note { get; }
    }

    public sealed class RulerStrategyStateSnapshot
    {
        public const int SchemaVersion = 1;

        public RulerStrategyStateSnapshot(long requestSequence, long nextAnnualTick,
            RulerStrategyPendingRequest? pendingRequest, RulerStrategyPlan activePlan,
            IEnumerable<RulerStrategyJournalEntry> journal)
        {
            if (requestSequence < 0 || nextAnnualTick < 0 ||
                pendingRequest.HasValue && pendingRequest.Value.Sequence > requestSequence ||
                activePlan != null && activePlan.RequestSequence > requestSequence)
                throw new ArgumentException("Invalid ruler strategy state.");
            var entries = (journal ?? Array.Empty<RulerStrategyJournalEntry>())
                .OrderBy(value => value.RequestSequence).ToArray();
            if (entries.Length > 32 || entries.Any(value => value.RequestSequence > requestSequence) ||
                entries.Select(value => value.RequestSequence).Distinct().Count() != entries.Length)
                throw new ArgumentException("Invalid ruler strategy journal.");
            RequestSequence = requestSequence;
            NextAnnualTick = nextAnnualTick;
            PendingRequest = pendingRequest;
            ActivePlan = activePlan;
            Journal = new ReadOnlyCollection<RulerStrategyJournalEntry>(entries);
        }

        public long RequestSequence { get; }
        public long NextAnnualTick { get; }
        public RulerStrategyPendingRequest? PendingRequest { get; }
        public RulerStrategyPlan ActivePlan { get; }
        public IReadOnlyList<RulerStrategyJournalEntry> Journal { get; }
    }

    public sealed class RulerStrategyState
    {
        public static readonly long AnnualIntervalTicks = TimeSpan.FromDays(365).Ticks;
        private readonly List<RulerStrategyJournalEntry> journal = new List<RulerStrategyJournalEntry>();

        public RulerStrategyState(long initialTick)
        {
            if (initialTick < 0) throw new ArgumentOutOfRangeException(nameof(initialTick));
            NextAnnualTick = checked(initialTick + AnnualIntervalTicks);
        }

        public long RequestSequence { get; private set; }
        public long NextAnnualTick { get; private set; }
        public RulerStrategyPendingRequest? PendingRequest { get; private set; }
        public RulerStrategyPlan ActivePlan { get; private set; }
        public IReadOnlyList<RulerStrategyJournalEntry> Journal => journal.AsReadOnly();

        public bool TryBegin(long tick, RulerStrategyTrigger trigger, string subject,
            out RulerStrategyPendingRequest request)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (PendingRequest.HasValue)
            {
                request = default;
                return false;
            }
            request = new RulerStrategyPendingRequest(++RequestSequence, tick, trigger, subject ?? string.Empty);
            PendingRequest = request;
            return true;
        }

        public void Complete(long completedTick, RulerStrategyProviderKind provider,
            RulerStrategyRequestStatus status, string note, RulerStrategyPlan acceptedPlan = null)
        {
            if (!PendingRequest.HasValue) throw new InvalidOperationException("No ruler strategy request is pending.");
            var pending = PendingRequest.Value;
            if (acceptedPlan != null && (status != RulerStrategyRequestStatus.Accepted ||
                    acceptedPlan.RequestSequence != pending.Sequence))
                throw new ArgumentException("Accepted ruler strategy plan does not match the pending request.");
            if (status == RulerStrategyRequestStatus.Accepted && acceptedPlan == null)
                throw new ArgumentNullException(nameof(acceptedPlan));
            if (acceptedPlan != null) ActivePlan = acceptedPlan;
            journal.Add(new RulerStrategyJournalEntry(pending.Sequence, pending.RequestedTick, completedTick,
                pending.Trigger, provider, status, note ?? string.Empty));
            while (journal.Count > 32) journal.RemoveAt(0);
            if (pending.Trigger == RulerStrategyTrigger.Annual)
                NextAnnualTick = checked(pending.RequestedTick + AnnualIntervalTicks);
            PendingRequest = null;
        }

        public RulerStrategyPlan PlanAt(long tick) => ActivePlan != null && ActivePlan.IsActiveAt(tick)
            ? ActivePlan : null;

        public RulerStrategyStateSnapshot CaptureState() => new RulerStrategyStateSnapshot(RequestSequence,
            NextAnnualTick, PendingRequest, ActivePlan, journal);

        public void Restore(RulerStrategyStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RequestSequence = snapshot.RequestSequence;
            NextAnnualTick = snapshot.NextAnnualTick;
            PendingRequest = snapshot.PendingRequest;
            ActivePlan = snapshot.ActivePlan;
            journal.Clear();
            journal.AddRange(snapshot.Journal);
        }
    }
}
