# BallWorld — Variables Reference

Living documentation of all meaningful variables across the project. Updated whenever variables are added, renamed, or removed.

---

## PlayerConfig (ScriptableObject)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `moveForce` | `float` | `PlayerConfig` | Horizontal force applied per frame while WASD is held | Applied as `ForceMode2D.Force` each FixedUpdate; scales with frame rate via physics timestep | Torso horizontal acceleration; interacts with `linearDamping` for top speed feel |
| `maxSpeed` | `float` | `PlayerConfig` | Speed cap — AddForce stops when this magnitude is exceeded | Checked per-FixedUpdate from config SO | Top speed feel |
| `standHeight` | `float` | `PlayerConfig` | Target vertical distance from lowest foot visual to torso, in source pixels | Enforced each FixedUpdate via velocity override on torso Y; converted to world units via `pixelToWorld` | Controls how "tall" the player stands above its feet; affects visual silhouette |
| `footSpreadX` | `float` | `PlayerConfig` | Half-distance between left and right foot nodes along X, in source pixels | Feet placed at `±footSpreadX * pixelToWorld` from hip node X each frame | Sets stance width |
| `footOffsetY` | `float` | `PlayerConfig` | Vertical offset of foot nodes below the torso node, in source pixels | Applied as a downward bias when positioning foot nodes | Controls how far feet hang below the body at rest |
| `torsoFrequency` | `float` | `PlayerConfig` | Torso spring natural frequency ω (rad/s) | Higher = snappier visual tracking of torso node; `TorsoStiffness` and `TorsoDamping` computed from this | Spring response speed for torso visual |
| `torsoDampingRatio` | `float` | `PlayerConfig` | Torso spring damping ratio ζ; 0 = perpetual bounce, 1 = critically damped | Lower = more overshoot/bounce; `TorsoDamping` computed from this | Torso visual oscillation character |
| `torsoMass` | `float` | `PlayerConfig` | Torso spring simulation mass | Higher = more resistance to external forces without changing spring feel; `TorsoStiffness` and `TorsoDamping` recomputed | Torso visual inertia |
| `hipFrequency` | `float` | `PlayerConfig` | Hip spring natural frequency ω (rad/s) | Higher = torso snaps to foot level faster after jumps/impacts | Hip spring response speed |
| `hipDampingRatio` | `float` | `PlayerConfig` | Hip spring damping ratio ζ | Lower = more torso bob on landing, higher = planted/rigid feel | Hip spring oscillation character |
| `hipMass` | `float` | `PlayerConfig` | Hip spring mass; also divides jump impulse | Higher = lower jump height from same jumpSpeed; `HipStiffness` and `HipDamping` recomputed | Hip spring inertia, jump height |
| `footFrequency` | `float` | `PlayerConfig` | Foot spring natural frequency ω (rad/s) | Higher = feet snap to formation faster during movement | Foot spring response speed |
| `footDampingRatio` | `float` | `PlayerConfig` | Foot spring damping ratio ζ | Lower = feet wobble after direction changes, higher = rigid formation | Foot spring oscillation character |
| `footSpringMass` | `float` | `PlayerConfig` | Foot spring simulation mass; independent of footMass (RB mass) | Affects visual inertia only; `FootStiffness` and `FootDamping` recomputed | Foot visual inertia |
| `jumpSpeed` | `float` | `PlayerConfig` | Base jump impulse (kg·m/s); actual velocity = jumpSpeed / footMass | Read per-frame from config by PlayerSkeletonRoot | Jump height at rest |
| `jumpOffsetFactor` | `float` | `PlayerConfig` | Extra impulse per world-unit of hip compression | Read per-frame from config; rewards crouching before jumping | Jump height bonus from compression |
| `footMass` | `float` | `PlayerConfig` | Mass of each foot Rigidbody2D | Set once at spawn (init-only); affects gravity, collision, jump height | Foot physics weight |
| `footGravityScale` | `float` | `PlayerConfig` | Gravity scale for each foot Rigidbody2D | Set once at spawn (init-only); increase for snappier landings | Fall speed and landing snap |
| `projectileDef` | `BallDefinition` | `PlayerConfig` | BallDefinition used for every shot | Passed to `Ball.Init` on each fire; determines sprite and mass of projectiles | Projectile appearance and physics weight |
| `firingPointOffset` | `float` | `PlayerConfig` | Distance from arm pivot to barrel tip, in source pixels | Converted to world units (`* pixelToWorld`) and set as FiringPoint localPosition.x | Where projectiles spawn along the barrel |
| `firingSpeed` | `float` | `PlayerConfig` | World units per second of fired projectiles | Applied as initial linearVelocity via `ball.Detach()` | Projectile travel speed |
| `fireCooldown` | `float` | `PlayerConfig` | Minimum seconds between shots | Enforced by `ProjectileGun`'s `lastFireTime` gate in Update | Maximum fire rate |
| `projectileInitialScale` | `float` | `PlayerConfig` | World-unit diameter of a projectile at the moment of spawn | Overrides `Ball.Init`'s scale immediately after spawning; `ProjectileScaleGrow` then animates to true diameter | Barrel-emergence illusion intensity |
| `projectileGrowTime` | `float` | `PlayerConfig` | Seconds for a projectile to reach its true diameter from spawn scale | Passed to `ProjectileScaleGrow`; rate = `diameter / growTime` so all sizes pop out in the same duration | Pop-out duration from barrel |
| `tempMinProjectileDiameter` | `float` | `PlayerConfig` | **[TEMP]** Minimum random projectile diameter in world units | Used until a projectile queue replaces random sizing | Smallest possible shot |
| `tempMaxProjectileDiameter` | `float` | `PlayerConfig` | **[TEMP]** Maximum random projectile diameter in world units | Used until a projectile queue replaces random sizing | Largest possible shot |
| `maxWalkableAngle` | `float` (degrees) | `PlayerConfig` | Max angle between surface normal and Vector2.up for a walkable surface | Lower = feet only lock on near-flat ground; higher = gecko-grip on walls | Foot landing discrimination; affects foot lock after step and after airborne |
| `strideTriggerDistance` | `float` (px) | `PlayerConfig` | How far behind ideal X position a locked foot must lag before it initiates a step | Lower = steps start sooner; higher = foot drags before stepping | Walk cadence onset and visual foot drag |
| `strideProjectionTime` | `float` (s) | `PlayerConfig` | Seconds of torso velocity projected forward to predict step target X | Lower = conservative short steps; higher = aggressive reaching strides | Where foot lands ahead of torso |
| `stepHeight` | `float` (px) | `PlayerConfig` | Peak height of the step arc above the start-to-target baseline | Lower = shuffling/gliding; higher = marching/bounding feel | Visual liftoff of each step |
| `baseStepDuration` | `float` (s) | `PlayerConfig` | Step duration at zero horizontal speed | Lower = snappy steps; higher = deliberate strides at rest | Cadence at standstill |
| `minStepDuration` | `float` (s) | `PlayerConfig` | Floor on step duration at high speeds | Prevents infinitely fast leg cycling when sprinting | Maximum step rate |
| `stepSpeedScale` | `float` | `PlayerConfig` | How much horizontal speed shortens step duration: `duration = base / (1 + speed * scale)` | Lower = uniform cadence regardless of speed; higher = dramatically faster steps when running | Speed responsiveness of walk animation |
| `idleSpeedThreshold` | `float` (wu/s) | `PlayerConfig` | Speed below which the player is considered idle; triggers idle stance correction | Lower = corrects sooner after stopping; higher = tolerates slow drift without correcting | Transition between walking and idle recovery behavior |
| `stepRaycastDistance` | `float` (px) | `PlayerConfig` | Downward raycast distance when probing for step target ground | Must exceed torso-to-ground distance; lower = misses far drops | Terrain adaptability of step target Y |

