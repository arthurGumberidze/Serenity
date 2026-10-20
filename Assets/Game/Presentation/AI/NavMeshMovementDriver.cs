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
        private readonly NavMeshPath lightweightPath = new NavMeshPath();
        private MovementState state;
        private Tier1MovementAuthority authority;
        private float appliedAgentSpeed = float.NaN;
        private readonly Vector3[] lightweightCorners = new Vector3[64];
        private int lightweightCornerCount;
        private Vector3 requestedDestination;
        private int lightweightCornerIndex;
        private float requestedStoppingDistance;
        private float lightweightSpeed;
        private bool hasRequestedDestination;
        private bool crowdDetailEnabled = true;

        public NavMeshMovementDriver(CharacterPresenter presenter, float baseSpeed)
        {
            this.presenter = presenter != null ? presenter : throw new ArgumentNullException(nameof(presenter));
            if (!presenter.IsBound) throw new InvalidOperationException("Movement requires a bound character presenter.");
            if (baseSpeed <= 0f || float.IsNaN(baseSpeed) || float.IsInfinity(baseSpeed))
                throw new ArgumentOutOfRangeException(nameof(baseSpeed));
            this.baseSpeed = baseSpeed;
            lightweightSpeed = baseSpeed;
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
        public bool IsAvailable => presenter != null && presenter.IsBound && agent != null &&
                                   (!crowdDetailEnabled && authority != Tier1MovementAuthority.DirectPlayer ||
                                    agent.isActiveAndEnabled && agent.isOnNavMesh);
        public bool HasBoundPresenter => presenter != null && presenter.IsBound;
        public NavMeshAgent Agent => agent;
        public Tier1MovementAuthority Authority => authority;
        public bool CrowdDetailEnabled => crowdDetailEnabled;
        public CharacterPresenter Presenter => presenter;

        public void SetCrowdDetail(bool enabled)
        {
            // Reissuing SetDestination on every LOD refresh continually restarts path planning.
            if (crowdDetailEnabled == enabled) return;
            crowdDetailEnabled = enabled;
            if (authority == Tier1MovementAuthority.DirectPlayer) return;
            presenter.SetAutomaticAnimationAllowed(enabled);
            if (enabled)
            {
                if (!EnableAgentAtPresenter())
                {
                    state = MovementState.Failed;
                    return;
                }
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                if (state == MovementState.Moving && hasRequestedDestination)
                {
                    agent.stoppingDistance = requestedStoppingDistance;
                    agent.isStopped = false;
                    state = agent.SetDestination(requestedDestination) ? MovementState.Moving : MovementState.Failed;
                }
                return;
            }
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                agent.enabled = false;
            }
        }

        public MovementState State
        {
            get
            {
                if (!IsAvailable) return MovementState.Failed;
                if (state != MovementState.Moving) return state;
                if (!UsesNavMeshAgent) return state;
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
                !ReachableNavMeshPoint.TryFind(presenter.transform.position,
                    new Vector3(target.X, target.Y, target.Z), out var destination))
            {
                state = MovementState.Failed;
                return;
            }
            requestedDestination = destination;
            requestedStoppingDistance = stoppingDistance;
            hasRequestedDestination = true;
            if (!UsesNavMeshAgent)
            {
                if (!NavMesh.CalculatePath(presenter.transform.position, destination, NavMesh.AllAreas, lightweightPath) ||
                    lightweightPath.status != NavMeshPathStatus.PathComplete)
                {
                    state = MovementState.Failed;
                    return;
                }
                lightweightCornerCount = lightweightPath.GetCornersNonAlloc(lightweightCorners);
                lightweightCornerIndex = lightweightCornerCount > 1 ? 1 : 0;
                state = lightweightCornerCount > 0 ? MovementState.Moving : MovementState.Failed;
                presenter.SetMovementSpeed(state == MovementState.Moving ? lightweightSpeed : 0f);
                return;
            }
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            state = agent.SetDestination(destination) ? MovementState.Moving : MovementState.Failed;
            presenter.SetMovementSpeed(state == MovementState.Moving ? agent.speed : 0f);
        }

        public void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            hasRequestedDestination = false;
            lightweightCornerCount = 0;
            lightweightCornerIndex = 0;
            state = MovementState.Idle;
            if (presenter != null) presenter.SetMovementSpeed(0f);
        }

        public void BeginDirectControl()
        {
            if (!EnableAgentAtPresenter())
                throw new InvalidOperationException("Direct control requires an active NavMesh agent.");
            if (!IsAvailable) throw new InvalidOperationException("Direct control requires an active NavMesh agent.");
            Stop();
            authority = Tier1MovementAuthority.DirectPlayer;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            presenter.SetAutomaticAnimationAllowed(true);
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
            if (agent != null) agent.updateRotation = true;
            authority = Tier1MovementAuthority.Autonomous;
            SetCrowdDetail(crowdDetailEnabled);
        }

        public void AdvanceLightweight(float unscaledDeltaTime, bool paused)
        {
            if (UsesNavMeshAgent || state != MovementState.Moving || paused || unscaledDeltaTime <= 0f) return;
            if (lightweightCornerIndex >= lightweightCornerCount)
            {
                state = MovementState.Arrived;
                presenter.SetMovementSpeed(0f);
                return;
            }
            var position = presenter.transform.position;
            var corner = lightweightCorners[lightweightCornerIndex];
            var flatOffset = Vector3.ProjectOnPlane(corner - position, Vector3.up);
            var finalCorner = lightweightCornerIndex == lightweightCornerCount - 1;
            var reachDistance = finalCorner ? requestedStoppingDistance + 0.05f : 0.12f;
            if (flatOffset.magnitude <= reachDistance)
            {
                if (finalCorner)
                {
                    state = MovementState.Arrived;
                    presenter.SetMovementSpeed(0f);
                    return;
                }
                lightweightCornerIndex++;
                return;
            }
            var direction = flatOffset.normalized;
            var distance = Math.Min(lightweightSpeed * unscaledDeltaTime,
                Math.Max(0f, flatOffset.magnitude - (finalCorner ? requestedStoppingDistance : 0f)));
            presenter.transform.position = position + direction * distance;
            presenter.transform.rotation = Quaternion.RotateTowards(presenter.transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), 540f * unscaledDeltaTime);
            presenter.SetMovementSpeed(lightweightSpeed);
        }

        public void UpdatePresentation(bool paused, int speedMultiplier, int movementPerThousand = 1000)
        {
            if (!IsAvailable) return;
            if (movementPerThousand < 0 || movementPerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(movementPerThousand));
            lightweightSpeed = baseSpeed * Math.Max(1, speedMultiplier) * movementPerThousand / 1000f;
            if (UsesNavMeshAgent) ApplyAgentSpeed(lightweightSpeed);
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
                if (UsesNavMeshAgent && !agent.isStopped) agent.isStopped = true;
                presenter.SetMovementSpeed(0f);
                presenter.SetSimulationPaused(true);
                return;
            }
            presenter.SetSimulationPaused(false);
            if (!UsesNavMeshAgent)
            {
                presenter.SetMovementSpeed(state == MovementState.Moving ? lightweightSpeed : 0f);
                return;
            }
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
            // Time compression must scale braking and turning as well as top speed.
            // Otherwise x10 units overshoot a tree and time out while trying to return to it.
            var timeScale = Mathf.Max(0.1f, speed / baseSpeed);
            agent.acceleration = 12f * timeScale * timeScale;
            agent.angularSpeed = 540f * timeScale;
            appliedAgentSpeed = speed;
        }

        private bool UsesNavMeshAgent => crowdDetailEnabled || authority == Tier1MovementAuthority.DirectPlayer;

        private bool EnableAgentAtPresenter()
        {
            if (agent == null) return false;
            if (!agent.enabled) agent.enabled = true;
            if (agent.isOnNavMesh) return true;
            if (!NavMesh.SamplePosition(presenter.transform.position, out var hit, 4f, NavMesh.AllAreas)) return false;
            return agent.Warp(hit.position);
        }
    }
}
