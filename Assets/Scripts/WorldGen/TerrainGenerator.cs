using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A serializable entry pairing a tile with a percentage weight.
/// Weights do not need to sum to 100 — they are normalized automatically.
/// </summary>
[System.Serializable]
public class WeightedTile
{
    [Tooltip("A tile from your tileset.")]
    public TileBase tile;

    [Tooltip("Relative weight. Higher = appears more often. E.g. 70/20/10 across three tiles.")]
    [Min(0f)]
    public float weight = 1f;
}

/// <summary>
/// Attach this to the same GameObject as ChunkManager.
///
/// SETUP STEPS:
///   1. Create a Grid GameObject in your scene (GameObject > 2D Object > Tilemap).
///   2. Assign the Grid here via the 'tilemapGrid' field.
///   3. Populate the 'Ground Tiles' array with WeightedTile entries from your tileset.
///   4. Tune chunkHeight, floorBuffer, and the noise fields to your liking.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    [Header("Tilemap Setup")]
    [Tooltip("The parent Grid object in your scene that Tilemaps will be children of.")]
    public Grid tilemapGrid;

    [Header("Chunk Dimensions")]
    [Tooltip("Width of each chunk in tiles. Must match ChunkManager.chunkWidth.")]
    public int chunkWidth = 32;

    [Tooltip("Total height of the chunk in tiles. Tiles are filled from y=0 up to the surface height.")]
    public int chunkHeight = 16;

    [Header("Ground Tiles")]
    [Tooltip("Add tiles here and assign each a weight. Weights are normalized so they don't need to sum to 100.")]
    public WeightedTile[] groundTiles;

    [Tooltip("Noise frequency controlling how broadly tile types are distributed. Lower = larger patches of the same tile.")]
    public float tileNoiseFrequency = 0.12f;

    [Header("Terrain Noise")]
    [Tooltip("Very slow noise for large landscape-scale rises and dips.")]
    public float macroFrequency = 0.008f;

    [Tooltip("Amplitude (tile count) for macro variation.")]
    public float macroAmplitude = 8f;

    [Tooltip("Noise frequency for broad hills. Lower = wider hills.")]
    public float broadFrequency = 0.04f;

    [Tooltip("Amplitude (tile count) for broad hills.")]
    public float broadAmplitude = 4f;

    [Tooltip("Mid-range frequency to break up uniform broad hills.")]
    public float midFrequency = 0.09f;

    [Tooltip("Amplitude (tile count) for mid-range variation.")]
    public float midAmplitude = 3f;

    [Tooltip("Noise frequency for finer bumps.")]
    public float detailFrequency = 0.15f;

    [Tooltip("Amplitude (tile count) for finer bumps.")]
    public float detailAmplitude = 2f;

    [Tooltip("Base floor height (tiles from bottom). The terrain oscillates above this.")]
    public int floorBuffer = 4;

    // --- Internal ---
    private int _seed;

    // Cumulative normalized weight thresholds, built once in InitSeed.
    // e.g. weights [70, 20, 10] become thresholds [0.70, 0.90, 1.00]
    private float[] _tileThresholds;

    public void InitSeed()
    {
        _seed = Random.Range(0, 100000);
        Debug.Log($"[TerrainGenerator] World seed: {_seed}");
        BuildTileThresholds();
    }

    /// <summary>
    /// Pre-computes cumulative normalized thresholds from the weight array.
    /// Called once on init so we don't recompute every tile placement.
    /// </summary>
    void BuildTileThresholds()
    {
        if (groundTiles == null || groundTiles.Length == 0)
        {
            Debug.LogError("[TerrainGenerator] No ground tiles assigned!");
            return;
        }

        // Sum all weights.
        float total = 0f;
        foreach (var wt in groundTiles)
            total += Mathf.Max(0f, wt.weight);

        if (total <= 0f)
        {
            Debug.LogError("[TerrainGenerator] All tile weights are zero!");
            return;
        }

        // Build cumulative thresholds in [0, 1].
        _tileThresholds = new float[groundTiles.Length];
        float cumulative = 0f;
        for (int i = 0; i < groundTiles.Length; i++)
        {
            cumulative += Mathf.Max(0f, groundTiles[i].weight) / total;
            _tileThresholds[i] = cumulative;
        }

        // Force the last threshold to exactly 1 to avoid float precision gaps.
        _tileThresholds[groundTiles.Length - 1] = 1f;
    }

    /// <summary>
    /// Builds and returns a GameObject containing a Tilemap for the given chunk.
    /// </summary>
    public GameObject GenerateChunk(int chunkIndex, float worldOriginX)
    {
        GameObject chunkGO = new GameObject($"Chunk_{chunkIndex}");
        chunkGO.transform.SetParent(tilemapGrid.transform, false);
        chunkGO.transform.localPosition = new Vector3(worldOriginX, 0, 0);

        Tilemap tilemap = chunkGO.AddComponent<Tilemap>();
        TilemapRenderer renderer = chunkGO.AddComponent<TilemapRenderer>();
        TilemapCollider2D collider = chunkGO.AddComponent<TilemapCollider2D>();

        Rigidbody2D rb = chunkGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = chunkGO.AddComponent<CompositeCollider2D>();
        collider.compositeOperation = Collider2D.CompositeOperation.Merge;

        int globalTileXStart = chunkIndex * chunkWidth;

        for (int localX = 0; localX < chunkWidth; localX++)
        {
            int globalTileX = globalTileXStart + localX;
            int surfaceHeight = GetSurfaceHeight(globalTileX);

            for (int y = 0; y <= surfaceHeight; y++)
            {
                TileBase tile = GetTileForPosition(globalTileX, y);
                tilemap.SetTile(new Vector3Int(localX, y, 0), tile);
            }
        }

        return chunkGO;
    }

    /// <summary>
    /// Samples a Perlin noise value for this world position and maps it to a
    /// tile from the weighted array. Adjacent tiles with similar noise values
    /// will naturally cluster into patches.
    /// </summary>
    TileBase GetTileForPosition(int globalTileX, int tileY)
    {
        if (_tileThresholds == null || _tileThresholds.Length == 0)
            return groundTiles[0].tile;

        // Use a separate seed offset on the Y axis so vertical columns
        // aren't uniform stripes.
        float seedOffset = _seed * 0.1f;
        float noiseValue = Mathf.PerlinNoise(
            globalTileX * tileNoiseFrequency + seedOffset,
            tileY * tileNoiseFrequency + seedOffset
        );

        // Map the [0,1] noise value through our cumulative thresholds.
        for (int i = 0; i < _tileThresholds.Length; i++)
        {
            if (noiseValue <= _tileThresholds[i])
                return groundTiles[i].tile;
        }

        // Fallback (should never reach here).
        return groundTiles[groundTiles.Length - 1].tile;
    }

    /// <summary>
    /// Returns the surface tile Y for a given global tile X coordinate.
    /// </summary>
    int GetSurfaceHeight(int globalTileX)
    {
        float seedOffset = _seed * 0.1f;

        float macro = Mathf.PerlinNoise(globalTileX * macroFrequency + seedOffset, 2f) * macroAmplitude;
        float broad = Mathf.PerlinNoise(globalTileX * broadFrequency + seedOffset, 0f) * broadAmplitude;
        float mid = Mathf.PerlinNoise(globalTileX * midFrequency + seedOffset, 3f) * midAmplitude;
        float detail = Mathf.PerlinNoise(globalTileX * detailFrequency + seedOffset, 1f) * detailAmplitude;

        return floorBuffer + Mathf.RoundToInt(macro + broad + mid + detail);
    }
}