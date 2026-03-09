using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class WeightedTile
{
    [Tooltip("A tile from your tileset.")]
    public TileBase tile;

    [Tooltip("Relative weight. Higher = appears more often. Weights are normalized automatically.")]
    [Min(0f)]
    public float weight = 1f;
}

/// <summary>
/// Attach this to the same GameObject as ChunkManager.
///
/// SETUP:
///   1. Create a Grid in your scene and assign it to 'tilemapGrid'.
///   2. Fill 'Ground Tiles' and optionally 'Surface Tiles' with weighted tiles.
///   3. Assign 'Trunk Tile' and 'Leaves Tile' for tree generation.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    [Header("Tilemap Setup")]
    public Grid tilemapGrid;

    [Header("Chunk Dimensions")]
    [Tooltip("Must match ChunkManager.chunkWidth.")]
    public int chunkWidth = 32;
    public int chunkHeight = 16;

    // -------------------------------------------------------------------------
    [Header("Ground Tiles")]
    public WeightedTile[] groundTiles;
    public float tileNoiseFrequency = 0.12f;

    // -------------------------------------------------------------------------
    [Header("Surface Layer")]
    public WeightedTile[] surfaceTiles;

    [Min(1)] public int surfaceDepth = 2;
    public float surfaceFuzzFrequency = 0.25f;
    [Min(0)] public int surfaceFuzzAmplitude = 2;
    public float surfaceTileNoiseFrequency = 0.15f;

    // -------------------------------------------------------------------------
    [Header("Trees")]
    [Tooltip("Tile used for the tree trunk.")]
    public TileBase trunkTile;

    [Tooltip("Tile used for the leaf canopy.")]
    public TileBase leavesTile;

    [Tooltip("How often trees appear. 0 = none, 1 = maximum density. Trees cluster naturally via Perlin noise.")]
    [Range(0f, 1f)]
    public float treeFrequency = 0.4f;

    [Tooltip("Noise scale for tree placement regions. Lower = broader sparse/dense zones.")]
    public float treePlacementScale = 0.3f;

    [Tooltip("Minimum trunk height in tiles.")]
    [Min(1)] public int trunkHeightMin = 3;

    [Tooltip("Maximum trunk height in tiles.")]
    [Min(1)] public int trunkHeightMax = 6;

    [Tooltip("Minimum canopy half-width in tiles (horizontal radius).")]
    [Min(1)] public int canopyWidthMin = 3;

    [Tooltip("Maximum canopy half-width in tiles (horizontal radius).")]
    [Min(1)] public int canopyWidthMax = 6;

    [Tooltip("Minimum canopy half-height in tiles (vertical radius). Keep this smaller than width for a flat, natural-looking crown.")]
    [Min(1)] public int canopyHeightMin = 2;

    [Tooltip("Maximum canopy half-height in tiles (vertical radius).")]
    [Min(1)] public int canopyHeightMax = 4;

    [Tooltip("Noise scale for warping the canopy silhouette. Higher = more jagged.")]
    public float canopyWarpScale = 0.6f;

    [Tooltip("Max tiles the warp noise can add or remove from the canopy radius at any angle.")]
    [Min(0)] public int canopyWarpAmplitude = 2;

    [Tooltip("Minimum flat ground required on each side of the trunk before a tree will spawn.")]
    [Min(0)] public int treeClearance = 2;

    [Tooltip("How many tiles of height difference are allowed in the clearance zone. 1 = perfectly flat only, 3 = gentle slopes allowed.")]
    [Min(1)] public int treeClearanceTolerance = 3;

    [Tooltip("Minimum horizontal distance in tiles between any two tree trunks.")]
    [Min(1)] public int treeMinSpacing = 4;

    // -------------------------------------------------------------------------
    [Header("Terrain Noise")]
    public float macroFrequency = 0.008f;
    public float macroAmplitude = 8f;
    public float broadFrequency = 0.04f;
    public float broadAmplitude = 4f;
    public float midFrequency = 0.09f;
    public float midAmplitude = 3f;
    public float detailFrequency = 0.15f;
    public float detailAmplitude = 2f;
    public int floorBuffer = 4;

    // -------------------------------------------------------------------------
    // Internal
    private int _seed;
    private float[] _groundThresholds;
    private float[] _surfaceThresholds;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates and returns the shared decoration Tilemap under the Grid.
    /// Call once from ChunkManager.Start() before any chunks are generated.
    /// </summary>
    public Tilemap CreateTreeLayerTilemap()
    {
        GameObject go = new GameObject("TreeLayer");
        go.tag = "Destructible";
        go.transform.SetParent(tilemapGrid.transform, false);
        go.transform.localPosition = Vector3.zero;

        Tilemap tm = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>();
        go.AddComponent<TilemapCollider2D>();
        return tm;
    }

    public void InitSeed()
    {
        _seed = Random.Range(0, 100000);
        Debug.Log($"[TerrainGenerator] World seed: {_seed}");
        _groundThresholds = BuildThresholds(groundTiles, "groundTiles");
        _surfaceThresholds = BuildThresholds(surfaceTiles, "surfaceTiles");
    }

    /// <summary>
    /// Generates a terrain chunk.
    /// decoTilemap  — the shared decoration tilemap owned by ChunkManager.
    /// decoTileList — populated with every world-space tile position written to
    ///                decoTilemap so ChunkManager can erase them on chunk destroy.
    /// </summary>
    public GameObject GenerateChunk(int chunkIndex, float worldOriginX,
                                    Tilemap decoTilemap, List<Vector3Int> decoTileList)
    {
        // --- Setup chunk GameObject ---
        GameObject chunkGO = new GameObject($"Chunk_{chunkIndex}");
        chunkGO.transform.SetParent(tilemapGrid.transform, false);
        chunkGO.transform.localPosition = new Vector3(worldOriginX, 0, 0);

        chunkGO.tag = "Destructible";
        Tilemap tilemap = chunkGO.AddComponent<Tilemap>();
        chunkGO.AddComponent<TilemapRenderer>();
        TilemapCollider2D collider = chunkGO.AddComponent<TilemapCollider2D>();

        // Per-tile colliders — required for destructible blocks so each tile's
        // collider is removed automatically when the tile is cleared, with no
        // composite rebuild needed.
        collider.useDelaunayMesh = false;

        int globalTileXStart = chunkIndex * chunkWidth;

        // --- Pass 1: heightmap with lookahead buffer ---
        int lookAhead = treeClearance + canopyWidthMax + canopyWarpAmplitude + 1;
        int mapWidth = chunkWidth + lookAhead * 2;
        int[] heights = new int[mapWidth];

        for (int i = 0; i < mapWidth; i++)
            heights[i] = GetSurfaceHeight(globalTileXStart - lookAhead + i);

        int CachedHeight(int gx)
        {
            int i = gx - (globalTileXStart - lookAhead);
            return (i >= 0 && i < mapWidth) ? heights[i] : GetSurfaceHeight(gx);
        }

        // --- Pass 2: decide tree placements ---
        var trees = new Dictionary<int, TreeData>();

        if (trunkTile != null && leavesTile != null)
        {
            int dbgNoise = 0, dbgClear = 0, dbgSpacing = 0, dbgPlaced = 0;

            for (int localX = 0; localX < chunkWidth; localX++)
            {
                int globalTileX = globalTileXStart + localX;
                int surface = CachedHeight(globalTileX);

                if (!ShouldPlaceTree(globalTileX)) { dbgNoise++; continue; }

                // Clearance: neighbours within treeClearance must be within 1 tile of trunk surface.
                bool clear = true;
                for (int dx = -treeClearance; dx <= treeClearance && clear; dx++)
                {
                    if (dx == 0) continue;
                    if (Mathf.Abs(CachedHeight(globalTileX + dx) - surface) > treeClearanceTolerance)
                        clear = false;
                }
                if (!clear) { dbgClear++; continue; }

                // Spacing: reject if a tree that actually passed all checks
                // was already placed within treeMinSpacing to the left.
                bool tooClose = false;
                for (int prevX = localX - treeMinSpacing; prevX < localX && !tooClose; prevX++)
                {
                    if (prevX >= 0 && trees.ContainsKey(prevX))
                        tooClose = true;
                }
                if (tooClose) { dbgSpacing++; continue; }

                int trunkH = DeterministicRange(globalTileX, 17f, trunkHeightMin, trunkHeightMax);
                int canopyW = DeterministicRange(globalTileX, 31f, canopyWidthMin, canopyWidthMax);
                int canopyH = DeterministicRange(globalTileX, 43f, canopyHeightMin, canopyHeightMax);

                trees[localX] = new TreeData(surface, trunkH, canopyW, canopyH);
                dbgPlaced++;
            }

            Debug.Log($"[Trees] Chunk {chunkIndex}: noise_filtered={dbgNoise} clear_filtered={dbgClear} spacing_filtered={dbgSpacing} placed={dbgPlaced} | trunkTile={(trunkTile != null)} leavesTile={(leavesTile != null)} freq={treeFrequency}");
        }
        else
        {
            Debug.LogWarning($"[Trees] Chunk {chunkIndex}: Skipped — trunkTile={(trunkTile != null)} leavesTile={(leavesTile != null)}");
        }

        // --- Pass 3: paint ground tiles ---
        for (int localX = 0; localX < chunkWidth; localX++)
        {
            int globalTileX = globalTileXStart + localX;
            int surfaceHeight = CachedHeight(globalTileX);
            int fuzzedDepth = GetFuzzedSurfaceDepth(globalTileX);

            for (int y = 0; y <= surfaceHeight; y++)
            {
                bool isSurfaceLayer = _surfaceThresholds != null
                                      && y > surfaceHeight - fuzzedDepth;

                TileBase tile = isSurfaceLayer
                    ? SampleTile(globalTileX, y, surfaceTiles, _surfaceThresholds, surfaceTileNoiseFrequency, 5f)
                    : SampleTile(globalTileX, y, groundTiles, _groundThresholds, tileNoiseFrequency, 0f);

                tilemap.SetTile(new Vector3Int(localX, y, 0), tile);
            }
        }

        // --- Pass 4: paint trunks onto the terrain tilemap, canopies onto the deco tilemap ---
        foreach (var kvp in trees)
        {
            int localX = kvp.Key;
            TreeData tree = kvp.Value;
            int globalTileX = globalTileXStart + localX;

            // Trunk — painted onto the solid terrain tilemap so it has collision.
            for (int t = 1; t <= tree.trunkHeight; t++)
                tilemap.SetTile(new Vector3Int(localX, tree.surfaceY + t, 0), trunkTile);

            // Canopy centre sits at the very top of the trunk.
            int canopyCenterY = tree.surfaceY + tree.trunkHeight;

            // The canopy is an upper ellipse: width controls horizontal spread,
            // height controls vertical rise. dy < 0 is skipped so leaves never
            // wrap back down around the trunk.
            int rw = tree.canopyWidth;
            int rh = tree.canopyHeight;
            int warp = canopyWarpAmplitude;

            for (int dy = 0; dy <= rh + warp; dy++)
            {
                for (int dx = -(rw + warp); dx <= rw + warp; dx++)
                {
                    // Ellipse equation: (dx/rw)^2 + (dy/rh)^2 <= 1
                    // We normalise dx and dy by their respective radii so width
                    // and height scale independently.
                    float nx = rw > 0 ? (float)dx / rw : 0f;
                    float ny = rh > 0 ? (float)dy / rh : 0f;
                    float ellipseDist = Mathf.Sqrt(nx * nx + ny * ny);

                    float angle = Mathf.Atan2(dy, dx);
                    float warpSample = Mathf.PerlinNoise(
                        (angle / (2f * Mathf.PI) + 0.5f) * canopyWarpScale + globalTileX * 0.07f + _seed * 0.00001f,
                        globalTileX * 0.05f + _seed * 0.00001f
                    );
                    // Warp is applied in normalised ellipse space so it scales
                    // proportionally rather than always adding the same tile count.
                    float warpedEdge = 1f + (warpSample * 2f - 1f) * (warp / (float)Mathf.Max(rw, rh));

                    if (ellipseDist > warpedEdge) continue;

                    // World-space tile position for the deco tilemap (which lives at Grid origin).
                    int worldTileX = globalTileX + dx;
                    int worldTileY = canopyCenterY + dy;
                    var worldPos = new Vector3Int(worldTileX, worldTileY, 0);

                    decoTilemap.SetTile(worldPos, leavesTile);
                    decoTileList.Add(worldPos);
                }
            }
        }

        return chunkGO;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    bool ShouldPlaceTree(int globalTileX)
    {
        float noise = Mathf.PerlinNoise(globalTileX * treePlacementScale + _seed * 0.00001f, 13f);
        return noise > (1f - treeFrequency);
    }

    int DeterministicRange(int globalTileX, float salt, int min, int max)
    {
        float t = Mathf.PerlinNoise(globalTileX * 0.37f + _seed * 0.00001f, salt);
        return min + Mathf.RoundToInt(t * (max - min));
    }

    int GetFuzzedSurfaceDepth(int globalTileX)
    {
        if (surfaceFuzzAmplitude == 0) return surfaceDepth;
        float fuzz = Mathf.PerlinNoise(globalTileX * surfaceFuzzFrequency + _seed * 0.00001f, 9f);
        return surfaceDepth + Mathf.RoundToInt(fuzz * surfaceFuzzAmplitude);
    }

    TileBase SampleTile(int globalTileX, int tileY, WeightedTile[] tiles, float[] thresholds, float frequency, float yOffset)
    {
        if (thresholds == null || thresholds.Length == 0)
            return groundTiles[0].tile;

        float noiseValue = Mathf.PerlinNoise(
            globalTileX * frequency + _seed * 0.00001f,
            tileY * frequency + _seed * 0.00001f + yOffset
        );

        for (int i = 0; i < thresholds.Length; i++)
            if (noiseValue <= thresholds[i]) return tiles[i].tile;

        return tiles[tiles.Length - 1].tile;
    }

    int GetSurfaceHeight(int globalTileX)
    {
        float o = _seed * 0.00001f;
        float macro = Mathf.PerlinNoise(globalTileX * macroFrequency + o, 2f) * macroAmplitude;
        float broad = Mathf.PerlinNoise(globalTileX * broadFrequency + o, 0f) * broadAmplitude;
        float mid = Mathf.PerlinNoise(globalTileX * midFrequency + o, 3f) * midAmplitude;
        float detail = Mathf.PerlinNoise(globalTileX * detailFrequency + o, 1f) * detailAmplitude;
        return floorBuffer + Mathf.RoundToInt(macro + broad + mid + detail);
    }

    float[] BuildThresholds(WeightedTile[] tiles, string label)
    {
        if (tiles == null || tiles.Length == 0) return null;

        float total = 0f;
        foreach (var wt in tiles) total += Mathf.Max(0f, wt.weight);
        if (total <= 0f) { Debug.LogError($"[TerrainGenerator] All weights zero in {label}!"); return null; }

        float[] thresholds = new float[tiles.Length];
        float cumulative = 0f;
        for (int i = 0; i < tiles.Length; i++)
        {
            cumulative += Mathf.Max(0f, tiles[i].weight) / total;
            thresholds[i] = cumulative;
        }
        thresholds[tiles.Length - 1] = 1f;
        return thresholds;
    }

    // -------------------------------------------------------------------------

    private struct TreeData
    {
        public int surfaceY;
        public int trunkHeight;
        public int canopyWidth;
        public int canopyHeight;

        public TreeData(int surfaceY, int trunkHeight, int canopyWidth, int canopyHeight)
        {
            this.surfaceY = surfaceY;
            this.trunkHeight = trunkHeight;
            this.canopyWidth = canopyWidth;
            this.canopyHeight = canopyHeight;
        }
    }
}