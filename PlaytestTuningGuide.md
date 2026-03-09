# BallWorld — Playtester Tuning Guide

## Controls

| Key | Action |
|---|---|
| `` ` `` | Toggle tuning overlay on/off |
| Tab | Next dimension |
| Shift+Tab | Previous dimension |
| RightShift | Skip dimension (keep current values) |
| Backspace | Reset current dimension to defaults |
| **Sweep phase** | |
| F5 | Log current value (press when it feels good, press again when it goes too far) |
| Enter | Lock logged range → advance to A/B |
| [ / ] | Slow down / speed up sweep |
| **A/B phase** | |
| 1 | Choose A (keeps lower range) |
| 2 | Choose B (keeps upper range) |
| **Any time** | |
| F9 | Save current config as named profile |
| F10 | Cycle and load saved profiles |

## General Workflow — Per Dimension

1. **Sweep**: Watch the overlay and play the test scenario. Press **F5** once when it starts feeling right, again when it goes too far. Press **Enter** to lock that bracket.
2. **A/B**: The overlay alternates between two values every 3 seconds. Press **1** if A is better, **2** if B is better. Repeat ~6–10 rounds until it converges automatically.
3. **Save**: After each dimension group (Player complete, Centipede complete), press **F9** and enter a name.

> **Init-only** dimensions marked with ⚠ will automatically respawn the entity when values change — this is expected.

> **Navigator mode**: Dimensions 18–20 (Pathing Speed, Pathing Behavior, Wriggle Feel) apply to the arc pathfinder. Dimensions 21–25 (Scent Trail through Fallback Behavior) are only relevant when the centipede uses the scent navigator (`useScentNavigator == true`). Skip whichever group doesn't apply to your current config.

---

## Player Dimensions

> **Spring primer**: All springs in this game use `(frequency, dampingRatio)` rather than raw stiffness/damping. **Frequency** (rad/s) controls how fast — doubling frequency doubles the snap. **Damping ratio** controls whether it bounces: `0` = infinite oscillation, `1` = no overshoot at all (critically damped), `0.5–0.7` = one gentle overshoot then settles. These two knobs are independent; you can have a slow spring that never bounces (low freq, high ratio) or a fast spring that wobbles (high freq, low ratio).

---

### 1. Foot Physics ⚠
*Requires respawn. Variables: `footMass`, `footGravityScale`*

**Architecture note**: Feet are real Rigidbody2Ds with gravity and physics. `footMass` is the actual RB mass — it appears in collision impulse resolution. `footGravityScale` controls how hard Unity pulls them down while airborne.

| Variable | Low value effect | High value effect |
|---|---|---|
| `footMass` (0.3 → 2.0) | Light, floaty feet — weak collision response | Heavy, grounded feet — strong collision thud on landing |
| `footGravityScale` (0.3 → 3.0) | Slow fall, feet linger in the air after jumping | Fast drop, feet snap down hard on landing |

**What to observe**: After jumping, watch the arc. Do feet leave the ground cleanly and return with appropriate weight? Drop from a high ledge — does the landing feel like mass, or like hitting a trampoline?

**Key interaction — Dim 9 (Jump Feel)**: `footMass` no longer divides the jump impulse directly (jumps are velocity-set). However, heavier feet have more momentum to resolve on landing and affect collision responses. Prefer adjusting `jumpSpeed` for jump height.

**Key interaction — Dim 5 (Foot Spring)**: Higher `footGravityScale` fights the foot spring during the airborne phase. If feet don't track back to formation well in the air, gravity may be overpowering the spring — consider raising `footFrequency` in tandem.

**Log when**: Falling from a ledge feels like landing with body weight, not floating down. Jump height and landing impact feel proportional to each other.

---

### 2. Movement
*Live — no respawn. Variables: `moveForce`, `maxSpeed`, `airControlRatio`, `groundDamping`, `airDamping`, `dampingTransitionSpeed`, `turnBoostFactor`*

**Architecture note**: `moveForce` is applied via `AddForce` each FixedUpdate with a **quadratic soft cap** near `maxSpeed` — force tapers off as speed approaches the ceiling in the same direction, so you can briefly exceed `maxSpeed` via other impulses (landing, jumps). When reversing direction while grounded and at speed, `turnBoostFactor` replaces the soft cap entirely, guaranteeing direction changes always get full power. `airControlRatio` scales `moveForce` to a fraction when no feet are locked. Torso damping lerps between `groundDamping` (stops fast) and `airDamping` (preserves momentum) at `dampingTransitionSpeed` per second — landing doesn't feel like hitting a brake.

| Variable | Low value effect | High value effect |
|---|---|---|
| `moveForce` (3 → 40) | Sluggish acceleration, tank-like ramp-up | Near-instant response, very snappy |
| `maxSpeed` (2 → 15 wu/s) | Tight, precise movement; walking pace | Wide-open sprinting; hard to stay accurate |
| `airControlRatio` (0 → 1) | Commits momentum — air direction almost unchanged | Full aerial redirection at same force as ground |
| `groundDamping` (2 → 10) | Character skids a long distance before stopping | Snaps to a stop almost instantly when key released |
| `airDamping` (0.1 → 2.0) | Floaty — momentum preserved through jumps and falls | Air resistance — speed bleeds off mid-jump |
| `dampingTransitionSpeed` (5 → 30) | Slow damping transition — gear-shift jolt on landing | Instant transition — snappy but can feel abrupt |
| `turnBoostFactor` (1.0 → 2.5) | Near maxSpeed, reversals still fight existing momentum | Directions snap instantly at any speed |

**What to observe**: Start from rest and run in one direction. How long before you reach full speed? Now reverse direction hard — does it feel sticky or immediate? Jump and try to redirect — can you influence your trajectory? Land from a jump — does the damping switch feel smooth? The ratio `moveForce / maxSpeed` shapes the ground feel; `airControlRatio / airDamping` shapes aerial control.

**Key interaction — Dim 3 (Walking Trigger)**: `maxSpeed` defines the upper end of the speed range the walking animation has to handle. `idleSpeedThreshold` is effectively a fraction of your `maxSpeed` — calibrate them together after settling on a `maxSpeed`.

**Key interaction — Dim 10 (Impact Crouch)**: When crouch energy is frozen (player holding down after landing), horizontal input is suppressed entirely — `moveForce` has no effect until the crouch releases. This is a design mechanic, not a movement bug.

**Log when**: The character starts and stops in a way that feels responsive without slipping around. Direction changes feel decisive. Jumps feel like they carry your momentum, not erase it.

---

### 3. Walking Trigger
*Live — no respawn. Variables: `strideTriggerDistance`, `idleSpeedThreshold`, `strideProjectionTime`*

**Architecture note**: A Locked foot only steps when it lags too far behind its ideal X position (torso X ± `footSpreadX`). The lag threshold is `strideTriggerDistance`. Below `idleSpeedThreshold` speed, steps don't trigger from walking lag — instead, if a foot has drifted more than 30% of `footSpreadX` from neutral, it quietly corrects back. `strideProjectionTime` shifts the step target X forward by `velocity * time`, so fast-moving characters plant their feet ahead of where they'll land.

| Variable | Low value effect | High value effect |
|---|---|---|
| `strideTriggerDistance` (1 → 10 px) | Feet step constantly, nervous/twitchy cadence | Feet lag and drag before stepping — shuffling feel |
| `idleSpeedThreshold` (0.1 → 1.5 wu/s) | Correction fires even when moving slowly; almost no idle state | Feet stop walking and drift without correcting until nearly stopped |
| `strideProjectionTime` (0 → 0.35 s) | Feet land directly under body, conservative | Feet reach far ahead at speed — aggressive, anticipatory stride |

**What to observe**: Walk slowly and count steps — do they feel deliberate or jittery? Sprint and watch where feet land relative to the torso. Stand still and slowly decelerate to a stop — do feet smoothly return to neutral stance?

**Key interaction — Dim 4 (Step Shape)**: This dimension controls *when* steps happen. Dim 4 controls *what each step looks like*. A high `strideTriggerDistance` with a high `stepHeight` produces infrequent but dramatic steps. Low trigger with low height produces a subtle shuffle. These are independent axes of the walk animation character.

**Key interaction — Dim 2 (Movement)**: `strideProjectionTime` multiplies actual torso velocity. At higher `maxSpeed`, the same `strideProjectionTime` projects further. If the character stumbles at speed, try reducing `strideProjectionTime` before adjusting movement variables.

**Log when**: At slow walk, feet step in clear 1-2-1-2 rhythm without over-correcting. At sprint, feet reach forward confidently. Stopping causes feet to settle naturally rather than teleporting.

---

### 4. Step Shape
*Live — no respawn. Variables: `stepHeight`, `baseStepDuration`, `minStepDuration`, `stepSpeedScale`*

**Architecture note**: Each step follows a smooth sin(π·t) arc from start to target — the foot rises to `stepHeight` at the midpoint, then descends. Duration is computed as `max(minStepDuration, baseStepDuration / (1 + |vel.x| · stepSpeedScale))`. The foot can also lock early if it contacts walkable ground during the descent half of the arc (past the midpoint).

| Variable | Low value effect | High value effect |
|---|---|---|
| `stepHeight` (0 → 12 px) | Gliding shuffle, barely leaves the ground | Marching-band lift — exaggerated, expressive |
| `baseStepDuration` (0.08 → 0.5 s) | Snappy steps even at rest | Slow, deliberate steps at standstill |
| `minStepDuration` (0.02 → 0.15 s) | Steps blur together at sprint — can look mechanical | Steps have a minimum visibility even at full speed |
| `stepSpeedScale` (0 → 0.8) | Uniform cadence regardless of speed | Step speed increases dramatically with movement speed |

**What to observe**: Walk slowly and look at the arc each foot traces. Is there a clear lift? Now sprint — does the cadence feel urgent? There's a natural tradeoff: high `stepHeight` at high `stepSpeedScale` can create extremely expressive running animation; low values of both produce a more grounded, grittier movement feel.

**Cadence formula at a glance**:
- At rest: duration = `baseStepDuration`
- At `maxSpeed` (e.g. 5 wu/s) with `stepSpeedScale` 0.3: duration = `base / (1 + 5 × 0.3)` = `base / 2.5`
- The `minStepDuration` floor kicks in when this would go lower

**Key interaction — Dim 3 (Walking Trigger)**: Trigger distance and step shape are orthogonal: trigger distance controls step frequency, step shape controls each step's visual quality. Tune them independently — get the cadence right first (Dim 3), then shape the arc (this dimension).

**Key interaction — Dim 6 (Hip Spring)**: Slow, high-arc steps take longer to complete. During a long step, the hip spring has more time to settle toward the one locked foot before the stepping foot lands. This can produce a visible sway that looks either organic or awkward depending on hip frequency.

**Log when**: Walk cadence feels unhurried and deliberate; sprint cadence feels urgent. The foot arc is visible but not distracting. Both states feel like the same character.

---

### 5. Foot Spring
*Live — no respawn. Variables: `footFrequency`, `footDampingRatio`, `footSpringMass`, `footGroundDamping`, `footAirDamping`*

**Architecture note**: The spring **only governs the Airborne state**. When a foot is Locked, it's kinematically pinned. When Stepping, it follows a scripted arc. Only when fully airborne does this spring pull feet back toward `(hipX ± footSpreadX, hipY)`. The X component has a **settle-lock**: once displacement and velocity both drop below threshold, X is pinned kinematically to avoid residual oscillation — it releases again if the hip moves far enough away. `footGroundDamping` / `footAirDamping` are the foot RB linear damping values, toggled based on whether any foot is Locked — high ground damping prevents feet from bouncing after contact; low air damping lets the spring work without the RB damper fighting it.

| Variable | Low value effect | High value effect |
|---|---|---|
| `footFrequency` (3 → 25 rad/s) | Feet drift lazily back to formation — slow float under body | Feet snap immediately to position — no drift visible |
| `footDampingRatio` (0.2 → 1.0) | Feet oscillate past formation, bounce/dangle during flight | Feet settle immediately with no overshoot |
| `footSpringMass` (0.1 → 2.0) | Lower computed stiffness/damping for the same ω/ζ — softer spring force per frame | Higher computed stiffness/damping — stiffer, but ω and ζ feel identical so the effect is subtle |
| `footGroundDamping` (1 → 8) | Feet have momentum after landing — may slide or bounce slightly | Feet stop dead the moment they lock to ground |
| `footAirDamping` (0 → 2.0) | Spring works freely — responsive X tracking in the air | RB damping fights the spring — sluggish aerial foot tracking |

**What to observe**: Jump and look at the feet during the arc. Do they tuck up under the body or spread wide? Land on a platform — do feet settle cleanly or wobble after touchdown? Lower `footGroundDamping` and notice if feet have a little bounce after locking — then raise it to kill that.

**Secondary parameters** (rarely need adjustment): `footXLockThreshold`, `footXLockVelocity`, `footXUnlockThreshold` — control when the airborne X settle-lock engages and releases. Defaults work well; only adjust if feet are sticking in a position when they shouldn't be, or failing to track the hip after large horizontal movements.

**Key interaction — Dim 1 (Foot Physics)**: `footGravityScale` fights the Y component of this spring. High gravity + low `footFrequency` = feet hang very low during airborne phase. If feet look like they're being pulled away from the body during jump, either raise frequency or reduce gravity scale.

**Key interaction — Dim 6 (Hip Spring)**: The spring target Y is the hip node Y — which is itself moving during a jump. Both springs settle simultaneously on landing. If landing looks jittery, the two springs may be resonating; try raising the damping ratio on one of them.

**Log when**: Feet track believably under the body during a jump arc. Landing transitions from airborne to locked without a visible "snap." Feet don't oscillate after touch-down.

---

### 6. Hip Spring
*Live — no respawn. Variables: `hipFrequency`, `hipDampingRatio`, `hipMass`*

**Architecture note**: The hip node is a virtual point that springs toward the Y position of the lowest locked foot. The torso then sits at `hipY + standHeight` — this is enforced each frame as a velocity override, so the torso always rides the hip. The hip spring is what gives the whole body its weight: low frequency = torso lags behind foot movement; low damping ratio = torso bobs up and down after each landing.

| Variable | Low value effect | High value effect |
|---|---|---|
| `hipFrequency` (3 → 20 rad/s) | Heavy lag between foot contact and torso rising — exaggerated squash feel | Near-rigid: torso snaps to foot level instantly |
| `hipDampingRatio` (0.2 → 1.0) | Strong torso bob on every landing — character bounces with each step | Planted, no bob — torso arrives and stays |
| `hipMass` (0.3 → 3.0) | Hip responds quickly to the jump impulse — rises in sync with feet | Hip lags at jump start — body "stretches" upward |

**What to observe**: Jump and land hard. Watch the torso: does it dip on landing and spring back? That's the hip spring in action. A low damping ratio gives you satisfying head-bobbing; too low and the character looks like it's perpetually bouncing. Try landing from increasing heights — does the impact feel proportional?

**Jump interaction with `hipMass`**: At the moment of jump, the hip is given an upward velocity. Heavier hip = slower initial rise. This is independent of how high the feet go. A heavy hip can make the character look like it's "stretching" at jump start.

**Key interaction — Dim 9 (Jump Feel)**: The hip spring compresses when the character lands and is still settling. This compression feeds `jumpOffsetFactor` — if you jump immediately after landing, the hip hasn't recovered yet, and you get a height bonus. A lower `hipFrequency` keeps the hip compressed longer, extending the window.

**Key interaction — Dim 10 (Impact Crouch)**: `hipDampingRatio` controls how long the bounce window stays open. Lower damping = hip stays compressed longer = bigger window for both `jumpOffsetFactor` and impact crouch energy.

**Key interaction — Dim 5 (Foot Spring)**: Both springs settle simultaneously on landing. If you see the torso oscillating after landing, the hip's damping ratio is the primary lever — not the foot spring.

**Log when**: Landing from a short hop feels light; landing from a high drop feels like weight. The torso recovers in a way that reads as "breathing" — alive, not mechanical.

---

### 7. Torso Spring
*Live — no respawn. Variables: `torsoFrequency`, `torsoDampingRatio`, `torsoMass`*

**Architecture note**: The torso spring is handled by `NodeWiggle` on the TorsoVisual child object. It springs the visual body toward the skeleton node position each LateUpdate. This is **purely cosmetic** — no game logic reads the visual position except the arm, which orbits the TorsoVisual. This means a very jiggly torso spring will also cause the arm to sway.

| Variable | Low value effect | High value effect |
|---|---|---|
| `torsoFrequency` (2 → 20 rad/s) | Extreme jelly — visual body lags far behind the skeleton on direction changes | Near-rigid — visual tracks skeleton almost exactly |
| `torsoDampingRatio` (0.1 → 1.0) | Long wobble after each impulse — the body "rings" | Zero overshoot — visual settles immediately |
| `torsoMass` | Changes computed stiffness/damping magnitudes but preserves ω/ζ — subtle unless very extreme | (Same — this mostly matters if external forces or TuningManager is writing raw stiffness/damping values) |

**What to observe**: Change direction sharply and watch the body. Does the torso lag and snap back? Try a jump — does the visual stretch slightly on takeoff? The torso spring is what makes the character feel "alive" vs. "stiff." This is the dimension most sensitive to personal taste.

**The arm connection**: Because `PlayerArmController` orbits `TorsoVisual.position` (not the skeleton node), a heavily bouncing torso spring makes the arm swing around the barrel. Low damping ratio here = the arm dances around on landing. Decide if that's appealing or distracting.

**Key interaction — Dim 6 (Hip Spring)**: The hip spring creates the underlying motion; the torso spring adds a second layer of lag on top of it. Stacking two underdamped springs (both low damping ratio) can create complex, organic wobble — or muddy, hard-to-read motion. A good starting point: tune hip spring for weight, torso spring for character.

**Log when**: The body reads as alive — it leans into acceleration, compresses on landing, and settles without ringing. The arm behavior is coherent with the torso motion rather than fighting it.

---

### 8. Stance Geometry
*Live — both read per-frame. Variables: `standHeight`, `footSpreadX`*

**Architecture note**: `standHeight` is applied every FixedUpdate as a velocity override on the torso Y — the torso is continuously driven to `hipY + standHeight · pixelToWorld`. `footSpreadX` is the horizontal distance from the hip node to each foot's ideal position, used in both the step target calculation and the airborne spring target. Both values are in **source pixels** and scaled by `pixelToWorld` at runtime.

| Variable | Low value effect | High value effect |
|---|---|---|
| `standHeight` (6 → 20 px) | Squat, compact character — torso sits close to feet | Tall, stilt-walker silhouette |
| `footSpreadX` (1 → 10 px) | Narrow stance, feet nearly together | Wide stance — sumo/frog-like |

**What to observe**: Stand still and evaluate the silhouette. Run and check whether the proportions still read as a character. Jump — a taller character has more "body height" to work with, which affects whether the jump arc looks proportional to the character's size.

**`standHeight` and Impact Crouch**: Taller characters have a larger range for crouch compression, since `maxCrouchDepth = standHeight × crouchDepthRatio`. If `standHeight` changes significantly, revisit Dim 10 to recheck the energy ceiling.

**`footSpreadX` and stride trigger**: Wider stance means each foot is further from the hip. The stride trigger fires when the foot lags more than `strideTriggerDistance` behind its ideal X — so wider stance means more absolute lag is allowed before stepping. If the walk cadence changed after adjusting spread, expect to revisit Dim 3.

**Log when**: Standing, the character's shape reads clearly as a body. The stance width feels stable — not so narrow that it looks like the character is on a tightrope, not so wide that movement looks comedic.

---

### 9. Jump Feel
*Live — read per-frame. Variables: `jumpSpeed`, `jumpOffsetFactor`, `forwardJumpFactor`, `directionalJumpDeadzone`, `variableJumpCutMultiplier`, `coyoteTime`, `jumpBufferTime`, `landingVelocityTolerance`, `jumpCoastTime`*

**Architecture note**: Jump velocity is **set directly** on feet (Celeste-style — velocity override, not impulse). `jumpMagnitude = jumpSpeed + totalHipCompression × jumpOffsetFactor`, scaled by `baseTorsoMass / rb.mass` for weight. `totalHipCompression` includes both the hip spring lag below the lowest locked foot and the current impact crouch amount (Dim 10). Directional lean: if horizontal speed > `directionalJumpDeadzone`, the jump vector tilts forward by `forwardJumpFactor × |hVel|`. **Variable height**: releasing Space while ascending cuts Y velocity on feet and hip by `variableJumpCutMultiplier` while fully preserving X velocity — the "hop dash." Coyote time allows jumping for a window after leaving a ledge. Buffer stores input for a window before landing. `landingVelocityTolerance` allows feet to lock while moving very slowly upward (catches apex landings without bouncing). `jumpCoastTime` suppresses the airborne X spring right after launch so feet visually carry their trajectory.

| Variable | Low value effect | High value effect |
|---|---|---|
| `jumpSpeed` (2 → 20) | Barely leaves the ground — hop rather than jump | Very high arc, risk of leaving the play area |
| `jumpOffsetFactor` (0 → 25) | Crouch gives no bonus — all jumps the same height | Crouched landing jump dramatically higher — timing becomes essential |
| `forwardJumpFactor` (0 → 0.3) | Always jumps vertically regardless of run speed | Sprint-jumps lean significantly forward — hop-dash range increases |
| `directionalJumpDeadzone` (0 → 1 wu/s) | Any tiny horizontal speed causes lean | Must be clearly moving before lean applies |
| `variableJumpCutMultiplier` (0 → 0.9) | Tap gives an extremely short hop, hold for full height | Minimal height control — tap and hold feel nearly identical |
| `coyoteTime` (0 → 0.2 s) | Must be on exact ground pixel to jump | Generous ledge forgiveness — jumping a moment after walking off still works |
| `jumpBufferTime` (0 → 0.2 s) | Must press jump very precisely as feet hit ground | Jump queued before landing fires automatically — very forgiving timing |
| `landingVelocityTolerance` (0 → 0.5 wu/s) | Feet only lock while falling — apex landings cause a momentary float | Feet lock even while barely rising — apex landings are smooth |
| `jumpCoastTime` (0 → 0.12 s) | Feet spring immediately back under body at launch | Feet carry launch angle visually — looks like a proper jump trajectory |

**What to observe**: Flat ground jumps isolate `jumpSpeed`. Sprint and jump — reveals `forwardJumpFactor`. Tap vs. hold Space — reveals `variableJumpCutMultiplier`. Walk off a ledge and press jump a moment later — tests `coyoteTime`. Press jump just before landing — tests `jumpBufferTime`. Land hard then jump immediately — Dim 10 crouch energy adds to `jumpOffsetFactor` bonus.

**The hop dash**: With `forwardJumpFactor` > 0 and `variableJumpCutMultiplier` low, a quick sprint-tap-jump travels mostly horizontally. This is intentional. Raise the cut multiplier or lower the forward factor to suppress it.

**Key interaction — Dim 6 (Hip Spring)**: `hipDampingRatio` controls how long the hip stays compressed after landing — directly extending the window where `jumpOffsetFactor` yields a bonus.

**Key interaction — Dim 10 (Impact Crouch)**: `_crouchAmount` is included in `totalHipCompression`. `jumpOffsetFactor` applies to both the hip spring lag and the stored crouch — one multiplier, two sources. Tune `jumpOffsetFactor` first (Dim 9), then `impactCrouchFactor` / `crouchDepthRatio` to control how much compression a given fall generates.

**Log when**: A flat-ground jump clears a comfortable obstacle. Sprint-jumping forward reaches further. Tapping Space gives a short hop; holding gives a full arc. Landing from height then jumping immediately is clearly rewarded with extra height.

---

### 10. Impact Crouch
*Live — no respawn. Variables: `impactCrouchFactor`, `crouchDepthRatio`, `crouchDissipationRate`, `impactSquashOvershoot`, `squashPunchDecayRate`, `landingCaptureWindow`, `footfallImpulse`, `footfallMinSpeed`*

**Architecture note**: When a foot locks from Airborne state, `FootMovement` captures the foot's downward speed and, after a short `landingCaptureWindow` (to accumulate both feet's speeds on two-footed landings), passes the max speed to `PlayerSkeletonRoot`. This converts to crouch: `crouchAmount = min(landingSpeed × impactCrouchFactor, standHeight × crouchDepthRatio)`. The crouch reduces `effectiveStandHeight`, compressing the body. `impactSquashOvershoot` adds an extra initial visual spike (decays at `squashPunchDecayRate`) for hard-landing juiciness. Crouch dissipates at `crouchDissipationRate` px/s unless the player holds down-key while grounded — that freezes dissipation and suppresses horizontal input (committed crouch). When any step completes (Stepping → Locked), `footfallImpulse` is applied forward to the torso — a small per-step acceleration scaled down near `maxSpeed`.

**Impact Crouch variables:**

| Variable | Low value effect | High value effect |
|---|---|---|
| `impactCrouchFactor` (0.1 → 1.5) | Even high drops barely compress — little energy storage | Even moderate drops store significant compression energy |
| `crouchDepthRatio` (0.1 → 0.6) | Shallow compression ceiling — small jump bonus cap | Deep compression — large energy ceiling, dramatic visual |
| `crouchDissipationRate` (5 → 40 px/s) | Long window — energy lingers for several seconds | Short window — must jump almost immediately after landing |
| `impactSquashOvershoot` (1.0 → 2.5) | No extra visual punch — only natural spring compression | Hard landing adds a strong extra spike that snaps back quickly |
| `squashPunchDecayRate` (15 → 80 px/s) | Overshoot spike lingers, blends with dissipation | Overshoot spikes and snaps back instantly — very punchy |
| `landingCaptureWindow` (0.02 → 0.1 s) | Only first foot's speed registers — second foot ignored | Accumulates both feet's landing speeds — more consistent on two-foot landings |

**Footfall variables:**

| Variable | Low value effect | High value effect |
|---|---|---|
| `footfallImpulse` (0 → 1.5) | Steps have no forward drive — pure visual walking | Each step gives a forward push — movement has a rhythmic, driven feel |
| `footfallMinSpeed` (0 → 1.5 wu/s) | Impulse fires even at slow walk — twitchy at low speed | Footfall only activates above a threshold — clean idle/walk distinction |

**What to observe**: Drop from a low vs. high ledge — crouch depth should scale with fall height. After landing, watch how long the body stays compressed — that's `crouchDissipationRate`. Hold down after landing: body stays compressed and movement blocks. Release: dissipation resumes. For footfall: walk and feel each step as a slight forward pulse; sprint — the rhythm should feel driven, not runaway.

**Visual vs. functional crouch**: `impactSquashOvershoot` and `squashPunchDecayRate` are **entirely visual** — they don't affect jump calculations. The functional crouch is `crouchAmount`; the visual adds `squashPunch` on top. Tune them independently.

**The committed crouch**: Holding down while crouched freezes energy AND blocks horizontal movement. Maximum energy storage costs mobility. Players who master this chain: heavy landing → crouch hold → released charged jump.

**Key interaction — Dim 9 (Jump Feel)**: `jumpOffsetFactor` multiplies `totalHipCompression`, which includes `crouchAmount`. Tune `jumpOffsetFactor` first, then control how much compression a given fall generates via `impactCrouchFactor` and `crouchDepthRatio`.

**Key interaction — Dim 6 (Hip Spring)**: Both hip spring compression and impact crouch feed into the jump. Tightly-damped hip (recovers fast) = short window for both; loosely-damped hip = long window.

**Key interaction — Dim 4 (Step Shape)**: Footfall impulse fires every time a step completes. Faster step cadence = more frequent impulses. If movement feels erratic at speed, check `footfallImpulse` before tuning step shape.

**Log when**: A hard landing visibly compresses the body and stores energy. A light hop barely registers. Jumping right after a heavy landing gives clearly more height. Walking feels driven by each step; sprinting has rhythm. The committed crouch creates a satisfying coil-and-release moment.

---

### 11. Body Integrity
*Live — no respawn. Variables: `leashSoftRadius`, `leashHardRadius`, `leashForceMult`, `maxFootSeparation`, `groundProbeDistance`, `edgeLandingNudge`, `minStepDistance`*

**Architecture note**: These variables are **defensive** — they prevent the procedural system from producing visibly broken states. The **body leash** constrains the torso from drifting too far from the foot center: within `leashSoftRadius`, no force; beyond it, a quadratic force ramp proportional to `moveForce × leashForceMult` pulls back; at `leashHardRadius`, the torso is hard-clamped and velocity zeroed. `maxFootSeparation` caps how far apart step targets can be placed — the stepping foot's target X is clamped to `other.lockX ± maxFootSeparation`. The **ground probe** fires each FixedUpdate downward `groundProbeDistance` px from each locked foot; if no walkable surface is found, the foot drops to Airborne. The **edge landing nudge** shifts a foot inward when it locks on a tilted contact normal, stabilizing it onto the platform face. The **obstacle pre-check** raycasts horizontally at arc-peak height before a step; if blocked, the step target is shortened or the step is cancelled if below `minStepDistance`.

**Body Leash:**

| Variable | Low value effect | High value effect |
|---|---|---|
| `leashSoftRadius` (2 → 12 px) | Leash begins pulling very close to foot center — tight, little drift freedom | Torso can drift far from feet before any force applies — loose feel |
| `leashHardRadius` (4 → 20 px) | Hard clamp fires frequently — torso teleports back often | Hard clamp is a distant safety net — rarely felt |
| `leashForceMult` (1 → 6) | Weak pull at the boundary — torso can push through easily | Strong pull — clear boundary, watch for oscillation near the edge |

**Step Integrity:**

| Variable | Low value effect | High value effect |
|---|---|---|
| `maxFootSeparation` (8 → 30 px) | Feet stay compact — no large splits | Feet can stride very far apart |
| `groundProbeDistance` (0.5 → 5 px) | Foot loses ground very easily — aggressive airborne transitions | Foot holds lock when slightly over an edge — stickier terrain adhesion |
| `edgeLandingNudge` (0 → 2 px) | No correction — foot lands exactly at edge contact point | Aggressively pushes foot inward — very stable edge landings |
| `minStepDistance` (0.3 → 3 px) | Very short steps still proceed after obstacle shortening | Short steps near walls are cancelled — foot stays locked rather than stepping trivially |

**What to observe**: These variables should be **largely invisible**. Run along a narrow ledge — does the body stay coherent without the leash visibly yanking? Sprint to one side — does the torso begin to resist after a natural-feeling distance? Walk off a ledge edge — does the foot drop appropriately? Walk toward a wall — does the foot shorten its step or cancel cleanly? Land on a platform edge — does the foot stick cleanly?

**The leash is not a feel dimension**: If players can feel it pulling, `leashSoftRadius` is too tight or `leashForceMult` is too high. The goal is a character that stays coherent without any apparent constraint.

**Key interaction — Dim 2 (Movement)**: Leash force = `moveForce × leashForceMult`. If `moveForce` increases significantly, the leash becomes proportionally stronger. Recalibrate `leashForceMult` if `moveForce` changes substantially.

**Key interaction — Dim 3 (Walking Trigger)**: `maxFootSeparation` caps how far ahead `strideProjectionTime` can place a step target. At high speeds, the separation limit may clamp steps that would otherwise reach far. If feet look like they're clamping unnaturally at sprint, try widening `maxFootSeparation` before reducing `strideProjectionTime`.

**Key interaction — Dim 8 (Stance Geometry)**: The leash anchors to the *current* foot positions, not ideal positions. When a foot is mid-step, the anchor shifts to the single locked foot. Wider `footSpreadX` means the foot center can shift more during single-foot phases.

**Log when**: Nothing looks broken. The torso doesn't teleport. Feet don't clip into walls. Locked feet don't hang in midair over gaps. Edge landings feel stable. Body integrity tuning is complete when you can't see it working.

---

### 12. Wall Interaction
*Live — no respawn. Variables: `maxWallSlideSpeed`, `wallSlideFootGravityScale`*

**Architecture note**: Wall sliding activates when three conditions are simultaneously true: (1) a foot has detected a wall via `FootContact.isWalled`, (2) the torso is descending (`linearVelocity.y < 0`), and (3) the player holds the direction key toward the wall. When active: torso Y velocity is clamped to `-maxWallSlideSpeed`. `FootMovement.SetWallSliding(true)` reduces airborne foot gravity to `wallSlideFootGravityScale`, keeping feet descending at a rate compatible with the torso rather than accelerating past it.

| Variable | Low value effect | High value effect |
|---|---|---|
| `maxWallSlideSpeed` (0.3 → 4 wu/s) | Very slow descent — nearly stationary wall cling | Barely reduces fall speed — wall slide almost imperceptible |
| `wallSlideFootGravityScale` (0.1 → 1.0) | Feet float high during slide — body appears to stretch upward | Feet accelerate downward — may separate below the body |

**What to observe**: Jump toward a vertical wall, start descending, and press into it. Does the descent visibly slow? Watch the feet — do they descend at a similar rate to the torso, or float up or drop down? Releasing the direction key should immediately restore normal fall.

**Foot gravity pairing**: The goal is matching `wallSlideFootGravityScale` to the clamped torso speed. Sweep `wallSlideFootGravityScale` while watching foot position relative to the torso during an active slide — feet should descend in sync with the body.

**Key interaction — Dim 1 (Foot Physics)**: `footGravityScale` is the baseline foot gravity. If the base foot gravity is very high, the wall slide scale needs to be proportionally lower to match torso descent rate.

**Key interaction — Dim 10 (Impact Crouch)**: Landing from a wall slide can produce landing velocity. With a high `impactCrouchFactor`, even a modest slide-to-floor landing may store meaningful crouch energy — enabling a wall-bounce jump.

**Log when**: Pressing into a wall while falling produces a clearly reduced descent rate. Feet remain at a believable height relative to the torso throughout the slide. Releasing the wall key immediately restores normal fall without a visual jolt.

---

### 13. Weight Feel
*Live — no respawn. Variables: `baseTorsoMass`, `ammoWeightPerUnit`*

**Architecture note**: Torso RB mass = `baseTorsoMass + (ammoCount × ammoWeightPerUnit)`. Jump magnitude is scaled by `baseTorsoMass / rb.mass` — at zero ammo the multiplier is 1.0 (no-op); as ammo adds mass, the multiplier drops below 1, reducing jump height. F = ma means the same `moveForce` produces less acceleration on a heavier torso. Springs keep identical perceptual feel (frequency/dampingRatio parameterization — ω and ζ are unchanged as mass grows).

| Variable | Low value effect | High value effect |
|---|---|---|
| `baseTorsoMass` (0.5 → 3.0) | Light base character — fast acceleration, high jumps at zero ammo | Heavy base — each ammo unit adds proportionally less fractional mass |
| `ammoWeightPerUnit` (0 → 0.1) | Ammo barely affects movement — carry as much as you like | Heavy ammo burden — full load noticeably reduces jump height and acceleration |

**What to observe**: Jump at zero ammo, note height. Reload to maximum ammo, jump again. Sprint acceleration at low vs. full ammo. The weight difference should be perceptible as a design tradeoff — ammo buys firepower at the cost of mobility.

**Base mass and the ammo gradient**: `baseTorsoMass` anchors the "nimble at zero ammo" feel. If you want the character heavy even when empty, raise `baseTorsoMass`. For maximum contrast (light empty, heavy full), keep `baseTorsoMass` low and `ammoWeightPerUnit` higher.

**Key interaction — Dim 9 (Jump Feel)**: Jump magnitude is multiplied by `baseTorsoMass / rb.mass`. If `baseTorsoMass` is raised, the same `jumpSpeed` produces higher jumps at zero ammo. Always reverify jump feel after changing `baseTorsoMass`.

**Key interaction — Dim 2 (Movement)**: A heavier torso requires more `moveForce` to reach the same speed in the same time. If maximum ammo makes the character uncontrollably sluggish, consider whether the weight penalty is intentionally severe.

**Log when**: Carrying maximum ammo feels meaningfully heavier — jump height and sprint are perceptibly reduced. Empty ammo feels nimble. Mid-ammo states feel like intermediate mobility tiers, not binary states.

---

### 14. Gun Feel
*Live — read per-frame. Variables: `firingSpeed`, `fireCooldown`, `projectileInitialScale`, `projectileGrowTime`, `tempMinProjectileDiameter`, `tempMaxProjectileDiameter`*

**Architecture note**: Projectile mass scales as `baseMass × diameter²` — a ball twice the diameter has four times the mass and delivers four times the collision impulse. This means larger shots deal far more physical damage to a centipede. `projectileInitialScale` and `projectileGrowTime` create a "barrel emergence" illusion — the ball spawns tiny at the barrel tip and scales up in flight, making it look like it came from inside the barrel.

| Variable | Low value effect | High value effect |
|---|---|---|
| `firingSpeed` (4 → 25 wu/s) | Slow projectile — must lead targets; arc visible | Near-hitscan — easy to hit; less satisfaction on contact |
| `fireCooldown` (0.05 → 0.8 s) | Rapid fire — sustained suppression | Slow, deliberate shots — each one matters |
| `projectileInitialScale` (0.01 → 0.3 wu) | Invisible at spawn — convincing barrel-emergence illusion | Ball just "appears" at full size, illusion broken |
| `projectileGrowTime` (0.05 → 0.4 s) | Fast pop — feels snappy | Slow growth — projectile is traveling while still tiny |
| `tempMinProjectileDiameter` (0.1 → 0.5 wu) | Many small shots — feels like a scatter gun | Large minimum — every shot is substantial |
| `tempMaxProjectileDiameter` (0.2 → 1.0 wu) | Uniformly small shots | Occasional huge shot — mass × diameter² means this can be devastating |

**What to observe**: Shoot at a stationary target at mid-range — does the travel time feel intentional or frustrating? Fire sustained — does the rhythm feel right? Watch the barrel when firing — does the ball look like it emerged from the barrel or just appear? Shoot different sizes at a centipede — do larger shots visibly hit harder?

**Diameter variance note**: `[TEMP]` labels indicate these will be replaced by a projectile queue system. For now, the random range creates shot variety — a wide range (0.1 → 0.8) means any given shot could be four times the area (and therefore mass) of the smallest shot, which is a 16× impulse difference.

**Key interaction — Dim 17 (Destruction)**: `firingSpeed` contributes directly to projectile kinetic energy. A small fast projectile can detach centipede segments just as well as a large slow one — the detachment check uses ball displacement from the node. Tune `firingSpeed` and projectile diameter together when adjusting destruction feel.

**Log when**: Shooting at range requires skill — enough travel time to feel satisfying to track. Fire rate feels appropriately rhythmic. The barrel emergence illusion reads as intentional, not as a glitch. Large shots visibly hit harder than small ones.

---

## Centipede Dimensions

> **Spring primer**: Centipede balls spring-chase their skeleton nodes using the same `(frequency, dampingRatio)` parameterization as the player springs. See the Player section spring primer above for the full explanation. The same knobs apply here: frequency controls snap speed, damping ratio controls whether it bounces.

---

### 15. Body Spring
*Live — no respawn. Variables: `wiggleFrequency`, `wiggleDampingRatio`, `wiggleMass`*

**Architecture note**: Each segment in the centipede is a kinematic `Rigidbody2D` that spring-chases a `SkeletonNode` every FixedUpdate via `Ball.UpdateCentipedeSpring()`. The config stores this spring as `(wiggleFrequency, wiggleDampingRatio, wiggleMass)` and exposes computed `WiggleStiffness` and `WiggleDamping` properties. `TuningManager` writes those computed values directly to each Ball's raw `springStiffness`/`springDamping` fields at runtime.

**Critical link to destruction**: The preemptive detachment check in `CentipedeController` uses `neededSq = (springStiffness / springMass) × (D² − d²)`. Since `springStiffness / springMass = wiggleFrequency²`, the energy threshold simplifies to `v² ≥ ω²(D² − d²)`. Changing `wiggleMass` doesn't affect this — mass cancels out. `wiggleFrequency` is the sole control over preemptive detachment sensitivity.

| Variable | Low value effect | High value effect |
|---|---|---|
| `wiggleFrequency` (3 → 25 rad/s) | Loose, jelly body — balls lag far behind nodes; large oscillations visible during turns | Tight tracking — balls stay nearly glued to nodes; minimal visual wobble |
| `wiggleDampingRatio` (0.1 → 1.0) | Long ripple after hits — segments ring like a chain of pendulums; body "shudders" | Instant settle — body stiffens after each perturbation, no persistent wobble |
| `wiggleMass` (0.1 → 5.0) | ω and ζ preserved, raw stiffness/damping magnitudes lower — subtle at moderate values | ω and ζ preserved, raw magnitudes higher — subtle |

**What to observe**: Watch the body as the centipede navigates a sharp turn — do the tail segments swing wide and ring back? Hit the centipede with a projectile and look for a ripple propagating head-to-tail. Low `wiggleDampingRatio` creates a satisfying "snake shudder" on impact that reads clearly as a biological creature. Too low and the body becomes hard to read — constantly oscillating segments look chaotic rather than reactive.

**Key interaction — Dim 17 (Destruction)**: `wiggleFrequency` directly sets the preemptive detach energy threshold (∝ ω²). High-frequency bodies absorb more kinetic energy before breaking — the spring must be moving fast relative to its displacement. You can use `wiggleFrequency` and `detachDistance` as two independent levers: `detachDistance` controls "how far before snap," `wiggleFrequency` controls "how much speed before snap."

**Key interaction — Dim 16 (Body Geometry)**: `followDistance` limits the physical space each ball has to oscillate in. Closely-spaced nodes constrain spring amplitude even at low frequency. Wide spacing allows large oscillations — low frequency + wide follow distance creates dramatic, visible wave motion down the body.

**Log when**: The body ripples noticeably after turns and projectile hits, reads as alive, and the ripple has a clear front-to-back travel that registers as a creature responding rather than a rigid rod bouncing.

---

### 16. Body Geometry ⚠
*Requires respawn. Variables: `nodeCount`, `followDistance`, `nodeRadius`*

**Architecture note**: `followDistance` is the distance each `SkeletonNode` trails behind its parent in the path-recording system — it sets physical segment spacing. `nodeRadius` is the world-unit radius for each ball's visual and collider. These two together define whether segments look overlapping (packed body), tangent (clean sausage), or gapped (skeletal chain). `nodeCount` determines total creature length and how many independent sub-centipedes can emerge from a split encounter.

| Variable | Low value effect | High value effect |
|---|---|---|
| `nodeCount` (2 → 20) | Short, nimble creature — quick to kill, few split opportunities | Long, imposing worm — many segments, many potential splits; each encounter is an escalating threat |
| `followDistance` (0.1 → 0.8 wu) | Segments tightly packed — dense, slug-like silhouette; body curves look smooth | Segments well-spaced — skeletal, open look; gaps visible between nodes during motion |
| `nodeRadius` (0.05 → 0.3 wu) | Small, delicate segments — narrow hitbox, difficult to shoot reliably | Large, prominent segments — fat hitbox, easy to hit; creature reads as bulky |

**What to observe**: Stand back and evaluate the silhouette in motion — does it read as a creature with a clear head-tail direction? Run along a wall and watch the tail swing — does it fill space naturally? Shoot off a tail segment: does the resulting split produce a sub-centipede of satisfying length, or a trivial free ball?

**Overlap math**: When `followDistance < 2 × nodeRadius`, adjacent balls overlap visually (touching or merged — looks like a slug). When `followDistance = 2 × nodeRadius`, they're tangent (clean sausage). When `followDistance > 2 × nodeRadius`, visible gaps appear (segmented, chain-like). Each aesthetic reads differently — overlapping is meatier; gapped is more mechanical and threatening-looking.

**`nodeCount` and threat escalation**: A long centipede hit in the middle splits into two independent hunters. High `nodeCount` means one encounter can become several. Consider the worst-case split scenario: a 12-node centipede hit twice in the middle produces four segments — if all become centipedes, the player is immediately overwhelmed. Splits that produce ≤2 nodes become free balls, not hunters.

**Key interaction — Dim 15 (Body Spring)**: `followDistance` determines the physical amplitude range available to the spring simulation. Wide follow distance + low `wiggleDampingRatio` = large, dramatic oscillations. Narrow follow distance constrains the spring regardless of frequency or damping.

**Key interaction — Dim 17 (Destruction)**: `nodeRadius` sets the collision circle — larger balls get hit by projectiles more readily. `nodeCount` determines how many detachments result in new centipedes vs. free balls. These two together define how "destructible" the creature actually feels in play.

**Log when**: The silhouette reads clearly as a distinct organism at play distance. Tail swings during navigation look like mass and inertia, not teleporting boxes. Splitting produces sub-centipedes long enough to feel like a new threat.

---

### 17. Destruction
*Live — no respawn. Variables: `detachDistance`*

**Architecture note**: Each FixedUpdate, `CentipedeController` checks every ball's position against its linked skeleton node. If the distance ≥ `detachDistance`, that ball detaches and everything trailing it splits into a new centipede (or free balls if the chain is too short). A second, preemptive check runs on all remaining balls: if their current spring speed is high enough to carry them past `detachDistance` — using the SHM energy estimate `v² ≥ ω²(D² − d²)` — they're detached immediately, preventing a slow cascade across several frames.

**What causes displacement**: Natural navigation — especially sharp turns — generates transient spring displacement as skeleton nodes jerk while balls lag. Projectile hits don't push kinematic balls via Unity physics, but the navigator's collision response (momentum inversion on impact) causes sudden heading changes that ripple through the trail, producing bursts of displacement across the body. Sustained fire compounds this — each hit resets or compounds the momentum change while the spring is still settling.

| Variable | Low value effect | High value effect |
|---|---|---|
| `detachDistance` (0.1 → 1.0 wu) | Fragile — sharp turns alone can split the centipede; any projectile hit causes detachment | Resilient — only severe, concentrated impacts split it; the creature feels nearly indestructible |

**What to observe**: Force the centipede into a tight corner so navigation causes sharp turns — does it split itself without being shot? Fire at one segment sustained — how many hits to separate it? Try a single large, fast projectile vs. several small ones. The goal: routine movement never splits the centipede, but a direct, concentrated hit always does.

**The strategic asymmetry of splits**: Detaching segment N splits the centipede into the original head chain (0 to N-1) and a reversed new centipede (N+1 to end). Hitting the middle produces two new hunters. Hitting near the tail produces one small loose ball. This means shooting the middle is the most dangerous thing a player can do if sub-centipedes then converge. This is a deliberate design tension.

**Key interaction — Dim 15 (Body Spring)**: `wiggleFrequency` sets the preemptive energy threshold (ω²). High frequency = spring is stiff = harder to preemptively detach. Tune `detachDistance` and `wiggleFrequency` independently: D for direct-hit sensitivity, ω for near-miss sensitivity.

**Key interaction — Dim 18 (Pathing Speed)**: Faster navigation generates more violent course corrections and more spring displacement per second. A `detachDistance` that holds together at low speed may trigger constantly at high speed. Always verify destruction feel after finalizing `speed`.

**Key interaction — Dim 14 (Gun Feel)**: Projectile speed and size determine how much the navigator is disturbed (via heading changes), which determines how much spring displacement builds up. `detachDistance` is the threshold that translates disturbance into actual splits. If destruction feels wrong, check whether gun feel or this threshold needs adjustment first.

**Log when**: Shooting a segment directly causes a clean, satisfying split. Sharp navigation turns alone do not split the centipede. Sustained fire on one spot reliably destroys that segment within 3–6 hits.

---

### 18. Pathing Speed ⚠
*Requires respawn. Variables: `speed`, `collisionCooldownDuration`*

**Architecture note**: `speed` is applied each FixedUpdate as `rb.linearVelocity = momentum × speed` — velocity-set, not force-based. There is no acceleration ramp; the centipede is instantly at full speed in whatever direction its momentum vector points. When using the scent navigator, this base speed is augmented by a trail-heat bonus (see Dim 24). `collisionCooldownDuration` prevents rapid momentum inversion when the head is pressed against a wall — it must expire before a new collision response fires.

| Variable | Low value effect | High value effect |
|---|---|---|
| `speed` (1 → 8 wu/s) | Slow, ominous approach — player has time to reposition and plan | Fast, aggressive pursuit — little reaction window; a stationary player is caught quickly |
| `collisionCooldownDuration` (0.1 → 2.0 s) | Rapid bouncing against walls — head jitters nervously off surfaces | Long cooldown — head can briefly stall in a corner before the response fires |

**What to observe**: Let the centipede approach from across the map — does the crossing time feel threatening? Sprint away from it — can you create distance with effort, or does it close relentlessly? Force it into a corner: does the collision cooldown produce a convincing bounce-and-recover, or does it stall awkwardly?

**Speed as a global difficulty knob**: Unlike most dimensions, `speed` has no aesthetic nuance — it is pure threat level. The right value is whatever makes the player want to run. Too slow and the centipede becomes furniture. Too fast and there's no counterplay. The scent navigator's speed boost (Dim 24) adds a dynamic ceiling above this base.

**`collisionCooldownDuration` and wall behavior**: When the head bounces off a wall (momentum inverts), the cooldown blocks the next collision response. If the head immediately re-contacts the same wall (e.g., approaching at a glancing angle), it slides through. This produces a "strafing along walls" behavior that can look natural or sticky depending on the value. Lower = more nervous/jittery. Higher = smoother wall-following but longer corner stalls.

**Key interaction — Dim 23 (Hunting Rhythm)**: `speed` amplifies the sweep-and-lock oscillator's effect on path shape. A fast centipede in its ballistic phase covers much more ground per cycle — high speed + low oscillation frequency produces enormous sweeping arcs that can circle the whole map.

**Key interaction — Dim 17 (Destruction)**: Faster navigation produces more violent heading changes and thus more spring displacement per second. Calibrate `detachDistance` after finalizing `speed` — they can't be tuned independently in isolation.

**Log when**: The player instinctively moves when they see the centipede approach. A skilled player can stay alive by kiting, but a stationary player is reliably caught within a few seconds.

---

## Scent Navigator Dimensions
*Only relevant when `useScentNavigator == true`. Skip these when using the arc pathfinder.*

> **How the scent field works**: The `ScentField` singleton maintains a ring buffer of recent player positions, each with a decaying weight. `Evaluate(pos)` sums `weight × exp(−age/decayTime) × exp(−distSq/2σ²)` over all samples — a superposition of time-decaying Gaussians. Gradient direction is estimated by sampling 8 radial points around the centipede head and summing the weighted directions. No path is planned — the route to the player emerges entirely from local hill-climbing on this field.

> **Multi-centipede behavior**: All centipedes share the same `ScentField`. Scent consumption by one centipede depletes the trail for all others, naturally creating territory behavior — centipedes spread out rather than stacking on the same path.

---

### 21. Scent Trail ⚠
*Init-only — requires respawn. Variables: `scentDecayTime`, `scentSigma`*

**Architecture note**: The player's position is sampled every `scentSampleInterval` seconds into a ring buffer of size `scentHistorySize`. At defaults (200 samples × 0.1s = 20 seconds of trail), capacity rarely needs adjustment. `scentDecayTime` is an exponential time constant: a sample's temporal weight is exactly 37% (e^−1) at age = `decayTime`, ~5% at 3× `decayTime`, and effectively invisible beyond that. `scentSigma` is the Gaussian spatial standard deviation: field strength falls to ~61% at distance σ from a sample, and ~14% at 2σ.

*Note: `scentHistorySize` and `scentSampleInterval` define total trail capacity. At defaults they rarely need tuning — adjust only if you want to experiment with much longer memory or finer trail resolution.*

| Variable | Low value effect | High value effect |
|---|---|---|
| `scentDecayTime` (2 → 20 s) | Trail vanishes quickly — centipede tracks only recent movement; a few seconds of stillness breaks pursuit | Long-lived trail — centipede follows paths walked minutes ago; the entire map stays scented |
| `scentSigma` (0.3 → 3.0 wu) | Narrow, precise corridor — centipede must nearly touch your exact path to feel the gradient | Wide, diffuse cloud — detectable from far away; steers well toward the general area but imprecise |

**What to observe**: Stand in one spot for 10 seconds, walk away, stand still again. Does the centipede follow the ghost path between your two positions? Try dashing back and forth over the same ground — does it respond to the concentrated trail? Hide behind cover without moving and time how long before pursuit weakens.

**The sigma/decay tradeoff**: Low sigma + high decay = precise, recent scent only — the centipede must nearly retrace your path, but loses you fast. High sigma + low decay = persistent regional field — the centipede can smell you from across the map but homes to the general area rather than your exact trail. This is the axis between "bloodhound tracking" and "area denial."

**Key interaction — Dim 22 (Scent Consumption)**: `scentSigma` sets how wide each sample's Gaussian footprint is. For consumption to fully suppress a sample, `scentConsumeRadius` should reach at least into the core of the Gaussian — rule of thumb: `scentConsumeRadius ≈ σ` or larger.

**Key interaction — Dim 24 (Trail Speed)**: Higher sigma produces much stronger raw field values (more Gaussian overlap per point). `scentGradientMaxStrength` must be recalibrated whenever sigma changes — they are tightly coupled.

**Log when**: Standing still, the centipede spirals convincingly toward your position. Walking away and returning causes the centipede to visibly track the breadcrumb path between your positions before reaching you.

---

### 22. Scent Consumption
*Live — no respawn. Variables: `scentConsumeRate`, `scentConsumeRadius`*

**Architecture note**: Each FixedUpdate, the navigator calls `ScentField.Consume(headPos, rate, radius, dt)`. Within `radius`, each sample's weight is reduced by `rate × proximity × dt`, where proximity is linear: 1 at center, 0 at the radius edge. A sample directly under the head loses `rate × dt` weight per second; samples at the radius edge are untouched. Samples consumed to zero are skipped in future evaluations. Because all centipedes share the field, one centipede's consumption erodes the trail for others.

| Variable | Low value effect | High value effect |
|---|---|---|
| `scentConsumeRate` (0.1 → 10 per second) | Slow erasure — centipede can circle the same trail many times without depleting it; loose, wide orbits | Fast erasure — trail collapses immediately behind the head; tight decisive spiral that closes quickly |
| `scentConsumeRadius` (0.2 → 2.0 wu) | Narrow erasure channel — head must nearly retrace its exact path to clear a sample | Wide clearing zone — broad swath erased per pass; field depletes faster relative to trail width |

**What to observe**: Stand completely still and count how many orbits the centipede makes before reaching you. High rate + wide radius = fewer orbits, fast close-in. Low rate + narrow radius = it may loop indefinitely. The ideal: a spiral that visibly tightens over time — the player can watch the trap close and feels urgency without it being instant.

**The spiral mechanics**: Without consumption, the gradient always points toward the densest accumulation — the centipede loops the same trail forever. Consumption "poisons" already-traversed zones, pushing the head toward unconsumed (fresher or denser) scent. This produces inward spiraling, not random wandering. The spiral tightness is controlled here; the spiral's radius is set by Dim 21 (sigma and decay).

**Multi-centipede note**: Two centipedes on the same target consume the trail cooperatively. High consume values mean the second centipede quickly loses the trail the first has followed — they spread out naturally. Low values let them stack on the same path.

**Key interaction — Dim 21 (Scent Trail)**: `scentConsumeRadius` should be calibrated against `scentSigma`. If sigma is large and consume radius is small, each pass only partially suppresses each sample — consumption is slow relative to trail density. For decisive consumption, ensure `scentConsumeRadius ≥ σ`.

**Key interaction — Dim 23 (Hunting Rhythm)**: Long ballistic phases (low oscillation frequency) mean the centipede covers more ground between consume calls — less trail erased per unit distance. High oscillation frequency + high consume rate = trail gets systematically cleared; low frequency + low rate = centipede may pass through fresh scent during a ballistic sweep without fully depleting it.

**Log when**: From a stationary position, the centipede visibly spirals inward rather than circling at a fixed radius. The spiral converges within 10–20 seconds of the centipede entering your scent cloud.

---

### 23. Hunting Rhythm
*Live — no respawn. Variables: `scentSteeringBlend`, `scentOscillationFrequency`, `scentGradientSampleRadius`*

**Architecture note**: These three variables shape *how* the gradient is translated into motion. Each FixedUpdate: (1) the oscillator phase advances, producing `sensitivity ∈ [0, 1]` via `0.5 + 0.5 × sin(phase)`; (2) gradient direction is computed at 8 radial points spaced at `scentGradientSampleRadius` around the head; (3) heading blends toward gradient at rate `scentSteeringBlend × sensitivity × dt`. When sensitivity is near 0 (the trough of the oscillator cycle), the centipede holds current momentum and sweeps ballistically. When sensitivity is near 1 (the peak), it snaps hard toward the gradient.

| Variable | Low value effect | High value effect |
|---|---|---|
| `scentSteeringBlend` (0.5 → 10 rad/s) | Gentle curves — heading shifts slowly toward the gradient; overshoots scent peaks, wide turns | Snappy turns — head pivots sharply at high sensitivity; can look jittery if very high |
| `scentOscillationFrequency` (0.1 → 2.0 Hz) | Long ballistic sweeps between snap-to events — ~10s cycles at 0.1 Hz; slow, deliberate, predatory | Rapid sensing pulses — constant course correction, no long sweeps; nervous, reactive behavior |
| `scentGradientSampleRadius` (0.2 → 2.0 wu) | Hyper-local gradient — noisy, reacts to tiny scent features; tight turns around small field variations | Broad gradient estimate — smooth global steering; head "sees" the shape of the field from further out |

**What to observe**: Run gentle slow curves for 20+ seconds and count how many times the centipede visibly changes heading. Is it making long predictable sweeps or nervous zigzags? Run a figure-8 — does the centipede cut across the center (smooth global steering) or trace your path exactly (tight local tracking)?

**The oscillator rhythm**: At `scentOscillationFrequency = 0.35 Hz` (default), each full cycle is ~2.86 seconds. For roughly half of that, sensitivity < 0.5 — the centipede sweeps ballistically. During the other half, it snaps toward gradient. This hunting rhythm is visible to a careful observer: coast, coast, turn, coast, turn. Low Hz makes it obvious and deliberate; high Hz makes it imperceptible. Both are valid — they create different creature personalities.

**Sample radius and field resolution**: The 8-point gradient ring should span across multiple Gaussian samples to measure meaningful slope. If `scentGradientSampleRadius` is much smaller than `scentSigma`, all 8 sample points land within a single Gaussian — the values are nearly equal and the gradient estimate is near-zero noise. Rule of thumb: `scentGradientSampleRadius ≈ scentSigma`. This value also controls the forward point used for the speed boost check (Dim 24) — a larger radius looks further ahead.

**Key interaction — Dim 21 (Scent Trail)**: `scentGradientSampleRadius` must match `scentSigma`. Any sigma change should be followed by revisiting this dimension.

**Key interaction — Dim 18 (Pathing Speed)**: At higher speed, the centipede covers far more ground per ballistic sweep. The same oscillator frequency produces very different path shapes at different speeds — a slow centipede makes tight loops; a fast one makes enormous arcs. If hunting rhythm looks wrong after adjusting speed, tune `scentOscillationFrequency` next.

**Log when**: Running curves, the centipede tracks you with a rhythm that feels intentional — not random, not locked-on. Decide on a personality: low blend + low oscillation = patient predator; high blend + high oscillation = reactive hunter. Pick what fits your game feel and lock it in.

---

### 24. Trail Speed
*Live — no respawn. Variables: `scentSpeedBoost`, `scentGradientMaxStrength`*

**Architecture note**: Each frame, the navigator samples field strength at `headPos + momentum × scentGradientSampleRadius` — the point directly ahead. This "forward heat" is normalized by `scentGradientMaxStrength` and clamped to [0, 1]. Final speed = `config.speed + config.scentSpeedBoost × trailHeat`. At maximum heat the centipede moves at `speed + scentSpeedBoost`; at zero heat it moves at base `speed`. There is no dead-zone — any nonzero forward heat produces some boost.

| Variable | Low value effect | High value effect |
|---|---|---|
| `scentSpeedBoost` (0 → 4 wu/s) | No acceleration bonus — trail heat has no effect on approach speed | Large surge — directly on a fresh hot trail the centipede can be significantly faster than baseline |
| `scentGradientMaxStrength` (0.5 → 20) | Even weak scent yields near-full boost — centipede surges constantly near any trail | Only very dense trail (stationary player, heavily overlapping samples) triggers full boost |

**What to observe**: Sprint in a straight line, then stop abruptly and watch the centipede's speed change as it enters your trail. Does it visibly surge? Then calibrate `scentGradientMaxStrength`: sweep it until the speed boost is just barely visible when you stand still — that's your normalization reference. Then set `scentSpeedBoost` for the desired threat ceiling.

**Calibration workflow — `scentGradientMaxStrength` first**: This is almost purely a calibration knob. The raw field value at a point depends on sigma, sample density, and decay — it has no intuitive unit. Tune `scentGradientMaxStrength` until the speed boost activates appropriately (not constantly, not never). Then treat `scentSpeedBoost` as the threat level dial. The existing guide's note — "calibrate max strength first, then tune boost" — is the correct order.

**What changes with sigma**: Larger `scentSigma` produces much higher raw field values (more Gaussian overlap). If sigma increases, `scentGradientMaxStrength` must increase proportionally or the boost will saturate immediately, making the centipede always sprint.

**Key interaction — Dim 21 (Scent Trail)**: `scentGradientMaxStrength` is sigma-dependent. Any time sigma changes, recalibrate this dimension immediately.

**Key interaction — Dim 18 (Pathing Speed)**: Speed boost adds to base speed. At high base speed, even a modest boost is a meaningful relative increase. At low base speed, a large boost creates a dramatic "lunge" on fresh trail — highly readable and satisfying if the player can react to it.

**Log when**: The centipede visibly surges when it crosses trail you recently ran. Sprinting far ahead creates meaningful breathing room. Standing still for 5+ seconds makes you a clearly hot target — the player can feel the difference.

---

### 25. Fallback Behavior
*Live — no respawn. Variables: `scentFallbackThreshold`, `scentFallbackBlend`*

**Architecture note**: Each FixedUpdate, field strength at the head position is compared to `scentFallbackThreshold`. If below, `IsInFallback = true` and heading blends toward the player's actual position at `scentFallbackBlend × dt`. This is intentionally weaker than typical gradient steering to prevent snapping. Fallback is not exclusive — if a gradient spike appears (player re-enters the area) while in fallback, gradient steering immediately re-dominates. The two behaviors compete continuously rather than switching cleanly.

| Variable | Low value effect | High value effect |
|---|---|---|
| `scentFallbackThreshold` (0.001 → 0.5) | Fallback fires only when the field is nearly dead — centipede can genuinely lose you if you hide long enough | Fallback fires early — switches to direct pursuit as soon as the trail thins; hiding is rarely effective |
| `scentFallbackBlend` (0.2 → 5.0) | Slow drift toward player — barely noticeable correction when trail is cold | Fast snap — abrupt behavioral shift from scent-following to direct line-of-sight pursuit |

**What to observe**: Find cover and hide completely still for 30–60 seconds (or set a short `scentDecayTime` to accelerate the test). Does the centipede eventually start closing in regardless? Is the transition gradual or abrupt? The fallback shouldn't feel like a different mode — it should feel like the centipede "found" you despite the cold trail, not like the AI switched systems.

**Threshold and the tension curve**: Low threshold = hiding is a real escape strategy — wait long enough and the centipede will drift away without the trail. High threshold = hiding barely helps; even faint residual scent triggers pursuit, and fallback kicks in the moment trail thins. This is one of the primary levers for whether "escape" is a viable player strategy.

**Key interaction — Dim 21 (Scent Trail)**: `scentDecayTime` determines how quickly the field drops below `scentFallbackThreshold`. Short decay + low threshold = fallback fires frequently during normal play. Long decay + high threshold = fallback may never fire unless the player hides for an extended time. The interplay between these two is the core tuning question: "how long must a player hide to break pursuit?"

**Key interaction — Dim 23 (Hunting Rhythm)**: During fallback, gradient steering is inactive — only the player-facing blend applies. The oscillator still runs, but since gradient is zero, the sensitivity pulse doesn't affect heading. Fallback effectively suspends the sweep-and-lock behavior until the centipede re-enters the scent field.

**Log when**: After an extended period of hiding with no movement, the centipede clearly changes behavior — either wandering away (low threshold) or slowly but surely approaching on a direct line (high threshold). The transition feels like a behavioral shift the player can observe and respond to, not a sudden lock-on.

---

## After All Dimensions

The system enters **Cross-Validation**. It will compare your final combined config against randomly perturbed variants, one at a time:
- Press **1** to keep the current base config
- Press **2** to adopt the perturbed variant (if it felt better overall)

This is optional — press **RightShift** to skip if you're satisfied.

When done: press **F9** to save a final named profile (e.g., "v1-final").
