using UnityEngine;

/// <summary>
/// Controls the hip node's Y position each FixedUpdate using a spring-damper
/// (NodeWiggle-style) that chases the lowest foot visual Y.
///
///   Y — spring toward the lowest foot Y each frame, so the hip settles
///       naturally rather than snapping rigidly.  Runs at -15 so
///       PlayerSkeletonRoot (-10) reads the freshly-updated Y.
///
///   X — locked directly below the torso by PlayerSkeletonRoot (-10),
///       which runs after this script.  Only Y is touched here.
/// </summary>
[DefaultExecutionOrder(-15)]
public class PlayerHipNode : MonoBehaviour
{
    [Tooltip("Left foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;

    [Tooltip("Right foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D rightFootRB;

    [Header("Y Spring — pulls hip toward lowest foot Y")]
    [Min(0f)] public float stiffness = 120f;
    [Min(0f)] public float damping   = 10f;
    [Min(0.01f)] public float mass   = 1f;

    // Tracked separately from the transform so the spring has genuine
    // inertia — identical bookkeeping to NodeWiggle.currentPos / velocity.
    private float hipY;
    private float hipVelocityY;

    void Start()
    {
        hipY = transform.position.y;
    }

    void FixedUpdate()
    {
        if (leftFootRB == null || rightFootRB == null) return;

        float targetY = Mathf.Min(leftFootRB.position.y, rightFootRB.position.y);

        // Spring-damper toward targetY — mirrors NodeWiggle but Y-axis only,
        // and uses fixedDeltaTime because this runs in FixedUpdate.
        float displacement  = hipY - targetY;
        float springForce   = -stiffness * displacement;
        float dampingForce  = -damping * hipVelocityY;
        float acceleration  = (springForce + dampingForce) / mass;

        hipVelocityY += acceleration * Time.fixedDeltaTime;
        hipY         += hipVelocityY * Time.fixedDeltaTime;

        // Only update Y — X is set by PlayerSkeletonRoot after this runs.
        transform.position = new Vector3(transform.position.x, hipY, 0f);
    }

    /// <summary>
    /// Seeds the hip's internal spring velocity for a jump impulse.
    /// Call from PlayerSkeletonRoot at the moment of jump so the hip rises
    /// in sync with the feet rather than lagging behind on the first frame.
    /// </summary>
    public void ApplyJumpImpulse(float upwardVelocity)
    {
        hipVelocityY = upwardVelocity;
    }
}
