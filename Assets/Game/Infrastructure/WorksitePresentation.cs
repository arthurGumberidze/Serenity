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

        private readonly Dictionary<StableEntityId, string> carriedKinds = new();
        private float nextRefresh;
        private readonly List<FishSchool> schools = new();
        private float fishTime;
        private sealed class FishSchool
        {
            public Transform Parent;
            public Transform[] Fish;
            public Animator[] Animators;
            public Vector3 Center;
            public float Phase;
            public StableEntityId SourceId;
            public int SchoolSize;
            public bool Solitary;
        }
        public int FishSchoolCount => schools.Count(x => x.Parent != null && !x.Solitary);
        public int SolitaryFishCount => schools.Count(x => x.Parent != null && x.Solitary);

        public void Initialize(LocalSceneCompositionRoot composition)
        {
            root = composition;
            foreach (var view in FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (AllPiles.Any(x => x.Id == view.PileId)) views[view.PileId] = view;
        }

        private readonly HashSet<StableEntityId> fisheryViews = new();
        public void CreateWaterBodyFish(string waterId, StableEntityId sourceId)
        {
            if (!fisheryViews.Add(sourceId)) return;
            StartCoroutine(PrepareWaterBodyFish(waterId,sourceId));
        }
        private System.Collections.IEnumerator PrepareWaterBodyFish(string waterId,StableEntityId sourceId)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var world = root.WorldDefinition;
            var seed = new System.Random(sourceId.GetHashCode());
            // Each school owns a disjoint swimming cell. Coverage is uniform; motions are independently phased.
            for (var x = world.Origin.X + 8; x < world.Origin.X + world.Columns * world.TileSizeMeters; x += 16)
            for (var z = world.Origin.Z + 8; z < world.Origin.Z + world.Rows * world.TileSizeMeters; z += 16)
            {
                if(timer.Elapsed.TotalMilliseconds>2){yield return null;timer.Restart();}
                if (root.WorldHeight.NearestWaterBodyId(x,z)!=waterId ||
                    root.WorldHeight.WaterBoundaryDistance(x,z,out var surface)>-8f) continue;
                var holder = new GameObject("Free swimming school").transform;
                holder.SetParent(transform,false);
                var center = new Vector3(x,surface-.08f,z);
                holder.gameObject.SetActive(false);
                var school = new FishSchool { Parent=holder,Center=center,
                    Phase=(float)seed.NextDouble()*6.28f,SourceId=sourceId,SchoolSize=seed.Next(6,16) };
                schools.Add(school); RefreshFish(school);
                if (seed.Next(0,3)==0)
                {
                    var singleHolder = new GameObject("Solitary silver fish").transform;
                    singleHolder.SetParent(transform,false); singleHolder.gameObject.SetActive(false);
                    var single = new FishSchool { Parent=singleHolder,
                        Center=center+new Vector3(seed.Next(-3,4),-.03f,seed.Next(-3,4)),
                        Phase=(float)seed.NextDouble()*6.28f,SourceId=sourceId,SchoolSize=1,Solitary=true };
                    schools.Add(single); RefreshFish(single);
                }
            }
        }

        private Transform MakeFish(Transform parent, int variant = 0)
        {
            var view = WorldPropCatalog.Create(variant % 3 == 0 ? "fish" : "fish_swimming", parent).transform;
            view.localScale *= .85f + (variant % 5) * .08f;
            return view;
        }

        private static void MakeSilver(Transform fish)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in fish.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                var silver = new Color(.68f,.76f,.82f,1f);
                block.SetColor("_BaseColor",silver); block.SetColor("_Color",silver);
                block.SetFloat("_Metallic",.7f); block.SetFloat("_Smoothness",.78f);
                renderer.SetPropertyBlock(block);
            }
        }

        public static Quaternion SmoothSwimmingRotation(Quaternion current, Vector3 heading,
            float deltaSeconds, float degreesPerSecond = 38f)
        {
            if (heading.sqrMagnitude < .0001f) return current;
            return Quaternion.RotateTowards(current,Quaternion.LookRotation(heading.normalized,Vector3.up),
                Mathf.Max(0,deltaSeconds)*degreesPerSecond);
        }

        private void RefreshFish(FishSchool school)
        {
            var visible = Camera.main != null && (Camera.main.transform.position-school.Center).sqrMagnitude < 220f*220f;
            var count=school.SchoolSize;
            if(visible && school.Fish==null)
            {
                school.Fish=new Transform[count];school.Animators=new Animator[count];
                for(var n=0;n<count;n++)
                {
                    school.Fish[n]=MakeFish(school.Parent,n+(int)(school.Phase*10));
                    school.Fish[n].name=school.Solitary?"Solitary silver fish":"Swimming fish "+n;
                    if(school.Solitary)MakeSilver(school.Fish[n]);
                    school.Animators[n]=school.Fish[n].GetComponentInChildren<Animator>();
                }
            }
            if(root.Production.TryGetSite(school.SourceId,out var stock) && stock.SourceCapacity>0)
                count=(int)Math.Ceiling(count*(double)stock.RemainingAmount/stock.SourceCapacity);
            school.Parent.gameObject.SetActive(visible && count>0);
            if(!visible)return;
            var speed=school.Solitary?.135f:(.18f+.025f*Mathf.Sin(school.Phase));
            var t=fishTime*speed+school.Phase;
            var radius=school.Solitary?3.1f:2.1f;
            var drift=new Vector3(Mathf.Sin(t)*radius,0,Mathf.Cos(t*.83f+school.Phase)*radius);
            var heading=new Vector3(Mathf.Cos(t),0,-Mathf.Sin(t*.83f+school.Phase)*.83f).normalized;
            var side=Vector3.Cross(Vector3.up,heading);
            for(var i=0;i<school.Fish.Length;i++)
            {
                school.Fish[i].gameObject.SetActive(i<count); if(i>=count)continue;
                // Staggered neighbours remain over one fish length apart; cells never overlap.
                var lateral=(i%2==0?-1f:1f)+Mathf.Sin(t*1.3f+i*2f)*.18f;
                var trailing=(i/2-(school.SchoolSize+1)/4f)*1.25f+(i%2)*.3f;
                school.Fish[i].position=school.Center+drift+side*lateral+heading*trailing;
                var desired=heading+side*Mathf.Sin(t+i)*.12f;
                var turnDelta=root.Clock!=null && root.Clock.IsPaused?0f:Time.unscaledDeltaTime;
                school.Fish[i].rotation=SmoothSwimmingRotation(school.Fish[i].rotation,desired,turnDelta,
                    school.Solitary?26f:38f);
                if(school.Animators[i]!=null)school.Animators[i].speed=root.Clock==null || root.Clock.IsPaused?0:1;
            }
        }

        private void Update()
        {
            if (root?.Clock == null) return;
            if (!root.Clock.IsPaused) fishTime += Time.unscaledDeltaTime;
            schools.RemoveAll(x => x.Parent == null);
            foreach (var school in schools)
            {
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
