using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Tiers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure.Performance
{
    public sealed class U29BenchmarkScenario
    {
        private readonly LocalSceneCompositionRoot root;
        private readonly U29BenchmarkConfig config;
        private readonly List<StableEntityId> createdIds = new List<StableEntityId>();

        private U29BenchmarkScenario(LocalSceneCompositionRoot compositionRoot, U29BenchmarkConfig benchmarkConfig)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            config = benchmarkConfig ?? throw new ArgumentNullException(nameof(benchmarkConfig));
        }

        public int TierChurnTransitions { get; private set; }
        public IReadOnlyList<StableEntityId> CreatedIds => createdIds;

        public static U29BenchmarkScenario Create(LocalSceneCompositionRoot root, U29BenchmarkConfig config)
        {
            var scenario = new U29BenchmarkScenario(root, config);
            scenario.BuildPopulation();
            scenario.EnsureRepresentativeWorkloads();
            scenario.AssertExactPopulationAndTiers();
            return scenario;
        }

        public static StableEntityId DeterministicCharacterId(long seed, int index)
        {
            unchecked
            {
                var first = 14695981039346656037UL;
                var second = 1099511628211UL;
                foreach (var value in (seed.ToString(CultureInfo.InvariantCulture) + "|u29_character"))
                    first = (first ^ value) * 1099511628211UL;
                foreach (var value in index.ToString(CultureInfo.InvariantCulture))
                    second = (second ^ value) * 14695981039346656037UL;
                return StableEntityId.FromDeterministicHash(first, second);
            }
        }

        public void ExerciseTierChurn()
        {
            var candidates = createdIds.Where(id => root.Tiers.TryGetTier(id, out _)).Take(12).ToArray();
            var original = candidates.ToDictionary(id => id, id => root.Tiers.GetTier(id));
            foreach (var id in candidates)
            {
                var tier = original[id];
                var next = tier == CharacterSimulationTier.Tier1 ? CharacterSimulationTier.Tier2 :
                    tier == CharacterSimulationTier.Tier2 ? CharacterSimulationTier.Tier3 : CharacterSimulationTier.Tier1;
                root.Tiers.Transition(id, next);
                TierChurnTransitions++;
            }
            foreach (var id in candidates.Reverse())
            {
                root.Tiers.Transition(id, original[id]);
                TierChurnTransitions++;
            }
            AssertExactPopulationAndTiers();
        }

        public U29RuntimeCounts CaptureCounts()
        {
            var all = root.Characters.GetAll();
            var tiers = root.Tiers.ManagedIds.Select(id => root.Tiers.GetTier(id)).ToArray();
            return new U29RuntimeCounts
            {
                canonicalCharacters = all.Count,
                distinctStableIds = all.Select(x => x.Id).Distinct().Count(),
                tier1 = tiers.Count(x => x == CharacterSimulationTier.Tier1),
                tier2 = tiers.Count(x => x == CharacterSimulationTier.Tier2),
                tier3 = tiers.Count(x => x == CharacterSimulationTier.Tier3),
                tier1Presenters = root.CharacterPresentations.Count,
                tier2Entities = root.Tier2Runtime.Materializer.EntityCount,
                tier3Records = root.Tier3Characters.Count,
                gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                uiSubscriptions = root.GameplayUi == null ? 0 : root.GameplayUi.SubscriptionCount,
                pendingEvents = root.Events == null ? 0 : root.Events.PendingPlayerEvents.Count,
                eventHistory = root.Events == null ? 0 : root.Events.History.Count,
                activeCombats = root.Combat == null ? 0 : root.Combat.Encounters.Count(x =>
                    x.Status == Game.Domain.Combat.CombatEncounterStatus.Active),
                militaryUnits = root.MilitaryUnits == null ? 0 : root.MilitaryUnits.Units.Count,
                activeTravels = root.GlobalMap == null ? 0 : root.GlobalMap.Travels.Count
            };
        }

        public ulong ComputeCanonicalStateHash()
        {
            unchecked
            {
                var hash = 14695981039346656037UL;
                foreach (var character in root.Characters.GetAll())
                {
                    hash = Mix(hash, character.Id.ToString());
                    var state = character.CaptureState();
                    hash = Mix(hash, (ulong)state.LifeState);
                    hash = Mix(hash, (ulong)state.Health.Head.Value);
                    hash = Mix(hash, (ulong)state.Health.Torso.Value);
                    hash = Mix(hash, (ulong)state.Health.LeftArm.Value);
                    hash = Mix(hash, (ulong)state.Health.RightArm.Value);
                    hash = Mix(hash, (ulong)state.Health.LeftLeg.Value);
                    hash = Mix(hash, (ulong)state.Health.RightLeg.Value);
                    hash = Mix(hash, (ulong)root.Tiers.GetTier(character.Id));
                }
                hash = Mix(hash, root.Production.ComputeStateHash());
                hash = Mix(hash, root.Research.ComputeStateHash());
                hash = Mix(hash, root.HealthFamily.ComputeStateHash());
                hash = Mix(hash, root.Weather.ComputeStateHash());
                hash = Mix(hash, root.Combat.ComputeStateHash());
                hash = Mix(hash, root.GlobalMap.ComputeStateHash());
                hash = Mix(hash, root.Events.ComputeStateHash());
                return hash;
            }
        }

        public U29SaveObservation ObserveSave(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException(nameof(outputDirectory));
            Directory.CreateDirectory(outputDirectory);
            var stopwatch = Stopwatch.StartNew();
            var canonicalHashBefore = ComputeCanonicalStateHash();
            var snapshot = root.Saves.Capture();
            stopwatch.Stop();
            var captureMs = stopwatch.Elapsed.TotalMilliseconds;

            var codec = new SaveXmlCodec();
            stopwatch.Restart();
            var xml = codec.Serialize(snapshot);
            stopwatch.Stop();
            var serializeMs = stopwatch.Elapsed.TotalMilliseconds;
            var savePath = Path.Combine(outputDirectory, "save-observation.xml");
            stopwatch.Restart();
            File.WriteAllBytes(savePath, xml);
            stopwatch.Stop();
            var writeMs = stopwatch.Elapsed.TotalMilliseconds;

            var identities = root.Tiers.ManagedIds.Select(id => new IdentityTier(id, root.Tiers.GetTier(id))).ToArray();
            var identityPath = Path.Combine(outputDirectory, "identity-tier-observation.txt");
            File.WriteAllText(identityPath, SerializeIdentityTiers(identities), new UTF8Encoding(false));
            stopwatch.Restart();
            var restoredSnapshot = codec.Deserialize(File.ReadAllBytes(savePath));
            var restoredIdentities = DeserializeIdentityTiers(File.ReadAllText(identityPath, Encoding.UTF8));
            stopwatch.Stop();
            var readDeserializeMs = stopwatch.Elapsed.TotalMilliseconds;
            if (restoredSnapshot.FormatVersion != snapshot.FormatVersion)
                throw new InvalidDataException("Save observation version changed during round-trip.");
            var restoreCoordinator = new SaveCoordinator(restoredSnapshot.SessionId, root.Clock,
                new SingleSnapshotStore(restoredSnapshot), root.Production, root.Research, root.Succession,
                root.HealthFamily, root.Weather, root.Combat, root.MilitaryUnits, root.PlayerControl,
                root.BuildingDamage, root.GlobalMap, root.StrategicAi, root.Events);
            stopwatch.Restart();
            restoreCoordinator.Load(StableEntityId.Parse("00000000000000000000000000002900"));
            root.RebindAfterLoad(restoreCoordinator.Clock);
            stopwatch.Stop();
            var canonicalHashAfter = ComputeCanonicalStateHash();
            var exact = identities.SequenceEqual(restoredIdentities);
            return new U29SaveObservation
            {
                captureMs = captureMs,
                xmlSerializeMs = serializeMs,
                fileWriteMs = writeMs,
                readDeserializeMs = readDeserializeMs,
                restoreMs = stopwatch.Elapsed.TotalMilliseconds,
                payloadBytes = xml.LongLength,
                restoredCharacters = restoredIdentities.Length,
                restoredTier1 = restoredIdentities.Count(x => x.Tier == CharacterSimulationTier.Tier1),
                restoredTier2 = restoredIdentities.Count(x => x.Tier == CharacterSimulationTier.Tier2),
                restoredTier3 = restoredIdentities.Count(x => x.Tier == CharacterSimulationTier.Tier3),
                identityTierHash = ComputeIdentityTierHash(restoredIdentities),
                identityTierHashHex = ComputeIdentityTierHash(restoredIdentities).ToString("X16", CultureInfo.InvariantCulture),
                exactIdentityTierRoundTrip = exact,
                canonicalHashBeforeRestore = canonicalHashBefore,
                canonicalHashAfterRestore = canonicalHashAfter,
                canonicalHashBeforeRestoreHex = canonicalHashBefore.ToString("X16", CultureInfo.InvariantCulture),
                canonicalHashAfterRestoreHex = canonicalHashAfter.ToString("X16", CultureInfo.InvariantCulture),
                exactCanonicalStateRoundTrip = canonicalHashBefore == canonicalHashAfter,
                restoredPendingEvents = root.Events.PendingPlayerEvents.Count,
                restoredEventHistory = root.Events.History.Count
            };
        }

        public static string SerializeIdentityTiers(IEnumerable<KeyValuePair<StableEntityId, CharacterSimulationTier>> values) =>
            SerializeIdentityTiers(values.Select(x => new IdentityTier(x.Key, x.Value)));

        private void BuildPopulation()
        {
            if (root.Characters.Count > config.population)
                throw new InvalidOperationException("Normal bootstrap exceeds the requested U29 population.");

            for (var index = 0; root.Characters.Count < config.population; index++)
            {
                var id = DeterministicCharacterId(config.seed, index);
                if (root.Characters.TryGet(id, out _)) continue;
                var character = CreateBenchmarkCharacter(id, index);
                root.Characters.Add(character);
                createdIds.Add(id);
                EnsureInventory(id);
            }

            foreach (var character in root.Characters.GetAll()) EnsureInventory(character.Id);

            var retainedTier1 = root.Tiers.ManagedIds.Where(id => root.Tiers.GetTier(id) == CharacterSimulationTier.Tier1)
                .Take(config.tier1Count).ToList();
            var retainedTier2 = root.Tiers.ManagedIds.Where(id => root.Tiers.GetTier(id) == CharacterSimulationTier.Tier2)
                .Take(config.tier2Count).ToList();
            var retainedTier3 = root.Tiers.ManagedIds.Where(id => root.Tiers.GetTier(id) == CharacterSimulationTier.Tier3)
                .Take(config.tier3Count).ToList();
            var assigned = new HashSet<StableEntityId>(retainedTier1.Concat(retainedTier2).Concat(retainedTier3));
            var available = root.Characters.GetAll().Select(x => x.Id).Where(id => !assigned.Contains(id)).ToList();
            Fill(retainedTier1, config.tier1Count, available);
            Fill(retainedTier2, config.tier2Count, available);
            Fill(retainedTier3, config.tier3Count, available);
            if (available.Count != 0) throw new InvalidOperationException("U29 tier assignment did not consume the population exactly.");

            ApplyTier(retainedTier1, CharacterSimulationTier.Tier1);
            ApplyTier(retainedTier2, CharacterSimulationTier.Tier2);
            ApplyTier(retainedTier3, CharacterSimulationTier.Tier3);
            root.Production.Population = root.Characters.Count;
            root.SynchronizeStarterSettlement();
        }

        private void ApplyTier(IEnumerable<StableEntityId> ids, CharacterSimulationTier target)
        {
            var ordinal = 0;
            var anchor = root.CameraController.transform.position;
            foreach (var id in ids.OrderBy(x => x))
            {
                if (root.Tiers.TryGetTier(id, out var existing))
                {
                    if (existing != target) root.Tiers.Transition(id, target);
                    ordinal++;
                    continue;
                }
                var position = PositionFor(target, ordinal++, anchor);
                root.Tiers.Materialize(root.Characters.Get(id), target,
                    new CharacterRuntimeState(id, position, default, false, "local",
                        lastSimulationCalendarTick: root.Clock.State.CalendarTicks,
                        lastSimulationBiologicalTick: root.Clock.State.BiologicalTicks));
            }
        }

        private void EnsureRepresentativeWorkloads()
        {
            var worker = root.Tiers.ManagedIds.First(id => root.Tiers.GetTier(id) == CharacterSimulationTier.Tier3);
            root.Research.TryAssignWorker(worker);
            if (root.Research.ActiveProject == null && root.Research.AvailableTechnologies.Count > 0)
                root.Research.Start(root.Research.AvailableTechnologies[0].Id);
            foreach (var resource in root.Resources.All) root.Production.SetTarget(resource.Id, Math.Max(20, config.population / 2));
        }

        private void EnsureInventory(StableEntityId id)
        {
            var owner = new InventoryOwner(InventoryOwnerKind.Character, id);
            if (!root.Inventories.TryGet(owner, out _))
                root.Inventories.Add(new ResourceInventory(owner, root.Resources, 128));
        }

        private void AssertExactPopulationAndTiers()
        {
            var counts = CaptureCounts();
            if (counts.canonicalCharacters != config.population || counts.distinctStableIds != config.population ||
                counts.tier1 != config.tier1Count || counts.tier2 != config.tier2Count || counts.tier3 != config.tier3Count ||
                counts.tier1Presenters != config.tier1Count || counts.tier2Entities != config.tier2Count ||
                counts.tier3Records != config.tier3Count)
                throw new InvalidOperationException("U29 population/tier representation invariant failed.");
        }

        private static Character CreateBenchmarkCharacter(StableEntityId id, int index) => Character.Restore(
            new CharacterState(id, new CharacterName("Benchmark " + index.ToString("D4", CultureInfo.InvariantCulture)),
                index % 2 == 0 ? CharacterSex.Male : CharacterSex.Female, 0, CharacterLifeState.Alive, null,
                null, null, null, null, new Game.Domain.Characters.ProfessionId("worker"),
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static WorldPosition PositionFor(CharacterSimulationTier tier, int ordinal, Vector3 anchor)
        {
            var angle = ordinal * 2.39996323d;
            var radius = tier == CharacterSimulationTier.Tier1 ? 8d + ordinal % 8 :
                tier == CharacterSimulationTier.Tier2 ? 36d + ordinal % 8 : 120d + ordinal % 16;
            return new WorldPosition(anchor.x + (float)(Math.Cos(angle) * radius), anchor.y,
                anchor.z + (float)(Math.Sin(angle) * radius));
        }

        private static void Fill(List<StableEntityId> destination, int targetCount, List<StableEntityId> available)
        {
            while (destination.Count < targetCount)
            {
                if (available.Count == 0) throw new InvalidOperationException("Insufficient U29 identities for tier mix.");
                destination.Add(available[0]);
                available.RemoveAt(0);
            }
        }

        private static string SerializeIdentityTiers(IEnumerable<IdentityTier> values)
        {
            var builder = new StringBuilder();
            foreach (var value in values.OrderBy(x => x.Id))
                builder.Append(value.Id).Append('|').Append((int)value.Tier).Append('\n');
            return builder.ToString();
        }

        private static IdentityTier[] DeserializeIdentityTiers(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var values = new List<IdentityTier>();
            var seen = new HashSet<StableEntityId>();
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length != 2 || !StableEntityId.TryParse(parts[0], out var id) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tierValue) ||
                    !Enum.IsDefined(typeof(CharacterSimulationTier), tierValue))
                    throw new InvalidDataException("Invalid U29 identity/tier observation.");
                if (!seen.Add(id)) throw new InvalidDataException("Duplicate U29 observed identity.");
                values.Add(new IdentityTier(id, (CharacterSimulationTier)tierValue));
            }
            return values.OrderBy(x => x.Id).ToArray();
        }

        private static ulong ComputeIdentityTierHash(IEnumerable<IdentityTier> values)
        {
            unchecked
            {
                var hash = 14695981039346656037UL;
                foreach (var value in values.OrderBy(x => x.Id))
                {
                    hash = Mix(hash, value.Id.ToString());
                    hash = Mix(hash, (ulong)value.Tier);
                }
                return hash;
            }
        }

        private static ulong Mix(ulong hash, string value)
        {
            unchecked
            {
                foreach (var character in value) hash = (hash ^ character) * 1099511628211UL;
                return hash;
            }
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            unchecked
            {
                for (var shift = 0; shift < 64; shift += 8) hash = (hash ^ (byte)(value >> shift)) * 1099511628211UL;
                return hash;
            }
        }

        private readonly struct IdentityTier : IEquatable<IdentityTier>
        {
            public IdentityTier(StableEntityId id, CharacterSimulationTier tier) { Id = id; Tier = tier; }
            public StableEntityId Id { get; }
            public CharacterSimulationTier Tier { get; }
            public bool Equals(IdentityTier other) => Id == other.Id && Tier == other.Tier;
            public override bool Equals(object obj) => obj is IdentityTier other && Equals(other);
            public override int GetHashCode() => (Id.GetHashCode() * 397) ^ (int)Tier;
        }

        private sealed class SingleSnapshotStore : ISaveStore
        {
            private readonly SaveSnapshot snapshot;
            public SingleSnapshotStore(SaveSnapshot value) => snapshot = value ?? throw new ArgumentNullException(nameof(value));
            public void Save(StableEntityId slotId, SaveSnapshot value) => throw new NotSupportedException();
            public SaveSnapshot Load(StableEntityId slotId) => snapshot;
        }
    }
}
