using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Static coroutine helpers that destroy and re-spawn player or centipede entities.
/// Used by TuningManager when a tuning variable flagged requiresRespawn changes value.
///
/// Each method captures positions before destruction, waits one frame for
/// Object.Destroy to finalize, then spawns fresh instances via the assembler.
/// </summary>
public static class AutoRespawner
{
    /// <summary>
    /// Destroys the current player and spawns a fresh one at the same position.
    /// </summary>
    public static IEnumerator RespawnPlayer(PlayerConfig config, PlayerAssembler assembler)
    {
        var player = Object.FindAnyObjectByType<PlayerSkeletonRoot>();
        if (player == null) yield break;

        Vector2 pos = player.transform.position;
        Object.Destroy(player.gameObject);
        yield return null; // wait one frame for Destroy to finalize

        assembler.Spawn(config, pos);
    }

    /// <summary>
    /// Destroys all centipedes using the specified config and spawns replacements
    /// at their original positions.
    /// </summary>
    public static IEnumerator RespawnCentipedes(CentipedeConfig config, CentipedeAssembler assembler)
    {
        var positions = new List<Vector2>();
        foreach (var controller in Object.FindObjectsByType<CentipedeController>(FindObjectsSortMode.None))
        {
            if (controller.Config != config) continue;
            positions.Add((Vector2)controller.transform.position);
            Object.Destroy(controller.gameObject);
        }

        if (positions.Count == 0) yield break;
        yield return null; // wait one frame for Destroy to finalize

        foreach (var pos in positions)
            assembler.Spawn(config, pos);
    }

    /// <summary>
    /// Destroys any existing player and spawns a fresh one at the given position.
    /// Works even when no player currently exists.
    /// </summary>
    public static IEnumerator SpawnFreshPlayer(PlayerConfig config, PlayerAssembler assembler, Vector2 position)
    {
        var existing = Object.FindAnyObjectByType<PlayerSkeletonRoot>();
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
            yield return null;
        }
        assembler.Spawn(config, position);
    }

    /// <summary>
    /// Destroys all existing centipedes and spawns fresh ones at the given positions.
    /// Works even when no centipedes currently exist.
    /// </summary>
    public static IEnumerator SpawnFreshCentipedes(CentipedeConfig config, CentipedeAssembler assembler, Vector2[] positions)
    {
        bool anyDestroyed = false;
        foreach (var controller in Object.FindObjectsByType<CentipedeController>(FindObjectsSortMode.None))
        {
            Object.Destroy(controller.gameObject);
            anyDestroyed = true;
        }

        if (anyDestroyed)
            yield return null;

        foreach (var pos in positions)
            assembler.Spawn(config, pos);
    }
}
