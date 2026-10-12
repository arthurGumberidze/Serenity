using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Game.Domain.AI;

namespace Game.Infrastructure.Reliability
{
    /// <summary>Pure, independently testable evidence calculations (schema 2).</summary>
    public static class U30Evidence
    {
        public const string CanonicalHashKind = "xml-and-exact-tier-runtime-v2";
        public const string OutcomeHashKind = "identity-preserving-canonical-xml-v2";

        public static double CpuPercent(long usedTicks, double wallSeconds) => wallSeconds <= 0 ? -1d :
            usedTicks / (double)TimeSpan.TicksPerSecond / wallSeconds * 100d;

        public static bool HasRepeatedTravelRejection(IEnumerable<AiDecision> decisions)
        {
            var recent = decisions.OrderByDescending(x => x.Sequence).Take(8).ToArray();
            return recent.Length == 8 && recent.All(x => x.Kind == AiDecisionKind.Recover &&
                x.Reason.StartsWith("Travel rejected:", StringComparison.Ordinal)) &&
                recent.All(x => x.Reason == recent[0].Reason && x.UnitId == recent[0].UnitId &&
                    x.TargetLocationId == recent[0].TargetLocationId);
        }

        public static string CanonicalHash(byte[] xml, IEnumerable<U30TierRuntimeRecord> records)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(CanonicalHashKind); writer.Write(xml.Length); writer.Write(xml);
                var ordered = records.OrderBy(x => x.character_id, StringComparer.Ordinal).ToArray();
                writer.Write(ordered.Length);
                foreach (var record in ordered)
                {
                    var state = record.ToState();
                    writer.Write(record.character_id); writer.Write(record.tier);
                    writer.Write(record.numeric_bits); writer.Write(state.IsMoving); writer.Write(state.LocationKey ?? "");
                    writer.Write(state.ProcessedCalendarTicks); writer.Write(state.ProcessedBiologicalTicks);
                    writer.Write(state.StepCount); writer.Write(state.LastSimulationCalendarTick);
                    writer.Write(state.LastSimulationBiologicalTick); writer.Write(state.OffCameraStepCount);
                    writer.Write(state.DeterministicAccumulator); writer.Write(state.AbstractActivityProgress);
                }
                return Hash(stream.ToArray());
            }
        }

        public static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }
}
