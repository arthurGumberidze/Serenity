using System;
using UnityEngine;

namespace Game.Presentation.CameraControl
{
    [Serializable]
    public sealed class RtsCameraSettings
    {
        [Min(0f)] public float MoveSpeed = 18f;
        [Min(0f)] public float EdgeScrollSpeed = 18f;
        public bool EdgeScrollEnabled = true;
        [Range(0.001f, 0.25f)] public float EdgeWidthNormalized = 0.02f;
        [Min(0f)] public float DragPanSpeed = 0.8f;
        [Min(0f)] public float RotationSpeed = 90f;
        [Min(0f)] public float ZoomSpeed = 8f;
        [Min(0f)] public float ZoomSmoothing = 35f;
        [Min(0.1f)] public float MinZoom = 8f;
        [Min(0.1f)] public float MaxZoom = 45f;
        [Range(15f, 80f)] public float PitchDegrees = 50f;
        public Vector2 BoundsMin = new Vector2(-45f, -45f);
        public Vector2 BoundsMax = new Vector2(45f, 45f);

        public void Validate()
        {
            MoveSpeed = Mathf.Max(0f, MoveSpeed);
            EdgeScrollSpeed = Mathf.Max(0f, EdgeScrollSpeed);
            EdgeWidthNormalized = Mathf.Clamp(EdgeWidthNormalized, 0.001f, 0.25f);
            DragPanSpeed = Mathf.Max(0f, DragPanSpeed);
            RotationSpeed = Mathf.Max(0f, RotationSpeed);
            ZoomSpeed = Mathf.Max(0f, ZoomSpeed);
            ZoomSmoothing = Mathf.Max(0f, ZoomSmoothing);
            MinZoom = Mathf.Max(0.1f, MinZoom);
            MaxZoom = Mathf.Max(MinZoom, MaxZoom);
            PitchDegrees = Mathf.Clamp(PitchDegrees, 15f, 80f);
            BoundsMax.x = Mathf.Max(BoundsMin.x, BoundsMax.x);
            BoundsMax.y = Mathf.Max(BoundsMin.y, BoundsMax.y);
        }
    }
}
