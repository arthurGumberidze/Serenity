using UnityEngine;
using UnityEngine.AI;

namespace Game.Presentation.AI
{
    /// <summary>Resolve the reachable side of a resource, not an isolated polygon on its top.</summary>
    public static class ReachableNavMeshPoint
    {
        public static bool TryFind(Vector3 origin, Vector3 target, out Vector3 destination)
        {
            destination = default;
            if (!NavMesh.SamplePosition(origin, out var start, 4f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (TryCandidate(start.position, target, target, 3f, path, out destination)) return true;
            for (var ring = 1; ring <= 3; ring++)
            for (var direction = 0; direction < 12; direction++)
            {
                var angle = direction * Mathf.PI / 6f;
                var candidate = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                if (TryCandidate(start.position, candidate, target, 0.75f, path, out destination)) return true;
            }
            return false;
        }

        private static bool TryCandidate(Vector3 start, Vector3 candidate, Vector3 target,
            float sampleRadius, NavMeshPath path, out Vector3 destination)
        {
            destination = default;
            if (!NavMesh.SamplePosition(candidate, out var hit, sampleRadius, NavMesh.AllAreas) ||
                Vector3.Distance(hit.position, target) > 3.1f ||
                !NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            destination = hit.position;
            return true;
        }
    }
}
