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
        private Material fishMaterial;
        private Material rippleMaterial;
        private Material fishPatternMaterial;
        private float nextRefresh;
        private readonly List<FishSchool> schools = new();
        private float fishTime;
        private sealed class FishSchool
        {
            public Transform Parent;
            public Transform[] Fish;
            public Vector3 Center;
            public float Radius;
        }
        public int FishSchoolCount => schools.Count(x => x.Parent != null);

        public void Initialize(LocalSceneCompositionRoot composition)
        {
            root = composition;
            woodMaterial = NewMaterial(new Color(0.38f, 0.21f, 0.09f));
            otherMaterial = NewMaterial(new Color(0.5f, 0.47f, 0.35f));
            fishMaterial = NewMaterial(new Color(0.52f, 0.73f, 0.69f));
            rippleMaterial = NewMaterial(new Color(0.48f, 0.72f, 0.74f));
            var art = Resources.Load<Game.Presentation.Environment.CompactWorldArtCatalog>("World/CompactWorldArt");
            if (art?.FishPattern != null)
            {
                fishPatternMaterial = NewMaterial(new Color(0.36f, 0.62f, 0.6f));
                fishPatternMaterial.SetTexture("_BaseMap", art.FishPattern);
                fishPatternMaterial.SetFloat("_AlphaClip", 1);
                fishPatternMaterial.SetFloat("_Cutoff", 0.35f);
                fishPatternMaterial.SetFloat("_Cull", 0);
                fishPatternMaterial.EnableKeyword("_ALPHATEST_ON");
            }
            foreach (var view in FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (root.WorldPiles.All.Any(x => x.Id == view.PileId)) views[view.PileId] = view;
        }

        public void CreateFish(Transform parent, Vector3 center, float radius = 2.8f)
        {
            var fish = new Transform[10];
            for (var i = 0; i < fish.Length; i++)
            {
                var body = MakeFish(parent);
                body.name = "Swimming fish " + i;
                body.localPosition = new Vector3(Mathf.Sin(i) * radius, 0.02f, Mathf.Cos(i) * radius);
                fish[i] = body;
            }
            var ripple = new GameObject("Fishing circle").AddComponent<LineRenderer>();
            ripple.transform.SetParent(parent, false);
            ripple.useWorldSpace = false;
            ripple.loop = true;
            ripple.positionCount = 48;
            ripple.widthMultiplier = 0.045f;
            ripple.sharedMaterial = rippleMaterial;
            ripple.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (var i = 0; i < 48; i++)
            {
                var angle = i * Mathf.PI * 2 / 48;
                ripple.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius * 1.15f, 0.02f,
                    Mathf.Cos(angle) * radius * 1.15f));
            }
            schools.Add(new FishSchool { Parent = parent, Fish = fish, Center = center, Radius = radius });
        }

        private Transform MakeFish(Transform parent)
        {
            if (fishPatternMaterial != null)
            {
                var holder = new GameObject("Synty fish").transform;
                holder.SetParent(parent, false);
                var pattern = Shape(holder, PrimitiveType.Quad, new Vector3(1.1f, 0.8f, 1), fishPatternMaterial);
                pattern.localRotation = Quaternion.Euler(90, -90, 0);
                return holder;
            }
            var body = Shape(parent, PrimitiveType.Sphere, new Vector3(0.22f, 0.15f, 0.65f), fishMaterial);
            var tail = Shape(body, PrimitiveType.Cube, new Vector3(1.7f, 0.5f, 0.2f), fishMaterial);
            tail.localPosition = new Vector3(0f, 0f, -0.52f);
            return body;
        }

        private void Update()
        {
            if (root?.Clock == null) return;
            if (!root.Clock.IsPaused) fishTime += Time.unscaledDeltaTime;
            schools.RemoveAll(x => x.Parent == null);
            foreach (var school in schools)
            {
                if (!school.Parent.gameObject.activeInHierarchy) continue;
                for (var i = 0; i < school.Fish.Length; i++)
                {
                    var phase = fishTime * (0.3f + i * 0.015f) + i * 1.7f;
                    var jumpPhase = Mathf.Repeat(fishTime + i * 3.1f, 16f);
                    var jump = jumpPhase < 0.8f ? Mathf.Sin(jumpPhase / 0.8f * Mathf.PI) * 0.5f : 0f;
                    var radius = school.Radius * (0.45f + i * 0.05f);
                    school.Fish[i].position = school.Center + new Vector3(Mathf.Sin(phase) * radius,
                        0.02f + jump, Mathf.Cos(phase) * radius);
                    school.Fish[i].rotation = Quaternion.Euler(-jump * 30f, phase * Mathf.Rad2Deg + 90f, 0f);
                }
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
                if (pile.ResourceId.ToString() == "fish")
                {
                    var index = root.WorldPiles.All.Where(x => x.ResourceId == pile.ResourceId &&
                        x.Coordinate.Equals(pile.Coordinate)).OrderBy(x => x.Id).TakeWhile(x => x.Id != pile.Id).Count();
                    position += new Vector3(0.65f + index % 3 * 0.7f, 0f, index / 3 % 3 * 0.65f);
                    position.y = root.TiledWorld.ProjectToGround(position).y;
                }
                var visible = inventory.GetAmount(pile.ResourceId).IsPositive &&
                    new Vector2(position.x - focus.x, position.z - focus.z).sqrMagnitude < 300f * 300f;
                if (!views.TryGetValue(pile.Id, out var view) || view == null)
                {
                    if (!visible) continue;
                    var holder = new GameObject("Harvest pile");
                    holder.transform.SetParent(transform, false);
                    holder.transform.position = position + Vector3.up * 0.16f;
                    var wood = pile.ResourceId.ToString() == "wood_log";
                    var isFish = pile.ResourceId.ToString() == "fish";
                    holder.name = isFish ? "Caught fish pile" : "Harvest pile";
                    var material = wood ? woodMaterial : otherMaterial;
                    for (var i = 0; i < 5; i++)
                    {
                        var part = isFish ? MakeFish(holder.transform) : Shape(holder.transform, wood ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
                            wood ? new Vector3(0.28f, 0.85f, 0.28f) : Vector3.one * 0.35f, material);
                        part.localPosition = new Vector3((i % 3 - 1) * 0.32f, i / 3 * 0.25f, 0f);
                        if (wood) part.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        if (isFish) part.localRotation = Quaternion.Euler(0f, i * 37f, fishPatternMaterial == null ? 90f : 0f);
                    }
                    var box = holder.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = new Vector3(0f, 0.2f, 0f);
                    box.size = new Vector3(1.2f, 0.8f, 2f);
                    view = holder.AddComponent<WorldResourcePilePresenter>();
                    views[pile.Id] = view;
                }
                view.Bind(pile, inventory); // Rebind canonical references after load too.
                if (pile.ResourceId.ToString() == "fish")
                {
                    view.transform.position = position + Vector3.up * 0.16f;
                    for (var i = 0; i < view.transform.childCount; i++)
                        view.transform.GetChild(i).gameObject.SetActive(i < inventory.GetAmount(pile.ResourceId).Units);
                }
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
