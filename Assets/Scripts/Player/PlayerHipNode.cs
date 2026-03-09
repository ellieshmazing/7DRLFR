using UnityEngine;

/// <summary>
/// Controls the hip node's Y position each FixedUpdate using a spring-damper
/// (NodeWiggle-style) that chases the highest locked foot Y when both are grounded,
/// or the single grounded foot's Y otherwise.
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
    [Tooltip("Live config SO — HipStiffness, HipDamping, hipMass read per-frame")]
    public PlayerConfig config;

    [Tooltip("FootMovement — provides GetGroundReferenceY() for hip spring target (wired by PlayerAssembler)")]
    public FootMovement footMovement;

    [Tooltip("Left foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;

    [Tooltip("Right foot visual Rigidbody2D (wired by PlayerAssembler)")]
    public Rigidbody2D rightFootRB;

    // Kept public so PlayerSkeletonRoot can read it for jump impulse division
    public float mass => config != null ? config.hipMass : 1f;

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
        if (footMovement == null || config == null) return;

        float stiffness = config.HipStiffness;
        float damping   = config.HipDamping;
        float m         = config.hipMass;

        float targetY = footMovement != null
            ? footMovement.GetGroundReferenceY()
            : Mathf.Min(leftFootRB.position.y, rightFootRB.position.y);

        // Spring-damper toward targetY — mirrors NodeWiggle but Y-axis only,
        // and uses fixedDeltaTime because this runs in FixedUpdate.
        float displacement  = hipY - targetY;
        float springForce   = -stiffness * displacement;
        float dampingForce  = -damping * hipVelocityY;
        float acceleration  = (springForce + dampingForce) / m;

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

    /// <summary>
    /// Cuts the hip's upward velocity by the given multiplier for variable
    /// jump height. Called by PlayerSkeletonRoot when the player releases
    /// jump early while still ascending.
    /// </summary>
    public void ApplyJumpCut(float multiplier)
    {
        if (hipVelocityY > 0f)
            hipVelocityY *= multiplier;
    }
}
