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

## PlayerFeet

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `PlayerFeet` | Live config SO reference | FootStiffness, FootDamping, footSpringMass, footSpreadX read per-frame | Foot spring and formation |
| `pixelToWorld` | `float` | `PlayerFeet` | Pixel-to-world conversion factor, cached at spawn | Multiplies footSpreadX to world units | Correct foot spacing |

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
