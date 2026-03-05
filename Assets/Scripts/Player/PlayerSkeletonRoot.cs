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
    [Header("Movement")]
    [Tooltip("Horizontal force applied per frame while input is held")]
    public float moveForce = 15f;

    [Tooltip("Speed cap — AddForce stops when this magnitude is exceeded")]
    public float maxSpeed = 5f;

    [Header("Stance")]
    [Tooltip("Vertical distance the torso sits above the hip node")]
    public float standHeight = 0.6f;

    [Header("Hip Node (wired by PlayerAssembler)")]
    public Transform hipNode;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
    }

    void FixedUpdate()
    {
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
    }
}