---

## PlayerSkeletonRoot

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `PlayerSkeletonRoot` | Live config SO reference | All tunable values read per-frame from this SO | All player movement and jump parameters |
| `pixelToWorld` | `float` | `PlayerSkeletonRoot` | Pixel-to-world conversion factor, cached at spawn | Multiplies pixel-space config values (standHeight) to world units | Correct scaling of config values |

---

## PlayerHipNode

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `PlayerHipNode` | Live config SO reference | HipStiffness, HipDamping, hipMass read per-frame | Hip spring response |

---

## FootMovement

Procedural walking FSM; replaces PlayerFeet. Attach to HipNode GO alongside PlayerHipNode.

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `FootMovement` | Live config SO reference | All foot movement params read per-frame | All procedural walk/step/airborne behavior |
| `pixelToWorld` | `float` | `FootMovement` | Pixel-to-world conversion factor, cached at spawn | Multiplies all pixel-space config values to world units | Step distances, arc heights, thresholds |
| `torsoRB` | `Rigidbody2D` | `FootMovement` | Torso physics body — source of velocity and position | linearVelocity read each FixedUpdate for step trigger and target prediction | Step gating, step projection |
| `footColliderRadius` | `float` | `FootMovement` | Foot circle collider radius in world units (wired by assembler) | Added to raycast hit Y so feet land on top of surfaces, not inside them | All step target Y positions |
| `leftFootRB` / `rightFootRB` | `Rigidbody2D` | `FootMovement` | Foot visual bodies (wired by assembler) | Position and velocity overridden each FixedUpdate per state | Foot physics position |
| `leftFootContact` / `rightFootContact` | `FootContact` | `FootMovement` | Ground contact detectors (wired by assembler) | `isGrounded` and `lastContactNormal` queried each frame | Lock triggers, walkability check |
| `_left.state` / `_right.state` | `FootState` | `FootMovement` | Per-foot FSM state (Locked/Stepping/Airborne) | Transitions drive all foot position logic each FixedUpdate | Everything downstream |
| `_left.lockPosition` / `_right.lockPosition` | `Vector2` | `FootMovement` | World position where a Locked foot is pinned | Set by LockFoot(); held fixed each frame in Locked state | Hip ground reference, jump gating |
| `_left.stepProgress` / `_right.stepProgress` | `float` | `FootMovement` | 0..1 normalized arc progress for a Stepping foot | Advances by dt/stepDuration each FixedUpdate | Arc position, step completion |
| `_left.stepDuration` / `_right.stepDuration` | `float` | `FootMovement` | Seconds for the current step arc to complete | Computed from baseStepDuration / (1 + speed * stepSpeedScale); higher speed = shorter duration | Step cadence at different movement speeds |

