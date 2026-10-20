using UnityEngine;
using UnityEngine.InputSystem;

namespace Farhad
{
    // Smoothly follows the target while keeping the view inside the world bounds (the background art),
    // and can shake. Scrolling down zooms out toward the widest view that still fits inside them; it
    // eases back in after a pause.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new(0f, 1.5f);
        public float smoothTime = 0.15f;
        public Vector2 minBounds;
        public Vector2 maxBounds;

        [Header("Overview zoom")]
        [Min(1)] public int zoomSteps = 3; // scroll notches from the normal view to the widest
        [Min(0f)] public float overviewHold = 2.5f; // seconds without scrolling before easing back in
        public float zoomSmoothTime = 0.25f;

        Camera cam;
        Vector3 velocity;
        Vector3 followPosition;
        float shakeAmount;
        float shakeDuration;
        float shakeEnd;
        float normalSize;
        float zoom; // 0 = normal view, 1 = widest view
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

            var goal = Clamp((Vector2)target.position + offset, halfWidth, halfHeight);
            var next = new Vector3(goal.x, goal.y, followPosition.z);
            followPosition = Vector3.SmoothDamp(followPosition, next, ref velocity, smoothTime);

            var shake = Vector3.zero;
            if (Time.time < shakeEnd)
                shake = (Vector3)Random.insideUnitCircle * (shakeAmount * (shakeEnd - Time.time) / shakeDuration);
            // Clamped again as shown: the smoothing lags behind a zoom out, and a shake can push past
            // the edge, and neither may show what lies beyond the bounds.
            var shown = Clamp(followPosition + shake, halfWidth, halfHeight);
            transform.position = new Vector3(shown.x, shown.y, followPosition.z);
        }

        Vector2 Clamp(Vector2 position, float halfWidth, float halfHeight) => new(
            ClampAxis(position.x, minBounds.x + halfWidth, maxBounds.x - halfWidth),
            ClampAxis(position.y, minBounds.y + halfHeight, maxBounds.y - halfHeight));

        void UpdateZoom()
        {
            var mouse = Mouse.current;
            float scroll = mouse != null && !PauseMenu.Paused ? mouse.scroll.ReadValue().y : 0f;
            if (scroll != 0f)
            {
                zoom = Mathf.Clamp01(zoom - Mathf.Sign(scroll) / zoomSteps);
                lastScrollTime = Time.unscaledTime;
            }
            if (zoom > 0f && Time.unscaledTime - lastScrollTime > overviewHold) zoom = 0f;

            // As far out as the view still fits inside the bounds both ways.
            var world = maxBounds - minBounds;
            float overviewSize = Mathf.Max(normalSize, Mathf.Min(world.y * 0.5f, world.x * 0.5f / cam.aspect));
            float goal = Mathf.Lerp(normalSize, overviewSize, zoom);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, goal, ref zoomVelocity, zoomSmoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
        }

        static float ClampAxis(float value, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
