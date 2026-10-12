using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Research;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
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
            GUILayout.Label("Исследования каменного века");
            var active = research.ActiveProject;
            var workerStatus = CaptureWorkerStatus();
            if (active == null) GUILayout.Label("Текущее исследование: нет");
            else
            {
                var remaining = active.RequiredWorkTicks - active.AccumulatedWorkTicks;
                var totalRate = workerStatus.ActiveWorkerIds.Sum(id => research.GetRatePerThousand(id));
                var etaTicks = totalRate <= 0 ? -1 : (long)Math.Ceiling(remaining * 1000d / totalRate);
                GUILayout.Label($"Изучается: {RussianTextCatalog.Content("technology", active.TechnologyId.ToString())} · прогресс {RussianTextCatalog.Percent(active.Progress)}");
                GUILayout.Label($"Скорость: {totalRate / 1000d:0.##}x · осталось тактов: {(etaTicks < 0 ? "неизвестно" : etaTicks.ToString())}");
            }
            GUILayout.Label($"Исследователи: назначено {workerStatus.AssignedCount} · сейчас работают {workerStatus.ActiveCount}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Назначить выбранных")) AssignSelected();
            if (GUILayout.Button("Снять выбранных")) RemoveSelected();
            GUILayout.EndHorizontal();
            if (active != null && workerStatus.ActiveCount == 0)
                GUILayout.Label("Прогресс остановлен: никто из назначенных исследователей сейчас не работает.");
            foreach (var workerId in workerStatus.AssignedWorkerIds.Take(3))
                GUILayout.Label("Назначен: " + WorkerLabel(workerId));
            var available = research.AvailableTechnologies;
            if (available.Count > 0)
            {
                selectedAvailableIndex = Mathf.Clamp(selectedAvailableIndex, 0, available.Count - 1);
                var selected = available[selectedAvailableIndex];
                GUILayout.Label($"Доступно: {RussianTextCatalog.Content("technology", selected.Id.ToString())} [{selected.Id}]");
                GUILayout.Label("Открывает: " + string.Join(", ", selected.Unlocks.Select(UnlockLabel)));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Следующее")) SelectNextAvailable();
                if (GUILayout.Button("Начать") && active == null) StartSelected();
                if (GUILayout.Button("Отменить") && active != null) research.Cancel();
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Доступно: нет");
            var locked = research.Catalog.All.Count(x => !research.IsCompleted(x.Id) && !research.IsAvailable(x.Id) &&
                (active == null || active.TechnologyId != x.Id));
            GUILayout.Label($"Завершено: {research.CompletedTechnologies.Count}/{research.Catalog.All.Count} · закрыто: {locked}");
            GUILayout.Label("Требования: " + (available.Count == 0 ? "нет" :
                string.Join(", ", available[selectedAvailableIndex].Prerequisites.Select(x => RussianTextCatalog.Content("technology", x.ToString())))));
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

        private static string UnlockLabel(TechnologyUnlock unlock)
        {
            switch (unlock.Kind)
            {
                case TechnologyUnlockKind.Building:
                    return RussianTextCatalog.Content("building", unlock.ContentId);
                case TechnologyUnlockKind.ProductionRecipe:
                    return RussianTextCatalog.Content("recipe", unlock.ContentId);
                default:
                    return RussianTextCatalog.Get("ui.research.capability");
            }
        }
    }
}
