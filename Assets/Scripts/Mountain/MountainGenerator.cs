using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // Writes a mountain into the tilemaps: one hollow vein that runs all the way through it, hollow
    // dead-ends that lead nowhere, hard rock, and hollow pockets. The mountain's shape either comes
    // from the level's background art (see MountainArtLayout) or is made up: plains on both sides
    // of a cliff-faced block.
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
        public Vector2Int deadEndLength = new(10, 26);

        // Where Farhad's feet start, and the middle of the goal, after the last Generate.
        public Vector3 SpawnPoint { get; private set; }
        public Vector3 GoalPoint { get; private set; }

        RockType[,] cells;
        int[] tops; // per column: the first row above the highest rock

        // The made-up mountain: a cliff-faced block with a hard crust on top, so it can't be climbed over.
        [ContextMenu("Generate")]
        public void Generate()
        {
            var rng = new System.Random(seed);
            float ox = rng.Next(0, 10000), oy = rng.Next(0, 10000);
            var shape = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                bool inMountain = x >= mountainStartX && x <= mountainEndX;
                int top = inMountain ? height - 1 - Mathf.RoundToInt(Mathf.PerlinNoise(ox + x * 0.15f, oy) * 4f) : groundLevel;
                for (int y = 0; y < top; y++) shape[x, y] = true;
            }

            var entrance = new Vector2Int(mountainStartX, groundLevel);
            var exit = new Vector2Int(mountainEndX, groundLevel);
            Build(rng, shape, entrance, exit, crust: 3, veinRoof: 8, wallWorldEdges: true);
            SpawnPoint = new Vector3(6.5f, groundLevel, 0f);
            GoalPoint = new Vector3((mountainEndX + width) * 0.5f, groundLevel + 2f, 0f);
        }

        // The mountain drawn in the background art. shape[x, y] is true for cells that are rock, and
        // backWallShape for every cell the drawn mountain reaches into, even a little (the back wall is
        // only seen, never touched). The vein runs from the foot of the mountain (entrance, on the
        // ground Farhad starts on) to where it breaks out at the far side (exit, the first open cell
        // above the rock there). Dead ends are scaled to the mountain's width, so a small mountain
        // isn't hollowed out.
        public void Generate(bool[,] shape, bool[,] backWallShape, Vector2Int entrance, Vector2Int exit, float spawnX)
        {
            width = shape.GetLength(0);
            height = shape.GetLength(1);
            groundLevel = entrance.y;
            mountainStartX = entrance.x;
            mountainEndX = exit.x;
            int span = exit.x - entrance.x;
            deadEndVeins = Math.Max(1, span / 8);
            deadEndLength = new Vector2Int(Math.Max(3, span / 5), Math.Max(5, span / 2));

            Build(new System.Random(seed), shape, entrance, exit, crust: 0, veinRoof: 4, wallWorldEdges: false, backWallShape);
            SpawnPoint = new Vector3(spawnX, entrance.y, 0f);
            GoalPoint = new Vector3(exit.x + 1.5f, exit.y + 1.5f, 0f);
        }

        void Build(System.Random rng, bool[,] shape, Vector2Int entrance, Vector2Int exit,
            int crust, int veinRoof, bool wallWorldEdges, bool[,] backWallShape = null)
        {
            float ox = rng.Next(0, 10000), oy = rng.Next(0, 10000);
            cells = new RockType[width, height];
            tops = new int[width];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!shape[x, y]) continue;
                tops[x] = y + 1;
                bool worldEdge = wallWorldEdges && (x == 0 || x == width - 1);
                cells[x, y] = worldEdge || y < 2 ? RockType.Hard : RockType.Solid;
            }

            for (int x = entrance.x; x <= exit.x; x++)
            {
                // A hard crust on the mountain (not on the plains) where it must not be dug from above.
                if (tops[x] > groundLevel + 1)
                    for (int y = Math.Max(2, tops[x] - crust); y < tops[x]; y++)
                        if (cells[x, y] != RockType.Empty) cells[x, y] = RockType.Hard;

                for (int y = 2; y < tops[x] - Math.Max(crust, 1); y++)
                {
                    if (cells[x, y] != RockType.Solid) continue;
                    if (Mathf.PerlinNoise(ox + 50f + x * 0.11f, oy + 50f + y * 0.11f) > hardRockThreshold)
                        cells[x, y] = RockType.Hard;
                    else if (Mathf.PerlinNoise(ox + 200f + x * 0.2f, oy + 200f + y * 0.2f) > hollowPocketThreshold)
                        cells[x, y] = RockType.Hollow;
                }
            }

            var vein = CarveMainVein(rng, entrance, exit, veinRoof);
            for (int i = 0; i < deadEndVeins; i++) CarveDeadEnd(rng, vein[rng.Next(vein.Count)], entrance.x, exit.x);

            WriteTiles(backWallShape ?? shape, wallWorldEdges);
        }

        // A 3-cell-tall hollow corridor from the entrance to the exit, so the mountain can always be
        // crossed by following hollow rock alone. It climbs and falls through random waypoints so the
        // whole height of the mountain is used, staying veinRoof cells under the mountain's surface.
        List<Vector2Int> CarveMainVein(System.Random rng, Vector2Int entrance, Vector2Int exit, int veinRoof)
        {
            var waypoints = new List<Vector2Int>();
            for (int i = 1; i < veinWaypoints; i++)
            {
                int wx = entrance.x + (exit.x - entrance.x) * i / veinWaypoints;
                int highest = Math.Max(entrance.y + 1, tops[wx] - veinRoof - 2);
                waypoints.Add(new Vector2Int(wx, rng.Next(entrance.y, highest)));
            }
            waypoints.Add(exit);

            var vein = new List<Vector2Int>();
            int y = entrance.y;
            int next = 0;
            for (int x = entrance.x; x <= exit.x; x++)
            {
                int prevY = y;
                while (next < waypoints.Count - 1 && waypoints[next].x <= x) next++;
                int dy = waypoints[next].y - y;
                int dx = Math.Max(1, waypoints[next].x - x);

                // Head for the waypoint, steeper the less room is left, with the odd wiggle away from it.
                if (dy != 0 && (Math.Abs(dy) >= dx || rng.NextDouble() < 0.2 + 0.7 * Math.Abs(dy) / dx)) y += Math.Sign(dy);
                else if (rng.NextDouble() < 0.12) y += rng.Next(2) == 0 ? 1 : -1;

                // Near the exit the vein may rise to it, even if that is close to the surface.
                int roof = Math.Max(tops[x] - veinRoof, exit.x - x <= Math.Abs(exit.y - y) + 3 ? exit.y : 3);
                y = Mathf.Clamp(y, 3, Math.Max(3, roof));

                // Cover both the old and new floor height so every step can be jumped.
                Carve(x, Mathf.Min(prevY, y), 3 + Math.Abs(y - prevY));
                vein.Add(new Vector2Int(x, y));
            }
            return vein;
        }

        // A cramped 2-cell-tall hollow branch off the main vein that goes nowhere.
        void CarveDeadEnd(System.Random rng, Vector2Int from, int minX, int maxX)
        {
            int dirX = rng.Next(2) == 0 ? -1 : 1;
            int x = from.x;
            int y = rng.Next(2) == 0 ? from.y + 3 : from.y - 2;
            int length = rng.Next(deadEndLength.x, deadEndLength.y);
            for (int i = 0; i < length; i++)
            {
                x = Mathf.Clamp(x, minX + 1, maxX - 1);
                y = Mathf.Clamp(y, 3, Math.Max(3, tops[x] - 5));
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

        void WriteTiles(bool[,] backWallShape, bool wallWorldEdges)
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
                if (backWallShape[x, y]) wallPositions.Add(pos);
            }

            // Keep the world edges walled above the mountain too.
            if (wallWorldEdges)
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
