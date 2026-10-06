using System;
using Game.Infrastructure;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    internal static class PointerSelectionTestPoint
    {
        public static Vector3 AimCameraAtUnblockedPoint(LocalSceneCompositionRoot root, Vector3 worldPoint)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var camera = root.PointerRaycaster.WorldCamera;
            camera.transform.SetPositionAndRotation(worldPoint + Vector3.up * 12f,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));

            var desiredPoint = FindUnblockedScreenPoint(root);
            var desiredRay = camera.ScreenPointToRay(desiredPoint);
            var distance = (worldPoint.y - desiredRay.origin.y) / desiredRay.direction.y;
            camera.transform.position += worldPoint - desiredRay.GetPoint(distance);
            Physics.SyncTransforms();

            var screenPoint = camera.WorldToScreenPoint(worldPoint);
            if (root.PointerRaycaster.IsPointerBlockedByUi(screenPoint))
                throw new InvalidOperationException("Could not place the test target outside development UI.");
            return screenPoint;
        }

        private static Vector2 FindUnblockedScreenPoint(LocalSceneCompositionRoot root)
        {
            const int samples = 20;
            var bestPoint = default(Vector2);
            var bestDistance = float.PositiveInfinity;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            for (var y = 0; y < samples; y++)
            for (var x = 0; x < samples; x++)
            {
                var point = new Vector2(
                    Mathf.Lerp(24f, Mathf.Max(24f, Screen.width - 24f), x / (samples - 1f)),
                    Mathf.Lerp(24f, Mathf.Max(24f, Screen.height - 24f), y / (samples - 1f)));
                if (root.PointerRaycaster.IsPointerBlockedByUi(point)) continue;
                var distance = (point - center).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestPoint = point;
                bestDistance = distance;
            }
            if (!float.IsPositiveInfinity(bestDistance)) return bestPoint;
            throw new InvalidOperationException("Development UI covers the complete test screen.");
        }
    }
}
