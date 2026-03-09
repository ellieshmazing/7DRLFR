# Body Integrity — Implementation Spec

## Goal

Add constraints to the player movement system so the torso, hip, and feet maintain physical coherence under all conditions. The primary mechanism is a "body leash" that bounds the torso's horizontal position relative to the feet's actual positions. Secondary mechanisms include: wall-obstruction force suppression (prevents force when feet are blocked), step path obstacle pre-check (prevents steps into walls), step arc collision detection (catches mid-arc wall hits), locked foot ground verification (detects disappeared ground), edge-landing nudge (stabilizes edge locks), and a foot separation limit (prevents extreme splits). All changes modify existing scripts — no new MonoBehaviours or GameObjects.

---

## Architecture Overview

```
FootMovement.FixedUpdate (order -20):
  // NEW: Ground probe — verify locked feet still have ground beneath them
  ProbeLockedFootGround(_left)
  ProbeLockedFootGround(_right)

  // Existing: coyote time tracking, jump coast decay, foot damping
  // Existing: trailing-foot-first ordering

  // Per-foot FSM (UpdateFoot) — MODIFIED:
  //   Locked state: unchanged (existing ground-loss check via isGrounded still runs)
  //   Stepping state: AdvanceStep adds arc collision linecast
  //   Airborne state: unchanged
  //   StartStep: pre-checks for wall obstacles, clamps by foot separation

  // Existing: direction reversal, assert no dual stepping

  // NEW public methods:
  //   GetFootCenterX() → float   — for leash anchor
  //   IsMovementBlockedByWall(moveDir) → bool  — for force suppression

PlayerHipNode.FixedUpdate (order -15):
  // Unchanged

PlayerSkeletonRoot.FixedUpdate (order -10):
  // 1. Ground state (existing)
  // 2. Read horizontal input (existing)

  // 3. NEW: Wall-obstruction force suppression
  //    IF IsMovementBlockedByWall(Sign(h)): h = 0

  // 4. Horizontal movement (existing, now uses suppressed h)
  // 5. Damping (existing)
  // 6. Crouch (existing)

  // 7. NEW: Body leash
  //    Compute foot center X from FootMovement
  //    Spring pull if beyond soft radius
  //    Hard clamp at hard radius + kill outward velocity

  // 8. Torso Y (existing)
  // 9. Hip X lock (existing — inherits leash-constrained torso X)
  // 10–12. Variable jump, wall slide, jump (existing)
```

---

## Behaviors

### Body Leash

**Concept:** One-dimensional position constraint bounding the torso's X within a maximum offset from the feet's center of mass.

**Role:** Prevents the torso from drifting away from grounded feet when feet cannot step forward (wall, edge, obstacle). The primary body-integrity mechanism.

**Logic:**
```
footCenterX = footMovement.GetFootCenterX()
displacement = torso.position.x - footCenterX
softRadiusWU = config.leashSoftRadius * pixelToWorld
hardRadiusWU = config.leashHardRadius * pixelToWorld

// Quadratic spring pull in the soft→hard zone
IF Abs(displacement) > softRadiusWU:
  excess = Abs(displacement) - softRadiusWU
  softRange = hardRadiusWU - softRadiusWU
  t = Clamp01(excess / softRange)           // 0 at soft edge, 1 at hard edge
  pullForce = moveForce * config.leashForceMult * t * t
  torsoRB.AddForce(Vector2(-Sign(displacement) * pullForce, 0))

// Hard clamp — safety net, should rarely be felt due to spring
IF Abs(displacement) > hardRadiusWU:
  clampedX = footCenterX + Sign(displacement) * hardRadiusWU
  torsoRB.position = Vector2(clampedX, torsoRB.position.y)
  IF Sign(torsoRB.linearVelocity.x) == Sign(displacement):
    torsoRB.linearVelocity = Vector2(0, torsoRB.linearVelocity.y)
```

### Wall-Obstruction Force Suppression

**Concept:** Directional force gating based on foot-wall contact. If feet are blocked in the movement direction, suppress horizontal force at the source rather than fighting it with the leash.

**Role:** Prevents force buildup against the leash when feet are stuck at walls. Works with the leash — suppression handles steady-state, leash handles transients and knockback.

