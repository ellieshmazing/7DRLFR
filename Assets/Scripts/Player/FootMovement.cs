using UnityEngine;

/// <summary>
/// Procedural walking FSM. Three-state per-foot machine (Locked / Stepping / Airborne)
/// that replaces PlayerFeet's static spring-to-formation behavior.
///
/// Locked   — foot is planted; holds rb.position exactly at lockPosition each frame.
/// Stepping — foot travels along a sinusoidal arc from start to target.
/// Airborne — foot is physics-driven with a spring toward the hip spread position.
///
/// Provides GetGroundReferenceY() for PlayerHipNode and CanJump()/OnJump() for
/// PlayerSkeletonRoot. Runs at -20, before PlayerHipNode (-15).
///
/// Attach to the HipNode GO alongside PlayerHipNode.
/// </summary>
[DefaultExecutionOrder(-20)]
public class FootMovement : MonoBehaviour
{
    [Header("Config (set by PlayerAssembler)")]
    public PlayerConfig config;
    public float pixelToWorld;

    [Header("Wiring (set by PlayerAssembler)")]
    public Rigidbody2D torsoRB;
    public Rigidbody2D leftFootRB;
    public Rigidbody2D rightFootRB;
    public FootContact leftFootContact;
    public FootContact rightFootContact;

    // ---- Internal state -------------------------------------------------

    enum FootState { Locked, Stepping, Airborne }

    struct FootData
    {
        public Rigidbody2D rb;
        public FootContact contact;
        public FootState   state;
        public Vector2     lockPosition;
        public Vector2     stepStartPos;
        public Vector2     stepTargetPos;
        public float       stepProgress;
        public float       stepDuration;
        public int         side;   // -1 = left, +1 = right
    }

    FootData _left, _right;
    float    _footColRadius;
    int      _notPlayerMask;

    // ---- Unity lifecycle ------------------------------------------------

    void Awake()
    {
        int layer = LayerMask.NameToLayer("Player");
        _notPlayerMask = layer >= 0 ? ~(1 << layer) : ~0;

        // Derive world-space collider radius from the actual CircleCollider2D.
        var col = leftFootRB != null ? leftFootRB.GetComponent<CircleCollider2D>() : null;
        _footColRadius = col != null ? col.radius * leftFootRB.transform.lossyScale.x : 0f;

        _left  = new FootData { rb = leftFootRB,  contact = leftFootContact,  side = -1 };
        _right = new FootData { rb = rightFootRB, contact = rightFootContact, side =  1 };
    }

    void Start()
    {
        // Begin airborne — feet lock on first ground contact.
        TransitionToAirborne(ref _left);
        TransitionToAirborne(ref _right);
    }

    void FixedUpdate()
    {
        if (config == null || torsoRB == null) return;

        Vector2 vel      = torsoRB.linearVelocity;
        Vector2 torsoPos = torsoRB.position;
        float   dt       = Time.fixedDeltaTime;

        UpdateFoot(ref _left,  ref _right, vel, torsoPos, dt);
        UpdateFoot(ref _right, ref _left,  vel, torsoPos, dt);
        HandleDirectionReversal(vel);
    }

    // ---- Public API -----------------------------------------------------

    /// <summary>
    /// Y coordinate PlayerHipNode should spring toward.
    /// Uses locked foot positions for stability; falls back to RB positions when airborne.
    /// </summary>
    public float GetGroundReferenceY()
    {
        bool ll = _left.state  == FootState.Locked;
        bool rl = _right.state == FootState.Locked;
        if (ll && rl) return Mathf.Min(_left.lockPosition.y, _right.lockPosition.y);
        if (ll)       return _left.lockPosition.y;
        if (rl)       return _right.lockPosition.y;
        return Mathf.Min(leftFootRB.position.y, rightFootRB.position.y);
    }

    /// <summary>Returns true if at least one foot is planted (Locked).</summary>
    public bool CanJump() =>
        _left.state == FootState.Locked || _right.state == FootState.Locked;

    /// <summary>World-space Y of the lowest locked foot, for jump impulse calculation.</summary>
    public float GetLockedFootY() => GetGroundReferenceY();

