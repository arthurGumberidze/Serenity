using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.Domain.Production;
using Game.Simulation.Production;
using Game.ECS.Tier2;
using Game.Presentation.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    /// <summary>Tier projection adapter over one canonical WildlifePopulationService.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapWildlifePresenter : MonoBehaviour
    {
        private readonly Dictionary<StableEntityId, GameObject> tier1Views =
            new Dictionary<StableEntityId, GameObject>();
        private WildlifePopulationService population;
        private Tier2Runtime tier2;
        private WorldMapWildlifeArtCatalog art;
        private Transform viewRoot;
        private bool shuttingDown;
        private float movementTime;
        private ProductionService production;
        private LocalSceneCompositionRoot session;
        private IReadOnlyDictionary<string, WildlifeEscapePolicy> escapePolicies;
        private sealed class EscapeRoute
        {
            public int Sequence, Attempt, Corner;
            public float RetryAt;
            public Vector3[] Corners;
        }
        private readonly Dictionary<StableEntityId, EscapeRoute> escapeRoutes = new();

        public void ConfigureHealth(ProductionService service, LocalSceneCompositionRoot world = null)
        {
            if (production != null) production.Animals.Damaged -= OnDamaged;
            production = service;
            session = world;
            escapePolicies = AnimalHuntingConfig.LoadEscape();
            production.Animals.Damaged += OnDamaged;
            if (population != null) population.IsAlive = production.Animals.IsAlive;
        }

        private void OnDamaged(HuntingImpact impact)
        {
            if (population == null || !population.TryGet(impact.Animal,out var state) ||
                !escapePolicies.TryGetValue(state.SpeciesId,out var policy)) return;
            var threat = state.Position;
            if (session != null && session.CharacterPresentations.TryGet(impact.Hunter,out var hunter))
            {
                var p = hunter.transform.position; threat = new WorldPosition(p.x,p.y,p.z);
            }
            population.BeginFlee(impact.Animal,threat,session?.Clock.State.CalendarTicks ?? 0,policy);
            escapeRoutes.Remove(impact.Animal);
        }
        private readonly Collider[] obstacles = new Collider[64];

        public WildlifePopulationService Population => population;
        public int Tier1ViewCount => tier1Views.Count;
        public int Tier2EntityCount => tier2?.Wildlife.EntityCount ?? 0;
        public int Tier3Count => population?.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier3) ?? 0;
        public IReadOnlyList<StableEntityId> Tier1Ids => tier1Views.Keys.OrderBy(value => value).ToArray();

        public void Initialize(WildlifePopulationService wildlife, Tier2Runtime tier2Runtime,
            WorldMapWildlifeArtCatalog artCatalog, WorldPosition focus)
        {
            if (population != null) throw new InvalidOperationException("Wildlife presenter is already initialized.");
            population = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
            if (production != null) population.IsAlive = production.Animals.IsAlive;
            tier2 = tier2Runtime ?? throw new ArgumentNullException(nameof(tier2Runtime));
            art = artCatalog ?? throw new ArgumentNullException(nameof(artCatalog));
            foreach (var species in population.Plan.Definition.Species)
                if (art.For(species.Id)?.Prefab == null)
                    throw new InvalidOperationException("Missing audited wildlife prefab for " + species.Id);
            var root = new GameObject("World Map V2 Wildlife Tier1");
            root.transform.SetParent(transform, false);
            viewRoot = root.transform;
            population.RebalanceTiers(focus);
            Reconcile();
        }

        public void Rebalance(WorldPosition focus)
        {
            RequireInitialized();
            population.RebalanceTiers(focus);
            Reconcile();
        }

        public void Transition(StableEntityId id, CharacterSimulationTier tier)
        {
            RequireInitialized();
            population.Transition(id, tier);
            Reconcile();
        }

        public void AdvanceTo(long calendarTicks)
        {
            RequireInitialized();
            population.AdvanceTo(calendarTicks);
            Reconcile();
        }

        public int ActiveProjectionCount(StableEntityId id)
        {
            var count = tier1Views.ContainsKey(id) ? 1 : 0;
            if (tier2 != null && tier2.Wildlife.IsActive(id)) count++;
            return count;
        }

        public void Reconcile()
        {
            RequireInitialized();
            var states = population.Animals.ToDictionary(value => value.Id);
            foreach (var state in states.Values.ToArray())
            {
                if (production == null) continue;
                if (production.Animals.TryGet(state.Id, out var health) && health.Harvested)
                { population.Remove(state.Id); states.Remove(state.Id); continue; }
                if (!production.TryGetSite(state.Id, out var site))
                {
                    site = ProductionSite.Restore(new ProductionSiteState(state.Id,
                        new ProductionRecipeId("hunt_animal"), ProductionSiteKind.NaturalSource,
                        state.Position, 1, 1, CropPlotPhase.Unplanted, 0, 0));
                    production.AddSite(site, state.SpeciesId);
                }
                site.MoveAnimal(state.Position);
            }
            foreach (var id in tier1Views.Keys.Where(id => !states.TryGetValue(id, out var state) ||
                         state.Tier != CharacterSimulationTier.Tier1).ToArray())
            {
                Destroy(tier1Views[id]);
                tier1Views.Remove(id);
            }
            foreach (var id in tier2.Wildlife.ActiveIds.Where(id => !states.TryGetValue(id, out var state) ||
                         state.Tier != CharacterSimulationTier.Tier2).ToArray()) tier2.Wildlife.Dematerialize(id);

            foreach (var state in states.Values.OrderBy(value => value.Id))
            {
                if (state.Tier == CharacterSimulationTier.Tier1)
                {
                    if (!tier1Views.TryGetValue(state.Id, out var view))
                    {
                        var entry = art.For(state.SpeciesId);
                        // Imported animals can have mirrored root transforms. Keep picking geometry on
                        // a positive-scale authority root and preserve the model transform underneath it.
                        view = new GameObject();
                        view.transform.SetParent(viewRoot, false);
                        var model = WorldMapScenery.Instantiate(entry.Prefab, view.transform);
                        // Vendor prefabs contain positions from their demonstration scene.
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.identity;
                        WorldMapWildlifeView.AlignVisibleGeometry(model, view.transform.position);
                        view.name = "WM08 " + state.SpeciesId + " " + state.Id;
                        view.transform.localScale *= entry.Scale;
                        foreach (var animator in view.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
                        var animalView = view.AddComponent<WorldMapWildlifeView>();
                        animalView.Bind(state.Id, state.SpeciesId);
                        animalView.ConfigureGrounding(model.transform);
                        if (production != null) view.AddComponent<AnimalHealthPresentation>().Bind(
                            production.Animals, state.Id, model.transform);
                        tier1Views.Add(state.Id, view);
                    }
                    view.transform.position = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
                    view.GetComponent<AnimalHealthPresentation>()?.Refresh();
                }
                else if (state.Tier == CharacterSimulationTier.Tier2)
                {
                    if (!tier2.Wildlife.IsActive(state.Id)) tier2.Wildlife.Materialize(state);
                    else tier2.Wildlife.Refresh(state);
                }
            }
        }

        public void Shutdown()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            if (production != null) production.Animals.Damaged -= OnDamaged;
            if (tier2 != null)
                foreach (var id in tier2.Wildlife.ActiveIds) tier2.Wildlife.Dematerialize(id);
            foreach (var view in tier1Views.Values) if (view != null) Destroy(view);
            tier1Views.Clear();
            population = null;
            tier2 = null;
        }

        private void OnDestroy() { if (!shuttingDown) Shutdown(); }

        private void LateUpdate()
        {
            if (population == null) return;
            foreach (var pair in tier1Views)
                if (population.IsAlive(pair.Key))
                    pair.Value.GetComponent<WorldMapWildlifeView>().GroundAnimatedFeet(pair.Value.transform.position.y);
        }

        public void MoveVisible(float delta, TiledWorldHeightSampler heights)
        {
            if (population == null || heights == null) return;
            foreach (var view in tier1Views.Values)
                view.GetComponent<WorldMapWildlifeView>().SetLocomotion(0f, delta <= 0f);
            if (delta <= 0f) return;
            delta = Mathf.Min(delta, .15f);
            movementTime += delta;
            var tick = session?.Clock.State.CalendarTicks ?? 0;
            population.CalmExpired(tick);
            foreach (var state in population.Animals.Where(x=>x.IsFleeing(tick) && population.IsAlive(x.Id)))
                MoveEscape(state,delta,heights);
            foreach (var pair in tier1Views.OrderBy(value => value.Key))
            {
                var state = population.Get(pair.Key);
                pair.Value.GetComponent<AnimalHealthPresentation>()?.Refresh();
                if (!population.IsAlive(state.Id)) continue;
                if (state.IsFleeing(tick)) continue;
                var home = population.Plan.HabitatGraph.Node(state.HabitatNodeId).Position;
                if(state.SpeciesId=="cow")
                    home=population.Plan.Individuals.FirstOrDefault(x=>x.Id==state.Id)?.Position ?? home;
                var phase = (pair.Key.ToGuid().ToByteArray()[0] / 255f) * Mathf.PI * 2f;
                var roam=state.SpeciesId=="cow"?8f:32f;
                var target = new Vector3(home.X + Mathf.Sin(movementTime * .08f + phase) * roam, 0f,
                    home.Z + Mathf.Cos(movementTime * .07f + phase) * roam);
                var previous = pair.Value.transform.position;
                target.y = previous.y;
                var next = Vector3.MoveTowards(previous, target, delta * .7f);
                if (!heights.Definition.ContainsClosed(next.x, next.z) || !heights.IsBuildable(next.x, next.z, 34f)) continue;
                next.y = heights.SampleHeight(next.x, next.z);
                var count = Physics.OverlapSphereNonAlloc(next + Vector3.up * .7f, .8f, obstacles, ~0,
                    QueryTriggerInteraction.Collide);
                var blocked = count == obstacles.Length;
                for (var i = 0; i < count && !blocked; i++)
                {
                    var obstacle = obstacles[i];
                    if (obstacle is TerrainCollider || obstacle.transform.IsChildOf(pair.Value.transform)) continue;
                    blocked = !obstacle.isTrigger || obstacle.GetComponentInParent<WorldMapWildlifeView>() != null ||
                        obstacle.GetComponentInParent<Game.Presentation.Production.ProductionSourcePresenter>() != null ||
                        obstacle.GetComponentInParent<Game.Presentation.Characters.CharacterPresenter>() != null;
                }
                if (blocked || !population.TryMove(pair.Key, new WorldPosition(next.x, next.y, next.z))) continue;
                pair.Value.transform.position = next;
                pair.Value.GetComponent<WorldMapWildlifeView>().SetLocomotion(Vector3.Distance(previous, next) / delta);
                if (production != null && production.TryGetSite(pair.Key, out var site))
                    site.MoveAnimal(new WorldPosition(next.x, next.y, next.z));
                var direction = next - previous; direction.y = 0f;
                if (direction.sqrMagnitude > .000001f)
                    pair.Value.transform.rotation = Quaternion.RotateTowards(pair.Value.transform.rotation,
                        Quaternion.LookRotation(direction), delta * 100f);
            }
        }

        private void MoveEscape(WildlifeIndividualState state, float delta, TiledWorldHeightSampler heights)
        {
            if (!escapePolicies.TryGetValue(state.SpeciesId,out var policy)) return;
            if (!escapeRoutes.TryGetValue(state.Id,out var route) || route.Sequence != state.FleeSequence)
                escapeRoutes[state.Id] = route = new EscapeRoute { Sequence = state.FleeSequence };
            var start = new Vector3(state.Position.X,state.Position.Y,state.Position.Z);
            if (route.Corners == null || route.Corner >= route.Corners.Length)
            {
                if (movementTime < route.RetryAt) return;
                route.RetryAt = movementTime+.4f;
                foreach (var candidate in WildlifeEscape.Candidates(state,policy,route.Attempt++))
                {
                    if (!heights.Definition.ContainsClosed(candidate.X,candidate.Z) ||
                        !heights.IsBuildable(candidate.X,candidate.Z,30f)) continue;
                    var target = new Vector3(candidate.X,heights.SampleHeight(candidate.X,candidate.Z),candidate.Z);
                    Vector3[] corners;
                    if (NavMesh.SamplePosition(start,out var origin,2f,NavMesh.AllAreas) &&
                        NavMesh.SamplePosition(target,out var destination,2f,NavMesh.AllAreas))
                    {
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(origin.position,destination.position,NavMesh.AllAreas,path) ||
                            path.status != NavMeshPathStatus.PathComplete) continue;
                        corners = path.corners;
                    }
                    else
                    {
                        // Far tiers have no NavMesh tiles. The same terrain policy still rejects water/cliffs.
                        if (tier1Views.ContainsKey(state.Id)) continue;
                        corners = new[] { start,target };
                    }
                    var valid = corners.Length>1;
                    for (var i=1; valid && i<corners.Length; i++)
                        valid = EscapeSegmentIsDry(corners[i-1],corners[i],heights);
                    if (!valid) continue;
                    route.Corners = corners; route.Corner=1; break;
                }
                if (route.Corners == null || route.Corner >= route.Corners.Length) return;
            }
            var next = Vector3.MoveTowards(start,route.Corners[route.Corner],delta*policy.Speed*
                (session?.Clock.State.SpeedMultiplier ?? 1));
            if (!EscapeSegmentIsDry(start,next,heights)) { route.Corners=null; return; }
            next.y = heights.SampleHeight(next.x,next.z);
            tier1Views.TryGetValue(state.Id,out var view);
            var count = Physics.OverlapSphereNonAlloc(next+Vector3.up*.8f,.55f,obstacles,~0,QueryTriggerInteraction.Ignore);
            var blocked = count == obstacles.Length;
            for (var i=0; i<count && !blocked; i++)
                blocked = !(obstacles[i] is TerrainCollider) && (view==null || !obstacles[i].transform.IsChildOf(view.transform));
            if (blocked || !population.TryMove(state.Id,new WorldPosition(next.x,next.y,next.z),1.4f))
            { route.Corners=null; return; }
            if (view != null)
            {
                view.transform.position=next;
                view.GetComponent<WorldMapWildlifeView>().SetLocomotion(Vector3.Distance(start, next) / delta);
                var direction=next-start; direction.y=0;
                if (direction.sqrMagnitude>.00001f) view.transform.rotation=Quaternion.LookRotation(direction);
            }
            if (production.TryGetSite(state.Id,out var site)) site.MoveAnimal(new WorldPosition(next.x,next.y,next.z));
            if ((next-route.Corners[route.Corner]).sqrMagnitude<.16f) route.Corner++;
        }

        private static bool EscapeSegmentIsDry(Vector3 start,Vector3 end,TiledWorldHeightSampler heights)
        {
            var steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/.6f));
            for(var i=0;i<=steps;i++)
            {
                var point=Vector3.Lerp(start,end,i/(float)steps);
                if(!heights.Definition.ContainsClosed(point.x,point.z) || !heights.IsBuildable(point.x,point.z,30f)) return false;
            }
            return true;
        }

        private void RequireInitialized()
        {
            if (population == null || tier2 == null) throw new InvalidOperationException("Wildlife presenter is not initialized.");
        }
    }
}
