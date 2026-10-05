using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Time;
using Game.ECS.Tier2;
using Game.Simulation.Time;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode
{
    [TestFixture]
    [Category("U12")]
    public class Tier2EcsPlayModeTests
    {
        [Test]
        public void ThousandEntitySteadyUpdateRecordsMeasuredRuntimeEvidence()
        {
            const int entityCount = 1000;
            const int measuredSteps = 120;
            var states = CreateTransfers(entityCount);
            var gameObjectsBefore = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;

            using (var runtime = new Tier2Runtime("U12 performance bootstrap"))
            {
                runtime.Materializer.Materialize(states);
                var delta = new GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
                runtime.Step(delta); // warm source generation, Burst and job scheduling paths

                var stopwatch = new Stopwatch();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                stopwatch.Start();
                for (var i = 0; i < measuredSteps; i++) runtime.Step(delta);
                stopwatch.Stop();
                var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                var gameObjectsAfter = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
                var burstAnnotated = Attribute.IsDefined(typeof(Tier2AgentSimulationJob), typeof(BurstCompileAttribute));
                var jobBacked = typeof(Tier2AgentSimulationJob).GetInterfaces().Contains(typeof(IJobEntity));

                Assert.That(runtime.Materializer.EntityCount, Is.EqualTo(entityCount));
                Assert.That(runtime.LastScheduledEntityCount, Is.EqualTo(entityCount));
                Assert.That(gameObjectsAfter - gameObjectsBefore, Is.Zero);
                Assert.That(burstAnnotated, Is.True);
                Assert.That(jobBacked, Is.True);
                Assert.That(allocatedBytes, Is.LessThanOrEqualTo(65536), "Steady updates must not create runaway managed allocations.");
                Assert.That(runtime.Materializer.Extract(states[0].CharacterId).StepCount,
                    Is.EqualTo(measuredSteps + 1));

                Debug.Log($"[U12_METRICS] entities={entityCount} measuredSteps={measuredSteps} " +
                          $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} allocatedBytes={allocatedBytes} " +
                          $"tier2GameObjectsAdded={gameObjectsAfter - gameObjectsBefore} " +
                          $"burstEnabled={BurstCompiler.IsEnabled} burstAnnotated={burstAnnotated} jobBacked={jobBacked}");
            }
        }

        [Test]
        public void PausedClockStopsTier2InsidePlayMode()
        {
            var clock = new GameClock(new GameTimeState(0, 0, (int)GameSpeed.Normal, true));
            var state = CreateTransfer(1);
            using (var runtime = new Tier2Runtime("U12 pause bootstrap"))
            {
                runtime.Materializer.Materialize(state);
                runtime.Step(clock.Advance(TimeSpan.FromSeconds(1)));
                Assert.That(runtime.Materializer.Extract(state.CharacterId).StepCount, Is.Zero);
                Assert.That(runtime.LastScheduledEntityCount, Is.Zero);
            }
        }

        private static List<Tier2TransferState> CreateTransfers(int count)
        {
            var result = new List<Tier2TransferState>(count);
            for (var i = 1; i <= count; i++) result.Add(CreateTransfer(i));
            return result;
        }

        private static Tier2TransferState CreateTransfer(int value)
        {
            return new Tier2TransferState(StableEntityId.Parse(value.ToString("x32")),
                value % 2 == 0 ? CharacterSex.Female : CharacterSex.Male, value,
                CharacterLifeState.Alive, null, new float3(value, 0, value % 31),
                new float3(1, 0, 0), true);
        }
    }
}
