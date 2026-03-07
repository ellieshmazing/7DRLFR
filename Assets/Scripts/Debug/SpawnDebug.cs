// TEMPORARY DEBUG FEATURE — delete this file and its GameObject when no longer needed.
// To remove: delete SpawnDebug.cs, SpawnDebug.cs.meta, and the GameObject in the scene.

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SpawnDebug : MonoBehaviour
{
    [SerializeField] private CentipedeAssembler centipedeAssembler;
    [SerializeField] private PlayerAssembler playerAssembler;

    private void Update()
    {
        if (!Keyboard.current.yKey.wasPressedThisFrame) return;
        centipedeAssembler?.TestSpawn();
        playerAssembler?.TestSpawn();
    }
}