---

## PlayerArmController

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

## ProjectileGun (MonoBehaviour — on Arm GO)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `initialScale` | `float` | `ProjectileGun` | Starting world-unit diameter of a spawned projectile | Overrides `Ball.Init`'s true-diameter scale at spawn; `ProjectileScaleGrow` then grows it to full size | Creates the barrel-emergence illusion |
| `growTime` | `float` | `ProjectileGun` | Seconds for a projectile to grow from spawn scale to true diameter | Passed to `ProjectileScaleGrow.Initialize`; rate computed as `diameter / growTime` — larger balls move faster to finish in equal time | Pop-out duration regardless of projectile size |
| `firingSpeed` | `float` | `ProjectileGun` | World units per second of a fired projectile | Applied via `ball.Detach(direction * firingSpeed)` — becomes the Rigidbody2D linearVelocity | Projectile travel speed |
| `fireCooldown` | `float` | `ProjectileGun` | Minimum seconds between consecutive shots | Gate in `Update`; compares `Time.time - lastFireTime` | Maximum sustained fire rate |
| `tempMinDiameter` | `float` | `ProjectileGun` | **[TEMP]** Lower bound of the random diameter range | Used by `GetProjectileDiameter()` until a queue replaces it | Smallest possible projectile spawned |
| `tempMaxDiameter` | `float` | `ProjectileGun` | **[TEMP]** Upper bound of the random diameter range | Used by `GetProjectileDiameter()` until a queue replaces it | Largest possible projectile spawned |

---