    /// <summary>Called by PlayerSkeletonRoot after applying jump impulse. Releases all locks.</summary>
    public void OnJump()
    {
        TransitionToAirborne(ref _left);
        TransitionToAirborne(ref _right);
    }

    // ---- Per-foot FSM ---------------------------------------------------

    void UpdateFoot(ref FootData foot, ref FootData other,
                    Vector2 vel, Vector2 torsoPos, float dt)
    {
        switch (foot.state)
        {
            case FootState.Locked:
            {
                // Hold position — override physics every frame.
                foot.rb.position       = foot.lockPosition;
                foot.rb.linearVelocity = Vector2.zero;
                foot.rb.gravityScale   = 0f;

                if (!foot.contact.isGrounded)
                {
                    TransitionToAirborne(ref foot);
                    break;
                }

                float idealX = torsoPos.x + foot.side * config.footSpreadX * pixelToWorld;

                if (Mathf.Abs(vel.x) > config.idleSpeedThreshold)
                {
                    // Step when foot lags too far behind the ideal formation position.
                    float signedBehind = (idealX - foot.lockPosition.x) * Mathf.Sign(vel.x);
                    if (signedBehind > config.strideTriggerDistance * pixelToWorld
                        && other.state != FootState.Stepping)
                        StartStep(ref foot, vel, torsoPos, null);
                }
                else
                {
                    // Idle: correct foot toward neutral spread if displaced.
                    float displacement = Mathf.Abs(foot.lockPosition.x - idealX);
                    if (displacement > config.footSpreadX * pixelToWorld * 0.3f
                        && other.state != FootState.Stepping)
                    {
                        float groundY = RaycastGroundY(idealX, torsoPos, foot.lockPosition.y);
                        StartStep(ref foot, vel, torsoPos, new Vector2(idealX, groundY));
                    }
                }
                break;
            }

            case FootState.Stepping:
                AdvanceStep(ref foot, dt);
                break;

            case FootState.Airborne:
            {
                foot.rb.gravityScale = config.footGravityScale;
                ApplyAirborneSpring(ref foot, dt);

                if (foot.contact.isGrounded && IsWalkable(foot.contact.lastContactNormal))
                {
                    LockFoot(ref foot, foot.rb.position);

                    // Catch-step: if other foot is also airborne, aim it toward neutral.
                    if (other.state == FootState.Airborne)
                    {
                        float ox = torsoPos.x + other.side * config.footSpreadX * pixelToWorld;
                        float oy = RaycastGroundY(ox, torsoPos, other.rb.position.y);
                        StartStep(ref other, vel, torsoPos, new Vector2(ox, oy));
                    }
                }
                break;
            }
        }
    }

    void AdvanceStep(ref FootData foot, float dt)
    {
        foot.stepProgress = Mathf.Min(foot.stepProgress + dt / foot.stepDuration, 1f);

        Vector2 arcPos = EvaluateArc(foot.stepStartPos, foot.stepTargetPos,
                                     foot.stepProgress, config.stepHeight * pixelToWorld);
        foot.rb.linearVelocity = (arcPos - foot.rb.position) / dt;

        // Early lock: walked onto a surface mid-arc.
        if (foot.contact.isGrounded && IsWalkable(foot.contact.lastContactNormal))
        {
            LockFoot(ref foot, foot.rb.position);
            return;
        }

        if (foot.stepProgress >= 1f)
            LockFoot(ref foot, foot.stepTargetPos);
    }

    void ApplyAirborneSpring(ref FootData foot, float dt)
    {
        // Spring toward hip spread position (same params as old PlayerFeet).
        float hipX = transform.position.x;
        float hipY = transform.position.y;

        float targetX   = hipX + foot.side * config.footSpreadX * pixelToWorld;
        float stiffness = config.FootStiffness;
        float damping   = config.FootDamping;
        float mass      = config.footSpringMass;

        float xDisp  = foot.rb.position.x - targetX;
        float xAccel = (-stiffness * xDisp  - damping * foot.rb.linearVelocity.x) / mass;
        float xVel   = foot.rb.linearVelocity.x + xAccel * dt;

        float yDisp  = foot.rb.position.y - hipY;
        float yAccel = (-stiffness * yDisp  - damping * foot.rb.linearVelocity.y) / mass;
        float yVel   = foot.rb.linearVelocity.y + yAccel * dt;

        foot.rb.linearVelocity = new Vector2(xVel, yVel);
    }

