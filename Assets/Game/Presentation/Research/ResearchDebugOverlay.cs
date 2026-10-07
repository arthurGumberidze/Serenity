using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Research;
using Game.Presentation.Interaction;
using Game.Simulation.Research;
using UnityEngine;

namespace Game.Presentation.Research
{
    public sealed class ResearchDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private static readonly Rect PanelRect = new Rect(450f, 242f, 390f, 300f);
        private ResearchService research;
        private SelectionProbe selection;
        private Tier1AiAgentRegistry agents;
        private CharacterRegistry characters;
        private WorldPointerRaycaster raycaster;
        private int selectedAvailableIndex;

        public void Initialize(ResearchService service, SelectionProbe selectionProbe,
            Tier1AiAgentRegistry agentRegistry, CharacterRegistry characterRegistry,
            WorldPointerRaycaster worldRaycaster)
        {
            research = service ?? throw new ArgumentNullException(nameof(service));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            agents = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
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

        public int AssignSelected() => research.AssignWorkers(selection.SelectedCharacterIds);
        public int RemoveSelected() => research.UnassignWorkers(selection.SelectedCharacterIds);
        public int AssignWorkers(System.Collections.Generic.IEnumerable<StableEntityId> workerIds) =>
            research.AssignWorkers(workerIds);
        public int RemoveWorkers(System.Collections.Generic.IEnumerable<StableEntityId> workerIds) =>
            research.UnassignWorkers(workerIds);
        public ResearchWorkerStatus CaptureWorkerStatus() => ResearchWorkerStatusQuery.Capture(research, agents);

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (research == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("Research - Stone Age");
            var active = research.ActiveProject;
            var workerStatus = CaptureWorkerStatus();
            if (active == null) GUILayout.Label("Active technology: none");
            else
            {
                var remaining = active.RequiredWorkTicks - active.AccumulatedWorkTicks;
                var totalRate = workerStatus.ActiveWorkerIds.Sum(id => research.GetRatePerThousand(id));
                var etaTicks = totalRate <= 0 ? -1 : (long)Math.Ceiling(remaining * 1000d / totalRate);
                GUILayout.Label($"Active technology: {active.TechnologyId}  Progress: {active.Progress:P1}");
                GUILayout.Label($"Rate: {totalRate / 1000d:0.##}x  ETA ticks: {(etaTicks < 0 ? "n/a" : etaTicks.ToString())}");
            }
            GUILayout.Label($"Assigned researchers: {workerStatus.AssignedCount}   Active researchers now: {workerStatus.ActiveCount}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Assign selected to Research")) AssignSelected();
            if (GUILayout.Button("Remove selected from Research")) RemoveSelected();
            GUILayout.EndHorizontal();
            if (active != null && workerStatus.ActiveCount == 0)
                GUILayout.Label("Progress paused: no assigned researcher is actively Researching.");
            foreach (var workerId in workerStatus.AssignedWorkerIds.Take(3))
                GUILayout.Label("Assigned: " + WorkerLabel(workerId));
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
            GUILayout.Label("Prerequisites: " + (available.Count == 0 ? "-" :
                string.Join(", ", available[selectedAvailableIndex].Prerequisites.Select(x => x.ToString()))));
            GUILayout.EndArea();
        }

        private string WorkerLabel(StableEntityId workerId)
        {
            var text = workerId.ToString();
            var shortId = text.Length <= 8 ? text : text.Substring(0, 8);
            return characters.TryGet(workerId, out var character)
                ? $"{character.Name.DisplayName} [{shortId}]"
                : shortId;
        }
    }
}