## CentipedeConfig (ScriptableObject)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `nodeCount` | `int` | `CentipedeConfig` | Total number of nodes including the head | Determines how many skeleton nodes and Ball visuals are spawned | Centipede length |
| `followDistance` | `float` | `CentipedeConfig` | World-unit gap between each node along the trail | Each body node follows its parent at exactly this distance | Segment spacing, overall body length |
| `nodeRadius` | `float` | `CentipedeConfig` | World-unit radius of each node's Ball visual | Passed to Ball.Init as `diameter = nodeRadius * 2`; sets localScale and CircleCollider2D | Visual size, collider size, effective mass (mass = baseMass × diameter²) |
| `nodeColor` | `Color` | `CentipedeConfig` | Tint applied to every node's SpriteRenderer | Applied via Ball.SetTint after Init | Node appearance |
| `wiggleFrequency` | `float` | `CentipedeConfig` | Ball spring natural frequency ω (rad/s) | Higher = balls track skeleton tightly; `WiggleStiffness` and `WiggleDamping` computed from this | Ball visual tracking tightness |
| `wiggleDampingRatio` | `float` | `CentipedeConfig` | Ball spring damping ratio ζ | Lower = more wobble after impacts; `WiggleDamping` computed from this | Ball oscillation character |
| `wiggleMass` | `float` | `CentipedeConfig` | Spring simulation mass per node | Higher = more sluggish response; affects detachment energy threshold | Visual inertia feel, detachment difficulty |
| `ballDefinition` | `BallDefinition` | `CentipedeConfig` | Ball type used for all node visuals | Determines sprite and baseMass; falls back to assembler's defaultBallDefinition if null | Node sprite, mass |
| `detachDistance` | `float` | `CentipedeConfig` | Distance a Ball must reach from its SkeletonNode to trigger detachment | CentipedeController checks per-ball each FixedUpdate; preemptive SHM energy check marks additional balls that are inevitably going to detach | Controls how hard a hit must be to break a centipede segment |
| `speed` | `float` | `CentipedeConfig` | Arc traversal speed in world units/sec | CentipedePathfinder sets `rb.linearVelocity` magnitude to this value while following an arc | How fast the centipede closes on the player |
| `minTurnRadius` | `float` | `CentipedeConfig` | Minimum allowed circular arc radius; prevents hairpin turns | Any computed arc with radius below this is rejected and resampled during Replan() | Tightest curve the centipede can execute; prevents self-overlap at sharp bends |
| `arcAngleVariance` | `float` | `CentipedeConfig` | ± random range (degrees) of arc departure angle from the direct approach heading | Sampled each Replan(); wider variance = more oblique arcs and surprising approach angles | How varied the centipede's attack vectors are |
| `replanInterval` | `float` | `CentipedeConfig` | Seconds between arc recalculations | Timer ticked in FixedUpdate; jitter added each reset to desync multiple centipedes | How often the centipede adapts its path to the player's current position |
| `replanJitter` | `float` | `CentipedeConfig` | Random ± seconds added to replanInterval each cycle | Prevents centipedes spawned together from replanning on the same frame | Variation in reaction cadence between individuals |
| `maxReplanAttempts` | `int` | `CentipedeConfig` | Max arc generation retries per Replan() call before accepting the best failed arc | Higher = more likely to satisfy minTurnRadius constraint; too high wastes CPU | Tradeoff between arc quality and planning cost |
| `waveAmplitude` | `float` | `CentipedeConfig` | Lateral peak displacement of the sinusoidal wriggle wave | Applied perpendicular to the arc tangent each frame; steers the physical path | How wide the centipede's wriggling motion is |
| `waveFrequency` | `float` | `CentipedeConfig` | Wave oscillations per second | Increments wavePhase by `waveFrequency * deltaTime` each FixedUpdate | How rapidly the centipede wriggles side to side |
| `wavePhaseOffsetPerNode` | `float` | `CentipedeConfig` | Phase shift per body node index for GetBodyWaveOffset() | Creates the appearance of a wave propagating from head to tail | Visual traveling-wave cadence along the body |
| `collisionCooldownDuration` | `float` | `CentipedeConfig` | Seconds before another collision response can trigger | Prevents rapid flip-flopping when the head grazes a wall repeatedly | Stability of collision response; lower = more reactive but jittery |
| `targetArrivalRadius` | `float` | `CentipedeConfig` | Distance to player at which the centipede considers the arc complete and replans | Checked each FixedUpdate against `Vector2.Distance(head, player)` | How close the centipede gets before picking a new arc |
| `useScentNavigator` | `bool` | `CentipedeConfig` | Enables ScentFieldNavigator instead of CentipedePathfinder for this config | Read by CentipedeAssembler.Spawn(); swaps which pathfinder component is added | Selects the pathfinding personality for this centipede type |
| `scentHistorySize` | `int` | `CentipedeConfig` | Ring buffer capacity for the shared ScentField; 200 × 0.1 s = 20 s of trail | Read once on ScentField.Initialize() from the first centipede config; subsequent centipedes reuse | How much scent trail history is retained in the field |
| `scentSampleInterval` | `float` | `CentipedeConfig` | Seconds between player position samples pushed into the scent field | Governs ScentField.Update() emission rate; lower = denser trail with more computation | Trail resolution vs. field evaluation cost |
| `scentDecayTime` | `float` | `CentipedeConfig` | Time constant for scent weight to decay to 37% (e^-1) of original strength | Used in ScentField.Evaluate() temporal factor exp(−age/decayTime) | How long the player's trail persists before fading |
| `scentSigma` | `float` | `CentipedeConfig` | Gaussian spatial spread (world units) of each player position sample | Controls the spatial extent of each sample's influence in ScentField.Evaluate(); set to ~half engagement distance | Effective "smell radius" per step of the scent trail |
| `scentGradientSampleRadius` | `float` | `CentipedeConfig` | Radius at which 8 direction samples are taken to compute the field gradient | Used in ScentFieldNavigator.ComputeGradientDirection(); should be ≤ scentSigma for best resolution | How locally vs. broadly the centipede reads the field |
| `scentSteeringBlend` | `float` | `CentipedeConfig` | Turn-rate factor; blends momentum toward gradient at this rate × sensitivity per second | Used in ScentFieldNavigator.FixedUpdate(); multiplied by oscillator sensitivity | How aggressively the centipede turns toward the scent at peak sensitivity |
| `scentConsumeRadius` | `float` | `CentipedeConfig` | World-unit radius within which the centipede suppresses nearby scent samples | Governs ScentField.Consume() proximity test; wider = more territory claimed per pass | How much of the trail is erased per frame of travel |
| `scentConsumeRate` | `float` | `CentipedeConfig` | Weight consumed per second at the centipede's exact center position | Applied in ScentField.Consume() scaled by proximity; higher = faster spiral tightening | Rate at which the inward spiral closes on a stationary target |
| `scentOscillationFrequency` | `float` | `CentipedeConfig` | Frequency (Hz) of the sweep-and-lock sensitivity oscillator | Increments sensitivityPhase by freq × 2π × dt each FixedUpdate | Speed of the predator sweep-lock rhythm; 0.35 Hz ≈ 3-second full cycle |
| `scentSpeedBoost` | `float` | `CentipedeConfig` | Max speed bonus (world units/sec) when centipede is directly on a hot trail | Added to base speed proportional to trailHeat = forwardFieldStrength/scentGradientMaxStrength | Creates natural acceleration surge on fresh scent |
| `scentGradientMaxStrength` | `float` | `CentipedeConfig` | Forward scent field strength that yields full speed boost | Normalizes trailHeat clamped to [0,1]; calibrate against expected field intensity | Sets sensitivity of the speed boost response |
| `scentFallbackThreshold` | `float` | `CentipedeConfig` | Field strength at head below which fallback direct pursuit activates | Tested each FixedUpdate in ScentFieldNavigator; activates when field is empty or fully consumed | Prevents the centipede from stalling when scent is absent |
| `scentFallbackBlend` | `float` | `CentipedeConfig` | Blend rate toward the player during fallback (weaker than gradient steering) | Applied as Lerp(momentum, toPlayer, scentFallbackBlend × dt) | Gentleness of the fallback so it doesn't instantly snap to player direction |

