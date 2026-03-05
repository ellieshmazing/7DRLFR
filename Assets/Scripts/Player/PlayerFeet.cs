using UnityEngine;

/// <summary>
/// Drives both foot visual Rigidbody2Ds relative to the hip node each FixedUpdate.
///
///   X — spring-damper toward (hip.X ± footSpreadX).  Uses the same stiffness /
///       damping / mass as the Y spring so both axes feel consistent.
///       Replacing the old rigid velocity-correction with a spring allows the
///       feet to swing naturally rather than snapping frame-to-frame.
///
///   Y — spring-damper toward hip.Y, additive to the foot's current Y velocity so
///       gravity and ground-collision responses from the physics engine are preserved.
///       When a foot is resting on the ground the hip node has already converged to
///       that foot's Y (via PlayerHipNode), so displacement ≈ 0 and the spring
///       contributes negligibly.  In the air the spring pulls both feet toward the
///       shared hip Y, giving a natural dangling feel.
///
/// Attach to the HipNode GameObject alongside PlayerHipNode.
/// Runs at order 0, after PlayerSkeletonRoot (-10) has set the hip's final X.
/// </summary>
[DefaultExecutionOrder(0)]
public class PlayerFeet : MonoBehaviour
{
    [Header("Formation")]
    [Tooltip("Horizontal distance from hip node centre to each foot")]
    [Min(0f)] public float footSpreadX = 0.2f;

    [Header("Spring — both axes; X pulls to spread, Y pulls to hip Y")]
    [Min(0f)] public float stiffness = 60f;
    [Min(0f)] public float damping   = 8f;
    [Min(0.01f)] public float mass   = 0.5f;

    [Header("References (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;
    public Rigidbody2D rightFootRB;

    void FixedUpdate()
    {
        if (leftFootRB == null || rightFootRB == null) return;

        float hipX = transform.position.x;
        float hipY = transform.position.y;

        UpdateFoot(leftFootRB,  hipX - footSpreadX, hipY);
        UpdateFoot(rightFootRB, hipX + footSpreadX, hipY);
    }

    void UpdateFoot(Rigidbody2D foot, float targetX, float hipY)
    {
        // X: spring-damper toward target horizontal position.
        //    Using the same spring model as Y keeps the feel consistent and
        //    replaces the old (targetX - pos) / dt rigid snap.
        float xDisplacement = foot.position.x - targetX;
        float xAcceleration = (-stiffness * xDisplacement - damping * foot.linearVelocity.x) / mass;
        float xVelocity     = foot.linearVelocity.x + xAcceleration * Time.fixedDeltaTime;

        // Y: spring-damper toward hip Y, additive so gravity / collision impulses survive.
        float yDisplacement = foot.position.y - hipY;
        float yAcceleration = (-stiffness * yDisplacement - damping * foot.linearVelocity.y) / mass;
        float yVelocity     = foot.linearVelocity.y + yAcceleration * Time.fixedDeltaTime;

        foot.linearVelocity = new Vector2(xVelocity, yVelocity);
    }
}
