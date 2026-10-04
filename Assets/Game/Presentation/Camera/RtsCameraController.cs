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
            Tick(Time.unscaledDeltaTime, Application.isFocused, new Vector2(Screen.width, Screen.height));
        }

        public void Tick(float unscaledDeltaTime, bool pointerFocused, Vector2 screenSize)
        {
            if (input == null || cameraMount == null || unscaledDeltaTime < 0f)
                return;

            var yaw = transform.eulerAngles.y + input.Rotate * settings.RotationSpeed * unscaledDeltaTime;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var translation = RtsCameraMath.HorizontalMotion(input.Move, yaw)
                * settings.MoveSpeed * unscaledDeltaTime;
            if (settings.EdgeScrollEnabled && pointerFocused)
            {
                var edgeDirection = RtsCameraMath.GetEdgeDirection(input.PointerPosition, screenSize,
                    settings.EdgeWidthNormalized);
                translation += RtsCameraMath.HorizontalMotion(edgeDirection, yaw)
                    * settings.EdgeScrollSpeed * unscaledDeltaTime;
            }
            if (input.IsPanPressed && screenSize.y > 0f)
            {
                var drag = input.PanDelta * (settings.DragPanSpeed * currentZoom / screenSize.y);
                translation += RtsCameraMath.HorizontalVector(new Vector2(-drag.x, -drag.y), yaw);
            }
            transform.position = RtsCameraMath.ClampToBounds(transform.position + translation,
                settings.BoundsMin, settings.BoundsMax);

            targetZoom = Mathf.Clamp(targetZoom - input.Zoom * settings.ZoomSpeed,
                settings.MinZoom, settings.MaxZoom);
            currentZoom = Mathf.MoveTowards(currentZoom, targetZoom, settings.ZoomSmoothing * unscaledDeltaTime);
            ApplyCameraMount();
        }

        private void Initialize()
        {
            settings ??= new RtsCameraSettings();
            settings.Validate();
            input = inputSource;
            targetZoom = Mathf.Clamp(initialZoom, settings.MinZoom, settings.MaxZoom);
            currentZoom = targetZoom;
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
