using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using Game.Simulation.AI;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.AI
{
    /// <summary>Single scene-level bridge. NPC decision logic never owns a MonoBehaviour Update.</summary>
    [DisallowMultipleComponent]
    public sealed class Tier1AiRuntimeDriver : MonoBehaviour
    {
        private readonly List<NavMeshMovementDriver> movements = new List<NavMeshMovementDriver>();
        private GameClock clock;
        private Tier1AiScheduler scheduler;
        private Func<int> movementModifierProvider;

        public GameClock Clock => clock;
        public Tier1AiScheduler Scheduler => scheduler;
        public IReadOnlyList<NavMeshMovementDriver> Movements => movements;
        public int PerAgentAiUpdateCount => 0;
        public event Action<GameTimeAdvance> SimulationAdvanced;

        public void Initialize(GameClock gameClock, Tier1AiScheduler aiScheduler,
            Func<int> weatherMovementModifierProvider = null)
        {
            if (clock != null || scheduler != null) throw new InvalidOperationException("Tier 1 AI runtime is already initialized.");
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            scheduler = aiScheduler ?? throw new ArgumentNullException(nameof(aiScheduler));
            movementModifierProvider = weatherMovementModifierProvider;
        }

        public Tier1AiAgentState Register(Character character, CharacterPresenter presenter, Tier1Needs needs = null)
        {
            if (clock == null || scheduler == null) throw new InvalidOperationException("Tier 1 AI runtime is not initialized.");
            if (movements.Exists(candidate => candidate.CharacterId == character.Id))
                throw new InvalidOperationException("Tier 1 movement is already registered for this character.");
            var movement = new NavMeshMovementDriver(presenter, scheduler.Settings.MovementSpeed);
            var position = movement.CurrentPosition;
            var state = new Tier1AiAgentState(character.Id, position, needs);
            scheduler.Register(character, state, movement);
            movements.Add(movement);
            return state;
        }

        public bool Unregister(StableEntityId characterId)
        {
            var removed = false;
            for (var i = movements.Count - 1; i >= 0; i--)
            {
                if (movements[i].CharacterId != characterId) continue;
                movements[i].Stop();
                movements.RemoveAt(i);
                removed = true;
            }
            return scheduler != null && scheduler.Unregister(characterId) || removed;
        }

        public bool TryGetMovement(StableEntityId characterId, out NavMeshMovementDriver movement)
        {
            movement = movements.Find(candidate => candidate.CharacterId == characterId);
            return movement != null;
        }

        public void SetPaused(bool paused)
        {
            if (clock == null) return;
            if (paused) clock.Pause();
            else clock.Resume();
            RefreshMovementPresentation();
        }

        public void SetSpeed(GameSpeed speed)
        {
            if (clock == null) throw new InvalidOperationException("Tier 1 AI runtime is not initialized.");
            clock.SetSpeed(speed);
            RefreshMovementPresentation();
        }

        private void Update()
        {
            if (clock == null || scheduler == null) return;
            for (var i = movements.Count - 1; i >= 0; i--)
            {
                if (movements[i].IsAvailable) continue;
                scheduler.Unregister(movements[i].CharacterId);
                movements.RemoveAt(i);
            }
            var advance = clock.Advance(TimeSpan.FromSeconds(Time.unscaledDeltaTime));
            scheduler.Advance(advance);
            SimulationAdvanced?.Invoke(advance);
            RefreshMovementPresentation();
        }

        private void OnDestroy()
        {
            scheduler?.Reset();
            movements.Clear();
            SimulationAdvanced = null;
        }

        private void RefreshMovementPresentation()
        {
            if (clock == null) return;
            var movementModifier = movementModifierProvider == null ? 1000 : movementModifierProvider();
            if (movementModifier < 0 || movementModifier > 2000)
                throw new InvalidOperationException("Movement modifier provider returned an invalid value.");
            for (var i = 0; i < movements.Count; i++)
                movements[i].UpdatePresentation(clock.IsPaused, (int)clock.Speed, movementModifier);
        }
    }
}
