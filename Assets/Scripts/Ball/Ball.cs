using UnityEngine;

/// <summary>
/// A single-GameObject physics sphere that serves as both a Centipede visual segment
/// and a free-flying projectile/object.
///
/// Centipede Mode (centipedeMode = true):
///   — Rigidbody is Kinematic; ball spring-chases its linked SkeletonNode each FixedUpdate.
///   — Placed on the "Centipede" physics layer; Centipede×Centipede collision is disabled
///     in the Physics 2D Matrix so segments don't push each other.
///   — Non-centipede balls (Default layer) still collide against Centipede-layer balls.
///
/// Free Mode (centipedeMode = false):
///   — Rigidbody is Dynamic; standard Unity physics applies.
///   — Placed on the "Default" layer; collides with everything.
///   — If the BallDefinition has a movementOverride, it runs in FixedUpdate instead of
///     (or in addition to) default physics.
///
/// Scale convention (project-wide):
///   transform.localScale = Vector3.one * diameter
///   CircleCollider2D.radius = 0.5f  (local space → diameter world units after scaling)
///   Sprite is authored at 1 world-unit diameter at scale 1.
/// </summary>
[DefaultExecutionOrder(0)]
public class Ball : MonoBehaviour
{
    // ── Centipede spring parameters ───────────────────────────────────────────
    [Header("Centipede Spring")]
    [Tooltip("Spring pull strength toward the linked node — higher = tighter snap-back")]
    public float springStiffness = 80f;

    [Tooltip("Oscillation decay — higher = settles faster")]
    public float springDamping = 5f;

    [Tooltip("Spring simulation mass — higher = more sluggish, heavier feel")]
    [Min(0.01f)]
    public float springMass = 1f;

    // ── Internal components ───────────────────────────────────────────────────
    private SpriteRenderer sr;
    private CircleCollider2D circ;
    private Rigidbody2D rb;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private BallDefinition def;
    private bool inCentipedeMode;
    private SkeletonNode linkedNode;
    private Vector2 springVelocity;

    // ── Cached layer IDs (shared across all Ball instances) ───────────────────
    private static int layerDefault   = -1;
    private static int layerCentipede = -1;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        EnsureComponents();
    }

    /// <summary>
    /// Configures this ball. Safe to call before Awake fires (e.g. on inactive hierarchy).
    /// </summary>
    /// <param name="definition">Type data — sprite, base mass, optional overrides.</param>
    /// <param name="diameter">
    /// Diameter in world units. Sets transform.localScale = Vector3.one * diameter.
    /// The sprite is authored at 1 world-unit diameter at scale 1.
    /// </param>
    /// <param name="centipedeMode">True if this ball is a Centipede segment visual.</param>
    /// <param name="node">SkeletonNode this ball springs toward (Centipede Mode only).</param>
    public void Init(BallDefinition definition, float diameter, bool centipedeMode, SkeletonNode node = null)
    {
        EnsureComponents();

        def = definition;

        sr.sprite = definition.sprite;

        // Scale: sprite is authored at 1 world-unit diameter at scale 1
        transform.localScale = Vector3.one * diameter;

        // Local radius 0.5 → diameter/2 world units after scaling
        circ.radius = 0.5f;

        // Mass scales with cross-sectional area for consistent feel across sizes
        rb.mass = definition.baseMass * diameter * diameter;
        rb.gravityScale = 0f;

        SetCentipedeMode(centipedeMode, node);
    }

    /// <summary>
    /// Switches between Centipede Mode (kinematic spring-chase) and free physics (dynamic).
    /// Can be called at runtime to eject a ball from a Centipede.
    /// </summary>
    public void SetCentipedeMode(bool enabled, SkeletonNode node = null)
    {
        EnsureComponents();

        inCentipedeMode = enabled;
        linkedNode      = node;
        springVelocity  = Vector2.zero;

        if (enabled)
        {
            rb.bodyType    = RigidbodyType2D.Kinematic;
            gameObject.layer = layerCentipede >= 0 ? layerCentipede : 0;
        }
        else
        {
            rb.bodyType    = RigidbodyType2D.Dynamic;
            gameObject.layer = layerDefault >= 0 ? layerDefault : 0;
        }
    }

    /// <summary>
    /// Overrides the sprite tint (e.g. for per-centipede color variation).
    /// </summary>
    public void SetTint(Color color)
    {
        EnsureComponents();
        sr.color = color;
    }

    // ─────────────────────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (inCentipedeMode)
        {
            UpdateCentipedeSpring();
        }
        else if (def?.movementOverride != null)
        {
            def.movementOverride.OnFixedUpdate(this, rb, Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Spring-damper that pulls this ball toward the linked SkeletonNode each FixedUpdate.
    /// Uses rb.MovePosition so the kinematic body's physics position updates correctly
    /// for collision detection.
    /// </summary>
    private void UpdateCentipedeSpring()
    {
        if (linkedNode == null) return;

        Vector2 anchor       = (Vector2)linkedNode.transform.position;
        Vector2 currentPos   = rb.position;
        Vector2 displacement = currentPos - anchor;

        Vector2 springForce  = -springStiffness * displacement;
        Vector2 dampingForce = -springDamping * springVelocity;
        Vector2 acceleration = (springForce + dampingForce) / springMass;

        springVelocity += acceleration * Time.fixedDeltaTime;
        rb.MovePosition(currentPos + springVelocity * Time.fixedDeltaTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        def?.effect?.OnCollision(this, collision);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureComponents()
    {
        if (sr != null) return;

        if (!TryGetComponent(out sr))   sr   = gameObject.AddComponent<SpriteRenderer>();
        if (!TryGetComponent(out circ)) circ = gameObject.AddComponent<CircleCollider2D>();
        if (!TryGetComponent(out rb))   rb   = gameObject.AddComponent<Rigidbody2D>();

        if (layerDefault   < 0) layerDefault   = LayerMask.NameToLayer("Default");
        if (layerCentipede < 0) layerCentipede = LayerMask.NameToLayer("Centipede");
    }
}
