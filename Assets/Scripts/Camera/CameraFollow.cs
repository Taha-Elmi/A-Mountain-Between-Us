using UnityEngine;

namespace Farhad
{
    // Smoothly follows the target while keeping the view inside the world bounds.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new(0f, 1.5f);
        public float smoothTime = 0.15f;
        public Vector2 minBounds;
        public Vector2 maxBounds;

        Camera cam;
        Vector3 velocity;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null) return;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            var goal = (Vector2)target.position + offset;
            goal.x = ClampAxis(goal.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            goal.y = ClampAxis(goal.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);

            var next = new Vector3(goal.x, goal.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, next, ref velocity, smoothTime);
        }

        static float ClampAxis(float value, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
