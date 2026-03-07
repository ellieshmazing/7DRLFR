# Centipede Pathfinding Design

## Goal
The centipede is an autonomous NPC whose sole objective is to reach and damage the player. It navigates using a **steering behavior** approach — not graph-based pathfinding (no navmesh, no A*). Instead, it follows pre-computed geometric arc paths that are periodically replanned, with a sinusoidal lateral offset applied on top to produce organic wriggling motion.

---

## Architecture Overview

```
Every frame:
  Advance position along current arc
  Apply sinusoidal lateral offset perpendicular to arc tangent
  Output: target world position for the centipede head

Every N seconds (with random jitter):
  Replan: generate a new circular arc from head to player
  Validate arc radius ≥ minTurnRadius; reject and retry if not

On collision:
  Abandon current arc
  Invert current heading
  Immediately replan from new heading
```

The centipede head chases this computed target position. The body follows via the existing SkeletonNode trail system.

---

## Behavior 1: Circular Arc Path

### Concept
Two points (centipede head, player) can be connected by infinitely many circular arcs. We constrain the choice by specifying the **entry angle** — the direction the arc departs from the head. This angle is randomized within a configurable range, producing varied approach directions that feel dynamic.

The centipede advances along the arc by incrementing an angle parameter `t` each frame. Speed along the arc is constant: `deltaAngle = speed / radius` per second (arc length = angle × radius).

### Path Generation
Given:
- `headPos` — current head world position
- `playerPos` — target world position
- `entryAngle` — randomized departure direction (see variables)

Compute:
1. Midpoint of the chord from `headPos` to `playerPos`
2. Perpendicular bisector of that chord
3. Circle center = point on perpendicular bisector such that the arc departs `headPos` at `entryAngle`
4. Derive `arcRadius`, `startAngle`, `endAngle`, `arcDirection` (CW or CCW)

If `arcRadius < minTurnRadius`, reject this arc and resample `entryAngle`.

### Path Following
Each frame, advance `currentAngle` toward `endAngle` at rate `speed / arcRadius`. Sample the arc position:

```
arcPosition = center + arcRadius * Vector2(cos(currentAngle), sin(currentAngle))
```

Arc tangent (needed for offset and heading):
```
arcTangent = perpendicular to (arcPosition - center), signed by arc direction
```

When `currentAngle` reaches `endAngle`, the arc is complete — trigger a replan.

---

## Behavior 2: Sinusoidal Lateral Offset

### Concept
The arc gives a clean geometric path. A sine wave applied perpendicular to the arc tangent produces organic wriggling. This offset is applied to the head's *target position* — it steers the physical path the centipede takes, not just a visual effect.

The wave travels down the body: each body node reads the same sine function but offset by a per-node phase. This makes the wave appear to propagate from head to tail, like a real worm.

### Per-Frame Calculation
At the head:
```
wavePhase += waveFrequency * deltaTime
lateralOffset = sin(wavePhase) * waveAmplitude
headTarget = arcPosition + arcTangent.Perpendicular() * lateralOffset
```

For body node at index `i`:
```
nodePhase = wavePhase - (i * wavePhaseOffsetPerNode)
nodeLateralOffset = sin(nodePhase) * waveAmplitude
```

Body nodes apply this offset relative to their position on the trail — the SkeletonNode system provides their base position.

---

## Behavior 3: Periodic Replanning

### Concept
Replanning every frame is wasteful and produces jitter. A fixed timer with random jitter gives the centipede a natural "reaction delay" and prevents multiple centipedes from replanning on the same frame.

Replanning is also triggered early by:
- Arc completion (head reached the target zone)
- Collision with an obstacle

### Replan Trigger Logic
```
replanTimer -= deltaTime
if replanTimer <= 0:
    Replan()
    replanTimer = replanInterval + Random(-replanJitter, replanJitter)
```

---

## Behavior 4: Collision Response

### Concept
The centipede does **not** attempt to avoid obstacles during planning. It generates arcs as if the world is empty. When a collision occurs (detected via Unity physics callback), the centipede abandons its current arc, inverts its current heading, and immediately replans. This produces emergent, unpredictable behavior at obstacles.

### On Collision
```
OnCollisionEnter2D:
    Invert currentHeading (flip the departure angle by 180°)
    CancelCurrentArc()
    Replan() using currentHeading as the new entry angle constraint
    Reset replanTimer
```

