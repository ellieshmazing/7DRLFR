using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Autonomous pathfinding for the centipede head.
///
/// Each replan generates a circular arc from the head to the player by randomizing
/// the departure angle within arcAngleVariance of the direct approach. Arcs whose
/// radius falls below minTurnRadius are rejected and resampled. A sinusoidal lateral
/// offset applied each frame produces organic wriggling motion that propagates
/// tail-ward through the SkeletonNode trail automatically.
///
/// Attach to the same GameObject as SkeletonRoot. Call Initialize() after AddComponent.
/// Collision response is available via NotifyCollision() for external callers (e.g. the
/// head Ball) or automatically via OnCollisionEnter2D if the head GO has a collider.
/// </summary>
[DefaultExecutionOrder(-5)]
[RequireComponent(typeof(Rigidbody2D))]
public class CentipedePathfinder : MonoBehaviour
{
    private readonly struct Arc
    {
        public readonly Vector2 Center;
        public readonly float   Radius;
        public readonly float   StartAngle;
        public readonly float   EndAngle;
        public readonly int     Direction; // +1 = CCW, -1 = CW
        public readonly bool    Valid;

        public Arc(Vector2 center, float radius, float startAngle, float endAngle, int direction)
        {
            Center     = center;
            Radius     = radius;
            StartAngle = startAngle;
            EndAngle   = endAngle;
            Direction  = direction;
            Valid      = true;
        }
    }

    private CentipedeConfig config;
    private Rigidbody2D     rb;

    private Arc   currentArc;
    private float currentAngle;
    private bool  hasArc;

    private float  wavePhase;
    private float  replanTimer;
    private float  collisionCooldown;
    private float? collisionBaseOverride;

    public Transform Target { get; private set; }

    // ── Initialization ────────────────────────────────────────────────────────

