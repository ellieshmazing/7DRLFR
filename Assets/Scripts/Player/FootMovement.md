# Foot Movement — Procedural Walking System

## Goal

Procedural walking system that makes the player's feet alternate between planted (Locked) and stepping (arc-interpolated) states, replacing the static spring-to-formation behavior in `PlayerFeet`. The locked foot provides the authoritative Y reference for the hip spring chain, gates jumping, and produces a natural walk/run animation that adapts to speed and terrain. Lateral velocity remains force-driven on the torso RB — this system controls foot placement, vertical grounding, and walk aesthetics only. Interacts with `PlayerHipNode` (ground reference), `PlayerSkeletonRoot` (jump gating), `FootContact` (collision/normal detection), and `PlayerConfig` (tuning variables).

## Architecture Overview

```
Every FixedUpdate (order -20, before PlayerHipNode at -15):
  Read velocity from torsoRB.linearVelocity
  Read torsoX, torsoY from torsoRB.position

  For each foot [left, right]:
    switch foot.state:

      Locked:
        Hold position: rb.position = lockPosition, rb.linearVelocity = zero, gravityScale = 0
        If FootContact.isGrounded == false:
          TransitionToAirborne(foot)                    // ground disappeared
        Else if |velocity.x| > idleSpeedThreshold:
          Check step trigger (foot behind ideal by > strideTriggerDistance)
          If triggered AND otherFoot.state != Stepping:
            StartStep(foot)
        Else:                                           // speed below idle threshold
          If foot not at ideal position AND otherFoot.state != Stepping:
            StartStep(foot, targetOverride = idealPosition)   // idle correction

      Stepping:
        Advance arc: stepProgress += dt / stepDuration
        Compute arcPos = EvaluateArc(startPos, targetPos, stepProgress, stepHeight)
        Drive foot: rb.linearVelocity = (arcPos - rb.position) / dt, gravityScale = 0
        If FootContact.isGrounded AND IsWalkable(contact.lastContactNormal):
          LockFoot(foot, rb.position)                   // early lock on walkable collision
        Else if stepProgress >= 1.0:
          LockFoot(foot, targetPos)                     // arc completed

      Airborne:
        gravityScale = config.footGravityScale
        Apply spring toward (hipX +/- footSpreadX, hipY)  // reuse existing foot spring params
        If FootContact.isGrounded AND IsWalkable(contact.lastContactNormal):
          LockFoot(foot, rb.position)                   // landing
          If otherFoot.state == Airborne:
            StartStep(otherFoot, targetOverride = otherFoot neutral position)  // catch-step

  HandleDirectionReversal(velocity)
  Update groundReferenceY for PlayerHipNode

On jump (called by PlayerSkeletonRoot):
  Require at least one foot in Locked state
  Both feet -> TransitionToAirborne
  Existing impulse logic fires unchanged
```

## Behaviors

### Foot State Machine

**Concept:** Per-limb finite state machine with three states (Locked, Stepping, Airborne), coordinated by a gait rule that only one foot may be Stepping at a time.

**Role:** Central controller — every foot position decision routes through the current state.

**Logic:**
```
enum FootState { Locked, Stepping, Airborne }

struct FootData:
    Rigidbody2D rb
    FootContact contact
    FootState state
    Vector2 lockPosition         // world pos where foot is planted (Locked)
    Vector2 stepStartPos         // world pos at step start (Stepping)
    Vector2 stepTargetPos        // world pos the arc aims for (Stepping)
    float   stepProgress         // 0..1 normalized time (Stepping)
    float   stepDuration         // seconds for this step (Stepping)
    int     side                 // -1 = left, +1 = right

Transition table:
  Locked   -> Stepping   : step trigger fires (foot behind ideal, or idle correction)
  Locked   -> Airborne   : ground lost (FootContact.isGrounded == false), or jump
  Stepping -> Locked     : arc completes (progress >= 1) or walkable collision mid-arc
  Stepping -> Locked     : direction reversal (lock at current position, then other foot steps)
  Stepping -> Airborne   : jump while stepping (forced unlock)
  Airborne -> Locked     : walkable collision detected (landing)
```

### Step Trigger & Gait Coordination

**Concept:** Distance-based trigger — a locked foot steps when it falls behind its ideal position (relative to the torso) in the movement direction, with a one-foot-at-a-time constraint.

**Role:** Determines WHEN each foot should start stepping and prevents both feet from stepping simultaneously.

