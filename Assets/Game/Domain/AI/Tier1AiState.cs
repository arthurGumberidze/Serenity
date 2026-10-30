using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.AI
{
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(float x, float y, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                float.IsNaN(z) || float.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(x), "World coordinates must be finite.");
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public double DistanceSquared(WorldPosition other)
        {
            var x = (double)X - other.X;
            var y = (double)Y - other.Y;
            var z = (double)Z - other.Z;
            return x * x + y * y + z * z;
        }

        public bool Equals(WorldPosition other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode(); }
        }
        public override string ToString() => $"({X:0.##},{Y:0.##},{Z:0.##})";
    }

    public enum UtilityActionKind
    {
        Idle = 0,
        Rest = 1,
        Haul = 2,
        ManualMove = 3,
        ManualHaul = 4,
        Produce = 5,
        Research = 6,
        ManualTask = 7,
        Eat = 8,
        Drink = 9
    }

    public enum AiActionPhase
    {
        Idle = 0,
        Resting = 1,
        MoveToSource = 2,
        Pickup = 3,
        MoveToDestination = 4,
        Dropoff = 5,
        Completed = 6,
        Failed = 7,
        Unmaterialized = 8,
        MoveToTarget = 9,
        Working = 10,
        DeliverProduction = 11,
        Researching = 12,
        Eating = 13,
        Drinking = 14
    }

    public sealed class Tier1Needs
    {
        public Tier1Needs(double hunger = 0d, double energy = 1d, double mood = 1d, double thirst = 0d)
        {
            Hunger = Validate(hunger, nameof(hunger));
            Energy = Validate(energy, nameof(energy));
            Mood = Validate(mood, nameof(mood));
            Thirst = Validate(thirst, nameof(thirst));
        }

        public double Hunger { get; private set; }
        public double Energy { get; private set; }
        public double Mood { get; private set; }
        public double Thirst { get; private set; }

        public void Advance(double hungerIncrease, double energyDecrease, double thirstIncrease = 0d)
        {
            if (!IsFiniteNonNegative(hungerIncrease)) throw new ArgumentOutOfRangeException(nameof(hungerIncrease));
            if (!IsFiniteNonNegative(energyDecrease)) throw new ArgumentOutOfRangeException(nameof(energyDecrease));
            if (!IsFiniteNonNegative(thirstIncrease)) throw new ArgumentOutOfRangeException(nameof(thirstIncrease));
            Hunger = Clamp01(Hunger + hungerIncrease);
            Energy = Clamp01(Energy - energyDecrease);
            Thirst = Clamp01(Thirst + thirstIncrease);
        }

        public void RecoverEnergy(double amount)
        {
            if (!IsFiniteNonNegative(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            Energy = Clamp01(Energy + amount);
        }

        public void SatisfyHunger(double amount)
        {
            if (!IsFiniteNonNegative(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            Hunger = Clamp01(Hunger - amount);
        }

        public void SatisfyThirst(double amount)
        {
            if (!IsFiniteNonNegative(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            Thirst = Clamp01(Thirst - amount);
        }

        public void AdjustMood(double delta)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            Mood = Clamp01(Mood + delta);
        }

        private static double Validate(double value, string parameterName)
        {
            if (!IsFiniteNonNegative(value) || value > 1d) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        private static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        private static double Clamp01(double value) => value <= 0d ? 0d : value >= 1d ? 1d : value;
    }

    public readonly struct Tier1AiAgentSnapshot
    {
        public Tier1AiAgentSnapshot(StableEntityId characterId, WorldPosition position, double hunger, double energy,
            UtilityActionKind currentAction, AiActionPhase phase, double mood = 1d, double thirst = 0d)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            if (!Enum.IsDefined(typeof(UtilityActionKind), currentAction)) throw new ArgumentOutOfRangeException(nameof(currentAction));
            if (!Enum.IsDefined(typeof(AiActionPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            CharacterId = characterId;
            Position = position;
            Hunger = hunger;
            Energy = energy;
            Mood = mood;
            Thirst = thirst;
            CurrentAction = currentAction;
            Phase = phase;
        }

        public StableEntityId CharacterId { get; }
        public WorldPosition Position { get; }
        public double Hunger { get; }
        public double Energy { get; }
        public double Mood { get; }
        public double Thirst { get; }
        public UtilityActionKind CurrentAction { get; }
        public AiActionPhase Phase { get; }
    }

    public sealed class Tier1AiAgentState
    {
        public Tier1AiAgentState(StableEntityId characterId, WorldPosition position, Tier1Needs needs = null)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            CharacterId = characterId;
            Position = position;
            Needs = needs ?? new Tier1Needs();
            CurrentAction = UtilityActionKind.Idle;
            Phase = AiActionPhase.Idle;
        }

        public StableEntityId CharacterId { get; }
        public WorldPosition Position { get; private set; }
        public Tier1Needs Needs { get; }
        public UtilityActionKind CurrentAction { get; private set; }
        public AiActionPhase Phase { get; private set; }
        public long DecisionsMade { get; private set; }
        public event Action<Tier1AiAgentState> ActionChanged;

        public void UpdatePosition(WorldPosition position) => Position = position;

        public void SetAction(UtilityActionKind action, AiActionPhase phase)
        {
            if (!Enum.IsDefined(typeof(UtilityActionKind), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (!Enum.IsDefined(typeof(AiActionPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            var changed = CurrentAction != action || Phase != phase;
            CurrentAction = action;
            Phase = phase;
            if (changed) ActionChanged?.Invoke(this);
        }

        public void SetPhase(AiActionPhase phase)
        {
            if (!Enum.IsDefined(typeof(AiActionPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            var changed = Phase != phase;
            Phase = phase;
            if (changed) ActionChanged?.Invoke(this);
        }

        public void RecordDecision() => DecisionsMade = checked(DecisionsMade + 1);

        public Tier1AiAgentSnapshot CaptureState() => new Tier1AiAgentSnapshot(CharacterId, Position,
            Needs.Hunger, Needs.Energy, CurrentAction, Phase, Needs.Mood, Needs.Thirst);
    }

    public sealed class Tier1AiAgentRegistry
    {
        private readonly Dictionary<StableEntityId, Tier1AiAgentState> agents =
            new Dictionary<StableEntityId, Tier1AiAgentState>();

        public int Count => agents.Count;
        public IEnumerable<Tier1AiAgentState> All => agents.Values.OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal);

        public void Add(Tier1AiAgentState agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (!agents.TryAdd(agent.CharacterId, agent))
                throw new InvalidOperationException("A Tier 1 AI agent is already registered for this character.");
        }

        public bool TryGet(StableEntityId characterId, out Tier1AiAgentState agent)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            return agents.TryGetValue(characterId, out agent);
        }

        public bool Remove(StableEntityId characterId, Tier1AiAgentState expected)
        {
            if (!agents.TryGetValue(characterId, out var current) || !ReferenceEquals(current, expected)) return false;
            return agents.Remove(characterId);
        }
    }
}
