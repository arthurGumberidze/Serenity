using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.Editor
{
    public sealed class NeighbourProgressionRepairTests
    {
        [Serializable]
        private sealed class AuditEntry
        {
            public long gameTick;
            public long planSequence;
            public string faction;
            public string resident;
            public string kind;
            public string detail;
        }

        private string previousSettingsPath;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", previousSettingsPath);
            previousSettingsPath = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator NeighbourCooksResearchesAndBuildsDuringFourteenGameDays()
        {
            var evidence = Path.GetFullPath("Artifacts/NeighbourProgressionRepair/fourteen-days");
            Directory.CreateDirectory(evidence);
            var settingsPath = Path.Combine(evidence, "user-settings-" + Guid.NewGuid().ToString("N") + ".json");
            new UserSettingsStore(settingsPath).Save(new UserSettingsData { rulerAiMode = RulerAiModes.Deterministic });
            previousSettingsPath = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", settingsPath);
            NewGameLaunchState.Request(20261006L);
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            LocalSceneCompositionRoot root;
            while ((root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>()) == null ||
                   !root.IsInitialized)
                yield return null;

            var faction = root.StarterEnemyAi;
            var initialBuildings = root.Buildings.All.Count(value =>
                value.OwnerSideId == faction.Strategic.SideId);
            var initialOperational = root.Buildings.All.Count(value =>
                value.OwnerSideId == faction.Strategic.SideId && value.IsOperational);

            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.SetPaused(false);
            for (var step = 0; step < 14 * 24; step++)
            {
                // Six real seconds at x10 and the 60:1 calendar scale is exactly one game hour.
                // Keeping the step below the two-hour movement timeout exercises physical work faithfully.
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(6));
                yield return null;
            }
            root.AiRuntime.SetPaused(true);

            var cookedBerries = faction.Production.GetProducedTotal(new ResourceId("cooked_berries"));
            var cookDiagnostics = string.Join(" | ", faction.Production.Sites
                .Where(value => value.RecipeId.ToString().StartsWith("cook_", StringComparison.Ordinal))
                .Select(value => value.RecipeId + ": available=" +
                    faction.Production.IsRecipeAvailable(value.RecipeId) + ", wait=" +
                    faction.Production.GetWaitingReason(value) + ", inputBerries=" +
                    faction.Inventories.Get(faction.Production.InputOwner(value.OwnerBuildingId))
                        .GetAmount(new ResourceId("berries")).Units + ", outputCookedBerries=" +
                    faction.Inventories.Get(faction.Production.OutputOwner(value.OwnerBuildingId))
                        .GetAmount(new ResourceId("cooked_berries")).Units));
            var berryInventories = string.Join(",", faction.Inventories.All
                .Where(value => value.GetAmount(new ResourceId("berries")).IsPositive)
                .Select(value => value.Owner.Kind + ":" + value.Owner.Id + "=" +
                    value.GetAmount(new ResourceId("berries")).Units + ":locked=" + value.IssuanceLocked));
            var foodHaulJobs = root.Work.Jobs
                .Where(value => value.ResourceId == new ResourceId("berries") ||
                    value.ResourceId == new ResourceId("wood_log")).ToArray();
            var foodHauls = string.Join(" | ", foodHaulJobs
                .GroupBy(value => value.Status + ":" + value.ResourceId + ":" + value.Target.Kind + "->" +
                    (value.Destination.HasValue ? value.Destination.Value.Kind.ToString() : "none"))
                .Select(value => value.Key + "=" + value.Count()));
            var inputContents = string.Join(",", faction.Production.Sites
                .Where(value => value.RecipeId.ToString() == "cook_berries")
                .SelectMany(value => faction.Inventories.Get(faction.Production.InputOwner(value.OwnerBuildingId))
                    .CaptureState().Amounts)
                .Select(value => value.ResourceId + "=" + value.Quantity.Units));
            var rejectedStorageJob = foodHaulJobs.LastOrDefault(value => value.Status.ToString() == "Failed" &&
                value.Target.Kind == Game.Domain.Work.WorkTargetKind.BuildingStorage && value.Destination.HasValue &&
                value.Destination.Value.Kind == Game.Domain.Work.WorkTargetKind.ProductionInput);
            var haulAvailability = rejectedStorageJob == null ? "no-storage-job" : string.Join(",",
                faction.Residents.Select(resident =>
                {
                    if (!root.AiAgents.TryGet(resident, out var agent)) return resident + "=no-agent";
                    return resident + "=" + root.NeighbourEconomy.HaulJobs.DiagnoseSpecified(agent,
                        new Game.Domain.Resources.InventoryOwner(Game.Domain.Resources.InventoryOwnerKind.BuildingStorage,
                            rejectedStorageJob.Target.EntityId),
                        new Game.Domain.Resources.InventoryOwner(Game.Domain.Resources.InventoryOwnerKind.ProductionInput,
                            rejectedStorageJob.Destination.Value.EntityId), rejectedStorageJob.ResourceId.Value,
                        rejectedStorageJob.Quantity);
                }));
            var haulRejections=string.Join(",",root.NeighbourEconomy.HaulJobs.SpecifiedRejections
                .OrderBy(value=>value.Key).Select(value=>value.Key+"="+value.Value));
            var haulExecutionFailures=string.Join(",",root.NeighbourEconomy.HaulExecutionFailures
                .OrderBy(value=>value.Key).Select(value=>value.Key+"="+value.Value));
            Assert.That(cookedBerries, Is.GreaterThan(0),
                "The neighbour must deliver raw berries to its campfire and cook food. " +
                "controlledFire=" + faction.Research.IsCompleted(new TechnologyId("controlled_fire")) +
                "; population=" + faction.Production.Population + "; prepared=" +
                faction.Production.PreparedFoodTotal + "/" + faction.Production.PreparedFoodTarget +
                "; sites=" + cookDiagnostics + "; berryInventories=" + berryInventories +
                "; inputContents=" + inputContents + "; foodHauls=" + foodHauls +
                "; haulAvailability=" + haulAvailability + "; haulRejections=" + haulRejections +
                "; haulExecutionFailures=" + haulExecutionFailures);
            Assert.That(faction.Research.IsCompleted(new TechnologyId("shelter_construction")), Is.True,
                "Food logistics must not let critical hunger permanently interrupt research.");
            Assert.That(root.Buildings.All.Count(value => value.OwnerSideId == faction.Strategic.SideId),
                Is.GreaterThan(initialBuildings), "The neighbour must place a new canonical building.");
            Assert.That(root.Buildings.All.Count(value =>
                    value.OwnerSideId == faction.Strategic.SideId && value.IsOperational),
                Is.GreaterThan(initialOperational), "Residents must deliver materials and finish the building.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ThreeAcceleratedMonthsKeepWorkRegistriesAndStepCostBounded()
        {
            var evidence = Path.GetFullPath("Artifacts/NeighbourAiOptimization/three-months");
            Directory.CreateDirectory(evidence);
            var settingsPath = Path.Combine(evidence, "user-settings-" + Guid.NewGuid().ToString("N") + ".json");
            new UserSettingsStore(settingsPath).Save(new UserSettingsData { rulerAiMode = RulerAiModes.Deterministic });
            previousSettingsPath = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", settingsPath);
            NewGameLaunchState.Request(20261031L);
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            LocalSceneCompositionRoot root;
            while ((root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>()) == null ||
                   !root.IsInitialized)
                yield return null;

            var faction = root.StarterEnemyAi;
            var monthlyMilliseconds = new List<double>();
            var registryCounts = new List<string>();
            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.SetPaused(false);
            for (var month = 1; month <= 3; month++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                for (var day = 0; day < 30; day++)
                {
                    root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(72));
                    root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(72));
                    yield return null;
                }
                stopwatch.Stop();
                monthlyMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                var openJobs = root.Work.OpenJobs.Count();
                registryCounts.Add("month=" + month + "; jobs=" + root.Work.JobCount +
                    "; open=" + openJobs + "; productionTasks=" + faction.Production.TrackedTaskCount);
                Assert.That(root.Work.JobCount, Is.LessThanOrEqualTo(openJobs + Game.Simulation.Work.WorkManager.TerminalHistoryLimit));
                Assert.That(faction.Production.TrackedTaskCount, Is.LessThanOrEqualTo(faction.Residents.Count));
            }
            root.AiRuntime.SetPaused(true);

            File.WriteAllLines(Path.Combine(evidence, "summary.txt"), registryCounts.Concat(new[]
            {
                "monthlyMilliseconds=" + string.Join(",", monthlyMilliseconds.Select(value => value.ToString("0.0")))
            }));
            TestContext.WriteLine(string.Join(" | ", registryCounts) + " | monthlyMs=" +
                string.Join(",", monthlyMilliseconds.Select(value => value.ToString("0.0"))));
        }

        [UnityTest, Timeout(7200000)]
        public IEnumerator OllamaMonthlyPlansGuideBusyResidentsForTwoGameYears()
        {
            var evidence = Path.GetFullPath("Artifacts/NeighbourProgressionRepair/ollama-two-years");
            Directory.CreateDirectory(evidence);
            // A persistent Editor may still have a FileSystemWatcher/reader on the prior run's settings file.
            // Each endurance attempt therefore gets an immutable input instead of replacing a live file.
            var settingsPath = Path.Combine(evidence, "user-settings-" + Guid.NewGuid().ToString("N") + ".json");
            new UserSettingsStore(settingsPath).Save(new UserSettingsData
            {
                rulerAiMode = RulerAiModes.Ollama,
                rulerAiIntervalMonths = 1
            });
            previousSettingsPath = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", settingsPath);

            yield return new EnterPlayMode();
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            LocalSceneCompositionRoot root;
            while ((root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>()) == null ||
                   !root.IsInitialized)
                yield return null;

            Assert.That(root.RulerStrategies.Mode, Is.EqualTo(RulerAiModes.Ollama),
                "The endurance route must never select the cloud provider.");
            var faction = root.StarterEnemyAi;
            var initialBuildings = root.Buildings.All.Count(value =>
                value.OwnerSideId == faction.Strategic.SideId && value.IsOperational);
            var startTick = root.Clock.State.CalendarTicks;
            var endTick = checked(startTick + TimeSpan.FromDays(730).Ticks);
            var acceptedPlans = new List<RulerStrategyPlan>();
            var observedJournalCount = 0;

            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.SetPaused(false);
            while (root.Clock.State.CalendarTicks < endTick)
            {
                // Twelve-hour deltas exercise two canonical decisions per day while keeping the two-year
                // local-model endurance run practical. Arrived movement is handled before stall timeouts.
                // ActionChanged writes audit entries synchronously; a frame is still yielded daily for
                // strategic-decision collection and immediately when the local provider starts a request.
                for (var slice = 0; slice < 2 && root.Clock.State.CalendarTicks < endTick; slice++)
                {
                    root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(72));
                    if (root.RulerStrategies.IsRequestInFlight) break;
                }
                yield return null;
                if (!root.RulerStrategies.IsRequestInFlight) continue;

                // Do not let accelerated calendar time consume the month while the local CPU is composing it.
                root.AiRuntime.SetPaused(true);
                var timeout = Time.realtimeSinceStartup + 210f;
                while (root.RulerStrategies.IsRequestInFlight && Time.realtimeSinceStartup < timeout)
                    yield return null;
                Assert.That(root.RulerStrategies.IsRequestInFlight, Is.False,
                    "Ollama did not finish the monthly strategy within 210 real seconds.");
                root.AiRuntime.SetPaused(false);

                var newEntries = faction.RulerStrategy.Journal.Skip(observedJournalCount).ToArray();
                observedJournalCount = faction.RulerStrategy.Journal.Count;
                foreach (var entry in newEntries)
                {
                    Assert.That(entry.Provider, Is.EqualTo(RulerStrategyProviderKind.Ollama));
                    Assert.That(entry.Status, Is.EqualTo(RulerStrategyRequestStatus.Accepted), entry.Note);
                    var plan = faction.RulerStrategy.ActivePlan;
                    Assert.That(plan, Is.Not.Null);
                    Assert.That(plan.RequestSequence, Is.EqualTo(entry.RequestSequence));
                    Assert.That(plan.ValidUntilTick - entry.RequestedTick,
                        Is.EqualTo(TimeSpan.FromDays(30).Ticks),
                        "A monthly Ollama plan must expire at the next monthly review.");
                    acceptedPlans.Add(plan);
                }
            }
            root.AiRuntime.SetPaused(true);
            yield return new WaitForSecondsRealtime(1.1f);

            var expectedRequests = 25; // day 2, then every 30 days through day 722.
            Assert.That(acceptedPlans.Count, Is.EqualTo(expectedRequests));
            Assert.That(acceptedPlans.Select(value => value.RequestSequence).Distinct().Count(),
                Is.EqualTo(expectedRequests));
            Assert.That(faction.Production.GetProducedTotal(new ResourceId("cooked_berries")), Is.GreaterThan(0));

            var audit = UnityEngine.Object.FindAnyObjectByType<RulerActionAudit>();
            Assert.That(audit, Is.Not.Null);
            audit.FlushNow();
            var entries = File.ReadLines(audit.LogPath).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(JsonUtility.FromJson<AuditEntry>).ToArray();
            var accepted = entries.Where(value => value.kind == "accepted_plan").ToArray();
            Assert.That(accepted.Length, Is.EqualTo(expectedRequests));
            Assert.That(accepted.Select(value => value.planSequence),
                Is.EquivalentTo(acceptedPlans.Select(value => value.RequestSequence)));
            foreach (var sequence in acceptedPlans.Select(value => value.RequestSequence))
                Assert.That(entries.Any(value => value.kind == "strategic_execution" &&
                    value.planSequence == sequence), Is.True,
                    "No canonical strategic execution was correlated with Ollama plan " + sequence + ".");
            Assert.That(entries.Any(value => value.kind == "strategic_execution" &&
                (value.detail ?? string.Empty).StartsWith("ConstructBuilding", StringComparison.Ordinal)), Is.True,
                "The two-year strategy never issued a canonical construction directive. " +
                "Physical completion is covered by the one-hour fourteen-day progression test.");

            var productive = new[] { "Produce/", "Haul/", "Research/", "ManualHaul/", "ManualTask/" };
            foreach (var resident in faction.Residents)
                Assert.That(entries.Any(value => value.kind == "resident_action" &&
                    value.resident == resident.ToString() && productive.Any(prefix =>
                        (value.detail ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))), Is.True,
                    "Resident never performed productive work: " + resident);
            var residentActions = entries.Where(value => value.kind == "resident_action" &&
                value.faction == faction.Strategic.Id.ToString()).ToArray();
            var failedMeals = residentActions.Count(value => value.detail == "Eat/Failed");
            Assert.That(failedMeals, Is.LessThan(Math.Max(1, residentActions.Length / 4)),
                "Residents spent too much of the two-year run in the starvation failure loop.");

            File.WriteAllLines(Path.Combine(evidence, "summary.txt"), new[]
            {
                "audit=" + audit.LogPath,
                "startTick=" + startTick,
                "endTick=" + root.Clock.State.CalendarTicks,
                "acceptedOllamaPlans=" + acceptedPlans.Count,
                "operationalBuildings=" + initialBuildings + " -> " +
                    root.Buildings.All.Count(value => value.OwnerSideId == faction.Strategic.SideId && value.IsOperational),
                "cookedBerriesProduced=" + faction.Production.GetProducedTotal(new ResourceId("cooked_berries")),
                "residentActions=" + residentActions.Length,
                "failedMeals=" + failedMeals
            });
        }
    }
}