**Logic:**
```
// Called on PlayerSkeletonRoot before applying horizontal force.
// Uses FootMovement.IsMovementBlockedByWall(moveDir)

wallBlocked = footMovement.IsMovementBlockedByWall(Sign(h))
IF wallBlocked:
  h = 0   // suppress horizontal input this frame
```

### Step Obstacle Pre-Check

**Concept:** Horizontal raycast at arc-peak height before starting a step. If a wall taller than the step arc is detected, the step target is shortened or the step is skipped entirely.

**Role:** Prevents steps from launching into walls and immediately early-locking, which causes visual stutter. Also allows feet to step OVER low obstacles shorter than `stepHeight`.

**Logic:**
```
// In StartStep, after computing target:
stepTarget = ComputeStepTarget(...)   // or explicit target

moveDir = Sign(stepTarget.x - foot.rb.position.x)
IF moveDir == 0: proceed with step

// Cast at the peak height the arc would reach — allows stepping over low walls
clearanceY = foot.rb.position.y + config.stepHeight * pixelToWorld + footColliderRadius
castDist = Abs(stepTarget.x - foot.rb.position.x) + footColliderRadius

hit = Physics2D.Raycast(
    Vector2(foot.rb.position.x, clearanceY),
    Vector2(moveDir, 0), castDist, _notPlayerMask)

IF hit.collider != null AND NOT IsWalkable(hit.normal):
  // Wall taller than step arc — shorten target
  shortenedX = hit.point.x - moveDir * footColliderRadius
  stepDist = Abs(shortenedX - foot.rb.position.x)

  IF stepDist < config.minStepDistance * pixelToWorld:
    // Too short to bother stepping — stay locked
    return (skip step)

  stepTarget.x = shortenedX
  stepTarget.y = RaycastGroundY(shortenedX, torsoY, foot.lockPosition.y)
```

### Step Arc Collision

**Concept:** Per-frame linecast along the step arc to catch walls the pre-check missed (narrow pillars, obstacles at non-peak height).

**Role:** Safety net during Stepping state. If the foot hits a non-walkable surface mid-arc, lock early at the pre-collision position instead of clipping through.

**Logic:**
```
// In AdvanceStep, after computing arcPos:
arcPos = EvaluateArc(...)

hit = Physics2D.Linecast(foot.rb.position, arcPos, _notPlayerMask)
IF hit.collider != null AND NOT IsWalkable(hit.normal):
  // Hit wall during arc — lock at contact point offset by radius
  lockPos = hit.point - (arcPos - foot.rb.position).normalized * footColliderRadius
  LockFoot(foot, lockPos)
  return
```

### Locked Foot Ground Probe

**Concept:** Per-frame downward raycast from each locked foot to verify the surface it locked onto still exists beneath it.

**Role:** Handles moving platforms, destructible terrain, and edge cases where `FootContact.isGrounded` stays true despite the logical ground having shifted. Acts as a ground-truth check independent of the collision system.

**Logic:**
```
// Called at the start of FootMovement.FixedUpdate for each foot
IF foot.state != Locked: return

probeDistWU = config.groundProbeDistance * pixelToWorld
hit = Physics2D.Raycast(
    foot.lockPosition, Vector2.down, probeDistWU, _notPlayerMask)

IF hit.collider == null OR NOT IsWalkable(hit.normal):
  TransitionToAirborne(foot)
```

### Edge Landing Nudge

**Concept:** When locking a foot after an airborne landing, nudge the lock position slightly inward if the contact normal is tilted (indicating a platform edge).

**Role:** Prevents foot from locking in a precarious position where it immediately loses contact next frame. Pushes the foot onto the platform surface for stable locking.

**Logic:**
```
// In LockFoot, when locking from Airborne state:
IF previousState == Airborne:
  normalX = foot.contact.lastContactNormal.x
  IF Abs(normalX) > 0.1:   // tilted contact = edge
    nudgeWU = config.edgeLandingNudge * pixelToWorld
    worldPos.x -= normalX * nudgeWU   // push away from edge direction
```

### Foot Separation Limit

