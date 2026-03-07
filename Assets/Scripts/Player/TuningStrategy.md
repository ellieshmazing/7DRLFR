# Tuning System — Runtime Variable Optimizer

## Goal

A runtime tuning system that lets the developer efficiently discover optimal variable values for BallWorld's Player and Centipede systems by *playing the game*. The system replaces raw stiffness/damping spring parameters with perceptually meaningful frequency/dampingRatio pairs (reducing coupled knobs), groups ~50 variables into 19 ordered tuning dimensions, and provides a multi-phase workflow: continuous sweep to bracket viable ranges, A/B forced-choice comparison to converge on preferred values, and cross-validation to catch interaction effects. Tuned values write directly to ScriptableObject assets. Named profiles are saved as SO duplicates in organized subfolders. A visual overlay shows current tuning state including a computed spring step-response waveform. Init-only variables trigger automatic entity respawning when changed.

Dimensions 1–14 apply to both pathfinder modes. When `useScentNavigator == true`, dimensions 12–13 (arc-specific: `minTurnRadius`, `arcAngleVariance`, `replanInterval`) are skipped automatically, and dimensions 15–19 (scent-specific) are appended. When using the arc pathfinder, dimensions 15–19 are skipped.

---

## Architecture Overview

```
TuningManager (MonoBehaviour, editor/dev-only singleton)
├── Holds: PlayerConfig ref, CentipedeConfig ref, PlayerAssembler ref, CentipedeAssembler ref
├── Holds: TuningDimensionDef[] (ordered array of SO assets defining each dimension)
├── State machine: Idle → Sweep → AB → CrossValidation → Complete
├── Active: dimensionIndex, variableIndexWithinDimension
├── Sweep state: elapsed, loggedValues[]
├── AB state: rangeLo, rangeHi, valueA, valueB, swapTimer, showingA, roundCount
└── Overlay: TuningOverlay component on a screen-space Canvas

Every Update (tuning active):
  Poll Keyboard.current for:
    - Toggle tuning on/off
    - Next/prev dimension
    - Log value (sweep), lock range (sweep→AB), choose A/B (AB phase)
    - Save/load profile
    - Adjust sweep speed

  If phase == Sweep:
    Advance sine interpolation → compute value from range
    Write value to config SO field (via reflection)
    If variable marked liveSync:
      Also update live MonoBehaviour instances (via cached references)
    If variable marked requiresRespawn:
      Queue deferred respawn
    On log key → append current value to loggedValues
    On lock key → compute bracket from logged values → transition to AB

  If phase == AB:
    Advance swap timer
    On timer expire → toggle A↔B, apply new value
    On choice key → narrow range via ternary search
    If range < epsilon → mark variable done, advance

  If phase == CrossValidation:
    Generate perturbed full-config variants
    Run round-robin AB: base vs each variant
    Adopt preferred variants

Every LateUpdate (tuning active):
  Update TuningOverlay:
    - Dimension name, phase label, variable name + value + progress bar
    - If spring dimension: render step-response waveform from ω, ζ
    - Control hint strip
    - Profile name + dirty indicator

On dimension complete:
  Apply final value to config SO, mark SO dirty
  Advance to next dimension

On profile save:
  #if UNITY_EDITOR: clone config SO → Assets/Configs/Tuning/{Entity}/

On profile load:
  Copy all fields from profile SO to active config SO
  Trigger respawn if any init-only field differs
```

---

## Behaviors

### Spring Parameterization Refactor

**Concept:** Second-order system canonical form — store natural frequency (ω) and damping ratio (ζ) instead of raw stiffness (k) and damping coefficient (c), so spring feel is decoupled from mass.

**Role:** Reduces each spring from 3 arbitrary knobs to 2 perceptually orthogonal ones. Changing mass for collision tuning no longer alters spring feel. Cuts the spring search space by one third.

**Logic:**
```
// Stored in config SO (replaces old stiffness + damping fields):
frequency: float       // ω — natural frequency (rad/s). Higher = snappier.
dampingRatio: float    // ζ — 0 = perpetual bounce, 1 = critically damped, >1 = overdamped.
mass: float            // m — kept explicit; affects collision response independently.

// Computed properties on the config SO (consumed by scripts):
Stiffness = frequency * frequency * mass           // k = ω²m
Damping   = 2.0 * dampingRatio * frequency * mass  // c = 2ζωm

// Invariant: changing mass recomputes k and c such that ω and ζ stay constant.
// The spring FEELS identical; only its response to external forces changes.
```

Seven spring systems adopt this pattern:
- **PlayerConfig**: torso, hip, foot (3 triads)
- **CentipedeConfig**: wiggle (1 triad)
- Ball.cs and NodeWiggle.cs continue to receive computed stiffness/damping values — their code does not change.

Default conversions from current values:
```
Torso:     ω = √(80/1)   ≈ 8.94,  ζ = 5 / (2√(80·1))   ≈ 0.28
Hip:       ω = √(120/1)  ≈ 10.95, ζ = 10 / (2√(120·1))  ≈ 0.46
Foot:      ω = √(60/0.5) ≈ 10.95, ζ = 8 / (2√(60·0.5))  ≈ 0.73
Centipede: ω = √(80/1)   ≈ 8.94,  ζ = 5 / (2√(80·1))    ≈ 0.28
```

---

### Live Config Reading Refactor

**Concept:** Direct config-SO reads per frame — scripts hold a reference to their config SO and read tunable values from it each FixedUpdate/LateUpdate, instead of caching values in local fields set once at spawn.

**Role:** Makes per-frame variables instantly responsive to SO changes during tuning, without needing the tuning system to track and update individual MonoBehaviour instances.

