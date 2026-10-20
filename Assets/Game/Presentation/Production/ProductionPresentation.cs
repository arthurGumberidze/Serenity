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
        private bool preserveAuthoredColor;

        public ProductionSite Site => site;
        public bool IsBound => site != null;
        public bool IsSelected => isSelected;
        public long RemainingAmount => site?.RemainingAmount ?? 0;
        public long SourceCapacity => site?.SourceCapacity ?? 0;

        public void Bind(ProductionSite productionSite, Color color, bool preserveColor = false)
        {
            site = productionSite ?? throw new ArgumentNullException(nameof(productionSite));
            activeColor = color;
            preserveAuthoredColor = preserveColor;
            displayedDepleted = null;
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            if (!preserveColor) gameObject.name = $"Production {site.RecipeId} [{site.Id}]";
            var hitProxy = GetComponent<StableWorldHitProxy>() ?? gameObject.AddComponent<StableWorldHitProxy>();
            hitProxy.Bind(site.Id, ContextTargetKind.ResourceSource);
            if (site.RecipeId.ToString() == "chop_wood" && transform.Find("Tree selection volume") == null)
            {
                // Select the visible crown as well as the trunk without blocking navigation.
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                var initialized = false;
                foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true))
                {
                    var worldBounds = mesh.bounds;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var point = transform.InverseTransformPoint(worldBounds.center + Vector3.Scale(
                            worldBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                                (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                        if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                if (initialized)
                {
                    var volume = new GameObject("Tree selection volume");
                    volume.transform.SetParent(transform, false);
                    volume.layer = gameObject.layer;
                    var collider = volume.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    var scale = transform.lossyScale;
                    // A full foliage AABB contains large empty gaps and can cover an entire camp
                    // in perspective. Keep a narrow trunk-to-crown column, not a wall of selection.
                    collider.center = new Vector3(0f, bounds.center.y, 0f);
                    collider.size = new Vector3(
                        Mathf.Min(bounds.size.x, 2.2f / Mathf.Max(0.01f, Mathf.Abs(scale.x))),
                        Mathf.Max(0.5f, bounds.size.y),
                        Mathf.Min(bounds.size.z, 2.2f / Mathf.Max(0.01f, Mathf.Abs(scale.z))));
                }
            }
            Refresh();
        }

        public void Refresh()
        {
            if (site == null || cachedRenderers.Length == 0) return;
            if (displayedDepleted == site.IsDepleted && !isSelected) return;
            if (displayedDepleted == false && site.IsDepleted)
            {
                var recipe = site.RecipeId.ToString();
                if (recipe == "chop_wood" || recipe == "gather_stone" || recipe == "gather_iron" || recipe == "gather_gold")
                    NaturalHarvestEffect.Play(gameObject, recipe == "chop_wood");
            }
            displayedDepleted = site.IsDepleted;
            propertyBlock ??= new MaterialPropertyBlock();
            var color = site.IsDepleted ? Color.gray : isSelected ? Color.Lerp(activeColor, Color.white, 0.55f) : activeColor;
            foreach (var renderer in cachedRenderers)
            {
                if (preserveAuthoredColor && !site.IsDepleted && !isSelected)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
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
