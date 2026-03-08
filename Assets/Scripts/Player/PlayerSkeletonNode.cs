using UnityEngine;

/// <summary>
/// Skeleton node for the player character.
///
/// Player nodes maintain a fixed local offset from their parent and travel
/// with it each frame.  The torso node is the root and is positioned
/// externally by PlayerSkeletonRoot; the hip node carries the foot-movement
/// system and is tracked by PlayerHipNode.
/// </summary>
public class PlayerSkeletonNode : MonoBehaviour
{
    [Tooltip("Fixed world-space offset from the parent node. Set by PlayerAssembler.")]
    public Vector2 localOffset;

    public Color gizmoColor = Color.cyan;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }
}
