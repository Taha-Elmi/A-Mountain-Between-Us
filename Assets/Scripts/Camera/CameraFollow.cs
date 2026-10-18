using UnityEngine;
using UnityEngine.InputSystem;

namespace Farhad
{
    // Smoothly follows the target while keeping the view inside the world bounds, and can shake.
    // Scrolling down zooms out toward a view of the whole mountain; it eases back in after a pause.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new(0f, 1.5f);
        public float smoothTime = 0.15f;
        public Vector2 minBounds;
        public Vector2 maxBounds;

        [Header("Overview zoom")]
        [Min(1)] public int zoomSteps = 3; // scroll notches from the normal view to the whole mountain
        [Min(0f)] public float overviewHold = 2.5f; // seconds without scrolling before easing back in
        public float zoomSmoothTime = 0.25f;

        Camera cam;
        Vector3 velocity;
        Vector3 followPosition;
        float shakeAmount;
        float shakeDuration;
        float shakeEnd;
        float normalSize;
        float zoom; // 0 = normal view, 1 = whole mountain
        float zoomVelocity;
        float lastScrollTime;

        void Awake()
        {
            cam = GetComponent<Camera>();
            followPosition = transform.position;
            normalSize = cam.orthographicSize;
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
            UpdateZoom();
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

        void UpdateZoom()
        {
            var mouse = Mouse.current;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (scroll != 0f)
            {
                zoom = Mathf.Clamp01(zoom - Mathf.Sign(scroll) / zoomSteps);
                lastScrollTime = Time.unscaledTime;
            }
            if (zoom > 0f && Time.unscaledTime - lastScrollTime > overviewHold) zoom = 0f;

            var world = maxBounds - minBounds;
            float overviewSize = Mathf.Max(normalSize, world.y * 0.5f, world.x * 0.5f / cam.aspect);
            float goal = Mathf.Lerp(normalSize, overviewSize, zoom);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, goal, ref zoomVelocity, zoomSmoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
        }

        static float ClampAxis(float value, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
