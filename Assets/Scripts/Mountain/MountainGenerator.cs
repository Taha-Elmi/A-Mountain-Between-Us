using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // Writes a mountain into the tilemaps: open plains on both sides, a cliff-faced mountain between,
    // one hollow vein that runs all the way through, and hollow dead-ends that lead nowhere.
    public class MountainGenerator : MonoBehaviour
    {
        public Tilemap rockMap;
        public Tilemap backWallMap;
        public TileBase solidTile;
        public TileBase hollowTile;
        public TileBase hardTile;
        public TileBase backWallTile;

        [Header("World (cells)")]
        public int width = 150;
        public int height = 44;
        public int groundLevel = 6;
        public int mountainStartX = 20;
        public int mountainEndX = 128;

        [Header("Generation")]
        public int seed = 1405;
        [Range(0f, 1f)] public float hardRockThreshold = 0.7f;
        [Range(0f, 1f)] public float hollowPocketThreshold = 0.8f;
        [Min(1)] public int veinWaypoints = 4;
        public int deadEndVeins = 10;

        // Where Farhad's feet start, on the plain before the cliff.
        public Vector3 SpawnPoint => new(6.5f, groundLevel, 0f);
        public Vector3 GoalPoint => new((mountainEndX + width) * 0.5f, groundLevel + 2f, 0f);

        RockType[,] cells;
        int[] tops;

        [ContextMenu("Generate")]
        public void Generate()
        {
            var rng = new System.Random(seed);
            float ox = rng.Next(0, 10000), oy = rng.Next(0, 10000);
            cells = new RockType[width, height];
            tops = new int[width];

            for (int x = 0; x < width; x++)
            {
                bool inMountain = IsMountain(x);
                int top = inMountain ? height - 1 - Mathf.RoundToInt(Mathf.PerlinNoise(ox + x * 0.15f, oy) * 4f) : groundLevel;
                tops[x] = top;
                bool worldEdge = x == 0 || x == width - 1;

                for (int y = 0; y < height; y++)
                {
                    if (worldEdge || y < 2 || (inMountain && y < top && y >= top - 3)) cells[x, y] = RockType.Hard;
                    else if (y < top) cells[x, y] = RockType.Solid;
                }
            }

            for (int x = mountainStartX; x <= mountainEndX; x++)
            for (int y = 2; y < tops[x] - 3; y++)
            {
                if (Mathf.PerlinNoise(ox + 50f + x * 0.11f, oy + 50f + y * 0.11f) > hardRockThreshold)
                    cells[x, y] = RockType.Hard;
                else if (Mathf.PerlinNoise(ox + 200f + x * 0.2f, oy + 200f + y * 0.2f) > hollowPocketThreshold)
                    cells[x, y] = RockType.Hollow;
            }

            var vein = CarveMainVein(rng);
            for (int i = 0; i < deadEndVeins; i++) CarveDeadEnd(rng, vein[rng.Next(vein.Count)]);

            WriteTiles();
        }

        bool IsMountain(int x) => x >= mountainStartX && x <= mountainEndX;

        // A 3-cell-tall hollow corridor from the cliff face to the far side, so the mountain can
        // always be crossed by following hollow rock alone. It climbs and falls through random
        // waypoints so the whole height of the mountain is used.
        List<Vector2Int> CarveMainVein(System.Random rng)
        {
            var waypoints = new List<Vector2Int>();
            for (int i = 1; i < veinWaypoints; i++)
            {
                int wx = mountainStartX + (mountainEndX - mountainStartX) * i / veinWaypoints;
                waypoints.Add(new Vector2Int(wx, rng.Next(groundLevel, tops[wx] - 10)));
            }
            waypoints.Add(new Vector2Int(mountainEndX, groundLevel));

            var vein = new List<Vector2Int>();
            int y = groundLevel;
            int next = 0;
            for (int x = mountainStartX; x <= mountainEndX; x++)
            {
                int prevY = y;
                while (next < waypoints.Count - 1 && waypoints[next].x <= x) next++;
                int dy = waypoints[next].y - y;
                int dx = Math.Max(1, waypoints[next].x - x);

                // Head for the waypoint, steeper the less room is left, with the odd wiggle away from it.
                if (dy != 0 && (Math.Abs(dy) >= dx || rng.NextDouble() < 0.2 + 0.7 * Math.Abs(dy) / dx)) y += Math.Sign(dy);
                else if (rng.NextDouble() < 0.12) y += rng.Next(2) == 0 ? 1 : -1;
                y = Mathf.Clamp(y, 3, tops[x] - 8);

                // Cover both the old and new floor height so every step can be jumped.
                Carve(x, Mathf.Min(prevY, y), 3 + Math.Abs(y - prevY));
                vein.Add(new Vector2Int(x, y));
            }
            return vein;
        }

        // A cramped 2-cell-tall hollow branch off the main vein that goes nowhere.
        void CarveDeadEnd(System.Random rng, Vector2Int from)
        {
            int dirX = rng.Next(2) == 0 ? -1 : 1;
            int x = from.x;
            int y = rng.Next(2) == 0 ? from.y + 3 : from.y - 2;
            int length = rng.Next(10, 26);
            for (int i = 0; i < length; i++)
            {
                x = Mathf.Clamp(x, mountainStartX + 1, mountainEndX - 1);
                y = Mathf.Clamp(y, 3, tops[x] - 5);
                Carve(x, y, 2);
                if (rng.NextDouble() < 0.6) x += dirX;
                else y += rng.Next(2) == 0 ? 1 : -1;
            }
        }

        void Carve(int x, int y, int cellsTall)
        {
            for (int i = 0; i < cellsTall; i++)
            {
                int cy = y + i;
                if (cy < 0 || cy >= height || cells[x, cy] == RockType.Empty) continue;
                cells[x, cy] = RockType.Hollow;
            }
        }

        void WriteTiles()
        {
            var rockPositions = new List<Vector3Int>();
            var rockTiles = new List<TileBase>();
            var wallPositions = new List<Vector3Int>();

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (cells[x, y] != RockType.Empty)
                {
                    rockPositions.Add(pos);
                    rockTiles.Add(TileFor(cells[x, y]));
                }
                if (y < groundLevel || (IsMountain(x) && y < tops[x])) wallPositions.Add(pos);
            }

            // Keep the world edges walled above the mountain too.
            for (int y = height; y < height + 8; y++)
            foreach (int x in new[] { 0, width - 1 })
            {
                rockPositions.Add(new Vector3Int(x, y, 0));
                rockTiles.Add(hardTile);
            }

            rockMap.ClearAllTiles();
            backWallMap.ClearAllTiles();
            rockMap.SetTiles(rockPositions.ToArray(), rockTiles.ToArray());
            backWallMap.SetTiles(wallPositions.ToArray(), Fill(backWallTile, wallPositions.Count));

#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        TileBase TileFor(RockType rock) => rock switch
        {
            RockType.Hollow => hollowTile,
            RockType.Hard => hardTile,
            _ => solidTile,
        };

        static TileBase[] Fill(TileBase tile, int count)
        {
            var tiles = new TileBase[count];
            Array.Fill(tiles, tile);
            return tiles;
        }
    }
}