**Logic:**
```
For each foot where state == Locked:
    idealX = torsoX + foot.side * footSpreadX * pixelToWorld
    signedBehind = (idealX - foot.lockPosition.x) * sign(velocity.x)

    if |velocity.x| > idleSpeedThreshold:
        if signedBehind > strideTriggerDistance * pixelToWorld AND otherFoot.state != Stepping:
            StartStep(foot)
    else:
        // Below idle threshold — correct toward neutral position
        displacement = |idealX - foot.lockPosition.x|
        if displacement > footSpreadX * pixelToWorld * 0.3 AND otherFoot.state != Stepping:
            raycast down from idealX to find groundY
            StartStep(foot, targetOverride = (idealX, groundY + footColliderRadius))

Tie-break when both feet are eligible (starting from idle):
    Pick the foot where foot.side * sign(velocity.x) < 0   // trailing foot
    // Moving right: left foot (-1 * +1 = -1 < 0) steps first
    // Moving left:  right foot (+1 * -1 = -1 < 0) steps first
```

### Step Target Prediction

**Concept:** Velocity-projected footfall validated by a downward raycast to find walkable ground.

**Role:** Determines WHERE the stepping foot will land, adapting to terrain and speed.

**Logic:**
```
idealX = torsoX + foot.side * footSpreadX * pixelToWorld
targetX = idealX + velocity.x * strideProjectionTime

rayOrigin = (targetX, torsoY + stepRaycastDistance * pixelToWorld * 0.5)
rayDist   = stepRaycastDistance * pixelToWorld
hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDist, ~playerLayerMask)

if hit.collider != null AND IsWalkable(hit.normal):
    targetY = hit.point.y + footColliderRadius
else:
    targetY = foot.lockPosition.y     // no walkable ground found — assume same height

return (targetX, targetY)
```

### Arc Interpolation

**Concept:** Parametric curve — linear horizontal interpolation with a sinusoidal height envelope on top.

**Role:** Defines the foot's curved path through space during a step, producing a natural lifting motion.

**Logic:**
```
EvaluateArc(startPos, targetPos, t, heightPx):
    // t in [0, 1], heightPx in source pixels
    heightWorld = heightPx * pixelToWorld

    x     = lerp(startPos.x, targetPos.x, t)
    yBase = lerp(startPos.y, targetPos.y, t)
    yArc  = yBase + heightWorld * sin(pi * t)

    return (x, yArc)

Step duration scales inversely with speed:
    duration = max(minStepDuration, baseStepDuration / (1 + |velocity.x| * stepSpeedScale))
```

### Walkable Surface Detection

**Concept:** Normal-angle threshold — a surface is walkable if its upward-facing normal is within a configurable cone from vertical.

**Role:** Gates where feet can lock, preventing stepping on walls, ceilings, or steep slopes.

**Logic:**
```
IsWalkable(normal):
    angle = Vector2.Angle(normal, Vector2.up)    // 0 = flat ground, 90 = vertical wall
    return angle <= maxWalkableAngle
```

### Direction Reversal

**Concept:** Immediate step cancellation and re-initiation when the player's movement direction flips while a foot is mid-step.

**Role:** Prevents the character from finishing a step in the wrong direction, keeping the walk visually responsive to input.

**Logic:**
```
Each FixedUpdate, after AdvanceStep:
    steppingFoot = find foot where state == Stepping
    if steppingFoot == null: return

    stepDir = sign(steppingFoot.stepTargetPos.x - steppingFoot.stepStartPos.x)
    moveDir = sign(velocity.x)

    // Only trigger if actually moving in opposite direction (above idle threshold)
    if |velocity.x| > idleSpeedThreshold AND moveDir != 0 AND moveDir != stepDir:
        LockFoot(steppingFoot, steppingFoot.rb.position)   // plant mid-arc
        otherFoot = the other foot
        if otherFoot.state == Locked:
            StartStep(otherFoot)                            // step in new direction
```

### Idle Stance Recovery

**Concept:** Corrective micro-steps when the player stops moving, returning feet to their neutral spread positions one at a time.

**Role:** Prevents the character from freezing mid-stride when input is released, and ensures both feet are planted for an instant jump from standstill.

**Logic:**
```
When |velocity.x| < idleSpeedThreshold AND foot.state == Locked:
    idealX = torsoX + foot.side * footSpreadX * pixelToWorld
    displacement = |foot.lockPosition.x - idealX|
    if displacement > footSpreadX * pixelToWorld * 0.3 AND otherFoot.state != Stepping:
        raycast down from idealX to find groundY
        StartStep(foot, targetOverride = (idealX, groundY + footColliderRadius))

// One foot corrects at a time due to the gait constraint.
// After the first correction completes, the second foot triggers on the next frame.
```

