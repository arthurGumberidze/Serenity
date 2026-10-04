using UnityEngine;

namespace Game.Presentation.CameraControl
{
    public static class RtsCameraMath
    {
        public static Vector2 GetEdgeDirection(Vector2 pointer, Vector2 screenSize, float edgeWidthNormalized)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
                return Vector2.zero;
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > screenSize.x || pointer.y > screenSize.y)
                return Vector2.zero;

            var normalized = new Vector2(pointer.x / screenSize.x, pointer.y / screenSize.y);
            var width = Mathf.Clamp(edgeWidthNormalized, 0.001f, 0.25f);
            var direction = Vector2.zero;
            if (normalized.x <= width) direction.x = -1f;
            else if (normalized.x >= 1f - width) direction.x = 1f;
            if (normalized.y <= width) direction.y = -1f;
            else if (normalized.y >= 1f - width) direction.y = 1f;
            return Vector2.ClampMagnitude(direction, 1f);
        }

        public static Vector3 HorizontalMotion(Vector2 input, float yawDegrees)
        {
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var motion = rotation * new Vector3(input.x, 0f, input.y);
            motion.y = 0f;
            return Vector3.ClampMagnitude(motion, 1f);
        }

        public static Vector3 HorizontalVector(Vector2 input, float yawDegrees)
        {
            var motion = Quaternion.Euler(0f, yawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
            motion.y = 0f;
            return motion;
        }

        public static Vector3 ClampToBounds(Vector3 position, Vector2 minimum, Vector2 maximum)
        {
            position.x = Mathf.Clamp(position.x, minimum.x, maximum.x);
            position.z = Mathf.Clamp(position.z, minimum.y, maximum.y);
            return position;
        }
    }
}
