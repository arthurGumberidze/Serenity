using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.Interaction
{
    public interface IWorldPointerUiBlocker
    {
        bool TryGetUiBlockingRect(out Rect guiRect);
    }

    public sealed class WorldPointerRaycaster : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera worldCamera;
        [SerializeField] private LayerMask worldLayers = ~0;
        [SerializeField, Min(0.1f)] private float maxDistance = 1000f;
        [SerializeField] private bool blockWhenPointerOverUi = true;

        private readonly List<IWorldPointerUiBlocker> uiBlockers = new List<IWorldPointerUiBlocker>();

        public UnityEngine.Camera WorldCamera => worldCamera;

        public void Configure(UnityEngine.Camera camera, LayerMask layers, float distance = 1000f)
        {
            worldCamera = camera;
            worldLayers = layers;
            maxDistance = Mathf.Max(0.1f, distance);
        }

        public void RegisterUiBlocker(IWorldPointerUiBlocker blocker)
        {
            if (blocker == null) throw new ArgumentNullException(nameof(blocker));
            if (!uiBlockers.Contains(blocker)) uiBlockers.Add(blocker);
        }

        public void UnregisterUiBlocker(IWorldPointerUiBlocker blocker)
        {
            if (blocker != null) uiBlockers.Remove(blocker);
        }

        public bool TryGetWorldHit(Vector2 pointerPosition, out RaycastHit hit)
        {
            hit = default;
            if (worldCamera == null || IsPointerBlockedByUi(pointerPosition))
                return false;
            var ray = worldCamera.ScreenPointToRay(pointerPosition);
            return Physics.Raycast(ray, out hit, maxDistance, worldLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryGetWorldPoint(Vector2 pointerPosition, out Vector3 point)
        {
            if (TryGetWorldHit(pointerPosition, out var hit))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        public bool IsPointerBlockedByUi(Vector2 pointerPosition)
        {
            if (!blockWhenPointerOverUi) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;

            var guiPosition = ScreenToGuiPoint(pointerPosition);
            for (var i = uiBlockers.Count - 1; i >= 0; i--)
            {
                var blocker = uiBlockers[i];
                if (blocker is UnityEngine.Object unityObject && unityObject == null)
                {
                    uiBlockers.RemoveAt(i);
                    continue;
                }
                if (blocker is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                if (blocker.TryGetUiBlockingRect(out var rect) && rect.Contains(guiPosition)) return true;
            }
            return false;
        }

        public static Vector2 ScreenToGuiPoint(Vector2 screenPoint) =>
            new Vector2(screenPoint.x, Screen.height - screenPoint.y);

        public static Vector2 GuiToScreenPoint(Vector2 guiPoint) =>
            new Vector2(guiPoint.x, Screen.height - guiPoint.y);
    }
}
