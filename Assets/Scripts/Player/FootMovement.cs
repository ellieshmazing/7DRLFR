using UnityEngine;

/// <summary>
/// Procedural walking system. Each foot runs a three-state FSM
/// (Locked / Stepping / Airborne). Locked feet hold position kinematically.
/// Stepping feet arc between start and target via velocity override. Airborne feet
/// are gravity + spring driven using the foot spring params from PlayerConfig.
///
/// Provides two distinct ground-state queries:
///   IsGrounded() — true when any foot is Locked (no grace). Used for force gating,
///                  damping, crouch eligibility.
///   CanJump()    — IsGrounded() OR within coyote time window. Used only for jump
///                  eligibility. Coyote time is cleared on jump to prevent ghost
///                  double-jumps.
///
/// Runs at order -20 so PlayerHipNode (-15) reads a fresh GetGroundReferenceY()
/// each frame. Attach to HipNode GO alongside PlayerHipNode.
/// Wired entirely by PlayerAssembler.
/// </summary>
[DefaultExecutionOrder(-20)]
public class FootMovement : MonoBehaviour
{
    [Tooltip("Live config SO — all foot movement params read per-frame")]
    public PlayerConfig config;

    [Tooltip("Pixel-to-world conversion factor, cached at spawn")]
    public float pixelToWorld;

    [Tooltip("Torso Rigidbody2D — source of velocity and world position")]
    public Rigidbody2D torsoRB;

    [Tooltip("Foot circle collider radius in world units — offsets step targets onto surfaces")]
    public float footColliderRadius;

    [Header("References (wired by PlayerAssembler)")]
    public Rigidbody2D leftFootRB;
    public Rigidbody2D rightFootRB;
    public FootContact  leftFootContact;
    public FootContact  rightFootContact;

    // -------------------------------------------------------------------------
    // Internal types
    // -------------------------------------------------------------------------

    private enum FootState { Locked, Stepping, Airborne }

    private class FootData
    {
        public Rigidbody2D rb;
        public FootContact  contact;
        public FootState    state = FootState.Airborne;
        public Vector2      lockPosition;
        public Vector2      stepStartPos;
        public Vector2      stepTargetPos;
        public float        stepProgress;
        public float        stepDuration;
        public int          side; // -1 = left, +1 = right
        public bool         xLocked;
        public float        lockedX;
    }

    private FootData _left;
    private FootData _right;
    private int      _notPlayerMask;

    // Coyote time: tracks when any foot was last Locked.
    // Set to -999 on jump to prevent ghost double-jumps.
    private float _lastGroundedTime = -999f;

    // Wall sliding: set externally by PlayerSkeletonRoot.
    // Controls foot gravity scale during wall contact.
    private bool _isWallSliding;

