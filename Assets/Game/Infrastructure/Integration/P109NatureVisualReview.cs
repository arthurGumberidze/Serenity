using System;
using System.Linq;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Opt-in development camera bookmarks only. Does not automate orders, weather or saves.</summary>
    public sealed class P109NatureVisualReview : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-p109NatureReview")) return;
            var host = new GameObject("P10.9 visual review camera bookmarks");
            DontDestroyOnLoad(host);
            host.AddComponent<P109NatureVisualReview>();
        }

        private void OnGUI()
        {
            if (root == null) root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null || root.WorldHeight == null) return;
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 175f, Screen.height - 112f, 350f, 65f), GUI.skin.box);
            GUILayout.Label("QA camera bookmarks — not an input acceptance test");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Camp")) root.CameraController.FocusHome();
            if (GUILayout.Button("Lake")) root.CameraController.FocusPosition(new Vector3(105f, 0f, 95f), 100f);
            if (GUILayout.Button("Lake close")) root.CameraController.FocusPosition(new Vector3(105f, 0f, 95f), 45f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
