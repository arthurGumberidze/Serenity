using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.ECS.Tier2;
using Game.Presentation.Environment;
using UnityEngine;

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
                        view = Instantiate(entry.Prefab, viewRoot);
                        view.name = "WM08 " + state.SpeciesId + " " + state.Id;
                        view.transform.localScale *= entry.Scale;
                        view.AddComponent<WorldMapWildlifeView>().Bind(state.Id, state.SpeciesId);
                        tier1Views.Add(state.Id, view);
                    }
                    view.transform.position = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
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
            if (tier2 != null)
                foreach (var id in tier2.Wildlife.ActiveIds) tier2.Wildlife.Dematerialize(id);
            foreach (var view in tier1Views.Values) if (view != null) Destroy(view);
            tier1Views.Clear();
            population = null;
            tier2 = null;
        }

        private void OnDestroy() { if (!shuttingDown) Shutdown(); }

        private void RequireInitialized()
        {
            if (population == null || tier2 == null) throw new InvalidOperationException("Wildlife presenter is not initialized.");
        }
    }
}
