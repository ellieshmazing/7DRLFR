
---
Date: 2026-03-06
Topic: Centipede autonomous pathfinding — circular arc steering with sinusoidal offset
Concepts:
  - **Steering Behaviors**: Rather than graph-based pathfinding, the centipede uses geometric arc paths derived from a single entry-angle constraint. This is a form of steering behavior — local, reactive motion control that produces plausible trajectories without a global map. The periodic replan handles long-range adaptation while the arc handles short-range execution, decoupling planning frequency from movement frequency.
  - **Emergent Complexity from Simple Rules**: The wriggling wave isn't applied to body nodes individually — it's applied to the head's *physical path*, and the trail system propagates it tail-ward naturally with built-in phase delay (the trail is a history of actual positions). The sinusoidal feel at each node is an emergent consequence of the trail recording a oscillating head, not an explicit computation per node.

---
Date: 2026-03-06
Topic: Temporary debug tooling
Concepts:
  - **Separation of concerns**: Isolating debug behavior into its own component (SpawnDebug) rather than scattering it across existing systems makes the feature's boundaries explicit and the cleanup path obvious — one file, one scene object.
  - **Access surface design**: Widening a method's visibility (private → public) to satisfy a caller is a deliberate tradeoff; when the caller is temporary, the visibility change should be noted as a matching cleanup obligation so the two changes are retired together.

---
Date: 2026-03-06
Topic: feature-implementer skill design
Concepts:
  - **Spec as contract**: A well-structured implementation spec (Goal, Architecture, Behaviors, Function Designs, Variables, Notes) acts as a contract between designer and implementer — each section eliminates a class of ambiguity. Goal names the integration surface. Architecture defines execution context. Behaviors name the atomic units. This mirrors how formal specs in software engineering reduce the gap between intent and implementation.
  - **Progressive disclosure in tooling**: Skills use a three-level loading system (metadata → body → bundled resources) that mirrors how humans read documentation — scan the summary, read relevant sections, consult references only as needed. Designing tools this way keeps the common case cheap while still supporting depth.

---
Date: 2026-03-06
Topic: Pathfinder mouse fallback / default behavior
Concepts:
  - **Graceful Degradation**: Designing a system to fall back to a simpler mode when its primary input is absent — here, the pathfinder targets the mouse when no player transform is set, so the feature is testable without a full scene setup.
  - **Separation of Concerns**: Keeping target resolution (`GetTargetPosition()`) isolated from movement logic means the steering math is identical in both modes; only the input source changes.

---
Date: 2026-03-06
Topic: Debug input reliability in Unity Editor (New Input System)
Concepts:
  - **Separation of concerns in debug tooling**: Embedding input polling inside a core system class (like CentipedeAssembler) risks coupling the system's lifecycle to debug behavior — if the object is disabled or transitions state, the input stops working. Dedicated debug scripts (SpawnDebug, CentipedeDestruction) on a stable manager GameObject give you a reliable, removable seam.
  - **Defensive null checks on input devices**: In Unity's New Input System, Keyboard.current can return null briefly during editor play-mode transitions or when input focus changes. A missing null guard silently throws a NullReferenceException that frame, making input appear "random" — the fix is a simple kb == null early-out before accessing any key state.

---
Date: 2026-03-06
Topic: Runtime variable tuning strategy — spring parameterization and multi-phase optimization
Concepts:
  - **Damping ratio parameterization**: A spring-mass-damper system is fully characterized by natural frequency (ω = √(k/m)) and damping ratio (ζ = c / 2√(km)), not by raw stiffness and damping. Storing ω and ζ instead of k and c makes the spring's *feel* independent of mass — you can change mass for collision tuning without altering the spring's response character. This reduces 3 arbitrary coupled knobs to 2 perceptually orthogonal ones per spring.
  - **Coordinate descent in perceptual space**: When tuning dozens of coupled game feel variables, searching the full space is intractable. Grouping variables into perceptual dimensions (clusters that a human perceives as one "axis of feel") and tuning them one at a time in dependency order is a form of coordinate descent. It converges because the dimensions are chosen to be roughly perceptually independent — changing torso spring feel doesn't invalidate the foot spring choice you already made.

