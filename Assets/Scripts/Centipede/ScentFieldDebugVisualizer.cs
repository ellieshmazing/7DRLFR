using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// In-play GL overlay that makes the ScentField and ScentFieldNavigator internals visible.
///
/// Press F1 (default) to cycle modes:
///   Off → SamplesOnly → Full → FullWithGrid → Off
///
/// Renders:
///   SamplesOnly — per-sample ring buffer dots (size = effective weight, color = consumed vs. fresh)
///   Full        — above + gradient/momentum arrows, sensitivity ring, consume radius,
///                 fallback indicator, momentum ghost trail per centipede
///   FullWithGrid — Full + continuous field heat map on a world-space grid
///
/// Standalone component. Remove from the scene before shipping.
/// Requires public additions to ScentField and ScentFieldNavigator (see spec).
/// </summary>
[DefaultExecutionOrder(10)]
public class ScentFieldDebugVisualizer : MonoBehaviour
{
    private enum DebugMode { Off, SamplesOnly, Full, FullWithGrid }

    // ── Toggle ────────────────────────────────────────────────────────────────

    [Header("Toggle")]
    [SerializeField] private Key toggleKey = Key.F1;

    // ── Sample Markers ────────────────────────────────────────────────────────

    [Header("Sample Markers")]
    [SerializeField] private float sampleMarkerMaxRadius  = 0.15f;
    [SerializeField] private Color freshSampleColor       = new Color(1f, 1f,  0.2f, 1f);
    [SerializeField] private Color consumedSampleColor    = new Color(0.6f, 0.1f, 0.9f, 1f);

    // ── Navigation Arrows ─────────────────────────────────────────────────────

    [Header("Navigation Arrows")]
    [SerializeField] private float gradientArrowLength = 0.6f;
    [SerializeField] private float momentumArrowLength = 0.8f;
    [SerializeField] private Color gradientArrowColor  = new Color(0.2f, 1f,   0.2f, 1f);
    [SerializeField] private Color momentumArrowColor  = new Color(1f,   1f,   1f,   0.9f);

    // ── Sensitivity Ring ──────────────────────────────────────────────────────

    [Header("Sensitivity Ring")]
    [SerializeField] private float sensitivityRingMinRadius = 0.1f;
    [SerializeField] private float sensitivityRingMaxRadius = 0.4f;
    [SerializeField] private Color sensitivityCoolColor     = new Color(0.2f, 0.4f, 1f,   0.5f);
    [SerializeField] private Color sensitivityHotColor      = new Color(1f,   0.3f, 0.1f, 0.9f);

    // ── Consume Radius ────────────────────────────────────────────────────────

    [Header("Consume Radius")]
    [SerializeField] private Color consumeRadiusColor = new Color(1f, 0.5f, 0.1f, 0.8f);

    // ── Fallback ──────────────────────────────────────────────────────────────

    [Header("Fallback")]
    [SerializeField] private Color fallbackColor      = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float fallbackMarkerSize = 0.25f;

    // ── Momentum Trail ────────────────────────────────────────────────────────

    [Header("Momentum Trail")]
    [SerializeField] private int   momentumHistoryCapacity = 40;
    [SerializeField] private Color momentumTrailColor      = new Color(0.8f, 0.8f, 1f, 1f);

    // ── Grid Heat Map ─────────────────────────────────────────────────────────

    [Header("Grid Heat Map")]
    [SerializeField] private float gridCellSize         = 0.5f;
    [SerializeField] private float gridExtent           = 8f;
    [SerializeField] private float gridMaxFieldStrength = 5f;
    [SerializeField] private float gridMaxAlpha         = 0.35f;
    [SerializeField] private Color gridColdColor        = new Color(0.1f, 0.1f, 0.8f, 1f);
    [SerializeField] private Color gridHotColor         = new Color(1f,   0.2f, 0.05f, 1f);

    // ── Runtime state ─────────────────────────────────────────────────────────

    private DebugMode    mode      = DebugMode.Off;
    private Material     lineMaterial;
    private ScentFieldNavigator[] navigators = System.Array.Empty<ScentFieldNavigator>();

    private readonly Dictionary<ScentFieldNavigator, CircularBuffer<Vector2>> momentumHistories
        = new Dictionary<ScentFieldNavigator, CircularBuffer<Vector2>>();

    private ScentField.DebugSample[] sampleReadBuf; // preallocated; never reallocated at runtime

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        sampleReadBuf = new ScentField.DebugSample[512];
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    private void Update()
    {
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            mode = (DebugMode)(((int)mode + 1) % 4);
    }

