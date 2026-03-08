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
    public FootContact leftFootContact;
    public FootContact rightFootContact;

    // -------------------------------------------------------------------------
    // Internal types
    // -------------------------------------------------------------------------

    private enum FootState { Locked, Stepping, Airborne }

    private class FootData
    {
        public Rigidbody2D rb;
        public FootContact contact;
        public FootState state = FootState.Airborne;
        public Vector2 lockPosition;
        public Vector2 stepStartPos;
        public Vector2 stepTargetPos;
        public float stepProgress;
        public float stepDuration;
        public int side; // -1 = left, +1 = right
        public bool xLocked;
        public float lockedX;
        public bool idleCorrectionArmed = true;
    }

    private FootData _left;
    private FootData _right;
    private int _notPlayerMask;

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

    // Landing velocity capture: accumulates max downward speed across both
    // feet over a short window so PlayerSkeletonRoot can convert it to crouch.
    private float _capturedLandingSpeed;
    private float _landingCaptureTimer;
    private bool _landingReady;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        _notPlayerMask = ~LayerMask.GetMask("Player");
        _left = new FootData { side = -1, state = FootState.Airborne };
        _right = new FootData { side = +1, state = FootState.Airborne };
    }

    void Start()
    {
        _left.rb = leftFootRB;
        _left.contact = leftFootContact;
        _right.rb = rightFootRB;
        _right.contact = rightFootContact;
    }

    void FixedUpdate()
    {
        if (config == null || torsoRB == null) return;

        float dt = Time.fixedDeltaTime;
        Vector2 vel = torsoRB.linearVelocity;
        float torsoX = torsoRB.position.x;
        float torsoY = torsoRB.position.y;

        // --- Ground probe: verify locked feet still have ground ---
        ProbeLockedFootGround(_left);
        ProbeLockedFootGround(_right);

        // --- Track grounded state for coyote time ---
        bool anyLocked = (_left.state == FootState.Locked || _right.state == FootState.Locked);
        if (anyLocked)
            _lastGroundedTime = Time.time;

        // --- Decay jump coast timer ---
        _jumpCoastTimer = Mathf.Max(0f, _jumpCoastTimer - dt);

        // --- Manage foot RB damping based on ground state ---
        float footDamp = anyLocked ? config.footGroundDamping : config.footAirDamping;
        if (_left.rb != null) _left.rb.linearDamping = footDamp;
        if (_right.rb != null) _right.rb.linearDamping = footDamp;

        // Trailing foot (side * sign(vel.x) < 0) processes first — tie-break rule.
        bool leftFirst = _left.side * Mathf.Sign(vel.x) <= 0;
        FootData first = leftFirst ? _left : _right;
        FootData second = leftFirst ? _right : _left;

        UpdateFoot(first, second, vel, torsoX, torsoY, dt);
        UpdateFoot(second, first, vel, torsoX, torsoY, dt);

        HandleDirectionReversal(vel, torsoX, torsoY);

        // --- Landing capture window ---
        if (_landingCaptureTimer > 0f)
        {
            _landingCaptureTimer -= dt;
            if (_landingCaptureTimer <= 0f)
                _landingReady = true;
        }

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
        bool ll = _left.state == FootState.Locked;
        bool rl = _right.state == FootState.Locked;
        if (ll && rl) return Mathf.Min(_left.lockPosition.y, _right.lockPosition.y);
        if (ll) return _left.lockPosition.y;
        if (rl) return _right.lockPosition.y;
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

    /// <summary>
    /// Returns the maximum downward velocity captured during the most recent
    /// landing window, then clears internal state. Returns 0 if no landing
    /// is pending. Called by PlayerSkeletonRoot each FixedUpdate.
    /// </summary>
    public float ConsumeLastLandingSpeed()
    {
        if (_landingReady)
        {
            _landingReady = false;
            float result = _capturedLandingSpeed;
            _capturedLandingSpeed = 0f;
            return result;
        }
        return 0f;
    }

    /// <summary>
    /// X coordinate representing the feet's center of mass. Used by the body
    /// leash in PlayerSkeletonRoot as the anchor point.
    /// Prefers locked foot positions; falls back to RB midpoint when airborne.
    /// </summary>
    public float GetFootCenterX()
    {
        bool ll = _left.state == FootState.Locked;
        bool rl = _right.state == FootState.Locked;
        if (ll && rl) return (_left.lockPosition.x + _right.lockPosition.x) * 0.5f;
        if (ll) return _left.lockPosition.x;
        if (rl) return _right.lockPosition.x;
        if (_left.rb != null && _right.rb != null)
            return (_left.rb.position.x + _right.rb.position.x) * 0.5f;
        return transform.position.x;
    }

    /// <summary>
    /// Checks whether locked feet are walled in the given direction. Returns
    /// true if horizontal force should be suppressed. Exception: if the other
    /// locked foot is further ahead in moveDir and not walled, movement is
    /// permitted (allows climbing over low walls).
    /// </summary>
    public bool IsMovementBlockedByWall(float moveDir)
    {
        if (moveDir == 0f) return false;

        bool ll = _left.state == FootState.Locked;
        bool rl = _right.state == FootState.Locked;
        if (!ll && !rl) return false;

        bool leftWalled = ll && _left.contact.isWalled
                           && _left.contact.lastWallNormal.x * moveDir < 0f;
        bool rightWalled = rl && _right.contact.isWalled
                           && _right.contact.lastWallNormal.x * moveDir < 0f;

        if (!leftWalled && !rightWalled) return false;

        // Exception: other locked foot is ahead in moveDir and not walled.
        if (leftWalled && rl && !rightWalled)
            if ((_right.lockPosition.x - _left.lockPosition.x) * moveDir > 0f)
                return false;

        if (rightWalled && ll && !leftWalled)
            if ((_left.lockPosition.x - _right.lockPosition.x) * moveDir > 0f)
                return false;

        return true;
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
                foot.rb.position = foot.lockPosition;
                foot.rb.linearVelocity = Vector2.zero;
                foot.rb.gravityScale = 0f;

                if (!foot.contact.isGrounded)
                {
                    TransitionToAirborne(foot);
                    break;
                }

                if (Mathf.Abs(vel.x) > config.idleSpeedThreshold)
                {
                    // Walking — step when foot falls behind its ideal position.
                    float idealX = torsoX + foot.side * config.footSpreadX * pixelToWorld;
                    float signedBehind = (idealX - foot.lockPosition.x) * Mathf.Sign(vel.x);
                    if (signedBehind > config.strideTriggerDistance * pixelToWorld
                        && other.state != FootState.Stepping)
                    {
                        StartStep(foot, other, vel, torsoX, torsoY);
                    }
                }
                else
                {
                    // Idle — correct feet back toward neutral spread, one at a time.
                    // Hysteresis prevents spring oscillation from re-triggering continuously:
                    // disarmed after firing; re-armed only once displacement falls below the
                    // smaller hysteresis band.
                    float idealX = torsoX + foot.side * config.footSpreadX * pixelToWorld;
                    float displacement = Mathf.Abs(foot.lockPosition.x - idealX);

                    if (!foot.idleCorrectionArmed
                        && displacement < config.idleCorrectionHysteresis * pixelToWorld)
                        foot.idleCorrectionArmed = true;

                    if (foot.idleCorrectionArmed
                        && displacement > config.idleCorrectionThreshold * pixelToWorld
                        && other.state != FootState.Stepping)
                    {
                        foot.idleCorrectionArmed = false;
                        float groundY = RaycastGroundY(idealX, torsoY, foot.lockPosition.y);
                        StartStep(foot, other, vel, torsoX, torsoY, new Vector2(idealX, groundY));
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
                float yDisp = foot.rb.position.y - hipY;
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
                        StartStep(other, foot, vel, torsoX, torsoY, new Vector2(otherIdealX, groundY));
                    }
                }
                break;
        }
    }

    // -------------------------------------------------------------------------
    // State transitions
    // -------------------------------------------------------------------------

    private void StartStep(FootData foot, FootData other, Vector2 vel,
                           float torsoX, float torsoY, Vector2? target = null)
    {
        foot.xLocked = false;
        foot.stepStartPos = foot.rb.position;
        Vector2 stepTarget = target ?? ComputeStepTarget(foot, vel, torsoX, torsoY);

        // --- Obstacle pre-check: horizontal ray at arc-peak height ---
        float moveDir = Mathf.Sign(stepTarget.x - foot.rb.position.x);
        if (moveDir != 0f)
        {
            float clearanceY = foot.rb.position.y
                             + config.stepHeight * pixelToWorld + footColliderRadius;
            float castDist = Mathf.Abs(stepTarget.x - foot.rb.position.x) + footColliderRadius;
            var hit = Physics2D.Raycast(
                new Vector2(foot.rb.position.x, clearanceY),
                new Vector2(moveDir, 0f), castDist, _notPlayerMask);

            if (hit.collider != null && !IsWalkable(hit.normal))
            {
                float shortenedX = hit.point.x - moveDir * footColliderRadius;
                if (Mathf.Abs(shortenedX - foot.rb.position.x) < config.minStepDistance * pixelToWorld)
                    return; // step too short — stay locked

                stepTarget.x = shortenedX;
                stepTarget.y = RaycastGroundY(shortenedX, torsoY, foot.rb.position.y);
            }
        }

        // --- Foot separation limit ---
        if (other.state == FootState.Locked)
        {
            float maxSepWU = config.maxFootSeparation * pixelToWorld;
            if (Mathf.Abs(stepTarget.x - other.lockPosition.x) > maxSepWU)
            {
                stepTarget.x = other.lockPosition.x
                             + Mathf.Sign(stepTarget.x - other.lockPosition.x) * maxSepWU;
                stepTarget.y = RaycastGroundY(stepTarget.x, torsoY, foot.rb.position.y);
            }
        }

        foot.stepTargetPos = stepTarget;
        foot.stepProgress = 0f;
        foot.stepDuration = Mathf.Max(
            config.minStepDuration,
            config.baseStepDuration / (1f + Mathf.Abs(vel.x) * config.stepSpeedScale));
        foot.state = FootState.Stepping;
        foot.rb.gravityScale = 0f;
    }

    private void AdvanceStep(FootData foot, float dt)
    {
        foot.stepProgress = Mathf.Min(foot.stepProgress + dt / foot.stepDuration, 1f);

        Vector2 arcPos = EvaluateArc(
            foot.stepStartPos, foot.stepTargetPos, foot.stepProgress, config.stepHeight);

        // Arc collision: linecast from current to next arc position.
        var arcHit = Physics2D.Linecast(foot.rb.position, arcPos, _notPlayerMask);
        if (arcHit.collider != null && !IsWalkable(arcHit.normal))
        {
            Vector2 lockPos = arcHit.point
                            - (arcPos - foot.rb.position).normalized * footColliderRadius;
            LockFoot(foot, lockPos);
            return;
        }

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

        // Capture landing velocity BEFORE zeroing — must precede state mutations.
        if (previousState == FootState.Airborne)
            CaptureLandingVelocity(foot);

        // Edge landing nudge: push foot inward on platform edges.
        if (previousState == FootState.Airborne && config != null)
        {
            float normalX = foot.contact.lastContactNormal.x;
            if (Mathf.Abs(normalX) > 0.1f)
            {
                float nudgeWU = config.edgeLandingNudge * pixelToWorld;
                worldPos.x -= normalX * nudgeWU;
            }
        }

        // Re-arm idle correction on fresh landings so the foot can correct if it
        // lands off-neutral. Keep disarmed when completing a correction step
        // (Stepping → Locked) so the hysteresis band must clear before re-firing.
        if (previousState == FootState.Airborne)
            foot.idleCorrectionArmed = true;

        foot.state = FootState.Locked;
        foot.lockPosition = worldPos;
        foot.rb.position = worldPos;
        foot.rb.linearVelocity = Vector2.zero;
        foot.rb.gravityScale = 0f;
        foot.xLocked = false;

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
        foot.state = FootState.Airborne;
        foot.xLocked = false;
        foot.idleCorrectionArmed = true; // will land fresh; eligible for correction immediately
        // Velocity intentionally NOT reset — preserve momentum from prior state.
        if (foot.rb != null)
            foot.rb.gravityScale = config.footGravityScale;
    }

    private void HandleDirectionReversal(Vector2 vel, float torsoX, float torsoY)
    {
        FootData stepping = _left.state == FootState.Stepping ? _left
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
                StartStep(other, stepping, vel, torsoX, torsoY);
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
        float xDisp = foot.rb.position.x - targetX;
        float xVel = foot.rb.linearVelocity.x;

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
                foot.rb.position = new Vector2(foot.lockedX, foot.rb.position.y);
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
            foot.xLocked = true;
            foot.lockedX = targetX;
            foot.rb.position = new Vector2(targetX, foot.rb.position.y);
            foot.rb.linearVelocity = new Vector2(0f, foot.rb.linearVelocity.y);
        }
    }

    private Vector2 ComputeStepTarget(FootData foot, Vector2 vel, float torsoX, float torsoY)
    {
        float idealX = torsoX + foot.side * config.footSpreadX * pixelToWorld;
        float targetX = idealX + vel.x * config.strideProjectionTime;
        float targetY = RaycastGroundY(targetX, torsoY, foot.lockPosition.y);
        return new Vector2(targetX, targetY);
    }

    private float RaycastGroundY(float x, float torsoY, float fallbackY)
    {
        float rayDist = config.stepRaycastDistance * pixelToWorld;
        float rayOriginY = torsoY;   // cast the full distance downward from torso level
        var hit = Physics2D.Raycast(
            new Vector2(x, rayOriginY), Vector2.down, rayDist, _notPlayerMask);
        if (hit.collider != null && IsWalkable(hit.normal))
            return hit.point.y + footColliderRadius;
        return fallbackY;
    }

    private Vector2 EvaluateArc(Vector2 startPos, Vector2 targetPos, float t, float heightPx)
    {
        float heightWorld = heightPx * pixelToWorld;
        float x = Mathf.Lerp(startPos.x, targetPos.x, t);
        float yBase = Mathf.Lerp(startPos.y, targetPos.y, t);
        float yArc = yBase + heightWorld * Mathf.Sin(Mathf.PI * t);
        return new Vector2(x, yArc);
    }

    private bool IsWalkable(Vector2 normal) =>
        Vector2.Angle(normal, Vector2.up) <= config.maxWalkableAngle;

    private void CaptureLandingVelocity(FootData foot)
    {
        float speed = Mathf.Abs(foot.rb.linearVelocity.y);
        _capturedLandingSpeed = Mathf.Max(_capturedLandingSpeed, speed);
        _landingCaptureTimer = config.landingCaptureWindow;
        _landingReady = false;
    }

    private void ProbeLockedFootGround(FootData foot)
    {
        if (foot.state != FootState.Locked) return;

        float probeDistWU = config.groundProbeDistance * pixelToWorld;
        var hit = Physics2D.Raycast(
            foot.lockPosition, Vector2.down, probeDistWU, _notPlayerMask);

        // Require both probe miss AND no physics contact — prevents floating-point
        // precision at the surface boundary from spuriously dropping a grounded foot
        // to Airborne, which would re-arm idleCorrectionArmed and restart the step loop.
        if ((hit.collider == null || !IsWalkable(hit.normal)) && !foot.contact.isGrounded)
            TransitionToAirborne(foot);
    }
}