Note: The centipede may briefly collide with the same object multiple times before clearing it. A short **collision cooldown** (e.g., 0.5s) prevents rapid flip-flopping.

---

## Behavior 5: Minimum Turn Radius

### Concept
Constraining arc radius prevents the centipede from making hairpin turns that cause the body to overlap itself. During arc generation, any arc whose radius falls below `minTurnRadius` is rejected and a new `entryAngle` is sampled.

This also has an emergent effect: the centipede approaches the player in wide sweeping arcs, which is more visually interesting and gives the player more time to react.

### Enforcement
During `Replan()`:
```
for attempt in 0..maxReplanAttempts:
    entryAngle = SampleEntryAngle()  // randomized
    arc = ComputeArc(headPos, playerPos, entryAngle)
    if arc.radius >= minTurnRadius:
        AcceptArc(arc)
        return
// fallback: accept best arc found, or use a straight approach
```

---

## Function Designs

### `CentipedePathfinder` (MonoBehaviour on centipede head GO)

```
Initialize(Transform target)
  Store reference to player transform
  Replan() immediately

Update() or FixedUpdate()
  Tick replan timer
  Advance arc parameter
  Compute arcPosition and arcTangent
  Compute headTarget with sinusoidal offset
  Output headTarget to centipede movement system

Replan()
  Sample entryAngle from [baseAngleToPlayer + Random(-arcAngleVariance, arcAngleVariance)]
  Attempt arc generation up to maxReplanAttempts
  Accept first valid arc (radius >= minTurnRadius)
  Reset replanTimer with jitter

ComputeArc(Vector2 from, Vector2 to, float entryAngle) → Arc
  Returns: center, radius, startAngle, endAngle, direction (CW/CCW)
  Derivation via perpendicular bisector + tangent constraint

AdvanceArc(float deltaTime)
  currentAngle += arcDirection * (speed / arcRadius) * deltaTime
  if arc complete: Replan()

GetHeadTarget() → Vector2
  arcPos = SampleArc(currentAngle)
  tangent = ArcTangent(currentAngle)
  return arcPos + tangent.Perpendicular() * sin(wavePhase) * waveAmplitude

OnCollisionEnter2D(Collision2D col)
  if collisionCooldown > 0: return
  collisionCooldown = collisionCooldownDuration
  InvertHeading()
  Replan()

GetBodyWaveOffset(int nodeIndex) → float
  phase = wavePhase - nodeIndex * wavePhaseOffsetPerNode
  return sin(phase) * waveAmplitude
```

---

## Modifiable Variables (ScriptableObject fields)

| Variable | Type | Default | Description |
|---|---|---|---|
| `speed` | float | 3.0 | Arc traversal speed in world units/sec |
| `minTurnRadius` | float | 1.5 | Minimum allowed arc radius; prevents hairpin turns |
| `arcAngleVariance` | float | 60° | ± random range of arc entry angle from direct approach |
| `replanInterval` | float | 2.0 | Seconds between path recalculations |
| `replanJitter` | float | 0.4 | Random ± added to replan interval each cycle |
| `maxReplanAttempts` | int | 8 | Max arc generation retries before accepting fallback |
| `waveAmplitude` | float | 0.4 | Lateral peak displacement of sinusoidal wave |
| `waveFrequency` | float | 2.0 | Wave oscillations per second |
| `wavePhaseOffsetPerNode` | float | 0.3 | Phase shift per body node so the wave travels tail-ward |
| `collisionCooldownDuration` | float | 0.5 | Seconds before another collision response can trigger |
| `targetArrivalRadius` | float | 0.5 | Distance to player that counts as "arc complete" |

---

## Notes for Implementation

- **Arc math**: The perpendicular bisector approach is the cleanest derivation. The circle center lies on the bisector; the tangent constraint pins which point on the bisector is the center. Expect to solve a small system of two equations.
- **Arc direction**: CW vs CCW is determined by the sign of the cross product between the chord direction and the entry tangent. This determines whether `currentAngle` increments or decrements.
- **Wave and arc are independent systems**: The arc handles steering toward the target. The sine wave purely adds lateral flair. They can be tuned separately.
- **Body node offsets**: `GetBodyWaveOffset(nodeIndex)` is a read-only query — the body system can call it per-node without the pathfinder needing any reference to body nodes.
- **Fallback arc**: If all replan attempts fail (e.g., very short distance to player), fall back to a straight line (infinite radius arc) or a minimal-curvature arc at exactly `minTurnRadius`.
