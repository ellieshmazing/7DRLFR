# BallWorld — Variables Reference

Living documentation of all meaningful variables across the project. Updated whenever variables are added, renamed, or removed.

---

## PlayerConfig (ScriptableObject)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| `moveForce` | `float` | `PlayerConfig` | Horizontal force applied per frame while WASD is held | Applied as `ForceMode2D.Force` each FixedUpdate; scales with frame rate via physics timestep | Torso horizontal acceleration; interacts with `linearDamping` for top speed feel |
| `standHeight` | `float` | `PlayerConfig` | Target vertical distance from lowest foot visual to torso | Enforced each FixedUpdate via velocity override on torso Y | Controls how "tall" the player stands above its feet; affects visual silhouette |
| `footSpreadX` | `float` | `PlayerConfig` | Half-distance between left and right foot nodes along X | Feet placed at `±footSpreadX` from hip node X each frame | Sets stance width |
| `footOffsetY` | `float` | `PlayerConfig` | Vertical offset of foot nodes below the torso node | Applied as a downward bias when positioning foot nodes | Controls how far feet hang below the body at rest |

---

## PlayerSkeletonRoot

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

## PlayerHipNode

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

## PlayerFeet

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

## PlayerArmController

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

## CentipedeConfig (ScriptableObject)

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet — populate as centipede vars are confirmed)* | | | | | |

---

## NodeWiggle

| Variable | Type | Location | Description | Behavior | Affects |
|---|---|---|---|---|---|
| *(none yet)* | | | | | |

---

*Add new sections per-script as variables are introduced.*
