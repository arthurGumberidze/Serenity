using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Research;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.World;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U25")]
    public sealed class GlobalMapPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGlobalLocalPreservesCanonicalIdentityResourcesAndBuildings()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var characterIds = root.Characters.GetAll().Select(x => x.Id).ToArray();
            var characterRefs = characterIds.Select(root.Characters.Get).ToArray();
            var buildingIds = root.Buildings.All.Select(x => x.Id).OrderBy(x => x).ToArray();
            var inventory = root.Inventories.CaptureStates();
            var dynasty = root.Dynasty.Id;
            var researchHash = root.Research.ComputeStateHash();
            var healthHash = root.HealthFamily.ComputeStateHash();
            root.SynchronizeStarterSettlement();

            Assert.That(root.ModeTransitions.EnterGlobal(root.Clock.State.CalendarTicks).Changed, Is.True);
            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Global));
            Assert.That(root.ModeTransitions.EnterLocation(new WorldLocationId("starter_settlement"),
                root.Clock.State.CalendarTicks).Changed, Is.True);
            yield return null;

            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Local));
            Assert.That(root.Characters.GetAll().Select(x => x.Id), Is.EqualTo(characterIds));
            for (var i = 0; i < characterIds.Length; i++)
                Assert.That(root.Characters.Get(characterIds[i]), Is.SameAs(characterRefs[i]));
            Assert.That(root.Buildings.All.Select(x => x.Id).OrderBy(x => x), Is.EqualTo(buildingIds));
            Assert.That(root.Inventories.CaptureStates().SelectMany(x => x.Amounts).Sum(x => x.Quantity.Units),
                Is.EqualTo(inventory.SelectMany(x => x.Amounts).Sum(x => x.Quantity.Units)));
            Assert.That(root.Dynasty.Id, Is.EqualTo(dynasty));
            Assert.That(root.Research.ComputeStateHash(), Is.EqualTo(researchHash));
            Assert.That(root.HealthFamily.ComputeStateHash(), Is.EqualTo(healthHash));
        }

        [UnityTest]
        public IEnumerator SameWorldStrategicViewShowsLocationsUnitTravelAndSettlementReturn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var unit = root.MilitaryUnits.Create(new MilitaryUnitId(Id(750)), new MilitarySideId("player"),
                new[] { root.MaleDemoCharacter.Id, root.FemaleDemoCharacter.Id }, root.MaleDemoCharacter.Id,
                MilitaryUnitRepresentationMode.Detailed, "starter_settlement");
            root.GlobalMapBridge.RequestEnterGlobal();
            for (var frame = 0; frame < 120 && !root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            var controller = Object.FindAnyObjectByType<Game.Presentation.Environment.P102ConnectedWorldPresentation>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Districts.Count, Is.EqualTo(root.GlobalMap.Locations.Count));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(root.MilitaryUnits.Get(unit.Id).Representation,
                Is.EqualTo(MilitaryUnitRepresentationMode.Aggregate));
            var order = root.GlobalMap.IssueTravelOrder(unit.Id, new WorldLocationId("river_ford"),
                root.Clock.State.CalendarTicks);
            Assert.That(order.Issued, Is.True);
            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.AdvanceForDebug(System.TimeSpan.FromSeconds(50));
            Assert.That(root.MilitaryUnits.Get(unit.Id).AbstractLocationId, Is.EqualTo("river_ford"));
            Assert.That(root.ModeTransitions.EnterLocation(new WorldLocationId("starter_settlement"),
                root.Clock.State.CalendarTicks).Changed, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalModeAdvancesResearchFireProductionWeatherAndAbstractBattle()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Research.AssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(root.Research.AvailableTechnologies.First().Id), Is.True);
            Assert.That(root.BuildingDamage.IgniteBuilding(root.DemoStorageBuilding.Id, 900,
                root.Clock.State.CalendarTicks).Status, Is.EqualTo(BuildingFireStatus.Burning));
            var battle = root.StartDemoUnitBattle(BattleMode.Physical);
            var productionBefore = root.Production.CaptureState().LastCalendarTick;
            var researchBefore = root.Research.ActiveProject.AccumulatedWorkTicks;
            var integrityBefore = root.DemoStorageBuilding.CurrentIntegrity;
            var combatBefore = root.Combat.ComputeStateHash();
            Assert.That(root.ModeTransitions.EnterGlobal(root.Clock.State.CalendarTicks).Changed, Is.True);
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).Mode,
                Is.EqualTo(BattleMode.Abstract));

            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.AdvanceForDebug(System.TimeSpan.FromSeconds(7));
            yield return null;

            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Global));
            Assert.That(root.Production.CaptureState().LastCalendarTick, Is.GreaterThan(productionBefore));
            Assert.That(root.Research.ActiveProject == null ||
                root.Research.ActiveProject.AccumulatedWorkTicks > researchBefore, Is.True);
            Assert.That(root.DemoStorageBuilding.CurrentIntegrity, Is.LessThan(integrityBefore));
            Assert.That(root.Combat.ComputeStateHash(), Is.Not.EqualTo(combatBefore));
            Assert.DoesNotThrow(() => root.Weather.GetWeatherAtCalendarTick(root.Clock.State.CalendarTicks));
        }

        private static Game.Domain.StableEntityId Id(int value) =>
            Game.Domain.StableEntityId.Parse(value.ToString("x32"));
    }
}