    private void FixedUpdate()
    {
        if (mode < DebugMode.Full) return;

        foreach (ScentFieldNavigator nav in navigators)
        {
            if (nav == null) continue;

            if (!momentumHistories.TryGetValue(nav, out CircularBuffer<Vector2> buf))
            {
                buf = new CircularBuffer<Vector2>(momentumHistoryCapacity);
                momentumHistories[nav] = buf;
            }

            buf.Push((Vector2)nav.transform.position);
        }
    }

    // URP does not call OnRenderObject on scene MonoBehaviours — subscribe to the pipeline event instead.
    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != Camera.main)       return;
        if (ScentField.Instance == null) return;
        if (mode == DebugMode.Off)       return;

        EnsureGLMaterial();
        if (lineMaterial == null) return;

        // Unlike OnRenderObject (built-in RP), this callback does not pre-load the
        // projection × view matrix — set both explicitly.
        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.modelview = camera.worldToCameraMatrix;

        DrawSamples();

        if (mode >= DebugMode.Full)
        {
            RefreshNavigatorList();
            foreach (ScentFieldNavigator nav in navigators)
            {
                if (nav == null) continue;
                DrawMomentumTrail(nav);
                DrawGradientAndMomentumArrows(nav);
                DrawSensitivityRing(nav);
                DrawConsumeRadius(nav);
                DrawFallbackIndicator(nav);
            }
        }

        if (mode == DebugMode.FullWithGrid)
            DrawGridHeatmap();

        GL.PopMatrix();
    }

    // ── Behavior: Per-sample markers ──────────────────────────────────────────

    private void DrawSamples()
    {
        float t = Time.time;
        int   n = ScentField.Instance.GetDebugSamples(sampleReadBuf, t);

        GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < n; i++)
        {
            ScentField.DebugSample s = sampleReadBuf[i];

            float age       = t - s.timestamp;
            float temporal  = Mathf.Exp(-age / ScentField.Instance.DecayTime);
            float effective = s.weight * temporal;
            if (effective < 0.005f) continue;

            Color color = Color.Lerp(consumedSampleColor, freshSampleColor, s.weight);
            color.a = Mathf.Clamp01(effective * 2f);

            float radius = sampleMarkerMaxRadius * Mathf.Clamp01(effective);
            DrawDisk_Buffered((Vector2)s.position, radius, color);
        }
        GL.End();
    }

    // ── Behavior: Gradient and momentum arrows ────────────────────────────────

    private void DrawGradientAndMomentumArrows(ScentFieldNavigator nav)
    {
        Vector2 pos = nav.transform.position;

        if (nav.GradientDirection.sqrMagnitude > 0.001f)
            DrawArrow(pos, pos + nav.GradientDirection * gradientArrowLength, gradientArrowColor);

        DrawArrow(pos, pos + nav.Momentum * momentumArrowLength, momentumArrowColor);
    }

    // ── Behavior: Sensitivity phase ring ──────────────────────────────────────

    private void DrawSensitivityRing(ScentFieldNavigator nav)
    {
        float s      = nav.Sensitivity;
        float radius = Mathf.Lerp(sensitivityRingMinRadius, sensitivityRingMaxRadius, s);
        Color color  = Color.Lerp(sensitivityCoolColor, sensitivityHotColor, s);
        DrawCircle((Vector2)nav.transform.position, radius, color, 24);
    }

    // ── Behavior: Consume radius indicator ────────────────────────────────────

    private void DrawConsumeRadius(ScentFieldNavigator nav)
    {
        float alpha = Mathf.Clamp01(nav.Config.scentConsumeRate / 4f);
        Color color = consumeRadiusColor;
        color.a    *= alpha;
        DrawCircle((Vector2)nav.transform.position, nav.Config.scentConsumeRadius, color, 16);
    }

    // ── Behavior: Fallback state indicator ────────────────────────────────────

    private void DrawFallbackIndicator(ScentFieldNavigator nav)
    {
        if (!nav.IsInFallback) return;

        Vector2 pos = nav.transform.position;
        DrawX(pos, fallbackMarkerSize, fallbackColor);

        if (nav.Target != null)
        {
            Color lineColor = fallbackColor;
            lineColor.a = 0.35f;
            DrawLine(pos, (Vector2)nav.Target.position, lineColor);
        }
    }

    // ── Behavior: Momentum history ghost trail ────────────────────────────────

    private void DrawMomentumTrail(ScentFieldNavigator nav)
    {
        if (!momentumHistories.TryGetValue(nav, out CircularBuffer<Vector2> buf)) return;

        int n = buf.Count;
        if (n < 2) return;

        GL.Begin(GL.LINES);
        for (int i = 1; i < n; i++)
        {
            float t0 = (float)(i - 1) / (n - 1);
            float t1 = (float)i       / (n - 1);

            Color c0 = momentumTrailColor; c0.a *= t0 * t0;
            Color c1 = momentumTrailColor; c1.a *= t1 * t1;

            GL.Color(c0); GL.Vertex((Vector3)buf[i - 1]);
            GL.Color(c1); GL.Vertex((Vector3)buf[i]);
        }
        GL.End();
    }

    // ── Behavior: Grid heat map ───────────────────────────────────────────────

    private void DrawGridHeatmap()
    {
        Vector2 camPos = Camera.main.transform.position;
        float   step   = gridCellSize;
        float   half   = gridExtent;

        GL.Begin(GL.QUADS);
        for (float wx = camPos.x - half; wx < camPos.x + half; wx += step)
        {
            for (float wy = camPos.y - half; wy < camPos.y + half; wy += step)
            {
                float val  = ScentField.Instance.Evaluate(new Vector2(wx + step * 0.5f, wy + step * 0.5f));
                float heat = Mathf.Clamp01(val / gridMaxFieldStrength);
                if (heat < 0.01f) continue;

                Color color = Color.Lerp(gridColdColor, gridHotColor, heat);
                color.a = heat * gridMaxAlpha;

                GL.Color(color);
                GL.Vertex3(wx,        wy,        0f);
                GL.Vertex3(wx + step, wy,        0f);
                GL.Vertex3(wx + step, wy + step, 0f);
                GL.Vertex3(wx,        wy + step, 0f);
            }
        }
        GL.End();
    }

    // ── GL helpers ────────────────────────────────────────────────────────────

    private void EnsureGLMaterial()
    {
        if (lineMaterial != null) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) { Debug.LogError("[ScentFieldDebugVisualizer] Shader 'Hidden/Internal-Colored' not found."); return; }
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull",     (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite",   0);
        lineMaterial.SetInt("_ZTest",    (int)CompareFunction.Always);
    }

    private void RefreshNavigatorList()
    {
        navigators = FindObjectsByType<ScentFieldNavigator>(FindObjectsSortMode.None);

        var toRemove = new List<ScentFieldNavigator>();
        foreach (ScentFieldNavigator key in momentumHistories.Keys)
        {
            if (key == null) toRemove.Add(key);
        }
        foreach (ScentFieldNavigator key in toRemove)
            momentumHistories.Remove(key);
    }

    /// <summary>
    /// Emits a 12-segment filled disk into an already-open GL.Begin(GL.TRIANGLES) block.
    /// Must be called between GL.Begin and GL.End.
    /// </summary>
    private static void DrawDisk_Buffered(Vector2 center, float radius, Color color)
    {
        const int segments = 12;
        GL.Color(color);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i       * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            GL.Vertex((Vector3)center);
            GL.Vertex((Vector3)(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius));
            GL.Vertex((Vector3)(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius));
        }
    }

    private static void DrawArrow(Vector2 from, Vector2 to, Color color)
    {
        Vector2     dir    = (to - from).normalized;
        Vector2     perp   = new Vector2(-dir.y, dir.x);
        const float tipLen = 0.1f;

        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex((Vector3)from); GL.Vertex((Vector3)to);
        GL.Vertex((Vector3)to);   GL.Vertex((Vector3)(to - dir * tipLen + perp * (tipLen * 0.5f)));
        GL.Vertex((Vector3)to);   GL.Vertex((Vector3)(to - dir * tipLen - perp * (tipLen * 0.5f)));
        GL.End();
    }

    private static void DrawCircle(Vector2 center, float radius, Color color, int segments)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i       * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            GL.Vertex((Vector3)(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius));
            GL.Vertex((Vector3)(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius));
        }
        GL.End();
    }

    private static void DrawX(Vector2 center, float size, Color color)
    {
        float h = size * 0.5f;
        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex((Vector3)(center + new Vector2(-h, -h)));
        GL.Vertex((Vector3)(center + new Vector2( h,  h)));
        GL.Vertex((Vector3)(center + new Vector2( h, -h)));
        GL.Vertex((Vector3)(center + new Vector2(-h,  h)));
        GL.End();
    }

    private static void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex((Vector3)from);
        GL.Vertex((Vector3)to);
        GL.End();
    }
}
