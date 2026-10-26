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
            timber = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            timber.SetColor("_BaseColor", new Color(.34f,.19f,.085f));
            root.Production.WorkPosition = Resolve;
            root.Production.OutputPosition = (worker, site) => fishOutputs.TryGetValue(site.Id, out var p)
                ? new WorldPosition(p.x, p.y, p.z) : site.Position;
        }
        public void RegisterFish(TiledWorldResourceNode node, StableEntityId id)
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
                var surface = dock.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true;
                surface.voxelSize = .08f;
                surface.minRegionArea = 0;
                Physics.SyncTransforms(); surface.BuildNavMesh();
                var link = dock.AddComponent<NavMeshLink>();
                link.startPoint = new Vector3(start.x,start.y-.12f,start.z)-direction.normalized;
                link.endPoint = start + direction.normalized * .8f + Vector3.up*.12f;
                link.width = 1.4f; link.bidirectional = true;
                stance = Vector3.Lerp(start,end,.85f) + Vector3.up * .16f;
                docks.Add(node.SourceZoneId,stance);
            }
            fishStances[id] = stance;
            fishOutputs[id] = catches[node.SourceZoneId];
            fishTargets[id] = new Vector3(node.VisualPosition.X,node.VisualPosition.Y,node.VisualPosition.Z);
        }
        private WorldPosition Resolve(StableEntityId worker, ProductionSite site)
        {
            if (site.RecipeId.ToString() == "fish_water" && fishStances.TryGetValue(site.Id,out var dock))
                return new WorldPosition(dock.x,dock.y,dock.z);
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
            foreach (var task in root.Production.ActiveTasks)
            {
                var movement = root.AiRuntime.Movements.FirstOrDefault(m => m.CharacterId == task.WorkerId);
                if (movement == null || !root.AiAgents.TryGet(task.WorkerId,out var agent) || agent.Phase != AiActionPhase.Working ||
                    !root.Production.TryGetSite(task.SiteId,out var site)) continue;
                var direction = new Vector3(site.Position.X,0,site.Position.Z)-movement.Presenter.transform.position;
                if (fishTargets.TryGetValue(site.Id,out var fishTarget)) direction = fishTarget-movement.Presenter.transform.position;
                if (fishTargets.TryGetValue(site.Id,out fishTarget)) movement.Presenter.SetFishingTarget(fishTarget);
                if (fishStances.TryGetValue(site.Id, out var stance) &&
                    Vector2.Distance(new Vector2(stance.x, stance.z), new Vector2(movement.Presenter.transform.position.x,
                        movement.Presenter.transform.position.z)) < 1.5f &&
                    NavMesh.SamplePosition(stance, out var deckPoint, .35f, NavMesh.AllAreas))
                {
                    // Keep the work stance on the elevated deck, including after a crowd-detail transition.
                    if (movement.Agent != null && movement.Agent.enabled && movement.Agent.isOnNavMesh)
                        movement.Agent.Warp(deckPoint.position);
                    else movement.Presenter.transform.position = deckPoint.position;
                }
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
