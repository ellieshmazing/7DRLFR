# ScentField Debug Visualizer

---

## Goal

`ScentFieldDebugVisualizer` is a standalone MonoBehaviour that renders an in-play overlay making the scent field and centipede internal state directly legible. It has four modes cycled by a keypress: **Off**, **SamplesOnly**, **Full** (samples + per-centipede state), and **FullWithGrid** (Full + continuous field heat map). All rendering uses `GL` in `OnRenderObject()` — no Gizmos, no editor dependency, visible in the Game view at runtime. The component is designed to be deleted before shipping; it adds zero cost when absent from the scene.

It interacts with two existing systems: `ScentField` (singleton) and any `ScentFieldNavigator` instances in the scene. Both require small public read-only additions (listed in Implementation Notes). The visualizer itself owns no simulation state.

---

## Architecture Overview

```
Every Update():
  If toggleKey pressed: mode = (mode + 1) % 4

Every FixedUpdate():
  For each ScentFieldNavigator nav in scene:
    Append nav.transform.position to momentumHistories[nav]   // world position ring buffer

Every OnRenderObject():
  If mode == Off: return
  EnsureGLMaterial()

  GL.PushMatrix()
  GL.LoadIdentity()
  GL.MultMatrix(Camera.main.worldToCameraMatrix)       // world-space GL drawing
  lineMaterial.SetPass(0)

  If mode >= SamplesOnly:
    DrawSamples()

  If mode >= Full:
    RefreshNavigatorList()
    For each nav in navigators:
      DrawMomentumTrail(nav)
      DrawGradientAndMomentumArrows(nav)
      DrawSensitivityRing(nav)
      DrawConsumeRadius(nav)
      DrawFallbackIndicator(nav)

  If mode == FullWithGrid:
    DrawGridHeatmap()

  GL.PopMatrix()
```

---

## Behaviors

### Behavior 1: Per-Sample Dot Markers

**Concept:** Map each ring-buffer entry to a filled disk whose size encodes effective field contribution and whose color encodes consumed vs. fresh weight.

**Role:** Shows exactly what `ScentField.Evaluate()` sums over — the raw ring buffer, not the blended result. Consumed samples (partially erased by centipedes) appear in a distinct hue.

**Logic:**
```
DrawSamples():
  t = Time.time
  n = ScentField.Instance.GetDebugSamples(sampleReadBuf, t)

  GL.Begin(GL.TRIANGLES)
  for i in 0..n-1:
    s = sampleReadBuf[i]
    age = t - s.timestamp
    temporal = exp(-age / ScentField.Instance.DecayTime)
    effective = s.weight * temporal
    if effective < 0.005: continue

    // Hue: raw weight encodes consumed vs. fresh (independent of age)
    hue   = Lerp(consumedSampleColor, freshSampleColor, s.weight)
    hue.a = Clamp01(effective * 2.0)      // fade below 0.5 effective weight

    radius = sampleMarkerMaxRadius * Clamp01(effective)
    DrawDisk_Buffered(s.position, radius, hue)   // emits GL.TRIANGLES into current Begin block
  GL.End()
```

### Behavior 2: Gradient and Momentum Arrows

**Concept:** Render the navigator's current gradient direction and momentum vector as two differently-colored arrows from the head.

**Role:** The angular gap between the two arrows shows the steering blend in action — it closes during high-sensitivity phases and widens during ballistic coasting.

**Logic:**
```
DrawGradientAndMomentumArrows(nav):
  pos = (Vector2)nav.transform.position

  // Gradient — where the nose says to go
  if nav.GradientDirection.sqrMagnitude > 0.001:
    DrawArrow(pos, pos + nav.GradientDirection * gradientArrowLength, gradientArrowColor)

  // Momentum — where the body is actually headed
  DrawArrow(pos, pos + nav.Momentum * momentumArrowLength, momentumArrowColor)
```

### Behavior 3: Sensitivity Phase Ring

**Concept:** Map the oscillator's current [0,1] sensitivity value to a ring whose radius and color change each frame.

**Role:** Makes the sweep-and-lock rhythm visible as a pulsing ring. With multiple centipedes, their rings pulse out-of-phase, revealing the per-centipede phase randomization.

