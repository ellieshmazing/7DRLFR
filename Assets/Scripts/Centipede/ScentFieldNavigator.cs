using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scent-gradient pathfinder for the centipede head. Competing design to CentipedePathfinder.
///
/// Navigates by ascending a shared scent field (ScentField) emitted by the player's recent
/// positions. No path is planned — the route emerges from local gradient-following.
///
/// Three layered mechanisms produce complex behavior:
///   1. Gradient ascent    — head steers toward regions of stronger scent
///   2. Momentum hysteresis — heading blends gradually (inertia prevents jitter)
///   3. Sweep-and-lock oscillator — sensitivity pulses so the centipede sweeps ballistically
///      then snaps to fresh scent, creating a predator hunting rhythm
///
/// Consumed-zone suppression (in ScentField.Consume) erases the field behind the centipede,
/// forcing an inward spiral on stationary targets and natural territory division between
/// multiple centipedes.
///
/// Attach to the same GameObject as SkeletonRoot. Call Initialize() after AddComponent.
/// Collision response: NotifyCollision() (same interface as CentipedePathfinder).
/// </summary>
[DefaultExecutionOrder(-5)]
[RequireComponent(typeof(Rigidbody2D))]
public class ScentFieldNavigator : MonoBehaviour
{
    // 8 equally-spaced unit vectors for gradient sampling (computed once at class load)
    private static readonly Vector2[] k_GradientDirs;

    static ScentFieldNavigator()
    {
        k_GradientDirs = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            k_GradientDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }

    private CentipedeConfig config;
    private Rigidbody2D     rb;
    private ScentField      field;

    private Vector2 momentum;           // normalized current heading
    private float   sensitivityPhase;   // sweep-and-lock oscillator phase
    private float   collisionCooldown;

    public Transform       Target           { get; private set; }

    // ── Debug read API (consumed by ScentFieldDebugVisualizer) ────────────────
    public Vector2         Momentum         { get; private set; }
    public Vector2         GradientDirection { get; private set; }
    public float           Sensitivity      { get; private set; }
    public bool            IsInFallback     { get; private set; }
    public CentipedeConfig Config           => config;

    // ── Initialization ────────────────────────────────────────────────────────

    private void OnEnable()
    {
        PlayerRegistry.OnPlayerChanged += OnPlayerChanged;
        // Sync with any player that already exists
        if (PlayerRegistry.PlayerTransform != null)
            Target = PlayerRegistry.PlayerTransform;
    }

    private void OnDisable()
    {
        PlayerRegistry.OnPlayerChanged -= OnPlayerChanged;
    }

    private void OnPlayerChanged(Transform playerTransform)
    {
        Target = playerTransform; // null when player is destroyed → mouse fallback in GetTargetPosition
    }

    public void Initialize(CentipedeConfig cfg, Transform playerTarget, ScentField scentField)
    {
        config = cfg;
        // Prefer the live registry; fall back to the explicitly passed transform
        Target = PlayerRegistry.PlayerTransform != null ? PlayerRegistry.PlayerTransform : playerTarget;
        field  = scentField;
        rb     = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        // Random initial heading so multiple centipedes don't all start parallel
        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        momentum = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));

        // Desynchronize the sweep-and-lock oscillator across centipedes
        sensitivityPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (config == null || field == null) return;

        float dt = Time.fixedDeltaTime;

        if (collisionCooldown > 0f)
            collisionCooldown -= dt;

        // ── 1. Advance sweep-and-lock oscillator ─────────────────────────────
        sensitivityPhase += config.scentOscillationFrequency * Mathf.PI * 2f * dt;
        float sensitivity = 0.5f + 0.5f * Mathf.Sin(sensitivityPhase); // [0, 1]
        Sensitivity = sensitivity;

        // ── 2. Compute scent gradient ─────────────────────────────────────────
        Vector2 gradient = ComputeGradientDirection();
        GradientDirection = gradient;

        // ── 3. Blend gradient into momentum (inertia-weighted) ────────────────
        if (gradient.sqrMagnitude > 0.001f)
        {
            float blendRate = config.scentSteeringBlend * sensitivity;
            momentum = Vector2.Lerp(momentum, gradient, blendRate * dt);
        }

        // ── 4. Fallback: field is empty or too weak → nudge toward player ─────
        float fieldAtHead = field.Evaluate(rb.position);
        IsInFallback = fieldAtHead < config.scentFallbackThreshold;
        if (IsInFallback)
        {
            Vector2 toPlayer = GetTargetPosition() - rb.position;
            if (toPlayer.sqrMagnitude > 0.001f)
                momentum = Vector2.Lerp(momentum, toPlayer.normalized, config.scentFallbackBlend * dt);
        }

        if (momentum.sqrMagnitude > 0.001f)
            momentum = momentum.normalized;
        Momentum = momentum;

        // ── 5. Speed boost proportional to forward gradient strength ──────────
        float forwardStrength = field.Evaluate(rb.position + momentum * config.scentGradientSampleRadius);
        float trailHeat       = Mathf.Clamp01(forwardStrength / config.scentGradientMaxStrength);
        float speed           = config.speed + config.scentSpeedBoost * trailHeat;

        // ── 6. Consume scent at head position ─────────────────────────────────
        field.Consume(rb.position, config.scentConsumeRate, config.scentConsumeRadius, dt);

        // ── 7. Apply velocity ─────────────────────────────────────────────────
        rb.linearVelocity = momentum * speed;
    }

    // ── Gradient computation ──────────────────────────────────────────────────

    /// <summary>
    /// Samples the scent field at 8 radial points around the head and returns
    /// the direction of steepest ascent (angular weighted sum — rotation invariant).
    /// Returns Vector2.zero if the field is flat at this location.
    /// </summary>
    private Vector2 ComputeGradientDirection()
    {
        float   r        = config.scentGradientSampleRadius;
        Vector2 pos      = rb.position;
        Vector2 gradient = Vector2.zero;

        for (int i = 0; i < k_GradientDirs.Length; i++)
        {
            float val = field.Evaluate(pos + k_GradientDirs[i] * r);
            gradient += k_GradientDirs[i] * val;
        }

        return gradient.sqrMagnitude > 0.001f ? gradient.normalized : Vector2.zero;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2 GetTargetPosition()
    {
        if (Target != null)
            return Target.position;

        // Debug fallback: follow mouse
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        return Camera.main.ScreenToWorldPoint(mouseScreen);
    }

    // ── Collision response ────────────────────────────────────────────────────

    /// <summary>
    /// Inverts the current heading on collision.
    /// Same interface as CentipedePathfinder.NotifyCollision() —
    /// call from the head Ball's collision callback or any trigger proxy on this GO.
    /// </summary>
    public void NotifyCollision()
    {
        if (collisionCooldown > 0f) return;

        collisionCooldown = config.collisionCooldownDuration;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            momentum = -rb.linearVelocity.normalized;
    }

    private void OnCollisionEnter2D(Collision2D col) => NotifyCollision();
}
