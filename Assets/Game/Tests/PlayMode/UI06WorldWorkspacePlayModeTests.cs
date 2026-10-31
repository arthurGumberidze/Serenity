using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.UI;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("UI06")]
    public sealed class UI06WorldWorkspacePlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui06-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator LocalMiniMapRealmRouteAndReturnPreserveCanonicalIdentityAndCamera()
        {
            LocalSceneCompositionRoot root = null;
            var deadline = Time.realtimeSinceStartup + 600f;
            while (root == null && Time.realtimeSinceStartup < deadline)
            {
                root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null || root.GameplayUi == null) { root = null; yield return null; }
            }
            Assert.That(root, Is.Not.Null);
            while (Object.FindAnyObjectByType<WorldLoadingOverlay>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;

            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            ui.RefreshNow();
            yield return null;
            Assert.That(ui.RootVisual.Q("local-mini-map"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("local-mini-map-grid").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));

            var active = root.GlobalMap.GetLocation(root.GlobalMap.ActiveLocationId);
            var layout = root.ConnectedWorld.Layout;
            Assert.That(ui.FocusLocalMiniMap((active.MapX - layout.OriginX) / layout.WidthMeters,
                (active.MapY - layout.OriginZ) / layout.DepthMeters), Is.True);

            var selectedId = root.Dynasty.CurrentMonarchId.Value;
            root.SelectionProbe.SelectCharacterIds(new[] { selectedId });
            var localPosition = root.CameraController.transform.position;
            var localRotation = root.CameraController.transform.rotation;
            var localZoom = root.CameraController.CurrentZoom;

            root.ConnectedWorld.RevealAll();
            root.StartDemoUnitBattle(Game.Domain.Combat.BattleMode.Abstract);
            var unit = root.MilitaryUnits.Units.First(value => value.OwnerSideId.Value == "player");
            root.MilitaryUnits.SetAbstractLocation(unit.Id, root.GlobalMap.ActiveLocationId.Value);
            unit = root.MilitaryUnits.Get(unit.Id);
            if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                unit = root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            var origin = new WorldLocationId(unit.AbstractLocationId);
            var route = root.GlobalMap.GetRoutes(origin).First();
            var destination = route.From == origin ? route.To : route.From;

            ui.Navigate(GameplayUiRouteId.Realm);
            ui.SelectMapUnit(unit.Id);
            ui.SelectMapLocation(destination);
            ui.RefreshNow();
            yield return null;

            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Global));
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            Assert.That(ui.RootVisual.Q("realm-workspace").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ui.RootVisual.Q("realm-layers"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("realm-detail"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("realm-route-summary"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("realm-travel"), Is.Not.Null);
            Assert.That(ui.RootVisual.Query<Button>("diplomacy-unavailable").ToList(), Is.Not.Empty);
            Assert.That(ui.RootVisual.Query<Button>("diplomacy-unavailable").ToList().All(value => !value.enabledSelf), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterId, Is.EqualTo(selectedId));
            var strategicView = root.GetComponent<P102StrategicWorldView>();
            var overlay = ui.RootVisual.Q("realm-map-overlay");
            var expectedViewport = new Rect(
                overlay.worldBound.xMin / ui.RootVisual.resolvedStyle.width,
                1f - overlay.worldBound.yMax / ui.RootVisual.resolvedStyle.height,
                overlay.worldBound.width / ui.RootVisual.resolvedStyle.width,
                overlay.worldBound.height / ui.RootVisual.resolvedStyle.height);
            Assert.That(strategicView.CurrentViewport.x, Is.EqualTo(expectedViewport.x).Within(.01f));
            Assert.That(strategicView.CurrentViewport.y, Is.EqualTo(expectedViewport.y).Within(.01f));
            Assert.That(strategicView.CurrentViewport.width, Is.EqualTo(expectedViewport.width).Within(.01f));
            Assert.That(strategicView.CurrentViewport.height, Is.EqualTo(expectedViewport.height).Within(.01f));
            Assert.That(root.PointerRaycaster.WorldCamera.rect, Is.EqualTo(strategicView.CurrentViewport));

            ui.SetPanel(GameplayPanel.Global);
            ui.RefreshNow();
            yield return null;
            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Local));
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.SelectionProbe.SelectedCharacterId, Is.EqualTo(selectedId));
            Assert.That(Vector3.Distance(root.CameraController.transform.position, localPosition), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(root.CameraController.transform.rotation, localRotation), Is.LessThan(.01f));
            Assert.That(root.CameraController.CurrentZoom, Is.EqualTo(localZoom).Within(.01f));
        }
    }
}
