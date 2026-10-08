using System;
using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.AI;
using Game.Simulation.Tiers;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U26")]
    public sealed class StrategicAiPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalRuntimeBootstrapsIndependentCanonicalStateAndDebugSurface()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var overlay = UnityEngine.Object.FindAnyObjectByType<StrategicAiDebugOverlay>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.StrategicAi, Is.Not.Null);
            Assert.That(root.StarterEnemyAi, Is.Not.Null);
            Assert.That(root.StrategicAi.Factions.Count, Is.EqualTo(1));
            Assert.That(root.StarterEnemyAi.Residents.Count, Is.EqualTo(6));
            Assert.That(root.StarterEnemyAi.Residents.All(id => root.Characters.TryGet(id, out _)), Is.True);
            Assert.That(root.StarterEnemyAi.Residents.All(id =>
                root.Tiers.GetTier(id) == CharacterSimulationTier.Tier3), Is.True);
            Assert.That(root.StarterEnemyAi.Inventories, Is.Not.SameAs(root.Inventories));
            Assert.That(root.StarterEnemyAi.Research, Is.Not.SameAs(root.Research));
            var enemy = root.GlobalMap.GetSettlementAt(new WorldLocationId("enemy_settlement"));
            var player = root.GlobalMap.GetSettlementAt(new WorldLocationId("starter_settlement"));
            Assert.That(enemy.ResidentCharacterIds, Is.EqualTo(root.StarterEnemyAi.Residents.OrderBy(x => x)));
            Assert.That(player.ResidentCharacterIds.Intersect(root.StarterEnemyAi.Residents), Is.Empty);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.TryGetUiBlockingRect(out var panel), Is.True);
            Assert.That(panel.width, Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalSimulationAdvanceAutomaticallyTicksStrategicScheduler()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var before = root.StarterEnemyAi.Strategic.DecisionSequence;
            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(50));
            yield return null;
            Assert.That(root.StarterEnemyAi.Strategic.DecisionSequence, Is.GreaterThan(before));
            Assert.That(root.StrategicAi.LastProcessedTick,
                Is.EqualTo(root.Clock.State.CalendarTicks));
            Assert.That(root.StarterEnemyAi.Strategic.RecentDecisions.Any(x =>
                x.Kind == AiDecisionKind.StartResearch), Is.True);
        }
    }
}