    // Jump coast: suppresses airborne X spring for a few frames after jump
    // so feet carry their launch velocity visually instead of snapping
    // back under the hip.
    private float _jumpCoastTimer;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        _notPlayerMask = ~LayerMask.GetMask("Player");
        _left  = new FootData { side = -1, state = FootState.Airborne };
        _right = new FootData { side = +1, state = FootState.Airborne };
    }

    void Start()
    {
        _left.rb       = leftFootRB;
        _left.contact  = leftFootContact;
        _right.rb      = rightFootRB;
        _right.contact = rightFootContact;
    }

    void FixedUpdate()
    {
        if (config == null || torsoRB == null) return;

        float   dt     = Time.fixedDeltaTime;
        Vector2 vel    = torsoRB.linearVelocity;
        float   torsoX = torsoRB.position.x;
        float   torsoY = torsoRB.position.y;

        // --- Track grounded state for coyote time ---
        bool anyLocked = (_left.state == FootState.Locked || _right.state == FootState.Locked);
        if (anyLocked)
            _lastGroundedTime = Time.time;

        // --- Decay jump coast timer ---
        _jumpCoastTimer = Mathf.Max(0f, _jumpCoastTimer - dt);

        // --- Manage foot RB damping based on ground state ---
        float footDamp = anyLocked ? config.footGroundDamping : config.footAirDamping;
        if (_left.rb  != null) _left.rb.linearDamping  = footDamp;
        if (_right.rb != null) _right.rb.linearDamping = footDamp;

        // Trailing foot (side * sign(vel.x) < 0) processes first — tie-break rule.
        bool     leftFirst = _left.side * Mathf.Sign(vel.x) <= 0;
        FootData first     = leftFirst ? _left  : _right;
        FootData second    = leftFirst ? _right : _left;

        UpdateFoot(first,  second, vel, torsoX, torsoY, dt);
        UpdateFoot(second, first,  vel, torsoX, torsoY, dt);

        HandleDirectionReversal(vel, torsoX, torsoY);

        // Invariant: at most one foot may be Stepping at a time.
        Debug.Assert(!(_left.state == FootState.Stepping && _right.state == FootState.Stepping),
            "[FootMovement] Both feet are Stepping simultaneously — this should never happen.");
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Y reference for PlayerHipNode's spring target. Returns the lowest locked
    /// foot's Y; falls back to raw RB positions when both feet are airborne.
    /// </summary>
    public float GetGroundReferenceY()
    {
        bool ll = _left.state  == FootState.Locked;
        bool rl = _right.state == FootState.Locked;
        if (ll && rl) return Mathf.Min(_left.lockPosition.y, _right.lockPosition.y);
        if (ll)       return _left.lockPosition.y;
        if (rl)       return _right.lockPosition.y;
        if (_left.rb != null && _right.rb != null)
            return Mathf.Min(_left.rb.position.y, _right.rb.position.y);
        return transform.position.y;
    }

    /// <summary>
    /// True if any foot is currently Locked. No grace period.
    /// Used for force gating, damping selection, and crouch eligibility.
    /// </summary>
    public bool IsGrounded() =>
        _left.state == FootState.Locked || _right.state == FootState.Locked;

    /// <summary>
    /// True if IsGrounded() or within the coyote time window.
    /// Used ONLY for jump eligibility. Coyote time is cleared on jump
    /// to prevent ghost double-jumps.
    /// </summary>
    public bool CanJump()
    {
        if (IsGrounded()) return true;
        if (config != null && Time.time - _lastGroundedTime <= config.coyoteTime)
            return true;
        return false;
    }

    /// <summary>Y of the lowest locked foot; matches GetGroundReferenceY semantics.</summary>
    public float GetLockedFootY() => GetGroundReferenceY();

    /// <summary>
    /// Called by PlayerSkeletonRoot BEFORE setting foot velocities for the jump.
    /// Clears coyote time (prevents ghost double-jumps), starts jump coast timer
    /// (suppresses X spring so feet carry launch momentum), and transitions both
    /// feet to Airborne.
    /// </summary>
    public void OnJump()
    {
        // Kill coyote window so a quick release+re-press can't double-jump.
        _lastGroundedTime = -999f;

        // Start coast timer: suppresses airborne X spring for a few frames
        // so feet visually carry their launch velocity.
        if (config != null)
            _jumpCoastTimer = config.jumpCoastTime;

        TransitionToAirborne(_left);
        TransitionToAirborne(_right);
    }

    /// <summary>
    /// Sets wall-sliding state. When true, airborne feet use the reduced
    /// wallSlideFootGravityScale instead of normal footGravityScale, keeping
    /// feet and torso descending at similar rates during a wall slide.
    /// Called by PlayerSkeletonRoot each FixedUpdate.
    /// </summary>
    public void SetWallSliding(bool sliding)
    {
        _isWallSliding = sliding;
    }

    // -------------------------------------------------------------------------
    // Per-foot state machine
    // -------------------------------------------------------------------------

    private void UpdateFoot(FootData foot, FootData other,
                            Vector2 vel, float torsoX, float torsoY, float dt)
    {
        switch (foot.state)
        {
            case FootState.Locked:
                foot.rb.position       = foot.lockPosition;
                foot.rb.linearVelocity = Vector2.zero;
                foot.rb.gravityScale   = 0f;

                if (!foot.contact.isGrounded)
                {
                    TransitionToAirborne(foot);
                    break;
                }

                if (Mathf.Abs(vel.x) > config.idleSpeedThreshold)
                {
                    // Walking — step when foot falls behind its ideal position.
                    float idealX       = torsoX + foot.side * config.footSpreadX * pixelToWorld;
                    float signedBehind = (idealX - foot.lockPosition.x) * Mathf.Sign(vel.x);
                    if (signedBehind > config.strideTriggerDistance * pixelToWorld
                        && other.state != FootState.Stepping)
                    {
                        StartStep(foot, vel, torsoX, torsoY);
                    }
                }
                else
                {
                    // Idle — correct feet back toward neutral spread, one at a time.
                    float idealX       = torsoX + foot.side * config.footSpreadX * pixelToWorld;
                    float displacement = Mathf.Abs(foot.lockPosition.x - idealX);
                    if (displacement > config.footSpreadX * pixelToWorld * 0.3f
                        && other.state != FootState.Stepping)
                    {
                        float groundY = RaycastGroundY(idealX, torsoY, foot.lockPosition.y);
                        StartStep(foot, vel, torsoX, torsoY, new Vector2(idealX, groundY));
                    }
                }
                break;

            case FootState.Stepping:
                AdvanceStep(foot, dt);
                break;

            case FootState.Airborne:
                // Gravity: use wall slide scale when sliding, normal otherwise.
                foot.rb.gravityScale = _isWallSliding
                    ? config.wallSlideFootGravityScale
                    : config.footGravityScale;

                float hipX = transform.position.x;
                float hipY = transform.position.y;

                // X: spring with settle-lock so foot tracks hip horizontally.
                // Suppressed during jump coast period.
                UpdateAirborneX(foot, hipX, dt);

                // Y: standard spring toward hip; gravity applied by physics engine.
                float yDisp  = foot.rb.position.y - hipY;
                float yAccel = (-config.FootStiffness * yDisp
                               - config.FootDamping * foot.rb.linearVelocity.y)
                               / config.footSpringMass;
                foot.rb.linearVelocity = new Vector2(
                    foot.rb.linearVelocity.x,
                    foot.rb.linearVelocity.y + yAccel * dt);

                // Landing: lock when touching walkable ground and descending
                // (or near apex — landingVelocityTolerance catches apex landings).
                if (foot.contact.isGrounded
                    && IsWalkable(foot.contact.lastContactNormal)
                    && foot.rb.linearVelocity.y < config.landingVelocityTolerance)
                {
                    LockFoot(foot, foot.rb.position);

                    // Catch-step: if other foot is also airborne, step it to neutral.
                    if (other.state == FootState.Airborne)
                    {
                        float otherIdealX = transform.position.x
                                          + other.side * config.footSpreadX * pixelToWorld;
                        float groundY = RaycastGroundY(otherIdealX, torsoY, other.rb.position.y);
                        StartStep(other, vel, torsoX, torsoY, new Vector2(otherIdealX, groundY));
                    }
                }
                break;
        }
    }

    // -------------------------------------------------------------------------
    // State transitions
    // -------------------------------------------------------------------------

    private void StartStep(FootData foot, Vector2 vel,
                           float torsoX, float torsoY, Vector2? target = null)
    {
        foot.xLocked      = false;
        foot.stepStartPos  = foot.rb.position;
        foot.stepTargetPos = target ?? ComputeStepTarget(foot, vel, torsoX, torsoY);
        foot.stepProgress  = 0f;
        foot.stepDuration  = Mathf.Max(
            config.minStepDuration,
            config.baseStepDuration / (1f + Mathf.Abs(vel.x) * config.stepSpeedScale));
        foot.state           = FootState.Stepping;
        foot.rb.gravityScale = 0f;
    }

    private void AdvanceStep(FootData foot, float dt)
    {
        foot.stepProgress = Mathf.Min(foot.stepProgress + dt / foot.stepDuration, 1f);

        Vector2 arcPos = EvaluateArc(
            foot.stepStartPos, foot.stepTargetPos, foot.stepProgress, config.stepHeight);
        foot.rb.linearVelocity = (arcPos - foot.rb.position) / dt;

        // Early lock: foot contacted walkable ground on the DESCENT (past arc peak).
        // Guard on progress > 0.5 so we don't immediately re-lock on the first frame
        // of a stride-triggered step, when the foot is still physically on the ground.
        if (foot.stepProgress > 0.5f
            && foot.contact.isGrounded
            && IsWalkable(foot.contact.lastContactNormal))
        {
            LockFoot(foot, foot.rb.position);
            return;
        }

        // Arc complete: lock at target.
        if (foot.stepProgress >= 1f)
            LockFoot(foot, foot.stepTargetPos);
    }

    private void LockFoot(FootData foot, Vector2 worldPos)
    {
        FootState previousState = foot.state;

        foot.state             = FootState.Locked;
        foot.lockPosition      = worldPos;
        foot.rb.position       = worldPos;
        foot.rb.linearVelocity = Vector2.zero;
        foot.rb.gravityScale   = 0f;
        foot.xLocked           = false;

        // Footfall impulse: when a step completes (Stepping → Locked),
        // apply a small forward push to the torso. Speed-gated to prevent
        // runaway acceleration (scales down near maxSpeed).
        if (previousState == FootState.Stepping
            && config.footfallImpulse > 0f
            && torsoRB != null)
        {
            float hSpeed = Mathf.Abs(torsoRB.linearVelocity.x);
            if (hSpeed > config.footfallMinSpeed && hSpeed < config.maxSpeed)
            {
                float dir = Mathf.Sign(torsoRB.linearVelocity.x);
                // Scale impulse down as speed approaches maxSpeed.
                float speedScale = Mathf.Clamp01(1f - hSpeed / config.maxSpeed);
                torsoRB.AddForce(
                    new Vector2(dir * config.footfallImpulse * speedScale, 0f),
                    ForceMode2D.Impulse);
            }
        }
    }

    private void TransitionToAirborne(FootData foot)
    {
        foot.state   = FootState.Airborne;
        foot.xLocked = false;
        // Velocity intentionally NOT reset — preserve momentum from prior state.
        if (foot.rb != null)
            foot.rb.gravityScale = config.footGravityScale;
    }

    private void HandleDirectionReversal(Vector2 vel, float torsoX, float torsoY)
    {
        FootData stepping = _left.state  == FootState.Stepping ? _left
                          : _right.state == FootState.Stepping ? _right
                          : null;
        if (stepping == null) return;

        float stepDir = Mathf.Sign(stepping.stepTargetPos.x - stepping.stepStartPos.x);
        float moveDir = Mathf.Sign(vel.x);

        if (Mathf.Abs(vel.x) > config.idleSpeedThreshold
            && moveDir != 0f
            && moveDir != stepDir)
        {
            LockFoot(stepping, stepping.rb.position);
            FootData other = stepping == _left ? _right : _left;
            if (other.state == FootState.Locked)
                StartStep(other, vel, torsoX, torsoY);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a horizontal spring toward the foot's ideal X (hipX ± footSpreadX).
    /// During the jump coast period, the spring is suppressed so feet carry their
    /// launch velocity visually. Once displacement and velocity both settle below
    /// threshold, the X is pinned kinematically (xLocked) so it tracks the hip
    /// with zero wobble.
    /// </summary>
    private void UpdateAirborneX(FootData foot, float hipX, float dt)
    {
        // During jump coast, let feet carry launch velocity — no X spring.
        if (_jumpCoastTimer > 0f)
            return;

        float targetX = hipX + foot.side * config.footSpreadX * pixelToWorld;
        float xDisp   = foot.rb.position.x - targetX;
        float xVel    = foot.rb.linearVelocity.x;

        if (foot.xLocked)
        {
            float unlockWu = config.footXUnlockThreshold * pixelToWorld;
            if (Mathf.Abs(xDisp) > unlockWu)
            {
                foot.xLocked = false;
                // Fall through to spring below.
            }
            else
            {
                // Hold x at the locked position; y remains spring/gravity driven.
                foot.rb.position       = new Vector2(foot.lockedX, foot.rb.position.y);
                foot.rb.linearVelocity = new Vector2(0f, foot.rb.linearVelocity.y);
                return;
            }
        }

        // Standard spring-damper on X.
        float xAccel = (-config.FootStiffness * xDisp - config.FootDamping * xVel)
                       / config.footSpringMass;
        foot.rb.linearVelocity = new Vector2(xVel + xAccel * dt, foot.rb.linearVelocity.y);

        // Settle-lock: once near target with low velocity, pin x to stop oscillation.
        float lockWu = config.footXLockThreshold * pixelToWorld;
        if (Mathf.Abs(xDisp) < lockWu && Mathf.Abs(xVel) < config.footXLockVelocity)
        {
            foot.xLocked           = true;
            foot.lockedX           = targetX;
            foot.rb.position       = new Vector2(targetX, foot.rb.position.y);
            foot.rb.linearVelocity = new Vector2(0f, foot.rb.linearVelocity.y);
        }
    }

    private Vector2 ComputeStepTarget(FootData foot, Vector2 vel, float torsoX, float torsoY)
    {
        float idealX  = torsoX + foot.side * config.footSpreadX * pixelToWorld;
        float targetX = idealX + vel.x * config.strideProjectionTime;
        float targetY = RaycastGroundY(targetX, torsoY, foot.lockPosition.y);
        return new Vector2(targetX, targetY);
    }

    private float RaycastGroundY(float x, float torsoY, float fallbackY)
    {
        float rayDist    = config.stepRaycastDistance * pixelToWorld;
        float rayOriginY = torsoY;   // cast the full distance downward from torso level
        var   hit        = Physics2D.Raycast(
            new Vector2(x, rayOriginY), Vector2.down, rayDist, _notPlayerMask);
        if (hit.collider != null && IsWalkable(hit.normal))
            return hit.point.y + footColliderRadius;
        return fallbackY;
    }

    private Vector2 EvaluateArc(Vector2 startPos, Vector2 targetPos, float t, float heightPx)
    {
        float heightWorld = heightPx * pixelToWorld;
        float x     = Mathf.Lerp(startPos.x, targetPos.x, t);
        float yBase = Mathf.Lerp(startPos.y, targetPos.y, t);
        float yArc  = yBase + heightWorld * Mathf.Sin(Mathf.PI * t);
        return new Vector2(x, yArc);
    }

    private bool IsWalkable(Vector2 normal) =>
        Vector2.Angle(normal, Vector2.up) <= config.maxWalkableAngle;
}
