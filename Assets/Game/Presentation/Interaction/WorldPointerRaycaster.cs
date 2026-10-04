using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.Interaction
{
    public sealed class WorldPointerRaycaster : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera worldCamera;
        [SerializeField] private LayerMask worldLayers = ~0;
        [SerializeField, Min(0.1f)] private float maxDistance = 1000f;
        [SerializeField] private bool blockWhenPointerOverUi = true;

        public UnityEngine.Camera WorldCamera => worldCamera;

        public void Configure(UnityEngine.Camera camera, LayerMask layers, float distance = 1000f)
        {
            worldCamera = camera;
            worldLayers = layers;
            maxDistance = Mathf.Max(0.1f, distance);
        }

        public bool TryGetWorldHit(Vector2 pointerPosition, out RaycastHit hit)
        {
            hit = default;
            if (worldCamera == null || IsPointerBlockedByUi())
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

        private bool IsPointerBlockedByUi()
        {
            return blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
