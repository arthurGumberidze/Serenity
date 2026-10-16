using System;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Production;
using UnityEngine;

namespace Game.Presentation.Production
{
    public sealed class ProductionSourcePresenter : MonoBehaviour
    {
        private ProductionSite site;
        private Renderer[] cachedRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private Color activeColor;
        private bool? displayedDepleted;
        private bool isSelected;

        public ProductionSite Site => site;
        public bool IsBound => site != null;
        public bool IsSelected => isSelected;
        public long RemainingAmount => site?.RemainingAmount ?? 0;
        public long SourceCapacity => site?.SourceCapacity ?? 0;

        public void Bind(ProductionSite productionSite, Color color)
        {
            site = productionSite ?? throw new ArgumentNullException(nameof(productionSite));
            activeColor = color;
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            gameObject.name = $"Production {site.RecipeId} [{site.Id}]";
            var hitProxy = GetComponent<StableWorldHitProxy>() ?? gameObject.AddComponent<StableWorldHitProxy>();
            hitProxy.Bind(site.Id, ContextTargetKind.ResourceSource);
            Refresh();
        }

        public void Refresh()
        {
            if (site == null || cachedRenderers.Length == 0) return;
            if (displayedDepleted == site.IsDepleted && !isSelected) return;
            displayedDepleted = site.IsDepleted;
            propertyBlock ??= new MaterialPropertyBlock();
            var color = site.IsDepleted ? Color.gray : isSelected ? Color.Lerp(activeColor, Color.white, 0.55f) : activeColor;
            foreach (var renderer in cachedRenderers)
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
            gameObject.SetActive(!site.IsDepleted || site.Kind == ProductionSiteKind.PrimitiveCropPlot);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            var hitProxy = GetComponent<StableWorldHitProxy>();
            if (hitProxy != null) hitProxy.SetSelected(selected);
            displayedDepleted = null;
            Refresh();
        }
    }

    public sealed class ProductionDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private static readonly Rect PanelRect = new Rect(450f, 12f, 390f, 222f);
        private static readonly ResourceId[] Tracked =
        {
            new ResourceId("berries"), new ResourceId("wood_log"),
            new ResourceId("stone"), new ResourceId("plant_fiber")
        };
        private ProductionService production;
        private ResourceCatalog resources;
        private WorldPointerRaycaster raycaster;

        public void Initialize(ProductionService service, ResourceCatalog catalog, WorldPointerRaycaster worldRaycaster)
        {
            production = service ?? throw new ArgumentNullException(nameof(service));
            resources = catalog ?? throw new ArgumentNullException(nameof(catalog));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = PanelRect;
            return production != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (production == null || resources == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label(RussianTextCatalog.Get("debug.production.title"));
            foreach (var id in Tracked)
            {
                var definition = resources.Get(id);
                GUILayout.Label(RussianTextCatalog.Format("debug.production.resource", RussianTextCatalog.Content("resource", id.ToString()),
                    production.GetCanonicalTotal(id), production.GetTarget(id), production.GetProducedTotal(id)));
            }
            var available = 0;
            var depleted = 0;
            foreach (var site in production.Sites)
                if (site.IsDepleted) depleted++; else if (site.IsWorkAvailable) available++;
            GUILayout.Label(RussianTextCatalog.Format("debug.production.jobs", production.ActiveTasks.Count, available, depleted));
            GUILayout.Label(RussianTextCatalog.Format("debug.production.food", production.Food.TotalConsumed, production.Food.TotalShortage,
                RussianTextCatalog.Get(production.Food.IsShortage ? "debug.production.short" : "debug.production.ok")));
            GUILayout.Label(RussianTextCatalog.Format("debug.production.tick", production.LastCalendarTick,
                production.ComputeStateHash().ToString("X16")));
            GUILayout.EndArea();
        }
    }
}
