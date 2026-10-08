using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Domain.Control;

namespace Game.Presentation.Input
{
    public sealed class LocalGameplayInputSource : MonoBehaviour, ILocalGameplayInput
    {
        [SerializeField] private InputActionAsset actionAsset;

        private InputActionMap cameraMap;
        private InputActionMap pointerMap;
        private InputActionMap buildingMap;
        private InputActionMap timeMap;
        private InputActionMap modeMap;
        private InputActionMap thirdPersonMap;
        private InputAction moveAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction panAction;
        private InputAction panModifierAction;
        private InputAction pointerPositionAction;
        private InputAction primaryClickAction;
        private InputAction secondaryClickAction;
        private InputAction selectionModifierAction;
        private InputAction buildModeAction;
        private InputAction buildConfirmAction;
        private InputAction buildCancelAction;
        private InputAction buildRotateAction;
        private InputAction pauseAction;
        private InputAction timeSlowerAction;
        private InputAction timeFasterAction;
        private InputAction controlModeToggleAction;
        private InputAction thirdPersonMoveAction;
        private InputAction thirdPersonLookAction;
        private InputAction thirdPersonRunAction;
        private InputAction thirdPersonExitAction;
        private PlayerControlMode controlMode = PlayerControlMode.Rts;

        public Vector2 Move => ReadVector2(moveAction);
        public float Zoom => ReadFloat(zoomAction);
        public float Rotate => ReadFloat(rotateAction);
        public Vector2 PanDelta => ReadVector2(panAction);
        public bool IsPanPressed => panModifierAction != null && panModifierAction.IsPressed();
        public bool IsAdditiveSelectionPressed => selectionModifierAction != null && selectionModifierAction.IsPressed();
        public Vector2 PointerPosition => ReadVector2(pointerPositionAction);
        public Vector2 ThirdPersonMove => ReadVector2(thirdPersonMoveAction);
        public Vector2 ThirdPersonLook => ReadVector2(thirdPersonLookAction);
        public bool ThirdPersonRun => thirdPersonRunAction != null && thirdPersonRunAction.IsPressed();
        public bool RtsActionsEnabled => cameraMap != null && cameraMap.enabled;
        public bool ThirdPersonActionsEnabled => thirdPersonMap != null && thirdPersonMap.enabled;
        public bool GlobalTimeActionsEnabled => timeMap != null && timeMap.enabled;

        public event Action PrimaryClicked;
        public event Action SecondaryClicked;
        public event Action BuildModeRequested;
        public event Action BuildConfirmed;
        public event Action BuildCancelled;
        public event Action BuildRotated;
        public event Action PauseToggled;
        public event Action TimeSlowerRequested;
        public event Action TimeFasterRequested;
        public event Action ControlModeToggled;
        public event Action ThirdPersonExitRequested;

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
            timeMap = actionAsset.FindActionMap("Time", true);
            modeMap = actionAsset.FindActionMap("Mode", true);
            thirdPersonMap = actionAsset.FindActionMap("ThirdPerson", true);
            moveAction = cameraMap.FindAction("Move", true);
            zoomAction = cameraMap.FindAction("Zoom", true);
            rotateAction = cameraMap.FindAction("Rotate", true);
            panAction = cameraMap.FindAction("Pan", true);
            panModifierAction = cameraMap.FindAction("PanModifier", true);
            pointerPositionAction = pointerMap.FindAction("Position", true);
            primaryClickAction = pointerMap.FindAction("PrimaryClick", true);
            secondaryClickAction = pointerMap.FindAction("SecondaryClick", true);
            selectionModifierAction = pointerMap.FindAction("SelectionModifier", true);
            buildModeAction = buildingMap.FindAction("ToggleMode", true);
            buildConfirmAction = buildingMap.FindAction("Confirm", true);
            buildCancelAction = buildingMap.FindAction("Cancel", true);
            buildRotateAction = buildingMap.FindAction("Rotate", true);
            pauseAction = timeMap.FindAction("TogglePause", true);
            timeSlowerAction = timeMap.FindAction("Slower", true);
            timeFasterAction = timeMap.FindAction("Faster", true);
            controlModeToggleAction = modeMap.FindAction("ToggleControl", true);
            thirdPersonMoveAction = thirdPersonMap.FindAction("Move", true);
            thirdPersonLookAction = thirdPersonMap.FindAction("Look", true);
            thirdPersonRunAction = thirdPersonMap.FindAction("Run", true);
            thirdPersonExitAction = thirdPersonMap.FindAction("Exit", true);
        }

