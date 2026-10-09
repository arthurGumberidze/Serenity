using System;
using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U24")]
    public sealed class BuildingDestructionFireCapturePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneComposesCentralServiceOverlayAndUiBlocker()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.BuildingDamage, Is.Not.Null);
            Assert.That(root.BuildingDamageDebug, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<BuildingDamageDebugOverlay>().Length, Is.EqualTo(1));
            Assert.That(root.Buildings.All.All(x => x.MaxIntegrity > 0), Is.True);
            Assert.That(root.BuildingDamageDebug.enabled, Is.False, "U27 hides legacy debug panels by default.");
            Assert.That(root.GameplayUi, Is.Not.Null);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(new Vector2(20f, Screen.height - 20f)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageAndFireRefreshTheExistingPresenter()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var building = root.DemoStorageBuilding;
            var presenter = root.DemoStoragePresenter;
            var id = building.Id;
            root.BuildingDamage.ApplyDamage(id, StableEntityId.NewId(), 100, BuildingDamageType.Generic,
                root.Clock.State.CalendarTicks);
            Assert.That(presenter.BuildingId, Is.EqualTo(id));
            Assert.That(building.StructuralStatus, Is.EqualTo(BuildingStructuralStatus.Damaged));
            Assert.That(root.BuildingDamage.IgniteBuilding(id, 800, root.Clock.State.CalendarTicks).Succeeded, Is.True);
            yield return null;
            Assert.That(presenter.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1));
            Assert.That(presenter.Building, Is.SameAs(building));
        }

        [UnityTest]
        public IEnumerator DestroyedStorageSpillsAndPhysicalAbstractHooksConverge()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var storage = root.DemoStorageBuilding;
            var inventory = root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storage.Id));
            var battleId = StableEntityId.NewId();
            var physical = root.BuildingDamage.ApplyCombatDamage(storage.Id, battleId, 1, 50,
                BattleMode.Physical, root.Clock.State.CalendarTicks);
            var abstracted = root.BuildingDamage.ApplyCombatDamage(storage.Id, battleId, 2, 50,
                BattleMode.Abstract, root.Clock.State.CalendarTicks);
            Assert.That(physical.Applied, Is.EqualTo(abstracted.Applied));
            Assert.That(inventory.TryAdd(new ResourceId("wood_log"), new ResourceQuantity(1)), Is.True);
            var before = root.Inventories.All.Sum(x => x.TotalUnits);
            root.BuildingDamage.ApplyDamage(storage.Id, StableEntityId.NewId(), storage.CurrentIntegrity,
                BuildingDamageType.Generic, root.Clock.State.CalendarTicks);
            yield return null;
            Assert.That(storage.IsDestroyed, Is.True);
            Assert.That(inventory.TotalUnits, Is.Zero);
            Assert.That(root.Inventories.All.Sum(x => x.TotalUnits), Is.EqualTo(before));
            Assert.That(root.WorldPiles.All.Any(x => x.Coordinate == storage.Coordinate), Is.True);

            Assert.That(root.Production.IsSiteOperational(storage.Id), Is.False);
        }
    }
}
