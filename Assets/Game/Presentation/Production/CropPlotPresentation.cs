using System.Collections.Generic;
using Game.Domain.Production;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Presentation.Production
{
    // Refreshed by the existing building scheduler. No per-field Update or independent crop state.
    public sealed class CropPlotPresentation : MonoBehaviour
    {
        private ProductionSite site;
        private BuildingPresenter building;
        private CropArtCatalog art;
        private Renderer[] legacy;
        private Transform plot, planting;
        private readonly List<Transform> plants = new();
        private float width, depth;
        private string displayedRecipe;
        private static Material soil;

        public void Configure(ProductionSite source, float widthMeters, float depthMeters)
        {
            site = source;
            building = GetComponent<BuildingPresenter>();
            art = Resources.Load<CropArtCatalog>("World/CropArt");
            width = Mathf.Max(1f, widthMeters - 0.25f);
            depth = Mathf.Max(1f, depthMeters - 0.25f);
            if (plot == null)
            {
                legacy = GetComponentsInChildren<Renderer>(true);
                plot = new GameObject("Cultivated field").transform;
                plot.SetParent(transform, false);
                if (soil == null)
                {
                    soil = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    soil.name = "Cultivated soil";
                    soil.SetColor("_BaseColor", new Color(0.22f, 0.12f, 0.065f));
                    soil.SetFloat("_Smoothness", 0f);
                }
                var rows = Mathf.Clamp(Mathf.FloorToInt(width / 0.65f), 2, 12);
                for (var row = 0; row < rows; row++)
                {
                    var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bed.name = "Soil row";
                    bed.transform.SetParent(plot, false);
                    bed.transform.localPosition = new Vector3(-width * 0.5f + (row + 0.5f) * width / rows, 0.045f, 0);
                    bed.transform.localScale = new Vector3(width / rows * 0.7f, 0.08f, depth * 0.8f);
                    var collider = bed.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    bed.GetComponent<Renderer>().sharedMaterial = soil;
                }
                AddFences();
            }
            displayedRecipe = null;
            Refresh();
        }

        public void Refresh()
        {
            if (site == null || plot == null || building == null || !building.IsBound) return;
            var active = building.Building.IsOperational && !building.Building.IsDestroyed;
            plot.gameObject.SetActive(active);
            if (!active) return;
            foreach (var renderer in legacy) if (renderer != null) renderer.enabled = false;
            if (displayedRecipe != site.RecipeId.ToString()) Replant();
            if (planting == null) return;
            planting.gameObject.SetActive(site.CropPhase != CropPlotPhase.Unplanted);
            var growth = site.CropPhase == CropPlotPhase.Ready ? 1f : site.GrowthRequiredTicks == 0 ? 0f :
                Mathf.Clamp01((float)((double)site.GrowthProgressTicks / site.GrowthRequiredTicks));
            foreach (var plant in plants) plant.localScale = Vector3.one * Mathf.Lerp(0.18f, 1f, growth);
        }

        private void Replant()
        {
            displayedRecipe = site.RecipeId.ToString();
            if (planting != null) { planting.gameObject.SetActive(false); Destroy(planting.gameObject); }
            plants.Clear();
            planting = new GameObject("Crop rows").transform;
            planting.SetParent(plot, false);
            var prefab = art != null ? art.For(displayedRecipe) : null;
            if (prefab == null) return;
            var rows = Mathf.Clamp(Mathf.FloorToInt(width / 0.65f), 2, 12);
            var columns = Mathf.Clamp(Mathf.FloorToInt(depth / 0.65f), 2, 12);
            for (var x = 0; x < rows; x++)
            for (var z = 0; z < columns; z++)
            {
                var holder = new GameObject("Growing plant").transform;
                holder.SetParent(planting, false);
                holder.localPosition = new Vector3(-width * 0.5f + (x + 0.5f) * width / rows, 0.09f,
                    (-depth * 0.5f + (z + 0.5f) * depth / columns) * 0.8f);
                var view = WorldMapScenery.Instantiate(prefab, holder);
                Fit(view, new Vector3(width / rows * 0.62f,
                    displayedRecipe == "cultivate_cabbage" ? 0.32f : 0.55f, depth / columns * 0.62f), false);
                plants.Add(holder);
            }
        }

        private void AddFences()
        {
            if (art == null || art.Fence == null) return;
            AddFence(new Vector3(0, 0, depth * 0.5f), width, 0f);
            AddFence(new Vector3(-width * 0.5f, 0, 0), depth, 90f);
            AddFence(new Vector3(width * 0.5f, 0, 0), depth, 90f);
            // Keep a real entrance wide enough for a resident; do not fence across the work approach.
            var gap = Mathf.Min(1.2f, width * 0.5f);
            var half = (width - gap) * 0.5f;
            AddFence(new Vector3(-(width + gap) * 0.25f, 0, -depth * 0.5f), half, 0f);
            AddFence(new Vector3((width + gap) * 0.25f, 0, -depth * 0.5f), half, 0f);
        }

        private void AddFence(Vector3 position, float length, float yaw)
        {
            var holder = new GameObject("Field fence").transform;
            holder.SetParent(plot, false); holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            var view = WorldMapScenery.Instantiate(art.Fence, holder);
            Fit(view, new Vector3(length, 0.65f, 0.13f), true);
        }

        private static void Fit(GameObject view, Vector3 size, bool stretch)
        {
            var parent = view.transform.parent;
            var bounds = new Bounds(); var initialized = false;
            foreach (var mesh in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) continue;
                var b = mesh.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var point = parent.InverseTransformPoint(mesh.transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!initialized) return;
            var scale = new Vector3(size.x / Mathf.Max(0.01f, bounds.size.x), size.y / Mathf.Max(0.01f, bounds.size.y),
                size.z / Mathf.Max(0.01f, bounds.size.z));
            if (!stretch) scale = Vector3.one * Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z));
            view.transform.localScale = Vector3.Scale(view.transform.localScale, scale);
            view.transform.localPosition = Vector3.Scale(view.transform.localPosition -
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
        }
    }
}
