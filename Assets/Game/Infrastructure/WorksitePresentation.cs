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
        private Mesh fishBodyMesh;
        private Mesh fishTailMesh;
        private float nextRefresh;
        private readonly List<FishSchool> schools = new();
        private float fishTime;
        private sealed class FishSchool
        {
            public Transform Parent;
            public Transform[] Fish;
            public Transform[] Tails;
            public Vector3 Center;
            public float Radius;
            public StableEntityId SourceId;
        }
        public int FishSchoolCount => schools.Count(x => x.Parent != null);

        public void Initialize(LocalSceneCompositionRoot composition)
        {
            root = composition;
            woodMaterial = NewMaterial(new Color(0.38f, 0.21f, 0.09f));
            otherMaterial = NewMaterial(new Color(0.5f, 0.47f, 0.35f));
            fishMaterial = NewMaterial(new Color(0.52f, 0.73f, 0.69f));
            rippleMaterial = NewMaterial(new Color(0.48f, 0.72f, 0.74f));
            // The imported FX texture is a particle mask, not a finished fish sprite.
            // Use shared, small 3D meshes rather than overlapping horizontal mask quads.
            fishBodyMesh = CreateFishBody();
            fishTailMesh = new Mesh { name = "Fish tail" };
            fishTailMesh.vertices = new[] { Vector3.zero, new Vector3(-0.17f, 0, -0.22f),
                new Vector3(0.17f, 0, -0.22f), new Vector3(0, 0.055f, -0.17f) };
            fishTailMesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 0, 2, 1, 1, 2, 3 };
            fishTailMesh.RecalculateNormals();
            fishTailMesh.RecalculateBounds();
            foreach (var view in FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (root.WorldPiles.All.Any(x => x.Id == view.PileId)) views[view.PileId] = view;
        }

        public void CreateFish(Transform parent, Vector3 center, float radius = 2.8f, StableEntityId sourceId = default)
        {
            var fish = new Transform[10];
            var tails = new Transform[fish.Length];
            for (var i = 0; i < fish.Length; i++)
            {
                var body = MakeFish(parent);
                body.name = "Swimming fish " + i;
                fish[i] = body;
                tails[i] = body.Find("Tail");
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
            var school = new FishSchool { Parent = parent, Fish = fish, Tails = tails, Center = center, Radius = radius,
                SourceId = sourceId };
            schools.Add(school);
            RefreshFish(school);
        }

        private Transform MakeFish(Transform parent)
        {
            var holder = new GameObject("Fish").transform;
            holder.SetParent(parent, false);
            MeshPart(holder, "Body", fishBodyMesh);
            MeshPart(holder, "Tail", fishTailMesh).localPosition = new Vector3(0, 0, -0.28f);
            return holder;
        }

        private Transform MeshPart(Transform parent, string name, Mesh mesh)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fishMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part.transform;
        }

        private static Mesh CreateFishBody()
        {
            const int sides = 8;
            var vertices = new Vector3[sides * 3];
            var triangles = new List<int>();
            for (var ring = 0; ring < 3; ring++)
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Mathf.PI * 2 / sides;
                var width = ring == 1 ? 0.15f : 0.025f;
                vertices[ring * sides + side] = new Vector3(Mathf.Cos(angle) * width,
                    Mathf.Sin(angle) * width * 0.65f, ring == 0 ? 0.32f : ring == 1 ? 0.06f : -0.28f);
                if (ring == 2) continue;
                var a = ring * sides + side; var b = ring * sides + (side + 1) % sides;
                triangles.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
            }
            for (var side = 1; side < sides - 1; side++)
                triangles.AddRange(new[] { 0, side, side + 1, sides * 2, sides * 2 + side + 1, sides * 2 + side });
            var mesh = new Mesh { name = "Shared low-poly fish body", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void RefreshFish(FishSchool school)
        {
            var count = school.Fish.Length;
            if (school.SourceId.IsValid && root.Production.TryGetSite(school.SourceId, out var site))
                count = (int)Math.Min(count, site.RemainingAmount);
            for (var i = 0; i < school.Fish.Length; i++)
            {
                school.Fish[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                // Fixed phase separation prevents different speeds bunching all fish together.
                var phase = fishTime * 0.3f + i * Mathf.PI * 2 / school.Fish.Length;
                var jumpPhase = Mathf.Repeat(fishTime + i * 3.1f, 32f);
                var jump = jumpPhase < 0.8f ? Mathf.Sin(jumpPhase / 0.8f * Mathf.PI) * 0.35f : 0f;
                var radius = school.Radius * (i % 2 == 0 ? 0.64f : 0.9f);
                school.Fish[i].position = school.Center + new Vector3(Mathf.Sin(phase) * radius,
                    0.04f + jump, Mathf.Cos(phase) * radius);
                school.Fish[i].rotation = Quaternion.Euler(-jump * 30f, phase * Mathf.Rad2Deg + 90f, 0f);
                school.Tails[i].localRotation = Quaternion.Euler(0, Mathf.Sin(fishTime * 8f + i) * 20f, 0);
            }
        }

        private void Update()
        {
            if (root?.Clock == null) return;
            if (!root.Clock.IsPaused) fishTime += Time.unscaledDeltaTime;
            schools.RemoveAll(x => x.Parent == null);
            foreach (var school in schools)
            {
                if (!school.Parent.gameObject.activeInHierarchy) continue;
                RefreshFish(school);
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshPiles();
            RefreshCarriedLoads();
        }

        private void LateUpdate()
        {
            if (root?.AiRuntime == null) return;
            foreach (var movement in root.AiRuntime.Movements)
            {
                if (!carriedViews.TryGetValue(movement.CharacterId, out var view) || view == null || !view.activeSelf) continue;
                var presenter = movement.Presenter;
                var animator = presenter.Animator;
                if (animator == null || !animator.isHuman) continue;
                var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (left == null || right == null) continue;
                view.transform.position = (left.position + right.position) * 0.5f + Vector3.up * 0.07f;
                var across = right.position - left.position;
                if (across.sqrMagnitude > 0.001f)
                    view.transform.rotation = Quaternion.FromToRotation(Vector3.up, across.normalized);
            }
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
                        if (isFish) part.localRotation = Quaternion.Euler(0f, i * 37f, 90f);
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

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
            if (fishBodyMesh != null) Destroy(fishBodyMesh);
            if (fishTailMesh != null) Destroy(fishTailMesh);
        }
    }
}
