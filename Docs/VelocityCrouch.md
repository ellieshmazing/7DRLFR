# Velocity-Based Crouch System

## Goal

Replace the button-driven crouch with an impact-driven system where landing velocity converts to crouch compression, storing potential energy for the next jump. The player builds jumping momentum through successive jumps: land → compress → jump higher → land harder → compress deeper → jump even higher, converging toward a ceiling derived from `standHeight`. Holding the down key freezes stored energy (at the cost of mobility) for timing control. The system integrates with the existing jump offset calculation (`totalHipCompression`), hip spring (`PlayerHipNode`), and torso visual spring (`NodeWiggle`).

## Architecture Overview

```
FootMovement.FixedUpdate (order -20):
  Per-foot state machine (unchanged core)
  On LockFoot from Airborne:
    Capture foot's pre-lock downward velocity (BEFORE zeroing it)
    Accumulate max across landing capture window
  Decay landing capture timer
  When window expires: mark landing speed ready for consumption
  Expose ConsumeLastLandingSpeed() → float

PlayerHipNode.FixedUpdate (order -15):
  Spring toward GetGroundReferenceY() (unchanged)

PlayerSkeletonRoot.FixedUpdate (order -10):
  Consume landing speed from FootMovement
  If landing speed > 0:
    Convert to crouch amount (clamped to maxCrouchDepth = standHeight * crouchDepthRatio)
    Take max of new crouch and existing crouch (don't reduce stored energy)
    Set squash punch = crouchAmount * (impactSquashOvershoot - 1.0)
    Set landing impact intensity (0–1) for external systems

  Crouch management:
    If NOT grounded → clear crouchFrozen
    Else if holding down AND crouchAmount > 0:
      Freeze dissipation, suppress horizontal movement (h = 0)
    Else:
      Unfreeze, linear decay crouchAmount toward 0

  Squash punch: linear decay toward 0 (faster than crouch dissipation)

  Torso Y = hipNode.Y + (standHeight - crouchAmount - squashPunch) * pixelToWorld

  Jump (existing logic, reads _crouchAmount unchanged):
    totalHipCompression = hipOffset + _crouchAmount * pixelToWorld
    On jump: clear _crouchFrozen
```

## Behaviors

### Impact Crouch Capture

**Concept:** Cross-component event signaling — landing detection lives in FootMovement (order -20), crouch state lives in PlayerSkeletonRoot (order -10). A consume-pattern property bridges the gap without callbacks.

**Role:** Captures the maximum downward velocity across both feet during a landing, making it available for crouch conversion on the same frame the capture window expires.

**Logic:**
```
In LockFoot(foot), at the top, after capturing previousState:
  IF previousState == Airborne:
    speed = Abs(foot.rb.linearVelocity.y)    // BEFORE zeroing velocity
    _capturedLandingSpeed = Max(_capturedLandingSpeed, speed)
    _landingCaptureTimer = config.landingCaptureWindow
    _landingReady = false    // window restarted

In FootMovement.FixedUpdate, after existing logic:
  IF _landingCaptureTimer > 0:
    _landingCaptureTimer -= dt
    IF _landingCaptureTimer <= 0:
      _landingReady = true

ConsumeLastLandingSpeed():
  IF _landingReady:
    _landingReady = false
    result = _capturedLandingSpeed
    _capturedLandingSpeed = 0
    return result
  return 0
```

### Crouch Conversion

**Concept:** Linear velocity-to-displacement mapping with capacity ceiling — landing speed maps to crouch depth via a flat multiplier, capped by a fraction of standHeight.

**Role:** Translates raw landing speed into a bounded crouch amount that feeds the existing jump offset system. The standHeight-derived ceiling means taller characters store more energy.

**Logic:**
```
In PlayerSkeletonRoot.FixedUpdate, after ground state checks:
  float landingSpeed = footMovement.ConsumeLastLandingSpeed()
  IF landingSpeed > 0:
    float maxDepth = config.standHeight * config.crouchDepthRatio
    float newCrouch = Min(landingSpeed * config.impactCrouchFactor, maxDepth)
    _crouchAmount = Max(_crouchAmount, newCrouch)    // never reduce stored energy

    // Visual overshoot (at 1.0, _squashPunch = 0 — natural spring behavior only)
    _squashPunch = _crouchAmount * (config.impactSquashOvershoot - 1.0f)

    // Normalized impact for external systems
    _lastImpactIntensity = _crouchAmount / maxDepth
```

### Crouch Dissipation

**Concept:** Linear decay with optional freeze — stored energy drains at a constant rate unless the player actively holds it by pressing down.

**Role:** Creates a timing window for jump utilization. Without action, the character returns to neutral stance. The freeze mechanic gives skilled players control over when to release stored energy.

