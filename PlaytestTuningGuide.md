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

> **Navigator mode**: Dimensions 14–16 (Pathing Speed, Pathing Behavior, Wriggle Feel) apply to the arc pathfinder. Dimensions 17–21 (Scent Trail through Fallback Behavior) are only relevant when the centipede uses the scent navigator (`useScentNavigator == true`). Skip whichever group doesn't apply to your current config.

---

## Player Dimensions

### 1. Foot Physics ⚠
**Play**: Drop from a ledge. Jump repeatedly. Try a crouched jump.
**Feel for**: Fall speed and landing weight. Does jump height feel right for the mass? Heavier mass = lower jumps.
**Log when**: Landing arc feels natural, not floaty or brick-heavy.

### 2. Movement
**Play**: Run left-right. Start and stop hard. Try quick direction reversals.
**Feel for**: `moveForce` = how snappy acceleration feels. `maxSpeed` = top speed ceiling.
**Log when**: Starting and stopping feel responsive without being twitchy.

### 3. Walking Trigger
**Play**: Run at a slow walk, then gradually speed up to a sprint. Watch when and how often feet step.
**Feel for**: `strideTriggerDistance` = how far a foot lags before stepping (low = constant twitchy steps, high = feet shuffle and lag). `idleSpeedThreshold` = speed below which feet just correct to neutral. `strideProjectionTime` = how far ahead feet reach on fast strides.
**Log when**: At a slow walk feet step deliberately; at a sprint feet reach out ahead with urgency.

### 4. Step Shape
**Play**: Walk slowly, then sprint. Watch the arc each foot traces through the air.
**Feel for**: `stepHeight` = lift per step (high = marching-band lift, low = shuffle). `baseStepDuration` = time for one step at rest. `minStepDuration` = minimum step time at sprint (prevents infinitely fast leg cycling). `stepSpeedScale` = how much speed compresses step duration.
**Log when**: Walk cadence feels unhurried; sprint cadence feels urgent without looking mechanical.

### 5. Foot Spring
**Play**: Jump and land. Watch feet during the airborne phase and on touchdown.
**Feel for**: Low damping = feet oscillate past formation and wobble on landing. High = feet snap immediately to position. (This spring only governs the airborne phase — Locked and Stepping feet are position-driven.)
**Log when**: Feet settle convincingly under the body after landing without looking glued.

### 6. Hip Spring
**Play**: Jump and land repeatedly. Watch the torso bob.
**Feel for**: `hipFrequency` = how fast torso snaps back to foot level. `hipDampingRatio` = how much it bobs. Low damping = head-bobbing on each landing.
**Log when**: Torso recovery after landing feels weighty but not laggy.

### 7. Torso Spring
**Play**: Change direction rapidly. Watch the torso visual lag behind the skeleton.
**Feel for**: Pure visual polish. Low damping = loose jelly. High = rigid lock.
**Log when**: The body reads as alive without feeling unstable.

### 8. Stance Geometry
**Play**: Stand still. Run. Jump. Evaluate silhouette.
**Feel for**: `standHeight` = torso height above feet. `footSpreadX` = width of stance.
**Log when**: The proportions look like a character, not a blob or a stilt-walker.

### 9. Jump Feel
**Play**: Jump from flat ground repeatedly (tests `jumpSpeed`). Crouch into the floor before jumping (tests `jumpOffsetFactor`).
**Feel for**: Arc height and hang time. Does crouching feel like it "charges" the jump?
**Log when**: Jumping feels satisfying and the crouch bonus is noticeable but not broken.

### 10. Gun Feel
**Play**: Shoot at targets at near, mid, and far range. Hold the fire button for sustained fire.
**Feel for**: `firingSpeed` = projectile velocity. `fireCooldown` = fire rate.
**Log when**: Hitting feels skill-based at range; auto-fire rhythm feels good.

---

## Centipede Dimensions

