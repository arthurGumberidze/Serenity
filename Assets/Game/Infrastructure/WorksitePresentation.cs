using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.Resources;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure
{
    // One bounded presentation scheduler; resource quantities always come from the domain.
    public sealed class WorksitePresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private IEnumerable<WorldResourcePile> AllPiles => root.StarterEnemyAi == null ? root.WorldPiles.All :
            root.WorldPiles.All.Concat(root.StarterEnemyAi.WorldPiles.All);
        private bool TryInventory(InventoryOwner owner,out ResourceInventory inventory)
        {
            if(root.Inventories.TryGet(owner,out inventory))return true;
            return root.StarterEnemyAi != null && root.StarterEnemyAi.Inventories.TryGet(owner,out inventory);
        }

        private readonly Dictionary<StableEntityId, WorldResourcePilePresenter> views = new();
        private readonly List<Material> materials = new();
        private readonly Dictionary<StableEntityId, GameObject> carriedViews = new();
        private Material rippleMaterial;
        private readonly Dictionary<StableEntityId, string> carriedKinds = new();
        private float nextRefresh;
        private readonly List<FishSchool> schools = new();
        private float fishTime;
        private sealed class FishSchool
        {
            public Transform Parent;
            public Transform[] Fish;
            public Vector3 Center;
            public float Radius;
            public StableEntityId SourceId;
        }
        public int FishSchoolCount => schools.Count(x => x.Parent != null);

        public void Initialize(LocalSceneCompositionRoot composition)
        {
            root = composition;
            rippleMaterial = NewMaterial(new Color(0.48f, 0.72f, 0.74f));
            foreach (var view in FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (AllPiles.Any(x => x.Id == view.PileId)) views[view.PileId] = view;
        }

        public void CreateFish(Transform parent, Vector3 center, float radius = 2.8f, StableEntityId sourceId = default)
        {
            var fish = new Transform[10];
            for (var i = 0; i < fish.Length; i++)
            {
                var body = MakeFish(parent);
                body.name = "Swimming fish " + i;
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
            var school = new FishSchool { Parent = parent, Fish = fish, Center = center, Radius = radius,
                SourceId = sourceId };
            schools.Add(school);
            RefreshFish(school);
        }

        private Transform MakeFish(Transform parent) => WorldPropCatalog.Create("fish", parent).transform;

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
                // Static imported fish follows the shared school scheduler; no fabricated tail mesh.
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
                    view.transform.rotation = Quaternion.LookRotation(presenter.transform.forward, Vector3.up);
            }
        }

        private void RefreshCarriedLoads()
        {
            if (root.AiRuntime == null) return;
            foreach (var movement in root.AiRuntime.Movements)
            {
                var id = movement.CharacterId;
                var hasLoad = TryInventory(new InventoryOwner(InventoryOwnerKind.CarryLoad, id), out var load) &&
                    load.TotalUnits > 0;
                var kind = hasLoad && load.GetAmount(new ResourceId("wood_log")).IsPositive
                    ? "carry_log" : "carry_basket";
                carriedViews.TryGetValue(id, out var view);
                if (hasLoad && (view == null || !carriedKinds.TryGetValue(id, out var previous) || previous != kind))
                {
                    if (view != null) { view.SetActive(false); Destroy(view); }
                    view = WorldPropCatalog.Create(kind, movement.Presenter.transform);
                    view.transform.localPosition = new Vector3(0f, .95f, .35f);
                    carriedViews[id] = view; carriedKinds[id] = kind;
                }
                if (view != null) view.SetActive(hasLoad && movement.Presenter.DetailedVisualActive);
            }
        }

        public void RefreshPiles()
        {
            if (root == null) return;
            var focus = root.CameraController.transform.position;
            foreach (var pile in AllPiles)
            {
                if (!TryInventory(pile.InventoryOwner, out var inventory)) continue;
                var position = root.BuildingSpawner.GridToWorld(pile.Coordinate);
                if (pile.ResourceId.ToString() == "fish")
                {
                    var index = AllPiles.Where(x => x.ResourceId == pile.ResourceId &&
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
                    holder.transform.position = position;
                    var isFish = pile.ResourceId.ToString() == "fish";
                    holder.name = isFish ? "Caught fish pile" : "Harvest pile " + pile.ResourceId;
                    if (isFish)
                    {
                        for (var i = 0; i < 5; i++)
                        {
                            var part = MakeFish(holder.transform);
                            part.localPosition = new Vector3((i % 3 - 1) * .28f, .08f + i / 3 * .12f, 0);
                            part.localRotation = Quaternion.Euler(0f, i * 37f, 90f);
                        }
                    }
                    else WorldPropCatalog.Create("pile_" + pile.ResourceId, holder.transform);
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
                    view.transform.position = position;
                    for (var i = 0; i < view.transform.childCount; i++)
                        view.transform.GetChild(i).gameObject.SetActive(i < inventory.GetAmount(pile.ResourceId).Units);
                }
                view.gameObject.SetActive(visible);
            }
            var ids = new HashSet<StableEntityId>(AllPiles.Select(x => x.Id));
            foreach (var pair in views) if (!ids.Contains(pair.Key) && pair.Value != null) pair.Value.gameObject.SetActive(false);
        }

        private Material NewMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);

        }
    }
}
