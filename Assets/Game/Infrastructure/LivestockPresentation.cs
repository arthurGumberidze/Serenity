using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;

namespace Game.Infrastructure
{
    // One bounded presentation scheduler for saved domestic animals. Never owns health or births.
    public sealed class LivestockPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private GameObject sheep;
        private CropArtCatalog art;
        private float nextRefresh;
        private readonly Dictionary<StableEntityId, GameObject> views = new();
        private readonly Dictionary<StableEntityId, Vector3> goals = new();
        private readonly Dictionary<StableEntityId, float> nextWander = new();
        private readonly Dictionary<StableEntityId, Vector3> renderedTargets = new();
        private readonly HashSet<StableEntityId> adultViews = new();
        public void Initialize(LocalSceneCompositionRoot session)
        { root = session; art = Resources.Load<CropArtCatalog>("World/CropArt"); sheep = art?.Sheep; }

        private void Update()
        {
            if (root?.Production == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            foreach(var penBuilding in root.Buildings.All.Where(x=>x.IsOperational && x.DefinitionId.ToString()=="animal_pasture"))
                root.Production.EstablishSheepPasture(penBuilding.Id,penBuilding.OwnerSideId);
            var states = root.Production.Animals.States.Where(x => x.Domestic && !x.Harvested).ToArray();
            var ids = new HashSet<StableEntityId>(states.Select(x => x.Id));
            foreach (var id in views.Keys.Where(x => !ids.Contains(x)).ToArray())
            { Destroy(views[id]); views.Remove(id); goals.Remove(id); nextWander.Remove(id); renderedTargets.Remove(id); adultViews.Remove(id); }
            foreach (var group in states.GroupBy(x => x.Home))
            {
                CropPlotPresentation pen = null;
                if (root.BuildingPresentations.TryGet(group.Key, out var building))
                    pen = building.GetComponent<CropPlotPresentation>();
                pen?.Refresh();
                var slot = 0;
                foreach (var animal in group.OrderBy(x => x.Id))
                {
                    if (!root.Production.TryGetSite(animal.Id, out var site) || sheep == null) continue;
                    var age = (float)animal.GrowthFraction(root.Clock.State.CalendarTicks,root.Production.Animals.Policy.LivestockMaturityDays);
                    if (!views.TryGetValue(animal.Id, out var view))
                    {
                        view = new GameObject("Domestic " + animal.Species + " " + animal.Id); view.transform.SetParent(transform, false);
                        var model = CreateModel(view,animal.Species,age<1);
                        if(age>=1)adultViews.Add(animal.Id);
                        var collider = view.AddComponent<CapsuleCollider>();
                        collider.radius = .4f; collider.height = 1.2f; collider.center = Vector3.up * .6f;
                        var obstacle = view.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                        obstacle.center = Vector3.up * .6f; obstacle.radius = .4f; obstacle.height = 1.2f;
                        view.AddComponent<ProductionSourcePresenter>().Bind(site, Color.white, true);
                        view.AddComponent<AnimalHealthPresentation>().Bind(root.Production.Animals, animal.Id, model.transform);
                        view.transform.position = new Vector3(site.Position.X,site.Position.Y,site.Position.Z);
                        views.Add(animal.Id, view);
                    }
                    if(animal.Alive && age>=1 && adultViews.Add(animal.Id))
                    {
                        var old=view.transform.Find("Animal model");
                        if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
                        var model=CreateModel(view,animal.Species,false);
                        view.GetComponent<AnimalHealthPresentation>().Bind(root.Production.Animals,animal.Id,model.transform);
                    }
                    var grazingSlot = slot++;
                    var position = new Vector3(site.Position.X, site.Position.Y, site.Position.Z);
                    if (animal.Alive && pen != null)
                        position = Wander(animal.Id, position, pen, grazingSlot, root.Clock.IsPaused ? 0f : .15f);
                    site.MoveAnimal(new WorldPosition(position.x, position.y, position.z));
                    renderedTargets[animal.Id] = position;
                    if(!animal.Alive)view.transform.position=position;
                    // A dead animal is an interaction target, not a navigation obstacle blocking its own butcher.
                    var body = view.GetComponent<CapsuleCollider>();
                    if (body != null) body.isTrigger = !animal.Alive;
                    var blocker = view.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                    if (blocker != null) blocker.enabled = animal.Alive;
                    var source = view.GetComponent<ProductionSourcePresenter>();
                    if (!ReferenceEquals(source.Site, site)) source.Bind(site, Color.white, true);
                    if (root.Clock != null)
                    {
                        var growth = Mathf.Lerp(.36f,1f,age);
                        view.transform.localScale = Vector3.one * growth;
                    }
                    var animator = view.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        foreach (var parameter in animator.parameters)
                            if (parameter.name == "Moving" && parameter.type == AnimatorControllerParameterType.Bool)
                                animator.SetBool("Moving", animal.Alive && goals.TryGetValue(animal.Id,out var goal) &&
                                    (goal-view.transform.position).sqrMagnitude > .08f);
                        animator.speed = root.Clock.IsPaused ? 0f : .65f;
                    }
                    view.GetComponent<AnimalHealthPresentation>().Refresh();
                }
            }
        }