---

## ScentField (MonoBehaviour, singleton)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `player` | `Transform` | `ScentField` | Reference to the player transform being tracked | Used in Update() to record player positions into the ring buffer | Source of scent emission |
| `samples` | `Sample[]` | `ScentField` | Ring buffer of `(position, timestamp, weight)` tuples | Circular write; head advances modulo historySize; count tracks valid entries | The scent field's raw data |
| `head` | `int` | `ScentField` | Next write index in the ring buffer | Advances each sample; wraps around at historySize | Buffer write position |
| `count` | `int` | `ScentField` | Number of valid samples currently in the buffer | Clamped to historySize; governs Evaluate() loop bounds | How many Gaussians are summed in field evaluation |

---

## ScentFieldNavigator (MonoBehaviour)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `momentum` | `Vector2` | `ScentFieldNavigator` | Normalized current heading direction | Blended toward gradient each FixedUpdate; normalized after blend | The actual direction of travel; inertia carrier |
| `sensitivityPhase` | `float` | `ScentFieldNavigator` | Phase accumulator for the sweep-and-lock oscillator | Incremented by scentOscillationFrequency × 2π × dt; initialized random per centipede | Drives sensitivity = 0.5 + 0.5 × sin(phase) |
| `Momentum` | `Vector2` | `ScentFieldNavigator` | Public read-only snapshot of `momentum` after normalization each FixedUpdate | Set after normalization step; used by ScentFieldDebugVisualizer for arrow rendering | Debug overlay |
| `GradientDirection` | `Vector2` | `ScentFieldNavigator` | Public read-only result of `ComputeGradientDirection()` each FixedUpdate | Zero when field is flat; used by visualizer to draw the gradient arrow | Debug overlay |
| `Sensitivity` | `float` | `ScentFieldNavigator` | Public read-only oscillator output [0,1] each FixedUpdate | Drives ring size and color in debug overlay | Debug overlay |
| `IsInFallback` | `bool` | `ScentFieldNavigator` | True when `fieldAtHead < scentFallbackThreshold`; set each FixedUpdate | Triggers fallback X marker and player direction line in debug overlay | Debug overlay |

