using System;
using System.Collections;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Research;
using Game.Infrastructure;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U16")]
    public sealed class ResearchPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGameplayComposesDebugPanelLimitedKnowledgeAndTier1Research()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.Research, Is.Not.Null);
            Assert.That(root.ResearchDebug, Is.Not.Null);
            Assert.That(root.Research.CompletedTechnologies,
                Is.EquivalentTo(root.Research.Catalog.StartingKnowledge.Select(x => x.Id)));
            Assert.That(root.Research.IsBuildingUnlocked(new Game.Domain.Buildings.BuildingDefinitionId("primitive_crop_plot")), Is.False);
            Assert.That(root.Research.AssignedWorkers, Is.Empty);
            root.Research.AssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.ResearchDebug.StartSelected(), Is.True);
            root.AiRuntime.SetPaused(false);
            for (var i = 0; i < 6 && root.Research.ActiveProject != null &&
                root.Research.ActiveProject.AccumulatedWorkTicks == 0; i++)
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1));
            Assert.That(root.Research.ActiveProject == null || root.Research.ActiveProject.AccumulatedWorkTicks > 0, Is.True);
            Assert.That(root.AiRuntime.PerAgentAiUpdateCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tier2AndTier3CommitThroughOneResearchAuthorityWithoutGameObjects()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.Research.AssignWorkers(new[] { root.MaleDemoCharacter.Id, root.FemaleDemoCharacter.Id });
            var technology = new TechnologyId("controlled_fire");
            Assert.That(root.Research.Start(technology), Is.True);
            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(root.FemaleDemoCharacter.Id, CharacterSimulationTier.Tier3);
            var before = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            root.Clock.State.CalendarTicks = TimeSpan.FromDays(2).Ticks;
            Assert.That(root.Tier2Runtime.AdvanceResearch(root.Research, root.Clock.State.CalendarTicks), Is.EqualTo(1));
            root.Tier3Adapter.AdvanceResearchToCurrent(root.Research);
            var after = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            Assert.That(root.Research.IsCompleted(technology), Is.True);
            Assert.That(root.Research.CompletionCount, Is.EqualTo(1));
            Assert.That(after, Is.EqualTo(before));
            yield return null;
        }
    }
}
