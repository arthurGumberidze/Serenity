using System;
using Game.Presentation.Input;
using UnityEngine;

namespace Game.Presentation.CameraControl
{
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private LocalGameplayInputSource inputSource;
        [SerializeField] private Transform cameraMount;
        [SerializeField] private RtsCameraSettings settings = new RtsCameraSettings();
        [SerializeField] private float initialZoom = 25f;

        private ILocalGameplayInput input;
        private float targetZoom;
        private float currentZoom;
        private Func<Vector3, float> groundHeight;
        private Vector3 home;

        public void ConfigureGround(Func<Vector3, float> sample, Vector3 start)
        {
            groundHeight = sample ?? throw new ArgumentNullException(nameof(sample));
            home = start;
            FocusHome();
        }

        public void FocusHome()
        {
            FocusPosition(home, 45f);
        }

        public void FocusPosition(Vector3 position, float zoom)
        {
            position = RtsCameraMath.ClampToBounds(position, settings.BoundsMin, settings.BoundsMax);
            if (groundHeight != null) position.y = groundHeight(position);
            transform.position = position;
            targetZoom = currentZoom = Mathf.Clamp(zoom, settings.MinZoom, settings.MaxZoom);
            ApplyCameraMount();
        }

        public RtsCameraSettings Settings => settings;
        public Transform CameraMount => cameraMount;
        public float CurrentZoom => currentZoom;

        public void Configure(LocalGameplayInputSource source, Transform mount, RtsCameraSettings cameraSettings, float zoom)
        {
            inputSource = source;
            cameraMount = mount;
            settings = cameraSettings ?? throw new ArgumentNullException(nameof(cameraSettings));
            initialZoom = zoom;
            Initialize();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            settings?.Validate();
            initialZoom = settings == null ? initialZoom : Mathf.Clamp(initialZoom, settings.MinZoom, settings.MaxZoom);
        }

        private void Update()
        {
            if (inputSource != null && inputSource.RtsActionsEnabled &&
                UnityEngine.InputSystem.Keyboard.current?.homeKey.wasPressedThisFrame == true) FocusHome();
            Tick(Time.unscaledDeltaTime, Application.isFocused, new Vector2(Screen.width, Screen.height));
        }

        public void Tick(float unscaledDeltaTime, bool pointerFocused, Vector2 screenSize)
        {
            if (input == null || cameraMount == null || unscaledDeltaTime < 0f)
                return;

            var currentYaw = transform.eulerAngles.y;
            var yaw = currentYaw + input.Rotate * settings.RotationSpeed * unscaledDeltaTime;
            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, yaw)) > 0.0001f)
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var translation = RtsCameraMath.HorizontalMotion(input.Move, yaw)
                * settings.MoveSpeed * unscaledDeltaTime;
            // Pointer motion is opt-in: hovering a screen edge must not move the world.
            if (input.IsPanPressed && pointerFocused && screenSize.y > 0f)
            {
                var drag = input.PanDelta * (settings.DragPanSpeed * currentZoom / screenSize.y);
                translation += RtsCameraMath.HorizontalVector(new Vector2(-drag.x, -drag.y), yaw);
            }
            var position = RtsCameraMath.ClampToBounds(transform.position + translation,
                settings.BoundsMin, settings.BoundsMax);
            if (groundHeight != null) position.y = groundHeight(position);
            if ((position - transform.position).sqrMagnitude > 0.00000001f) transform.position = position;

            targetZoom = Mathf.Clamp(targetZoom - input.Zoom * settings.ZoomSpeed,
                settings.MinZoom, settings.MaxZoom);
            var zoom = Mathf.MoveTowards(currentZoom, targetZoom, settings.ZoomSmoothing * unscaledDeltaTime);
            if (Mathf.Abs(zoom - currentZoom) > 0.0001f)
            {
                currentZoom = zoom;
                ApplyCameraMount();
            }
        }

        private void Initialize()
        {
            settings ??= new RtsCameraSettings();
            settings.Validate();
            input = inputSource;
            targetZoom = Mathf.Clamp(initialZoom, settings.MinZoom, settings.MaxZoom);
            currentZoom = targetZoom;
            if (groundHeight != null)
            {
                FocusHome();
                return;
            }
            ApplyCameraMount();
        }

        private void ApplyCameraMount()
        {
            if (cameraMount == null)
                return;
            cameraMount.localPosition = Quaternion.Euler(settings.PitchDegrees, 0f, 0f) * Vector3.back * currentZoom;
            cameraMount.localRotation = Quaternion.Euler(settings.PitchDegrees, 0f, 0f);
        }
    }
}