---

## ScentFieldDebugVisualizer (MonoBehaviour — debug only, remove before shipping)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `toggleKey` | `Key` | `ScentFieldDebugVisualizer` | InputSystem key that cycles debug modes (Off → SamplesOnly → Full → FullWithGrid) | Each press advances mode by 1, wraps at 4 | Debug mode selection |
| `sampleMarkerMaxRadius` | `float` | `ScentFieldDebugVisualizer` | World-unit radius of a fully fresh (effective=1) sample disk | Scales down with effective weight; try 0.05–0.3 | Sample dot size |
| `freshSampleColor` | `Color` | `ScentFieldDebugVisualizer` | Disk color for uncon­sumed (weight=1) samples | Lerped toward consumedSampleColor as raw weight decreases | Sample color fresh end |
| `consumedSampleColor` | `Color` | `ScentFieldDebugVisualizer` | Disk color for fully consumed (weight≈0) samples | Lerped toward freshSampleColor as raw weight increases | Sample color consumed end |
| `gradientArrowLength` | `float` | `ScentFieldDebugVisualizer` | World-unit length of the gradient direction arrow at each head | Try 0.3–1.0 | Gradient arrow size |
| `momentumArrowLength` | `float` | `ScentFieldDebugVisualizer` | World-unit length of the momentum arrow at each head | Set slightly longer than gradient to visually distinguish them | Momentum arrow size |
| `gradientArrowColor` | `Color` | `ScentFieldDebugVisualizer` | Color of the gradient direction arrow | — | Gradient arrow color |
| `momentumArrowColor` | `Color` | `ScentFieldDebugVisualizer` | Color of the momentum (current heading) arrow | — | Momentum arrow color |
| `sensitivityRingMinRadius` | `float` | `ScentFieldDebugVisualizer` | Ring radius when sensitivity = 0 (coasting phase) | Try 0.05–0.2 | Ring visual range low |
| `sensitivityRingMaxRadius` | `float` | `ScentFieldDebugVisualizer` | Ring radius when sensitivity = 1 (snapping phase) | Try 0.25–0.6 | Ring visual range high |
| `sensitivityCoolColor` | `Color` | `ScentFieldDebugVisualizer` | Ring color at sensitivity = 0 | Lerped toward hot at sensitivity = 1 | Ring color |
| `sensitivityHotColor` | `Color` | `ScentFieldDebugVisualizer` | Ring color at sensitivity = 1 | — | Ring color |
| `consumeRadiusColor` | `Color` | `ScentFieldDebugVisualizer` | Wire circle color for consume radius; alpha additionally scaled by scentConsumeRate/4 | — | Consume radius indicator |
| `fallbackColor` | `Color` | `ScentFieldDebugVisualizer` | Color of the fallback X marker and player line | — | Fallback indicator |
| `fallbackMarkerSize` | `float` | `ScentFieldDebugVisualizer` | World-unit arm length of the fallback X | — | Fallback X size |
| `momentumHistoryCapacity` | `int` | `ScentFieldDebugVisualizer` | Number of past world positions stored per navigator in the ghost trail | At 50 Hz FixedUpdate, 40 = 0.8 s of trail; try 20–80 | Ghost trail length |
| `momentumTrailColor` | `Color` | `ScentFieldDebugVisualizer` | Base color for ghost trail; alpha quadratically faded from newest to oldest | — | Trail color |
| `gridCellSize` | `float` | `ScentFieldDebugVisualizer` | World units per heat map cell; smaller = smoother but more field evaluations | Try 0.25–1.0 | Grid resolution |
| `gridExtent` | `float` | `ScentFieldDebugVisualizer` | Half-size of heat map in world units; grid covers 2×extent per axis | Try 5–15 | Grid coverage area |
| `gridMaxFieldStrength` | `float` | `ScentFieldDebugVisualizer` | Field strength that maps to full hot color; values above clamp to hot | Match to scentGradientMaxStrength in config; try 3–10 | Grid color normalization |
| `gridMaxAlpha` | `float` | `ScentFieldDebugVisualizer` | Maximum alpha of any heat map cell | Keep below 0.5 to avoid obscuring gameplay; try 0.2–0.6 | Grid transparency |
| `gridColdColor` | `Color` | `ScentFieldDebugVisualizer` | Grid cell color at zero field strength | — | Grid cold end color |
| `gridHotColor` | `Color` | `ScentFieldDebugVisualizer` | Grid cell color at full field strength | — | Grid hot end color |

