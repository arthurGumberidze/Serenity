using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Time;
using Game.ECS.Tier2;
using Game.Simulation.Time;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests
{
    [TestFixture]
    [Category("U12")]
    public class Tier2EcsTests
    {
        [Test]
        public void ComponentsAreUnmanagedAndChunkFriendly()
        {
            Assert.That(UnsafeUtility.IsUnmanaged<Tier2StableIdentity>(), Is.True);
            Assert.That(UnsafeUtility.IsUnmanaged<Tier2BiologicalState>(), Is.True);
            Assert.That(UnsafeUtility.IsUnmanaged<Tier2Position>(), Is.True);
            Assert.That(UnsafeUtility.IsUnmanaged<Tier2Movement>(), Is.True);
            Assert.That(UnsafeUtility.IsUnmanaged<Tier2SimulationProgress>(), Is.True);
            Assert.That(UnsafeUtility.SizeOf<Tier2StableIdentity>(), Is.EqualTo(16));
            Assert.That(UnsafeUtility.SizeOf<Tier2BiologicalState>(), Is.LessThanOrEqualTo(24));
            Assert.That(UnsafeUtility.SizeOf<Tier2Position>(), Is.EqualTo(12));
            Assert.That(UnsafeUtility.SizeOf<Tier2Movement>(), Is.LessThanOrEqualTo(16));
            Assert.That(UnsafeUtility.SizeOf<Tier2SimulationProgress>(), Is.EqualTo(24));
            Debug.Log($"[U12_LAYOUT] identity={UnsafeUtility.SizeOf<Tier2StableIdentity>()} " +
                      $"biology={UnsafeUtility.SizeOf<Tier2BiologicalState>()} " +
                      $"position={UnsafeUtility.SizeOf<Tier2Position>()} movement={UnsafeUtility.SizeOf<Tier2Movement>()} " +
                      $"progress={UnsafeUtility.SizeOf<Tier2SimulationProgress>()} total=92");
        }

        [Test]
        public void MaterializeCharacterPreservesCanonicalStableIdentity()
        {
            var character = CreateCharacter();
            var transfer = Tier2TransferState.FromCharacter(character, new float3(2, 0, 3), new float3(4, 0, 0), true);
            using (var runtime = new Tier2Runtime())
            {
                var entity = runtime.Materializer.Materialize(transfer);
                var identity = runtime.World.EntityManager.GetComponentData<Tier2StableIdentity>(entity);
                Assert.That(identity.ToDomain(), Is.EqualTo(character.Id));
                Assert.That(runtime.Materializer.Extract(entity).CharacterId, Is.EqualTo(character.Id));
            }
        }

        [Test]
        public void DuplicateStableIdentityIsRejectedWithoutCreatingAnotherEntity()
        {
            var state = CreateTransfer(1);
            using (var runtime = new Tier2Runtime())
            {
                runtime.Materializer.Materialize(state);
                Assert.Throws<InvalidOperationException>(() => runtime.Materializer.Materialize(state));
                Assert.That(runtime.Materializer.EntityCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void BatchDuplicateIsRejectedAtomically()
        {
            var state = CreateTransfer(1);
            using (var runtime = new Tier2Runtime())
            {
                Assert.Throws<InvalidOperationException>(() =>
                    runtime.Materializer.Materialize(new[] { state, state }));
                Assert.That(runtime.Materializer.EntityCount, Is.Zero);
            }
        }

        [Test]
        public void OneThousandEntitiesMaterializeWithoutGameObjects()
        {
            var states = CreateTransfers(1000);
            var gameObjectsBefore = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            using (var runtime = new Tier2Runtime())
            {
                runtime.Materializer.Materialize(states);
                Assert.That(runtime.Materializer.EntityCount, Is.EqualTo(1000));
                Assert.That(UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(gameObjectsBefore));
            }
        }

        [Test]
        public void BatchedSystemUpdatesEveryEntity()
        {
            using (var runtime = new Tier2Runtime())
            {
                var states = CreateTransfers(1000);
                runtime.Materializer.Materialize(states);
                var delta = new GameTimeAdvance(TimeSpan.FromHours(12), TimeSpan.FromDays(200));
                runtime.Step(delta);
                Assert.That(runtime.LastScheduledEntityCount, Is.EqualTo(1000));
                for (var i = 0; i < states.Count; i++)
                {
                    var extracted = runtime.Materializer.Extract(states[i].CharacterId);
                    Assert.That(extracted.StepCount, Is.EqualTo(1));
                    Assert.That(extracted.ProcessedCalendarTicks, Is.EqualTo(delta.CalendarDelta.Ticks));
                    Assert.That(extracted.ProcessedBiologicalTicks, Is.EqualTo(delta.BiologicalDelta.Ticks));
                    Assert.That(extracted.Position.x, Is.EqualTo(states[i].Position.x + 0.5f).Within(0.00001f));
                }
            }
        }

        [Test]
        public void ActivePauseProducesACompleteTier2NoOp()
        {
            var clock = new GameClock(new GameTimeState(0, 0, (int)GameSpeed.Normal, true));
            using (var runtime = new Tier2Runtime())
            {
                var state = CreateTransfer(1);
                runtime.Materializer.Materialize(state);
                runtime.Step(clock.Advance(TimeSpan.FromSeconds(5)));
                var extracted = runtime.Materializer.Extract(state.CharacterId);
                Assert.That(runtime.LastScheduledEntityCount, Is.Zero);
                Assert.That(extracted.Position, Is.EqualTo(state.Position));
                Assert.That(extracted.StepCount, Is.Zero);
                Assert.That(extracted.ProcessedCalendarTicks, Is.Zero);
                Assert.That(extracted.ProcessedBiologicalTicks, Is.Zero);
            }
        }

        [Test]
        public void IdenticalExplicitStepsProduceIdenticalState()
        {
            var states = CreateTransfers(128);
            using (var first = new Tier2Runtime("determinism-a"))
            using (var second = new Tier2Runtime("determinism-b"))
            {
                first.Materializer.Materialize(states);
                second.Materializer.Materialize(states);
                for (var i = 0; i < 50; i++)
                {
                    var delta = new GameTimeAdvance(TimeSpan.FromMinutes((i % 5) + 1), TimeSpan.FromHours(i + 1));
                    first.Step(delta);
                    second.Step(delta);
                }
                for (var i = 0; i < states.Count; i++)
                {
                    var a = first.Materializer.Extract(states[i].CharacterId);
                    var b = second.Materializer.Extract(states[i].CharacterId);
                    Assert.That(a.Position, Is.EqualTo(b.Position));
                    Assert.That(a.ProcessedCalendarTicks, Is.EqualTo(b.ProcessedCalendarTicks));
                    Assert.That(a.ProcessedBiologicalTicks, Is.EqualTo(b.ProcessedBiologicalTicks));
                    Assert.That(a.StepCount, Is.EqualTo(b.StepCount));
                }
            }
        }

        [Test]
        public void ExtractionPreservesEverySupportedField()
        {
            var source = new Tier2TransferState(Id(9), CharacterSex.Female, 1234,
                CharacterLifeState.Dead, 5678, new float3(1, 2, 3), new float3(4, 5, 6), false, 77, 88, 8);
            using (var runtime = new Tier2Runtime())
            {
                var entity = runtime.Materializer.Materialize(source);
                var extracted = runtime.Materializer.Extract(entity);
                Assert.That(extracted.CharacterId, Is.EqualTo(source.CharacterId));
                Assert.That(extracted.Sex, Is.EqualTo(source.Sex));
                Assert.That(extracted.BiologicalBirthTick, Is.EqualTo(source.BiologicalBirthTick));
                Assert.That(extracted.LifeState, Is.EqualTo(source.LifeState));
                Assert.That(extracted.BiologicalDeathTick, Is.EqualTo(source.BiologicalDeathTick));
                Assert.That(extracted.Position, Is.EqualTo(source.Position));
                Assert.That(extracted.UnitsPerGameDay, Is.EqualTo(source.UnitsPerGameDay));
                Assert.That(extracted.IsMoving, Is.EqualTo(source.IsMoving));
                Assert.That(extracted.ProcessedCalendarTicks, Is.EqualTo(source.ProcessedCalendarTicks));
                Assert.That(extracted.ProcessedBiologicalTicks, Is.EqualTo(source.ProcessedBiologicalTicks));
                Assert.That(extracted.StepCount, Is.EqualTo(source.StepCount));
            }
        }

        [Test]
        public void TenThousandEntitiesCanBeUpdatedByOneJobBackedSystem()
        {
            using (var runtime = new Tier2Runtime())
            {
                var states = CreateTransfers(10000);
                runtime.Materializer.Materialize(states);
                runtime.Step(new GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1)));
                Assert.That(runtime.Materializer.EntityCount, Is.EqualTo(10000));
                Assert.That(runtime.LastScheduledEntityCount, Is.EqualTo(10000));
                Assert.That(runtime.Materializer.Extract(states[9999].CharacterId).StepCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void EcsRuntimeContainsNoPerEntityMonoBehaviour()
        {
            var monoBehaviours = typeof(Tier2Runtime).Assembly.GetTypes()
                .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type)).ToArray();
            Assert.That(monoBehaviours, Is.Empty);
            Assert.That(typeof(Tier2AgentSimulationJob).GetInterfaces(), Does.Contain(typeof(IJobEntity)));
        }

        private static Character CreateCharacter()
        {
            return Character.CreateNew(new CharacterCreationData(new CharacterName("Tier", "Two"), CharacterSex.Male,
                0, null, null, null, null, null,
                new[] { new TraitId("steady"), new TraitId("calm"), new TraitId("strong"), new TraitId("kind") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
        }

        internal static List<Tier2TransferState> CreateTransfers(int count)
        {
            var result = new List<Tier2TransferState>(count);
            for (var i = 1; i <= count; i++) result.Add(CreateTransfer(i));
            return result;
        }

        internal static Tier2TransferState CreateTransfer(int value)
        {
            return new Tier2TransferState(Id(value), value % 2 == 0 ? CharacterSex.Female : CharacterSex.Male,
                value, CharacterLifeState.Alive, null, new float3(value, 0, value % 31),
                new float3(1, 0, 0), true);
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