**Logic:**
```
// Before (current pattern — assembler copies value once):
assembler sets: hipNode.stiffness = config.hipWiggleStiffness
hipNode reads:  stiffness (local field) each FixedUpdate

// After (tuning-ready pattern — script reads from SO each frame):
assembler sets: hipNode.config = config
hipNode reads:  config.HipStiffness (computed property) each FixedUpdate
```

Scripts that need this refactor (per-frame readers):
| Script | Fields to read from config | Config SO |
|---|---|---|
| `PlayerSkeletonRoot` | moveForce, maxSpeed, standHeight, jumpSpeed, jumpOffsetFactor | PlayerConfig |
| `PlayerHipNode` | HipStiffness, HipDamping, hipMass | PlayerConfig |
| `PlayerFeet` | FootStiffness, FootDamping, footMass, footSpreadX | PlayerConfig |
| `NodeWiggle` (torso) | TorsoStiffness, TorsoDamping, torsoMass | PlayerConfig |
| `Ball` (centipede) | WiggleStiffness, WiggleDamping, wiggleMass | CentipedeConfig |
| `CentipedeController` | detachDistance | CentipedeConfig |

For NodeWiggle (used in both Player and Centipede contexts): add a generic `ISpringConfig` interface with `Stiffness`, `Damping`, `Mass` properties, or simply keep the local-field approach for NodeWiggle and have the tuning system dual-write to both the SO and the live instance. The dual-write path is pragmatic since there's exactly one torso NodeWiggle per player.

Init-only variables (footMass → rb.mass, footGravityScale → rb.gravityScale, nodeRadius, pathfinding params) remain set-once. The tuning system triggers auto-respawn when these change.

**Additional refactor**: Move `jumpSpeed` and `jumpOffsetFactor` from PlayerAssembler into PlayerConfig. They are player feel parameters and belong alongside moveForce and standHeight. PlayerAssembler then reads them from config during Spawn(), and PlayerSkeletonRoot reads them per-frame from config.

---

### Tuning Dimension Registry

**Concept:** Coordinate descent — group coupled variables into perceptual dimensions, tune one dimension at a time in dependency order.

**Role:** Structures ~40 raw variables into 14 cognitively tractable tuning passes. Each dimension isolates one axis of feel.

**Logic:**
```
TuningDimensionDef (ScriptableObject):
  name: string                     // e.g. "Foot Physics"
  testScenario: string             // e.g. "Drop from height, jump, watch arc"
  variables: TuningVariable[]
  sweepDuration: float             // seconds for one min→max sine cycle

TuningVariable (serializable struct):
  targetConfig: ScriptableObject   // PlayerConfig or CentipedeConfig asset ref
  fieldName: string                // reflection target, e.g. "moveForce"
  min: float
  max: float
  defaultValue: float
  requiresRespawn: bool            // true for rb.mass, collider setup, pathfinder params
  liveSync: bool                   // true if live MonoBehaviour instances need dual-write

TuningManager.dimensionDefs: TuningDimensionDef[]  // ordered array, set in Inspector
```

Within a multi-variable dimension, variables are tuned sequentially (coordinate descent within the dimension). When variable 0 is finalized, variable 1 begins its sweep.

---

### Sweep Phase

**Concept:** Continuous parametric interpolation with manual logging — sine-wave oscillation between range endpoints at a pace slow enough to perceive changes.

**Role:** Quickly identifies the "plausible band" for each variable by feeling the transitions, then brackets the range for convergence.

**Logic:**
```
On enter sweep for variable V:
  sweepElapsed = 0
  loggedValues = []

Each Update while sweeping:
  sweepElapsed += deltaTime * sweepSpeedMultiplier
  // Sine gives smooth reversals at endpoints (no jarring snap)
  normalized = (sin(sweepElapsed * 2π / V.sweepDuration - π/2) + 1) / 2
  value = lerp(V.min, V.max, normalized)
  ApplyValue(V, value)

  if logKey pressed:
    loggedValues.append(value)
    // Brief screen flash or sound cue confirms the log

  if lockKey pressed:
    if loggedValues.count >= 2:
      spread = max(loggedValues) - min(loggedValues)
      padding = spread * 0.15
      bracketMin = clamp(min(loggedValues) - padding, V.min, V.max)
      bracketMax = clamp(max(loggedValues) + padding, V.min, V.max)
    else:
      // Not enough logs — use full range
      bracketMin = V.min
      bracketMax = V.max
    transition to AB phase with range [bracketMin, bracketMax]
```

---

### A/B Convergence Phase

**Concept:** Ternary search via forced-choice pairwise comparison — the most reliable method for subjective feel evaluation because relative judgments ("A or B?") are far more accurate than absolute ones.

**Role:** Converges on the preferred value within the bracketed range in ~6–10 rounds per variable.

**Logic:**
```
On enter AB for variable V with range [lo, hi]:
  epsilon = (V.max - V.min) * abEpsilon   // fraction of TOTAL range
  A = lo + (hi - lo) / 3
  B = lo + 2 * (hi - lo) / 3
  swapTimer = 0
  showingA = true
  roundCount = 0
  ApplyValue(V, A)

Each Update:
  swapTimer += deltaTime
  if swapTimer >= abSwapInterval:
    swapTimer = 0
    showingA = !showingA
    ApplyValue(V, showingA ? A : B)

  if chooseAKey pressed:
    hi = B              // eliminate upper third
    CommitChoice()
  if chooseBKey pressed:
    lo = A              // eliminate lower third
    CommitChoice()

CommitChoice():
  roundCount++
  if (hi - lo) < epsilon:
    finalValue = (lo + hi) / 2
    ApplyValue(V, finalValue)
    AdvanceToNextVariable()   // next var in dimension, or next dimension
  else:
    A = lo + (hi - lo) / 3
    B = lo + 2 * (hi - lo) / 3
    swapTimer = 0
    showingA = true
    ApplyValue(V, A)
```

