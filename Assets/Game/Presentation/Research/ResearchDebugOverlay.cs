using System;
using System.Linq;
using Game.Domain.Research;
using Game.Presentation.Interaction;
using Game.Simulation.Research;
using UnityEngine;

namespace Game.Presentation.Research
{
    public sealed class ResearchDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private static readonly Rect PanelRect = new Rect(450f, 242f, 390f, 232f);
        private ResearchService research;
        private WorldPointerRaycaster raycaster;
        private int selectedAvailableIndex;

        public void Initialize(ResearchService service, WorldPointerRaycaster worldRaycaster)
        {
            research = service ?? throw new ArgumentNullException(nameof(service));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = PanelRect;
            return research != null && (Application.isEditor || Debug.isDebugBuild);
        }

        public bool StartSelected()
        {
            var available = research.AvailableTechnologies;
            if (available.Count == 0) return false;
            selectedAvailableIndex = Mathf.Clamp(selectedAvailableIndex, 0, available.Count - 1);
            return research.Start(available[selectedAvailableIndex].Id);
        }

        public void SelectNextAvailable()
        {
            var count = research?.AvailableTechnologies.Count ?? 0;
            selectedAvailableIndex = count == 0 ? 0 : (selectedAvailableIndex + 1) % count;
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (research == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("Research - Stone Age");
            var active = research.ActiveProject;
            if (active == null) GUILayout.Label("Active: none");
            else
            {
                var remaining = active.RequiredWorkTicks - active.AccumulatedWorkTicks;
                var totalRate = research.AssignedWorkers.Sum(id => research.GetRatePerThousand(id));
                var etaTicks = totalRate <= 0 ? -1 : (long)Math.Ceiling(remaining * 1000d / totalRate);
                GUILayout.Label($"Active: {active.TechnologyId}  {active.Progress:P1}");
                GUILayout.Label($"Rate: {totalRate / 1000d:0.##}x  ETA ticks: {(etaTicks < 0 ? "n/a" : etaTicks.ToString())}");
            }
            var available = research.AvailableTechnologies;
            if (available.Count > 0)
            {
                selectedAvailableIndex = Mathf.Clamp(selectedAvailableIndex, 0, available.Count - 1);
                var selected = available[selectedAvailableIndex];
                GUILayout.Label($"Available: {selected.DisplayName} [{selected.Id}]");
                GUILayout.Label("Unlocks: " + string.Join(", ", selected.Unlocks.Select(x => x.ContentId)));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Next")) SelectNextAvailable();
                if (GUILayout.Button("Start") && active == null) StartSelected();
                if (GUILayout.Button("Cancel") && active != null) research.Cancel();
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Available: none");
            var locked = research.Catalog.All.Count(x => !research.IsCompleted(x.Id) && !research.IsAvailable(x.Id) &&
                (active == null || active.TechnologyId != x.Id));
            GUILayout.Label($"Completed: {research.CompletedTechnologies.Count}/{research.Catalog.All.Count}  Locked: {locked}");
            GUILayout.Label($"Workers: {research.AssignedWorkers.Count}  tick={research.LastProcessedTick}  hash={research.ComputeStateHash():X16}");
            GUILayout.Label("Prerequisites: " + (available.Count == 0 ? "-" :
                string.Join(", ", available[selectedAvailableIndex].Prerequisites.Select(x => x.ToString()))));
            GUILayout.EndArea();
        }
    }
}
