using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Attach this to a persistent GameObject (e.g. "GameManager").
/// Tracks the player's current chunk and spawns/destroys chunks accordingly.
///
/// Chunk layout relative to player chunk (P):
///   [P-3][P-2][P-1][P][P+1][P+2]
///    ^^^--- destroyed beyond this     ^^^--- pre-generated ahead
///
/// A single shared decoration Tilemap (created at startup under the Grid) is
/// used for cross-chunk elements like tree canopies. When a chunk is destroyed,
/// its decoration tiles are erased from that shared layer.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player Transform - used to determine current chunk.")]
    public Transform player;

    // Fetched automatically from this same GameObject.
    private TerrainGenerator terrainGenerator;

    [Header("Chunk Settings")]
    [Tooltip("Must match TerrainGenerator.chunkWidth (32).")]
    public int chunkWidth = 32;

    [Tooltip("How many chunks to keep BEHIND the player before destroying them.")]
    public int chunksToKeepBehind = 3;

    [Tooltip("How many chunks to pre-generate AHEAD of the player.")]
    public int chunksToKeepAhead = 2;

    // --- Internal ---
    private Dictionary<int, GameObject> _activeChunks = new();
    private Dictionary<int, List<Vector3Int>> _chunkDecoTiles = new(); // tracks deco tiles per chunk
    private Tilemap _decoTilemap;
    private int _currentChunkIndex = 0;

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();

        if (player == null)
        {
            player = Camera.main.transform;
            Debug.LogWarning("[ChunkManager] No player assigned � falling back to Camera.main.");
        }

        // Create the shared decoration Tilemap once, as a sibling of chunk Tilemaps.
        _decoTilemap = terrainGenerator.CreateTreeLayerTilemap();

        terrainGenerator.InitSeed();

        _currentChunkIndex = WorldXToChunkIndex(player.position.x);
        RefreshChunks();
    }

    void Update()
    {
        int newChunkIndex = WorldXToChunkIndex(player.position.x);
        if (newChunkIndex != _currentChunkIndex)
        {
            _currentChunkIndex = newChunkIndex;
            RefreshChunks();
        }
    }

    void RefreshChunks()
    {
        int minChunk = _currentChunkIndex - chunksToKeepBehind;
        int maxChunk = _currentChunkIndex + chunksToKeepAhead;

        for (int i = minChunk; i <= maxChunk; i++)
        {
            if (!_activeChunks.ContainsKey(i))
                SpawnChunk(i);
        }

        List<int> toRemove = new();
        foreach (var kvp in _activeChunks)
        {
            if (kvp.Key < minChunk || kvp.Key > maxChunk)
                toRemove.Add(kvp.Key);
        }

        foreach (int idx in toRemove)
        {
            // Erase this chunk's decoration tiles from the shared tilemap.
            if (_chunkDecoTiles.TryGetValue(idx, out var tiles))
            {
                foreach (var pos in tiles)
                    _decoTilemap.SetTile(pos, null);
                _chunkDecoTiles.Remove(idx);
            }

            Destroy(_activeChunks[idx]);
            _activeChunks.Remove(idx);
        }
    }

    void SpawnChunk(int chunkIndex)
    {
        float worldX = chunkIndex * chunkWidth;

        List<Vector3Int> decoTiles = new();
        GameObject chunkGO = terrainGenerator.GenerateChunk(chunkIndex, worldX, _decoTilemap, decoTiles);

        _activeChunks[chunkIndex] = chunkGO;
        _chunkDecoTiles[chunkIndex] = decoTiles;
    }

    int WorldXToChunkIndex(float worldX)
    {
        return Mathf.FloorToInt(worldX / chunkWidth);
    }

    /// <summary>
    /// Resets all chunk state and regenerates terrain around the given player Transform.
    /// Call after all "Destructible"-tagged objects have been destroyed so the internal
    /// dictionaries reference only defunct objects that Unity has already cleaned up.
    /// </summary>
    public void Restart(Transform newPlayer)
    {
        // Terrain chunks and the deco tilemap were destroyed by tag — clear stale refs.
        _activeChunks.Clear();
        _chunkDecoTiles.Clear();
        _decoTilemap = null;

        // New seed → fresh procedural world.
        terrainGenerator.InitSeed();

        // Recreate the shared decoration tilemap.
        _decoTilemap = terrainGenerator.CreateTreeLayerTilemap();

        player = newPlayer;
        _currentChunkIndex = WorldXToChunkIndex(player.position.x);
        RefreshChunks();
    }
}