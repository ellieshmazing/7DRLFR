# Randomized Centipede Spawning

## Goal

Each centipede spawned by GameLoop should have randomized nodeCount, nodeRadius (with proportional followDistance), speed, and sprite variant — creating visual and gameplay variety where no two centipedes look or behave identically. Speed is inversely coupled with both size and node count so that large, many-segmented centipedes feel lumbering while small, short ones feel zippy. Obstacle detection radius scales with nodeRadius to keep navigation proportional. Sprite variants are drawn from BallGamersheet indices 1–5, assigned uniformly across all nodes of a single centipede. The base CentipedeConfig SO is never mutated; a runtime clone is created per spawn with randomized fields.

## Architecture Overview

```
GameLoop.SpawnCentipede():
  1. Roll random values: nodeCount, nodeRadius, speed, spriteIndex
  2. Derive coupled values: followDistance = nodeRadius * FOLLOW_RATIO
  3. Compute inverse-coupled speed: speed biased by normalized size+count
  4. Scale obstacleDetectionRadius proportionally with nodeRadius
  5. Clone centipedeConfig via ScriptableObject.Instantiate()
  6. Write randomized values into the clone
  7. Pick sprite from serialized Sprite[] array by random index
  8. Call centipedeAssembler.Spawn(clone, position, overrideSprite)

CentipedeAssembler.Spawn(config, position, overrideSprite):
  ... existing assembly logic ...
  After each Ball.Init(), if overrideSprite != null:
    ball.SetSprite(overrideSprite)
```

## Behaviors

### Proportional Size Randomization

**Concept:** Affine parameter coupling — randomize nodeRadius, derive followDistance from a fixed ratio to preserve chain density at all scales.

**Role:** Ensures centipedes look visually coherent (no overlapping or gappy chains) regardless of randomized size.

**Logic:**
```
nodeRadius = Random(MIN_RADIUS, MAX_RADIUS)
followDistance = nodeRadius * FOLLOW_RATIO
```

### Inverse Size-Speed Coupling

**Concept:** Weighted interpolation across two normalized dimensions (size and count) to bias speed inversely with overall "bulk."

**Role:** Creates coherent centipede personalities — small/short = fast, large/many = slow — rather than chaotic random combinations.

**Logic:**
```
sizeT  = InverseLerp(MIN_RADIUS, MAX_RADIUS, nodeRadius)       // 0 = smallest, 1 = largest
countT = InverseLerp(MIN_NODES, MAX_NODES, nodeCount)           // 0 = fewest, 1 = most
bulkT  = (sizeT + countT) / 2                                   // combined bulk factor
speed  = Lerp(MAX_SPEED, MIN_SPEED, bulkT)                      // invert: bulk 0 → fast, bulk 1 → slow
```

### Obstacle Detection Scaling

**Concept:** Proportional scaling of a spatial parameter to maintain consistent behavior across size variants.

**Role:** Prevents small centipedes from steering around walls too early, and large ones from clipping into walls.

**Logic:**
```
scaleFactor = nodeRadius / DEFAULT_NODE_RADIUS
clone.obstacleDetectionRadius = config.obstacleDetectionRadius * scaleFactor
```

### Sprite Variant Selection

**Concept:** Index-based sampling from a serialized sprite sub-range, applied as a post-Init cosmetic override.

**Role:** Gives each centipede a distinct color/look. All nodes in one centipede share the same sprite for visual unity.

**Logic:**
```
spriteIndex = Random(0, spriteVariants.Length)   // 0-indexed into the 5-element array
overrideSprite = spriteVariants[spriteIndex]
// Applied per-ball after Ball.Init() in CentipedeAssembler
```

## Function Designs

### `GameLoop.SpawnCentipede() → void`
Rolls randomized parameters, creates a runtime CentipedeConfig clone, and delegates to CentipedeAssembler.

**Side effects:** Instantiates a runtime SO clone (not destroyed — acceptable leak for spawn-frequency objects). Spawns a centipede hierarchy.

