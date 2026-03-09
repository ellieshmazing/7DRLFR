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
| `hipFrequency` | `float` | `PlayerConfig` | Torso offset spring natural frequency ω (rad/s) | Higher = torso snaps faster to standHeight above hip; lower = more pronounced squash-and-stretch arc | Torso offset spring response speed |
| `hipDampingRatio` | `float` | `PlayerConfig` | Torso offset spring damping ratio ζ | Lower = more oscillation after landing/fall (bobbing); higher = quick settle; ~0.8 recommended | Torso offset spring oscillation character |
| `hipMass` | `float` | `PlayerConfig` | Torso offset spring virtual mass | Higher = more inertia, more pronounced stretch and squash during jumps and falls; `HipStiffness` and `HipDamping` recomputed | Torso offset spring inertia |
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
| `idleCorrectionThreshold` | `float` (px) | `PlayerConfig` | Displacement from neutral X at which idle correction fires a step | Lower = feet snap to neutral aggressively; higher = feet tolerate more spread before correcting | Idle stance tidiness vs. correction frequency |
| `idleCorrectionHysteresis` | `float` (px) | `PlayerConfig` | Displacement must fall below this before idle correction can fire again | Must be < `idleCorrectionThreshold`; smaller gap = more responsive re-arm; larger = more settling time required | Prevents spring oscillation from continuously re-triggering idle steps |
| `idleCorrectionArmed` | `bool` | `FootData` (FootMovement) | Per-foot flag; false after idle correction fires, true once displacement re-enters hysteresis band | Disarmed on step trigger; re-armed when displacement < hysteresis; reset to true on Airborne transition and fresh landings | Controls whether idle correction is eligible to fire for this foot |
| `stepHeight` | `float` (px) | `PlayerConfig` | Peak height of the step arc above the start-to-target baseline | Lower = shuffling/gliding; higher = marching/bounding feel | Visual liftoff of each step |
| `baseStepDuration` | `float` (s) | `PlayerConfig` | Step duration at zero horizontal speed | Lower = snappy steps; higher = deliberate strides at rest | Cadence at standstill |
| `minStepDuration` | `float` (s) | `PlayerConfig` | Floor on step duration at high speeds | Prevents infinitely fast leg cycling when sprinting | Maximum step rate |
| `stepSpeedScale` | `float` | `PlayerConfig` | How much horizontal speed shortens step duration: `duration = base / (1 + speed * scale)` | Lower = uniform cadence regardless of speed; higher = dramatically faster steps when running | Speed responsiveness of walk animation |
| `idleSpeedThreshold` | `float` (wu/s) | `PlayerConfig` | Speed below which the player is considered idle; triggers idle stance correction | Lower = corrects sooner after stopping; higher = tolerates slow drift without correcting | Transition between walking and idle recovery behavior |
| `stepRaycastDistance` | `float` (px) | `PlayerConfig` | Downward raycast distance when probing for step target ground | Must exceed torso-to-ground distance; lower = misses far drops | Terrain adaptability of step target Y |
| `airControlRatio` | `float` | `PlayerConfig` | Force multiplier when airborne (0 = no air control, 1 = full) | Scales moveForce when no feet are locked; 0.25 default means 25% air control | Horizontal movement in air |
| `groundDamping` | `float` | `PlayerConfig` | Torso RB linear damping when grounded | Higher = quicker stops and direction changes; default 5.0 | Torso deceleration on ground |
| `airDamping` | `float` | `PlayerConfig` | Torso RB linear damping when airborne | Lower = more momentum preservation through jumps; default 0.5 | Torso deceleration in air |
| `dampingTransitionSpeed` | `float` | `PlayerConfig` | Rate at which damping lerps between ground/air values (units/s) | Prevents jarring speed loss on landing; default 15.0 | Landing smoothness |
| `turnBoostFactor` | `float` | `PlayerConfig` | Force multiplier for direction reversal at max speed while grounded | Lets player change direction even at max speed; default 1.5 | Turn responsiveness |
| `forwardJumpFactor` | `float` | `PlayerConfig` | How much horizontal speed contributes to jump direction | 0 = always vertical; at 0.15, sprint-jumps lean forward | Jump arc angle |
| `directionalJumpDeadzone` | `float` | `PlayerConfig` | Horizontal speed below which jumps are perfectly vertical | Prevents accidental lean at low speeds; default 0.5 wu/s | Jump direction reliability |
| `variableJumpCutMultiplier` | `float` | `PlayerConfig` | Y velocity multiplier when jump button released early | Lower = more height control; X is preserved for hop dash; default 0.45 | Variable jump height, hop dash |
| `coyoteTime` | `float` | `PlayerConfig` | Seconds after leaving ground where jump is still valid | Grace period for ledge jumps; default 0.1s | Jump forgiveness |
| `jumpBufferTime` | `float` | `PlayerConfig` | Seconds before landing where jump input is buffered | Pre-landing jump queuing; default 0.1s | Landing-jump forgiveness |
| `landingVelocityTolerance` | `float` | `PlayerConfig` | Max upward velocity at which feet can still lock to ground | Allows apex landings; default 0.3 wu/s | Foot locking reliability |
| `jumpCoastTime` | `float` | `PlayerConfig` | Seconds after jump where foot X spring is suppressed | Lets feet carry launch velocity visually; default 0.06s | Visual launch coherence |
| `impactCrouchFactor` | `float` | `PlayerConfig` | Multiplier converting landing speed (wu/s) to crouch depth (source px) | Higher = deeper compression per unit landing speed; default 0.5 | Crouch depth on landing, jump power bonus |
| `crouchDepthRatio` | `float` | `PlayerConfig` | Fraction of standHeight that defines max crouch depth | Higher = more energy storage ceiling; max crouch = standHeight × this; default 0.4 | Max crouch range, max jump power bonus |
| `crouchDissipationRate` | `float` | `PlayerConfig` | Source pixels per second of crouch decay when not frozen | Lower = longer window to use stored energy; default 15 px/s | Timing window for jump after landing |
| `impactSquashOvershoot` | `float` | `PlayerConfig` | Visual overshoot multiplier on landing (1.0 = natural spring only) | Higher = juicier visual compression on impact; default 1.0 | Visual landing weight |
| `squashPunchDecayRate` | `float` | `PlayerConfig` | Source pixels per second of squash punch decay | Higher = snappier visual recovery; default 40 px/s | Visual squash duration |
| `landingCaptureWindow` | `float` | `PlayerConfig` | Seconds to accumulate max landing speed across both feet | Covers gap between first and second foot locking; default 0.05 s | Landing detection reliability |
| `footfallImpulse` | `float` | `PlayerConfig` | Forward impulse on torso each time a foot locks from stepping | Creates rhythm of step-driven acceleration; scaled down near maxSpeed; default 0.3 | Step-driven acceleration feel |
| `footfallMinSpeed` | `float` | `PlayerConfig` | Minimum horizontal speed for footfall impulse to fire | Prevents impulse at very low speeds; default 0.3 wu/s | Footfall activation threshold |
| `footGroundDamping` | `float` | `PlayerConfig` | Foot RB linear damping when any foot is grounded | Moderate damping when on ground; default 4.0 | Foot ground behavior |
| `footAirDamping` | `float` | `PlayerConfig` | Foot RB linear damping when airborne | Low damping prevents spring fighting in air; default 0.3 | Foot air responsiveness |
| `baseTorsoMass` | `float` | `PlayerConfig` | Base torso Rigidbody2D mass with no ammo | Foundation for weight system; jump scales by baseTorsoMass / currentMass; default 1.0 | Base movement feel, weight system |
| `ammoWeightPerUnit` | `float` | `PlayerConfig` | Mass added to torso per unit of ammo | Controls weight scaling per ammo; default 0.02 | Dynamic weight from ammo |
| `maxWallSlideSpeed` | `float` | `PlayerConfig` | Maximum downward speed during wall slide | Clamps torso fall speed when sliding a wall; default 2.0 wu/s | Wall slide descent rate |
| `wallSlideFootGravityScale` | `float` | `PlayerConfig` | Foot gravity scale during wall slide | Must match torso wall slide behavior to prevent feet/torso separation; default 0.5 | Foot/torso coherence during wall slide |
| `leashSoftRadius` | `float` (px) | `PlayerConfig` | Distance from foot center X where the body leash spring begins pulling torso back | Lower = tighter body, less freedom to drift from feet; default 6 px | Torso horizontal freedom, body integrity |
| `leashHardRadius` | `float` (px) | `PlayerConfig` | Hard clamp distance from foot center X; must be > leashSoftRadius | Safety net that should rarely be felt due to quadratic spring ramp; default 10 px | Maximum torso-to-feet separation |
| `leashForceMult` | `float` | `PlayerConfig` | Leash force at hard boundary as multiple of moveForce; quadratic ramp from soft→hard | Higher = stronger pull, more invisible hard clamp; default 3.0 | Leash strength, body coherence |
| `maxFootSeparation` | `float` (px) | `PlayerConfig` | Max horizontal distance between two feet; step targets clamped to this | Lower = more compact stance, prevents bizarre splits; default 20 px | Foot split limit, stance compactness |
| `groundProbeDistance` | `float` (px) | `PlayerConfig` | Downward raycast from locked foot to verify ground still exists | Too low risks false positives on uneven terrain; default 2 px | Locked foot ground verification |
| `edgeLandingNudge` | `float` (px) | `PlayerConfig` | Inward nudge when locking on a tilted contact normal (platform edge) | Higher = more aggressive centering onto platform surface; default 0.5 px | Edge landing stability |
| `minStepDistance` | `float` (px) | `PlayerConfig` | Minimum step distance after obstacle pre-check shortening; below this, step is skipped | Prevents trivially short steps into walls; default 1 px | Step obstacle handling |

---

## PlayerSkeletonRoot

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `PlayerSkeletonRoot` | Live config SO reference | All tunable values read per-frame from this SO | All player movement and jump parameters |
| `pixelToWorld` | `float` | `PlayerSkeletonRoot` | Pixel-to-world conversion factor, cached at spawn | Multiplies pixel-space config values (standHeight) to world units | Correct scaling of config values |

---

## PlayerSkeletonRoot (runtime state)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `_torsoSpringY` | `float` | `PlayerSkeletonRoot` | Current Y position of the torso offset spring | Integrates each FixedUpdate toward `hipNode.Y + standHeight`; lags behind when hip moves rapidly | Torso Y position (desiredY = _torsoSpringY − visualCrouch) |
| `_torsoSpringVelY` | `float` | `PlayerSkeletonRoot` | Velocity of the torso offset spring | Accelerated by spring-damper force each frame; cut by variableJumpCutMultiplier on jump release | Torso Y velocity, stretch/squash intensity |

## PlayerHipNode

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `config` | `PlayerConfig` | `PlayerHipNode` | Live config SO reference | hipMass read per-frame for external systems | Virtual spring mass property |

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