---

## BallDefinition (ScriptableObject)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `type` | `BallType` | `BallDefinition` | Enum identifier for this ball type | Used to identify type at runtime; one SO asset per enum value | Type checking, lookup |
| `sprite` | `Sprite` | `BallDefinition` | Sprite for this ball type | Applied to Ball's SpriteRenderer on Init; author at 1 world-unit diameter at scale 1 | Visual appearance |
| `baseMass` | `float` | `BallDefinition` | Mass for a ball of diameter 1 | Actual RB mass = baseMass × diameter² (area-based scaling for consistent physics feel) | Physics weight, collision response |
| `movementOverride` | `BallMovementOverride` | `BallDefinition` | Optional custom movement equation (free mode only) | If assigned, OnFixedUpdate is called each physics step instead of default Unity physics | Movement behavior for free-flying balls of this type |
| `effect` | `BallEffect` | `BallDefinition` | Optional collision effect | OnCollision called on OnCollisionEnter2D; active in both Centipede Mode and free mode | Special on-hit behavior |

---

## Ball (MonoBehaviour)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `springStiffness` | `float` | `Ball` | Spring pull strength toward the linked SkeletonNode (Centipede Mode) | Higher = tighter visual tracking of skeleton position; dual-written by TuningManager | How closely Ball follows node in centipede form |
| `springDamping` | `float` | `Ball` | Oscillation decay on the Centipede spring | Higher = settles faster; dual-written by TuningManager | Wobble and bounce after direction changes |
| `springMass` | `float` | `Ball` | Spring simulation mass (Centipede Mode) | Higher = more sluggish and heavy-feeling; dual-written by TuningManager | Visual inertia in centipede form |

---

## NodeWiggle

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `stiffness` | `float` | `NodeWiggle` | Spring pull strength toward parent transform | Higher = tighter snap-back; dual-written by TuningManager from torso spring computed properties | How closely visual child tracks parent node (player torso etc.) |
| `damping` | `float` | `NodeWiggle` | Oscillation decay | Higher = settles faster; dual-written by TuningManager | Wobble duration |
| `mass` | `float` | `NodeWiggle` | Spring simulation mass | Higher = more sluggish; dual-written by TuningManager | Visual inertia feel |

---

## TuningManager (MonoBehaviour — dev-only singleton)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `sweepSpeedMultiplier` | `float` | `TuningManager` | Multiplier on sweep cycle speed | Lower = slower, more deliberate sweep; adjustable at runtime with `[` / `]` keys | Sweep pace |
| `abSwapInterval` | `float` | `TuningManager` | Seconds per A/B exposure before auto-swap | Shorter = faster convergence, harder to evaluate | A/B comparison tempo |
| `abEpsilon` | `float` | `TuningManager` | Fraction of total variable range at which A/B declares convergence | Lower = finer precision but more rounds | Convergence threshold |
| `crossValPerturbation` | `float` | `TuningManager` | Random ± fraction applied during cross-validation | Lower = subtler variants | Cross-validation sensitivity |
| `crossValVariants` | `int` | `TuningManager` | Number of perturbed configs to test | More = thorough but longer | Cross-validation coverage |

---

## Scale Convention (project-wide)
Sprites are authored at **1 world-unit diameter at scale 1**.
`transform.localScale = Vector3.one * diameter`
`CircleCollider2D.radius = 0.5f` (local space → diameter/2 world units after scaling)

---

*Add new sections per-script as variables are introduced.*
