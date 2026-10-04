using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Input
{
    public sealed class LocalGameplayInputSource : MonoBehaviour, ILocalGameplayInput
    {
        [SerializeField] private InputActionAsset actionAsset;

        private InputActionMap cameraMap;
        private InputActionMap pointerMap;
        private InputAction moveAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction panAction;
        private InputAction panModifierAction;
        private InputAction pointerPositionAction;
        private InputAction primaryClickAction;

        public Vector2 Move => ReadVector2(moveAction);
        public float Zoom => ReadFloat(zoomAction);
        public float Rotate => ReadFloat(rotateAction);
        public Vector2 PanDelta => ReadVector2(panAction);
        public bool IsPanPressed => panModifierAction != null && panModifierAction.IsPressed();
        public Vector2 PointerPosition => ReadVector2(pointerPositionAction);

        public event Action PrimaryClicked;

        public InputActionAsset Actions => actionAsset;

        public void Configure(InputActionAsset asset)
        {
            if (Application.isPlaying && enabled && isActiveAndEnabled)
                DisableActions();

            actionAsset = asset;
            if (!Application.isPlaying)
                return;
            ResolveActions();

            if (enabled && isActiveAndEnabled)
                EnableActions();
        }

        private void Awake()
        {
            ResolveActions();
        }

        private void OnEnable()
        {
            if (actionAsset == null)
                return;
            if (cameraMap == null)
                ResolveActions();
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void ResolveActions()
        {
            if (actionAsset == null)
                throw new InvalidOperationException("Local gameplay InputActionAsset is required.");

            cameraMap = actionAsset.FindActionMap("Camera", true);
            pointerMap = actionAsset.FindActionMap("Pointer", true);
            moveAction = cameraMap.FindAction("Move", true);
            zoomAction = cameraMap.FindAction("Zoom", true);
            rotateAction = cameraMap.FindAction("Rotate", true);
            panAction = cameraMap.FindAction("Pan", true);
            panModifierAction = cameraMap.FindAction("PanModifier", true);
            pointerPositionAction = pointerMap.FindAction("Position", true);
            primaryClickAction = pointerMap.FindAction("PrimaryClick", true);
        }

        private void EnableActions()
        {
            if (cameraMap == null || pointerMap == null)
                return;
            primaryClickAction.performed -= OnPrimaryClick;
            primaryClickAction.performed += OnPrimaryClick;
            cameraMap.Enable();
            pointerMap.Enable();
        }

        private void DisableActions()
        {
            if (primaryClickAction != null)
                primaryClickAction.performed -= OnPrimaryClick;
            cameraMap?.Disable();
            pointerMap?.Disable();
        }

        private void OnPrimaryClick(InputAction.CallbackContext context)
        {
            PrimaryClicked?.Invoke();
        }

        private static Vector2 ReadVector2(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private static float ReadFloat(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<float>() : 0f;
        }
    }
}
