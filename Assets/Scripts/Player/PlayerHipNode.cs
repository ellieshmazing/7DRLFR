using UnityEngine;

/// <summary>
/// Locks the hip node's Y position each FixedUpdate directly to the feet's
/// ground reference, giving the feet full authority over vertical position.
///
/// The spring-based inertia that gives the body its feel — stretching during
/// falls, compressing on landing — lives in the torso-offset spring inside
/// PlayerSkeletonRoot, not here. The hip is a pure positional anchor.
///
///   Y — hard-locked to footMovement.GetGroundReferenceY() each frame.
///   X — locked directly below the torso by PlayerSkeletonRoot (-10),
///       which runs after this script.  Only Y is touched here.
/// </summary>
[DefaultExecutionOrder(-15)]
public class PlayerHipNode : MonoBehaviour
{
    [Tooltip("Live config SO — read per-frame")]
    public PlayerConfig config;

    [Tooltip("FootMovement — provides GetGroundReferenceY() (wired by PlayerAssembler)")]
    public FootMovement footMovement;

    [Tooltip("Left foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;

    [Tooltip("Right foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D rightFootRB;

    // Kept public so external systems can read the virtual spring mass.
    public float mass => config != null ? config.hipMass : 1f;

    void FixedUpdate()
    {
        if (footMovement == null) return;

        float groundY = footMovement.GetGroundReferenceY();

        // Only update Y — X is set by PlayerSkeletonRoot after this runs.
        transform.position = new Vector3(transform.position.x, groundY, 0f);
    }
}
