using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Skeleton node for the player character.
///
/// Unlike the centipede's SkeletonNode (which trail-follows a parent),
/// player nodes maintain a fixed local offset from their parent and travel
/// with it each frame.  The torso node is the root and is positioned
/// externally by PlayerSkeletonRoot; foot nodes always sit at
/// (torsoWorldPos + localOffset).
///
/// When a foot's visual is blocked by the ground, PlayerFootWiggle calls
/// SnapTo() to pull this node back up to the visual rather than letting it
/// drift underground.
/// </summary>
public class PlayerSkeletonNode : MonoBehaviour
{
    [Header("Tree Structure")]
    public PlayerSkeletonNode parent;
    public List<PlayerSkeletonNode> children = new List<PlayerSkeletonNode>();

    [Header("Offset from Parent")]
    [Tooltip("Fixed world-space offset from the parent node. Set by PlayerAssembler.")]
    public Vector2 localOffset;

    public Color gizmoColor = Color.cyan;

    /// <summary>Current world position of this node.</summary>
    public Vector2 WorldPosition => (Vector2)transform.position;

    /// <summary>Directly set this node's world position (used by root).</summary>
    public void SetWorldPosition(Vector2 pos) => transform.position = (Vector3)pos;

    /// <summary>
    /// Overrides this node's position — used by PlayerFootWiggle when the
    /// visual foot is stopped by a physical surface and the node needs to
    /// follow the visual upward instead of staying underground.
    /// </summary>
    public void SnapTo(Vector2 worldPos) => transform.position = (Vector3)worldPos;

    /// <summary>
    /// Moves all children to (myWorldPos + child.localOffset), then recurses.
    /// Call each frame from PlayerSkeletonRoot after the torso has moved.
    /// </summary>
    public void PropagateToChildren(Vector2 myWorldPos)
    {
        foreach (var child in children)
        {
            child.transform.position = (Vector3)(myWorldPos + child.localOffset);
            child.PropagateToChildren(child.WorldPosition);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        if (parent != null)
            Gizmos.DrawLine(transform.position, parent.transform.position);
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }
}
