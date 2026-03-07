using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to a persistent GameObject (e.g. "GameManager").
/// Tracks the player's current chunk and spawns/destroys chunks accordingly.
///
/// Chunk layout relative to player chunk (P):
///   [P-3][P-2][P-1][P][P+1][P+2]
///    ^^^--- destroyed beyond this     ^^^--- pre-generated ahead
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player Transform - used to determine current chunk.")]
    public Transform player;

    // Fetched automatically from this same GameObject — no need to assign in Inspector.
    private TerrainGenerator terrainGenerator;

    [Header("Chunk Settings")]
    [Tooltip("Must match TerrainGenerator.chunkWidth (32).")]
    public int chunkWidth = 32;

    [Tooltip("How many chunks to keep BEHIND the player before destroying them.")]
    public int chunksToKeepBehind = 3;

    [Tooltip("How many chunks to pre-generate AHEAD of the player.")]
    public int chunksToKeepAhead = 2;

    // --- Internal State ---
    private Dictionary<int, GameObject> _activeChunks = new();
    private int _currentChunkIndex = 0;

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();

        // Debug fallback: if no player is assigned, track the main camera instead.
        if (player == null)
        {
            player = Camera.main.transform;
            Debug.LogWarning("[ChunkManager] No player assigned — falling back to Camera.main for chunk tracking.");
        }

        // Seed the terrain generator once.
        terrainGenerator.InitSeed();

        // Generate the starting chunks around the player's spawn.
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

    /// <summary>
    /// Spawns any missing chunks in the keep window, destroys any outside it.
    /// </summary>
    void RefreshChunks()
    {
        int minChunk = _currentChunkIndex - chunksToKeepBehind;
        int maxChunk = _currentChunkIndex + chunksToKeepAhead;

        // Spawn missing chunks in the window.
        for (int i = minChunk; i <= maxChunk; i++)
        {
            if (!_activeChunks.ContainsKey(i))
                SpawnChunk(i);
        }

        // Destroy chunks that have fallen outside the window.
        List<int> toRemove = new();
        foreach (var kvp in _activeChunks)
        {
            if (kvp.Key < minChunk || kvp.Key > maxChunk)
                toRemove.Add(kvp.Key);
        }

        foreach (int idx in toRemove)
        {
            Destroy(_activeChunks[idx]);
            _activeChunks.Remove(idx);
        }
    }

    void SpawnChunk(int chunkIndex)
    {
        // World-space X origin of this chunk.
        float worldX = chunkIndex * chunkWidth;

        GameObject chunkGO = terrainGenerator.GenerateChunk(chunkIndex, worldX);
        _activeChunks[chunkIndex] = chunkGO;
    }

    /// <summary>
    /// Converts a world-space X position to a chunk index.
    /// Handles negative indices correctly for any future left-side expansion.
    /// </summary>
    int WorldXToChunkIndex(float worldX)
    {
        return Mathf.FloorToInt(worldX / chunkWidth);
    }
}