        private GameObject CreateModel(GameObject view,string species,bool baby)
        {
            var model=WorldMapScenery.Instantiate(art?.Animal(species,baby) ?? sheep,view.transform);
            model.name="Animal model";model.SetActive(true);
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)return model;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var height=species=="cow"?1.6f:species=="horse"?1.8f:species=="duck"||species=="chicken"?.45f:1f;
            var scale=height/Mathf.Max(.1f,bounds.size.y/view.transform.lossyScale.y);
            var offset=view.transform.InverseTransformVector(new Vector3(bounds.center.x-view.transform.position.x,
                bounds.min.y-view.transform.position.y,bounds.center.z-view.transform.position.z));
            model.transform.localScale*=scale;
            model.transform.localPosition-=offset*scale;
            return model;
        }

        private void LateUpdate()
        {
            if(root?.Clock==null || root.Clock.IsPaused)return;
            foreach(var pair in renderedTargets)
            {
                if(!views.TryGetValue(pair.Key,out var view)||view==null)continue;
                var direction=pair.Value-view.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.0001f)
                    view.transform.rotation=Quaternion.RotateTowards(view.transform.rotation,
                        Quaternion.LookRotation(direction),Time.unscaledDeltaTime*180f);
                view.transform.position=Vector3.MoveTowards(view.transform.position,pair.Value,Time.unscaledDeltaTime*.65f);
            }
        }

        private Vector3 Wander(StableEntityId id, Vector3 current, CropPlotPresentation pen, int slot, float delta)
        {
            var area = pen.transform;
            if (!goals.TryGetValue(id,out var goal) || !nextWander.TryGetValue(id,out var retry) || Time.unscaledTime >= retry)
            {
                var bytes = id.ToGuid().ToByteArray();
                var seed = BitConverter.ToInt32(bytes,0) ^ (int)(Time.unscaledTime*17f);
                var random = new System.Random(seed);
                var local = new Vector3(((float)random.NextDouble()*2-1)*pen.WidthMeters*.42f,0,
                    ((float)random.NextDouble()*2-1)*pen.DepthMeters*.42f);
                goal = area.TransformPoint(local);
                goal = root.TiledWorld.ProjectToGround(goal,.05f);
                goals[id] = goal;
                nextWander[id] = Time.unscaledTime + 2f + (float)random.NextDouble()*4f;
            }
            var next = Vector3.MoveTowards(current,goal,delta*.55f);
            next.y = root.TiledWorld.ProjectToGround(next,.05f).y;
            var localNext = area.InverseTransformPoint(next);
            if (Mathf.Abs(localNext.x)>pen.WidthMeters*.48f || Mathf.Abs(localNext.z)>pen.DepthMeters*.48f)
            {
                localNext.x=Mathf.Clamp(localNext.x,-pen.WidthMeters*.42f,pen.WidthMeters*.42f);
                localNext.z=Mathf.Clamp(localNext.z,-pen.DepthMeters*.42f,pen.DepthMeters*.42f);
                goals.Remove(id);return root.TiledWorld.ProjectToGround(area.TransformPoint(localNext),.05f);
            }
            return next;
        }
        private void OnDestroy()
        { foreach (var view in views.Values) if (view != null) Destroy(view); }
    }
}
