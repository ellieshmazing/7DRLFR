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
}
