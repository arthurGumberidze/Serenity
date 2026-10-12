using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Research;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingDebugOption
    {
        internal BuildingDebugOption(BuildingDefinition definition, bool isAvailable, string lockReason)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            IsAvailable = isAvailable;
            LockReason = lockReason ?? string.Empty;
        }

        public BuildingDefinition Definition { get; }
        public bool IsAvailable { get; }
        public string LockReason { get; }
    }

    public static class BuildingDebugCatalogView
    {
        public static IReadOnlyList<BuildingDebugOption> Capture(BuildingPresentationCatalog catalog,
            ResearchService research)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (research == null) throw new ArgumentNullException(nameof(research));
            return catalog.Entries.Select(entry => entry.CreateDefinition())
                .OrderBy(definition => definition.Id.ToString(), StringComparer.Ordinal)
                .Select(definition => CreateOption(definition, research))
                .ToArray();
        }

        private static BuildingDebugOption CreateOption(BuildingDefinition definition, ResearchService research)
        {
            if (research.IsBuildingUnlocked(definition.Id))
                return new BuildingDebugOption(definition, true, string.Empty);
            var technologyIds = research.Catalog.All
                .Where(technology => technology.Unlocks.Any(unlock =>
                    unlock.Kind == TechnologyUnlockKind.Building &&
                    string.Equals(unlock.ContentId, definition.Id.ToString(), StringComparison.Ordinal)))
                .Select(technology => technology.Id.ToString())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var reason = technologyIds.Length == 0
                ? "не открывается текущим каталогом исследований"
                : "требуется: " + string.Join(" или ", technologyIds.Select(id => RussianTextCatalog.Content("technology", id)));
            return new BuildingDebugOption(definition, false, reason);
        }
    }

    /// <summary>Development-only catalog selector. Placement and validation remain in the U08/U09 path.</summary>
    public sealed class BuildingDebugSelector : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 390f;
        private const float PanelHeight = 142f;
        private BuildingPresentationCatalog catalog;
        private ResearchService research;
        private BuildingPlacementController placement;
        private WorldPointerRaycaster raycaster;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<BuildingDebugOption> Options => catalog == null || research == null
            ? Array.Empty<BuildingDebugOption>()
            : BuildingDebugCatalogView.Capture(catalog, research);

        public void Initialize(BuildingPresentationCatalog presentationCatalog, ResearchService researchService,
            BuildingPlacementController placementController, WorldPointerRaycaster worldRaycaster)
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
            catalog = presentationCatalog ?? throw new ArgumentNullException(nameof(presentationCatalog));
            research = researchService ?? throw new ArgumentNullException(nameof(researchService));
            placement = placementController ?? throw new ArgumentNullException(nameof(placementController));
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            Subscribe();
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return IsOpen && catalog != null && research != null &&
                (Application.isEditor || Debug.isDebugBuild);
        }

        public void Open()
        {
            if (catalog == null || research == null || placement == null)
                throw new InvalidOperationException("Building debug selector is not initialized.");
            IsOpen = true;
        }

        public void Close() => IsOpen = false;
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public bool Select(BuildingDefinitionId definitionId)
        {
            var option = Options.FirstOrDefault(candidate => candidate.Definition.Id == definitionId);
            if (option == null) return false;
            if (!placement.TryStartPlacement(definitionId)) return false;
            Close();
            return true;
        }

        private void OnEnable()
        {
            Subscribe();
            raycaster?.RegisterUiBlocker(this);
        }

        private void OnDisable()
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
        }

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (placement == null) return;
            placement.BuildMenuRequested -= Toggle;
            placement.BuildMenuRequested += Toggle;
        }

        private void Unsubscribe()
        {
            if (placement != null) placement.BuildMenuRequested -= Toggle;
        }

        private void OnGUI()
        {
            if (!TryGetUiBlockingRect(out var panelRect)) return;
            GUI.Box(panelRect, GUIContent.none);
            GUI.BeginGroup(panelRect);
            GUI.Label(new Rect(8f, 7f, PanelWidth - 16f, 20f), "Постройки (B — закрыть · R — повернуть · Esc/ПКМ — отменить)");
            var options = Options;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var y = 31f + i * 34f;
                var label = option.IsAvailable ? "ДОСТУПНО" : "ЗАКРЫТО · " + option.LockReason;
                GUI.enabled = option.IsAvailable;
                if (GUI.Button(new Rect(8f, y, 154f, 26f), RussianTextCatalog.Content("building", option.Definition.Id.ToString())))
                    Select(option.Definition.Id);
                GUI.enabled = true;
                GUI.Label(new Rect(170f, y + 3f, PanelWidth - 178f, 22f),
                    $"{option.Definition.Id}: {label}");
            }
            GUI.EndGroup();
        }

        private static Rect GetPanelRect() =>
            new Rect(450f, Mathf.Max(550f, Screen.height - PanelHeight - 12f), PanelWidth, PanelHeight);
    }
}
