using UnityEngine;

/// <summary>
/// Spring-damper wiggle for player feet, applied via Rigidbody2D forces.
///
/// Two modes, selected each FixedUpdate based on the foot node's position
/// relative to this visual:
///
///   NORMAL (node at or above visual):
///     Apply a spring force that pulls the visual foot toward the skeleton node,
///     identical in spirit to NodeWiggle but physics-based (AddForce) so the
///     foot's Dynamic RB still responds to colliders.
///
///   GROUND CONTACT (node is below visual by more than groundSnapThreshold):
///     The node has drifted underground while the visual was stopped by a
///     physical surface.  Snap the node up to the visual's Y position instead
///     of pushing the visual downward into the ground.
///
/// Runs after PlayerSkeletonRoot (execution order 0 > -10) so it always
/// reads freshly-propagated node positions.
/// </summary>
[DefaultExecutionOrder(0)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFootWiggle : MonoBehaviour
{
    [Tooltip("Spring pull strength — higher = tighter snap-back")]
    public float stiffness = 60f;

    [Tooltip("Oscillation decay — higher = settles faster")]
    public float damping = 8f;

    [Tooltip("Visual weight — higher = more sluggish, heavier feel")]
    public float mass = 0.5f;

    [Tooltip("If the node is this far below the visual, treat it as ground contact " +
             "and snap the node up rather than pushing the visual down")]
    public float groundSnapThreshold = 0.01f;

    private Rigidbody2D rb;
    private PlayerSkeletonNode parentNode;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // The foot visual is a child of the foot SkeletonNode GO
        parentNode = transform.parent.GetComponent<PlayerSkeletonNode>();
    }

    void FixedUpdate()
    {
        if (parentNode == null) return;

        Vector2 nodePos   = parentNode.WorldPosition;
        Vector2 visualPos = rb.position;

        if (nodePos.y < visualPos.y - groundSnapThreshold)
        {
            // Ground contact: the node went underground but the visual is resting
            // on a surface.  Pull the node back up to match the visual's Y.
            parentNode.SnapTo(new Vector2(nodePos.x, visualPos.y));
        }
        else
        {
            // Normal: spring-pull the visual toward the node.
            // Mirrors NodeWiggle's integration directly on the RB velocity so the
            // script 'mass' field is the sole inertia parameter (ForceMode2D has no
            // Acceleration mode in 2D, so we integrate velocity manually).
            Vector2 displacement    = visualPos - nodePos;
            Vector2 springForce     = -stiffness * displacement;
            Vector2 dampingForce    = -damping   * rb.linearVelocity;
            Vector2 acceleration    = (springForce + dampingForce) / mass;

            rb.linearVelocity += acceleration * Time.fixedDeltaTime;
        }
    }
}