---

### Cross-Validation Phase

**Concept:** Perturbation testing — compare the composite tuned result against random ±N% variations to catch interaction effects missed by per-dimension tuning.

**Role:** Safety net for coordinate descent. Catches cases where individually-optimal values don't compose well.

**Logic:**
```
On enter cross-validation:
  baseSnapshot = snapshot all tuned fields from active config SOs
  perturbedVariants = []

  for i in 0..crossValVariants:
    variant = clone(baseSnapshot)
    // Only perturb variables within the same entity (Player OR Centipede)
    for each tuned variable V in current entity:
      offset = random(-crossValPerturbation, +crossValPerturbation) * V.value
      variant[V] = clamp(V.value + offset, V.min, V.max)
    perturbedVariants.append(variant)

  // Round-robin comparison: base vs each variant
  for each variant:
    Apply base, play for abSwapInterval seconds
    Apply variant, play for abSwapInterval seconds
    Wait for user choice (1 = keep base, 2 = adopt variant)
    if variant preferred: base = variant

  Apply final base to config SOs
  Mark tuning complete
```

This phase is optional — the user can skip it if satisfied with the per-dimension results.

---

### Profile Management

**Concept:** ScriptableObject duplication for named configuration snapshots, with organized subfolder storage.

**Role:** Preserves tuning results as reusable assets. Enables A/B comparison between named feel identities (e.g., "Snappy" vs "Floaty"). Provides undo safety for SO modifications during Play Mode.

**Logic:**
```
Folder structure:
  Assets/Configs/Tuning/
    Player/
      PlayerConfig_Snappy.asset
      PlayerConfig_Heavy.asset
    Centipede/
      CentipedeConfig_Aggressive.asset
      CentipedeConfig_Relaxed.asset

SaveProfile(configSO, name, entityType):
  #if UNITY_EDITOR
  subfolder = "Assets/Configs/Tuning/" + entityType + "/"
  EnsureDirectoryExists(subfolder)
  path = subfolder + configSO.name + "_" + name + ".asset"
  clone = ScriptableObject.Instantiate(configSO)
  AssetDatabase.CreateAsset(clone, path)
  AssetDatabase.SaveAssets()
  #endif

LoadProfile(profilePath, activeConfig):
  #if UNITY_EDITOR
  profile = AssetDatabase.LoadAssetAtPath(profilePath)
  EditorUtility.CopySerialized(profile, activeConfig)
  EditorUtility.SetDirty(activeConfig)
  // Check if any init-only fields differ → trigger respawn
  #endif

CycleProfiles(direction):
  // Scan subfolder for all .asset files matching the config type
  // Cycle forward/backward through the list
  // Load the selected profile
```

---

### Visual Overlay

**Concept:** Runtime feedback HUD — on-screen display of tuning state plus a computed step-response waveform that visualizes what the current spring parameters *do*.

**Role:** Provides cognitive grounding so the developer can see the relationship between numbers and behavior. The waveform makes spring dimensions especially intuitive — you see the overshoot, settle time, and frequency before you even feel them.

**Logic:**
```
Screen-space Canvas overlay (anchored upper-right, semi-transparent panel)

Layout:
  ┌─────────────────────────────────────────┐
  │ TUNING: Hip Spring           [SWEEP]    │
  │                                         │
  │ hipFrequency: 10.95  [5 ═══■════ 25]   │
  │ hipDampingRatio: ——  (next)             │
  │                                         │
  │ ┌──────────────────┐                    │
  │ │     ╱‾‾‾‾‾‾‾‾    │  ω = 10.95        │
  │ │   ╱               │  ζ = 0.46         │
  │ │  ╱                │                   │
  │ └──────────────────┘                    │
  │                                         │
  │ Test: Land from jump, watch torso bob   │
  │                                         │
  │ Profile: Default  [MODIFIED]            │
  │ [Tab]Next [Space]Log [Enter]Lock [1/2]  │
  └─────────────────────────────────────────┘

  In AB phase, replace progress bar with:
  │ hipFrequency: 10.95  [A ■ · · B]  3.0s │
  │                        ▲ showing A      │

Waveform rendering:
  Compute 64-sample step response from current ω and ζ (see SampleStepResponse)
  Draw as a UI polyline (LineRenderer on a RawImage, or GL.Lines in OnPostRender)
  Y-axis: 0 to 1.3 (accommodates underdamped overshoot)
  X-axis: 0 to 4/(ζω) seconds (4 time constants)
  Draw horizontal line at y = 1.0 (equilibrium reference)

Overlay is ONLY visible when tuning is active. Does not render during normal play.
```

---

### Auto-Respawn

**Concept:** Transparent entity reconstruction — when an init-only variable changes, destroy the entity and respawn it at its current position so the new value takes effect.

**Role:** Eliminates scene restart when tuning variables like footMass, nodeRadius, or pathfinding speed. Keeps the tuning flow uninterrupted.

**Logic:**
```
When ApplyValue sets a field where requiresRespawn == true:
  Determine entity type from the config SO type
  Set respawnPending flag (debounce: wait until frame end to batch multiple changes)

At end of frame (via coroutine or LateUpdate gate):
  if respawnPending:
    if entityType == Player:
      player = FindAnyObjectByType<PlayerSkeletonRoot>()
      pos = player.transform.position
      Destroy(player.gameObject)
      yield return null   // wait one frame for Destroy to complete
      assembler.Spawn(config, pos)

    if entityType == Centipede:
      foreach root in FindObjectsByType<CentipedeController>():
        if root.config == targetConfig:
          pos = root.transform.position
          Destroy(root.gameObject)
      yield return null
      // Respawn each destroyed centipede
      foreach recorded (pos, config):
        centipedeAssembler.Spawn(config, pos)

    respawnPending = false
```

