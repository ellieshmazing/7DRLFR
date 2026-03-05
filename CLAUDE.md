# Project: BallWorld

Use `theone-unity-standards` skill for all code.

---

## Unity Environment
- **Unity Version**: Unity 6 (confirmed — uses `rb.linearVelocity`, `rb.linearDamping`)
- **Input System**: **New Input System** (`using UnityEngine.InputSystem`) — legacy `UnityEngine.Input` is **disabled**
- **Physics**: Unity 2D Physics (`Rigidbody2D`, `Collider2D`)
- `ForceMode2D` only has `Force` and `Impulse` — no `Acceleration` or `VelocityChange`

---

## Core Architecture: The Ball Paradigm
All entities share this structure:
- **Skeleton nodes** — plain GameObjects, no physics, positioned by code each frame
- **Visual children** — child GOs with `SpriteRenderer` + spring physics; chase their parent node
- **Colliders and Rigidbodies live on the visuals, never on the skeleton nodes**

---

## Codebase Map

### Centipede Enemy — `Centipede/`
| File | Role |
|---|---|
| `SkeletonNode.cs` | Trail-following node: records parent path, follows at `followDistance` |
| `SkeletonRoot.cs` | Calls `RecordAndPropagate()` each FixedUpdate |
| `NodeWiggle.cs` | LateUpdate spring-damper on visual child (transform-based, no RB) |
| `CentipedeConfig.cs` | ScriptableObject — all centipede params |
| `CentipedeAssembler.cs` | Runtime spawner |
| `DebugMouseFollow.cs` | Dev tool: head follows mouse via new Input System |

### Player Character — `Player/`
| File | Role |
|---|---|
| `PlayerSkeletonNode.cs` | Fixed-offset node; `SnapTo()` lets foot visuals pull nodes upward |
| `PlayerSkeletonRoot.cs` | WASD forces (horizontal only), standHeight Y-constraint via foot visual positions |
| `PlayerFootWiggle.cs` | FixedUpdate spring on Dynamic RB; snaps node up when `node.y < visual.y` (ground contact) |
| `PlayerArmController.cs` | LateUpdate: orbits `torsoVisual` at fixed radius, rotates to face mouse |
| `PlayerConfig.cs` | ScriptableObject — all player params |
| `PlayerAssembler.cs` | Runtime spawner matching CentipedeAssembler pattern |

---

## Critical API Patterns

### Input — new Input System only
```csharp
using UnityEngine.InputSystem;

Vector2 mouseScreen = Mouse.current.position.ReadValue();
Vector2 mouseWorld  = Camera.main.ScreenToWorldPoint(mouseScreen);

var kb = Keyboard.current;
float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
        - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f);
```

### Rigidbody2D (Unity 6)
```csharp
rb.linearVelocity   // NOT rb.velocity
rb.linearDamping    // NOT rb.drag
```

### Assembler pattern
```csharp
root.SetActive(false);  // suppress Awake/Start during construction
// ... AddComponent, create child hierarchy ...
root.SetActive(true);   // Awake + Start fire on complete hierarchy
```

### Sprite sizing
```csharp
// Sprites authored at 1 world-unit diameter at scale 1
visualGO.transform.localScale = Vector3.one * radius * 2f;
collider.radius = 0.5f; // local space → radius world units after scaling
```

---

## Script Execution Order (`[DefaultExecutionOrder]`)
| Order | Script | Loop |
|---|---|---|
| -10 | `PlayerSkeletonRoot` | FixedUpdate |
| 0 | `PlayerFootWiggle` | FixedUpdate |
| 0 | `NodeWiggle` | LateUpdate |
| 1 | `PlayerArmController` | LateUpdate |

---

## Player Hierarchy
```
Player  (PlayerSkeletonRoot, PlayerSkeletonNode, Rigidbody2D [gravityScale=0])
├── TorsoVisual  (SpriteRenderer, NodeWiggle)
├── Arm          (SpriteRenderer, PlayerArmController — orbits TorsoVisual)
├── LeftFootNode  (PlayerSkeletonNode — fixed offset from torso)
│   └── FootVisual  (SpriteRenderer, CircleCollider2D, Rigidbody2D [Dynamic], PlayerFootWiggle)
└── RightFootNode (PlayerSkeletonNode)
    └── FootVisual
```

**Key behaviors:**
- Torso Y = `lowestFootVisual.y + standHeight`, enforced via velocity override each FixedUpdate
- Foot nodes: isosceles triangle below torso (`±footSpreadX`, `footOffsetY`)
- Ground contact: when `node.y < visual.y`, `SnapTo(visual.y)` — node rises to meet visual
- Arm orbit center = `TorsoVisual.position` (post-wiggle), not the node position

---

## Non-Script Setup Still Required
- [ ] Physics layer **"Player"** — assign foot visuals; disable Player↔Player self-collision
- [ ] `PlayerAssembler` GameObject in scene with `defaultSprite` assigned (circle, 1 world-unit diameter)
- [ ] `PlayerConfig` asset configured (sprites, `moveForce`, `standHeight`, foot offsets)
- [ ] Arm sprite must point **RIGHT** at 0° (barrel faces +X; script rotates to mouse)
- [ ] Ground needs a `Collider2D` for feet to land on