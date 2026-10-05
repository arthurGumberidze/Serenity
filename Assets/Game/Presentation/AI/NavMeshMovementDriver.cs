using System;
using Game.Domain;
using Game.Domain.AI;
using Game.Presentation.Characters;
using Game.Simulation.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Presentation.AI
{
    public sealed class NavMeshMovementDriver : ITier1MovementDriver
    {
        private readonly CharacterPresenter presenter;
        private readonly NavMeshAgent agent;
        private readonly float baseSpeed;
        private readonly StableEntityId characterId;
        private MovementState state;

        public NavMeshMovementDriver(CharacterPresenter presenter, float baseSpeed)
        {
            this.presenter = presenter != null ? presenter : throw new ArgumentNullException(nameof(presenter));
            if (!presenter.IsBound) throw new InvalidOperationException("Movement requires a bound character presenter.");
            if (baseSpeed <= 0f || float.IsNaN(baseSpeed) || float.IsInfinity(baseSpeed))
                throw new ArgumentOutOfRangeException(nameof(baseSpeed));
            this.baseSpeed = baseSpeed;
            characterId = presenter.CharacterId;
            agent = presenter.GetComponent<NavMeshAgent>();
            if (agent == null) agent = presenter.gameObject.AddComponent<NavMeshAgent>();
            agent.speed = baseSpeed;
            agent.angularSpeed = 540f;
            agent.acceleration = 12f;
            agent.radius = 0.32f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(presenter.transform.position, out var hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            state = agent.isOnNavMesh ? MovementState.Idle : MovementState.Failed;
        }

        public StableEntityId CharacterId => characterId;
        public WorldPosition CurrentPosition
        {
            get
            {
                var position = presenter != null ? presenter.transform.position : Vector3.zero;
                return new WorldPosition(position.x, position.y, position.z);
            }
        }
        public bool IsAvailable => presenter != null && presenter.IsBound && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public NavMeshAgent Agent => agent;

        public MovementState State
        {
            get
            {
                if (!IsAvailable) return MovementState.Failed;
                if (state != MovementState.Moving) return state;
                if (agent.pathPending) return MovementState.Moving;
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return MovementState.Failed;
                if (agent.remainingDistance <= agent.stoppingDistance + 0.05f && agent.velocity.sqrMagnitude <= 0.04f)
                    return MovementState.Arrived;
                return MovementState.Moving;
            }
        }

        public void MoveTo(WorldPosition target, float stoppingDistance)
        {
            if (!IsAvailable || stoppingDistance < 0f ||
                !NavMesh.SamplePosition(new Vector3(target.X, target.Y, target.Z), out var hit, 3f, NavMesh.AllAreas))
            {
                state = MovementState.Failed;
                return;
            }
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            state = agent.SetDestination(hit.position) ? MovementState.Moving : MovementState.Failed;
        }

        public void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            state = MovementState.Idle;
            if (presenter != null) presenter.SetMovementSpeed(0f);
        }

        public void UpdatePresentation(bool paused, int speedMultiplier)
        {
            if (!IsAvailable) return;
            agent.speed = baseSpeed * Math.Max(1, speedMultiplier);
            if (paused)
            {
                agent.isStopped = true;
                presenter.SetMovementSpeed(0f);
                presenter.SetSimulationPaused(true);
                return;
            }
            presenter.SetSimulationPaused(false);
            if (state == MovementState.Moving && agent.hasPath) agent.isStopped = false;
            presenter.SetMovementSpeed(agent.velocity.magnitude);
        }
    }
}
