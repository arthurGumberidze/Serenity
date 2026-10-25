using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.World;
using Game.Presentation.Production;
using Unity.AI.Navigation;
using UnityEngine;
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
        private Material timber;
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
                var length = Mathf.Min(9f, Vector3.Distance(new Vector3(start.x,0,start.z),new Vector3(target.x,0,target.z)));
                length = Mathf.Max(3f, length);
                var end = start + direction.normalized * length;
                end.y = Mathf.Max(start.y, target.y);
                var dock = new GameObject("Wooden fishing pier " + node.SourceZoneId);
                dock.transform.SetParent(transform, false);
                var forward = end - start;
                var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = "Walkable deck"; deck.transform.SetParent(dock.transform, false);
                deck.transform.position = (start + end) * .5f;
                deck.transform.rotation = Quaternion.LookRotation(forward);
                deck.transform.localScale = new Vector3(2.4f,.22f,forward.magnitude);
                deck.GetComponent<Renderer>().sharedMaterial = timber;
                // Real plank seams and support posts, all sharing one material.
                for (var d = 0f; d <= length; d += .55f)
                {
                    var center = Vector3.Lerp(start,end,d/length);
                    var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plank.transform.SetParent(dock.transform,false);
                    plank.transform.position = center + Vector3.up * .14f;
                    plank.transform.rotation = deck.transform.rotation;
                    plank.transform.localScale = new Vector3(2.5f,.08f,.51f);
                    plank.GetComponent<Renderer>().sharedMaterial = timber;
                    Destroy(plank.GetComponent<Collider>());
                }
                var right = Vector3.Cross(Vector3.up,direction.normalized);
                for (var side = -1; side <= 1; side += 2)
                for (var d = 0f; d <= length; d += 3f)
                {
                    var center = Vector3.Lerp(start,end,d/length) + right * side * 1.12f;
                    var bottom = root.WorldHeight.SampleHeight(center.x,center.z) - .25f;
                    var height = Mathf.Max(.8f,center.y - bottom + .65f);
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.transform.SetParent(dock.transform,false);
                    post.transform.position = new Vector3(center.x,bottom+height*.5f,center.z);
                    post.transform.localScale = new Vector3(.18f,height,.18f);
                    post.GetComponent<Renderer>().sharedMaterial = timber;
                    Destroy(post.GetComponent<Collider>());
                }
                var surface = dock.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                Physics.SyncTransforms(); surface.BuildNavMesh();
                var link = dock.AddComponent<NavMeshLink>();
                link.startPoint = new Vector3(start.x,start.y-.12f,start.z)-direction.normalized;
                link.endPoint = start + direction.normalized * .8f + Vector3.up*.12f;
                link.width = 1.4f; link.bidirectional = true;
                stance = Vector3.Lerp(start,end,.85f) + Vector3.up * .16f;
                docks.Add(node.SourceZoneId,stance);
            }
            fishStances[id] = stance;
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
                }
            }
        }
        private void OnDestroy() { if(timber != null) Destroy(timber); }
    }
}
