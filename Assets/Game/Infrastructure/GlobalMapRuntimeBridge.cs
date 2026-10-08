using System;
using System.Collections;
using Game.Domain.World;
using Game.Presentation.World;
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
        private bool busy;
        public bool IsGlobalSceneLoaded => SceneManager.GetSceneByName("GlobalMap").isLoaded;

        public void Initialize(LocalSceneCompositionRoot compositionRoot)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            localCamera = root.PointerRaycaster.WorldCamera;
        }

        public void RequestEnterGlobal()
        {
            if (!busy) StartCoroutine(EnterGlobal());
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
                () => root.Clock.State.CalendarTicks, _ =>
                {
                    root.ApplyLocalLocationScope(root.GlobalMap.ActiveLocationId);
                    StartCoroutine(ReturnToLocal());
                });
            busy = false;
        }

        private IEnumerator ReturnToLocal()
        {
            if (busy) yield break;
            busy = true;
            if (localCamera != null) localCamera.enabled = true;
            root.CameraController.enabled = true;
            root.Input.enabled = true;
            if (IsGlobalSceneLoaded) yield return SceneManager.UnloadSceneAsync("GlobalMap");
            busy = false;
        }

        private void OnGUI()
        {
            if (root == null || root.GlobalMap == null || root.GlobalMap.Mode != GameWorldMode.Local || busy) return;
            if (GUI.Button(new Rect(Screen.width - 190, 12, 178, 34), "Open Global Map"))
                RequestEnterGlobal();
        }
    }
}