---
Date: 2026-03-06
Topic: Sprite scaling and collider sizing in Ball.Init
Concepts:
  - **Coordinate Space Layering**: A sprite's visual size in world space is `pixelWidth / pixelsPerUnit × localScale` — not just `localScale`. When a system assumes sprites are "1 unit at scale 1" but the actual assets are 16px @ PPU=128 (0.125 units), the collider ends up 8× larger than the visual. Deriving scale from the sprite's real PPU at runtime eliminates this class of mismatch entirely.
  - **Convention vs. Reality Gap**: Bugs like this arise when a scale contract is documented but not enforced. The comment said "authored at 1wu" while the asset said otherwise. Reading from the asset directly (sprite.rect.width / sprite.pixelsPerUnit) makes the convention self-enforcing — the code is now true regardless of what any comment says.

---
Date: 2026-03-07
Topic: Runtime tuning system implementation — spring parameterization, live config reading, multi-phase optimizer
Concepts:
  - **Perceptual orthogonality in parameter spaces**: Replacing raw (stiffness, damping, mass) with (frequency, dampingRatio, mass) doesn't just reduce knob count — it makes the remaining knobs *perceptually independent*. Frequency controls how fast the spring responds; dampingRatio controls how much it oscillates. Changing one doesn't alter the other's feel, which means a human tuner can evaluate each axis in isolation. This is the deep reason coordinate descent works in feel-tuning: the dimensions are chosen to be perceptually orthogonal.
  - **Live config reading vs. dual-write as an architecture tradeoff**: Scripts that read from a config SO each frame (PlayerHipNode, PlayerFeet) get instant tuning response with zero sync code. Scripts that cache values locally (NodeWiggle, Ball) require explicit dual-write from the tuning system — more code, but the caching avoids per-frame reflection or property access in hot paths. The right choice depends on how many live instances exist and whether the frame cost matters.

---
Date: 2026-03-07
Topic: Competing pathfinder — scent-gradient navigation with consumed-zone suppression
Concepts:
  - **Emergent Navigation**: When complex, useful global behavior arises from a single simple local rule — "move toward where the smell is strongest" — rather than from planning. The spiral approach pattern on a stationary player is never explicitly coded; it falls out of the interaction between gradient ascent and field consumption. This distinction matters because emergent systems are often more interesting and surprising to play against than planned ones.
  - **Scalar Field Navigation**: Using a continuously-valued spatial function to guide movement, rather than a graph or geometric path. Every point in the world has a field value; steering is just climbing the gradient. This produces smooth, organic trajectories that naturally adapt to any player shape or obstacle arrangement, and gives multiple agents the ability to share a single environmental signal without explicit coordination.

---
Date: 2026-03-07
Topic: ScentField debug visualizer — GL overlay for scalar field and navigator state
Concepts:
  - **Scalar Field Visualization**: A scalar field assigns a single value to every point in space. Visualizing it means sampling on a grid and mapping each value to color — the same technique used in fluid sim heat maps and physics debug overlays. Here, sampling the scent field on a world-space grid reveals the Gaussian blending between footprints, the sigma influence radius, and where decay has hollowed out old regions of the trail.
  - **State Legibility via Direct Debug Rendering**: Complex AI behavior becomes tunable when every invisible internal variable has a visual proxy — arrows for vectors, pulsing rings for oscillators, color shifts for state flags. The gap between the gradient arrow and momentum arrow is the steering blend made visible in one glance, which would otherwise require reading logs or adding breakpoints.
---
Date: 2026-03-07
Topic: GL debug overlay rendering
Concepts:
  - **GL Matrix Stack**: In OnRenderObject(), Unity pre-loads the full projection × view matrix. GL.LoadIdentity() discards it; GL.MultMatrix(worldToCameraMatrix) only restores the view component — leaving vertices in view space with no projection applied. The safe pattern is GL.PushMatrix() alone, relying on the already-correct matrix.
  - **Defensive null guards**: Shader.Find() can return null in builds or stripped shaders. Guarding before Material construction prevents a silent null-ref crash that's hard to trace from a black screen.

