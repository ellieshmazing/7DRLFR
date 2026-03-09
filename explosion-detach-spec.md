# Explosion-Only Centipede Detachment

## Goal

Replace the centipede's displacement-based detachment system with explosion-radius-based detachment. When an ExplosiveBall explodes, any centipede Ball whose collider falls within the blast radius detaches from its centipede. This is the **only** detachment trigger — remove all per-frame displacement checks, SHM energy preemption, and the `detachDistance` config variable. The existing chain resolution logic (splitting centipedes, freeing solo balls, spawning reversed sub-centipedes) remains unchanged; only the *trigger* changes.

## Architecture Overview

```
FixedUpdate (CentipedeController, order 5):
  — REMOVED: no per-frame displacement check
  — REMOVED: no SHM energy preemption
  — CentipedeController.FixedUpdate body is now empty or removed

On ExplosiveBall collision (FireballEffect.OnCollision):
  1. Physics2D.OverlapCircleAll(hitPoint, explosionRadius)
  2. For each collider hit:
       if collider has Ball component AND ball.inCentipedeMode:
         add ball to affectedBalls set
  3. Group affectedBalls by their parent CentipedeController
  4. For each CentipedeController:
       call controller.DetachBalls(affectedBallSet)
  5. Apply explosion force impulses (existing behavior, unchanged)
  6. Tilemap destruction (existing behavior, unchanged)

CentipedeController.DetachBalls(affectedBalls):
  1. Build detach mask: for each ball in parallel lists, mark index if ball is in affectedBalls
  2. Run existing chain resolution (HandleDetachment logic) using that mask
     — find chains of surviving consecutive nodes
     — reparent chain heads
     — unparent and free detached balls
     — destroy detached skeleton nodes
     — spawn split sub-centipedes for chains of ≥2
     — free solo survivors as individual balls
```

## Behaviors

### Explosion Spatial Query

**Concept:** The existing `Physics2D.OverlapCircleAll` call in `FireballEffect.OnCollision` serves as the sole authority for which balls detach.

**Role:** Replaces per-frame displacement polling with a one-shot spatial query at explosion time.

**Logic:**
```
colliders = Physics2D.OverlapCircleAll(hitPoint, explosionRadius)
affectedBalls = empty list
for each collider in colliders:
  ball = collider.GetComponent<Ball>()  // or GetComponentInParent
  if ball != null AND ball.inCentipedeMode:
    affectedBalls.add(ball)
```

### Batch Grouping by Controller

**Concept:** Multiple balls from the same centipede may fall within one blast. They must be resolved as a single batch per centipede to avoid cascading partial splits.

**Role:** Groups affected balls by their owning CentipedeController, then issues one detach call per controller.

**Logic:**
```
controllerMap = empty dictionary<CentipedeController, list<Ball>>
for each ball in affectedBalls:
  controller = ball.linkedNode.root.GetComponent<CentipedeController>()
  if controller != null:
    controllerMap[controller].add(ball)

for each (controller, balls) in controllerMap:
  controller.DetachBalls(balls)
```

### Chain Resolution (Existing — Unchanged)

**Concept:** Given a set of marked indices in the parallel node/ball arrays, resolve surviving chains into sub-centipedes, free solo nodes, and clean up destroyed nodes.

**Role:** The core split/destroy/free logic. Already implemented in `HandleDetachment`. Only the *input* changes — instead of displacement-triggered marks, it receives explosion-triggered marks.

**Logic:**
```
// Build boolean mask from the provided ball set
for i in 0..balls.Count:
  if providedSet.contains(balls[i]):
    detached[i] = true

// Everything below is the existing HandleDetachment logic:
// - find consecutive chains of non-detached indices
// - reparent first node of non-zero chains to scene root
// - unparent all detached balls from skeleton hierarchy
// - destroy topmost detached skeleton nodes
// - call ball.Detach(springVelocity) on each detached ball
// - surviving chains of ≥2 → SpawnSplitCentipede (reversed)
// - solo survivors → free as individual balls
```

## Function Designs

### `FireballEffect.OnCollision(ball, collision) → void`
Handles explosion on impact. Now additionally triggers centipede detachment for any centipede ball within blast radius.

**Parameters:**
- `ball`: The Ball that collided (the explosive projectile)
- `collision`: Unity Collision2D data