**Concept:** Maximum horizontal distance between two feet. Step targets are clamped so the stepping foot never lands further than `maxFootSeparation` from the other locked foot.

**Role:** Prevents bizarre splits when one foot lands on a far ledge while the other is locked below. Keeps the body compact.

**Logic:**
```
// In StartStep, after obstacle pre-check:
IF other.state == Locked:
  maxSepWU = config.maxFootSeparation * pixelToWorld
  otherX = other.lockPosition.x
  IF Abs(stepTarget.x - otherX) > maxSepWU:
    stepTarget.x = otherX + Sign(stepTarget.x - otherX) * maxSepWU
    stepTarget.y = RaycastGroundY(stepTarget.x, torsoY, foot.lockPosition.y)
```

---

## Function Designs

### `GetFootCenterX() → float`
Returns the X coordinate that represents the feet's center of mass. Used by the body leash as the anchor point.

**Returns:** X position of locked foot center; falls back to RB midpoint when both airborne.

```
bool ll = _left.state == Locked
bool rl = _right.state == Locked
IF ll AND rl: return (_left.lockPosition.x + _right.lockPosition.x) / 2
IF ll: return _left.lockPosition.x
IF rl: return _right.lockPosition.x
// Both airborne — use physics positions
IF _left.rb != null AND _right.rb != null:
  return (_left.rb.position.x + _right.rb.position.x) / 2
return transform.position.x
```

### `IsMovementBlockedByWall(moveDir: float) → bool`
Checks whether locked feet are walled in the given direction, with an exception: if the other foot is locked further ahead, movement is permitted (allows climbing over low walls).

**Parameters:**
- `moveDir`: Sign of horizontal input (+1 right, -1 left, 0 none)

**Returns:** True if force should be suppressed in this direction.

```
IF moveDir == 0: return false

bool ll = _left.state == Locked
bool rl = _right.state == Locked
IF NOT ll AND NOT rl: return false

// A foot is "walled in moveDir" if locked + wall contact + wall normal opposes moveDir
bool leftWalled = ll AND _left.contact.isWalled
                  AND _left.contact.lastWallNormal.x * moveDir < 0
bool rightWalled = rl AND _right.contact.isWalled
                   AND _right.contact.lastWallNormal.x * moveDir < 0

IF NOT leftWalled AND NOT rightWalled: return false

// Exception: if the OTHER locked foot is further ahead in moveDir and not walled,
// movement is permitted (one foot can push off while the other is blocked).
IF leftWalled AND rl AND NOT rightWalled:
  IF (_right.lockPosition.x - _left.lockPosition.x) * moveDir > 0:
    return false   // right foot is ahead — can still move

IF rightWalled AND ll AND NOT leftWalled:
  IF (_left.lockPosition.x - _right.lockPosition.x) * moveDir > 0:
    return false   // left foot is ahead — can still move

return true
```

### `ProbeLockedFootGround(foot: FootData) → void`
Raycasts downward from a locked foot's position to verify ground still exists. Transitions to airborne if ground is gone.

**Side effects:** May call `TransitionToAirborne(foot)`.

```
IF foot.state != Locked: return

probeDistWU = config.groundProbeDistance * pixelToWorld
hit = Physics2D.Raycast(
    foot.lockPosition, Vector2.down, probeDistWU, _notPlayerMask)

IF hit.collider == null OR NOT IsWalkable(hit.normal):
  TransitionToAirborne(foot)
```

### `ApplyBodyLeash(dt: float) → void`
Applies the body leash constraint on PlayerSkeletonRoot. Must run after force application, before hip X lock.

**Parameters:**
- `dt`: Fixed timestep

**Side effects:** May AddForce to torso RB, may clamp torso position and zero outward velocity.

```
IF footMovement == null: return

footCenterX = footMovement.GetFootCenterX()
displacement = rb.position.x - footCenterX
softRadiusWU = config.leashSoftRadius * pixelToWorld
hardRadiusWU = config.leashHardRadius * pixelToWorld

IF Abs(displacement) > softRadiusWU:
  excess = Abs(displacement) - softRadiusWU
  softRange = hardRadiusWU - softRadiusWU
  IF softRange > 0:
    t = Clamp01(excess / softRange)
    pullForce = config.moveForce * config.leashForceMult * t * t
    rb.AddForce(Vector2(-Sign(displacement) * pullForce, 0))

IF Abs(displacement) > hardRadiusWU:
  clampedX = footCenterX + Sign(displacement) * hardRadiusWU
  rb.position = Vector2(clampedX, rb.position.y)
  IF Sign(rb.linearVelocity.x) == Sign(displacement):
    rb.linearVelocity = Vector2(0, rb.linearVelocity.y)
```

