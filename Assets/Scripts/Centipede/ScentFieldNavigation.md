# Centipede Pathfinding: The Hunting Field
## Scent-Gradient Navigation — Competing Pathfinder Design

---

## Goal

The **ScentField Navigator** is a drop-in alternative to `CentipedePathfinder`. Where the arc
navigator plans explicit geometric paths, this system has no plan at all. The centipede navigates
by ascending a **spatiotemporal scent gradient** — a decaying field of the player's recent
positions. The path is not computed; it *emerges* from the physics of gradient-following.

The result is a different predator personality: deliberate, memory-driven, and territorial.

```
Every N seconds (scentSampleInterval):
  Player position → pushed into ScentField ring buffer with timestamp + weight = 1.0

Every FixedUpdate (per centipede):
  Compute numerical gradient of scent field at head position
  Advance sweep-and-lock phase oscillator
  Blend gradient direction into current momentum (weighted by oscillator sensitivity)
  If field is flat/empty → fall back to direct approach toward player
  Consume (suppress) scent near head to prevent backtracking
  Speed boost proportional to forward gradient strength
  Set rb.linearVelocity = momentum * speed

Every FixedUpdate (ScentField):
  Record new player position if sampleInterval has elapsed
```

The centipede head drives velocity. The body follows via the existing SkeletonNode trail.

---

## The Key Insight

This system intentionally avoids the word "navigation." The centipede does not know where the
player is. It only knows which direction *smells stronger*. That single local rule produces
complex global behavior — this is **emergent navigation**, not planned navigation.

The consumed-zone suppression is the heart of it: as the centipede moves, it erases the field
behind it. It cannot re-traverse the same path because the scent there is gone. On a stationary
player, this forces the centipede into a tight inward spiral — mathematically, the gradient
always points toward unconsumed field, which is always slightly further inward. The spiral
emerges without any spiral-planning code.

---

## Architecture: ScentField (Shared Singleton)

```
ScentField : MonoBehaviour  (singleton — DontDestroyOnLoad not needed; lives for scene lifetime)
  ├── Sample ring buffer: (position, timestamp, weight)[] of size scentHistorySize
  ├── Update(): push player position at scentSampleInterval
  ├── Evaluate(pos) → float: sum of time-decaying Gaussians at that world point
  └── Consume(pos, rate, radius, dt): reduce sample weights near pos

One ScentField shared by ALL centipedes in the scene.
Created lazily by ScentField.GetOrCreate() (called from CentipedeAssembler).
```

The field value at any point is:

```
Field(p) = Σ_i  weight_i · exp(−age_i / decayTime) · exp(−|p − pos_i|² / (2σ²))
```

Where:
- `weight_i` starts at 1.0 and is reduced by centipede consumption
- `age_i = currentTime − sample_i.timestamp`
- `decayTime` controls how long the scent persists
- `σ` (sigma) controls how far each sample's influence spreads

---

## Behavior 1: Scent Emission

### Concept
The player continuously leaves a scent trail. Implementation is a **ring buffer of world
positions with timestamps**. No spatial grid, no texture — just a list of point samples.
Each sample is a Gaussian blob in space that fades exponentially over time.

Sampling at a fixed interval (e.g. 0.1s) means 10 samples/second. With 200 samples stored,
the field holds 20 seconds of history. Older samples fall below perceptible weight and are
effectively dead (the ring buffer simply overwrites them).

### Emission Logic
```
ScentField.Update():
  if Time.time − lastSampleTime < sampleInterval: return
  samples[head] = { position: player.position, timestamp: Time.time, weight: 1.0 }
  head = (head + 1) % historySize
  count = min(count + 1, historySize)
  lastSampleTime = Time.time
```

---

## Behavior 2: Numerical Gradient Computation

### Concept
To know which direction to move, the centipede needs the **gradient of the scent field** — the
direction of steepest ascent. Rather than computing it analytically (which requires knowing the
closed-form field), it uses a **numerical approximation**: sample the field at 8 equally-spaced
points around the head at radius `scentGradientSampleRadius`, and compute the weighted sum of
their directions.

This is equivalent to convolving the field with a ring-shaped directional kernel, which gives a
smooth, rotation-invariant gradient estimate. It degrades gracefully when the field is nearly
flat (the sum approaches zero) rather than producing NaN or infinity.