**Side effects:** Detaches centipede balls in radius, applies explosion forces, destroys tilemap tiles, spawns VFX.

```
hitPoint = collision.GetContact(0).point

// --- NEW: Detach centipede balls in radius ---
colliders = Physics2D.OverlapCircleAll(hitPoint, explosionRadius)
controllerMap = new Dictionary<CentipedeController, List<Ball>>()
for each collider in colliders:
  hitBall = collider.GetComponent<Ball>()
  if hitBall != null AND hitBall.inCentipedeMode:
    controller = hitBall.linkedNode.root.GetComponent<CentipedeController>()
    if controller != null:
      controllerMap.GetOrAdd(controller).add(hitBall)

for each (controller, balls) in controllerMap:
  controller.DetachBalls(balls)

// --- EXISTING: explosion force, tilemap destruction, VFX ---
// (unchanged)
```

### `CentipedeController.DetachBalls(affectedBalls: IEnumerable<Ball>) → void`
Public method. Marks the given balls for detachment and runs the existing chain resolution.

**Parameters:**
- `affectedBalls`: Set of Ball references that should detach from this centipede.

**Side effects:** Destroys skeleton nodes, frees balls, may spawn split sub-centipedes, may destroy this centipede entirely.

```
// Build index mask
detachMask = new bool[balls.Count]
for i in 0..balls.Count:
  if affectedBalls.contains(balls[i]):
    detachMask[i] = true

if no indices marked:
  return  // nothing to do

// Delegate to existing chain resolution
HandleDetachment(detachMask)
```

### Removals

- **Remove** `CheckForDetachment()` — the per-frame displacement polling method
- **Remove** the SHM energy preemption logic (the `mass·v² ≥ stiffness·(D² − d²)` check) from `HandleDetachment`
- **Remove** `detachDistance` from `CentipedeConfig`
- **Remove** `CentipedeController.FixedUpdate` body (or the entire FixedUpdate if nothing else uses it)

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| `explosionRadius` | float | (existing value) | Radius of the Physics2D.OverlapCircle query. Controls how far the explosion reaches. Already exists on FireballEffect — no new variable needed. Try existing value first; increase for more dramatic chain reactions. |

No new variables are introduced. The removed variable `detachDistance` (previously on CentipedeConfig) is deleted.

## Implementation Notes

- **Detach before force:** `DetachBalls` must be called **before** the existing explosion force loop. This way, newly-freed balls are Dynamic when the force impulse hits them, and they receive a standard `AddForce(Impulse)` instead of needing the special `InjectSpringVelocity` kinematic path. This simplifies FireballEffect — the `inCentipedeMode` branch in the force loop can be removed since all affected balls are already Dynamic by the time force is applied.

- **Null safety on controller lookup:** A ball's `linkedNode` could theoretically be null if it was already detached by a prior explosion in the same frame. Guard with a null check.

- **HandleDetachment refactor:** The existing `HandleDetachment` currently builds its own detach mask from displacement checks. Refactor it to accept a `bool[] detachMask` parameter instead. The new `DetachBalls` builds the mask and passes it in. Remove the displacement-check and SHM-energy code that was previously inside `HandleDetachment`.

- **Execution order is no longer critical for CentipedeController:** Since there's no per-frame check, the `[DefaultExecutionOrder(5)]` on CentipedeController becomes irrelevant for detachment timing. However, keep it to avoid breaking other systems that might depend on the ordering.

- **Edge case — entire centipede destroyed:** If all balls in a centipede are within the blast, every index is marked. The chain resolution produces zero surviving chains. The existing logic already handles this (destroys all nodes, frees all balls, no sub-centipedes spawned).

- **Edge case — overlapping explosions:** If two ExplosiveBalls explode on the same frame, the second explosion's `DetachBalls` call may find that some balls are already detached (no longer `inCentipedeMode`). The spatial query filters these out naturally (`hitBall.inCentipedeMode` check).

- **`InjectSpringVelocity` removal opportunity:** After detachment happens first, centipede balls in the force loop are always Dynamic. The special `if kinematic → InjectSpringVelocity` branch can be removed — all bodies in the overlap just get `AddForce(Impulse)`. This simplifies FireballEffect's force application code.
