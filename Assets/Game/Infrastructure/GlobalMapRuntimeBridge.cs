using System;
using System.Collections;
using Game.Domain.World;
using Game.Presentation.World;
using Game.Presentation.Localization;
using Game.Simulation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure
{
    /// <summary>Scene-lifetime adapter. The canonical session remains in LocalSceneCompositionRoot.</summary>
    public sealed class GlobalMapRuntimeBridge : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private Camera localCamera;
        private AudioListener localAudioListener;
        private bool busy;
        private bool developerUiVisible;
        public bool IsGlobalSceneLoaded => SceneManager.GetSceneByName("GlobalMap").isLoaded;

        public void Initialize(LocalSceneCompositionRoot compositionRoot)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            localCamera = root.PointerRaycaster.WorldCamera;
            localAudioListener = localCamera != null ? localCamera.GetComponent<AudioListener>() : null;
        }

        public void RequestEnterGlobal()
        {
            if (!busy) StartCoroutine(EnterGlobal());
        }

        public void SetDeveloperUiVisible(bool visible)
        {
            developerUiVisible = visible;
            var controller = FindGlobalController();
            if (controller != null) controller.ShowDeveloperUi = visible;
        }

        public void RequestEnterLocation(WorldLocationId locationId)
        {
            if (busy) return;
            var result = root.ModeTransitions.EnterLocation(locationId, root.Clock.State.CalendarTicks);
            if (result.Changed)
            {
                root.ApplyLocalLocationScope(root.GlobalMap.ActiveLocationId);
                StartCoroutine(ReturnToLocal());
            }
        }

        public void RequestEnterBattle(Game.Domain.StableEntityId battleId)
        {
            if (busy) return;
            var result = root.ModeTransitions.EnterBattle(battleId, root.Clock.State.CalendarTicks);
            if (result.Changed) StartCoroutine(ReturnToLocal());
        }

        public void ReconcileAfterLoad()
        {
            if (busy || root == null || root.GlobalMap == null) return;
            if (root.GlobalMap.Mode == GameWorldMode.Global && !IsGlobalSceneLoaded)
                StartCoroutine(LoadGlobalPresentation());
            else if (root.GlobalMap.Mode == GameWorldMode.Local && IsGlobalSceneLoaded)
                StartCoroutine(ReturnToLocal());
        }

        private void Update()
        {
            // SaveCoordinator may restore Global mode directly. Rebind the disposable scene lazily.
            if (root != null && root.GlobalMap != null && root.GlobalMap.Mode == GameWorldMode.Global &&
                !busy && !IsGlobalSceneLoaded)
                StartCoroutine(LoadGlobalPresentation());
        }

        private IEnumerator EnterGlobal()
        {
            busy = true;
            root.SynchronizeStarterSettlement();
            var result = root.ModeTransitions.EnterGlobal(root.Clock.State.CalendarTicks);
            if (!result.Changed) { busy = false; yield break; }
            yield return LoadGlobalPresentation();
        }

        private IEnumerator LoadGlobalPresentation()
        {
            busy = true;
            // Disable the persistent local listener before the additive scene activates its own.
            // Waiting until after LoadSceneAsync completes produces one frame with two listeners.
            if (localAudioListener != null) localAudioListener.enabled = false;
            if (!IsGlobalSceneLoaded) yield return SceneManager.LoadSceneAsync("GlobalMap", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("GlobalMap");
            GlobalMapPresentationController controller = null;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                controller = sceneRoot.GetComponentInChildren<GlobalMapPresentationController>(true);
                if (controller != null) break;
            }
            if (controller == null) throw new InvalidOperationException("GlobalMap scene has no presentation controller.");
            if (localCamera != null) localCamera.enabled = false;
            root.CameraController.enabled = false;
            root.Input.enabled = false;
            controller.Initialize(root.GlobalMap, root.MilitaryUnits, root.Battles, root.ModeTransitions,
                root.Production, root.DirectWorkCommands, () => root.Clock.State.CalendarTicks, _ =>
                {
                    root.ApplyLocalLocationScope(root.GlobalMap.ActiveLocationId);
                    StartCoroutine(ReturnToLocal());
                }, root.Weather, root.NaturalAtmosphereProfile);
            controller.ShowDeveloperUi = developerUiVisible;
            busy = false;
        }

        private IEnumerator ReturnToLocal()
        {
            if (busy) yield break;
            busy = true;
            if (IsGlobalSceneLoaded) yield return SceneManager.UnloadSceneAsync("GlobalMap");
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
            if (localCamera != null) localCamera.enabled = true;
            if (localAudioListener != null) localAudioListener.enabled = true;
            root.CameraController.enabled = true;
            root.Input.enabled = true;
            busy = false;
        }

        private void OnGUI()
        {
            if (!developerUiVisible || root == null || root.GlobalMap == null ||
                root.GlobalMap.Mode != GameWorldMode.Local || busy) return;
            if (GUI.Button(new Rect(Screen.width - 190, 12, 178, 34), RussianTextCatalog.Get("debug.open_global_map")))
                RequestEnterGlobal();
        }

        private GlobalMapPresentationController FindGlobalController()
        {
            if (!IsGlobalSceneLoaded) return null;
            var scene = SceneManager.GetSceneByName("GlobalMap");
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                var controller = sceneRoot.GetComponentInChildren<GlobalMapPresentationController>(true);
                if (controller != null) return controller;
            }
            return null;
        }
    }
}
