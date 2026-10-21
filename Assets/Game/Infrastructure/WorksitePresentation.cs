using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.Resources;
using UnityEngine;

namespace Game.Infrastructure
{
    // One bounded presentation scheduler; resource quantities always come from the domain.
    public sealed class WorksitePresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private readonly Dictionary<StableEntityId, WorldResourcePilePresenter> views = new();
        private readonly List<Material> materials = new();
        private readonly Dictionary<StableEntityId, GameObject> carriedViews = new();
        private Material woodMaterial;
        private Material otherMaterial;
        private float nextRefresh;
        private Transform[] fish = Array.Empty<Transform>();
        private Vector3 pondCenter;
        private float fishTime;

        public void Initialize(LocalSceneCompositionRoot composition)
        {
            root = composition;
            woodMaterial = NewMaterial(new Color(0.38f, 0.21f, 0.09f));
            otherMaterial = NewMaterial(new Color(0.5f, 0.47f, 0.35f));
            foreach (var view in FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (root.WorldPiles.All.Any(x => x.Id == view.PileId)) views[view.PileId] = view;
        }

        public void CreateFish(Transform parent, Vector3 center)
        {
            pondCenter = center;
            var silver = NewMaterial(new Color(0.38f, 0.58f, 0.58f));
            fish = new Transform[8];
            for (var i = 0; i < fish.Length; i++)
            {
                var body = Shape(parent, PrimitiveType.Sphere, new Vector3(0.32f, 0.22f, 0.95f), silver);
                body.name = "Swimming fish " + i;
                var tail = Shape(body, PrimitiveType.Cube, new Vector3(1.9f, 0.4f, 0.22f), silver);
                tail.localPosition = new Vector3(0f, 0f, -0.55f);
                fish[i] = body;
            }
        }

        private void Update()
        {
            if (root?.Clock == null) return;
            if (!root.Clock.IsPaused) fishTime += Time.unscaledDeltaTime;
            for (var i = 0; i < fish.Length; i++)
            {
                if (fish[i] == null) continue;
                var phase = fishTime * (0.3f + i * 0.015f) + i * 1.7f;
                var jumpPhase = Mathf.Repeat(fishTime + i * 3.1f, 16f);
                var jump = jumpPhase < 1.1f ? Mathf.Sin(jumpPhase / 1.1f * Mathf.PI) * 1.1f : 0f;
                fish[i].position = pondCenter + new Vector3(Mathf.Sin(phase) * (7f + i),
                    0.06f + jump, Mathf.Cos(phase) * (4f + i * 0.5f));
                fish[i].rotation = Quaternion.Euler(-jump * 30f, phase * Mathf.Rad2Deg + 90f, 0f);
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshPiles();
            RefreshCarriedLoads();
        }

        private void RefreshCarriedLoads()
        {
            if (root.AiRuntime == null) return;
            foreach (var movement in root.AiRuntime.Movements)
            {
                var id = movement.CharacterId;
                var hasLoad = root.Inventories.TryGet(new InventoryOwner(InventoryOwnerKind.CarryLoad, id), out var load) &&
                    load.TotalUnits > 0;
                if (!carriedViews.TryGetValue(id, out var view) || view == null)
                {
                    if (!hasLoad) continue;
                    var wood = Shape(movement.Presenter.transform, PrimitiveType.Cylinder,
                        new Vector3(0.28f, 0.55f, 0.28f), woodMaterial);
                    wood.localPosition = new Vector3(0f, 0.95f, 0.35f);
                    wood.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    view = wood.gameObject;
                    view.name = "Physical transport load";
                    carriedViews[id] = view;
                }
                view.SetActive(hasLoad && movement.Presenter.DetailedVisualActive);
                if (hasLoad) view.GetComponent<Renderer>().sharedMaterial =
                    load.GetAmount(new ResourceId("wood_log")).IsPositive ? woodMaterial : otherMaterial;
            }
        }

        public void RefreshPiles()
        {
            if (root == null) return;
            var focus = root.CameraController.transform.position;
            foreach (var pile in root.WorldPiles.All)
            {
                if (!root.Inventories.TryGet(pile.InventoryOwner, out var inventory)) continue;
                var position = root.BuildingSpawner.GridToWorld(pile.Coordinate);
                var visible = inventory.GetAmount(pile.ResourceId).IsPositive &&
                    new Vector2(position.x - focus.x, position.z - focus.z).sqrMagnitude < 300f * 300f;
                if (!views.TryGetValue(pile.Id, out var view) || view == null)
                {
                    if (!visible) continue;
                    var holder = new GameObject("Harvest pile");
                    holder.transform.SetParent(transform, false);
                    holder.transform.position = position + Vector3.up * 0.16f;
                    var wood = pile.ResourceId.ToString() == "wood_log";
                    var material = wood ? woodMaterial : otherMaterial;
                    for (var i = 0; i < 5; i++)
                    {
                        var part = Shape(holder.transform, wood ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
                            wood ? new Vector3(0.28f, 0.85f, 0.28f) : Vector3.one * 0.35f, material);
                        part.localPosition = new Vector3((i % 3 - 1) * 0.32f, i / 3 * 0.25f, 0f);
                        if (wood) part.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    var box = holder.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = new Vector3(0f, 0.2f, 0f);
                    box.size = new Vector3(1.2f, 0.8f, 2f);
                    view = holder.AddComponent<WorldResourcePilePresenter>();
                    views[pile.Id] = view;
                }
                view.Bind(pile, inventory); // Rebind canonical references after load too.
                view.gameObject.SetActive(visible);
            }
            var ids = new HashSet<StableEntityId>(root.WorldPiles.All.Select(x => x.Id));
            foreach (var pair in views) if (!ids.Contains(pair.Key) && pair.Value != null) pair.Value.gameObject.SetActive(false);
        }

        private Material NewMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private static Transform Shape(Transform parent, PrimitiveType kind, Vector3 scale, Material material)
        {
            var value = GameObject.CreatePrimitive(kind);
            value.transform.SetParent(parent, false);
            value.transform.localScale = scale;
            value.GetComponent<Collider>().enabled = false;
            Destroy(value.GetComponent<Collider>());
            value.GetComponent<Renderer>().sharedMaterial = material;
            return value.transform;
        }

        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