### `Modified: StartStep`
Adds obstacle pre-check and foot separation clamping before initiating a step.

**Side effects:** May modify step target, may abort step entirely.

```
// After computing stepTargetPos (either from target param or ComputeStepTarget):

// 1. Obstacle pre-check
moveDir = Sign(stepTargetPos.x - foot.rb.position.x)
IF moveDir != 0:
  clearanceY = foot.rb.position.y + config.stepHeight * pixelToWorld + footColliderRadius
  castDist = Abs(stepTargetPos.x - foot.rb.position.x) + footColliderRadius
  hit = Physics2D.Raycast(
      Vector2(foot.rb.position.x, clearanceY),
      Vector2(moveDir, 0), castDist, _notPlayerMask)

  IF hit.collider != null AND NOT IsWalkable(hit.normal):
    shortenedX = hit.point.x - moveDir * footColliderRadius
    IF Abs(shortenedX - foot.rb.position.x) < config.minStepDistance * pixelToWorld:
      return   // step too short — stay locked
    stepTargetPos.x = shortenedX
    stepTargetPos.y = RaycastGroundY(shortenedX, torsoY, foot.lockPosition.y)

// 2. Foot separation limit
IF other.state == Locked:
  maxSepWU = config.maxFootSeparation * pixelToWorld
  IF Abs(stepTargetPos.x - other.lockPosition.x) > maxSepWU:
    stepTargetPos.x = other.lockPosition.x + Sign(stepTargetPos.x - other.lockPosition.x) * maxSepWU
    stepTargetPos.y = RaycastGroundY(stepTargetPos.x, torsoY, foot.lockPosition.y)

// 3. Existing step initialization (state, duration, etc.)
```

### `Modified: AdvanceStep`
Adds linecast collision check before applying arc velocity.

**Side effects:** May call `LockFoot` early if wall hit.

```
foot.stepProgress = Min(foot.stepProgress + dt / foot.stepDuration, 1)
arcPos = EvaluateArc(foot.stepStartPos, foot.stepTargetPos, foot.stepProgress, config.stepHeight)

// NEW: Arc collision check
hit = Physics2D.Linecast(foot.rb.position, arcPos, _notPlayerMask)
IF hit.collider != null AND NOT IsWalkable(hit.normal):
  lockPos = hit.point - (arcPos - foot.rb.position).normalized * footColliderRadius
  LockFoot(foot, lockPos)
  return

// Existing: velocity override, early lock on descent, arc completion
```

### `Modified: LockFoot`
Adds edge landing nudge when transitioning from Airborne.

```
// After setting previousState, before setting foot.lockPosition:

IF previousState == Airborne:
  normalX = foot.contact.lastContactNormal.x
  IF Abs(normalX) > 0.1:
    nudgeWU = config.edgeLandingNudge * pixelToWorld
    worldPos.x -= normalX * nudgeWU
```

---

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| leashSoftRadius | float | 6 | Source pixels. Distance from foot center X where the leash spring begins pulling the torso back. Lower = tighter body, less freedom to drift. try 4–10 |
| leashHardRadius | float | 10 | Source pixels. Hard clamp distance from foot center X. Must be > leashSoftRadius. The spring should be strong enough near this boundary that it's imperceptible. try 8–16 |
| leashForceMult | float | 3 | Leash force at hard boundary expressed as a multiple of moveForce. Quadratic ramp from soft→hard. Higher = more invisible hard clamp. try 2–5 |
| maxFootSeparation | float | 20 | Source pixels. Maximum horizontal distance between two feet. Step targets are clamped to this. try 12–30; lower = more compact stance |
| groundProbeDistance | float | 2 | Source pixels. Downward raycast from locked foot to verify ground. Too low risks false positives on uneven terrain. try 1–4 |
| edgeLandingNudge | float | 0.5 | Source pixels. Inward nudge when locking on a tilted contact normal (platform edge). try 0.3–1.5; higher = more aggressive centering |
| minStepDistance | float | 1 | Source pixels. Minimum step distance after obstacle pre-check shortening. Below this, the step is skipped. try 0.5–2 |

