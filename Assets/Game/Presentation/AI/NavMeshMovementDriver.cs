using System;
using Game.Domain;
using Game.Domain.AI;
using Game.Presentation.Characters;
using Game.Simulation.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Presentation.AI
{
    public enum Tier1MovementAuthority
    {
        Autonomous = 0,
        DirectPlayer = 1
    }

    public sealed class NavMeshMovementDriver : ITier1MovementDriver
    {
        private readonly CharacterPresenter presenter;
        private readonly NavMeshAgent agent;
        private readonly float baseSpeed;
        private readonly StableEntityId characterId;
        private MovementState state;
        private Tier1MovementAuthority authority;
        private float appliedAgentSpeed = float.NaN;

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
            ApplyAgentSpeed(baseSpeed);
            agent.angularSpeed = 540f;
            agent.acceleration = 12f;
            agent.radius = 0.32f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
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
        public bool HasBoundPresenter => presenter != null && presenter.IsBound;
        public NavMeshAgent Agent => agent;
        public Tier1MovementAuthority Authority => authority;

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
            if (authority == Tier1MovementAuthority.DirectPlayer) return;
            if (!IsAvailable || stoppingDistance < 0f ||
                !NavMesh.SamplePosition(new Vector3(target.X, target.Y, target.Z), out var hit, 3f, NavMesh.AllAreas))
            {
                state = MovementState.Failed;
                return;
            }
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            state = agent.SetDestination(hit.position) ? MovementState.Moving : MovementState.Failed;
            presenter.SetMovementSpeed(state == MovementState.Moving ? agent.speed : 0f);
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

        public void BeginDirectControl()
        {
            if (!IsAvailable) throw new InvalidOperationException("Direct control requires an active NavMesh agent.");
            Stop();
            authority = Tier1MovementAuthority.DirectPlayer;
            agent.updateRotation = false;
        }

        public void MoveDirect(Vector3 worldDirection, float unscaledDeltaTime, bool running, bool paused,
            int speedMultiplier, int movementPerThousand)
        {
            if (authority != Tier1MovementAuthority.DirectPlayer)
                throw new InvalidOperationException("Direct movement does not own this character.");
            if (!IsAvailable || unscaledDeltaTime < 0f || movementPerThousand < 0 || movementPerThousand > 2000)
                return;
            agent.isStopped = true;
            agent.ResetPath();
            var direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (paused || direction.sqrMagnitude <= 0.0001f)
            {
                presenter.SetMovementSpeed(0f);
                presenter.SetSimulationPaused(paused);
                presenter.RefreshAnimation(Time.unscaledTime);
                state = MovementState.Idle;
                return;
            }
            direction.Normalize();
            var runMultiplier = running ? 1.65f : 1f;
            var speed = baseSpeed * Math.Max(1, speedMultiplier) * movementPerThousand / 1000f * runMultiplier;
            agent.Move(direction * (speed * unscaledDeltaTime));
            presenter.transform.rotation = Quaternion.RotateTowards(presenter.transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), 720f * unscaledDeltaTime);
            presenter.SetSimulationPaused(false);
            presenter.SetMovementSpeed(speed);
            presenter.RefreshAnimation(Time.unscaledTime);
            state = MovementState.Moving;
        }

        public void EndDirectControl()
        {
            if (authority != Tier1MovementAuthority.DirectPlayer) return;
            Stop();
            agent.updateRotation = true;
            authority = Tier1MovementAuthority.Autonomous;
        }

        public void UpdatePresentation(bool paused, int speedMultiplier, int movementPerThousand = 1000)
        {
            if (!IsAvailable) return;
            if (movementPerThousand < 0 || movementPerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(movementPerThousand));
            ApplyAgentSpeed(baseSpeed * Math.Max(1, speedMultiplier) * movementPerThousand / 1000f);
            if (authority == Tier1MovementAuthority.DirectPlayer)
            {
                if (!agent.isStopped) agent.isStopped = true;
                if (paused)
                {
                    presenter.SetMovementSpeed(0f);
                    presenter.SetSimulationPaused(true);
                }
                return;
            }
            if (paused)
            {
                if (!agent.isStopped) agent.isStopped = true;
                presenter.SetMovementSpeed(0f);
                presenter.SetSimulationPaused(true);
                return;
            }
            presenter.SetSimulationPaused(false);
            if (state == MovementState.Moving && agent.hasPath)
            {
                if (agent.isStopped) agent.isStopped = false;
                presenter.SetMovementSpeed(agent.velocity.magnitude);
                return;
            }
            presenter.SetMovementSpeed(0f);
        }

        public void RefreshAnimation(float unscaledTime) => presenter.RefreshAnimation(unscaledTime);

        private void ApplyAgentSpeed(float speed)
        {
            if (!float.IsNaN(appliedAgentSpeed) && Mathf.Abs(appliedAgentSpeed - speed) <= 0.001f) return;
            agent.speed = speed;
            appliedAgentSpeed = speed;
        }
    }
}
