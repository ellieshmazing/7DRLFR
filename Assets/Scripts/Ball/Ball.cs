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
///   localScale  = diameter / spriteWorldDiam   (spriteWorldDiam = sprite.rect.width / sprite.pixelsPerUnit)
///   circ.radius = spriteWorldDiam / 2           (local space → diameter/2 world units after scaling)
/// This is PPU-agnostic: the sprite always renders at exactly `diameter` world units.
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

    /// <summary>Current spring simulation velocity in world space.</summary>
    public Vector2 SpringVelocity => springVelocity;

    /// <summary>
    /// The ball's Rigidbody2D. Effects and movement overrides may read or write it
    /// directly (e.g. to switch to Kinematic for a sticky freeze, or to add force).
    /// </summary>
    public Rigidbody2D Rigidbody => rb;

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
    /// <param name="diameter">Desired diameter in world units.</param>
    /// <param name="centipedeMode">True if this ball is a Centipede segment visual.</param>
    /// <param name="node">SkeletonNode this ball springs toward (Centipede Mode only).</param>
    public void Init(BallDefinition definition, float diameter, bool centipedeMode, SkeletonNode node = null)
    {
        EnsureComponents();

        def = definition;

        sr.sprite = definition.sprite;

        // Compute localScale so the sprite displays at exactly `diameter` world units,
        // regardless of the sprite's authored PPU. circ.radius in local space is set to
        // half the sprite's natural world diameter so world-space radius = diameter/2. ✓
        float spriteWorldDiam  = SpriteWorldDiameter(definition.sprite);
        transform.localScale   = Vector3.one * (diameter / spriteWorldDiam);
        circ.radius            = spriteWorldDiam * 0.5f;

        // Mass scales with cross-sectional area for consistent feel across sizes
        rb.mass = definition.baseMass * diameter * diameter;
        rb.gravityScale = 0f;

        SetCentipedeMode(centipedeMode, node);
    }

    /// <summary>
    /// Returns the localScale value that makes <paramref name="sprite"/> render at
    /// exactly <paramref name="diameter"/> world units. Use this when you need to
    /// drive scale externally (e.g. ProjectileScaleGrow) after calling Init.
    /// Safe with a null sprite (falls back to returning diameter unchanged).
    /// </summary>
    public static float LocalScaleForDiameter(Sprite sprite, float diameter)
    {
        return diameter / SpriteWorldDiameter(sprite);
    }

    // Returns the sprite's natural world-space diameter at localScale = 1.
    private static float SpriteWorldDiameter(Sprite sprite)
    {
        if (sprite == null || sprite.pixelsPerUnit <= 0f) return 1f;
        return sprite.rect.width / sprite.pixelsPerUnit;
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

    /// <summary>Adds to the internal spring simulation velocity (test/debug use).</summary>
    public void InjectSpringVelocity(Vector2 addedVelocity)
    {
        springVelocity += addedVelocity;
    }

    /// <summary>
    /// Sets the ball's velocity and fires all OnLaunch callbacks on the definition's
    /// effect and movementOverride. Call this when firing a projectile from scratch.
    /// For centipede ejection use <see cref="Detach"/> instead, which calls this internally.
    /// </summary>
    public void Launch(Vector2 launchVelocity)
    {
        EnsureComponents();
        rb.linearVelocity = launchVelocity;
        def?.movementOverride?.OnLaunch(this, rb, launchVelocity);
        def?.effect?.OnLaunch(this, rb, launchVelocity);
    }

    /// <summary>
    /// Detaches this ball from its centipede segment: switches to free Dynamic physics
    /// and calls <see cref="Launch"/> with <paramref name="launchVelocity"/>.
    /// </summary>
    public void Detach(Vector2 launchVelocity)
    {
        SetCentipedeMode(false);
        Launch(launchVelocity);
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
            // Override may manipulate rb freely: apply forces, set velocity,
            // or switch rb.bodyType to Kinematic for full positional control.
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