### 11. Body Spring
**Play**: Watch the centipede traverse the map. Hit it with a projectile. Watch the wobble and recovery.
**Feel for**: Low frequency = loose, jelly body. High = tight tracking. Low damping = ripple wobble after hits.
**Log when**: The body has organic character without falling apart visually.

### 12. Body Geometry ⚠
**Play**: Watch at rest and in motion. Evaluate segment spacing and body proportions.
**Feel for**: `followDistance` = gap between segments. `nodeRadius` = segment size (also affects detachment energy).
**Log when**: The centipede looks like a coherent creature.

### 13. Destruction
**Play**: Shoot the centipede with different projectile sizes, angles, and sustained fire.
**Feel for**: Too low = trivial to destroy. Too high = feels unfair/unkillable.
**Log when**: Breaking a segment feels like an achievement; surviving a hit feels close.

### 14. Pathing Speed ⚠
**Play**: Let the centipede approach you. Try to evade it.
**Feel for**: `speed` = raw threat level. Higher speed = more aggressive.
**Log when**: It feels threatening but evadable with skill.

---

## Scent Navigator Dimensions
*Only relevant when `useScentNavigator == true`. Skip these when using the arc pathfinder.*

### 17. Scent Trail
**Play**: Stand still for 10 seconds, walk away and stand still again. Watch the centipede follow the ghost path.
**Feel for**: `scentDecayTime` = how long the trail lingers (high = centipede can track old paths across the whole map). `scentSigma` = spatial spread of the trail (high = blurry cloud, gradient activates from further away; low = centipede must stay close to the line).
**Log when**: The centipede visibly follows where you've been, not just where you are.

### 18. Scent Consumption
**Play**: Stand completely still and watch the centipede spiral in toward you.
**Feel for**: `scentConsumeRate` = how fast the trail erases as the centipede passes. `scentConsumeRadius` = how wide the erasure is. High rate + wide radius = tight decisive spiral that closes fast. Low rate = looser circles, centipede may drift past and arc back.
**Log when**: The spiral tightens convincingly; centipede clearly homes in rather than wandering.

### 19. Hunting Rhythm
**Play**: Run gentle, slow curves for 20+ seconds. Watch how the centipede's heading changes over time.
**Feel for**: `scentSteeringBlend` = aggressiveness of turn-rate toward gradient (high = sharp reactive turns). `scentOscillationFrequency` = sweep-and-lock cycle (high = rapid snappy cycles, low = long ballistic sweeps). `scentGradientSampleRadius` = how wide the gradient sample ring is (high = smooth global steering, low = hyper-local).
**Log when**: Low blend + low oscillation = predatory deliberate approach. High blend + high oscillation = nervous reactive zigzag.

### 20. Trail Speed
**Play**: Sprint in a straight line, then stop abruptly. Watch if the centipede surges.
**Feel for**: First calibrate `scentGradientMaxStrength` (sweep until speed boost is clearly visible). Then tune `scentSpeedBoost` for threat level — how much faster does it close when it's directly on your trail?
**Log when**: The surge feels tactically meaningful — sprinting ahead matters.

### 21. Fallback Behavior
**Play**: Hide behind cover for 30+ seconds until the scent field fully decays.
**Feel for**: `scentFallbackThreshold` = how low the field must drop before direct pursuit activates (high = centipede switches to chasing you quickly when the field is sparse). `scentFallbackBlend` = how fast it snaps toward your position once fallback fires.
**Log when**: The transition from scent-following to direct chase feels purposeful, not abrupt.

---

## After All Dimensions

The system enters **Cross-Validation**. It will compare your final combined config against randomly perturbed variants, one at a time:
- Press **1** to keep the current base config
- Press **2** to adopt the perturbed variant (if it felt better overall)

This is optional — press **RightShift** to skip if you're satisfied.

When done: press **F9** to save a final named profile (e.g., "v1-final").
