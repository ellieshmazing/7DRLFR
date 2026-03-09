# Pincer Attack Mechanic

## Goal

Adds animated pincers to the centipede head (SkeletonRoot) that kill the player on contact. Two sprite renderers share one sprite asset — one mirrored — rotating in opposing directions via a sinusoidal drive to produce a bilateral clapping/biting motion. Static rectangular trigger colliders (decoupled from the animation) define the actual kill zone. Hit effects are dispatched through an `IPlayerHitEffect` interface so future effects (stun, knockback, slowdown) compose without modifying `PincerController`. The pincers also graduate from a slow idle click to a fast attack click when the player enters proximity, giving a readable threat escalation cue. All components are created at runtime by `CentipedeAssembler`.

---

## Architecture Overview

```
CentipedeAssembler.Spawn():
  root.SetActive(false)
  AddComponent<PincerController>(root)
  PincerController.Build(config)
    → create LeftPincer GO  (child of root)  [SpriteRenderer, flipX=false]
    → create RightPincer GO (child of root)  [SpriteRenderer, flipX=true ]
    → create LeftHitbox GO  (child of root)  [BoxCollider2D trigger, Kinematic RB2D, PincerHitDetector]
    → create RightHitbox GO (child of root)  [BoxCollider2D trigger, Kinematic RB2D, PincerHitDetector]
  root.SetActive(true)

FixedUpdate (PincerController, order 0):
  distToPlayer = distance from head to PlayerRegistry.PlayerTransform
  currentClickSpeed = lerp between idleClickSpeed and attackClickSpeed based on proximity
  angle = sin(time * currentClickSpeed * 2π) * clickAngle
  LeftPincer.localRotation  = Quaternion(0, 0, +angle)
  RightPincer.localRotation = Quaternion(0, 0, -angle)
  check sign crossing of sin → fire OnClick event if newly negative-peak

On PincerHitDetector.OnTriggerEnter2D(collider):
  if collider is on Player layer:
    PincerController.HandlePlayerHit(collider.gameObject)

PincerController.HandlePlayerHit(playerGO):
  foreach effect in hitEffects:
    effect.Apply(playerGO)
```

---

## Behaviors

### Bilateral pivot animation

**Concept:** `sin(t × ω) × θ` drives opposing rotations on two sprite renderers, producing symmetric clapping motion with no state machine.

**Role:** Creates the visible biting animation.

**Logic:**
```
t       = Time.time (accumulated, not delta)
ω       = currentClickSpeed × 2π        // radians per second
angle   = sin(t × ω) × clickAngle       // degrees

LeftPincer.localEulerAngles  = (0, 0, +angle)
RightPincer.localEulerAngles = (0, 0, -angle)
```

The two renderers use the same sprite asset. `RightPincer.SpriteRenderer.flipX = true` mirrors it. Both GOs start at the same local position offset (`pincerOffset`) from the head center. The pivoting happens around each GO's own origin — position the GOs so the pivot point corresponds to the pincer's base joint.

---

### Behavioral state: Idle → Attack

**Concept:** Distance-gated speed scaling — `currentClickSpeed` is linearly interpolated between `idleClickSpeed` and `attackClickSpeed` based on how close the player is to the head, clamped to [0, 1].

**Role:** Provides a readable escalation cue before contact; the pincers visibly accelerate as the centipede closes in.

**Logic:**
```
if PlayerRegistry.PlayerTransform == null:
  currentClickSpeed = idleClickSpeed
else:
  dist = Vector2.Distance(head.position, player.position)
  t    = 1 - saturate((dist - attackInnerRadius) / (attackOuterRadius - attackInnerRadius))
         // t = 0 at outer radius, 1 at inner radius
  currentClickSpeed = lerp(idleClickSpeed, attackClickSpeed, t)
```

`saturate(x)` means `Clamp01(x)`. `attackOuterRadius` is where escalation begins; `attackInnerRadius` is where it maxes out.

---

### Click event at full closure

**Concept:** Sign-crossing detection — compare the sign of `sin(t × ω)` this frame to last frame's sign. A negative-peak crossing (last > 0, current ≤ 0 after the midpoint) signals the pincers snapping shut.

**Role:** Provides a hook point for audio, particles, and screen shake without modifying `PincerController`.

**Logic:**
```
prevSin  = sin(prevT × ω)
currSin  = sin(t × ω)
halfPhase = (t × ω) mod (2π) > π      // past the halfway point in the cycle

if prevSin > 0 and currSin <= 0 and halfPhase:
  OnClick?.Invoke()

prevT = t
```

Alternative: track `prevAngle` sign. The halfPhase guard avoids a false fire on the zero-crossing going the other way (opening).

---

### Spring wobble on pincer visual

**Concept:** `NodeWiggle` is a transform-space spring-damper applied in LateUpdate. Adding it to each pincer GO means the click rotation and the spring offset compose additively — the spring adds a positional/rotational lag on top of the sine drive.

**Role:** Makes the pincers physically react to the centipede's locomotion, matching every other visual in the codebase.