### Airborne-to-Landing Transition

**Concept:** First-contact lock with a complementary catch-step for the trailing limb.

**Role:** Smoothly transitions from freefall to grounded stance with a natural "catching yourself" landing motion.

**Logic:**
```
When foot.state == Airborne AND foot.contact.isGrounded AND IsWalkable(foot.contact.lastContactNormal):
    LockFoot(foot, foot.rb.position)

    if otherFoot.state == Airborne:
        // Other foot does a catch-step to its neutral spread position
        idealX = torsoX + otherFoot.side * footSpreadX * pixelToWorld
        raycast down from idealX -> groundY
        targetPos = (idealX, groundY + footColliderRadius)
        StartStep(otherFoot, targetOverride = targetPos)
```

### Hip Ground Reference

**Concept:** Single authoritative Y value for the hip spring target, derived from locked-foot positions rather than raw physics-body positions.

**Role:** Replaces `PlayerHipNode`'s current `min(leftFoot.y, rightFoot.y)` with a stable, state-aware ground reference that only changes at discrete lock events.

**Logic:**
```
GetGroundReferenceY():
    lockedFeet = [f for f in feet if f.state == Locked]
    if lockedFeet is not empty:
        return min(f.lockPosition.y for f in lockedFeet)
    else:
        // Both airborne — fall back to dynamic RB positions (same as old behavior)
        return min(leftFootRB.position.y, rightFootRB.position.y)
```

## Function Designs

### `ComputeStepTarget(foot: FootData, velocity: Vector2, torsoX: float, torsoY: float) → Vector2`

Finds where a stepping foot should land by projecting the foot's ideal position forward along the torso's velocity vector, then raycasting down for walkable ground.

**Parameters:**
- `foot` — the foot initiating the step; `foot.side` determines the spread offset direction
- `velocity` — torso velocity from `torsoRB.linearVelocity`
- `torsoX` — torso's current X world position
- `torsoY` — torso's current Y world position (raycast origin starts above this)

**Returns:** World-space target position for the foot center (surface Y + collider radius offset).

**Side effects:** Performs one `Physics2D.Raycast`.

```
idealX  = torsoX + foot.side * footSpreadX * pixelToWorld
targetX = idealX + velocity.x * strideProjectionTime

rayOriginY = torsoY + stepRaycastDistance * pixelToWorld * 0.5
rayDist    = stepRaycastDistance * pixelToWorld
hit = Physics2D.Raycast((targetX, rayOriginY), Vector2.down, rayDist, ~playerLayerMask)

if hit.collider != null AND IsWalkable(hit.normal):
    return (targetX, hit.point.y + footColliderRadius)
else:
    return (targetX, foot.lockPosition.y)    // fallback: assume same height
```

### `StartStep(foot: FootData, targetOverride: Vector2? = null) → void`

Initiates a step for the given foot. Computes (or accepts) the target position and sets up arc interpolation state.

**Parameters:**
- `foot` — the foot to begin stepping
- `targetOverride` — if non-null, skip `ComputeStepTarget` and use this position directly (used for idle corrections and landing catch-steps)

**Side effects:** Mutates `foot.state` → Stepping. Sets `stepStartPos`, `stepTargetPos`, `stepProgress = 0`, `stepDuration`. Sets `foot.rb.gravityScale = 0`.

```
foot.stepStartPos  = foot.rb.position
foot.stepTargetPos = targetOverride ?? ComputeStepTarget(foot, velocity, torsoX, torsoY)
foot.stepProgress  = 0
foot.stepDuration  = max(minStepDuration, baseStepDuration / (1 + |velocity.x| * stepSpeedScale))
foot.state         = Stepping
foot.rb.gravityScale = 0
```

### `AdvanceStep(foot: FootData, dt: float) → void`

Progresses a stepping foot along its arc. Checks for walkable collision (early lock) and arc completion.

**Parameters:**
- `foot` — the stepping foot
- `dt` — `Time.fixedDeltaTime`

**Side effects:** Advances `foot.stepProgress`. Sets `foot.rb.linearVelocity`. May call `LockFoot` on collision or completion.

Ordering: must run AFTER `FootContact` has processed collision events from the prior physics step (guaranteed because `OnCollisionEnter2D` fires during the physics step between FixedUpdates).