---

## Function Designs

### `SpringParams.ComputeStiffness(frequency: float, mass: float) → float`
Derives spring constant k from natural frequency and mass.

**Parameters:**
- `frequency` — ω in rad/s
- `mass` — spring simulation mass

**Returns:** k = ω² × m

```
return frequency * frequency * mass
```

---

### `SpringParams.ComputeDamping(frequency: float, dampingRatio: float, mass: float) → float`
Derives damping coefficient c from natural frequency, damping ratio, and mass.

**Parameters:**
- `frequency` — ω in rad/s
- `dampingRatio` — ζ (1.0 = critically damped)
- `mass` — spring simulation mass

**Returns:** c = 2ζωm

```
return 2f * dampingRatio * frequency * mass
```

---

### `TuningManager.ApplyValue(variable: TuningVariable, value: float) → void`
Writes a value to the config SO and, if needed, syncs to live instances or queues respawn.

**Parameters:**
- `variable` — the TuningVariable definition (contains SO ref, field name, flags)
- `value` — the float value to set

**Side effects:** Modifies the SO field via reflection. May modify live MonoBehaviour fields. May queue a deferred respawn.

```
// 1. Write to SO (persistent)
field = variable.targetConfig.GetType().GetField(variable.fieldName)
field.SetValue(variable.targetConfig, value)
#if UNITY_EDITOR
EditorUtility.SetDirty(variable.targetConfig)
#endif

// 2. Live sync for per-frame variables that still use local fields
//    (e.g. NodeWiggle, which reads local stiffness/damping/mass)
if variable.liveSync:
  SyncLiveInstances(variable)

// 3. Queue respawn for init-only variables
if variable.requiresRespawn:
  QueueRespawn(variable.targetConfig)
```

---

### `TuningManager.SyncLiveInstances(variable: TuningVariable) → void`
Updates live MonoBehaviour instance fields to match the current config SO value. Used only for scripts that haven't been refactored to read from config per-frame (e.g., NodeWiggle).

**Parameters:**
- `variable` — contains the SO ref and field name

**Side effects:** Modifies fields on live MonoBehaviour instances via a lookup table. The lookup table maps (configSOType, fieldName) → (componentType, instanceFieldName).

```
// Example mapping:
// (PlayerConfig, "torsoFrequency") → find NodeWiggle on TorsoVisual,
//     set stiffness = config.TorsoStiffness, damping = config.TorsoDamping

// For spring frequency/dampingRatio changes, the derived stiffness AND damping
// must both be recomputed and written to the live instance, because both
// depend on frequency.

lookup = syncMappings[(variable.targetConfig.GetType(), variable.fieldName)]
foreach mapping in lookup:
  instance = FindLiveComponent(mapping.componentType, mapping.identifier)
  field = mapping.componentType.GetField(mapping.instanceFieldName)
  field.SetValue(instance, mapping.computeValue(variable.targetConfig))
```

The sync mapping table is initialized once at TuningManager.Awake and cached. It handles the spring parameterization indirection (SO stores frequency → live instance receives computed stiffness).

---

### `OverlayRenderer.SampleStepResponse(frequency: float, dampingRatio: float, sampleCount: int) → float[]`
Computes an analytical step-response curve for the overlay waveform display.

**Parameters:**
- `frequency` — ω in rad/s
- `dampingRatio` — ζ
- `sampleCount` — number of samples (typically 64)

**Returns:** Array of y-values (0 = rest, 1 = equilibrium; may exceed 1 for underdamped overshoot).

```
// Time span: enough to see ~4 oscillation cycles or 4 time constants
if dampingRatio < 0.1:
  T = 8π / frequency                    // low damping: show several oscillations
else:
  T = 4.0 / (dampingRatio * frequency)  // normal: ~4 time constants to settle

dt = T / sampleCount
samples = new float[sampleCount]

for i in 0..sampleCount:
  t = i * dt
  if abs(dampingRatio - 1.0) < 0.001:
    // Critically damped — special case to avoid sqrt(0)
    samples[i] = 1 - (1 + frequency * t) * exp(-frequency * t)
  else if dampingRatio < 1.0:
    // Underdamped — oscillates toward equilibrium
    ωd = frequency * sqrt(1 - dampingRatio²)
    φ = acos(dampingRatio)
    samples[i] = 1 - (exp(-dampingRatio * frequency * t) / sqrt(1 - dampingRatio²))
                     * sin(ωd * t + φ)
  else:
    // Overdamped — two real exponentials
    s1 = -frequency * (dampingRatio + sqrt(dampingRatio² - 1))
    s2 = -frequency * (dampingRatio - sqrt(dampingRatio² - 1))
    samples[i] = 1 - (s2 * exp(s1 * t) - s1 * exp(s2 * t)) / (s2 - s1)

return samples
```

---

### `AutoRespawner.RespawnPlayer(config: PlayerConfig, assembler: PlayerAssembler) → IEnumerator`
Coroutine that destroys the current player and spawns a replacement at the same position.

**Parameters:**
- `config` — the modified PlayerConfig SO
- `assembler` — the PlayerAssembler instance in the scene

**Side effects:** Destroys the player GameObject. Spawns a new one at the same world position.

```
player = FindAnyObjectByType<PlayerSkeletonRoot>()
if player == null: yield break

pos = player.transform.position
Destroy(player.gameObject)
yield return null   // wait one frame for Destroy to process

assembler.Spawn(config, pos)
```

