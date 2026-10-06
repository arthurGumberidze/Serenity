using System;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Simulation.Production;
using UnityEngine;

namespace Game.Presentation.Production
{
    public sealed class ProductionSourcePresenter : MonoBehaviour
    {
        private ProductionSite site;
        private Renderer cachedRenderer;
        private Color activeColor;

        public ProductionSite Site => site;
        public bool IsBound => site != null;

        public void Bind(ProductionSite productionSite, Color color)
        {
            site = productionSite ?? throw new ArgumentNullException(nameof(productionSite));
            activeColor = color;
            cachedRenderer = GetComponentInChildren<Renderer>();
            gameObject.name = $"Production {site.RecipeId} [{site.Id}]";
            Refresh();
        }

        public void Refresh()
        {
            if (site == null || cachedRenderer == null) return;
            var block = new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", site.IsDepleted ? Color.gray : activeColor);
            cachedRenderer.SetPropertyBlock(block);
            gameObject.SetActive(!site.IsDepleted || site.Kind == ProductionSiteKind.PrimitiveCropPlot);
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
            GUILayout.Label("Production (canonical total / target / produced)");
            foreach (var id in Tracked)
            {
                var definition = resources.Get(id);
                GUILayout.Label($"{definition.DisplayName}: {production.GetCanonicalTotal(id)} / " +
                    $"{production.GetTarget(id)} / {production.GetProducedTotal(id)}");
            }
            var available = 0;
            var depleted = 0;
            foreach (var site in production.Sites)
                if (site.IsDepleted) depleted++; else if (site.IsWorkAvailable) available++;
            GUILayout.Label($"Jobs: {production.ActiveTasks.Count}  Sources: {available} available / {depleted} depleted");
            GUILayout.Label($"Food consumed: {production.Food.TotalConsumed}  shortage: {production.Food.TotalShortage} " +
                $"({(production.Food.IsShortage ? "SHORT" : "ok")})");
            GUILayout.Label($"Production tick: {production.LastCalendarTick}  hash: {production.ComputeStateHash():X16}");
            GUILayout.EndArea();
        }
    }
}
