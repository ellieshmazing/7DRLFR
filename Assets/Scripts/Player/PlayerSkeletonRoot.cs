using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Root controller for the player skeleton.
///
/// FixedUpdate responsibilities (runs at -10, after PlayerHipNode at -15):
///   1. Apply WASD horizontal forces to the torso Rigidbody2D.
///   2. Read hipNode.Y (already set by PlayerHipNode this frame) and override
///      the torso's Y velocity so it sits at hipNode.Y + standHeight.
///   3. Lock hipNode.X directly below the torso.
///   4. On Space press: apply a jump impulse to the feet and hip node.
///
/// The torso has no gravity — its Y is driven entirely by the hip constraint.
/// Foot visuals have Dynamic RBs with gravity; PlayerHipNode tracks the lowest
/// foot Y so the whole body rises when feet contact a surface.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(PlayerSkeletonNode))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSkeletonRoot : MonoBehaviour
{
    [Header("Config (set by PlayerAssembler)")]
    [Tooltip("Live config SO — moveForce, maxSpeed, standHeight, jumpSpeed, jumpOffsetFactor read per-frame")]
    public PlayerConfig config;

    [Tooltip("Pixel-to-world conversion factor, cached at spawn")]
    public float pixelToWorld;

    [Header("Wiring (set by PlayerAssembler)")]
    public Transform hipNode;
    public PlayerHipNode hipNodeScript;
    public FootMovement footMovement;
    public Rigidbody2D leftFootRB;
    public Rigidbody2D rightFootRB;

    private Rigidbody2D rb;
    private bool _jumpRequested;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        // Buffer the jump request so it isn't lost between Update and FixedUpdate.
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
            _jumpRequested = true;
    }

    void FixedUpdate()
    {
        if (config == null) return;

        // Read tunable values from config SO each frame
        float moveForce  = config.moveForce;
        float maxSpeed   = config.maxSpeed;
        float standHeight = config.standHeight * pixelToWorld;
        float jumpSpeed   = config.jumpSpeed;
        float jumpOffsetFactor = config.jumpOffsetFactor;

        // 1. Horizontal WASD movement
        var kb = Keyboard.current;
        float h = kb != null
            ? (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
            - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f)
            : 0f;

        if (rb.linearVelocity.magnitude < maxSpeed)
            rb.AddForce(new Vector2(h, 0f) * moveForce);

        if (hipNode == null) return;

        // 2. Torso Y: converge to hipNode.Y + standHeight each physics step
        //    hipNode.Y was set by PlayerHipNode (order -15) to the lowest foot Y
        float desiredY    = hipNode.position.y + standHeight;
        float yCorrection = (desiredY - rb.position.y) / Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, yCorrection);

        // 3. Lock hipNode X directly below the torso
        //    (Y is owned by PlayerHipNode; only X is set here)
        hipNode.position = new Vector3(rb.position.x, hipNode.position.y, 0f);

        // 4. Jump
        TryJump(jumpSpeed, jumpOffsetFactor);
    }

    void TryJump(float jumpSpeed, float jumpOffsetFactor)
    {
        if (!_jumpRequested) return;
        _jumpRequested = false;

        if (leftFootRB == null || rightFootRB == null) return;
        if (footMovement == null || !footMovement.CanJump()) return;

        // Hip offset: positive when the hip node has been pulled below the lowest locked foot
        // (spring compression = stored potential energy).
        float lowestFootY = footMovement.GetLockedFootY();
        float hipOffset   = Mathf.Max(0f, lowestFootY - hipNode.position.y);

        // Total impulse (kg·m/s) — linear in hip offset, physically grounded in
        // spring PE→KE: v_extra = offset * sqrt(k/m), so jumpOffsetFactor ≈ sqrt(k/m).
        float jumpImpulse = jumpSpeed + hipOffset * jumpOffsetFactor;

        // Convert impulse to velocity for each body: v = J / m.
        // Heavier feet produce less velocity from the same impulse, naturally
        // reducing jump height without touching the jumpSpeed parameter.
        float footJumpVel = jumpImpulse / leftFootRB.mass;

        leftFootRB.linearVelocity  = new Vector2(leftFootRB.linearVelocity.x, footJumpVel);
        rightFootRB.linearVelocity = new Vector2(rightFootRB.linearVelocity.x, footJumpVel);

        // Seed the hip spring's internal velocity using the same impulse ÷ hip mass,
        // so it rises with the feet at a consistent rate regardless of spring tuning.
        if (hipNodeScript != null)
            hipNodeScript.ApplyJumpImpulse(jumpImpulse / hipNodeScript.mass);

        // Release foot locks so they fly freely.
        footMovement.OnJump();
    }
}