**Logic:**
```
In PlayerSkeletonRoot.FixedUpdate, replacing the old crouch block:
  bool downHeld = kb != null
      && (kb.sKey.isPressed || kb.downArrowKey.isPressed)

  IF NOT isGrounded:
    _crouchFrozen = false
    // Airborne: crouch still dissipates normally
    _crouchAmount = MoveTowards(_crouchAmount, 0, config.crouchDissipationRate * dt)
  ELSE IF downHeld AND _crouchAmount > 0:
    _crouchFrozen = true
    // Dissipation paused — crouchAmount holds its value
  ELSE:
    _crouchFrozen = false
    _crouchAmount = MoveTowards(_crouchAmount, 0, config.crouchDissipationRate * dt)

  // Squash punch always decays (independent of freeze)
  _squashPunch = MoveTowards(_squashPunch, 0, config.squashPunchDecayRate * dt)
```

### Movement Suppression During Hold

**Concept:** Input gating — horizontal force application is blocked while the player holds crouch, creating a mobility-vs-timing trade-off.

**Role:** Prevents the player from repositioning while storing energy. Existing ground damping decelerates naturally — no special braking logic needed.

**Logic:**
```
In PlayerSkeletonRoot.FixedUpdate, step 2 (horizontal movement):
  // After reading h from keyboard:
  IF _crouchFrozen:
    h = 0    // suppress all horizontal input

  // Rest of movement code unchanged
```

### Visual Squash Punch

**Concept:** Overshoot multiplier — an instantaneous extra compression on landing that decays quickly, amplifying the visual impact without affecting stored jump energy.

**Role:** Visual juice that sells the weight of heavy landings. At multiplier 1.0, `_squashPunch` is 0 and only the natural torso spring (NodeWiggle) provides overshoot. Values above 1.0 add progressively more dramatic compression.

**Logic:**
```
// Torso Y uses BOTH crouch and squash punch:
float visualCrouch = _crouchAmount + _squashPunch
float effectiveStandHeight = (config.standHeight - visualCrouch) * pixelToWorld
float desiredY = hipNode.position.y + effectiveStandHeight
float yCorrection = (desiredY - rb.position.y) / dt
rb.linearVelocity = new Vector2(rb.linearVelocity.x, yCorrection)

// Jump reads ONLY _crouchAmount (NOT _squashPunch):
float totalHipCompression = hipOffset + _crouchAmount * pixelToWorld
```

### Landing Impact Intensity API

**Concept:** Normalized event signal — a 0-1 value representing impact severity, exposed for external systems (particles, camera shake, audio).

**Role:** Decouples visual/audio feedback from crouch internals. External systems read the intensity without needing to understand crouch mechanics.

**Logic:**
```
// Set once per landing in Crouch Conversion:
_lastImpactIntensity = _crouchAmount / maxCrouchDepth

// Public read-only properties:
public float LastImpactIntensity => _lastImpactIntensity
public float CrouchRatio => (config != null && config.standHeight * config.crouchDepthRatio > 0)
    ? _crouchAmount / (config.standHeight * config.crouchDepthRatio)
    : 0f
```

## Function Designs

### `ConsumeLastLandingSpeed() → float`
Returns the maximum downward velocity captured during the most recent landing window, then clears internal state. Returns 0 if no landing is pending.

**Parameters:** None.
**Returns:** Landing speed in wu/s (always >= 0).
**Side effects:** Clears `_capturedLandingSpeed`, `_landingReady`.

```
IF _landingReady:
  _landingReady = false
  result = _capturedLandingSpeed
  _capturedLandingSpeed = 0
  return result
return 0
```

**Ordering:** Called from PlayerSkeletonRoot.FixedUpdate (order -10), after FootMovement.FixedUpdate (order -20) has managed the capture window. The landing speed is ready on the same frame the window expires.

### `CaptureLandingVelocity(foot: FootData) → void`
Captures a foot's pre-lock downward velocity into the accumulator. Called inside LockFoot when transitioning from Airborne, BEFORE the velocity is zeroed.

**Parameters:**
- `foot`: FootData being locked. `foot.rb.linearVelocity.y` is read before LockFoot zeroes it.

**Side effects:** Updates `_capturedLandingSpeed` (max), resets `_landingCaptureTimer`, clears `_landingReady`.

```
float speed = Abs(foot.rb.linearVelocity.y)
_capturedLandingSpeed = Max(_capturedLandingSpeed, speed)
_landingCaptureTimer = landingCaptureWindow
_landingReady = false
```

