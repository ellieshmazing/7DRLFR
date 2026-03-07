
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
