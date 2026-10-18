using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // Hollow rock breaks; solid rock never does, and striking it brings the mountain down.
    public enum RockType : byte { Empty, Solid, Hollow }

    // Runtime state of the diggable mountain. The rock type of each cell is the tile asset painted
    // into the rock tilemap, so levels can be generated or painted by hand with a Tile Palette.
    public class MountainGrid : MonoBehaviour
    {
        public Tilemap rockMap;
        public TileBase solidTile;
        public TileBase hollowTile; // looks exactly like solid rock: hollowness only shows when revealed

        [Header("Digging")]
        [Min(1)] public int hollowHits = 1;
        public Color damagedTint = new(0.45f, 0.38f, 0.34f);

        // Revealed hollow rock is drawn over with the artist's loose-stone tile in white, standing out
        // from the grey ink of the drawn mountain, on a tilemap just above the rock.
        [Header("Revealing hollow rock")]
        public Tilemap revealMap;
        public TileBase revealTile;
        public Color revealTint = Color.white;
        [Tooltip("Seconds the reveal takes to fade in, and again to fade out.")]
        [Min(0.01f)] public float revealFade = 0.6f;

        struct Reveal
        {
            public float start, end;
        }

        readonly Dictionary<Vector3Int, int> damage = new();
        readonly Dictionary<Vector3Int, Reveal> reveals = new();
        readonly List<Vector3Int> revealCells = new();
        bool debugReveal;

        public bool DebugReveal => debugReveal;

        void Awake()
        {
            if (revealMap == null)
                Debug.LogError("MountainGrid has no reveal tilemap, so calling Shirin can't show hollow rock. " +
                               "The scene is out of date: rebuild it with Farhad > Build Dig Prototype Scene.", this);
        }

        public RockType GetRock(Vector3Int cell)
        {
            var tile = rockMap.GetTile(cell);
            if (tile == null) return RockType.Empty;
            return tile == hollowTile ? RockType.Hollow : RockType.Solid;
        }

        // Returns true when the strike broke the rock. Solid rock never breaks.
        public bool Strike(Vector3Int cell)
        {
            if (GetRock(cell) != RockType.Hollow) return false;

            damage.TryGetValue(cell, out int hits);
            hits++;
            if (hits >= hollowHits)
            {
                damage.Remove(cell);
                rockMap.SetTile(cell, null);
                if (!debugReveal) HideReveal(cell);
                reveals.Remove(cell);
                return true;
            }

            damage[cell] = hits;
            rockMap.SetTileFlags(cell, TileFlags.None);
            rockMap.SetColor(cell, Color.Lerp(Color.white, damagedTint, hits / (float)hollowHits));
            return false;
        }

        // Shows the hollow rock within radius tiles of the area (Farhad's body) for duration seconds.
        // The reveal spreads outward at spread tiles per second. Returns how many hollow cells showed.
        public int RevealAround(Bounds area, float radius, float duration, float spread)
        {
            int found = 0;
            var min = rockMap.WorldToCell(area.min - Vector3.one * radius);
            var max = rockMap.WorldToCell(area.max + Vector3.one * radius);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (GetRock(cell) != RockType.Hollow) continue;
                var center = rockMap.GetCellCenterWorld(cell);
                center.z = area.center.z;
                float distance = Mathf.Sqrt(area.SqrDistance(center));
                if (distance > radius) continue;

                float start = Time.time + (spread > 0f ? distance / spread : 0f);
                var reveal = new Reveal { start = start, end = start + duration };
                if (reveals.TryGetValue(cell, out var old) && Time.time < old.end)
                {
                    // Already showing: keep it showing, without fading out and back in.
                    reveal.start = Mathf.Min(old.start, start);
                    reveal.end = Mathf.Max(old.end, reveal.end);
                }
                reveals[cell] = reveal;
                found++;
            }
            return found;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) SetDebugReveal(!debugReveal);

            if (reveals.Count == 0) return;
            revealCells.Clear();
            revealCells.AddRange(reveals.Keys);
            float now = Time.time;
            foreach (var cell in revealCells)
            {
                var r = reveals[cell];
                if (now >= r.end || GetRock(cell) != RockType.Hollow)
                {
                    reveals.Remove(cell);
                    if (!debugReveal) HideReveal(cell);
                    continue;
                }
                if (debugReveal) continue;

                float fadeIn = Mathf.InverseLerp(r.start, r.start + revealFade, now);
                float fadeOut = Mathf.InverseLerp(r.end, r.end - revealFade, now);
                ShowReveal(cell, Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut)));
            }
        }

        void SetDebugReveal(bool on)
        {
            debugReveal = on;
            foreach (var cell in rockMap.cellBounds.allPositionsWithin)
            {
                if (GetRock(cell) != RockType.Hollow) continue;
                if (on) ShowReveal(cell, 1f);
                else HideReveal(cell);
            }
        }

        void ShowReveal(Vector3Int cell, float alpha)
        {
            if (revealMap == null) return;
            if (revealMap.GetTile(cell) == null)
            {
                revealMap.SetTile(cell, revealTile);
                revealMap.SetTileFlags(cell, TileFlags.None);
            }
            var color = revealTint;
            color.a = alpha;
            revealMap.SetColor(cell, color);
        }

        void HideReveal(Vector3Int cell)
        {
            if (revealMap != null) revealMap.SetTile(cell, null);
        }
    }
}