### Gradient Computation
```
ComputeGradientDirection():
  gradient = Vector2.zero
  r = scentGradientSampleRadius
  for i in 0..7:
    angle = i * π/4    (0°, 45°, 90°, ... 315°)
    dir   = (cos(angle), sin(angle))
    val   = ScentField.Evaluate(head.position + dir * r)
    gradient += dir * val

  return normalize(gradient)  // or Vector2.zero if too small
```

Eight sample points cost 8 × O(scentHistorySize) field evaluations per centipede per frame —
roughly 1600 iterations at typical settings. Negligible cost.

---

## Behavior 3: Momentum-Blended Steering

### Concept
Raw gradient-following is jittery — the computed gradient direction changes each frame as the
field shifts and the centipede moves. Instead, the centipede has a **momentum vector** (its
current heading) that blends *toward* the gradient at a configurable rate. This creates physical
inertia: the centipede commits to a direction and turns gradually, like a real creature.

The blend rate is modulated by the sensitivity oscillator (Behavior 4), so the effective
turning speed pulses over time. During low-sensitivity phases the centipede is nearly ballistic;
during high-sensitivity phases it snaps toward the gradient decisively.

### Steering Update
```
FixedUpdate():
  sensitivity = 0.5 + 0.5 * sin(sensitivityPhase)    // [0, 1]

  gradient = ComputeGradientDirection()
  if gradient.magnitude > 0.001:
    blendRate = scentSteeringBlend * sensitivity
    momentum  = Lerp(momentum, gradient, blendRate * deltaTime)

  momentum = normalize(momentum)
  rb.linearVelocity = momentum * effectiveSpeed
```

---

## Behavior 4: Sweep-and-Lock Phase Oscillator

### Concept
Real predators do not continuously correct their heading. They sweep — maintain a heading for
a moment — then lock onto a new one. This creates the feeling of decisiveness and makes the
centipede feel dangerous rather than mechanical.

A simple sine wave oscillator modulates steering sensitivity between 0 and 1. During the low
half-cycle (sensitivity ≈ 0), momentum dominates and the centipede holds its heading. During
the high half-cycle (sensitivity ≈ 1), the gradient dominates and the centipede snaps toward
the freshest scent. The oscillator frequency is slow (0.2–0.5 Hz) — a full sweep-lock cycle
takes 2–5 seconds.

Each centipede starts with a random phase offset, so multiple centipedes are desynchronized.
This prevents the eerie "all centipedes turn at the same moment" behavior.

### Oscillator
```
sensitivityPhase += scentOscillationFrequency * 2π * deltaTime
sensitivity = 0.5 + 0.5 * sin(sensitivityPhase)     // initialized to Random.Range(0, 2π)
```

---

## Behavior 5: Consumed Zone Suppression

### Concept
This is the mechanism that generates spiral approach paths and prevents backtracking.

As the centipede passes through a region, it **suppresses the weight of nearby scent samples**.
The suppression is strongest at the centipede's center and falls off with distance. When the
centipede leaves a region, the scent there is partially or fully consumed — the field behind it
is weaker than ahead.

On a stationary player, the freshest (unconsumed) scent always lies in the region the centipede
hasn't visited yet. The gradient always points inward and slightly to the side of the centipede's
current path — a spiral emerges.

On a moving player, consumed zones act as **territory markers**: the region the centipede
recently passed through has lower signal, so it steers away from that region. Multiple
centipedes naturally spread out because each one degrades the field in its wake.

### Consumption
```
ScentField.Consume(headPos, rate, radius, dt):
  for each sample s:
    distSq = |headPos − s.position|²
    if distSq >= radius²: continue
    proximity = 1 − distSq / radius²    // 1 at center, 0 at edge
    s.weight = max(0, s.weight − rate * proximity * dt)
```

---

## Behavior 6: Speed Modulation by Gradient Strength

### Concept
A predator on a fresh trail moves fast. On a cold trail, it moves carefully. This simple rule
creates natural hunting rhythm without any explicit speed state machine.

Gradient strength is measured by evaluating the field one `scentGradientSampleRadius` ahead of
the centipede in its current momentum direction. High forward field strength = hot trail =
speed boost. This also creates a positive feedback loop: moving faster down a hot trail depletes
the field faster, which then reduces the boost — a natural speed regulator.