---
Date: 2026-03-07
Topic: URP render pipeline compatibility for GL debug overlay
Concepts:
  - **Render Pipeline Callbacks**: Built-in RP dispatches OnRenderObject to all active MonoBehaviours; URP does not. URP exposes RenderPipelineManager.endCameraRendering as the equivalent hook, fired once per camera per frame from the SRP internals. Debug tools that target URP must subscribe to this event rather than override the legacy message.
  - **Explicit GL Matrix Setup**: Built-in RP pre-loads projection × view into the GL matrix stack before OnRenderObject fires. The URP endCameraRendering callback makes no such guarantee — projection and modelview must be set explicitly via GL.LoadProjectionMatrix and GL.modelview before issuing any world-space GL draw calls.

---
Date: 2026-03-07
Topic: FootMovement — procedural walking FSM
Concepts:
  - **Finite State Machine per limb**: Decomposing a character's leg behavior into discrete states (Locked/Stepping/Airborne) rather than a single spring makes each state's intent explicit and eliminates the contradictions that arise when physics, animation, and grounding logic fight over the same Rigidbody. The gait constraint (only one foot Stepping at a time) emerges naturally as a single predicate rather than a complex priority system.
  - **Kinematic override vs. physics**: Locking a foot means zeroing its velocity and snapping its position every FixedUpdate — effectively making a Dynamic RB behave kinematically without changing its type. This lets the foot participate in collision detection (still resolves contacts) while the locomotion system has total positional authority. The stable `lockPosition` bookkeeping value is what lets the hip spring target a non-jittery reference — when to trust the simulation vs. maintain your own authoritative state is a core procedural animation tradeoff.
---
Date: 2026-03-07
Topic: Debug-mode scent emission override
Concepts:
  - **Separation of simulation and debug concerns**: The scent field knows nothing about debug mode — it just exposes a SetPositionOverride hook. The visualizer owns the decision to use it. This keeps the simulation clean while still letting the debug tool feed in artificial state.
  - **Priority layering**: Player transform > override position > no emission. Each consumer in the stack does the simplest possible check and falls through cleanly. This pattern avoids conditional sprawl when adding debug paths to production systems.

---
Date: 2026-03-07
Topic: FootMovement FSM — debugging stride suppression
Concepts:
  - **Contact-state lag in FSMs**: Physics contact callbacks fire at the end of a physics step, before the next FixedUpdate. When a foot transitions from Locked→Stepping, the ground contact registered in the previous step is still active on the first frame of Stepping — causing the "early lock" guard to fire immediately and cancel every stride. The fix is to gate such guards on a progress threshold (past the arc peak), so the foot has time to physically lift off before re-locking is allowed.
  - **Local vs. world space collider radius**: CircleCollider2D.radius is in the GO's local space. When the GO has a non-unit scale (e.g. spriteLocalScale = 4), the world-space radius is radius × lossyScale — failing to account for this embeds step targets into the ground and cascades into incorrect ground reference heights for the hip spring.

---
Date: 2026-03-07
Topic: TuningStrategy dimension design for procedural walking
Concepts:
  - **Dependency ordering in tuning passes**: Tuning dimensions must follow the causal chain of the mechanic — stride trigger defines *when* a step fires, and step shape defines *what that step looks like*. Evaluating arc height before trigger cadence is correct confounds the two axes of feel, making both harder to isolate. Bottom-up ordering (trigger → shape → spring) mirrors how the system builds on itself.
  - **Separating setup parameters from feel parameters**: Variables like `maxWalkableAngle` and `stepRaycastDistance` are constraints that should be set correctly once, not swept for feel. True feel dimensions contain variables with a continuous perceptual gradient (low ↔ high produces a clearly different sensation). Mixing setup constants into feel dimensions wastes tuning rounds and obscures the subjective signal.

---
Date: 2026-03-07
Topic: Deprecating CentipedePathfinder in favor of ScentFieldNavigator
Concepts:
  - **Emergent Navigation vs. Planned Navigation**: Arc-based pathfinders compute an explicit geometric route to the target. Scent-gradient navigators have no route at all — the path emerges from following local field gradients. Emergent approaches are often simpler to tune and produce more surprising, organic behavior because the complexity lives in the environment (the field), not the agent.
  - **Dead Code Debt**: Keeping two competing systems in a codebase — even when only one is active — creates ongoing maintenance cost: documentation drift, config fields that do nothing, and confusion about which path is "real." Committing to one system and deleting the other makes the design legible and prevents future regressions from accidentally re-enabling the old path.

