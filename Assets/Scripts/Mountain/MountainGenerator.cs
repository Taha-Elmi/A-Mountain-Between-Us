using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // Writes a mountain into the tilemaps: solid rock with one winding hollow way through it, hollow
    // decoys branching off it, and lone hollow pockets (see HollowLayout). The way is planned around
    // Farhad's calls to Shirin, so a player who remembers what each call shows can get through with
    // them. The mountain's shape either comes from the level's background art (see MountainArtLayout)
    // or is made up: plains on both sides of a cliff-faced block. Every time the level loads, the
    // hollow rock is laid out anew in the same shape.
    [DefaultExecutionOrder(-100)]
    public class MountainGenerator : MonoBehaviour
    {
        public Tilemap rockMap;
        public Tilemap backWallMap;
        public TileBase solidTile;
        public TileBase hollowTile;
        public TileBase backWallTile;

        [Header("World (cells)")]
        public int width = 150;
        public int height = 44;
        public int groundLevel = 6;
        public int mountainStartX = 20;
        public int mountainEndX = 128;
        [Tooltip("Where Farhad stands once he has broken out on the far side. Set when the mountain is built.")]
        public Vector2Int exit = new(-1, -1);

        [Header("Hollow rock")]
        [Tooltip("Lay the hollow rock out anew for every run (retries keep it). Off: keep the layout saved in the scene.")]
        public bool randomizeOnPlay = true;
        [Tooltip("The layout saved in the scene when the mountain is built. Each random layout is logged with its seed.")]
        public int seed = 1405;
        [Tooltip("Farhad's calls to Shirin: the way through is planned around how many there are and how far they reach.")]
        public ShirinCall call;
        [Tooltip("Tiles taken off a call's reach when planning, so the way can still be followed from a call made a little short of the ideal spot.")]
        [Min(0f)] public float revealSlack = 0.5f;
        [Tooltip("Rows the way through must climb and drop in all, so it winds instead of running straight across.")]
        [Min(0)] public int minVerticalTravel = 12;
        [Tooltip("Hollow branches off the way that look just like it but lead nowhere.")]
        [Min(0)] public int decoys = 8;
        [Tooltip("Moves in each decoy, shortest to longest.")]
        public Vector2Int decoyMoves = new(3, 7);
        [Tooltip("Lone pockets of one to three hollow cells.")]
        [Min(0)] public int pockets = 8;
        [Tooltip("Cells of solid rock always kept between hollow rock and the mountain's outside, except where the way goes in and comes out.")]
        [Min(1)] public int surfaceMargin = 1;

        // Where Farhad's feet start, and the middle of the goal, after the last Generate.
        public Vector3 SpawnPoint { get; private set; }
        public Vector3 GoalPoint { get; private set; }

        bool[,] shape;
        bool[,] hollow;

        Vector2Int Entrance => new(mountainStartX, groundLevel);

        void Awake()
        {
            // The scene saves each rock cell's sprite, but a PictureTile's squares are cut at runtime
            // and never saved, so a built level loads with every cell blank. Ask the tiles again.
            rockMap.RefreshAllTiles();

            // A retry after a rockfall keeps this run's layout; only a fresh run gets a new one.
            bool retry = DigRun.Enter();
            if (!randomizeOnPlay) return;
            if (Relayout(DigRun.Seed) && !retry) Debug.Log($"Mountain laid out with seed {DigRun.Seed}.", this);
        }

        // The made-up mountain: a cliff-faced block.
        [ContextMenu("Generate")]
        public void Generate()
        {
            var rng = new System.Random(seed);
            float ox = rng.Next(0, 10000), oy = rng.Next(0, 10000);
            shape = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                bool inMountain = x >= mountainStartX && x <= mountainEndX;
                int top = inMountain ? height - 1 - Mathf.RoundToInt(Mathf.PerlinNoise(ox + x * 0.15f, oy) * 4f) : groundLevel;
                for (int y = 0; y < top; y++) shape[x, y] = true;
            }

            exit = new Vector2Int(mountainEndX, groundLevel);
            LayOut(seed);
            WriteTiles(shape, wallWorldEdges: true);
            SpawnPoint = new Vector3(6.5f, groundLevel, 0f);
            GoalPoint = new Vector3((mountainEndX + width) * 0.5f, groundLevel + 2f, 0f);
        }

        // The mountain drawn in the background art. shape[x, y] is true for cells that are rock, and
        // backWallShape for every cell the drawn mountain reaches into, even a little (the back wall is
        // only seen, never touched). The way through runs from the foot of the mountain (entrance, on
        // the ground Farhad starts on) to where it breaks out at the far side (exit, the first open
        // cell above the rock there).
        public void Generate(bool[,] shape, bool[,] backWallShape, Vector2Int entrance, Vector2Int exit, float spawnX)
        {
            this.shape = shape;
            width = shape.GetLength(0);
            height = shape.GetLength(1);
            groundLevel = entrance.y;
            mountainStartX = entrance.x;
            mountainEndX = exit.x;
            this.exit = exit;

            LayOut(seed);
            WriteTiles(backWallShape, wallWorldEdges: false);
            SpawnPoint = new Vector3(spawnX, entrance.y, 0f);
            GoalPoint = new Vector3(exit.x + 1.5f, exit.y + 1.5f, 0f);
        }

        // Lays the hollow rock out anew in the mountain already in the rock tilemap, keeping its shape.
        public bool Relayout(int layoutSeed)
        {
            shape = new bool[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                shape[x, y] = rockMap.HasTile(new Vector3Int(x, y, 0));

            // Scenes built before the exit was saved: the way breaks out on top of the far column.
            if (exit.x < 0)
            {
                int top = 0;
                for (int y = 0; y < height; y++) if (shape[mountainEndX, y]) top = y + 1;
                exit = new Vector2Int(mountainEndX, top);
            }

            if (!LayOut(layoutSeed)) return false;
            WriteRock();
            return true;
        }

        bool LayOut(int layoutSeed)
        {
            if (call == null) call = FindAnyObjectByType<ShirinCall>();
            var settings = new HollowLayout.Settings
            {
                calls = call != null ? call.calls : ShirinCall.DefaultCalls,
                revealRadius = call != null ? call.revealRadius : ShirinCall.DefaultRevealRadius,
                revealSlack = revealSlack,
                minVerticalTravel = minVerticalTravel,
                decoys = decoys,
                decoyMoves = decoyMoves,
                pockets = pockets,
                surfaceMargin = surfaceMargin,
            };

            var layout = HollowLayout.Plan(shape, Entrance, exit, settings, layoutSeed);
            if (layout == null)
            {
                Debug.LogError($"No way through the mountain could be laid out from {Entrance} to {exit}, so it is all solid. " +
                               "Check the mountain's entrance and exit.", this);
                hollow = new bool[width, height];
                return false;
            }
            if (!layout.FitsCalls)
                Debug.LogWarning($"The mountain is too big to cross with {settings.calls} calls reaching {settings.revealRadius} tiles, " +
                                 "so the way through was laid out without planning it around them. Raise the calls or their reveal radius.", this);
            else if (!layout.NeedsAllCalls)
                Debug.LogWarning($"Calls reaching {settings.revealRadius} tiles show so much of the mountain that fewer than {settings.calls} " +
                                 "are enough to cross it. Lower their reveal radius to make every call count.", this);
            hollow = layout.Hollow;
            return true;
        }

        void WriteTiles(bool[,] backWallShape, bool wallWorldEdges)
        {
            rockMap.ClearAllTiles();
            backWallMap.ClearAllTiles();
            WriteRock();

            // Keep the world edges walled above the mountain too.
            if (wallWorldEdges)
                for (int y = height; y < height + 8; y++)
                foreach (int x in new[] { 0, width - 1 })
                    rockMap.SetTile(new Vector3Int(x, y, 0), solidTile);

            var wallPositions = new List<Vector3Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (backWallShape[x, y]) wallPositions.Add(new Vector3Int(x, y, 0));
            backWallMap.SetTiles(wallPositions.ToArray(), Fill(backWallTile, wallPositions.Count));

#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        void WriteRock()
        {
            var positions = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!shape[x, y]) continue;
                positions.Add(new Vector3Int(x, y, 0));
                tiles.Add(hollow[x, y] ? hollowTile : solidTile);
            }
            rockMap.SetTiles(positions.ToArray(), tiles.ToArray());
        }

        static TileBase[] Fill(TileBase tile, int count)
        {
            var tiles = new TileBase[count];
            Array.Fill(tiles, tile);
            return tiles;
        }
    }
}