---

## Implementation Notes

### Execution Order Is Preserved
All changes fit within the existing -20 → -15 → -10 chain. FootMovement's ground probe runs at the start of its FixedUpdate (before per-foot FSM). The leash runs inside PlayerSkeletonRoot's FixedUpdate between force application and hip X lock. No new execution order attributes needed.

### Leash Must Run Before Hip X Lock
The leash may clamp `rb.position.x`. The hip X lock (`hipNode.position = new Vector3(rb.position.x, ...)`) copies the torso's X to the hip. If the leash runs after the hip X lock, the hip would have the unclamped position. Insert the leash call between step 6 (crouch) and step 8 (torso Y) in PlayerSkeletonRoot — or equivalently, between the damping section and the torso Y constraint.

### Pre-Check Raycasts at Arc Peak, Not Foot Level
The step obstacle pre-check fires a horizontal ray at `foot.y + stepHeight * pixelToWorld + footColliderRadius` — the maximum height the foot's collider reaches during the arc. This lets feet step OVER walls shorter than the step height. If the wall is taller, the ray hits it and the step is shortened. Without the height offset, every bump and curb would block stepping.

### Arc Collision Uses Linecast, Not Raycast
`Physics2D.Linecast(currentPos, nextArcPos)` checks the exact segment the foot will traverse this frame. A Raycast with direction + distance would also work but Linecast is semantically clearer for point-to-point checks. Both ignore colliders containing the origin by default (Unity's "Queries Start In Colliders" setting), which is fine because the foot shouldn't be inside a wall at the start of the cast.

### Ground Probe Complements, Does Not Replace, isGrounded
The `FootContact.isGrounded` counter (enter/exit) handles normal ground loss (foot physically separates from surface). The ground probe handles the case where `isGrounded` stays true but the ground has logically moved — e.g., a moving platform shifts sideways. Both are needed. The probe runs first in FixedUpdate; the existing isGrounded check runs inside UpdateFoot's Locked case. A foot caught by either check transitions to airborne.

### Edge Nudge Is Small and Directional
The nudge is `config.edgeLandingNudge * pixelToWorld` — at default 0.5px with playerScale 0.5, that's ~0.016 world units. It pushes the foot opposite to the horizontal component of the contact normal, which on a box collider edge points at ~45° outward. The nudge pulls the foot inward onto the platform. If the contact normal is vertical (flat ground), `normalX ≈ 0` and no nudge occurs.

### Wall Suppression and the "Other Foot Ahead" Exception
`IsMovementBlockedByWall` returns false when one foot is walled but the other locked foot is further ahead in the movement direction. This handles climbing low walls: the front foot steps onto the wall top (via arc), the back foot is blocked by the wall side. Because the front foot is ahead, force isn't suppressed, and the player can continue forward. The back foot eventually steps up via the normal stepping FSM.

### `leashForceMult` Scales with `moveForce`
The leash force at the hard boundary equals `moveForce * leashForceMult`. At default values (moveForce=15, leashForceMult=3), that's a 45N pull — 3x the player's acceleration force. This guarantees the player cannot sustain enough force to feel the hard clamp. If moveForce is tuned up, the leash automatically scales.

### StartStep Needs Access to `other` FootData
The foot separation clamp in `StartStep` needs the other foot's lock position. Currently, `StartStep` doesn't receive the other foot. The signature must be extended to pass `other`, or the clamping logic can be inlined at the call sites in `UpdateFoot` (where both `foot` and `other` are available). Extending the signature is cleaner.

### Common Wrong Approach: Leash on Hip Instead of Torso
Constraining the hip's X to the feet and letting the torso follow via the hip X lock seems equivalent but isn't. The torso is the RB that receives forces — constraining it directly is more effective and avoids a one-frame lag where the torso moves past the limit before the hip catches up.
