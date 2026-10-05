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
        private InputActionMap buildingMap;
        private InputAction moveAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction panAction;
        private InputAction panModifierAction;
        private InputAction pointerPositionAction;
        private InputAction primaryClickAction;
        private InputAction buildModeAction;
        private InputAction buildConfirmAction;
        private InputAction buildCancelAction;
        private InputAction buildRotateAction;

        public Vector2 Move => ReadVector2(moveAction);
        public float Zoom => ReadFloat(zoomAction);
        public float Rotate => ReadFloat(rotateAction);
        public Vector2 PanDelta => ReadVector2(panAction);
        public bool IsPanPressed => panModifierAction != null && panModifierAction.IsPressed();
        public Vector2 PointerPosition => ReadVector2(pointerPositionAction);

        public event Action PrimaryClicked;
        public event Action BuildModeRequested;
        public event Action BuildConfirmed;
        public event Action BuildCancelled;
        public event Action BuildRotated;

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
            buildingMap = actionAsset.FindActionMap("Building", true);
            moveAction = cameraMap.FindAction("Move", true);
            zoomAction = cameraMap.FindAction("Zoom", true);
            rotateAction = cameraMap.FindAction("Rotate", true);
            panAction = cameraMap.FindAction("Pan", true);
            panModifierAction = cameraMap.FindAction("PanModifier", true);
            pointerPositionAction = pointerMap.FindAction("Position", true);
            primaryClickAction = pointerMap.FindAction("PrimaryClick", true);
            buildModeAction = buildingMap.FindAction("ToggleMode", true);
            buildConfirmAction = buildingMap.FindAction("Confirm", true);
            buildCancelAction = buildingMap.FindAction("Cancel", true);
            buildRotateAction = buildingMap.FindAction("Rotate", true);
        }

        private void EnableActions()
        {
            if (cameraMap == null || pointerMap == null)
                return;
            primaryClickAction.performed -= OnPrimaryClick;
            primaryClickAction.performed += OnPrimaryClick;
            buildModeAction.performed -= OnBuildMode;
            buildModeAction.performed += OnBuildMode;
            buildConfirmAction.performed -= OnBuildConfirm;
            buildConfirmAction.performed += OnBuildConfirm;
            buildCancelAction.performed -= OnBuildCancel;
            buildCancelAction.performed += OnBuildCancel;
            buildRotateAction.performed -= OnBuildRotate;
            buildRotateAction.performed += OnBuildRotate;
            cameraMap.Enable();
            pointerMap.Enable();
            buildingMap.Enable();
        }

        private void DisableActions()
        {
            if (primaryClickAction != null)
                primaryClickAction.performed -= OnPrimaryClick;
            if (buildModeAction != null) buildModeAction.performed -= OnBuildMode;
            if (buildConfirmAction != null) buildConfirmAction.performed -= OnBuildConfirm;
            if (buildCancelAction != null) buildCancelAction.performed -= OnBuildCancel;
            if (buildRotateAction != null) buildRotateAction.performed -= OnBuildRotate;
            cameraMap?.Disable();
            pointerMap?.Disable();
            buildingMap?.Disable();
        }

        private void OnPrimaryClick(InputAction.CallbackContext context)
        {
            PrimaryClicked?.Invoke();
        }

        private void OnBuildMode(InputAction.CallbackContext context) => BuildModeRequested?.Invoke();
        private void OnBuildConfirm(InputAction.CallbackContext context) => BuildConfirmed?.Invoke();
        private void OnBuildCancel(InputAction.CallbackContext context) => BuildCancelled?.Invoke();
        private void OnBuildRotate(InputAction.CallbackContext context) => BuildRotated?.Invoke();

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