---
Date: 2026-03-07
Topic: Updating tuning dimension registry after removing arc pathfinder
Concepts:
  - **Coordinate Descent Ordering**: When you restructure a tuning dimension sequence, the order still must follow the dependency chain — each dimension should only be tuned after the variables it depends on are locked. Collapsing arc-specific dims and renumbering the scent dims means the scent system now tunes in the right order relative to base `speed` (dim 12 → 13–17), preserving the deliberate bottom-up dependency logic.
  - **Tuning as Documentation**: A dimension definition (name, variables, scenario, ranges) is more than a runtime config — it's a compressed specification of *what the variable controls perceptually*. Keeping it synchronized with the codebase is part of the same discipline as keeping VARIABLES.md in sync: when a variable is removed from the system, its tuning entry must be removed too, or the tuning workflow silently operates on fields that no longer exist.

---
Date: 2026-03-07
Topic: Tuning system dimension design for procedural walking
Concepts:
  - **Coordinate descent ordering**: When tuning variables that feed into each other, the order of dimensions matters as much as the dimensions themselves. Walk Shape (trigger geometry) must precede Walk Timing (duration/speed) because the cadence only reads correctly once you know where and when steps fire — evaluating timing on top of broken shape produces confounded judgments.
  - **Init-only vs live variables**: Distinguishing variables that must take effect at spawn (rb.mass, gravityScale) from those readable per-frame determines whether the tuning system can sweep them continuously or must batch-respawn. FootMovement's FSM vars are all live-readable, making them cheap to tune — a key advantage of the per-frame config pattern.

---
Date: 2026-03-07
Topic: Player registry and auto-targeting
Concepts:
  - **Observer Pattern**: Systems subscribe to a shared event rather than polling or holding direct references. `PlayerRegistry.OnPlayerChanged` lets the camera and navigator react instantly to player spawn/death without coupling them to `PlayerAssembler`.
  - **Graceful Degradation**: When the primary target is absent, a system falls back to sensible behavior (mouse follow) rather than failing. The fallback keeps the game playable in editor dev sessions where no player exists yet.

---
Date: 2026-03-07
Topic: footColliderRadius bug diagnosis
Concepts:
  - **Single Source of Truth**: When two code paths produce the same value via different routes (assembler vs. Start()), one is necessarily wrong or redundant. The diagnostic question is always which one is authoritative — here, the assembler should own all wiring, and Start() was silently patching a mistake rather than the two being equivalent alternatives.
  - **Local vs. World Space in Physics**: Collider radii are local-space values that Unity scales by the object's lossyScale for physics. A formula like `col.radius * lossyScale.x` recovers the world-space radius — but the simpler expression `0.5f * playerScale` reaches the same value without reading runtime component state, making it better as a setup-time computation.

---
Date: 2026-03-07
Topic: Dead code removal — old foot system
Concepts:
  - **System Archaeology**: When replacing a system incrementally, the old code often lingers past its death date — referenced in comments, stale hierarchy docs, and vestigial base classes. Periodically sweeping for orphaned files and ghost references keeps the codebase honest and prevents future readers from being misled about how the system actually works.
  - **Minimal Interface Principle**: `PlayerSkeletonNode` used to expose tree traversal, snapping, and world-position helpers that the new `FootMovement` system never calls. Stripping it to just `localOffset` + gizmo makes its true contract obvious — it's a tagged scene object, not a behavior node.

---
Date: 2026-03-07
Topic: Jump mechanic and procedural foot-hip spring coupling
Concepts:
  - **Coupled oscillators**: When two springs reference each other as targets (hip chases foot Y, foot chases hip Y), they form a coupled oscillator — each influences the other's equilibrium. The emergent behavior (realistic mutual wobble during flight) arises from the interaction, not from either spring alone. This is a common source of organic feel in procedural animation.
  - **Execution order as logic**: Placing jump velocity assignment at order -10 (after FootMovement locks at -20) is a deliberate architectural choice where script execution order IS the control flow. The lock zeroes the velocity, then the jump overwrites it — this only works because the pipeline is deterministic. Breaking that ordering would silently break the mechanic.