**Critical ordering:** Must execute BEFORE `foot.rb.linearVelocity = Vector2.zero` in LockFoot.

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| impactCrouchFactor | float | 0.5 | Multiplier converting landing speed (wu/s) to crouch depth (source px). Controls how much a given fall compresses the body. try 0.3–1.0; lower = subtle landings, higher = dramatic compression |
| crouchDepthRatio | float | 0.4 | Fraction of standHeight that defines max crouch depth. This is the ceiling for the momentum chain. try 0.2–0.6; lower = shallow compression cap, higher = deep squats and bigger max jumps |
| crouchDissipationRate | float | 15.0 | Source pixels per second of crouch decay when not frozen. Controls how long the player has to jump before stored energy is lost. try 8–30; lower = forgiving timing window, higher = must jump quickly |
| impactSquashOvershoot | float | 1.0 | Visual overshoot multiplier on landing. 1.0 = no extra squash (natural torso spring only). Values above 1.0 add extra compression that decays via squashPunchDecayRate. try 1.0–2.0; higher = juicier landings |
| squashPunchDecayRate | float | 40.0 | Source pixels per second of squash punch decay. Should be faster than crouchDissipationRate so the visual punch is brief. try 25–60; lower = lingering squash, higher = snappy pop-back |
| landingCaptureWindow | float | 0.05 | Seconds to accumulate max landing speed across both feet. Covers the gap between first and second foot locking. try 0.03–0.08; too low = might miss second foot, too high = delayed crouch response |

### Variables Removed

| Variable | Reason |
|---|---|
| maxCrouchDepth | Replaced by `standHeight * crouchDepthRatio` — max crouch is now derived from stance geometry |
| crouchSpeed | No longer needed — crouch is set instantly on impact, not animated from button input |

## Implementation Notes

### Velocity Capture Must Precede Velocity Zero
In `LockFoot()`, insert the capture call at the very top, after capturing `previousState` but before any state mutations:
```
FootState previousState = foot.state;
if (previousState == FootState.Airborne)
    CaptureLandingVelocity(foot);    // BEFORE velocity zero
foot.state = FootState.Locked;
foot.lockPosition = worldPos;
foot.rb.position = worldPos;
foot.rb.linearVelocity = Vector2.zero;    // velocity zeroed here
```

### MaxCrouchDepth Is Derived, Not Stored
Remove `maxCrouchDepth` from PlayerConfig. All references should use `config.standHeight * config.crouchDepthRatio`. This means upgrading `standHeight` automatically increases the jump energy ceiling — intentional design coupling.

### Jump System — No Changes Needed
The existing jump code reads `_crouchAmount` unchanged:
```
float totalHipCompression = hipOffset + _crouchAmount * pixelToWorld;
float jumpMagnitude = jumpSpeed + totalHipCompression * jumpOffsetFactor;
```
The momentum chain emerges naturally: higher crouch → higher jump → faster landing → deeper crouch → higher jump, until `maxCrouchDepth` caps it.

### Squash Punch Is Visual Only
`_squashPunch` affects `effectiveStandHeight` (torso RB position) but NOT the jump calculation. Jump reads `_crouchAmount` directly. The overshoot multiplier is pure juice — no gameplay impact.

### Crouch Frozen Clears on Jump
In `TryJump`, after `_jumpedThisPress = true`:
```
_crouchFrozen = false;
```
The crouch amount itself decays naturally via the dissipation branch next frame — no snap.

### Crouch Frozen Clears When Airborne
If the player becomes airborne without jumping (walked off edge, knockback):
```
IF NOT isGrounded:
    _crouchFrozen = false
```
Prevents the player from being stuck in frozen state.

### Down Key Has No Effect When Airborne or Uncrouched
The freeze condition requires `isGrounded AND _crouchAmount > 0`. Pressing down while airborne or while standing at full height does nothing. No exploit path.

### Landing While Already Crouched
If the player lands before fully dissipating from a previous landing, `_crouchAmount = Max(_crouchAmount, newCrouch)` ensures soft landings don't reduce stored energy. Only harder landings increase it.

### Landing While Frozen
If the player is holding down and somehow lands again, the max-take rule still applies. `_crouchFrozen` remains true. The player keeps the higher of the two values.

### Momentum Chain Convergence Math
With defaults (standHeight=12, crouchDepthRatio=0.4, impactCrouchFactor=0.5, jumpOffsetFactor=10):
- maxCrouchDepth = 12 * 0.4 = 4.8 source px
- Base jump (8 wu/s) → land at ~8 wu/s → crouch = min(8 * 0.5, 4.8) = 4.0 px
- Boosted jump → land faster → crouch = min(faster * 0.5, 4.8) → converges at cap
- The system amplifies when `jumpOffsetFactor * impactCrouchFactor * pixelToWorld > 1`. Tune these three together.

### Existing Code Removal
- Remove the `downHeld` button check for crouch compression in PlayerSkeletonRoot (old lines 163-176)
- Remove `maxCrouchDepth` and `crouchSpeed` fields from PlayerConfig
- The `_crouchAmount` field stays — same name, same role, different driver

### Frame Ordering Unchanged
The -20 → -15 → -10 execution order chain is preserved. FootMovement captures landing velocity at -20, PlayerSkeletonRoot consumes it at -10. No new ordering dependencies introduced.
