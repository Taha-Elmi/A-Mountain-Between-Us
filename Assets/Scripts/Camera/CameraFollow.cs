using UnityEngine;

namespace Farhad
{
    // Smoothly follows the target while keeping the view inside the world bounds, and can shake.
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
        Vector3 followPosition;
        float shakeAmount;
        float shakeDuration;
        float shakeEnd;

        void Awake()
        {
            cam = GetComponent<Camera>();
            followPosition = transform.position;
        }

        public void Shake(float amount, float duration)
        {
            // A new shake never weakens one already running.
            float remaining = Mathf.Max(0f, shakeEnd - Time.time);
            if (amount * duration < shakeAmount * remaining) return;
            shakeAmount = amount;
            shakeDuration = duration;
            shakeEnd = Time.time + duration;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            var goal = (Vector2)target.position + offset;
            goal.x = ClampAxis(goal.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            goal.y = ClampAxis(goal.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);

            var next = new Vector3(goal.x, goal.y, followPosition.z);
            followPosition = Vector3.SmoothDamp(followPosition, next, ref velocity, smoothTime);

            var shake = Vector3.zero;
            if (Time.time < shakeEnd)
                shake = (Vector3)Random.insideUnitCircle * (shakeAmount * (shakeEnd - Time.time) / shakeDuration);
            transform.position = followPosition + shake;
        }

        static float ClampAxis(float value, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