### Speed Computation
```
forwardFieldStrength = ScentField.Evaluate(headPos + momentum * scentGradientSampleRadius)
trailHeat   = clamp01(forwardFieldStrength / scentGradientMaxStrength)
effectiveSpeed = speed + scentSpeedBoost * trailHeat
```

---

## Behavior 7: Fallback Direct Approach

### Concept
When the scent field is empty (game start, player just spawned, scent fully consumed) the
gradient computation returns zero — the centipede has no meaningful direction signal. Rather
than stalling, it falls back to direct pursuit: weakly blending its momentum toward the player's
current world position.

This fallback is gentle (low blend rate) to avoid instant snapping. It represents the centipede
"sensing" the player through some weaker sense when it can't smell them. Once fresh scent
accumulates (a few seconds of player movement), the gradient signal takes over.

```
fieldAtHead = ScentField.Evaluate(headPos)
if fieldAtHead < scentFallbackThreshold and Target != null:
  toPlayer = normalize(Target.position − headPos)
  momentum = Lerp(momentum, toPlayer, scentFallbackBlend * deltaTime)
```

---

## Emergent Gameplay Dynamics

| Player Behavior | Centipede Response | Why |
|---|---|---|
| Stand still | Tight inward spiral, closes in inevitably | Unconsumed scent always ahead; centipede carves inward |
| Move in straight line | Followed at distance, may be cut off | Centipede follows trail; ahead-gradient pulls it forward |
| Reverse direction | Very dangerous — runs into centipede on own trail | Centipede already heading toward where you came from |
| Move in wide circles | Centipede orbits, gradually tightens | Trail curves; centipede follows the arc |
| Erratic movement | Centipede confuses itself, falls back to direct approach | Scattered gradient; consumed zones interfere |
| Stand behind cover | Centipede stalls at edge of scent cloud | Gradient is flat in covered region; fallback activates |

**Multiple centipedes**: each consumes the field in its wake, creating natural territory. They
spread out to attack from unconsumed directions without any explicit communication. Emergent
flanking.

---

## Function Designs

### `ScentField` (MonoBehaviour, singleton)

```
GetOrCreate() → ScentField   [static]
  If Instance != null: return Instance
  Create new GameObject("[ScentField]"), AddComponent<ScentField>()
  return new instance

Initialize(playerTransform, CentipedeConfig cfg)
  If already initialized (player != null): return   // reuse across centipedes
  Store player reference; copy historySize, sampleInterval, decayTime, sigma from cfg
  Allocate samples[historySize]; reset head/count/lastSampleTime

Update()
  If time since last sample < sampleInterval: return
  Write { player.position, Time.time, 1.0 } at samples[head]
  Advance head (ring); clamp count to historySize

Evaluate(Vector2 pos) → float
  t = Time.time; twoSigSq = 2 * sigma²
  sum = 0
  for each valid sample s:
    if s.weight < 0.001: skip
    temporal = exp(−(t − s.timestamp) / decayTime)
    spatial  = exp(−|pos − s.position|² / twoSigSq)
    sum += s.weight * temporal * spatial
  return sum

Consume(Vector2 pos, float rate, float radius, float dt)
  for each valid sample s:
    distSq = |pos − s.position|²
    if distSq >= radius²: skip
    proximity = 1 − distSq / radius²
    s.weight = max(0, s.weight − rate * proximity * dt)

Clear()
  count = 0; head = 0   // discard all samples (e.g., on player respawn)
```

### `ScentFieldNavigator` (MonoBehaviour, [DefaultExecutionOrder(-5)])

```
Initialize(CentipedeConfig cfg, Transform playerTarget, ScentField scentField)
  Store config, target, field; get Rigidbody2D
  Set rb.gravityScale = 0, rb.constraints = FreezeRotation
  Set momentum = random unit vector
  Set sensitivityPhase = Random.Range(0, 2π)

FixedUpdate()
  Tick collisionCooldown
  Advance sensitivityPhase; compute sensitivity = 0.5 + 0.5 * sin(sensitivityPhase)
  gradient = ComputeGradientDirection()
  If gradient valid: Lerp momentum toward gradient at (scentSteeringBlend * sensitivity * dt)
  If field at head < scentFallbackThreshold: Lerp momentum toward player at (scentFallbackBlend * dt)
  Normalize momentum
  Compute forwardFieldStrength; compute effectiveSpeed with boost
  Consume field at head position
  rb.linearVelocity = momentum * effectiveSpeed

ComputeGradientDirection() → Vector2
  gradient = Vector2.zero
  for i in 0..7:
    dir = (cos(i * π/4), sin(i * π/4))
    gradient += dir * field.Evaluate(headPos + dir * scentGradientSampleRadius)
  return normalize(gradient) or Vector2.zero if flat

NotifyCollision()
  If collisionCooldown > 0: return
  collisionCooldown = collisionCooldownDuration
  If rb.linearVelocity not negligible: momentum = -normalize(rb.linearVelocity)

OnCollisionEnter2D(col) → NotifyCollision()
```