**Logic:**
```
DrawSensitivityRing(nav):
  s      = nav.Sensitivity                                   // [0, 1]
  radius = Lerp(sensitivityRingMinRadius, sensitivityRingMaxRadius, s)
  color  = Lerp(sensitivityCoolColor, sensitivityHotColor, s)
  DrawCircle((Vector2)nav.transform.position, radius, color, segments: 24)
```

### Behavior 4: Consume Radius Indicator

**Concept:** Draw a wire circle at the consume radius so the erasure footprint is directly visible.

**Role:** On a slow-moving centipede the circle barely moves between frames; on a fast one it sweeps a continuous band through the sample markers — illustrating how speed affects spiral tightness.

**Logic:**
```
DrawConsumeRadius(nav):
  alpha = Clamp01(nav.Config.scentConsumeRate / 4.0)   // normalize to visible range
  color = consumeRadiusColor
  color.a *= alpha
  DrawCircle((Vector2)nav.transform.position, nav.Config.scentConsumeRadius, color, segments: 16)
```

### Behavior 5: Fallback State Indicator

**Concept:** When `IsInFallback` is true, render a crosshair at the head and a line to the player.

**Role:** Distinguishes "gradient-following" from "direct pursuit" — the two modes look identical in motion without this marker.

**Logic:**
```
DrawFallbackIndicator(nav):
  if not nav.IsInFallback: return
  pos = (Vector2)nav.transform.position

  DrawX(pos, fallbackMarkerSize, fallbackColor)

  if nav.Target != null:
    lineColor = fallbackColor
    lineColor.a = 0.35
    DrawLine(pos, (Vector2)nav.Target.position, lineColor)
```

### Behavior 6: Mode Toggle

**Concept:** Cycle through four named states with a single key. Off is state 0, so the visualizer costs nothing when not needed.

**Role:** Prevents the overlay from cluttering normal play while keeping all layers one keypress away.

**Logic:**
```
// Enum: Off=0, SamplesOnly=1, Full=2, FullWithGrid=3

Update():
  if Keyboard.current[toggleKey].wasPressedThisFrame:
    mode = (DebugMode)(((int)mode + 1) % 4)
```

Use `UnityEngine.InputSystem.Key` for `toggleKey` (serialized field, default `Key.F1`).

### Behavior 7: Momentum History Ghost Trail

**Concept:** Store a fixed-capacity ring buffer of recent world positions per navigator. Render as a fading polyline behind the head.

**Role:** Makes the heading-blend temporal behavior legible — you can watch the trail gradually curve toward the gradient during snap phases and straighten during ballistic phases.

**Logic:**
```
// State: Dictionary<ScentFieldNavigator, CircularBuffer<Vector2>> momentumHistories

FixedUpdate():
  For each nav in navigators:
    if not momentumHistories.ContainsKey(nav):
      momentumHistories[nav] = new CircularBuffer<Vector2>(momentumHistoryCapacity)
    momentumHistories[nav].Push((Vector2)nav.transform.position)

DrawMomentumTrail(nav):
  if not momentumHistories.ContainsKey(nav): return
  buf = momentumHistories[nav]
  n   = buf.Count
  if n < 2: return

  GL.Begin(GL.LINES)
  for i in 1..n-1:
    t0 = (float)(i - 1) / (n - 1)   // 0=oldest, 1=newest
    t1 = (float)i       / (n - 1)

    // Quadratic fade: old segments nearly invisible
    c0 = momentumTrailColor; c0.a *= t0 * t0
    c1 = momentumTrailColor; c1.a *= t1 * t1

    GL.Color(c0); GL.Vertex(buf[i - 1])
    GL.Color(c1); GL.Vertex(buf[i])
  GL.End()
```

`CircularBuffer<T>` can be a simple fixed-array wrapper with a `Push()` that overwrites oldest, indexed `[0] = oldest … [Count-1] = newest`.

### Behavior 8: Grid Heat Map

**Concept:** Evaluate `ScentField.Evaluate()` on a uniform world-space grid centered on the camera, color each cell by field intensity.

**Role:** Shows the continuous blended shape of the field — Gaussian overlap between nearby samples, sigma influence radius, and how decay has hollowed out old regions.

