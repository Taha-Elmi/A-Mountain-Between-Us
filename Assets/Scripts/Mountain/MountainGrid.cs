using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Farhad
{
    public enum RockType : byte { Empty, Solid, Hollow, Hard }

    // Runtime state of the diggable mountain. The rock type of each cell is the tile asset painted
    // into the rock tilemap, so levels can be generated or painted by hand with a Tile Palette.
    public class MountainGrid : MonoBehaviour
    {
        public Tilemap rockMap;
        public TileBase solidTile;
        public TileBase hollowTile; // uses the same sprite as solidTile: hollow rock must not be visible
        public TileBase hardTile;

        [Header("Digging")]
        [Min(1)] public int solidHits = 5;
        [Min(1)] public int hollowHits = 1;
        public Color damagedTint = new(0.45f, 0.38f, 0.34f);

        [Header("Knocking")]
        public Color echoTint = new(1f, 0.8f, 0.45f);
        public float echoDuration = 2.5f;

        readonly Dictionary<Vector3Int, int> damage = new();
        readonly Dictionary<Vector3Int, float> echoEnds = new();
        readonly List<Vector3Int> echoCells = new();
        bool debugReveal;

        public bool DebugReveal => debugReveal;

        public RockType GetRock(Vector3Int cell)
        {
            var tile = rockMap.GetTile(cell);
            if (tile == null) return RockType.Empty;
            if (tile == hollowTile) return RockType.Hollow;
            if (tile == hardTile) return RockType.Hard;
            return RockType.Solid;
        }

        public int HitsFor(RockType rock) => rock == RockType.Hollow ? hollowHits : solidHits;

        // Returns true when the strike broke the rock.
        public bool Strike(Vector3Int cell)
        {
            var rock = GetRock(cell);
            if (rock is RockType.Empty or RockType.Hard) return false;

            damage.TryGetValue(cell, out int hits);
            hits++;
            if (hits >= HitsFor(rock))
            {
                damage.Remove(cell);
                echoEnds.Remove(cell);
                rockMap.SetTile(cell, null);
                return true;
            }

            damage[cell] = hits;
            RefreshColor(cell);
            return false;
        }

        // Makes hollow rock around the cell ring back for a moment. Returns how many hollow cells answered.
        public int Knock(Vector3Int cell, float radius)
        {
            int answered = 0;
            int r = Mathf.CeilToInt(radius);
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y > radius * radius) continue;
                var c = cell + new Vector3Int(x, y, 0);
                if (GetRock(c) != RockType.Hollow) continue;
                echoEnds[c] = Time.time + echoDuration;
                answered++;
            }
            return answered;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) SetDebugReveal(!debugReveal);

            if (echoEnds.Count == 0) return;
            echoCells.Clear();
            echoCells.AddRange(echoEnds.Keys);
            foreach (var c in echoCells)
            {
                if (Time.time >= echoEnds[c]) echoEnds.Remove(c);
                RefreshColor(c);
            }
        }

        void SetDebugReveal(bool on)
        {
            debugReveal = on;
            foreach (var c in rockMap.cellBounds.allPositionsWithin)
                if (GetRock(c) == RockType.Hollow) RefreshColor(c);
        }

        void RefreshColor(Vector3Int cell)
        {
            var rock = GetRock(cell);
            if (rock == RockType.Empty) return;

            var color = Color.white;
            if (damage.TryGetValue(cell, out int hits))
                color = Color.Lerp(Color.white, damagedTint, hits / (float)HitsFor(rock));

            float glow = 0f;
            if (echoEnds.TryGetValue(cell, out float end))
                glow = Mathf.SmoothStep(0f, 1f, (end - Time.time) / echoDuration);
            if (debugReveal && rock == RockType.Hollow)
                glow = Mathf.Max(glow, 0.6f);

            rockMap.SetTileFlags(cell, TileFlags.None);
            rockMap.SetColor(cell, Color.Lerp(color, echoTint, glow));
        }
    }
}
