// TEMPORARY DEBUG FEATURE — delete this file and its GameObject when no longer needed.
// To remove: delete CentipedeDestruction.cs, CentipedeDestruction.cs.meta, and the GameObject in the scene.

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CentipedeDestruction : MonoBehaviour
{
    [SerializeField] private CentipedeAssembler centipedeAssembler;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.tKey.wasPressedThisFrame) return;
        centipedeAssembler?.ExecuteDetachmentTest();
    }
}