```
foot.stepProgress = min(foot.stepProgress + dt / foot.stepDuration, 1.0)

arcPos = EvaluateArc(foot.stepStartPos, foot.stepTargetPos, foot.stepProgress, stepHeight)
foot.rb.linearVelocity = (arcPos - (Vector2)foot.rb.position) / dt

// Early lock: foot contacted a walkable surface mid-arc
if foot.contact.isGrounded AND IsWalkable(foot.contact.lastContactNormal):
    LockFoot(foot, foot.rb.position)
    return

// Arc completed: lock at target
if foot.stepProgress >= 1.0:
    LockFoot(foot, foot.stepTargetPos)
```

### `LockFoot(foot: FootData, worldPos: Vector2) → void`

Transitions a foot to the Locked state at a specific world position. The foot becomes truly stationary.

**Parameters:**
- `foot` — the foot to lock
- `worldPos` — world-space lock position

**Side effects:** Sets `foot.state = Locked`, `foot.lockPosition = worldPos`. Zeroes velocity. Sets `gravityScale = 0`. Snaps `rb.position` to `lockPosition`.

```
foot.state         = Locked
foot.lockPosition  = worldPos
foot.rb.position   = worldPos
foot.rb.linearVelocity = Vector2.zero
foot.rb.gravityScale   = 0
```

### `TransitionToAirborne(foot: FootData) → void`

Transitions a foot to the Airborne state. Restores physics-driven behavior (gravity + spring).

**Parameters:**
- `foot` — the foot to unlock

**Side effects:** Sets `foot.state = Airborne`. Restores `gravityScale`. Does NOT zero velocity — preserves momentum from the previous state.

```
foot.state           = Airborne
foot.rb.gravityScale = config.footGravityScale
// velocity is intentionally NOT reset — let physics continue from current momentum
```

### `GetGroundReferenceY() → float`

Returns the Y coordinate that `PlayerHipNode` should spring toward. Prefers locked-foot positions; falls back to raw RB positions when airborne.

**Returns:** World-space Y value for the hip spring target.

```
lockedYs = collect lockPosition.y for each foot where state == Locked
if lockedYs is not empty:
    return min(lockedYs)
else:
    return min(leftFootRB.position.y, rightFootRB.position.y)
```

### `OnJump() → void`

Called by `PlayerSkeletonRoot` after applying jump impulse. Releases all locks so feet can fly freely.

**Side effects:** Both feet → Airborne via `TransitionToAirborne`. Does not modify velocity (impulse was already applied by the caller).

```
TransitionToAirborne(leftFoot)
TransitionToAirborne(rightFoot)
```

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| maxWalkableAngle | float (degrees) | 50 | Max angle between a surface normal and Vector2.up for the surface to count as walkable. 0 = only perfectly flat ground, 90 = any surface including walls. try 30–60; lower = stricter footing, higher = gecko-grip |
| strideTriggerDistance | float (px) | 5 | How far behind its ideal position (in the movement direction) a locked foot must be before it triggers a step, in source pixels. try 2–8; lower = steps initiate sooner, higher = foot drags further before stepping |
| strideProjectionTime | float (s) | 0.15 | Seconds of velocity projected forward when computing step target X. Controls how far ahead the foot reaches. try 0.05–0.3; lower = conservative short steps, higher = aggressive reaching strides |
| stepHeight | float (px) | 4 | Peak height of the step arc above the straight line between start and target, in source pixels. try 2–8; lower = shuffling/gliding, higher = marching/bounding |
| baseStepDuration | float (s) | 0.2 | Time for one complete step at zero horizontal speed. try 0.1–0.4; lower = snappy steps, higher = deliberate strides |
| minStepDuration | float (s) | 0.06 | Floor on step duration at high speeds. Prevents infinitely fast leg cycling. try 0.04–0.12 |
| stepSpeedScale | float | 0.3 | How much horizontal speed shortens step duration: `duration = base / (1 + speed * scale)`. try 0.1–0.6; lower = uniform cadence regardless of speed, higher = dramatically faster steps when running |
| idleSpeedThreshold | float (wu/s) | 0.5 | Horizontal speed in world units/s below which the system considers the player idle and begins correcting feet to neutral stance. try 0.2–1.0; lower = corrects sooner after stopping, higher = tolerates slow drift |
| stepRaycastDistance | float (px) | 30 | How far downward to raycast when probing for step target ground, in source pixels. Must exceed the torso-to-ground distance. try 20–50 |

## Implementation Notes