---

### `AutoRespawner.RespawnCentipedes(config: CentipedeConfig, assembler: CentipedeAssembler) → IEnumerator`
Coroutine that destroys all centipedes using a specific config and spawns replacements.

**Parameters:**
- `config` — the modified CentipedeConfig SO
- `assembler` — the CentipedeAssembler instance in the scene

**Side effects:** Destroys matching centipede GameObjects. Spawns replacements.

```
positions = []
foreach controller in FindObjectsByType<CentipedeController>():
  if controller.config == config:
    positions.append(controller.transform.position)
    Destroy(controller.gameObject)

if positions.count == 0: yield break
yield return null

foreach pos in positions:
  assembler.Spawn(config, pos)
```

---

## Modifiable Variables

### Tuning System Controls

| Variable | Type | Default | Description |
|---|---|---|---|
| `tuningToggleKey` | `Key` | `BackQuote` | Toggles tuning mode on/off. try BackQuote (tilde key) or F1; must not conflict with gameplay keys |
| `nextDimensionKey` | `Key` | `Tab` | Advance to next tuning dimension. try Tab or PageDown |
| `prevDimensionKey` | `Key` | `LeftShift+Tab` | Return to previous dimension |
| `logKey` | `Key` | `F5` | Log current value during sweep. try F5 or F6; avoid Space (conflicts with jump) |
| `lockKey` | `Key` | `Return` | Transition from sweep → A/B with bracketed range |
| `chooseAKey` | `Key` | `Alpha1` | Select configuration A during A/B phase |
| `chooseBKey` | `Key` | `Alpha2` | Select configuration B during A/B phase |
| `saveProfileKey` | `Key` | `F9` | Save current config as named profile |
| `loadProfileKey` | `Key` | `F10` | Cycle and load profiles from subfolder |
| `resetKey` | `Key` | `Backspace` | Reset current dimension to its default values |
| `skipDimensionKey` | `Key` | `RightShift` | Skip current dimension without tuning (keep current values) |
| `sweepSpeedMultiplier` | `float` | `1.0` | Multiplier on sweep cycle speed; try 0.3–3.0; lower = slower sweep (more deliberate), higher = faster (quick scan). Adjust with [ and ] keys at runtime |
| `abSwapInterval` | `float` | `3.0` | Seconds per A/B exposure before auto-swap; try 2.0–6.0; shorter = faster convergence but harder to evaluate, longer = more confident choices |
| `abEpsilon` | `float` | `0.02` | Fraction of total variable range at which A/B declares convergence; try 0.01–0.05; lower = finer precision but more rounds |
| `crossValPerturbation` | `float` | `0.10` | Random ± fraction applied to all tuned values during cross-validation; try 0.05–0.20; lower = subtler variants |
| `crossValVariants` | `int` | `4` | Number of perturbed configs to test; try 3–6; more = thorough but longer |
| `overlayWidth` | `float` | `320` | Pixel width of the overlay panel; try 280–420 |
| `overlayOpacity` | `float` | `0.85` | Background opacity; try 0.6–0.95; lower = less obtrusive |

### Spring Parameterization — PlayerConfig (replaces stiffness/damping fields)

| Variable | Type | Default | Description |
|---|---|---|---|
| `torsoFrequency` | `float` | `8.94` | Torso spring natural frequency ω (rad/s); try 4–20; higher = snappier visual tracking, lower = floatier trailing. Current default produces same behavior as old stiffness=80 |
| `torsoDampingRatio` | `float` | `0.28` | Torso spring damping ratio ζ; try 0.1–1.5; lower = more overshoot/bounce, 1.0 = critically damped (no overshoot), higher = sluggish settle. Current default is quite bouncy |
| `torsoMass` | `float` | `1.0` | Torso spring mass; try 0.3–3.0; higher = more resistance to external forces without changing spring feel |
| `hipFrequency` | `float` | `10.95` | Hip spring natural frequency; try 5–25; higher = torso snaps to foot level faster after jumps/impacts |
| `hipDampingRatio` | `float` | `0.46` | Hip spring damping ratio; try 0.2–1.2; lower = more torso bob on landing, higher = planted/rigid feel |
| `hipMass` | `float` | `1.0` | Hip spring mass; try 0.5–3.0; also divides jump impulse — higher = lower jump height from same jumpSpeed |
| `footFrequency` | `float` | `10.95` | Foot spring natural frequency; try 5–20; higher = feet snap to formation faster during movement |
| `footDampingRatio` | `float` | `0.73` | Foot spring damping ratio; try 0.3–1.2; lower = feet wobble after direction changes, higher = rigid formation |
| `footSpringMass` | `float` | `0.5` | Foot spring simulation mass; try 0.2–2.0; independent of footMass (RB mass) — affects visual inertia only |
| `jumpSpeed` | `float` | `8.0` | Base jump impulse (moved from PlayerAssembler); try 3–20; higher = higher base jump arc |
| `jumpOffsetFactor` | `float` | `10.0` | Extra impulse per unit hip compression (moved from PlayerAssembler); try 0–25; higher = more reward for crouching before jumping |

### Spring Parameterization — CentipedeConfig (replaces stiffness/damping fields)

| Variable | Type | Default | Description |
|---|---|---|---|
| `wiggleFrequency` | `float` | `8.94` | Ball spring natural frequency; try 4–20; higher = balls track skeleton tightly, lower = loose jelly feel |
| `wiggleDampingRatio` | `float` | `0.28` | Ball spring damping ratio; try 0.1–1.2; lower = more wobble after impacts, higher = rigid tracking |
| `wiggleMass` | `float` | `1.0` | Ball spring mass; try 0.3–3.0; affects detachment energy threshold — higher = harder to knock balls off |

