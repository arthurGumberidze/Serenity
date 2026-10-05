using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Presentation.AI;
using Game.Simulation.AI;
using Game.Simulation.Tiers;
using UnityEngine;

namespace Game.Presentation.Characters
{
    /// <summary>
    /// Tier 1 structural adapter. Demotion cancels NavMesh-only execution through the U10 scheduler;
    /// U11 manual orders are requeued by their action cancellation, claims are released, and inventory stays canonical.
    /// </summary>
    public sealed class Tier1CharacterAdapter : ICharacterTierAdapter
    {
        private readonly CharacterPresentationCatalog catalog;
        private readonly CharacterPresentationRegistry presentations;
        private readonly CharacterPresentationSpawner spawner;
        private readonly Tier1AiRuntimeDriver aiRuntime;
        private readonly Tier1AiAgentRegistry aiAgents;
        private readonly Dictionary<StableEntityId, CharacterRuntimeState> continuity =
            new Dictionary<StableEntityId, CharacterRuntimeState>();

        public Tier1CharacterAdapter(CharacterPresentationCatalog presentationCatalog,
            CharacterPresentationRegistry presentationRegistry, CharacterPresentationSpawner presentationSpawner,
            Tier1AiRuntimeDriver runtime, Tier1AiAgentRegistry agentRegistry)
        {
            catalog = presentationCatalog != null ? presentationCatalog : throw new ArgumentNullException(nameof(presentationCatalog));
            presentations = presentationRegistry ?? throw new ArgumentNullException(nameof(presentationRegistry));
            spawner = presentationSpawner ?? throw new ArgumentNullException(nameof(presentationSpawner));
            aiRuntime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            aiAgents = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        }

        public CharacterSimulationTier Tier => CharacterSimulationTier.Tier1;

        public bool IsActive(StableEntityId characterId) =>
            presentations.TryGet(characterId, out var presenter) && presenter != null && presenter.IsBound;

        public CharacterRuntimeState Capture(StableEntityId characterId)
        {
            if (!presentations.TryGet(characterId, out var presenter) || presenter == null || !presenter.IsBound)
                throw new InvalidOperationException("Tier 1 presentation is not active.");
            var unityPosition = presenter.transform.position;
            var position = new WorldPosition(unityPosition.x, unityPosition.y, unityPosition.z);
            var hunger = 0d;
            var energy = 1d;
            if (aiAgents.TryGet(characterId, out var agent))
            {
                hunger = agent.Needs.Hunger;
                energy = agent.Needs.Energy;
            }
            var basis = continuity.TryGetValue(characterId, out var retained)
                ? retained
                : new CharacterRuntimeState(characterId, position, default, false);
            var state = basis.WithTier1State(position, hunger, energy);
            continuity[characterId] = state;
            return state;
        }

        public void ValidateMaterialization(Character character, CharacterRuntimeState state)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Id != state.CharacterId)
                throw new ArgumentException("Character and Tier 1 state must share one identity.", nameof(state));
            if (IsActive(character.Id)) throw new InvalidOperationException("Tier 1 presentation is already active.");
            catalog.Resolve(character); // deterministic sex mapping and prefab validation; no random selection
        }

        public void Materialize(Character character, CharacterRuntimeState state)
        {
            ValidateMaterialization(character, state);
            var position = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
            var presenter = spawner.Spawn(character, position, Quaternion.identity);
            try
            {
                aiRuntime.Register(character, presenter, new Tier1Needs(state.Hunger, state.Energy));
                continuity[character.Id] = state.WithTier1State(state.Position, state.Hunger, state.Energy);
            }
            catch
            {
                spawner.Despawn(presenter);
                throw;
            }
        }

        public void Dematerialize(StableEntityId characterId)
        {
            if (!presentations.TryGet(characterId, out var presenter) || presenter == null)
                throw new InvalidOperationException("Tier 1 presentation is not active.");
            aiRuntime.Unregister(characterId);
            spawner.Despawn(presenter);
            continuity.Remove(characterId);
        }
    }
}
