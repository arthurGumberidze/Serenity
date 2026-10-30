using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.World;
using Game.Presentation.Production;
using Unity.AI.Navigation;
using UnityEngine;
using Game.Presentation.Environment;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    // One scene adapter supplies reachable work stances without moving canonical resource identities.
    public sealed class WorldWorkApproaches : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private readonly Dictionary<string, Vector3> docks = new();
        private readonly Dictionary<StableEntityId, Vector3> fishStances = new();
        private readonly Dictionary<StableEntityId, Vector3> fishTargets = new();
        private readonly Dictionary<string, Vector3> catches = new();
        private readonly Dictionary<StableEntityId, Vector3> fishOutputs = new();
        private sealed class PierWorkplace
        {
            public string Key;
            public StableEntityId Building;
            public Vector3 Stance, Target, Output;
        }
        private readonly Dictionary<StableEntityId,List<PierWorkplace>> fishPiers = new();
        private readonly Dictionary<string,GameObject> dockViews = new();
        private readonly HashSet<StableEntityId> builtPiers = new();
        private readonly Dictionary<(StableEntityId worker,StableEntityId site),Vector3> workerFishOutputs = new();
        private float nextPierRefresh;
        private readonly Dictionary<(StableEntityId worker,StableEntityId site),Vector3> bankStances = new();
        private readonly Dictionary<(StableEntityId worker,StableEntityId site),Vector3> bankTargets = new();
        private readonly Dictionary<(StableEntityId worker,StableEntityId site),StableEntityId> bankTasks = new();
        private Material timber;
        private Material chipMaterial, stoneMaterial;
        private Mesh fragmentMesh;
        private float effectWindow;
        private int effectsThisWindow;
        private readonly Dictionary<StableEntityId, int> lastImpacts = new();
        public int WorkImpactBursts { get; private set; }
        private float nextSourceRefresh;
        private readonly Dictionary<StableEntityId, ProductionSourcePresenter> sources = new();
        private ProductionSourcePresenter Source(StableEntityId id)
        {
            if (Time.unscaledTime >= nextSourceRefresh)
            {
                sources.Clear();
                foreach (var source in FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None))
                    if (source.IsBound) sources[source.Site.Id] = source;
                nextSourceRefresh = Time.unscaledTime + 2f;
            }
            return sources.TryGetValue(id,out var found) ? found : null;
        }
        public void Initialize(LocalSceneCompositionRoot session)
        {
            root = session;
            root.Production.WaterBodyAt = p => root.WorldHeight.NearestWaterBodyId(p.X,p.Z);
            root.Production.IsWaterAt = p => root.WorldHeight.IsWater(p.X,p.Z);
            timber = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            timber.SetColor("_BaseColor", new Color(.34f,.19f,.085f));
            root.Production.WorkPosition = Resolve;
            root.Production.ManualFishingAccess = id => fishPiers.TryGetValue(id,out var piers) && piers.Any(PierOperational);
            root.Production.OutputPosition = (worker, site) => workerFishOutputs.TryGetValue((worker,site.Id),out var p) || fishOutputs.TryGetValue(site.Id, out p)
                ? new WorldPosition(p.x, p.y, p.z) : site.Position;
        }
        private bool PierOperational(PierWorkplace pier) => !pier.Building.IsValid ||
            root.Buildings.TryGet(pier.Building,out var building) && building.IsOperational;
        public void RegisterFish(TiledWorldResourceNode node, StableEntityId id, StableEntityId buildingId = default)
        {
            if (!docks.TryGetValue(node.SourceZoneId, out var stance))
            {
                var start = new Vector3(node.Position.X, node.Position.Y + .12f, node.Position.Z);
                var target = new Vector3(node.VisualPosition.X, node.VisualPosition.Y + .4f, node.VisualPosition.Z);
                var direction = target - start; direction.y = 0;
                if (direction.sqrMagnitude < 1f) return;
                var distance = direction.magnitude;
                direction.Normalize();
                // Fishing landing markers may sit well inland. Find the actual shore on their approach.
                for (var d = 0f; d <= distance; d += .25f)
                {
                    var point = start + direction * d;
                    if (!root.WorldHeight.IsWater(point.x, point.z)) continue;
                    start = point - direction * 1.5f;
                    break;
                }
                start.y = Mathf.Max(root.WorldHeight.SampleHeight(start.x, start.z) + .12f, target.y);
                var catchPoint = start - direction * 1.8f;
                catchPoint.y = root.WorldHeight.SampleHeight(catchPoint.x, catchPoint.z);
                catches[node.SourceZoneId] = catchPoint;
                var length = Mathf.Min(9f, Vector3.Distance(new Vector3(start.x,0,start.z),new Vector3(target.x,0,target.z)));
                length = Mathf.Max(3f, length);
                var end = start + direction.normalized * length;
                end.y = Mathf.Max(start.y, target.y);
                var dock = new GameObject("Wooden fishing pier " + node.SourceZoneId);
                dock.transform.SetParent(transform, false);
                dockViews[node.SourceZoneId]=dock;
                var forward = end - start;
                var pier = WorldPropCatalog.Create("pier", dock.transform);
                pier.transform.position = (start + end) * .5f + Vector3.up * .18f;
                pier.transform.rotation = Quaternion.LookRotation(forward);
                pier.transform.localScale = new Vector3(1f, 1f, length / 9f);
                // Navigation collision is independent of the imported decorative posts/boards.
                var deck = new GameObject("Walkable deck");
                deck.transform.SetParent(dock.transform, false);
                deck.transform.position = (start + end) * .5f + Vector3.up * .07f;
                deck.transform.rotation = Quaternion.LookRotation(forward);
                var deckCollider = deck.AddComponent<BoxCollider>();
                deckCollider.size = new Vector3(2.4f, .22f, length);
                if (buildingId.IsValid)
                    dock.AddComponent<Game.Presentation.Interaction.StableWorldHitProxy>()
                        .Bind(buildingId,Game.Domain.Work.ContextTargetKind.Building);
                var surface = dock.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true;
                surface.voxelSize = .08f;
                surface.minRegionArea = 0;
                Physics.SyncTransforms(); surface.BuildNavMesh();
                var link = dock.AddComponent<NavMeshLink>();
                var bank=start-direction.normalized;
                bank.y=root.WorldHeight.SampleHeight(bank.x,bank.z)+.02f;
                if(NavMesh.SamplePosition(bank,out var bankSurface,1.5f,NavMesh.AllAreas))bank=bankSurface.position;
                link.startPoint = bank;
                link.endPoint = start + direction.normalized * .8f + Vector3.up*.18f;
                link.width = 1.4f; link.bidirectional = true;
                stance = Vector3.Lerp(start,end,.85f) + Vector3.up * .16f;
                docks.Add(node.SourceZoneId,stance);
            }
            fishStances[id] = stance;
            fishOutputs[id] = catches[node.SourceZoneId];
            fishTargets[id] = new Vector3(node.VisualPosition.X,node.VisualPosition.Y,node.VisualPosition.Z);
            if(!fishPiers.TryGetValue(id,out var piers))fishPiers[id]=piers=new List<PierWorkplace>();
            if(!piers.Any(x=>x.Key==node.SourceZoneId))piers.Add(new PierWorkplace {
                Key=node.SourceZoneId,Building=buildingId,Stance=stance,Output=fishOutputs[id],Target=fishTargets[id] });
        }
        private WorldPosition Resolve(StableEntityId worker, ProductionSite site)
        {
            if (site.RecipeId.ToString() == "fish_water")
            {
                RefreshBuiltPiers();
                var key=(worker,site.Id);
                var taskId=root.Production.ActiveTasks.FirstOrDefault(x=>x.WorkerId==worker && x.SiteId==site.Id)?.Id ?? default;
                var requested=root.Production.RequestedFishingPiers.TryGetValue(worker,out var requestedPier);
                if(!requested && bankStances.TryGetValue(key,out var cached) && bankTasks.TryGetValue(key,out var previous) && previous==taskId)
                    return new WorldPosition(cached.x,cached.y,cached.z);
                if(!fishPiers.TryGetValue(site.Id,out var piers))return site.Position;
                var from=root.Tiers.GetRuntimeState(worker).Position;
                var origin=new Vector3(from.X,from.Y,from.Z);
                var pier=piers.Where(x=>PierOperational(x) && (!requested || x.Building==requestedPier)).OrderBy(x=>(x.Stance-origin).sqrMagnitude)
                    .FirstOrDefault(x => Game.Presentation.AI.ReachableNavMeshPoint.TryFind(origin,x.Stance,out var end) &&
                        (end-x.Stance).sqrMagnitude < 1f);
                if(pier==null)return site.Position;
                bankStances[key]=pier.Stance;bankTargets[key]=pier.Target;bankTasks[key]=taskId;
                workerFishOutputs[key]=pier.Output;
                return new WorldPosition(pier.Stance.x,pier.Stance.y,pier.Stance.z);
            }
            if (site.RecipeId.ToString() == "hunt_animal" && root.Production.Animals.TryGet(site.Id,out var animal) && !animal.Alive)
            {
                var from = root.Tiers.GetRuntimeState(worker).Position;
                var away = new Vector3(from.X-site.Position.X,0,from.Z-site.Position.Z).normalized;
                if (away.sqrMagnitude < .01f) away = Vector3.forward;
                var stance = new Vector3(site.Position.X,site.Position.Y,site.Position.Z)+away*.9f;
                return new WorldPosition(stance.x,root.WorldHeight.SampleHeight(stance.x,stance.z),stance.z);
            }
            if(site.Kind==ProductionSiteKind.BuildingWorkplace && root.Buildings.TryGet(site.OwnerBuildingId,out var workplaceBuilding))
                return root.ConstructionWorkTarget(workplaceBuilding);
            if (site.RecipeId.ToString() == "hunt_animal" || site.Kind != ProductionSiteKind.NaturalSource)
                return site.Position;
            if (!root.CharacterPresentations.TryGet(worker,out var person)) return site.Position;
            var source = Source(site.Id);
            Bounds bounds;
            // A tree crown is not its work surface. Stand beside the trunk, not under the leaf perimeter.
            // Batched trees have a proxy collider and no Renderer on their source object.
            if (site.RecipeId.ToString() == "chop_wood")
                bounds = source != null && source.TryGetComponent<CapsuleCollider>(out var trunk) ? trunk.bounds :
                    new Bounds(new Vector3(site.Position.X,site.Position.Y+1f,site.Position.Z), new Vector3(.7f,2f,.7f));
            else
            {
                if (source == null) return site.Position;
                var renderers = source.GetComponentsInChildren<Renderer>().Where(x => !(x is LineRenderer) && !(x is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0) return site.Position;
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            var direction = person.transform.position - bounds.center; direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
            direction.Normalize();
            var radius = Mathf.Min(bounds.extents.x / Mathf.Max(.001f,Mathf.Abs(direction.x)),
                bounds.extents.z / Mathf.Max(.001f,Mathf.Abs(direction.z)));
            var point = bounds.center + direction * (radius + .65f);
            point.y = root.WorldHeight.SampleHeight(point.x,point.z);
            return new WorldPosition(point.x,point.y,point.z);
        }
        private void LateUpdate()
        {
            if (root?.AiRuntime == null) return;
            RefreshBuiltPiers();
            foreach (var task in root.Production.ActiveTasks)
            {
                var movement = root.AiRuntime.Movements.FirstOrDefault(m => m.CharacterId == task.WorkerId);
                if (movement == null || !root.AiAgents.TryGet(task.WorkerId,out var agent) || agent.Phase != AiActionPhase.Working ||
                    !root.Production.TryGetSite(task.SiteId,out var site)) continue;
                var direction = new Vector3(site.Position.X,0,site.Position.Z)-movement.Presenter.transform.position;
                if (bankTargets.TryGetValue((task.WorkerId,site.Id),out var fishTarget))
                { direction=fishTarget-movement.Presenter.transform.position; movement.Presenter.SetFishingTarget(fishTarget); }
                else if (fishTargets.TryGetValue(site.Id, out fishTarget))
                { direction = fishTarget - movement.Presenter.transform.position; movement.Presenter.SetFishingTarget(fishTarget); }
                direction.y = 0;
                if (direction.sqrMagnitude > .01f) movement.Presenter.transform.rotation = Quaternion.LookRotation(direction);
                if (task.RecipeId.ToString() == "chop_wood" || task.RecipeId.ToString() == "gather_stone")
                {
                    var contact = new Vector3(site.Position.X,movement.Presenter.transform.position.y + 1f,site.Position.Z);
                    var source = Source(site.Id);
                    if (task.RecipeId.ToString() == "chop_wood" && source != null && source.TryGetComponent<CapsuleCollider>(out var trunk))
                        contact = trunk.ClosestPoint(movement.Presenter.transform.position + Vector3.up);
                    if (source != null && task.RecipeId.ToString() != "chop_wood")
                    {
                        var renderer = source.GetComponentsInChildren<Renderer>().FirstOrDefault(x => !(x is LineRenderer));
                        if (renderer != null) contact = renderer.bounds.ClosestPoint(movement.Presenter.transform.position + Vector3.up);
                    }
                    movement.Presenter.SetWorkContact(contact);
                    var sequence = movement.Presenter.WorkImpactSequence;
                    if (lastImpacts.TryGetValue(task.WorkerId, out var previous) && sequence != previous)
                        EmitWorkImpact(contact, task.RecipeId.ToString() == "chop_wood");
                    lastImpacts[task.WorkerId] = sequence;
                }
            }
        }
        private void RefreshBuiltPiers()
        {
            if(root?.Production==null||root.Buildings==null||Time.unscaledTime<nextPierRefresh)return;
            nextPierRefresh=Time.unscaledTime+1f;
            foreach(var building in root.Buildings.All.Where(x=>x.IsOperational&&x.DefinitionId.FamilyId.ToString()=="fishing_pier"))
            {
                if(builtPiers.Contains(building.Id)||!root.BuildingPresentations.TryGet(building.Id,out var view))continue;
                var start=view.transform.position;var forward=view.transform.forward;
                var target=start;var found=false;
                for(var distance=.5f;distance<=50 && !found;distance+=.5f)
                {
                    for(var sector=0;sector<16;sector++)
                    {
                        var direction=Quaternion.Euler(0,sector*22.5f,0)*forward;
                        var point=start+direction*distance;
                        if(!root.WorldDefinition.ContainsClosed(point.x,point.z)||!root.WorldHeight.IsWater(point.x,point.z))continue;
                        target=point+direction*3f;
                        if(!root.WorldDefinition.ContainsClosed(target.x,target.z))continue;
                        found=true;break;
                    }
                }
                if(!found||!root.WorldDefinition.TryGetTile(start.x,start.z,out var tile))continue;
                root.WorldHeight.WaterBoundaryDistance(target.x,target.z,out var surface);target.y=surface;
                var stock=root.Production.FishStockAt(new WorldPosition(target.x,target.y,target.z));
                if(stock==null)continue;
                RegisterFish(new TiledWorldResourceNode(0,tile,"fish_water",NaturalResourceBiome.Lake,
                    new WorldPosition(start.x,start.y,start.z),stock.SourceCapacity,"player:"+building.Id,
                    new WorldPosition(target.x,target.y,target.z)),stock.Id,building.Id);
                var preview=view.transform.Find("Authored pier preview");
                if(preview!=null)preview.gameObject.SetActive(false);
                builtPiers.Add(building.Id);
            }
            foreach(var list in fishPiers.Values)
            foreach(var pier in list.Where(x=>x.Building.IsValid&&!PierOperational(x)).ToArray())
            {
                if(dockViews.TryGetValue(pier.Key,out var view)){Destroy(view);dockViews.Remove(pier.Key);}
                docks.Remove(pier.Key);catches.Remove(pier.Key);builtPiers.Remove(pier.Building);list.Remove(pier);
                bankTasks.Clear();
            }
        }
        private void EmitWorkImpact(Vector3 contact, bool wood)
        {
            if (Time.unscaledTime >= effectWindow) { effectWindow = Time.unscaledTime+1f; effectsThisWindow = 0; }
            var camera = Camera.main;
            if (effectsThisWindow >= 16 || camera == null || (camera.transform.position-contact).sqrMagnitude > 10000f) return;
            effectsThisWindow++; WorkImpactBursts++;
            if (fragmentMesh == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragmentMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);
                chipMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                chipMaterial.SetColor("_BaseColor",new Color(.7f,.42f,.18f));
                stoneMaterial = new Material(chipMaterial); stoneMaterial.SetColor("_BaseColor",new Color(.48f,.49f,.47f));
            }
            var effect = new GameObject(wood ? "Wood chips impact" : "Stone fragments impact");
            effect.transform.position = contact;
            effect.transform.rotation = Quaternion.Euler(-65,0,0);
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop=false; main.duration=.15f; main.startLifetime=.65f;
            main.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,2.5f); main.startSize=new ParticleSystem.MinMaxCurve(.025f,.075f);
            main.gravityModifier=1f; main.maxParticles=12;
            var emission=particles.emission; emission.rateOverTime=0;
            var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=65; shape.radius=.06f;
            var renderer=particles.GetComponent<ParticleSystemRenderer>(); renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=fragmentMesh; renderer.sharedMaterial=wood ? chipMaterial : stoneMaterial;
            particles.Play(); particles.Emit(10); Destroy(effect,1f);
        }
        private void OnDestroy()
        {
            if(timber != null) Destroy(timber);
            if(chipMaterial != null) Destroy(chipMaterial);
            if(stoneMaterial != null) Destroy(stoneMaterial);
        }
    }
}
