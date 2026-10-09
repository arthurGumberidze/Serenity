using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Events;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Events;
using Game.Simulation.Health;
using Game.Simulation.Time;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U28")]
    public sealed class EventSystemTests
    {
        private static readonly long Day = TimeSpan.FromDays(1).Ticks;

        [Test]
        public void StoneAgeCatalogContainsStandaloneChainPlayerAndAiContent()
        {
            var catalog = StoneAgeEventCatalog.Create();
            Assert.That(catalog.All.Count, Is.EqualTo(4));
            Assert.That(catalog.All.Any(x => x.Nodes.Count >= 2), Is.True);
            Assert.That(catalog.All.Any(x => x.Audience == EventAudience.Player && x.Nodes.Any(n => n.Choices.Count >= 2)), Is.True);
            Assert.That(catalog.All.Any(x => x.Audience == EventAudience.Ai), Is.True);
            Assert.That(catalog.All.SelectMany(x => x.Nodes).SelectMany(x => x.Choices)
                .SelectMany(x => x.Effects).Any(x => x.Kind == EventEffectKind.HealthCondition), Is.True);
        }

        [Test]
        public void CatalogRejectsDuplicateMissingAndImmediateCycleDefinitions()
        {
            var valid = Definition("valid", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")));
            Assert.Throws<ArgumentException>(() => new EventCatalog(new[] { valid, valid }));
            var missing = new EventDefinition(new EventDefinitionId("missing"), EventCategory.Family,
                EventAudience.Player, new EventOwnerId("player"), EventTargetSelector.PlayerCouple,
                new EventTriggerDefinition(EventTriggerKind.Periodic, Day, Day), 1, 0,
                new EventNodeId("absent"), new[] { Node("start", Choice("finish")) });
            Assert.Throws<ArgumentException>(() => new EventCatalog(new[] { missing }));
            var cycle = Definition("cycle", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("loop", null, new EventTransitionDefinition(new EventNodeId("start"), 1))));
            Assert.Throws<ArgumentException>(() => new EventCatalog(new[] { cycle }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTriggerDefinition(EventTriggerKind.Periodic, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTransitionDefinition(new EventNodeId("x"), 0));

            var hugeA = Definition("huge_a", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), weight: int.MaxValue);
            var hugeB = Definition("huge_b", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), weight: int.MaxValue);
            Assert.Throws<OverflowException>(() => new EventCatalog(new[] { hugeA, hugeB }));
        }

        [Test]
        public void TriggerBoundaryAndRepeatedAdvanceAreExactNoOps()
        {
            var fixture = Fixture.Create(11, Definition("boundary", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("finish")), firstTick: Day, interval: Day * 2));
            fixture.Events.AdvanceTo(Day - 1);
            Assert.That(fixture.Events.PendingPlayerEvents, Is.Empty);
            fixture.Events.AdvanceTo(Day);
            Assert.That(fixture.Events.PendingPlayerEvents.Count, Is.EqualTo(1));
            fixture.Events.AdvanceTo(Day * 3);
            Assert.That(fixture.Events.PendingPlayerEvents.Count, Is.EqualTo(1),
                "An unresolved definition/scope must not accumulate duplicate pending offers.");
            var hash = fixture.Events.ComputeStateHash();
            fixture.Events.AdvanceTo(Day * 3);
            Assert.That(fixture.Events.ComputeStateHash(), Is.EqualTo(hash));
        }

        [Test]
        public void AbsoluteHookAndExplicitTriggersUseTheSameValidatedScheduler()
        {
            var absolute = TriggeredDefinition("absolute",
                new EventTriggerDefinition(EventTriggerKind.AbsoluteTick, Day));
            var hook = TriggeredDefinition("hook",
                new EventTriggerDefinition(EventTriggerKind.CanonicalHook, hookKind: CanonicalEventHookKind.BattleFinished));
            var explicitRequest = TriggeredDefinition("explicit",
                new EventTriggerDefinition(EventTriggerKind.Explicit), EventTargetSelector.Explicit);
            var fixture = Fixture.Create(13, new EventCatalog(new[] { explicitRequest, hook, absolute }));

            fixture.Events.AdvanceTo(Day);
            Assert.That(fixture.Events.PendingPlayerEvents.Select(x => x.DefinitionId),
                Does.Contain(absolute.Id));
            var hooked = fixture.Events.RaiseHook(CanonicalEventHookKind.BattleFinished, Day);
            Assert.That(hooked.Count, Is.EqualTo(2));
            Assert.That(hooked.All(x => x.DefinitionId == hook.Id), Is.True);
            var target = new EventTargetSet(new[] { EventTargetRef.Character(fixture.PlayerA.Id) });
            Assert.That(fixture.Events.Enqueue(explicitRequest.Id, target, Day).DefinitionId,
                Is.EqualTo(explicitRequest.Id));
        }

        [Test]
        public void WeightedSelectionIsStableAcrossDefinitionOrderAndCanVaryBySeed()
        {
            var a = Definition("weighted_a", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), weight: 1);
            var b = Definition("weighted_b", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), weight: 3);
            var forward = Fixture.Create(77, new EventCatalog(new[] { a, b }));
            var reverse = Fixture.Create(77, new EventCatalog(new[] { b, a }));
            forward.Events.AdvanceTo(Day); reverse.Events.AdvanceTo(Day);
            Assert.That(forward.Events.PendingPlayerEvents.Single().DefinitionId,
                Is.EqualTo(reverse.Events.PendingPlayerEvents.Single().DefinitionId));
            Assert.That(forward.Events.ComputeStateHash(), Is.EqualTo(reverse.Events.ComputeStateHash()));
            var results = new HashSet<EventDefinitionId>();
            for (var seed = 1; seed <= 64; seed++)
            {
                var sample = Fixture.Create(seed, new EventCatalog(new[] { a, b }));
                sample.Events.AdvanceTo(Day);
                results.Add(sample.Events.PendingPlayerEvents.Single().DefinitionId);
            }
            Assert.That(results.Count, Is.GreaterThan(1));
        }

        [Test]
        public void NoEventWeightIsARealWeightedOutcome()
        {
            var definition = Definition("rare", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), weight: 1);
            var observedNoEvent = false;
            for (var seed = 1; seed <= 32 && !observedNoEvent; seed++)
            {
                var fixture = Fixture.Create(seed, new EventCatalog(new[] { definition }), noEventWeight: 1000);
                fixture.Events.AdvanceTo(Day);
                observedNoEvent = fixture.Events.PendingPlayerEvents.Count == 0;
            }
            Assert.That(observedNoEvent, Is.True);
        }

        [Test]
        public void CooldownUsesDefinitionAndTargetScopeAndSurvivesRestoreAtExactBoundary()
        {
            var definition = Definition("cooldown", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")), cooldown: Day * 3, interval: Day);
            var fixture = Fixture.Create(8, definition);
            fixture.Events.AdvanceTo(Day);
            var first = fixture.Events.PendingPlayerEvents.Single();
            Assert.That(fixture.Events.ResolvePlayerChoice(first.InstanceId, new EventChoiceId("finish"), Day).Applied, Is.True);
            fixture.Events.AdvanceTo(Day * 3);
            Assert.That(fixture.Events.PendingPlayerEvents, Is.Empty);
            var saved = fixture.Events.CaptureState();
            fixture.Events.RestoreState(saved);
            fixture.Events.AdvanceTo(Day * 4);
            Assert.That(fixture.Events.PendingPlayerEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void StandaloneRelationshipOutcomeAppliesExactlyOnceAndRepeatedChoiceIsTypedNoOp()
        {
            var effect = new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_support", 0, 1);
            var fixture = Fixture.Create(21, Definition("support", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("support", effect))));
            fixture.Events.AdvanceTo(Day);
            var instance = fixture.Events.PendingPlayerEvents.Single();
            var first = fixture.Events.ResolvePlayerChoice(instance.InstanceId, new EventChoiceId("support"), Day);
            var second = fixture.Events.ResolvePlayerChoice(instance.InstanceId, new EventChoiceId("support"), Day);
            Assert.That(first.Status, Is.EqualTo(EventChoiceCommandStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(EventChoiceCommandStatus.InstanceNotFound));
            Assert.That(fixture.Health.Events.Count, Is.EqualTo(1));
            Assert.That(fixture.PlayerA.TryGetRelationship(fixture.PlayerB.Id, out var relation), Is.True);
            Assert.That(relation.Affinity.Value, Is.EqualTo(12));
        }

        [Test]
        public void MultiNodeChainContinuesAcrossSaveLoadWithoutRepeatingFirstEffect()
        {
            var first = Node("first", Choice("reconcile",
                new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_support", 0, 1),
                new EventTransitionDefinition(new EventNodeId("second"), 1, Day)));
            var second = Node("second", Choice("complete"));
            var fixture = Fixture.Create(31, Definition("chain", EventAudience.Player,
                EventTargetSelector.PlayerCouple, new[] { first, second }, 1, Day * 10, Day, Day * 10,
                InvalidEventTargetPolicy.Cancel));
            fixture.Events.AdvanceTo(Day);
            var offered = fixture.Events.PendingPlayerEvents.Single();
            var resolution = fixture.Events.ResolvePlayerChoice(offered.InstanceId, new EventChoiceId("reconcile"), Day);
            Assert.That(resolution.Instance.Status, Is.EqualTo(EventStatus.Waiting));
            Assert.That(fixture.Health.Events.Count, Is.EqualTo(1));
            var saved = fixture.Events.CaptureState();
            fixture.Events.RestoreState(saved);
            fixture.Events.AdvanceTo(Day * 2);
            var pending = fixture.Events.PendingPlayerEvents.Single();
            Assert.That(pending.InstanceId, Is.EqualTo(offered.InstanceId));
            Assert.That(pending.CurrentNodeId, Is.EqualTo(new EventNodeId("second")));
            fixture.Events.ResolvePlayerChoice(pending.InstanceId, new EventChoiceId("complete"), Day * 2);
            Assert.That(fixture.Health.Events.Count, Is.EqualTo(1));
            Assert.That(fixture.Events.History.Single().Status, Is.EqualTo(EventStatus.Completed));
        }

        [Test]
        public void PendingChoiceRoundTripPreservesInstanceAndAvailableChoices()
        {
            var fixture = Fixture.Create(44, Definition("pending", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("left"), Choice("right"))));
            fixture.Events.AdvanceTo(Day);
            var before = fixture.Events.PendingPlayerEvents.Single();
            var hash = fixture.Events.ComputeStateHash();
            fixture.Events.RestoreState(fixture.Events.CaptureState());
            var after = fixture.Events.PendingPlayerEvents.Single();
            Assert.That(after.InstanceId, Is.EqualTo(before.InstanceId));
            Assert.That(fixture.Events.Catalog.Get(after.DefinitionId).Nodes.Single().Choices.Select(x => x.Id),
                Is.EquivalentTo(new[] { new EventChoiceId("left"), new EventChoiceId("right") }));
            Assert.That(fixture.Events.ComputeStateHash(), Is.EqualTo(hash));
        }

        [Test]
        public void AiChoiceUsesIntegerScoreAndWorksWithoutPresentation()
        {
            var node = new EventNodeDefinition(new EventNodeId("start"), "AI choice", "AI chooses by score.", new[]
            {
                new EventChoiceDefinition(new EventChoiceId("low"), "Low", 1,
                    new[] { new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_dispute", 0, 1) }),
                new EventChoiceDefinition(new EventChoiceId("high"), "High", 100,
                    new[] { new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_support", 0, 1) })
            });
            var fixture = Fixture.Create(52, Definition("ai_choice", EventAudience.Ai,
                EventTargetSelector.PlayerCouple, node));
            fixture.Events.AdvanceTo(Day);
            Assert.That(fixture.Events.PendingPlayerEvents, Is.Empty);
            Assert.That(fixture.Events.History.Single().ChoiceId, Is.EqualTo(new EventChoiceId("high")));
            Assert.That(fixture.PlayerA.TryGetRelationship(fixture.PlayerB.Id, out var relation), Is.True);
            Assert.That(relation.Affinity.Value, Is.EqualTo(12));
        }

        [Test]
        public void MissingOrDeadTargetFollowsDeclaredPolicyAndInvalidChoiceDoesNotMutate()
        {
            var fixture = Fixture.Create(63, Definition("invalid_target", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("valid")),
                invalidPolicy: InvalidEventTargetPolicy.Expire));
            fixture.Events.AdvanceTo(Day);
            var instance = fixture.Events.PendingPlayerEvents.Single();
            var bad = fixture.Events.ResolvePlayerChoice(instance.InstanceId, new EventChoiceId("wrong"), Day);
            Assert.That(bad.Status, Is.EqualTo(EventChoiceCommandStatus.ChoiceNotFound));
            Assert.That(fixture.Events.PendingPlayerEvents.Count, Is.EqualTo(1));
            fixture.PlayerB.MarkDead(Character.BiologicalTicksPerYear * 20L);
            var dead = fixture.Events.ResolvePlayerChoice(instance.InstanceId, new EventChoiceId("valid"), Day);
            Assert.That(dead.Status, Is.EqualTo(EventChoiceCommandStatus.TargetInvalid));
            Assert.That(fixture.Events.History.Single().Status, Is.EqualTo(EventStatus.Expired));

            var alternateDefinition = new EventDefinition(new EventDefinitionId("alternate_target"),
                EventCategory.Character, EventAudience.Player, new EventOwnerId("player"),
                EventTargetSelector.PlayerCharacter,
                new EventTriggerDefinition(EventTriggerKind.Periodic, Day, Day * 10), 1, 0,
                new EventNodeId("start"), new[]
                {
                    Node("start", Choice("continue")),
                    Node("alternate", Choice("acknowledge", new EventEffectDefinition(EventEffectKind.None)))
                }, new[] { new EventConditionDefinition(EventConditionKind.TargetAlive) },
                InvalidEventTargetPolicy.AlternateNode, new EventNodeId("alternate"));
            var alternateFixture = Fixture.Create(64, alternateDefinition, aiCount: 0);
            alternateFixture.Events.AdvanceTo(Day);
            var alternateInstance = alternateFixture.Events.PendingPlayerEvents.First();
            Assert.That(alternateInstance.Targets[0].TryGetStableEntityId(out var targetId), Is.True);
            alternateFixture.Registry.Get(targetId).MarkDead(Character.BiologicalTicksPerYear * 20L);
            Assert.That(alternateFixture.Events.ResolvePlayerChoice(alternateInstance.InstanceId,
                new EventChoiceId("continue"), Day).Status, Is.EqualTo(EventChoiceCommandStatus.TargetInvalid));
            var alternate = alternateFixture.Events.PendingPlayerEvents.Single(x => x.InstanceId == alternateInstance.InstanceId);
            Assert.That(alternate.CurrentNodeId, Is.EqualTo(new EventNodeId("alternate")));
            Assert.That(alternateFixture.Events.ResolvePlayerChoice(alternate.InstanceId,
                new EventChoiceId("acknowledge"), Day).Applied, Is.True);
            Assert.That(alternateFixture.Events.History.Single().Status, Is.EqualTo(EventStatus.Completed));
        }

        [Test]
        public void DirectCatchUpIncrementalAndReversedTargetsProduceSameHash()
        {
            var definition = Definition("determinism", EventAudience.Ai, EventTargetSelector.AiCharacter,
                Node("start", Choice("finish")), interval: Day, cooldown: Day);
            var direct = Fixture.Create(71, definition, reverseTargets: false);
            var stepped = Fixture.Create(71, definition, reverseTargets: true);
            direct.Events.AdvanceTo(Day * 20);
            for (var day = 1; day <= 20; day++) stepped.Events.AdvanceTo(Day * day);
            Assert.That(direct.Events.ComputeStateHash(), Is.EqualTo(stepped.Events.ComputeStateHash()));
        }

        [Test]
        public void EventStateRoundTripsThroughStrictSnapshotV14AndV13RemainsReadable()
        {
            var definition = Definition("persist", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")));
            var fixture = Fixture.Create(81, definition);
            fixture.Events.AdvanceTo(Day);
            var snapshot = new SaveSnapshot(SaveSnapshot.EventVersion, Id(900),
                new GameTimeState(Day, Character.BiologicalTicksPerYear * 20L, 1, false), 400,
                events: fixture.Events.CaptureState());
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            var restored = Fixture.Create(81, definition);
            restored.Events.RestoreState(decoded.Events);
            Assert.That(decoded.FormatVersion, Is.EqualTo(14));
            Assert.That(restored.Events.ComputeStateHash(), Is.EqualTo(fixture.Events.ComputeStateHash()));

            var legacy = new SaveSnapshot(SaveSnapshot.StrategicAiVersion, Id(901), new GameTimeState(), 400);
            var legacyDecoded = codec.Deserialize(codec.Serialize(legacy));
            Assert.That(legacyDecoded.FormatVersion, Is.EqualTo(13));
            Assert.That(legacyDecoded.Events, Is.Null);

            var legacyTarget = Fixture.Create(81, definition);
            legacyTarget.Events.AdvanceTo(Day);
            Assert.That(legacyTarget.Events.ActiveInstances, Is.Not.Empty);
            var coordinator = new SaveCoordinator(Id(906), new GameClock(new GameTimeState()),
                new MemoryStore(legacyDecoded), eventService: legacyTarget.Events);
            coordinator.Load(Id(907));
            Assert.That(legacyTarget.Events.ActiveInstances, Is.Empty);
            Assert.That(legacyTarget.Events.History, Is.Empty);
            Assert.That(legacyTarget.Events.LastProcessedTick, Is.EqualTo(legacyDecoded.CalendarTicks));
        }

        [Test]
        public void CorruptV14PayloadAndDetachedRestoreValidationRejectBeforeCommit()
        {
            var definition = Definition("restore", EventAudience.Player, EventTargetSelector.PlayerCouple,
                Node("start", Choice("finish")));
            var fixture = Fixture.Create(91, definition);
            fixture.Events.AdvanceTo(Day);
            var beforeHash = fixture.Events.ComputeStateHash();
            var state = fixture.Events.CaptureState();
            var duplicate = new EventSaveState(state.WorldSeed, state.LastProcessedTick, state.NextRequestSequence,
                new[] { state.Instances[0], state.Instances[0] }, state.Cooldowns, state.History,
                state.ArchivedCount, state.ArchivedChecksum);
            Assert.Throws<ArgumentException>(() => fixture.Events.RestoreState(duplicate));
            Assert.That(fixture.Events.ComputeStateHash(), Is.EqualTo(beforeHash));

            var codec = new SaveXmlCodec();
            var snapshot = new SaveSnapshot(14, Id(902), new GameTimeState(Day, 0, 1, false), 400,
                events: state);
            var xml = Encoding.UTF8.GetString(codec.Serialize(snapshot));
            xml = xml.Replace("<nodeId>start</nodeId>", "<nodeId>missing</nodeId>");
            var decoded = codec.Deserialize(Encoding.UTF8.GetBytes(xml));
            Assert.Throws<ArgumentException>(() => fixture.Events.RestoreState(decoded.Events));
            Assert.That(fixture.Events.ComputeStateHash(), Is.EqualTo(beforeHash));
        }

        [Test]
        public void HistoryRetentionIsBoundedAndArchivesDeterministically()
        {
            var definition = new EventDefinition(new EventDefinitionId("explicit_history"), EventCategory.Character,
                EventAudience.Player, new EventOwnerId("player"), EventTargetSelector.Explicit,
                new EventTriggerDefinition(EventTriggerKind.Explicit), 1, 0, new EventNodeId("start"),
                new[] { Node("start", Choice("finish")) });
            var fixture = Fixture.Create(101, definition, historyLimit: 3);
            var target = new EventTargetSet(new[] { EventTargetRef.Character(fixture.PlayerA.Id) });
            for (var i = 0; i < 8; i++)
            {
                var instance = fixture.Events.Enqueue(definition.Id, target, i);
                fixture.Events.ResolvePlayerChoice(instance.InstanceId, new EventChoiceId("finish"), i);
            }
            Assert.That(fixture.Events.History.Count, Is.EqualTo(3));
            Assert.That(fixture.Events.ArchivedCount, Is.EqualTo(5));
            Assert.That(fixture.Events.ArchivedChecksum, Is.Not.Zero);
        }

        [Test]
        public void RestoreRejectsMissingDefinitionAndDifferentWorldSeed()
        {
            var source = Fixture.Create(111, Definition("source", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("finish"))));
            source.Events.AdvanceTo(Day);
            var saved = source.Events.CaptureState();
            var differentCatalog = Fixture.Create(111, Definition("other", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("finish"))));
            Assert.Throws<ArgumentException>(() => differentCatalog.Events.RestoreState(saved));
            var differentSeed = Fixture.Create(112, Definition("source", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("finish"))));
            Assert.Throws<InvalidOperationException>(() => differentSeed.Events.RestoreState(saved));
        }

        [Test]
        public void ImmediateTransitionBudgetTerminatesLongAcyclicChainAsInvalid()
        {
            var nodes = new List<EventNodeDefinition>();
            for (var i = 0; i < EventService.DefaultImmediateTransitionBudget + 2; i++)
            {
                var id = "node_" + i;
                nodes.Add(i == EventService.DefaultImmediateTransitionBudget + 1
                    ? Node(id, Choice("finish"))
                    : Node(id, Choice("next", null,
                        new EventTransitionDefinition(new EventNodeId("node_" + (i + 1)), 1))));
            }
            var definition = Definition("long_chain", EventAudience.Ai, EventTargetSelector.AiCharacter,
                nodes.ToArray(), 1, Day * 10, Day, Day * 10, InvalidEventTargetPolicy.Cancel);
            var fixture = Fixture.Create(115, definition, aiCount: 1);
            fixture.Events.AdvanceTo(Day);
            Assert.That(fixture.Events.ActiveInstances, Is.Empty);
            Assert.That(fixture.Events.History.Single().Status, Is.EqualTo(EventStatus.Invalid));
            Assert.That(fixture.Events.History.Single().OutcomeCode, Is.EqualTo("transition_budget_exceeded"));
        }

        [Test]
        public void SaveCoordinatorRejectsEventPayloadWhenNoEventServiceIsAttached()
        {
            var fixture = Fixture.Create(117, Definition("payload", EventAudience.Player,
                EventTargetSelector.PlayerCouple, Node("start", Choice("finish"))));
            fixture.Events.AdvanceTo(Day);
            var snapshot = new SaveSnapshot(SaveSnapshot.EventVersion, Id(903),
                new GameTimeState(Day, 0, 1, false), 400, events: fixture.Events.CaptureState());
            var slot = Id(904);
            var store = new MemoryStore(snapshot);
            var coordinator = new SaveCoordinator(Id(905), new GameClock(new GameTimeState()), store);
            Assert.Throws<InvalidOperationException>(() => coordinator.Load(slot));
        }

        [Test]
        public void ScaleSanityProcessesManyTargetsAndCheckpointsWithoutActiveQueueGrowth()
        {
            var definition = Definition("scale", EventAudience.Ai, EventTargetSelector.AiCharacter,
                Node("start", Choice("finish")), interval: Day, cooldown: Day);
            var fixture = Fixture.Create(121, definition, aiCount: 100);
            fixture.Events.AdvanceTo(Day * 50);
            Assert.That(fixture.Events.ActiveInstances, Is.Empty);
            Assert.That(fixture.Events.History.Count, Is.LessThanOrEqualTo(EventService.DefaultHistoryLimit));
            Assert.That(fixture.Events.ArchivedCount, Is.GreaterThan(0));
        }

        private static EventDefinition Definition(string id, EventAudience audience, EventTargetSelector selector,
            params EventNodeDefinition[] nodes) => Definition(id, audience, selector, nodes, 1, 0, Day, Day,
                InvalidEventTargetPolicy.Cancel);

        private static EventDefinition Definition(string id, EventAudience audience, EventTargetSelector selector,
            EventNodeDefinition node, int weight = 1, long cooldown = 0, long firstTick = -1, long interval = -1,
            InvalidEventTargetPolicy invalidPolicy = InvalidEventTargetPolicy.Cancel) =>
            Definition(id, audience, selector, new[] { node }, weight, cooldown,
                firstTick < 0 ? Day : firstTick, interval < 0 ? Day : interval, invalidPolicy);

        private static EventDefinition Definition(string id, EventAudience audience, EventTargetSelector selector,
            EventNodeDefinition first, EventNodeDefinition second) =>
            Definition(id, audience, selector, new[] { first, second }, 1, 0, Day, Day, InvalidEventTargetPolicy.Cancel);

        private static EventDefinition Definition(string id, EventAudience audience, EventTargetSelector selector,
            EventNodeDefinition[] nodes, int weight, long cooldown, long firstTick, long interval,
            InvalidEventTargetPolicy invalidPolicy) => new EventDefinition(new EventDefinitionId(id),
                EventCategory.Family, audience, new EventOwnerId(audience == EventAudience.Player ? "player" : "enemy"),
                selector, new EventTriggerDefinition(EventTriggerKind.Periodic, firstTick, interval), weight,
                cooldown, nodes[0].Id, nodes,
                selector == EventTargetSelector.PlayerCouple
                    ? new[] { new EventConditionDefinition(EventConditionKind.TargetsAreSpouses) }
                    : new[] { new EventConditionDefinition(EventConditionKind.TargetAlive) }, invalidPolicy);

        private static EventDefinition TriggeredDefinition(string id, EventTriggerDefinition trigger,
            EventTargetSelector selector = EventTargetSelector.PlayerCharacter) =>
            new EventDefinition(new EventDefinitionId(id), EventCategory.Character, EventAudience.Player,
                new EventOwnerId("player"), selector, trigger, 1, 0, new EventNodeId("start"),
                new[] { Node("start", Choice("finish")) },
                new[] { new EventConditionDefinition(EventConditionKind.TargetAlive) });

        private static EventNodeDefinition Node(string id, params EventChoiceDefinition[] choices) =>
            new EventNodeDefinition(new EventNodeId(id), id + " title", id + " description", choices);

        private static EventChoiceDefinition Choice(string id, EventEffectDefinition effect = null,
            params EventTransitionDefinition[] transitions) => new EventChoiceDefinition(new EventChoiceId(id), id,
                10, effect == null ? null : new[] { effect }, transitions);

        private sealed class Fixture
        {
            public CharacterRegistry Registry;
            public Character PlayerA;
            public Character PlayerB;
            public List<Character> Ai;
            public HealthAndFamilyService Health;
            public EventService Events;

            public static Fixture Create(long seed, EventDefinition definition, bool reverseTargets = false,
                int noEventWeight = 0, int historyLimit = EventService.DefaultHistoryLimit, int aiCount = 2) =>
                Create(seed, new EventCatalog(new[] { definition }), reverseTargets, noEventWeight, historyLimit, aiCount);

            public static Fixture Create(long seed, EventCatalog catalog, bool reverseTargets = false,
                int noEventWeight = 0, int historyLimit = EventService.DefaultHistoryLimit, int aiCount = 2)
            {
                var value = new Fixture { Registry = new CharacterRegistry(), Ai = new List<Character>() };
                value.PlayerA = Person(1, "Aren", CharacterSex.Male);
                value.PlayerB = Person(2, "Mira", CharacterSex.Female);
                value.Registry.Add(value.PlayerA); value.Registry.Add(value.PlayerB);
                value.Registry.LinkSpouses(value.PlayerA.Id, value.PlayerB.Id);
                for (var i = 0; i < aiCount; i++)
                {
                    var person = Person(100 + i, "Remote" + i, i % 2 == 0 ? CharacterSex.Male : CharacterSex.Female);
                    value.Registry.Add(person); value.Ai.Add(person);
                }
                value.Health = new HealthAndFamilyService(value.Registry, HealthConditionCatalog.CreateStoneAge(), seed);
                var playerIds = new[] { value.PlayerA.Id, value.PlayerB.Id };
                Func<IEnumerable<StableEntityId>> aiIds = () => reverseTargets
                    ? value.Ai.Select(x => x.Id).Reverse() : value.Ai.Select(x => x.Id);
                var source = new CharacterEventTargetSource(value.Registry, () => playerIds, aiIds,
                    () => Character.BiologicalTicksPerYear * 20L);
                value.Events = new EventService(catalog, source,
                    new CanonicalEventEffectExecutor(value.Registry, value.Health), seed, 0,
                    noEventWeight, historyLimit);
                return value;
            }
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly SaveSnapshot snapshot;
            public MemoryStore(SaveSnapshot value) { snapshot = value; }
            public void Save(StableEntityId slotId, SaveSnapshot value) => throw new NotSupportedException();
            public SaveSnapshot Load(StableEntityId slotId) => snapshot;
        }

        private static Character Person(int id, string name, CharacterSex sex) => Character.Restore(new CharacterState(
            Id(id), new CharacterName(name), sex, 0, CharacterLifeState.Alive, null, Array.Empty<ParentLink>(), null,
            null, null, null, new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