### Scent Navigator — CentipedeConfig (used when `useScentNavigator == true`)

| Variable | Type | Default | Description |
|---|---|---|---|
| `scentDecayTime` | `float` | `8.0` | Trail persistence time constant (seconds); try 3–20; lower = centipede only follows fresh tracks; higher = long-lived memory enables wide-area spiraling. Read per-frame by ScentField if refactored (see Implementation Notes). |
| `scentSigma` | `float` | `1.5` | Gaussian spatial spread of each footprint (world units); try 0.5–3.0; lower = narrow precise trail, centipede must stay close to follow it; higher = broad cloud, gradient is smoother and more globally directional. Read per-frame by ScentField if refactored. |
| `scentGradientSampleRadius` | `float` | `0.8` | Radius of the 8-point gradient sampling ring around the head; try 0.3–2.0; lower = responds to very local field differences; higher = integrates a wider neighborhood, producing smoother but less reactive steering. |
| `scentSteeringBlend` | `float` | `4.0` | Turn rate toward gradient at full oscillator sensitivity (blends/sec); try 1–10; lower = wide lazy arcs, committed heading; higher = sharp reactive turns toward fresh scent. |
| `scentConsumeRadius` | `float` | `0.6` | World-unit radius of scent erasure as centipede passes; try 0.3–1.5; wider = more aggressive suppression, forces tighter inward spirals on stationary players. |
| `scentConsumeRate` | `float` | `2.0` | Suppression weight per second at the centipede center; try 0.5–5.0; higher = trail erased quickly, backtracking impossible; lower = leaves partial wake, centipede may retrace. |
| `scentOscillationFrequency` | `float` | `0.35` | Sweep-and-lock cycle rate in Hz; try 0.1–1.0; lower = long ballistic sweeps (deliberate, predatory); higher = rapid snap-lock cycles (nervous, unpredictable). |
| `scentSpeedBoost` | `float` | `1.0` | Extra speed (world units/sec) when on a hot trail; try 0–3.0; higher = centipede surges on fresh scent, more tactically dangerous; 0 = constant speed regardless of trail quality. |
| `scentGradientMaxStrength` | `float` | `5.0` | Forward field strength that yields full speed boost; try 1–10; calibrate against your `scentDecayTime`/`scentSigma` combination — if boost never triggers, reduce this; if always at max, increase it. |
| `scentFallbackThreshold` | `float` | `0.05` | Field strength below which direct pursuit activates; try 0.01–0.2; higher = centipede switches to direct chase sooner when field is weak; lower = keeps following gradient longer. |
| `scentFallbackBlend` | `float` | `0.8` | Blend rate toward player during direct fallback; try 0.2–2.0; lower = sluggish reaction to player position; higher = snappy direct pursuit when field is cold. |

---

## Implementation Notes

### Tuning Dimension Definitions (Full Reference)

Each dimension is a TuningDimensionDef ScriptableObject. Create one asset per row and add them to TuningManager's array in this order.

#### Player Dimensions (tune first)

| # | Dimension Name | Variables (fieldName) | Ranges | Init-only? | Test Scenario |
|---|---|---|---|---|---|
| 1 | Foot Physics | `footMass` (0.1–5), `footGravityScale` (0.3–3) | — | **Yes** | Drop from height. Jump repeatedly. Watch landing arc and fall speed. Heavier = less jump height. |
| 2 | Movement | `moveForce` (5–50), `maxSpeed` (2–15) | — | No | Run left-right. Start and stop. Try to dodge. moveForce = acceleration feel, maxSpeed = top speed feel. |
| 3 | Foot Spring | `footFrequency` (5–20), `footDampingRatio` (0.3–1.2) | — | No | Run and stop suddenly. Watch feet settle. Change direction. Low damping = swingy feet. |
| 4 | Hip Spring | `hipFrequency` (5–25), `hipDampingRatio` (0.2–1.2) | — | No | Jump and land. Watch torso bob. Higher frequency = faster recovery. Low damping = head-bobbing. |
| 5 | Torso Spring | `torsoFrequency` (4–20), `torsoDampingRatio` (0.1–1.5) | — | No | Change direction rapidly. Watch torso visual lag behind skeleton node. Pure visual polish. |
| 6 | Stance Geometry | `standHeight` (6–20 px), `footSpreadX` (2–10 px) | — | No | Stand still. Run. Jump. Evaluate silhouette proportions and stability feel. |
| 7 | Jump Feel | `jumpSpeed` (3–20), `jumpOffsetFactor` (0–25) | — | No | Jump from flat ground (tests jumpSpeed). Crouch into ground then jump (tests offsetFactor). Cross a gap. |
| 8 | Gun Feel | `firingSpeed` (3–25), `fireCooldown` (0.05–0.5) | — | No | Shoot at targets at near, mid, and far range. Hold fire button for sustained fire. |

#### Centipede Dimensions (tune second)

| # | Dimension Name | Variables (fieldName) | Ranges | Init-only? | Test Scenario |
|---|---|---|---|---|---|
| 9 | Body Spring | `wiggleFrequency` (4–20), `wiggleDampingRatio` (0.1–1.2) | — | No | Watch centipede traverse. Hit it with a projectile. Observe wobble and recovery. |
| 10 | Body Geometry | `followDistance` (0.1–0.8), `nodeRadius` (0.05–0.4) | — | **Yes** | Watch centipede at rest and in motion. Evaluate spacing and body proportions. |
| 11 | Destruction | `detachDistance` (0.2–1.5) | — | No | Shoot centipede with different projectile sizes. How hard is it to break? Too easy = trivial. Too hard = frustrating. |
| 12 | Pathing Speed | `speed` (1–8), `minTurnRadius` (0.5–4) | — | **Yes** | Let centipede chase. Feel the threat level. High speed + low turn radius = aggressive. |
| 13 | Pathing Behavior | `arcAngleVariance` (10–120°), `replanInterval` (0.5–4s) | — | **Yes** | Watch approach patterns for 30+ seconds. High variance = unpredictable. Low replan = adaptive. |
| 14 | Wriggle Feel | `waveAmplitude` (0.1–1.0), `waveFrequency` (0.5–5), `wavePhaseOffsetPerNode` (0.1–1.0) | — | **Yes** | Watch centipede approach from a distance. Evaluate character and organic feel. |