---

## Modifiable Variables (CentipedeConfig additions)

| Variable | Type | Default | Description |
|---|---|---|---|
| `useScentNavigator` | bool | false | Swap CentipedePathfinder for ScentFieldNavigator on this config |
| `scentHistorySize` | int | 200 | Ring buffer capacity; 200 × 0.1s = 20 seconds of trail |
| `scentSampleInterval` | float | 0.1 | Seconds between player position samples |
| `scentDecayTime` | float | 8.0 | Time constant (seconds) for scent weight to decay to 37% |
| `scentSigma` | float | 1.5 | Gaussian spatial spread (world units); affects how "wide" each sample's influence is |
| `scentGradientSampleRadius` | float | 0.8 | Radius at which 8 gradient samples are taken around the head |
| `scentSteeringBlend` | float | 4.0 | Turn-rate factor (blends/sec at full sensitivity); lower = wider, lazier turns |
| `scentConsumeRadius` | float | 0.6 | World-unit radius within which passing consumes scent |
| `scentConsumeRate` | float | 2.0 | Weight consumed per second at the centipede's center |
| `scentOscillationFrequency` | float | 0.35 | Sweep-and-lock oscillator frequency in Hz; full cycle = 1/freq seconds |
| `scentSpeedBoost` | float | 1.0 | Max speed added when on a hot trail (world units/sec) |
| `scentGradientMaxStrength` | float | 5.0 | Field strength at forward sample that yields full speed boost |
| `scentFallbackThreshold` | float | 0.05 | Field strength at head below which fallback direct approach activates |
| `scentFallbackBlend` | float | 0.8 | Blend rate toward player during fallback (weaker than gradient steering) |

---

## Implementation Notes

- **Shared field**: All centipedes query the same `ScentField.Instance`. This is intentional —
  consumption by one affects what others perceive, producing territory dynamics. `GetOrCreate()`
  handles lazy initialization so the assembler doesn't need a scene reference.

- **Field parameters are per-scene**: `scentHistorySize`, `scentSampleInterval`, `scentDecayTime`,
  and `scentSigma` are read once from the **first** centipede's config that initializes the field.
  Subsequent centipedes reuse the existing field. For a scene with mixed centipede types, give
  these parameters only to the first config; later configs just tune navigation parameters.

- **Sigma tuning**: `scentSigma` controls the effective "smell radius." At σ = 1.5wu, a sample at
  distance 3wu has ~14% influence; at 4.5wu it's ~1%. Set σ to roughly half the game arena
  width to ensure the centipede can smell the player from anywhere in typical scenarios.

- **Collision response is identical** to `CentipedePathfinder`: momentum inverts, collisionCooldown
  engages. The `NotifyCollision()` method has the same signature — Ball collision callbacks
  that already call `GetComponent<CentipedePathfinder>()?.NotifyCollision()` should be updated
  to be type-agnostic (e.g., `GetComponent<ScentFieldNavigator>()?.NotifyCollision()`), or
  refactored to an interface.

- **No arc or replan timer**: Unlike the arc pathfinder, there is no replan event. The system
  is fully reactive — it evaluates the field every frame. This means it responds instantly to
  player position changes with no dead zones between replans.

- **Player respawn**: Call `ScentField.Instance.Clear()` after respawning the player to erase
  the old trail. Otherwise the centipede may initially move toward the ghost of the old position.

- **Debug visualization**: `ScentField.Evaluate()` can be called from `OnDrawGizmos()` to
  render a heat map of the field. Sample a regular grid, map each value to a color, draw
  `Debug.DrawRay` upward to show field height. Disable in builds.