**Logic:**
```
DrawGridHeatmap():
  camPos = (Vector2)Camera.main.transform.position
  step   = gridCellSize
  half   = gridExtent

  GL.Begin(GL.QUADS)
  for x = camPos.x - half to camPos.x + half step step:
    for y = camPos.y - half to camPos.y + half step step:
      center = (x + step * 0.5, y + step * 0.5)
      val    = ScentField.Instance.Evaluate(center)
      heat   = Clamp01(val / gridMaxFieldStrength)
      if heat < 0.01: continue        // skip empty cells entirely

      color   = Lerp(gridColdColor, gridHotColor, heat)
      color.a = heat * gridMaxAlpha
      GL.Color(color)
      GL.Vertex3(x,        y,        0)
      GL.Vertex3(x + step, y,        0)
      GL.Vertex3(x + step, y + step, 0)
      GL.Vertex3(x,        y + step, 0)
  GL.End()
```

Cost: `(2 * gridExtent / gridCellSize)²` evaluations per frame, each O(scentHistorySize). At extent=10, step=0.5: 1600 evaluations × 200 iterations = 320,000 operations. Acceptable but not free — keep FullWithGrid a deliberate toggle.

---

## Function Designs

### `EnsureGLMaterial() → void`
Creates the unlit alpha-blended `Material` used for all GL draws. Idempotent — skips creation if material already exists.

**Side effects:** Allocates a `Material` asset on first call (retained for lifetime of component).

```
EnsureGLMaterial():
  if lineMaterial != null: return
  shader = Shader.Find("Hidden/Internal-Colored")
  lineMaterial = new Material(shader)
  lineMaterial.hideFlags = HideFlags.HideAndDontSave
  lineMaterial.SetInt("_SrcBlend", BlendMode.SrcAlpha)
  lineMaterial.SetInt("_DstBlend", BlendMode.OneMinusSrcAlpha)
  lineMaterial.SetInt("_Cull",     CullMode.Off)
  lineMaterial.SetInt("_ZWrite",   0)
  lineMaterial.SetInt("_ZTest",    CompareFunction.Always)  // draw on top of everything
```

`Hidden/Internal-Colored` is a Unity built-in shader. `_ZTest = Always` prevents geometry from clipping the overlay.

---

### `RefreshNavigatorList() → void`
Rebuilds the cached list of `ScentFieldNavigator` instances from the scene. Cheap enough to call every frame; `FindObjectsByType` with no sort is O(n) over all MonoBehaviours.

**Side effects:** Updates `navigators` list. Removes entries whose key navigator has been destroyed from `momentumHistories`.

```
RefreshNavigatorList():
  navigators = FindObjectsByType<ScentFieldNavigator>(FindObjectsSortMode.None)
  // Prune destroyed keys from history dict
  for each key in momentumHistories.Keys:
    if key == null: momentumHistories.Remove(key)
```

---

### `DrawDisk_Buffered(center: Vector2, radius: float, color: Color) → void`
Emits a filled disk as a triangle fan into an already-open `GL.Begin(GL.TRIANGLES)` block.

**Parameters:**
- `center` — World-space center of the disk.
- `radius` — World-space radius.
- `color` — RGBA color including alpha.

**Side effects:** Emits GL vertices into the current GL draw call. Must be called between `GL.Begin(GL.TRIANGLES)` and `GL.End()`.

```
DrawDisk_Buffered(center, radius, color):
  GL.Color(color)
  segments = 12     // hardcoded; disks are small, 12 is sufficient
  for i in 0..segments-1:
    a0 = i       * 2π / segments
    a1 = (i + 1) * 2π / segments
    GL.Vertex(center)
    GL.Vertex(center + (cos(a0), sin(a0)) * radius)
    GL.Vertex(center + (cos(a1), sin(a1)) * radius)
```

---

### `DrawArrow(from: Vector2, to: Vector2, color: Color) → void`
Draws a line with a two-segment arrowhead at `to`. Opens and closes its own GL call.

**Parameters:**
- `from`, `to` — World-space endpoints.
- `color` — Arrow color.

```
DrawArrow(from, to, color):
  dir    = normalize(to - from)
  perp   = (-dir.y, dir.x)
  tipLen = 0.1   // arrowhead arm length in world units

  GL.Begin(GL.LINES)
  GL.Color(color)
  // Shaft
  GL.Vertex(from); GL.Vertex(to)
  // Arrowhead
  GL.Vertex(to); GL.Vertex(to - dir * tipLen + perp * tipLen * 0.5)
  GL.Vertex(to); GL.Vertex(to - dir * tipLen - perp * tipLen * 0.5)
  GL.End()
```