#### Scent Navigator Dimensions (tune instead of 12–13 when `useScentNavigator == true`)

| # | Dimension Name | Variables (fieldName) | Ranges | Init-only? | Test Scenario |
|---|---|---|---|---|---|
| 15 | Scent Trail | `scentDecayTime` (3–20), `scentSigma` (0.5–3.0) | — | No* | Let player stand still 10 seconds, move away, stand still again. Watch centipede follow the ghost path. High decayTime = long-lived memory. High sigma = blurry wide trail, gradient activates earlier. (*Requires ScentField live-read refactor — see Implementation Notes.) |
| 16 | Scent Consumption | `scentConsumeRate` (0.5–5.0), `scentConsumeRadius` (0.3–1.5) | — | No | Stand completely still. Watch centipede spiral in. High rate + wide radius = tight decisive spiral that closes fast. Low rate = looser circles, centipede may drift past and arc back. |
| 17 | Hunting Rhythm | `scentSteeringBlend` (1.0–10.0), `scentOscillationFrequency` (0.1–1.0), `scentGradientSampleRadius` (0.3–2.0) | — | No | Run gentle curves for 20+ seconds. Watch heading changes. Low blend + low oscillation = slow deliberate sweep. High blend + high oscillation = jittery reactive zigzag. High sample radius = smooth global steering. |
| 18 | Trail Speed | `scentSpeedBoost` (0–3.0), `scentGradientMaxStrength` (1.0–10.0) | — | No | Sprint in a straight line, then stop abruptly. Watch if centipede surges. Calibrate `scentGradientMaxStrength` first (sweep until boost is clearly visible), then tune `scentSpeedBoost` for threat level. |
| 19 | Fallback Behavior | `scentFallbackThreshold` (0.01–0.2), `scentFallbackBlend` (0.2–2.0) | — | No | Hide behind cover for 30+ seconds until field fully decays. High threshold = centipede switches to direct chase quickly. High blend = fast snap-to-player once fallback activates. |

### Tuning Order Rationale

The order is not arbitrary — it follows the dependency chain:

**Player (bottom-up):**
1. Foot Physics first because `footMass` affects jump height, collision response, and how much spring force is needed to move the feet. Every dimension above depends on feet feeling right.
2. Movement before springs because movement speed determines how much spring oscillation the player sees during normal play. A spring setting that looks great at low speed might look frantic at high speed.
3. Springs bottom-up (foot → hip → torso) because each layer tracks the one below. Tuning torso spring before hip spring means you'll re-tune torso when hip changes.
4. Stance Geometry after springs because it's visual proportion tuning — the motion character should be locked before adjusting silhouette.
5. Jump after stance because jump impulse interacts with footMass and hipMass, both of which should be finalized.
6. Gun last because it's nearly independent of body feel.

**Centipede:**
1. Body Spring first because it defines the centipede's visual character.
2. Geometry before destruction because nodeRadius affects ball mass (quadratic scaling), which affects detachment energy.
3. Destruction before pathing because fragility determines threat level — pathing speed should be tuned against it.
4. Wriggle last because it's visual polish.

**Scent Navigator (dimensions 15–19, replaces 12–13):**
1. Scent Trail first because `scentDecayTime` and `scentSigma` define the signal all other behaviors are reacting to. Wrong trail shape makes every other dimension impossible to evaluate — the centipede either has no signal or a field that's so broad every direction looks the same.
2. Consumption before Rhythm because the spiral behavior (consumption) is the core distinguishing mechanic of this system. Tune the spiral feel before layering in the sweep-lock character on top.
3. Hunting Rhythm after consumption because `scentSteeringBlend` and `scentOscillationFrequency` modulate how the centipede responds to the field — they only make sense once the field and consumption are producing meaningful gradients.
4. Trail Speed is secondary feel — the surge on a hot trail is a refinement. Tune it after the basic approach behavior is correct. `scentGradientMaxStrength` is a calibration constant that should be set relative to observed field values; tune it first within this dimension.
5. Fallback is an edge case by design. It only fires when the field is cold, which happens rarely in normal gameplay. Tune it last.

### Scale Independence

`playerScale` multiplies all pixel-to-world offsets but does NOT affect physics values (moveForce, maxSpeed, spring params, footMass, jumpSpeed). This means:

- Moderate scale changes (±50%) preserve the tuned feel reasonably well.
- Large scale changes will make the player feel "slower" relative to body size because movement speed is absolute, not relative. If you double playerScale, the player traverses fewer body-lengths per second.
- Spring oscillations will appear proportionally smaller at larger scales (same absolute amplitude, larger visual body).

The tuning system excludes playerScale. After tuning at a chosen scale, if you later change playerScale significantly, re-run the Movement and Stance Geometry dimensions.

### Files Modified by This Feature

**Config SOs (spring refactor):**
- `PlayerConfig.cs` — replace stiffness/damping fields with frequency/dampingRatio; add computed properties; absorb jumpSpeed and jumpOffsetFactor from PlayerAssembler
- `CentipedeConfig.cs` — replace wiggleStiffness/wiggleDamping with wiggleFrequency/wiggleDampingRatio; add computed properties