        private void EnableActions()
        {
            if (cameraMap == null || pointerMap == null)
                return;
            primaryClickAction.performed -= OnPrimaryClick;
            primaryClickAction.performed += OnPrimaryClick;
            secondaryClickAction.performed -= OnSecondaryClick;
            secondaryClickAction.performed += OnSecondaryClick;
            buildModeAction.performed -= OnBuildMode;
            buildModeAction.performed += OnBuildMode;
            buildConfirmAction.performed -= OnBuildConfirm;
            buildConfirmAction.performed += OnBuildConfirm;
            buildCancelAction.performed -= OnBuildCancel;
            buildCancelAction.performed += OnBuildCancel;
            buildRotateAction.performed -= OnBuildRotate;
            buildRotateAction.performed += OnBuildRotate;
            pauseAction.performed -= OnPauseToggle;
            pauseAction.performed += OnPauseToggle;
            timeSlowerAction.performed -= OnTimeSlower;
            timeSlowerAction.performed += OnTimeSlower;
            timeFasterAction.performed -= OnTimeFaster;
            timeFasterAction.performed += OnTimeFaster;
            controlModeToggleAction.performed -= OnControlModeToggle;
            controlModeToggleAction.performed += OnControlModeToggle;
            thirdPersonExitAction.performed -= OnThirdPersonExit;
            thirdPersonExitAction.performed += OnThirdPersonExit;
            timeMap.Enable();
            modeMap.Enable();
            ApplyControlModeMaps();
        }

        private void DisableActions()
        {
            if (primaryClickAction != null)
                primaryClickAction.performed -= OnPrimaryClick;
            if (secondaryClickAction != null)
                secondaryClickAction.performed -= OnSecondaryClick;
            if (buildModeAction != null) buildModeAction.performed -= OnBuildMode;
            if (buildConfirmAction != null) buildConfirmAction.performed -= OnBuildConfirm;
            if (buildCancelAction != null) buildCancelAction.performed -= OnBuildCancel;
            if (buildRotateAction != null) buildRotateAction.performed -= OnBuildRotate;
            if (pauseAction != null) pauseAction.performed -= OnPauseToggle;
            if (timeSlowerAction != null) timeSlowerAction.performed -= OnTimeSlower;
            if (timeFasterAction != null) timeFasterAction.performed -= OnTimeFaster;
            if (controlModeToggleAction != null) controlModeToggleAction.performed -= OnControlModeToggle;
            if (thirdPersonExitAction != null) thirdPersonExitAction.performed -= OnThirdPersonExit;
            cameraMap?.Disable();
            pointerMap?.Disable();
            buildingMap?.Disable();
            timeMap?.Disable();
            modeMap?.Disable();
            thirdPersonMap?.Disable();
        }

        private void OnPrimaryClick(InputAction.CallbackContext context)
        {
            PrimaryClicked?.Invoke();
        }

        private void OnSecondaryClick(InputAction.CallbackContext context)
        {
            SecondaryClicked?.Invoke();
        }

        private void OnBuildMode(InputAction.CallbackContext context) => BuildModeRequested?.Invoke();
        private void OnBuildConfirm(InputAction.CallbackContext context) => BuildConfirmed?.Invoke();
        private void OnBuildCancel(InputAction.CallbackContext context) => BuildCancelled?.Invoke();
        private void OnBuildRotate(InputAction.CallbackContext context) => BuildRotated?.Invoke();
        private void OnPauseToggle(InputAction.CallbackContext context) => PauseToggled?.Invoke();
        private void OnTimeSlower(InputAction.CallbackContext context) => TimeSlowerRequested?.Invoke();
        private void OnTimeFaster(InputAction.CallbackContext context) => TimeFasterRequested?.Invoke();
        private void OnControlModeToggle(InputAction.CallbackContext context) => ControlModeToggled?.Invoke();
        private void OnThirdPersonExit(InputAction.CallbackContext context) => ThirdPersonExitRequested?.Invoke();

        public void SetPlayerControlMode(PlayerControlMode mode)
        {
            if (!Enum.IsDefined(typeof(PlayerControlMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            controlMode = mode;
            if (isActiveAndEnabled) ApplyControlModeMaps();
        }

        private void ApplyControlModeMaps()
        {
            if (controlMode == PlayerControlMode.ThirdPerson)
            {
                cameraMap?.Disable();
                pointerMap?.Disable();
                buildingMap?.Disable();
                thirdPersonMap?.Enable();
            }
            else
            {
                thirdPersonMap?.Disable();
                cameraMap?.Enable();
                pointerMap?.Enable();
                buildingMap?.Enable();
            }
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
