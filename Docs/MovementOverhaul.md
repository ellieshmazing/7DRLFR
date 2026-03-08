# Movement Overhaul — Design Specification (Rev 2)

## Mission Statement

> **Movement in BallWorld is a conversation between the player and the ground.** Every platform is a promise: if you can reach it, you can stand on it. The feet are the interface — they grip, push, launch, and land with weight the player can feel and predict. Mastery comes from understanding the body: how momentum builds through footfalls, how a crouch loads a spring, how the arc of a leap follows the push of a stride. The character doesn't move because you pressed a button — it moves because its feet found purchase and pushed.

### Design Pillars

1. **Feet are the truth.** The visual state of the feet IS the mechanical state. Feet on ground = full control. Feet in air = committed to your arc. The player reads the feet to understand the system.
2. **Predictable physics, emergent depth.** Same inputs → same outputs. The depth comes from how systems compose (momentum + crouch + timing), not from hidden complexity.
3. **Weight you can feel.** The character has mass that matters. Ammo adds weight. Crouching loads energy. Every jump is a negotiation between the force you store and the mass you carry.
4. **Forgiveness at the edges, precision at the core.** Coyote time and jump buffering catch honest mistakes. But the core movement rewards players who understand the rhythm of footfalls and the geometry of leaps.

---

## Current System Analysis

### What works (keep):
- Skeleton/visual paradigm (nodes position, visuals spring-chase)
- Spring parameterization (frequency/dampingRatio → computed stiffness/damping)
- Execution order chain: FootMovement(-20) → PlayerHipNode(-15) → PlayerSkeletonRoot(-10)
- FootMovement's three-state FSM (Locked/Stepping/Airborne)
- Step targeting with velocity projection
- Hip spring tracking lowest foot Y
- Assembler pattern (inactive during construction)

### What's broken:
1. **Torso-driven, not foot-driven** — WASD applies force to torso regardless of foot state. Feet are decorative.
2. **Jump is purely vertical** — Y velocity only. Running + jumping feels like hopping sideways, not leaping.
3. **No ground/air distinction** — Same damping, same force, same control in air and on ground. linearDamping=3 kills all air momentum.
4. **No forgiveness systems** — No coyote time, no jump buffering. Miss by 1 frame, miss the jump.
5. **No variable jump height** — Hold or tap space → same jump. No expression in the input.
6. **No wall/obstacle interaction** — No sliding, no realistic collision response, no knockback integration.
7. **No weight system** — All movement parameters are static.
8. **Hip crouch is accidental** — Only happens from landing bounce, not from player intent. Can't be mastered.
9. **maxSpeed checks magnitude** — High Y velocity blocks horizontal force application during falls.

---

## Architecture Overview

### Critical Design Distinction: `isGrounded` vs `canJump`

