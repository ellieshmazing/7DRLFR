using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Root controller for the player skeleton.
///
/// FixedUpdate responsibilities (runs at -10, after PlayerHipNode at -15):
///   1. Read isGrounded (any foot locked, no grace) and canJump (includes coyote time).
///   2. Apply WASD horizontal forces — foot-gated: full force when grounded,
///      reduced by airControlRatio when airborne. Soft speed cap with quadratic
///      falloff near maxSpeed; direction-reversal boost on ground.
///   3. Lerp linearDamping between groundDamping and airDamping at
///      dampingTransitionSpeed — smooth landing transitions, no jarring brake.
///   4. Crouch: hold down while isGrounded to compress standHeight. The hip
///      naturally lowers, loading energy for the jump offset system.
///      Crouch decays smoothly (never snaps) to avoid torso visual pop.
///   5. Torso Y constraint: velocity-override to hipNode.Y + effectiveStandHeight.
///   6. Hip X lock: hipNode.X = torso.X.
///   7. Variable jump height: releasing space while ascending cuts Y velocity
///      on feet and hip. X is preserved (the "hop dash" mechanic).
///   8. Wall slide: when feet touch a wall and the player holds toward it while
///      descending, clamp downward velocity and signal FootMovement to reduce
///      foot gravity.
///   9. Jump: directional (velocity-based lean with deadzone), weight-scaled,
///      coyote-time-aware, input-buffered. Jump velocity is set directly on
///      feet (Celeste-style) — no mass division, no impulse/velocity confusion.
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
    [Tooltip("Live config SO — all player params read per-frame")]
    public PlayerConfig config;

    [Tooltip("Pixel-to-world conversion factor, cached at spawn")]
    public float pixelToWorld;

    [Header("Wiring (set by PlayerAssembler)")]
    public Transform     hipNode;
    public PlayerHipNode hipNodeScript;
    public FootMovement  footMovement;
    public Rigidbody2D   leftFootRB;
    public Rigidbody2D   rightFootRB;
    public FootContact   leftFootContact;
    public FootContact   rightFootContact;

    private Rigidbody2D rb;

    // --- Jump input state ---
    private float _jumpBufferTimer;
    private bool  _jumpHeld;
    private bool  _jumpHeldLastFrame;
    private bool  _jumpedThisPress;

    // --- Crouch ---
    private float _crouchAmount; // in source pixels (0 to maxCrouchDepth)

    // --- Damping lerp ---
    private float _currentDamping;

    // --- Weight ---
    private float _currentAmmoWeight;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
    }

    void OnDestroy()
    {
        PlayerRegistry.Unregister(transform);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Buffer jump input — captured in Update for frame-perfect response.
        if (kb.spaceKey.wasPressedThisFrame)
        {
            _jumpBufferTimer = config != null ? config.jumpBufferTime : 0.1f;
            _jumpHeld = true;
            _jumpedThisPress = false;
        }

        if (kb.spaceKey.wasReleasedThisFrame)
            _jumpHeld = false;

        // Decay buffer in Update for framerate-responsive input.
        _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (config == null) return;

        // Read tunable values from config SO each frame
        float moveForce         = config.moveForce;
        float maxSpeed          = config.maxSpeed;
        float standHeight       = config.standHeight;
        float jumpSpeed         = config.jumpSpeed;
        float jumpOffsetFactor  = config.jumpOffsetFactor;
        float airControlRatio   = config.airControlRatio;
        float groundDamping     = config.groundDamping;
        float airDamping        = config.airDamping;
        float dt                = Time.fixedDeltaTime;

        // --- 1. Ground state (TWO SEPARATE CHECKS) ---
        bool isGrounded = footMovement != null && footMovement.IsGrounded();
        bool canJump    = footMovement != null && footMovement.CanJump();

        // --- 2. Horizontal movement — FOOT-GATED ---
        var kb = Keyboard.current;
        float h = kb != null
            ? (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
            - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f)
            : 0f;

        // --- 2b. Wall-obstruction force suppression ---
        if (h != 0f && footMovement != null && footMovement.IsMovementBlockedByWall(Mathf.Sign(h)))
            h = 0f;

        float effectiveForce = isGrounded ? moveForce : moveForce * airControlRatio;
        float hSpeed = Mathf.Abs(rb.linearVelocity.x);

        if (h != 0f)
        {
            bool sameDirection = Mathf.Sign(h) == Mathf.Sign(rb.linearVelocity.x)
                                 && hSpeed > 0.01f;

            if (!sameDirection || hSpeed < maxSpeed)
            {
                // Soft cap: quadratic falloff near maxSpeed.
                float speedRatio = sameDirection ? Mathf.Clamp01(hSpeed / maxSpeed) : 0f;
                float forceMult  = 1f - speedRatio * speedRatio;

                // Direction reversal boost when grounded.
                if (!sameDirection && isGrounded && hSpeed > 0.01f)
                    forceMult = config.turnBoostFactor;

                rb.AddForce(new Vector2(h, 0f) * effectiveForce * forceMult);
            }
        }

        if (hipNode == null) return;

        // --- 3. Damping — lerped transition ---
        float targetDamping = isGrounded ? groundDamping : airDamping;
        _currentDamping = Mathf.MoveTowards(_currentDamping, targetDamping,
            config.dampingTransitionSpeed * dt);
        rb.linearDamping = _currentDamping;

        // --- 4. Crouch (gated on isGrounded, NOT canJump) ---
        bool downHeld = kb != null &&
            (kb.sKey.isPressed || kb.downArrowKey.isPressed);

        if (isGrounded && downHeld)
        {
            _crouchAmount = Mathf.MoveTowards(_crouchAmount, config.maxCrouchDepth,
                config.crouchSpeed * dt);
        }
        else
        {
            // Release at 2x compress speed — smooth, never snaps.
            _crouchAmount = Mathf.MoveTowards(_crouchAmount, 0f,
                config.crouchSpeed * 2f * dt);
        }

        // --- 5. Body leash — constrain torso X to feet ---
        ApplyBodyLeash();

        // --- 6. Torso Y: converge to hipNode.Y + effectiveStandHeight ---
        float effectiveStandHeight = (standHeight - _crouchAmount) * pixelToWorld;
        float desiredY    = hipNode.position.y + effectiveStandHeight;
        float yCorrection = (desiredY - rb.position.y) / dt;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, yCorrection);

        // --- 6. Lock hipNode X directly below the torso ---
        hipNode.position = new Vector3(rb.position.x, hipNode.position.y, 0f);

        // --- 7. Variable jump height — on release ---
        // Short hops preserve full X momentum — the "hop dash" mechanic.
        if (_jumpHeldLastFrame && !_jumpHeld)
        {
            if (leftFootRB != null && leftFootRB.linearVelocity.y > 0f)
                ApplyJumpCut(config.variableJumpCutMultiplier);
        }
        _jumpHeldLastFrame = _jumpHeld;

        // --- 8. Wall slide ---
        UpdateWallSlide(h);

        // --- 9. Jump ---
        TryJump(jumpSpeed, jumpOffsetFactor, canJump);
    }

    // -------------------------------------------------------------------------
    // Jump
    // -------------------------------------------------------------------------

    void TryJump(float jumpSpeed, float jumpOffsetFactor, bool canJump)
    {
        if (_jumpBufferTimer <= 0f) return;
        if (_jumpedThisPress) return;
        if (leftFootRB == null || rightFootRB == null) return;
        if (footMovement == null || !canJump) return;

        _jumpBufferTimer = 0f;
        _jumpedThisPress = true;

        // Hip compression: positive when hip is below lowest locked foot.
        float lowestFootY = footMovement.GetLockedFootY();
        float hipOffset   = Mathf.Max(0f, lowestFootY - hipNode.position.y);

        // Total compression includes intentional crouch.
        float totalHipCompression = hipOffset + _crouchAmount * pixelToWorld;

        // Jump magnitude (velocity, wu/s) with weight scaling.
        // baseTorsoMass / rb.mass: heavier = lower jump. At base weight, no-op.
        float jumpMagnitude = jumpSpeed + totalHipCompression * jumpOffsetFactor;
        jumpMagnitude *= config.baseTorsoMass / rb.mass;

        // Directional jump vector with deadzone.
        float hVel = rb.linearVelocity.x;
        if (Mathf.Abs(hVel) < config.directionalJumpDeadzone)
            hVel = 0f;

        float forwardComponent = Mathf.Abs(hVel) * config.forwardJumpFactor;
        Vector2 jumpDir = new Vector2(
            Mathf.Sign(hVel) * forwardComponent,
            1f).normalized;

        // jumpMagnitude is a velocity — set directly, no mass division.
        Vector2 jumpVelocity = jumpDir * jumpMagnitude;

        // IMPORTANT: Call OnJump BEFORE setting velocities.
        // Transitions feet to Airborne, clears coyote timer, starts coast timer.
        footMovement.OnJump();

        // Apply to feet (velocity-SET, Celeste-style).
        leftFootRB.linearVelocity  = jumpVelocity;
        rightFootRB.linearVelocity = jumpVelocity;

        // Apply Y to hip (velocity, not impulse — consistent units).
        if (hipNodeScript != null)
            hipNodeScript.ApplyJumpImpulse(jumpVelocity.y);

        // Apply X boost to torso (the forward component of the leap).
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + jumpVelocity.x,
            rb.linearVelocity.y);

        // Crouch decays naturally via the ELSE branch next frame — no snap.
    }

    void ApplyJumpCut(float multiplier)
    {
        // Cut Y velocity on feet. X is preserved (hop dash).
        if (leftFootRB.linearVelocity.y > 0f)
            leftFootRB.linearVelocity = new Vector2(
                leftFootRB.linearVelocity.x,
                leftFootRB.linearVelocity.y * multiplier);

        if (rightFootRB.linearVelocity.y > 0f)
            rightFootRB.linearVelocity = new Vector2(
                rightFootRB.linearVelocity.x,
                rightFootRB.linearVelocity.y * multiplier);

        // Cut hip Y velocity.
        if (hipNodeScript != null)
            hipNodeScript.ApplyJumpCut(multiplier);
    }

    // -------------------------------------------------------------------------
    // Wall Slide
    // -------------------------------------------------------------------------

    void UpdateWallSlide(float hInput)
    {
        if (footMovement == null || leftFootContact == null || rightFootContact == null)
        {
            footMovement?.SetWallSliding(false);
            return;
        }

        bool touchingWall = leftFootContact.isWalled || rightFootContact.isWalled;
        bool descending   = rb.linearVelocity.y < 0f;

        // holdingTowardWall: player presses into the wall.
        bool holdingTowardWall = false;
        if (hInput != 0f)
        {
            if (leftFootContact.isWalled)
                holdingTowardWall = hInput * leftFootContact.lastWallNormal.x < 0f;
            else if (rightFootContact.isWalled)
                holdingTowardWall = hInput * rightFootContact.lastWallNormal.x < 0f;
        }

        bool sliding = touchingWall && descending && holdingTowardWall;

        if (sliding)
        {
            // Clamp downward velocity to max wall slide speed.
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                Mathf.Max(rb.linearVelocity.y, -config.maxWallSlideSpeed));

            footMovement.SetWallSliding(true);
        }
        else
        {
            footMovement.SetWallSliding(false);
        }
    }

    // -------------------------------------------------------------------------
    // Body Leash
    // -------------------------------------------------------------------------

    void ApplyBodyLeash()
    {
        if (footMovement == null) return;

        float footCenterX   = footMovement.GetFootCenterX();
        float displacement  = rb.position.x - footCenterX;
        float softRadiusWU  = config.leashSoftRadius * pixelToWorld;
        float hardRadiusWU  = config.leashHardRadius * pixelToWorld;

        // Quadratic spring pull in the soft→hard zone.
        if (Mathf.Abs(displacement) > softRadiusWU)
        {
            float excess    = Mathf.Abs(displacement) - softRadiusWU;
            float softRange = hardRadiusWU - softRadiusWU;
            if (softRange > 0f)
            {
                float t         = Mathf.Clamp01(excess / softRange);
                float pullForce = config.moveForce * config.leashForceMult * t * t;
                rb.AddForce(new Vector2(-Mathf.Sign(displacement) * pullForce, 0f));
            }
        }

        // Hard clamp — safety net.
        if (Mathf.Abs(displacement) > hardRadiusWU)
        {
            float clampedX = footCenterX + Mathf.Sign(displacement) * hardRadiusWU;
            rb.position = new Vector2(clampedX, rb.position.y);

            if (Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(displacement))
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    // -------------------------------------------------------------------------
    // Weight API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the current ammo weight. Call when ammo count changes.
    /// Adjusts torso RB mass so physics naturally responds:
    /// heavier = slower acceleration (F=ma), lower jumps (velocity / mass scaling).
    /// Springs keep their feel (frequency/dampingRatio parameterization).
    /// </summary>
    public void SetAmmoWeight(float totalAmmoWeight)
    {
        _currentAmmoWeight = totalAmmoWeight;
        if (config != null)
            rb.mass = config.baseTorsoMass + totalAmmoWeight;
    }

    /// <summary>Current total ammo weight (read-only).</summary>
    public float CurrentAmmoWeight => _currentAmmoWeight;
}
