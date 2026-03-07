
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
Topic: FootMovement — procedural walking FSM
Concepts:
  - **Finite State Machine per limb**: Decomposing a character's leg behavior into discrete states (Locked/Stepping/Airborne) rather than a single spring makes each state's intent explicit and eliminates the contradictions that arise when physics, animation, and grounding logic fight over the same Rigidbody. The gait constraint (only one foot Stepping at a time) emerges naturally as a single predicate rather than a complex priority system.
  - **Stable reference vs. raw physics**: The hip spring targets a `lockPosition` (discrete, only updated at state transitions) rather than the raw `rb.position` of the foot (jittery, one physics step behind). This is the core tradeoff in procedural animation — when to trust the physics simulation and when to maintain your own authoritative bookkeeping value.
---
Date: 2026-03-07
Topic: URP render pipeline compatibility for GL debug overlay
Concepts:
  - **Render Pipeline Callbacks**: Built-in RP dispatches OnRenderObject to all active MonoBehaviours; URP does not. URP exposes RenderPipelineManager.endCameraRendering as the equivalent hook, fired once per camera per frame from the SRP internals. Debug tools that target URP must subscribe to this event rather than override the legacy message.
  - **Explicit GL Matrix Setup**: Built-in RP pre-loads projection × view into the GL matrix stack before OnRenderObject fires. The URP endCameraRendering callback makes no such guarantee — projection and modelview must be set explicitly via GL.LoadProjectionMatrix and GL.modelview before issuing any world-space GL draw calls.