Two separate flags, NOT interchangeable (critique #20):

- **`isGrounded`** = any foot currently in Locked state. No grace period. Used for:
  - Force gating (ground vs air control)
  - Damping selection (ground vs air)
  - Crouch eligibility
  - Foot damping management

- **`canJump`** = `isGrounded` OR within coyote time window. Used ONLY for:
  - Jump eligibility check

This separation ensures coyote time extends the jump window without granting full ground control while airborne.

### Data Flow (revised)

```
WASD Input
    ↓
PlayerSkeletonRoot.FixedUpdate (order -10)
    ├→ isGrounded = footMovement.IsGrounded()   (any foot locked, no grace)
    ├→ canJump = footMovement.CanJump()         (includes coyote time)
    ├→ Apply moveForce (full if isGrounded, reduced if airborne)
    ├→ Lerp linearDamping toward target (groundDamping or airDamping)
    ├→ Speed cap on horizontal only (soft cap near maxSpeed)
    ├→ Torso Y = HipNode.Y + effectiveStandHeight
    ├→ Hip X lock
    ├→ Jump logic:
    │   ├→ Buffer jump input (jumpBufferTime)
    │   ├→ Check canJump (includes coyoteTime)
    │   ├→ Compute directional jump vector (with deadzone)
    │   ├→ Weight scaling on jump magnitude
    │   ├→ Variable jump height (on release)
    │   └→ Apply to feet + hip + torso
    └→ Crouch logic (hold down while isGrounded — NOT canJump)

FootMovement.FixedUpdate (order -20)
    ├→ Per-foot state machine (unchanged core)
    ├→ Grounded/coyote time tracking
    ├→ Footfall impulse on lock-from-step (speed-gated)
    ├→ Landing velocity tolerance
    ├→ Wall sliding foot gravity management
    ├→ Foot RB damping management (ground vs air)
    └→ IsGrounded() / CanJump() / OnJump()

PlayerHipNode.FixedUpdate (order -15)
    └→ hipY spring toward GetGroundReferenceY()
```

### File Changes Summary

| File | Changes |
|------|---------|
| `PlayerSkeletonRoot.cs` | Foot-gated forces, ground/air damping (lerped), directional jump (with deadzone + weight scaling), variable jump height, jump buffering, crouch input, wall slide, weight API |
| `FootMovement.cs` | `IsGrounded()` + `CanJump()` separation, coyote time (cleared on jump), footfall impulse (speed-gated), landing tolerance, wall slide gravity, foot damping management, jump coast timer |
| `PlayerHipNode.cs` | `ApplyJumpCut()` method |
| `FootContact.cs` | Per-frame wall detection via `OnCollisionStay2D` |
| `PlayerConfig.cs` | ~18 new variables for all new systems |
| `PlayerAssembler.cs` | Wire baseTorsoMass, compute footColliderRadius from collider, set initial damping |

---

## Detailed Function Designs

### PlayerSkeletonRoot.cs — Full Revised Design

```
FIELDS (new):
  _jumpBufferTimer    : float     — time remaining on jump buffer
  _jumpHeld           : bool      — is jump button currently held
  _jumpedThisPress    : bool      — has this press already triggered a jump
  _crouchAmount       : float     — current crouch depth (0 to maxCrouchDepth), in source pixels
  _currentDamping     : float     — current lerped damping value
  _currentAmmoWeight  : float     — set by external systems via SetAmmoWeight()

Update():
  var kb = Keyboard.current
  IF kb != null AND kb.spaceKey.wasPressedThisFrame:
    _jumpBufferTimer = config.jumpBufferTime
    _jumpHeld = true
    _jumpedThisPress = false

  IF kb != null AND kb.spaceKey.wasReleasedThisFrame:
    _jumpHeld = false

  // Decay buffer in Update for framerate-responsive input
  _jumpBufferTimer = Max(0, _jumpBufferTimer - Time.deltaTime)

FixedUpdate():
  IF config == null: return

  // Read tunable values per-frame
  float moveForce       = config.moveForce
  float maxSpeed        = config.maxSpeed
  float standHeight     = config.standHeight * pixelToWorld
  float jumpSpeed       = config.jumpSpeed
  float jumpOffsetFactor = config.jumpOffsetFactor
  float airControlRatio = config.airControlRatio
  float groundDamping   = config.groundDamping
  float airDamping      = config.airDamping
  float dt              = Time.fixedDeltaTime

  // -- 1. Ground state (TWO SEPARATE CHECKS) --
  bool isGrounded = footMovement.IsGrounded()  // any foot locked, no grace
  bool canJump    = footMovement.CanJump()     // includes coyote time

  // -- 2. Horizontal movement — FOOT-GATED --
  float h = read WASD horizontal input
  float effectiveForce = isGrounded ? moveForce : moveForce * airControlRatio
  float hSpeed = Abs(rb.linearVelocity.x)

  // Soft speed cap: force scales down near maxSpeed (critique #8)
  IF h != 0:
    bool sameDirection = Sign(h) == Sign(rb.linearVelocity.x)
    IF NOT sameDirection OR hSpeed < maxSpeed:
      float speedRatio = sameDirection ? Clamp01(hSpeed / maxSpeed) : 0
      float forceMult = 1 - speedRatio * speedRatio  // quadratic falloff
      IF NOT sameDirection AND isGrounded:
        forceMult = config.turnBoostFactor  // boost direction changes on ground
      rb.AddForce(Vector2(h, 0) * effectiveForce * forceMult)

  // -- 3. Damping — lerped transition (critique #9) --
  float targetDamping = isGrounded ? groundDamping : airDamping
  _currentDamping = MoveToward(_currentDamping, targetDamping,
    config.dampingTransitionSpeed * dt)
  rb.linearDamping = _currentDamping

  // -- 4. Crouch (gated on isGrounded, NOT canJump — critique #14) --
  bool downHeld = Keyboard.current?.sKey.isPressed == true
                  || Keyboard.current?.downArrowKey.isPressed == true
  IF isGrounded AND downHeld:
    _crouchAmount = MoveToward(_crouchAmount, config.maxCrouchDepth,
      config.crouchSpeed * dt)
  ELSE:
    // Release faster than compress; does NOT snap on jump (critique #2)
    _crouchAmount = MoveToward(_crouchAmount, 0, config.crouchSpeed * 2 * dt)

  // -- 5. Torso Y constraint (with crouch) --
  float effectiveStandHeight = (config.standHeight - _crouchAmount) * pixelToWorld
  float desiredY = hipNode.position.y + effectiveStandHeight
  float yCorrection = (desiredY - rb.position.y) / dt
  rb.linearVelocity = Vector2(rb.linearVelocity.x, yCorrection)

  // -- 6. Hip X lock (unchanged) --
  hipNode.position = Vector3(rb.position.x, hipNode.position.y, 0)

  // -- 7. Variable jump height — on release (critique #5: intentional design) --
  // Short hops preserve full horizontal momentum — this is the "hop dash",
  // an intended mechanic that rewards mastery of jump release timing.
  IF _jumpHeldLastFrame AND NOT _jumpHeld:
    IF leftFootRB.linearVelocity.y > 0:
      ApplyJumpCut(config.variableJumpCutMultiplier)
  _jumpHeldLastFrame = _jumpHeld

  // -- 8. Wall slide --
  UpdateWallSlide(h)

  // -- 9. Jump --
  TryJump(jumpSpeed, jumpOffsetFactor, canJump)

TryJump(jumpSpeed, jumpOffsetFactor, canJump):
  IF _jumpBufferTimer <= 0: return
  IF _jumpedThisPress: return
  IF NOT canJump: return

  _jumpBufferTimer = 0
  _jumpedThisPress = true

  // Hip compression
  float lowestFootY = footMovement.GetLockedFootY()
  float hipOffset = Max(0, lowestFootY - hipNode.position.y)
  float totalHipCompression = hipOffset + _crouchAmount * pixelToWorld

  // Jump magnitude with weight scaling (critique #7)
  float jumpMagnitude = jumpSpeed + totalHipCompression * jumpOffsetFactor
  jumpMagnitude *= config.baseTorsoMass / rb.mass  // heavier = lower jump

  // Directional jump vector with deadzone (critique #24)
  float hVel = rb.linearVelocity.x
  IF Abs(hVel) < config.directionalJumpDeadzone:
    hVel = 0
  float forwardComponent = Abs(hVel) * config.forwardJumpFactor
  Vector2 jumpDir = Normalize(Vector2(Sign(hVel) * forwardComponent, 1))

  // jumpMagnitude is a VELOCITY magnitude (critique #3)
  // Set directly on feet — no mass division. Feet velocity = jump velocity.
  Vector2 jumpVelocity = jumpDir * jumpMagnitude

  // IMPORTANT: Call OnJump BEFORE setting velocities (critique #25)
  // This transitions feet to Airborne first, then we set their velocity.
  footMovement.OnJump()

  // Apply to feet (velocity-SET, Celeste-style)
  leftFootRB.linearVelocity = jumpVelocity
  rightFootRB.linearVelocity = jumpVelocity

  // Apply Y to hip (velocity, not impulse)
  hipNodeScript.ApplyJumpImpulse(jumpVelocity.y)

  // Apply X boost to torso (the forward component of the leap)
  // Also divided by mass for consistency (critique #3)
  rb.linearVelocity = Vector2(
    rb.linearVelocity.x + jumpVelocity.x,
    rb.linearVelocity.y)

  // Crouch amount does NOT snap to 0 — it decays naturally via the
  // ELSE branch of the crouch logic next frame (critique #2)

UpdateWallSlide(h):
  bool touchingWall = leftFootContact.isWalled || rightFootContact.isWalled
  bool descending = rb.linearVelocity.y < 0
  // holdingTowardWall: input direction pushes into the wall
  bool holdingTowardWall = false
  IF leftFootContact.isWalled:
    holdingTowardWall = h * leftFootContact.lastWallNormal.x < 0
  ELSE IF rightFootContact.isWalled:
    holdingTowardWall = h * rightFootContact.lastWallNormal.x < 0

  bool sliding = touchingWall AND descending AND holdingTowardWall

  IF sliding:
    // Clamp downward velocity to max wall slide speed
    rb.linearVelocity = Vector2(rb.linearVelocity.x,
      Max(rb.linearVelocity.y, -config.maxWallSlideSpeed))
    footMovement.SetWallSliding(true)
  ELSE:
    footMovement.SetWallSliding(false)

ApplyJumpCut(multiplier):
  // Cut upward Y velocity on feet and hip. X is preserved (hop dash).
  IF leftFootRB.linearVelocity.y > 0:
    leftFootRB.linearVelocity = new Vector2(
      leftFootRB.linearVelocity.x,
      leftFootRB.linearVelocity.y * multiplier)
  IF rightFootRB.linearVelocity.y > 0:
    rightFootRB.linearVelocity = new Vector2(
      rightFootRB.linearVelocity.x,
      rightFootRB.linearVelocity.y * multiplier)
  hipNodeScript.ApplyJumpCut(multiplier)

// -- Weight API --
public void SetAmmoWeight(float totalAmmoWeight):
  _currentAmmoWeight = totalAmmoWeight
  rb.mass = config.baseTorsoMass + totalAmmoWeight
```

### FootMovement.cs — Full Revised Changes

```
NEW FIELDS:
  _lastGroundedTime   : float = -999    — Time.time when any foot was last Locked
  _isWallSliding      : bool = false    — set by PlayerSkeletonRoot
  _jumpCoastTimer     : float = 0       — suppresses airborne X spring after jump

NEW PUBLIC METHODS:
  // Returns true if any foot is currently Locked (no grace period)
  bool IsGrounded():
    return _left.state == Locked || _right.state == Locked

  // Returns true if grounded OR within coyote window
  bool CanJump():
    IF IsGrounded(): return true
    IF Time.time - _lastGroundedTime <= config.coyoteTime: return true
    return false

  // Sets wall sliding state (called by PlayerSkeletonRoot)
  void SetWallSliding(bool sliding):
    _isWallSliding = sliding

MODIFIED OnJump():
  // Clear coyote time to prevent ghost double-jump (critique #4)
  _lastGroundedTime = -999f
  // Start jump coast timer to suppress foot X spring (critique #1)
  _jumpCoastTimer = config.jumpCoastTime
  // Transition feet to airborne
  TransitionToAirborne(_left)
  TransitionToAirborne(_right)

MODIFIED FixedUpdate():
  // ... existing prologue ...

  // Track grounded state for coyote time
  bool anyLocked = (_left.state == Locked || _right.state == Locked)
  IF anyLocked:
    _lastGroundedTime = Time.time

  // Decay jump coast timer
  _jumpCoastTimer = Max(0, _jumpCoastTimer - Time.fixedDeltaTime)

  // Manage foot RB damping based on state (critique #21)
  float footDamp = anyLocked ? config.footGroundDamping : config.footAirDamping
  _left.rb.linearDamping = footDamp
  _right.rb.linearDamping = footDamp

  // ... existing foot update logic ...

  // Assert no simultaneous stepping (critique #23)
  Debug.Assert(!(_left.state == Stepping && _right.state == Stepping),
    "FootMovement: both feet stepping simultaneously!")

MODIFIED UpdateAirborneX():
  // During jump coast period, suppress X spring (critique #1)
  IF _jumpCoastTimer > 0:
    // Let foot coast freely — no spring force on X
    return  // (Y spring still applies in the main Airborne case)

  // ... existing spring logic unchanged ...

MODIFIED Airborne landing check:
  // Tolerance for apex landing (critique: reliable foot locking)
  IF foot.contact.isGrounded
     AND IsWalkable(foot.contact.lastContactNormal)
     AND foot.rb.linearVelocity.y < config.landingVelocityTolerance:
    LockFoot(foot, foot.rb.position)

MODIFIED Airborne gravity:
  // Use wall slide gravity when sliding (critique #6)
  foot.rb.gravityScale = _isWallSliding
    ? config.wallSlideFootGravityScale
    : config.footGravityScale

MODIFIED LockFoot(foot, worldPos):
  FootState previousState = foot.state   // capture before changing
  foot.state = Locked
  foot.lockPosition = worldPos
  foot.rb.position = worldPos
  foot.rb.linearVelocity = Vector2.zero
  foot.rb.gravityScale = 0
  foot.xLocked = false

  // Footfall impulse: only on step→lock, speed-gated (critiques #13, #22)
  IF previousState == Stepping
     AND config.footfallImpulse > 0
     AND torsoRB != null:
    float hSpeed = Abs(torsoRB.linearVelocity.x)
    IF hSpeed > config.footfallMinSpeed AND hSpeed < config.maxSpeed:
      float dir = Sign(torsoRB.linearVelocity.x)
      // Scale impulse down near maxSpeed to prevent runaway (critique #13)
      float speedScale = Clamp01(1 - hSpeed / config.maxSpeed)
      torsoRB.AddForce(Vector2(dir * config.footfallImpulse * speedScale, 0),
        ForceMode2D.Impulse)
```

### FootContact.cs — Revised Wall Detection (Per-Frame Approach)

```
// Use OnCollisionStay2D for wall detection (critique #12)
// No counters for walls — just check normals each physics frame.

PUBLIC FIELDS:
  bool isGrounded => _contactCount > 0
  Vector2 lastContactNormal { get; private set; }
  bool isWalled { get; private set; }
  Vector2 lastWallNormal { get; private set; }

PRIVATE FIELDS:
  int _contactCount = 0
  bool _walledThisFrame = false

// Ground contacts still use enter/exit counter (proven reliable)
void OnCollisionEnter2D(Collision2D col):
  _contactCount++
  lastContactNormal = col.GetContact(0).normal

void OnCollisionStay2D(Collision2D col):
  Vector2 normal = col.GetContact(0).normal
  float angle = Vector2.Angle(normal, Vector2.up)
  // Update ground normal
  lastContactNormal = normal
  // Wall detection: any non-walkable contact counts
  // maxWalkableAngle is not accessible here — use a fixed threshold
  // or receive it from config. Use 50 degrees as built-in default.
  IF angle > 50f:  // steeper than walkable = wall
    _walledThisFrame = true
    lastWallNormal = normal

void OnCollisionExit2D(Collision2D _):
  _contactCount = Max(0, _contactCount - 1)

void FixedUpdate():
  // Per-frame wall detection: set from Stay, reset each frame
  isWalled = _walledThisFrame
  _walledThisFrame = false
```

### PlayerHipNode.cs — Jump Cut Support

```
NEW METHOD:
public void ApplyJumpCut(float multiplier):
  IF hipVelocityY > 0:
    hipVelocityY *= multiplier
```

### PlayerAssembler.cs — Changes

```
In Spawn():
  // Set initial torso mass from config
  torsoRB.mass = config.baseTorsoMass

  // Set linearDamping to ground value initially
  torsoRB.linearDamping = config.groundDamping

  // Calculate footColliderRadius from actual collider (critique #11)
  // After creating foot visuals:
  float actualFootRadius = col.radius * go.transform.lossyScale.x
  footMovement.footColliderRadius = actualFootRadius
```

### PlayerConfig.cs — All New Variables

```csharp
// ═══════════════════════════════════════════════════════
// GROUND VS AIR CONTROL
// ═══════════════════════════════════════════════════════

[Header("Ground vs Air Control")]
[Tooltip("Force multiplier when airborne (0 = no air control, 1 = full). try 0.15–0.4")]
[Range(0f, 1f)] public float airControlRatio = 0.25f;

[Tooltip("Linear damping when grounded. Higher = quicker stops. try 3–8")]
[Min(0f)] public float groundDamping = 5f;

[Tooltip("Linear damping when airborne. Lower = more momentum preservation. try 0.3–1.5")]
[Min(0f)] public float airDamping = 0.5f;

[Tooltip("How fast damping transitions between ground/air values (wu/s). " +
         "Prevents jarring speed loss on landing. try 10–30")]
[Min(1f)] public float dampingTransitionSpeed = 15f;

[Tooltip("Force multiplier for direction reversal at max speed. try 1.0–2.0")]
[Min(0f)] public float turnBoostFactor = 1.5f;

// ═══════════════════════════════════════════════════════
// JUMP — DIRECTIONAL
// ═══════════════════════════════════════════════════════

[Header("Jump — Directional")]
[Tooltip("How much horizontal speed contributes to jump direction. " +
         "0 = always vertical. try 0.05–0.3")]
[Min(0f)] public float forwardJumpFactor = 0.15f;

[Tooltip("Horizontal speed below which jumps are perfectly vertical. " +
         "Prevents accidental lean at low speeds. try 0.3–0.8")]
[Min(0f)] public float directionalJumpDeadzone = 0.5f;

// ═══════════════════════════════════════════════════════
// JUMP — VARIABLE HEIGHT
// ═══════════════════════════════════════════════════════

[Header("Jump — Variable Height")]
[Tooltip("Y velocity multiplier when jump is released early. " +
         "Lower = more height control. X is preserved (hop dash). try 0.3–0.6")]
[Range(0f, 1f)] public float variableJumpCutMultiplier = 0.45f;

// ═══════════════════════════════════════════════════════
// JUMP — FORGIVENESS
// ═══════════════════════════════════════════════════════

[Header("Jump — Forgiveness")]
[Tooltip("Seconds after leaving ground where jump is still valid. try 0.06–0.15")]
[Min(0f)] public float coyoteTime = 0.1f;

[Tooltip("Seconds before landing where a jump input is buffered. try 0.06–0.12")]
[Min(0f)] public float jumpBufferTime = 0.1f;

// ═══════════════════════════════════════════════════════
// JUMP — LANDING
// ═══════════════════════════════════════════════════════

[Header("Jump — Landing")]
[Tooltip("Max upward velocity at which feet can still lock to ground " +
         "(apex tolerance). try 0.1–0.5")]
[Min(0f)] public float landingVelocityTolerance = 0.3f;

[Tooltip("Seconds after a jump where foot X spring is suppressed, " +
         "letting feet carry launch momentum visually. try 0.04–0.1")]
[Min(0f)] public float jumpCoastTime = 0.06f;

// ═══════════════════════════════════════════════════════
// CROUCH
// ═══════════════════════════════════════════════════════

[Header("Crouch")]
[Tooltip("Max crouch depth in source pixels. try 3–8")]
[Min(0f)] public float maxCrouchDepth = 5f;

[Tooltip("Speed of crouch compression in px/s. Release is 2x this. try 15–40")]
[Min(0.1f)] public float crouchSpeed = 25f;

// ═══════════════════════════════════════════════════════
// FOOTFALL
// ═══════════════════════════════════════════════════════

[Header("Footfall")]
[Tooltip("Forward impulse applied to torso each time a foot locks from stepping. " +
         "Scaled down near maxSpeed to prevent runaway. try 0.1–1.0")]
[Min(0f)] public float footfallImpulse = 0.3f;

[Tooltip("Minimum horizontal speed for footfall impulse to apply. try 0.3–1.0")]
[Min(0f)] public float footfallMinSpeed = 0.3f;

// ═══════════════════════════════════════════════════════
// FOOT DAMPING
// ═══════════════════════════════════════════════════════

[Header("Foot RB Damping")]
[Tooltip("Foot linear damping when any foot is grounded. try 2–6")]
[Min(0f)] public float footGroundDamping = 4f;

[Tooltip("Foot linear damping when airborne. Lower = less spring fighting. try 0.1–1.0")]
[Min(0f)] public float footAirDamping = 0.3f;

// ═══════════════════════════════════════════════════════
// WEIGHT
// ═══════════════════════════════════════════════════════

[Header("Weight")]
[Tooltip("Base torso RB mass (no ammo). try 1–3")]
[Min(0.1f)] public float baseTorsoMass = 1f;

[Tooltip("Mass added per unit of ammo. try 0.01–0.1")]
[Min(0f)] public float ammoWeightPerUnit = 0.02f;

// ═══════════════════════════════════════════════════════
// WALL INTERACTION
// ═══════════════════════════════════════════════════════

[Header("Wall Interaction")]
[Tooltip("Max downward speed during wall slide. try 1.0–3.0")]
[Min(0f)] public float maxWallSlideSpeed = 2f;

[Tooltip("Foot gravity scale during wall slide. " +
         "Must match torso wall slide behavior to prevent separation. try 0.3–0.7")]
[Min(0f)] public float wallSlideFootGravityScale = 0.5f;
```

---

## Emergent Behaviors

These behaviors emerge naturally from the systems above, without additional code:

1. **Sprint-leap**: Running at max speed + jump → forward leap due to forwardJumpFactor. The faster you run, the more horizontal the arc. Players learn to time speed for gap-crossing.

2. **Crouch-launch**: Hold down → crouch → jump → powered vertical leap. Crouch decays naturally (not snapped), creating a smooth "uncoil" visual. Combined with sprint, this becomes a long-distance leap.

3. **Hop dash**: Releasing jump early cuts Y velocity but preserves full X momentum. Quick jump-release while sprinting = a low, fast horizontal dash. Named intentionally as a skill-expression mechanic.

4. **Momentum commitment**: Low air damping means jumps are committal. Air control nudges but can't reverse. Damping transitions smoothly on landing — the player gets a brief "slide" before ground friction takes hold.

5. **Weight management**: Firing ammo makes you lighter → higher jumps, faster movement. Running out of ammo has a silver lining. Heavy characters resist knockback more (F=ma).

6. **Footfall rhythm**: At certain speeds, the stepping cadence creates a rhythm of impulses. The impulse scales down near maxSpeed (no runaway). Players feel the "gallop."

7. **Ledge-catch reliability**: Landing velocity tolerance means apex landings work. Coyote time catches near-misses on edges.

8. **Wall-stall**: Touching a wall while descending and holding toward it slows the fall. Feet and torso descend together (matched gravity reduction). Gives the player a moment to reassess.

9. **Knockback authenticity**: External forces apply to the torso. Foot system responds naturally (feet go airborne, air control kicks in). Heavier characters absorb more.

---

## Implementation Order

### Phase 1: Config & Foundation
1. Add ALL new config variables to `PlayerConfig.cs`
2. Add `IsGrounded()` and revise `CanJump()` in `FootMovement.cs`
3. Add `ApplyJumpCut()` to `PlayerHipNode.cs`
4. Revise `FootContact.cs` with per-frame wall detection
5. Update `PlayerAssembler.cs` (baseTorsoMass, damping, footColliderRadius)

### Phase 2: Core Movement Feel
6. Foot-gated movement in `PlayerSkeletonRoot.cs` (isGrounded force split)
7. Soft speed cap (quadratic falloff + turn boost)
8. Damping lerp (ground ↔ air with dampingTransitionSpeed)
9. Foot RB damping management in `FootMovement.cs`
10. Coyote time tracking in `FootMovement.cs`

### Phase 3: Jump Expression
11. Jump buffering in `PlayerSkeletonRoot.cs` (Update + FixedUpdate)
12. Directional jumping with deadzone and weight scaling
13. Variable jump height (release to cut Y, preserve X)
14. Jump coast timer in `FootMovement.cs` (suppress X spring)
15. Clear coyote time on jump (prevent ghost double-jump)

### Phase 4: Crouch & Footfall
16. Intentional crouch (hold down while isGrounded, smooth decay)
17. Footfall impulse in `LockFoot()` (speed-gated, scaled near max)

### Phase 5: Wall & Weight
18. Wall slide in `PlayerSkeletonRoot.cs` (velocity clamp + foot gravity)
19. `SetWallSliding()` in `FootMovement.cs`
20. `SetAmmoWeight()` weight API

### Phase 6: Cleanup
21. Update `VARIABLES.md`
22. Append to `LEARNING.md`

---

## Variables Table (all new additions)

| Variable | Type | Location | Default | Description | Affects |
|---|---|---|---|---|---|
| airControlRatio | float | PlayerConfig | 0.25 | Force multiplier when airborne | Horizontal air control |
| groundDamping | float | PlayerConfig | 5.0 | Linear damping when grounded | Stop/turn responsiveness |
| airDamping | float | PlayerConfig | 0.5 | Linear damping when airborne | Air momentum preservation |
| dampingTransitionSpeed | float | PlayerConfig | 15.0 | Damping lerp rate (wu/s) | Landing smoothness |
| turnBoostFactor | float | PlayerConfig | 1.5 | Force multiplier for direction reversal | Turn responsiveness |
| forwardJumpFactor | float | PlayerConfig | 0.15 | Horizontal vel → jump direction | Leap angle |
| directionalJumpDeadzone | float | PlayerConfig | 0.5 | Min speed for directional jump | Vertical hop reliability |
| variableJumpCutMultiplier | float | PlayerConfig | 0.45 | Y velocity cut on early release | Short hop height |
| coyoteTime | float | PlayerConfig | 0.1 | Post-ground jump grace period | Jump forgiveness |
| jumpBufferTime | float | PlayerConfig | 0.1 | Pre-landing jump buffer | Landing-jump forgiveness |
| landingVelocityTolerance | float | PlayerConfig | 0.3 | Max upward vel for foot lock | Apex landing reliability |
| jumpCoastTime | float | PlayerConfig | 0.06 | X spring suppression after jump | Visual launch coherence |
| maxCrouchDepth | float | PlayerConfig | 5.0 | Max crouch in source px | Crouch range |
| crouchSpeed | float | PlayerConfig | 25.0 | Crouch rate in px/s | Crouch responsiveness |
| footfallImpulse | float | PlayerConfig | 0.3 | Forward impulse per footfall | Step-driven acceleration |
| footfallMinSpeed | float | PlayerConfig | 0.3 | Min speed for footfall impulse | Footfall activation |
| footGroundDamping | float | PlayerConfig | 4.0 | Foot RB damping when grounded | Foot ground behavior |
| footAirDamping | float | PlayerConfig | 0.3 | Foot RB damping when airborne | Foot air responsiveness |
| baseTorsoMass | float | PlayerConfig | 1.0 | Base RB mass (no ammo) | Base movement feel |
| ammoWeightPerUnit | float | PlayerConfig | 0.02 | Mass per ammo unit | Weight scaling |
| maxWallSlideSpeed | float | PlayerConfig | 2.0 | Max downward speed on wall | Wall slide speed |
| wallSlideFootGravityScale | float | PlayerConfig | 0.5 | Foot gravity during wall slide | Foot/torso coherence |

---

## Implementation Notes

### Jump Velocity Is a Velocity, Not an Impulse (critique #3)
`jumpMagnitude` is a velocity magnitude in wu/s. It is set directly on feet: `foot.linearVelocity = jumpDir * jumpMagnitude`. No division by mass. The `baseTorsoMass / rb.mass` scaling reduces the velocity when heavy. This is dimensionally consistent across all body parts.

### Coyote Time Cleared on Jump (critique #4)
When `OnJump()` fires, `_lastGroundedTime` is set to `-999f`. This guarantees `CanJump()` returns false until a foot actually locks again. No ghost double-jumps possible.

### Crouch Does Not Snap (critique #2)
`_crouchAmount` is never set to 0 instantly. On jump, the crouch logic's ELSE branch handles the decay at `crouchSpeed * 2` rate. The torso rises smoothly over 2-4 frames. The jump impulse was already computed using the compressed state, so launch force is correct.

### Hop Dash Is Intentional (critique #5)
Variable jump height cuts Y velocity but preserves X. This means short hops while sprinting produce a low, fast horizontal dash. This is a named, intentional mechanic — not a bug.

### Foot X Spring Suppressed After Jump (critique #1)
`jumpCoastTime` (~60ms, 3 frames) suppresses the airborne X spring. Feet carry their launch velocity visually before the spring re-engages. Without this, the stiff foot spring (k≈60) would snap feet back under the torso immediately, breaking the visual.

### Wall Slide Foot Coherence (critique #6)
`SetWallSliding(true)` changes foot `gravityScale` to `wallSlideFootGravityScale`. Since the torso's downward velocity is also clamped to `maxWallSlideSpeed`, both body parts descend at similar rates. No visual separation.

### Weight Affects Jumps (critique #7)
`jumpMagnitude *= config.baseTorsoMass / rb.mass`. At base weight (1.0), this is a no-op. At 1.5x weight, jumps are 67% as high. Subtle but felt.

### footColliderRadius Derived From Truth (critique #11)
`PlayerAssembler` computes `footColliderRadius` from `col.radius * transform.lossyScale.x` instead of a parallel calculation. This stays correct if sprites or scale change.

### Execution Order Unchanged
The -20 → -15 → -10 chain still works. FootMovement runs first (foot states, coyote tracking), PlayerHipNode springs toward feet, PlayerSkeletonRoot reads all state and applies forces/jumps.