- **Replaces `PlayerFeet`.** `FootMovement` replaces `PlayerFeet.cs` entirely. Remove `PlayerFeet` from the HipNode; add `FootMovement` in its place. It attaches to the HipNode GO and is wired by `PlayerAssembler`.

- **Execution order is -20.** FootMovement must run before PlayerHipNode (-15) so the ground reference Y is fresh when the hip reads it. Updated chain: FootMovement (-20) → PlayerHipNode (-15) → PlayerSkeletonRoot (-10).

- **FootContact enhancement.** Add a `public Vector2 lastContactNormal` property to `FootContact`. Update it in both `OnCollisionEnter2D` and `OnCollisionStay2D` from `collision.GetContact(0).normal`. This is the only change to `FootContact` — the existing `isGrounded` counter logic stays.

- **PlayerHipNode modification.** Replace the hardcoded `min(leftFootRB.y, rightFootRB.y)` target with `footMovement.GetGroundReferenceY()`. Add a public `FootMovement footMovement` field wired by the assembler. No other changes to its spring logic.

- **PlayerSkeletonRoot jump modification.** Replace the `FootContact.isGrounded` check with `footMovement.CanJump()` (returns true if any foot is Locked). After applying jump impulse to foot RBs and hip, call `footMovement.OnJump()` to release all locks. Use `footMovement.GetLockedFootY()` for the hip-offset calculation instead of raw foot RB positions.

- **GravityScale toggling.** Locked and Stepping feet run with `gravityScale = 0` (position fully controlled by FootMovement). Airborne feet run with `gravityScale = config.footGravityScale` (physics-driven). Toggle on every state transition. This prevents gravity from fighting the arc or drifting a locked foot.

- **Airborne spring reuses existing foot spring params.** During Airborne, apply the same spring-damper logic that current `PlayerFeet` uses: X spring toward `hipX ± footSpreadX`, Y spring toward `hipY`, using `config.FootStiffness`, `config.FootDamping`, `config.footSpringMass`. No new spring variables needed.

- **Foot collider radius offset.** When computing step target Y from raycast hits, add the foot's collider radius so the foot circle sits ON the surface, not inside it. The assembler already computes this as `0.5 * SPRITE_PX / PPU` (= 0.0625). Pass it to FootMovement or recompute from the CircleCollider2D at startup.

- **Velocity override vs. MovePosition.** The stepping foot is driven via `rb.linearVelocity = (arcPos - rb.position) / dt` rather than `rb.MovePosition()`. Both approaches compute velocity internally; the explicit version makes the intent clear and avoids MovePosition's interpolation behavior.

- **One-frame hover on lock.** When a foot locks (arc completion or collision), there is a one-frame delay before the physics solver fully resolves the new stationary state. This is imperceptible. Do not add complexity to work around it.

- **Raycast miss fallback.** If `ComputeStepTarget`'s raycast hits nothing (or only non-walkable surfaces), use the foot's current lock Y as the target height. The foot steps to that height. If there is truly no ground at the target, the foot locks, then `FootContact.isGrounded` reads false on the next frame, and the foot transitions to Airborne and falls. Self-correcting within two frames.

- **Direction reversal dead zone.** The reversal detection uses `idleSpeedThreshold` as the minimum speed to count as "moving in a direction." This prevents velocity micro-oscillations near zero from triggering constant step cancellations. Reversal only fires when the player is clearly moving in the opposite direction from the step.

- **Non-walkable collision during step.** If a stepping foot contacts a non-walkable surface (wall, steep slope), the step continues — the physics engine prevents penetration and the velocity override pushes the foot along the surface. When the arc completes, the foot locks at its final resolved position. If that position has no ground, it transitions to Airborne on the next frame.

- **Idle correction stagger.** When returning to idle, the gait constraint (one step at a time) means feet correct sequentially. The first foot steps to neutral, locks, then the second foot triggers its correction on the following frame. This produces a natural two-step settle rather than both feet snapping simultaneously.

- **PlayerConfig additions.** Add all Modifiable Variables under a new `[Header("Foot Movement")]` section. All pixel-space values convert via `pixelToWorld` at runtime, consistent with existing spatial config values (`standHeight`, `footSpreadX`, etc.).

- **PlayerAssembler wiring.** Replace `PlayerFeet` component creation with `FootMovement`. Wire: both foot `Rigidbody2D` refs, both `FootContact` refs, `torsoRB` (the root's `Rigidbody2D`), hip node transform, config SO, `pixelToWorld`, and foot collider radius.