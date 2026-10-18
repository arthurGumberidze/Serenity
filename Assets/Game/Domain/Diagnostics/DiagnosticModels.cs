using System;

namespace Game.Domain.Diagnostics
{
    public sealed class DiagnosticSaveState
    {
        public DiagnosticSaveState(bool cheatsUsed, ulong sequence)
        {
            CheatsUsed = cheatsUsed;
            Sequence = sequence;
        }

        public bool CheatsUsed { get; }
        public ulong Sequence { get; }
        public bool AchievementsEnabled => !CheatsUsed;
    }

    public enum DiagnosticCommandKind
    {
        UnlockAll = 1,
        GrantResources = 2,
        CreateArmy = 3,
        CreateFamily = 4
    }

    public enum DiagnosticFailure
    {
        None = 0,
        EmptyCode = 1,
        UnknownCode = 2,
        MissingStorage = 3,
        MissingBarracks = 4,
        MissingHome = 5,
        NoIndoorBedPosition = 6,
        TransactionFailed = 7
    }

    public readonly struct DiagnosticParseResult
    {
        public DiagnosticParseResult(DiagnosticCommandKind command, DiagnosticFailure failure)
        {
            Command = command;
            Failure = failure;
        }

        public DiagnosticCommandKind Command { get; }
        public DiagnosticFailure Failure { get; }
        public bool IsValid => Failure == DiagnosticFailure.None;
    }

    public static class DiagnosticCommandParser
    {
        public static DiagnosticParseResult Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new DiagnosticParseResult(default, DiagnosticFailure.EmptyCode);
            switch (raw.Trim().ToLowerInvariant())
            {
                case "serenity": return new DiagnosticParseResult(DiagnosticCommandKind.UnlockAll, DiagnosticFailure.None);
                case "res_serenity": return new DiagnosticParseResult(DiagnosticCommandKind.GrantResources, DiagnosticFailure.None);
                case "army_serenity": return new DiagnosticParseResult(DiagnosticCommandKind.CreateArmy, DiagnosticFailure.None);
                case "family_serenity": return new DiagnosticParseResult(DiagnosticCommandKind.CreateFamily, DiagnosticFailure.None);
                default: return new DiagnosticParseResult(default, DiagnosticFailure.UnknownCode);
            }
        }
    }

    public sealed class DiagnosticExecutionResult
    {
        public DiagnosticExecutionResult(DiagnosticCommandKind command, DiagnosticFailure failure,
            int affectedCount = 0, long grantedUnits = 0, StableEntityId? firstCharacterId = null,
            StableEntityId? secondCharacterId = null, StableEntityId? bedId = null,
            StableEntityId? pregnancyId = null)
        {
            Command = command;
            Failure = failure;
            AffectedCount = affectedCount;
            GrantedUnits = grantedUnits;
            FirstCharacterId = firstCharacterId;
            SecondCharacterId = secondCharacterId;
            BedId = bedId;
            PregnancyId = pregnancyId;
        }

        public DiagnosticCommandKind Command { get; }
        public DiagnosticFailure Failure { get; }
        public int AffectedCount { get; }
        public long GrantedUnits { get; }
        public StableEntityId? FirstCharacterId { get; }
        public StableEntityId? SecondCharacterId { get; }
        public StableEntityId? BedId { get; }
        public StableEntityId? PregnancyId { get; }
        public bool Succeeded => Failure == DiagnosticFailure.None;
    }
}