**Scripts (live config reading):**
- `PlayerSkeletonRoot.cs` — add `PlayerConfig config` field; read moveForce, maxSpeed, standHeight, jumpSpeed, jumpOffsetFactor from config per-frame
- `PlayerHipNode.cs` — add `PlayerConfig config` field; read HipStiffness, HipDamping, hipMass from config per-frame
- `PlayerFeet.cs` — add `PlayerConfig config` field; read FootStiffness, FootDamping, footSpringMass, footSpreadX from config per-frame
- `PlayerAssembler.cs` — remove jumpSpeed/jumpOffsetFactor (moved to PlayerConfig); wire config refs to scripts; use computed properties for stiffness/damping

**Scripts (minimal change):**
- `CentipedeAssembler.cs` — wire config ref to Ball instances; use computed properties
- `CentipedeController.cs` — add config ref field for live detachDistance reading
- `Ball.cs` — optionally add CentipedeConfig ref for live spring reading (or dual-write)
- `NodeWiggle.cs` — no change (continues using local fields; tuning system dual-writes)

**New files:**
- `TuningManager.cs` — singleton MonoBehaviour; state machine, input, orchestration
- `TuningDimensionDef.cs` — ScriptableObject defining one tuning dimension
- `TuningOverlay.cs` — Canvas-based UI overlay with waveform rendering
- `AutoRespawner.cs` — coroutine-based destroy-and-respawn for init-only variable changes
- `SpringParams.cs` — static helper class with ComputeStiffness/ComputeDamping
- 14× TuningDimensionDef assets (one per dimension, configured in Inspector)

### Edge Cases

1. **Log key vs gameplay keys**: The default log key is F5, avoiding conflict with Space (jump). If the active dimension is Jump Feel, the overlay should remind the user to jump during testing — the log key and jump key are already separate.

2. **A/B swap mid-air**: When swapping init-only variables (footMass, footGravityScale) during an A/B comparison, defer the respawn until the player is grounded. Check `FootContact.isGrounded` before triggering respawn; if airborne, set a `pendingRespawnOnLand` flag.

3. **Multiple centipedes, different configs**: Auto-respawn must match centipedes to their specific config SO instance. `CentipedeController.config` provides this reference. Only respawn centipedes whose config matches the one being tuned.

4. **SO persistence in editor**: ScriptableObject field changes during Play Mode persist in the Unity Editor. This is intentional — it's the core mechanism for "direct SO editing." The overlay shows a `[MODIFIED]` badge. The profile system is the undo safety net: save a "Baseline" profile before tuning begins.

5. **Reflection field name typos**: If `TuningVariable.fieldName` doesn't match an actual field, `GetField()` returns null. The TuningManager should validate all dimension definitions at Awake and log errors for mismatched field names.

6. **Damping ratio near 1.0**: The step-response formula branches at ζ = 1. Use tolerance `abs(ζ - 1.0) < 0.001` to select the critically damped path, avoiding `sqrt(1 - ζ²)` producing NaN.

7. **Sweep on init-only variables**: Continuous sweeping of an init-only variable (e.g., footMass) triggers a respawn every frame, which is destructive. Throttle: only respawn when the interpolated value crosses a discrete step threshold (e.g., round footMass to nearest 0.1 before comparing to the last-applied value).

8. **Cross-validation scope**: Only perturb dimensions within the same entity. Don't perturb centipede variables when cross-validating the player, and vice versa. Run Player cross-validation first, then Centipede cross-validation.

10. **Conditional dimension skipping for scent/arc navigator**: When `CentipedeConfig.useScentNavigator == true`, TuningManager must skip dimensions 12 and 13 at runtime — they contain arc-only fields (`minTurnRadius`, `arcAngleVariance`, `replanInterval`) that have no effect on ScentFieldNavigator. Conversely, when using the arc pathfinder, skip dimensions 15–19. Detect this by inspecting the target config's `useScentNavigator` bool before entering a dimension. Dimension 12's `speed` variable is shared — it remains relevant for both navigators, but in scent mode the entire dimension is replaced by dimensions 15–19 which tune scent-specific speed behavior alongside the base `speed` (kept at its current value from dimension 12's earlier arc-mode pass).

11. **`scentDecayTime` and `scentSigma` are not currently live**: `ScentField` copies these two values from config at `Initialize()` into private fields (`decayTime`, `sigma`) and uses those copies in `Evaluate()`. Without a refactor they would require respawn. Preferred fix: store a `CentipedeConfig` reference in `ScentField` instead of copying, and read `config.scentDecayTime` / `config.scentSigma` directly inside `Evaluate()`. This is a one-line change per field and enables live tuning without respawn. Mark them `requiresRespawn: false` after this refactor. If the refactor is deferred, mark them `requiresRespawn: true` and also call `ScentField.Instance.Clear()` alongside the respawn so the new config values govern a fresh field (not a stale field initialized under the old values).

12. **Scent speed boost calibration (`scentGradientMaxStrength`)**: This is not a feel variable — it's a normalization constant. Before tuning `scentSpeedBoost`, use Sweep phase to find a value where `trailHeat` visibly modulates speed (not stuck at 0 or clamped at 1). A typical calibrated value is 2–3× the `Evaluate()` return when the centipede is squarely on the player's trail. If the boost never seems to activate, lower this value; if it's always at max, raise it.

13. **Computed property stiffness/damping must be recalculated on BOTH frequency and dampingRatio changes**: When tuning `hipFrequency`, the live instance needs both its stiffness AND damping updated (both depend on ω). The sync mapping must handle this — a single fieldName change may require writing multiple instance fields.