---

### `DrawCircle(center: Vector2, radius: float, color: Color, segments: int) → void`
Draws a wire circle as a line loop. Opens and closes its own GL call.

```
DrawCircle(center, radius, color, segments):
  GL.Begin(GL.LINES)
  GL.Color(color)
  for i in 0..segments-1:
    a0 = i       * 2π / segments
    a1 = (i + 1) * 2π / segments
    GL.Vertex(center + (cos(a0), sin(a0)) * radius)
    GL.Vertex(center + (cos(a1), sin(a1)) * radius)
  GL.End()
```

---

### `DrawX(center: Vector2, size: float, color: Color) → void`
Draws two crossed diagonal line segments as a fallback marker.

```
DrawX(center, size, color):
  h = size * 0.5
  GL.Begin(GL.LINES)
  GL.Color(color)
  GL.Vertex(center + (-h, -h)); GL.Vertex(center + ( h,  h))
  GL.Vertex(center + ( h, -h)); GL.Vertex(center + (-h,  h))
  GL.End()
```

---

### `ScentField.GetDebugSamples(output: DebugSample[], currentTime: float) → int`
**Addition to `ScentField`.** Fills a caller-provided array with current sample data. Returns count written.

**Parameters:**
- `output` — Pre-allocated array of `ScentField.DebugSample`. Must be at least `historySize` long.
- `currentTime` — `Time.time` from the caller (avoids redundant `Time.time` calls inside the loop).

**Returns:** Number of valid entries written to `output`.

```
// New public struct inside ScentField:
public struct DebugSample {
  public Vector2 position;
  public float   timestamp;
  public float   weight;    // current consumed weight [0, 1]
}

public int GetDebugSamples(DebugSample[] output, float currentTime):
  written = 0
  for i in 0..count-1:
    s = samples[i]
    age      = currentTime - s.timestamp
    temporal = exp(-age / decayTime)
    if s.weight * temporal < 0.005: continue   // invisible; skip
    output[written++] = { s.position, s.timestamp, s.weight }
  return written
```

Also add: `public float DecayTime => decayTime;` (read-only property).

---

### `ScentFieldNavigator` additions (read-only properties)

Four properties expose internal state without changing the navigator's behavior. Update each inside `FixedUpdate()` just before the value is computed or consumed.

```
public Vector2 Momentum          { get; private set; }   // set after normalization step
public Vector2 GradientDirection { get; private set; }   // set after ComputeGradientDirection()
public float   Sensitivity       { get; private set; }   // set after oscillator advance
public bool    IsInFallback      { get; private set; }   // set based on fieldAtHead check
public CentipedeConfig Config    => config;              // trivial getter
```

Update assignments in `FixedUpdate()` at the point each value is computed — not at the end of the method, since the frame order matters for the visualizer reading them in `OnRenderObject()` (which runs after Update, after FixedUpdate in the same frame).

---

## Modifiable Variables