**Logic:**
```
// During Build(), after creating LeftPincer and RightPincer GOs:
leftWiggle  = LeftPincer.AddComponent<NodeWiggle>()
rightWiggle = RightPincer.AddComponent<NodeWiggle>()
// NodeWiggle reads stiffness/damping from CentipedeConfig.WiggleStiffness/WiggleDamping
// PincerController does not need to set these — NodeWiggle reads from config at runtime
```

`NodeWiggle` is already dual-written by `TuningManager` — pincer wiggles participate in the tuning system for free.

---

### Static trigger hitboxes

**Concept:** Hitbox/hurtbox separation — the BoxCollider2D positions are fixed siblings of the pincer visuals (children of the head GO, not children of the animated sprite GOs) so they never rotate with the animation.

**Role:** Makes the collision boundary more forgiving than the visual implies, and prevents frustrating edge-hits from animation extremes.

**Logic:**
```
// LeftHitbox GO — child of root, NOT child of LeftPincer
LeftHitbox.localPosition  = (-pincerHitboxOffsetX, pincerHitboxOffsetY, 0)
LeftHitbox.BoxCollider2D  = size (hitboxWidth, hitboxHeight), isTrigger = true
LeftHitbox.Rigidbody2D    = Kinematic, simulated = true, useFullKinematicContacts = true

// Mirror for RightHitbox
RightHitbox.localPosition = (+pincerHitboxOffsetX, pincerHitboxOffsetY, 0)

// PincerHitDetector on each hitbox GO
// PincerHitDetector.OnTriggerEnter2D → calls controller.HandlePlayerHit()
```

Layer: assign hitbox GOs to the Centipede physics layer. Ensure Player↔Centipede collision is enabled in the Layer Collision Matrix.

---

### Extensible hit dispatch

**Concept:** Strategy pattern — `IPlayerHitEffect` is an interface with a single `Apply(GameObject player)` method. `PincerController` holds `List<IPlayerHitEffect> hitEffects` and iterates it on contact.

**Role:** New hit behaviors (stun, knockback, damage) are new classes added to the list — no changes to `PincerController`.

**Logic:**
```
interface IPlayerHitEffect:
  Apply(playerGO: GameObject) → void

class DestroyPlayerEffect : IPlayerHitEffect:
  Apply(playerGO):
    PlayerRegistry.Unregister(playerGO.transform)
    Destroy(playerGO)

PincerController.HandlePlayerHit(playerGO):
  for each effect in hitEffects:
    effect.Apply(playerGO)
  // guard: if player destroyed by first effect, subsequent effects may see null — check
```

`HandlePlayerHit` should null-check `playerGO` before the loop and bail if null. Effects are responsible for checking `if (playerGO == null) return` if they run after a destroying effect.

---

## Function Designs

### `Build(config: CentipedeConfig, sprite: Sprite) → void`
Creates all child GOs for pincers and hitboxes on the head GameObject. Must be called while root is inactive (before `SetActive(true)`).

**Parameters:**
- `config` — source of all pincer tuning values
- `sprite` — the sprite asset to assign to both renderers (right side gets `flipX = true`)

**Side effects:** Creates 4 child GOs on the head: `LeftPincer`, `RightPincer`, `LeftHitbox`, `RightHitbox`. Adds `NodeWiggle` to each pincer GO. Adds `PincerHitDetector` to each hitbox GO.

```
LeftPincer  = new GameObject("LeftPincer")
LeftPincer.parent = head
LeftPincer.localPosition = (-config.pincerOffsetX, config.pincerOffsetY, 0)
leftRenderer = LeftPincer.AddComponent<SpriteRenderer>()
leftRenderer.sprite = sprite
leftRenderer.flipX  = false
leftRenderer.sortingOrder = headRenderer.sortingOrder + 1
LeftPincer.localScale = Vector3(config.pincerSize, config.pincerSize, 1)
LeftPincer.AddComponent<NodeWiggle>()

// Mirror for RightPincer (flipX = true, offsetX positive)

leftHitboxGO = new GameObject("LeftHitbox")
leftHitboxGO.parent = head           // NOT child of LeftPincer
leftHitboxGO.localPosition = (-config.pincerHitboxOffsetX, config.pincerHitboxOffsetY, 0)
leftBox = leftHitboxGO.AddComponent<BoxCollider2D>()
leftBox.size = config.pincerColliderSize
leftBox.isTrigger = true
leftRB = leftHitboxGO.AddComponent<Rigidbody2D>()
leftRB.bodyType = Kinematic
leftRB.simulated = true
leftHitboxGO.AddComponent<PincerHitDetector>().controller = this

// Mirror for RightHitbox
```

---

### `HandlePlayerHit(playerGO: GameObject) → void`
Dispatches all hit effects. Called by `PincerHitDetector` on trigger entry.

