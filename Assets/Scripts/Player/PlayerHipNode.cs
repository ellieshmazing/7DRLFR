using UnityEngine;

/// <summary>
/// Controls the hip node's world position each FixedUpdate:
///
///   X — locked directly below the torso; set by PlayerSkeletonRoot (order -10)
///       after this script runs, so only the Y axis is touched here.
///
///   Y — snaps to the lowest foot visual each frame, identical in spirit to the
///       old per-foot SnapTo() logic but centralised at the hip level.
///       When a foot is on the ground the hip tracks it upward, which in turn
///       raises the torso via PlayerSkeletonRoot's standHeight constraint.
///
/// Runs at -15 so PlayerSkeletonRoot (-10) reads the freshly-updated Y.
/// </summary>
[DefaultExecutionOrder(-15)]
public class PlayerHipNode : MonoBehaviour
{
    [Tooltip("Left foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;

    [Tooltip("Right foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D rightFootRB;

    void FixedUpdate()
    {
        if (leftFootRB == null || rightFootRB == null) return;

        float lowestY = Mathf.Min(leftFootRB.position.y, rightFootRB.position.y);

        // Only update Y — X is set by PlayerSkeletonRoot after this runs
        transform.position = new Vector3(transform.position.x, lowestY, 0f);
    }
}