    public void Initialize(CentipedeConfig cfg, Transform playerTarget)
    {
        config = cfg;
        Target = playerTarget;
        rb     = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        wavePhase = Random.Range(0f, Mathf.PI * 2f);
        Replan();
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    private Vector2 GetTargetPosition()
    {
        if (Target != null)
            return Target.position;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        return Camera.main.ScreenToWorldPoint(mouseScreen);
    }

    private void FixedUpdate()
    {
        if (config == null) return;

        if (collisionCooldown > 0f)
            collisionCooldown -= Time.fixedDeltaTime;

        replanTimer -= Time.fixedDeltaTime;
        if (replanTimer <= 0f)
            Replan();

        if (!hasArc) return;

        if (Vector2.Distance(rb.position, GetTargetPosition()) <= config.targetArrivalRadius)
        {
            Replan();
            return;
        }

        wavePhase    += config.waveFrequency * Time.fixedDeltaTime;
        AdvanceArc(Time.fixedDeltaTime);

        Vector2 headTarget = GetHeadTarget();
        Vector2 toTarget   = headTarget - rb.position;
        float   dist       = toTarget.magnitude;

        rb.linearVelocity = dist > 0.01f ? (toTarget / dist) * config.speed : Vector2.zero;
    }

    // ── Replanning ────────────────────────────────────────────────────────────

    private void Replan()
    {
        if (config == null || rb == null) return;

        ResetReplanTimer();

        Vector2 headPos   = rb.position;
        Vector2 playerPos = GetTargetPosition();
        if (Vector2.Distance(headPos, playerPos) < 0.01f) return;

        float baseAngle = collisionBaseOverride
            ?? Mathf.Atan2(playerPos.y - headPos.y, playerPos.x - headPos.x);
        collisionBaseOverride = null;

        float varianceRad  = config.arcAngleVariance * Mathf.Deg2Rad;
        Arc   bestFailed   = default;
        float bestFailedR  = 0f;

        for (int attempt = 0; attempt < config.maxReplanAttempts; attempt++)
        {
            float entryAngle = baseAngle + Random.Range(-varianceRad, varianceRad);
            Arc   arc        = ComputeArc(headPos, playerPos, entryAngle);

            if (!arc.Valid) continue;

            if (arc.Radius >= config.minTurnRadius)
            {
                AcceptArc(arc);
                return;
            }

            if (arc.Radius > bestFailedR)
            {
                bestFailedR = arc.Radius;
                bestFailed  = arc;
            }
        }

        if (bestFailed.Valid)
            AcceptArc(bestFailed);
        else
        {
            Arc fallback = ComputeArc(headPos, playerPos, baseAngle);
            if (fallback.Valid) AcceptArc(fallback);
        }
    }

    // ── Arc math ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives the unique circular arc that departs <paramref name="from"/> at
    /// <paramref name="entryAngle"/> and passes through <paramref name="to"/>.
    ///
    /// The entry tangent T = (cos θ, sin θ). The cross product T × d (where
    /// d = to − from) determines whether the center lies to the left (CCW arc)
    /// or right (CW arc) of the tangent. Radius = |d|² / (2 · perpDir · d).
    /// </summary>
    private static Arc ComputeArc(Vector2 from, Vector2 to, float entryAngle)
    {
        Vector2 d    = to - from;
        float   dSqr = d.sqrMagnitude;
        if (dSqr < 0.0001f) return default;

        float tx    = Mathf.Cos(entryAngle);
        float ty    = Mathf.Sin(entryAngle);
        float cross = tx * d.y - ty * d.x;

        Vector2 perpDir;
        int     direction;

        if (cross >= 0f)
        {
            perpDir   = new Vector2(-ty, tx); // left of tangent → CCW
            direction = 1;
        }
        else
        {
            perpDir   = new Vector2(ty, -tx); // right of tangent → CW
            direction = -1;
        }

        float dot = Vector2.Dot(perpDir, d);
        if (Mathf.Abs(dot) < 0.0001f) return default;

        float   radius = dSqr / (2f * dot);
        if (radius <= 0f) return default;

        Vector2 center     = from + perpDir * radius;
        float   startAngle = Mathf.Atan2(from.y - center.y, from.x - center.x);
        float   endAngle   = Mathf.Atan2(to.y   - center.y, to.x   - center.x);

        if (direction > 0)
            while (endAngle <= startAngle) endAngle += 2f * Mathf.PI;
        else
            while (endAngle >= startAngle) endAngle -= 2f * Mathf.PI;

        return new Arc(center, radius, startAngle, endAngle, direction);
    }

    private void AcceptArc(Arc arc)
    {
        currentArc   = arc;
        currentAngle = arc.StartAngle;
        hasArc       = true;
    }

    private void AdvanceArc(float deltaTime)
    {
        currentAngle += currentArc.Direction * (config.speed / currentArc.Radius) * deltaTime;

        bool complete = currentArc.Direction > 0
            ? currentAngle >= currentArc.EndAngle
            : currentAngle <= currentArc.EndAngle;

        if (complete) Replan();
    }

    private Vector2 GetHeadTarget()
    {
        Vector2 arcPos  = currentArc.Center
            + currentArc.Radius * new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));

        Vector2 tangent = currentArc.Direction
            * new Vector2(-Mathf.Sin(currentAngle), Mathf.Cos(currentAngle));

        Vector2 lateral = new Vector2(-tangent.y, tangent.x);

        return arcPos + lateral * (Mathf.Sin(wavePhase) * config.waveAmplitude);
    }

    // ── Body wave query ───────────────────────────────────────────────────────

    /// <summary>
    /// Lateral wave offset for a body node at the given index.
    /// The SkeletonNode trail naturally propagates the wriggle by recording the
    /// head's physical path — this method is available for systems that need an
    /// explicit per-node phase value (e.g. particle effects, custom node rendering).
    /// </summary>
    public float GetBodyWaveOffset(int nodeIndex)
    {
        float phase = wavePhase - nodeIndex * config.wavePhaseOffsetPerNode;
        return Mathf.Sin(phase) * config.waveAmplitude;
    }

    // ── Collision response ────────────────────────────────────────────────────

    /// <summary>
    /// Inverts the current heading and replans immediately.
    /// Call from the head Ball's collision callback or any trigger proxy on this GO.
    /// Respects collisionCooldownDuration to prevent rapid flip-flopping.
    /// </summary>
    public void NotifyCollision()
    {
        if (collisionCooldown > 0f) return;

        collisionCooldown = config.collisionCooldownDuration;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            collisionBaseOverride = Mathf.Atan2(-rb.linearVelocity.y, -rb.linearVelocity.x);

        hasArc = false;
        Replan();
    }

    private void OnCollisionEnter2D(Collision2D col) => NotifyCollision();

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void ResetReplanTimer()
    {
        replanTimer = config.replanInterval
            + Random.Range(-config.replanJitter, config.replanJitter);
    }
}