    // ---- Transitions ----------------------------------------------------

    void StartStep(ref FootData foot, Vector2 vel, Vector2 torsoPos, Vector2? targetOverride)
    {
        foot.stepStartPos  = foot.rb.position;
        foot.stepTargetPos = targetOverride ?? ComputeStepTarget(ref foot, vel, torsoPos);
        foot.stepProgress  = 0f;
        float speed = Mathf.Abs(vel.x);
        foot.stepDuration  = Mathf.Max(config.minStepDuration,
                                       config.baseStepDuration / (1f + speed * config.stepSpeedScale));
        foot.state           = FootState.Stepping;
        foot.rb.gravityScale = 0f;
    }

    void LockFoot(ref FootData foot, Vector2 worldPos)
    {
        foot.state             = FootState.Locked;
        foot.lockPosition      = worldPos;
        foot.rb.position       = worldPos;
        foot.rb.linearVelocity = Vector2.zero;
        foot.rb.gravityScale   = 0f;
    }

    void TransitionToAirborne(ref FootData foot)
    {
        foot.state           = FootState.Airborne;
        foot.rb.gravityScale = config != null ? config.footGravityScale : 1f;
        // Velocity is intentionally NOT reset — preserves momentum.
    }

    void HandleDirectionReversal(Vector2 vel)
    {
        if (Mathf.Abs(vel.x) <= config.idleSpeedThreshold) return;

        float moveDir = Mathf.Sign(vel.x);

        if (_left.state == FootState.Stepping)
        {
            float stepDir = Mathf.Sign(_left.stepTargetPos.x - _left.stepStartPos.x);
            if (stepDir != 0f && moveDir != stepDir)
            {
                LockFoot(ref _left, _left.rb.position);
                if (_right.state == FootState.Locked)
                    StartStep(ref _right, vel, torsoRB.position, null);
            }
        }
        else if (_right.state == FootState.Stepping)
        {
            float stepDir = Mathf.Sign(_right.stepTargetPos.x - _right.stepStartPos.x);
            if (stepDir != 0f && moveDir != stepDir)
            {
                LockFoot(ref _right, _right.rb.position);
                if (_left.state == FootState.Locked)
                    StartStep(ref _left, vel, torsoRB.position, null);
            }
        }
    }

    // ---- Helpers --------------------------------------------------------

    Vector2 ComputeStepTarget(ref FootData foot, Vector2 vel, Vector2 torsoPos)
    {
        float idealX  = torsoPos.x + foot.side * config.footSpreadX * pixelToWorld;
        float targetX = idealX + vel.x * config.strideProjectionTime;
        float targetY = RaycastGroundY(targetX, torsoPos, foot.lockPosition.y);
        return new Vector2(targetX, targetY);
    }

    float RaycastGroundY(float x, Vector2 torsoPos, float fallbackY)
    {
        float rayDist    = config.stepRaycastDistance * pixelToWorld;
        float rayOriginY = torsoPos.y + rayDist * 0.5f;
        var   hit        = Physics2D.Raycast(new Vector2(x, rayOriginY),
                                             Vector2.down, rayDist, _notPlayerMask);
        return hit.collider != null && IsWalkable(hit.normal)
            ? hit.point.y + _footColRadius
            : fallbackY;
    }

    static Vector2 EvaluateArc(Vector2 start, Vector2 end, float t, float heightWorld)
    {
        float x    = Mathf.Lerp(start.x, end.x, t);
        float yBase = Mathf.Lerp(start.y, end.y, t);
        float yArc  = yBase + heightWorld * Mathf.Sin(Mathf.PI * t);
        return new Vector2(x, yArc);
    }

    bool IsWalkable(Vector2 normal) =>
        Vector2.Angle(normal, Vector2.up) <= config.maxWalkableAngle;
}
