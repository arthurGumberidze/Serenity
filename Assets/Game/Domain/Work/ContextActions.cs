using System;
using Game.Domain.AI;
using Game.Domain.Resources;

namespace Game.Domain.Work
{
    public enum ContextActionKind
    {
        Inspect = 0,
        Gather = 1,
        Mine = 2,
        Chop = 3,
        Haul = 4,
        Build = 5,
        Guard = 6,
        Move = 7
    }

    public enum ContextTargetKind
    {
        Ground = 0,
        Area = 1,
        Character = 2,
        ResourcePile = 3,
        ResourceSource = 4,
        Building = 5
    }

    public enum ContextResourceClass
    {
        None = 0,
        Gatherable = 1,
        Mineable = 2,
        Choppable = 3
    }

    public enum ContextUnavailableReason
    {
        None = 0,
        WrongTargetType = 1,
        WrongRole = 2,
        TooYoung = 3,
        InvalidStatus = 4,
        MissingTool = 5,
        MissingTechnology = 6,
        TooFar = 7,
        NoPath = 8,
        NoResource = 9,
        NoCapacity = 10,
        Busy = 11,
        Cooldown = 12,
        CriticalNeed = 13,
        NonInterruptiblePhase = 14
    }

    public readonly struct ContextCommandTarget
    {
        public ContextCommandTarget(ContextTargetKind kind, WorldPosition position,
            StableEntityId entityId = default, ResourceId? resourceId = null,
            ContextResourceClass resourceClass = ContextResourceClass.None)
        {
            if (!Enum.IsDefined(typeof(ContextTargetKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind != ContextTargetKind.Ground && kind != ContextTargetKind.Area && !entityId.IsValid)
                throw new ArgumentException("Entity target requires StableEntityId.", nameof(entityId));
            Kind = kind;
            Position = position;
            EntityId = entityId;
            ResourceId = resourceId;
            ResourceClass = resourceClass;
        }

        public ContextTargetKind Kind { get; }
        public WorldPosition Position { get; }
        public StableEntityId EntityId { get; }
        public ResourceId? ResourceId { get; }
        public ContextResourceClass ResourceClass { get; }
    }

    public readonly struct ContextActionFacts
    {
        public ContextActionFacts(bool alive = true, bool adult = true, bool correctRole = true,
            bool validStatus = true, bool hasTool = true, bool technologyUnlocked = true,
            bool withinDistance = true, bool pathAvailable = true, bool resourceAvailable = true,
            bool capacityAvailable = true, bool busy = false, bool cooldownReady = true,
            bool criticalNeed = false, bool interruptible = true)
        {
            Alive = alive;
            Adult = adult;
            CorrectRole = correctRole;
            ValidStatus = validStatus;
            HasTool = hasTool;
            TechnologyUnlocked = technologyUnlocked;
            WithinDistance = withinDistance;
            PathAvailable = pathAvailable;
            ResourceAvailable = resourceAvailable;
            CapacityAvailable = capacityAvailable;
            Busy = busy;
            CooldownReady = cooldownReady;
            CriticalNeed = criticalNeed;
            Interruptible = interruptible;
        }

        public bool Alive { get; }
        public bool Adult { get; }
        public bool CorrectRole { get; }
        public bool ValidStatus { get; }
        public bool HasTool { get; }
        public bool TechnologyUnlocked { get; }
        public bool WithinDistance { get; }
        public bool PathAvailable { get; }
        public bool ResourceAvailable { get; }
        public bool CapacityAvailable { get; }
        public bool Busy { get; }
        public bool CooldownReady { get; }
        public bool CriticalNeed { get; }
        public bool Interruptible { get; }
    }

    public readonly struct ContextActionAvailability
    {
        public ContextActionAvailability(bool enabled, ContextUnavailableReason reason,
            bool delayed = false)
        {
            Enabled = enabled;
            Reason = reason;
            Delayed = delayed;
        }

        public bool Enabled { get; }
        public ContextUnavailableReason Reason { get; }
        public bool Delayed { get; }
    }

    public static class ContextActionRules
    {
        public static ContextActionAvailability Evaluate(ContextActionKind action, ContextCommandTarget target,
            ContextActionFacts facts)
        {
            if (!SupportsTarget(action, target))
                return Disabled(ContextUnavailableReason.WrongTargetType);
            if (!facts.Alive || !facts.ValidStatus)
                return Disabled(ContextUnavailableReason.InvalidStatus);
            if (action != ContextActionKind.Move && action != ContextActionKind.Inspect && !facts.Adult)
                return Disabled(ContextUnavailableReason.TooYoung);
            if (!facts.CorrectRole) return Disabled(ContextUnavailableReason.WrongRole);
            if (!facts.HasTool) return Disabled(ContextUnavailableReason.MissingTool);
            if (!facts.TechnologyUnlocked) return Disabled(ContextUnavailableReason.MissingTechnology);
            if (!facts.WithinDistance) return Disabled(ContextUnavailableReason.TooFar);
            if (!facts.PathAvailable) return Disabled(ContextUnavailableReason.NoPath);
            if (!facts.ResourceAvailable) return Disabled(ContextUnavailableReason.NoResource);
            if (!facts.CapacityAvailable) return Disabled(ContextUnavailableReason.NoCapacity);
            if (!facts.CooldownReady) return Disabled(ContextUnavailableReason.Cooldown);
            if (facts.CriticalNeed)
                return new ContextActionAvailability(true, ContextUnavailableReason.CriticalNeed, true);
            if (facts.Busy && !facts.Interruptible)
                return new ContextActionAvailability(true, ContextUnavailableReason.NonInterruptiblePhase, true);
            if (facts.Busy)
                return new ContextActionAvailability(true, ContextUnavailableReason.Busy, true);
            return new ContextActionAvailability(true, ContextUnavailableReason.None);
        }

        private static bool SupportsTarget(ContextActionKind action, ContextCommandTarget target)
        {
            switch (action)
            {
                case ContextActionKind.Inspect:
                    return target.Kind == ContextTargetKind.Character || target.Kind == ContextTargetKind.ResourcePile ||
                           target.Kind == ContextTargetKind.ResourceSource || target.Kind == ContextTargetKind.Building;
                case ContextActionKind.Gather:
                    return target.Kind == ContextTargetKind.ResourceSource &&
                           target.ResourceClass == ContextResourceClass.Gatherable;
                case ContextActionKind.Mine:
                    return target.Kind == ContextTargetKind.ResourceSource &&
                           target.ResourceClass == ContextResourceClass.Mineable;
                case ContextActionKind.Chop:
                    return target.Kind == ContextTargetKind.ResourceSource &&
                           target.ResourceClass == ContextResourceClass.Choppable;
                case ContextActionKind.Haul:
                    return target.Kind == ContextTargetKind.ResourcePile;
                case ContextActionKind.Build:
                    return target.Kind == ContextTargetKind.Building;
                case ContextActionKind.Guard:
                    return target.Kind == ContextTargetKind.Character || target.Kind == ContextTargetKind.Building ||
                           target.Kind == ContextTargetKind.Ground || target.Kind == ContextTargetKind.Area;
                case ContextActionKind.Move:
                    return target.Kind == ContextTargetKind.Ground || target.Kind == ContextTargetKind.Area;
                default:
                    return false;
            }
        }

        private static ContextActionAvailability Disabled(ContextUnavailableReason reason) =>
            new ContextActionAvailability(false, reason);
    }
}
