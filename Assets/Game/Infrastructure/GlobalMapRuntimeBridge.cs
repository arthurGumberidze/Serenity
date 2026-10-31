using System;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Presentation.Localization;
using Game.Presentation.World;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>P10 adapter for a same-scene strategic camera over the one canonical terrain.</summary>
    public sealed class GlobalMapRuntimeBridge : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private P102StrategicWorldView strategicView;
        private bool developerUiVisible;
        private Vector3 savedCameraFocus;
        private Quaternion savedCameraRotation;
        private float savedCameraZoom;
        private bool hasSavedCameraView;
        public bool IsGlobalSceneLoaded => strategicView != null && strategicView.IsActive;

        public void Initialize(LocalSceneCompositionRoot compositionRoot)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            var world = FindAnyObjectByType<P102ConnectedWorldPresentation>();
            strategicView = gameObject.AddComponent<P102StrategicWorldView>();
            if (root.TiledWorld != null)
                strategicView.Initialize(root.PointerRaycaster.WorldCamera, root.WorldDefinition,
                    root.ConnectedWorld, root.GlobalMap, root.Production, root.TiledWorld.SetStrategicMode,
                    root.TiledWorld.StrategicGroundTexture);
            else
            {
                if (world == null) throw new InvalidOperationException("P10.2 connected-world presentation is missing.");
                strategicView.Initialize(root.PointerRaycaster.WorldCamera, world, root.ConnectedWorld, root.Production);
            }
            strategicView.ConfigureResourceCommands(root.DirectWorkCommands);
            strategicView.SetLegacyControlsVisible(false);
        }

        public void RequestEnterGlobal()
        {
            root.SynchronizeStarterSettlement();
            var result = root.ModeTransitions.EnterGlobal(root.Clock.State.CalendarTicks);
            if (result.Changed || root.GlobalMap.Mode == GameWorldMode.Global) EnterStrategicView();
        }

        public void SetDeveloperUiVisible(bool visible) => developerUiVisible = visible;

        public void RequestEnterLocation(WorldLocationId locationId)
        {
            var result = root.ModeTransitions.EnterLocation(locationId, root.Clock.State.CalendarTicks);
            if (result.Changed)
            {
                ExitStrategicView();
                root.ApplyLocalLocationScope(locationId);
            }
        }

        public void RequestEnterBattle(Game.Domain.StableEntityId encounterId)
        {
            var result = root.ModeTransitions.EnterBattle(encounterId, root.Clock.State.CalendarTicks);
            if (result.Changed) ExitStrategicView();
        }

        public void ReconcileAfterLoad()
        {
            if (root.GlobalMap.Mode == GameWorldMode.Global) EnterStrategicView();
            else ExitStrategicView();
        }

        private void EnterStrategicView()
        {
            if (!strategicView.IsActive)
            {
                savedCameraFocus = root.CameraController.transform.position;
                savedCameraRotation = root.CameraController.transform.rotation;
                savedCameraZoom = root.CameraController.CurrentZoom;
                hasSavedCameraView = true;
            }
            root.CameraController.enabled = false;
            root.Input.enabled = false;
            strategicView.Enter();
        }

        private void ExitStrategicView()
        {
            strategicView.Exit();
            if (hasSavedCameraView)
            {
                root.CameraController.RestoreView(savedCameraFocus, savedCameraRotation, savedCameraZoom);
                hasSavedCameraView = false;
            }
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
            root.CameraController.enabled = true;
            root.Input.enabled = true;
        }

        private void OnGUI()
        {
            if (!developerUiVisible || root == null || root.GlobalMap == null ||
                root.GlobalMap.Mode != GameWorldMode.Local) return;
            if (GUI.Button(new Rect(Screen.width - 190, 12, 178, 34), RussianTextCatalog.Get("debug.open_global_map")))
                RequestEnterGlobal();
        }
    }
}
