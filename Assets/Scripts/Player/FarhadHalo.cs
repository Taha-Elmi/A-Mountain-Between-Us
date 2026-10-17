using UnityEngine;

namespace Farhad
{
    // Keeps Farhad readable in the black-and-white world: a white glow around his silhouette that
    // mirrors his current animation frame, and flares when he calls Shirin.
    // Draws with the Farhad/SpriteHalo shader on its own renderer, just behind his sprite.
    [RequireComponent(typeof(SpriteRenderer))]
    public class FarhadHalo : MonoBehaviour
    {
        public SpriteRenderer body;
        public ShirinCall call; // optional

        public Color color = new(1f, 1f, 1f, 0.85f);
        public Color callColor = new(1f, 1f, 1f, 1f);
        [Tooltip("Glow width in texels of his sprite: about 4 texels per screen pixel at 1080p.")]
        public float radius = 28f;
        public float callRadius = 40f;
        [Tooltip("Seconds the flare takes to die down after he calls Shirin.")]
        public float callFlareTime = 1.5f;

        static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");

        SpriteRenderer halo;
        MaterialPropertyBlock block;

        void Awake()
        {
            halo = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            halo.enabled = body.enabled;
            halo.sprite = body.sprite;
            halo.flipX = body.flipX;

            float flare = call != null ? 1f - Mathf.Clamp01((Time.time - call.LastCallTime) / callFlareTime) : 0f;
            flare *= flare;
            halo.GetPropertyBlock(block);
            block.SetColor(HaloColorId, Color.Lerp(color, callColor, flare));
            block.SetFloat(RadiusId, Mathf.Lerp(radius, callRadius, flare));
            halo.SetPropertyBlock(block);
        }
    }
}