**Parameters:**
- `playerGO` — the player root GameObject (obtained from the collider's root or `PlayerRegistry.PlayerTransform.gameObject`)

**Side effects:** Iterates `hitEffects`; default effect destroys the player and unregisters from `PlayerRegistry`.

```
if playerGO == null: return
for each effect in hitEffects:
  if playerGO == null: break    // destroyed by a prior effect
  effect.Apply(playerGO)
```

---

### `UpdateAnimation() → void` (called from FixedUpdate)
Drives the click animation and fires `OnClick` at full closure.

**Side effects:** Sets `localEulerAngles` on `LeftPincer` and `RightPincer`. May invoke `OnClick`.

```
float t    = Time.time
float ω    = currentClickSpeed * 2π
float sin  = Mathf.Sin(t * ω)
float angle = sin * clickAngle

LeftPincer.localEulerAngles  = (0, 0, +angle)
RightPincer.localEulerAngles = (0, 0, -angle)

// Click event: detect negative-peak crossing
float halfPhase = (t * ω) % (2π) > π
if prevSin > 0 && sin <= 0 && halfPhase:
  OnClick?.Invoke()

prevSin = sin
```

---

## Modifiable Variables

All pincer variables live in `CentipedeConfig` under a `[Header("Pincers")]` block.

| Variable | Type | Default | Description |
|---|---|---|---|
| `pincerSprite` | `Sprite` | null | Sprite asset used for both pincer renderers. Right renderer gets `flipX = true`. Required — assembler skips pincer setup if null. |
| `pincerSize` | `float` | `0.4` | Uniform local scale of each pincer GO. Controls visual size of the claw relative to the head. Try 0.2–0.8; lower = subtle, higher = dramatic. |
| `pincerOffsetX` | `float` | `0.12` | Local X distance from head center to each pincer pivot. Controls how far apart the pincers sit. Try 0.05–0.25. |
| `pincerOffsetY` | `float` | `0.1` | Local Y distance from head center to each pincer pivot (positive = forward/up relative to head facing). Try -0.1–0.2. |
| `idleClickSpeed` | `float` | `1.5` | Click frequency in Hz when player is outside attack radius. Try 0.5–3.0; lower = lazy, higher = restless. |
| `attackClickSpeed` | `float` | `4.0` | Click frequency in Hz when player is at inner radius. Try 2.0–8.0. |
| `clickAngle` | `float` | `35` | Degrees each pincer rotates from center at peak. Controls how wide the pincers open. Try 15–60; lower = tight snip, higher = wide bite. |
| `attackOuterRadius` | `float` | `3.0` | Distance at which the pincers begin speeding up. Try 2.0–6.0. |
| `attackInnerRadius` | `float` | `1.0` | Distance at which pincers reach full `attackClickSpeed`. Try 0.5–2.0; must be < `attackOuterRadius`. |
| `pincerColliderSize` | `Vector2` | `(0.1, 0.15)` | Width × height of each hitbox in world units. Intentionally smaller than the visual. Try (0.05–0.15, 0.1–0.25). |
| `pincerHitboxOffsetX` | `float` | `0.1` | Local X of each hitbox from head center. Should roughly match the pincer tip position at rest. |
| `pincerHitboxOffsetY` | `float` | `0.1` | Local Y of each hitbox from head center. |

---

## Implementation Notes

**Sprite ordering:** Pincer renderers should sort above the head ball visual. Set `sortingOrder = headBall.SpriteRenderer.sortingOrder + 1`. Use the same `sortingLayerName` as the head.

**NodeWiggle initialization:** `NodeWiggle` reads stiffness/damping from its target node in `LateUpdate`. When added to the pincer GOs, it will read from `CentipedeConfig.WiggleStiffness/WiggleDamping` if `TuningManager` dual-writes to it. Verify `TuningManager` picks up new `NodeWiggle` instances via its component scan.

**Kinematic RB and trigger contacts:** `useFullKinematicContacts` must be `true` on the hitbox RBs so Unity reports all overlapping triggers. If using `simulationMode = Script` or `Continuous` in Project Settings, verify kinematic triggers still fire.

**Player layer detection in PincerHitDetector:** Do not compare tag strings — use `LayerMask`. Define a `[SerializeField] LayerMask playerLayer` on `PincerHitDetector` or check against `PlayerRegistry.PlayerTransform` directly:
```
void OnTriggerEnter2D(Collider2D other):
  root = other.transform.root
  if root == PlayerRegistry.PlayerTransform:
    controller.HandlePlayerHit(root.gameObject)
```

**Sign-crossing false positive:** The click event fires on the closing stroke (sin going from positive to negative past the halfway point). The `halfPhase` guard (`(t×ω) mod 2π > π`) isolates the second half of the cycle. Without it, the zero-crossing on the opening stroke also triggers.

**Assembler guard — null sprite:** If `config.pincerSprite == null`, skip all pincer setup silently. Do not add `PincerController`. This allows centipedes configured without pincers to remain unaffected.

**Execution order:** `PincerController` runs at order `0` (same as `Ball`). It reads `Time.time` (not `FixedDeltaTime`), so animation timing is framerate-independent. `NodeWiggle` runs in `LateUpdate` and composes after the rotation is already set — no ordering conflict.

**DestroyPlayerEffect and null safety:** `Destroy(playerGO)` in Unity does not immediately set the reference to null within the same frame. Use a `destroyed` bool flag in `HandlePlayerHit`, or check `playerGO == null` using Unity's overloaded null check (which does detect pending-destroy objects).
