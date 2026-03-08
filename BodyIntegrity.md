### 11. Body Integrity
*Live — read per-frame. Variables: `leashSoftRadius`, `leashHardRadius`, `leashForceMult`, `maxFootSeparation`, `groundProbeDistance`, `edgeLandingNudge`, `minStepDistance`*

**Architecture note**: This dimension contains five distinct defensive mechanisms — none of them are about feel, all of them are about preventing the procedural system from producing incoherent states.

1. **Body leash** (`PlayerSkeletonRoot`): Every FixedUpdate, if the torso's X position has drifted more than `leashSoftRadius` (world units) from the foot center X, a quadratic spring pulls it back. Force ramps from zero at the soft boundary to `moveForce × leashForceMult` at the hard boundary. At exactly `leashHardRadius`, the torso is hard-clamped back to the boundary and any outward velocity is zeroed — the spring is a warning; the clamp is the guarantee.

2. **Ground probe** (`FootMovement`): Each FixedUpdate, every Locked foot fires a short downward raycast of length `groundProbeDistance`. If it finds nothing walkable below, the foot immediately transitions to Airborne. This catches edge-walk cases where the floor has run out but the foot is still flagged as grounded — without it, one foot can remain "locked" to empty space while the other walks normally, breaking the hip reference Y.

3. **Foot separation limit** (`FootMovement.StartStep`): When a step is triggered and the other foot is already Locked, the new step target X is clamped so the two feet are never more than `maxFootSeparation` apart. If the clamp moves the target, the target Y is re-raycast to find valid ground at the clamped X. Without this, a fast-moving character can stride so far ahead that the feet are in two different game scenarios simultaneously.

4. **Edge landing nudge** (`FootMovement.LockFoot`): When a foot transitions from Airborne to Locked, the contact normal from `FootContact` is checked. If the normal has a significant horizontal component (X > 0.1) — indicating a tilted surface or platform edge — the lock position is nudged inward by `edgeLandingNudge`. This prevents feet from locking on the very edge of a platform where the next ground probe will immediately fail.

5. **Obstacle pre-check** (`FootMovement.StartStep`): Before committing to a step, a horizontal raycast checks for non-walkable colliders in the step path. If an obstacle is found, the step target X is shortened to stop at the obstacle surface minus a foot-radius clearance. If the shortened step would be smaller than `minStepDistance`, the step is cancelled entirely — the foot stays locked rather than taking a one-pixel micro-step into a wall.

| Variable | Low value effect | High value effect |
|---|---|---|
| **— Body Leash —** | | |
| `leashSoftRadius` (4 → 10 px) | Leash spring engages very close to foot center — torso is kept almost directly above feet at all times | Leash doesn't engage until torso has drifted noticeably — permits wider, more dynamic separation |
| `leashHardRadius` (8 → 16 px) | Hard clamp fires frequently; torso feels like it hits a wall at moderate drift | Hard clamp is a distant safety net; in normal play it never fires |
| `leashForceMult` (2 → 5) | Weak pull at the hard boundary — the quadratic ramp is gentle, transitions into the clamp more abruptly | Strong pull well before the clamp — the spring itself prevents the hard clamp from ever triggering |
| **— Step Integrity —** | | |
| `maxFootSeparation` (12 → 30 px) | Feet must stay close together — sprinting forces micro-steps, constrained cadence | Wide strides allowed — feet can project far apart before the clamp engages |
| `groundProbeDistance` (1 → 4 px) | Very short probe — a locked foot can lose ground quickly as the character walks off an edge; more frequent unlocks | Long probe — foot remains locked on thin ledges and slight dips; less likely to trigger false airborne |
| `edgeLandingNudge` (0.3 → 1.5 px) | Small inward push on tilted landings — foot may still be near enough to the edge to fail the next ground probe | Larger push — foot is moved well onto stable ground, but too high a value nudges the character visibly inward |
| `minStepDistance` (0.5 → 2 px) | Very short steps are allowed after obstacle shortening — foot may micro-step into a corner | Only steps with meaningful travel distance are allowed — near-wall positions cause the foot to stay locked |

**What to observe**: Body integrity variables are working correctly when you cannot see them doing anything. Run into a corner — the torso should press against the wall naturally and stop, not teleport or rubber-band. Walk off a platform edge — one foot should drop to airborne cleanly as it loses ground, not hover. Jump onto a thin ledge — both feet should land stably without one immediately falling off the edge. Sprint across open ground — the feet should take long confident strides without appearing to separate unnaturally. These are pass/fail states, not feel observations.

**Finding the failure modes**: The only way to tune these parameters is to push the system until it breaks. Sprint and slam into a wall corner to stress the leash and the obstacle pre-check simultaneously. Walk slowly off the edge of a platform to observe the ground probe cadence. Land on progressively thinner ledges to find the edge landing nudge threshold. If nothing breaks at normal play speeds, the variables are doing their job.

**Key interaction — Dim 2 (Movement)**: The leash spring force is `moveForce × leashForceMult`. These are directly multiplied — raising `moveForce` makes the leash stronger in absolute terms. If you increase `moveForce` for a more responsive character, the leash may become unnecessarily aggressive; consider whether `leashForceMult` needs to come down proportionally to preserve the original leash feel.

**Key interaction — Dim 3 (Walking Trigger)**: `maxFootSeparation` is a hard ceiling on how far apart a step can project the feet. At high `maxSpeed` combined with high `strideProjectionTime`, the stride calculation may naturally want to project a foot well beyond `maxFootSeparation` — the clamp will fire and shorten it. This interaction is invisible at moderate speeds but can visibly suppress stride length at sprint. If sprinting looks choppy without an obvious cause, check whether the separation clamp is activating.

**Key interaction — Dim 8 (Stance Geometry)**: The body leash anchors to the *current* foot center X — the midpoint between the two actual foot positions — not to the ideal `footSpreadX` positions. During a step, one foot is in motion and its X contribution shifts; the leash anchor shifts with it. Wider `footSpreadX` means the feet spend more time away from the torso's X, making the leash anchor slightly more dynamic. If leash behavior feels different after adjusting stance geometry, this is why.

**Log when**: The character stays spatially coherent in every situation tested — no torso teleports, no floating feet, no one-frame corrections visible as pops. The integrity system is silent. Nothing broke.