---
Date: 2026-03-07
Topic: Ball type extension system — OnLaunch hooks and composable behavior
Concepts:
  - **Lifecycle hooks as extension points**: Rather than subclassing Ball for each type, the system exposes named hooks (OnLaunch, OnFixedUpdate, OnCollision) that fire at meaningful moments. Each hook is a seam — a place where behavior can be injected without modifying the host class. The richer the hook vocabulary, the more types you can create without touching core code.
  - **ScriptableObject as strategy pattern**: Using ScriptableObjects for BallEffect and BallMovementOverride is the Unity idiom for the Strategy pattern — swapping an algorithm at data time rather than compile time. One BallDefinition asset composes up to three strategies (sprite/mass, movement, effect), and the same strategy asset can be shared across multiple definitions without duplication.

---
Date: 2026-03-07
Topic: Jump grounding — foot re-lock fix
Concepts:
  - **Intent-gated state transitions**: Gating an FSM transition on velocity direction (y < 0) converts a raw physics fact (collision contact) into a game-logic fact (the foot is genuinely descending). Without this gate, physics events that are technically true but semantically wrong (contact persisting for one frame after a jump) can drive the FSM into incorrect states.
  - **Physics vs. game-logic lag**: Collision callbacks (OnCollisionExit2D) fire asynchronously relative to gameplay code, creating a gap where `isGrounded` can remain true while the entity has logically left the ground. The velocity check bridges this gap without arbitrary timers.

---
Date: 2026-03-07
Topic: Tuning dimension reorganization — scent navigation & procedural walking
Concepts:
  - **Dimension deprecation hygiene**: Tuning parameters belong to specific mechanical systems. When a system is replaced (arc-based pathing → scent navigation), its tuning dimensions become invalid garbage — they reference fields that no longer exist, and leaving them in the array would cause reflection errors at runtime. Treating the dimension list as living documentation that must stay synchronized with the config forces you to notice when mechanics diverge.
  - **Parameter taxonomy**: Not every config field belongs in a tuning dimension. Some params are structural (nodeCount, scentHistorySize), some are tech constants (stepRaycastDistance), and some are [TEMP] placeholders. Choosing which to expose for interactive tuning — and which to leave as raw SO fields — is itself a design decision: it defines the *tunable surface* of the game feel.

---
Date: 2026-03-07
Topic: Player tuning guide — expanded dimension documentation
Concepts:
  - **Parameterization legibility**: Exposing springs as (frequency, dampingRatio) rather than raw stiffness/damping lets a tuner reason about "snappiness" and "bounciness" independently — two knobs that map directly to perceptible qualities. When a parameter space has been carefully chosen, documentation can teach intuition rather than just describe values.
  - **Spring stacking**: Layering two springs in sequence (foot spring → hip spring → torso spring) lets each carry a distinct perceptual job (weight of landing, body momentum, visual aliveness) while composing naturally. The risk is resonance — two underdamped springs at similar frequencies will reinforce each other into muddy oscillation.

---
Date: 2026-03-07
Topic: Centipede and Scent Navigator tuning documentation
Concepts:
  - **Emergent behavior from local rules**: The scent navigator never plans a path — the centipede's route to the player emerges entirely from local gradient-following on a sum of decaying Gaussians. The hunting rhythm, spiral, and territory-splitting between multiple centipedes are all emergent consequences of three simple per-frame operations: sample, blend, consume.
  - **Parameter orthogonality as design tool**: Variables like `wiggleFrequency` and `detachDistance` each control a distinct aspect of a mechanic (energy threshold vs. displacement threshold), which makes them tunable independently. Identifying where two parameters are truly orthogonal vs. tightly coupled (like `scentSigma` and `scentGradientMaxStrength`) is a core documentation discipline — it tells the tester which knobs to reach for without disturbing other knobs.

---
Date: 2026-03-07
Topic: Step-based tuning sweep for init-only variables
Concepts:
  - **Discrete vs. Continuous Parameter Spaces**: Some game parameters (like collider radius or pathfinder grid resolution) only take effect after a full respawn, making continuous sweeps meaningless — the entity lives its whole life at one value. A discrete step-and-observe loop is the correct mental model for these: set, respawn, watch, repeat. The step size (10-25% of range) trades coverage for observation time.
  - **Ping-Pong Iteration**: Rather than sweeping monotonically (missing the high or low end) or randomly (revisiting values), a direction-reversing walk guarantees full coverage of the parameter space with predictable, human-followable progression — the same property the sine sweep provides for continuous variables, just discretized.