```
cam = Camera.main
if cam == null: return

compute spawnX, spawnY from camera bounds (existing logic)

nodeCount  = RandomInt(MIN_NODES, MAX_NODES + 1)
nodeRadius = RandomFloat(MIN_RADIUS, MAX_RADIUS)
followDistance = nodeRadius * FOLLOW_RATIO

sizeT  = InverseLerp(MIN_RADIUS, MAX_RADIUS, nodeRadius)
countT = InverseLerp(MIN_NODES, MAX_NODES, nodeCount)
bulkT  = (sizeT + countT) / 2
speed  = Lerp(MAX_SPEED, MIN_SPEED, bulkT)

clone = ScriptableObject.Instantiate(centipedeConfig)
clone.nodeCount = nodeCount
clone.nodeRadius = nodeRadius
clone.followDistance = followDistance
clone.speed = speed
clone.obstacleDetectionRadius = centipedeConfig.obstacleDetectionRadius * (nodeRadius / centipedeConfig.nodeRadius)

sprite = centipedeVariantSprites[Random(0, centipedeVariantSprites.Length)]

centipedeAssembler.Spawn(clone, position, sprite)
```

### `CentipedeAssembler.Spawn(config, position, overrideSprite) → GameObject`
New overload (or optional parameter) that passes a sprite override through to SetupNodeBall.

**Parameters:**
- `config`: CentipedeConfig (may be a runtime clone with randomized values)
- `position`: World-space spawn position
- `overrideSprite`: Sprite to use instead of the BallDefinition's sprite. Null = use default.

**Side effects:** Spawns full centipede hierarchy. Overrides ball sprites if overrideSprite is non-null.

```
// Existing Spawn logic unchanged, except:
// After each ball = SetupNodeBall(node, config):
if overrideSprite != null:
    ball.SetSprite(overrideSprite)
```

### `Ball.SetSprite(sprite) → void`
Replaces the ball's visual sprite and recomputes scale to maintain the correct world-space diameter.

**Parameters:**
- `sprite`: Replacement Sprite. Must have same cell dimensions as the original for consistent sizing, or scale is recomputed.

**Side effects:** Mutates `sr.sprite` and `transform.localScale`.

```
sr.sprite = sprite
spriteWorldDiam = SpriteWorldDiameter(sprite)
// Recover current diameter from existing scale: diameter = localScale.x * oldSpriteWorldDiam
// But simpler: store diameter in Init, reuse it
transform.localScale = Vector3.one * (diameter / spriteWorldDiam)
circ.radius = spriteWorldDiam * 0.5
```

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| MIN_NODES | int | 3 | Minimum node count per centipede. Lower = more runty centipedes appear. try 2–5 |
| MAX_NODES | int | 10 | Maximum node count per centipede. Higher = longer worms. try 8–15 |
| MIN_RADIUS | float | 0.08 | Smallest nodeRadius in world units. Lower = tinier segments. try 0.05–0.12 |
| MAX_RADIUS | float | 0.25 | Largest nodeRadius in world units. Higher = chunkier segments. try 0.2–0.4 |
| FOLLOW_RATIO | float | 2.0 | followDistance / nodeRadius ratio. Higher = gappier chain, lower = overlapping. try 1.5–2.5 |
| MIN_SPEED | float | 1.5 | Speed for the bulkiest centipede (large + many nodes). try 1.0–2.5 |
| MAX_SPEED | float | 5.0 | Speed for the smallest centipede (small + few nodes). try 4.0–6.0; hard cap at 5 per requirement |
| centipedeVariantSprites | Sprite[] | — | Inspector-assigned array of 5 sprites (BallGamersheet_1 through BallGamersheet_5). One is picked at random per centipede. |

## Implementation Notes

- **SO clone lifecycle:** Runtime clones are not destroyed. CentipedeController.Initialize already stores a config reference; the clone is small (~1KB) and centipede spawn rate is bounded, so accumulation is negligible for a play session. If this ever becomes a concern, CentipedeController.OnDestroy could destroy the clone.
- **Ball.diameter storage:** Ball.Init does not currently store the diameter as a field. SetSprite needs it to recompute scale. Either add a `float diameter` field to Ball set during Init, or recover it from `transform.localScale.x * SpriteWorldDiameter(currentSprite)`. Storing it is cleaner.
- **Sprite PPU consistency:** All BallGamersheet sub-sprites are 16×16 at the same PPU, so SetSprite's scale recomputation will produce identical results. The recomputation is still correct to include as a safety net.
- **Spawn overload vs optional parameter:** Use a default parameter `Sprite overrideSprite = null` on the existing Spawn method rather than a true overload. This avoids duplicating the method body and keeps the test-spawn code path working (passes null).
- **Random.Range for int is exclusive on max:** `Random.Range(MIN_NODES, MAX_NODES + 1)` to include MAX_NODES.
- **No config field additions:** The randomization constants (MIN_NODES, MAX_SPEED, etc.) live as serialized fields on GameLoop under a new header, not on CentipedeConfig. The config SO remains the "base template" with its authored defaults.