| Variable | Type | Default | Description |
|---|---|---|---|
| `toggleKey` | `Key` | `Key.F1` | InputSystem key that cycles through debug modes. Change if F1 conflicts with other bindings. |
| `sampleMarkerMaxRadius` | float | 0.15 | World-unit radius of a fresh (weight=1), undecayed sample disk. Try 0.05–0.3; smaller = precise, larger = visible from afar. |
| `freshSampleColor` | Color | (1, 1, 0.2, 1) — bright yellow | Color for samples with weight=1.0 (uncon­sumed). |
| `consumedSampleColor` | Color | (0.6, 0.1, 0.9, 1) — purple | Color for samples with weight near 0.0 (fully consumed). Lerped with freshSampleColor by raw weight. |
| `gradientArrowLength` | float | 0.6 | Length of the gradient direction arrow. Try 0.3–1.0. |
| `momentumArrowLength` | float | 0.8 | Length of the momentum arrow. Set slightly longer than gradient to distinguish them. |
| `gradientArrowColor` | Color | (0.2, 1, 0.2, 1) — green | Color of the gradient direction arrow. |
| `momentumArrowColor` | Color | (1, 1, 1, 0.9) — white | Color of the momentum (heading) arrow. |
| `sensitivityRingMinRadius` | float | 0.1 | Ring radius when sensitivity = 0 (coasting). Try 0.05–0.2. |
| `sensitivityRingMaxRadius` | float | 0.4 | Ring radius when sensitivity = 1 (snapping). Try 0.25–0.6. |
| `sensitivityCoolColor` | Color | (0.2, 0.4, 1, 0.5) — blue | Ring color during low-sensitivity (ballistic) phase. |
| `sensitivityHotColor` | Color | (1, 0.3, 0.1, 0.9) — orange-red | Ring color during high-sensitivity (snapping) phase. |
| `consumeRadiusColor` | Color | (1, 0.5, 0.1, 0.8) — amber | Wire circle showing the consume radius. Alpha is additionally scaled by normalized consume rate. |
| `fallbackColor` | Color | (1, 0.1, 0.1, 1) — red | Color of the fallback X marker and player direction line. |
| `fallbackMarkerSize` | float | 0.25 | World-unit arm length of the fallback X. |
| `momentumHistoryCapacity` | int | 40 | Number of past positions stored per navigator. At 50Hz FixedUpdate, 40 = 0.8 seconds of trail. Try 20–80. |
| `momentumTrailColor` | Color | (0.8, 0.8, 1, 1) — pale blue | Base color for the ghost trail; alpha is additionally faded quadratically from newest to oldest. |
| `gridCellSize` | float | 0.5 | World units per grid cell in heat map mode. Smaller = smoother but more expensive. Try 0.25–1.0. |
| `gridExtent` | float | 8.0 | Half-size of the heat map grid in world units (grid covers 2×extent in each axis). Try 5–15. |
| `gridMaxFieldStrength` | float | 5.0 | Field strength that maps to full heat color. Values above this clamp to hot. Match to `scentGradientMaxStrength` in config. |
| `gridMaxAlpha` | float | 0.35 | Maximum alpha of any grid cell. Keep below 0.5 so the grid doesn't obscure gameplay. Try 0.2–0.6. |
| `gridColdColor` | Color | (0.1, 0.1, 0.8, 1) — dark blue | Grid cell color at zero field strength (only visible if gridMaxAlpha is high). |
| `gridHotColor` | Color | (1, 0.2, 0.05, 1) — red | Grid cell color at full field strength. |

---

## Implementation Notes

- **GL matrix setup**: `GL.LoadIdentity()` + `GL.MultMatrix(Camera.main.worldToCameraMatrix)` puts GL in world space. Do this once per `OnRenderObject()` inside `GL.PushMatrix()`. Without it, GL coordinates are in clip space and world-unit sizes mean nothing.

- **`OnRenderObject()` is per-camera**: If you have multiple cameras (e.g. a UI camera), it fires multiple times per frame. Guard with `if (Camera.current != Camera.main) return;` at the top of the method.

- **`Hidden/Internal-Colored` shader availability**: This Unity built-in shader is present in all Unity 6 projects. `Shader.Find()` returns null only if the shader is stripped at build time — this is a debug-only component so stripping is irrelevant, but you can `Assert.IsNotNull(shader)` to catch it during development.

- **FixedUpdate vs. OnRenderObject timing**: `FixedUpdate` runs before `Update` and `OnRenderObject`. The navigator sets its public properties during `FixedUpdate`; by the time `OnRenderObject` reads them the values are current-frame. No ordering problem.

- **`FindObjectsByType` caching**: Call `RefreshNavigatorList()` once per `OnRenderObject()` frame (not once per component lifetime) so newly spawned centipedes are picked up automatically. The cost is negligible.

- **CircularBuffer implementation**: A minimal implementation needs a fixed `T[] buf`, `int head`, `int count`, and indexer `[i]` that maps `i` to `buf[(head - count + i + capacity) % capacity]`. `Push(v)` writes `buf[head]`, advances `head`, increments count up to capacity.

- **Preallocate `sampleReadBuf`**: Allocate `new ScentField.DebugSample[200]` once in `Start()` (or on first use). Never allocate inside `OnRenderObject()` — this runs every frame on the render thread and GC pressure here will cause frame hitches.

- **`_ZTest = Always`**: Setting depth test to Always on the GL material draws the overlay on top of all world geometry, including the centipede body. This is intentional for a debug tool — arrows and rings should never be occluded. Remove this line if you want them to depth-clip into the scene.

- **Cleanup on destroy**: In `OnDestroy()`, call `Destroy(lineMaterial)` to avoid leaking the procedural material asset.
