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

### 3. Foot Spring
**Play**: Run, then stop suddenly. Change direction. Watch the feet.
**Feel for**: Low damping = feet swing past target and wobble. High damping = feet snap rigidly.
**Log when**: Feet settle convincingly after movement without looking glued.

### 4. Hip Spring
**Play**: Jump and land repeatedly. Watch the torso bob.
**Feel for**: `hipFrequency` = how fast torso snaps back to foot level. `hipDampingRatio` = how much it bobs. Low damping = head-bobbing on each landing.
**Log when**: Torso recovery after landing feels weighty but not laggy.

### 5. Torso Spring
**Play**: Change direction rapidly. Watch the torso visual lag behind the skeleton.
**Feel for**: Pure visual polish. Low damping = loose jelly. High = rigid lock.
**Log when**: The body reads as alive without feeling unstable.

### 6. Stance Geometry
**Play**: Stand still. Run. Jump. Evaluate silhouette.
**Feel for**: `standHeight` = torso height above feet. `footSpreadX` = width of stance.
**Log when**: The proportions look like a character, not a blob or a stilt-walker.

### 7. Jump Feel
**Play**: Jump from flat ground repeatedly (tests `jumpSpeed`). Crouch into the floor before jumping (tests `jumpOffsetFactor`).
**Feel for**: Arc height and hang time. Does crouching feel like it "charges" the jump?
**Log when**: Jumping feels satisfying and the crouch bonus is noticeable but not broken.

### 8. Gun Feel
**Play**: Shoot at targets at near, mid, and far range. Hold the fire button for sustained fire.
**Feel for**: `firingSpeed` = projectile velocity. `fireCooldown` = fire rate.
**Log when**: Hitting feels skill-based at range; auto-fire rhythm feels good.

---

## Centipede Dimensions

### 9. Body Spring
**Play**: Watch the centipede traverse the map. Hit it with a projectile. Watch the wobble and recovery.
**Feel for**: Low frequency = loose, jelly body. High = tight tracking. Low damping = ripple wobble after hits.
**Log when**: The body has organic character without falling apart visually.

### 10. Body Geometry ⚠
**Play**: Watch at rest and in motion. Evaluate segment spacing and body proportions.
**Feel for**: `followDistance` = gap between segments. `nodeRadius` = segment size (also affects detachment energy).
**Log when**: The centipede looks like a coherent creature.

### 11. Destruction
**Play**: Shoot the centipede with different projectile sizes, angles, and sustained fire.
**Feel for**: Too low = trivial to destroy. Too high = feels unfair/unkillable.
**Log when**: Breaking a segment feels like an achievement; surviving a hit feels close.

### 12. Pathing Speed ⚠
**Play**: Let the centipede approach you. Try to evade it.
**Feel for**: `speed` = raw threat level. `minTurnRadius` = how tightly it corners. High speed + low radius = aggressive.
**Log when**: It feels threatening but evadable with skill.

### 13. Pathing Behavior ⚠
**Play**: Watch approach patterns for at least 30 seconds. Try to predict where it's heading.
**Feel for**: `arcAngleVariance` = randomness of direction. `replanInterval` = how often it recalculates. High variance = unpredictable. Low replan = adaptive.
**Log when**: It feels intelligent without being psychic, and random without feeling aimless.

### 14. Wriggle Feel ⚠
**Play**: Watch the centipede approach from a distance.
**Feel for**: `waveAmplitude` = side-to-side sway magnitude. `waveFrequency` = speed of the wriggle. `wavePhaseOffsetPerNode` = how the wave propagates through the body.
**Log when**: The motion reads as organic and alive. The wave should feel like it's *driving* the creature, not just decorating it.

---

## After All 14 Dimensions

The system enters **Cross-Validation**. It will compare your final combined config against randomly perturbed variants, one at a time:
- Press **1** to keep the current base config
- Press **2** to adopt the perturbed variant (if it felt better overall)

This is optional — press **RightShift** to skip if you're satisfied.

When done: press **F9** to save a final named profile (e.g., "v1-final").
