using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space overlay that displays the current tuning state.
/// Creates its own Canvas + UI elements programmatically in Awake (no prefab).
/// Reads from a TuningManager reference each LateUpdate and renders:
///   - Dimension name and phase badge
///   - Variable name, value, and progress indicator (sweep bar or A/B indicator)
///   - Step-response waveform for spring dimensions
///   - Test scenario text and control hints
/// Hidden when Phase == Idle.
/// </summary>
public class TuningOverlay : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Tooltip("Reference to the TuningManager driving the tuning session.")]
    public TuningManager tuningManager;

    // ── Constants ──────────────────────────────────────────────────
    const float DEFAULT_WIDTH      = 320f;
    const float MARGIN             = 10f;
    const float PADDING            = 8f;
    const int   TITLE_FONT_SIZE    = 16;
    const int   BODY_FONT_SIZE     = 13;
    const int   HINT_FONT_SIZE     = 12;
    const int   WAVEFORM_W         = 128;
    const int   WAVEFORM_H         = 64;
    const int   STEP_SAMPLES       = 128;
    const float WAVEFORM_Y_MAX     = 1.3f;
    const int   PROGRESS_BAR_CHARS = 20;

    // ── UI Hierarchy ───────────────────────────────────────────────
    Canvas        canvas;
    CanvasScaler  scaler;
    GameObject    panelGO;
    Image         panelImage;
    RectTransform panelRect;

    // Text elements (top to bottom)
    Text       titleText;
    Text       phaseBadgeText;
    Text       variableLinesText;
    GameObject waveformRowGO;
    RawImage   waveformImage;
    Text       waveformStatsText;
    Text       testScenarioText;
    Text       profileText;
    Text       controlHintsText;

    // ── Waveform ───────────────────────────────────────────────────
    Texture2D waveformTex;
    Color32[] clearPixels;
    readonly Color32 bgColor      = new Color32(20, 20, 20, 200);
    readonly Color32 equilibColor = new Color32(80, 80, 80, 255);
    readonly Color32 curveColor   = new Color32(0, 230, 50, 255);

    // ── Cache ──────────────────────────────────────────────────────
    float   cachedFrequency = -1f;
    float   cachedDamping   = -1f;
    float[] cachedSamples;

    // ================================================================
    //  LIFECYCLE
    // ================================================================

    void Awake()
    {
        BuildUI();
        panelGO.SetActive(false);
    }

    void LateUpdate()
    {
        if (tuningManager == null)                       { Hide(); return; }
        if (tuningManager.Phase == TuningManager.TuningPhase.Idle)     { Hide(); return; }

        Show();
        Refresh();
    }

    void OnDestroy()
    {
        if (waveformTex != null) Destroy(waveformTex);
    }

    // ================================================================
    //  UI CONSTRUCTION (all programmatic, no prefabs)
    // ================================================================

    void BuildUI()
    {
        // ── Canvas ─────────────────────────────────────────────────
        var canvasGO = new GameObject("TuningOverlayCanvas");
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel ──────────────────────────────────────────────────
        panelGO   = CreateChild(canvasGO, "Panel");
        panelRect = panelGO.GetComponent<RectTransform>();
        AnchorTopRight(panelRect);

        panelImage       = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, GetOpacity());

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset((int)PADDING, (int)PADDING, (int)PADDING, (int)PADDING);
        vlg.spacing                = 4f;
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // ── Title row ──────────────────────────────────────────────
        var titleRow = CreateChild(panelGO, "TitleRow");
        var hlg = titleRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;

        titleText      = CreateText(titleRow, "TitleText",      TITLE_FONT_SIZE, Color.white, FontStyle.Bold);
        phaseBadgeText = CreateText(titleRow, "PhaseBadgeText", TITLE_FONT_SIZE, new Color(1f, 0.85f, 0.3f), FontStyle.Bold);

        // Title fills remaining space; badge is content-sized
        var titleLE = titleText.gameObject.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1f;

        // ── Variable lines ─────────────────────────────────────────
        variableLinesText = CreateText(panelGO, "VariableLines", BODY_FONT_SIZE, Color.white, FontStyle.Normal);

        // ── Waveform row ───────────────────────────────────────────
        waveformRowGO = CreateChild(panelGO, "WaveformRow");
        var waveHlg = waveformRowGO.AddComponent<HorizontalLayoutGroup>();
        waveHlg.spacing                = 6f;
        waveHlg.childAlignment         = TextAnchor.MiddleLeft;
        waveHlg.childForceExpandWidth  = false;
        waveHlg.childForceExpandHeight = false;
        waveHlg.childControlWidth      = true;
        waveHlg.childControlHeight     = true;

        // Texture
        waveformTex            = new Texture2D(WAVEFORM_W, WAVEFORM_H, TextureFormat.RGBA32, false);
        waveformTex.filterMode = FilterMode.Point;
        waveformTex.wrapMode   = TextureWrapMode.Clamp;

        clearPixels = new Color32[WAVEFORM_W * WAVEFORM_H];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = bgColor;

        var imgGO     = CreateChild(waveformRowGO, "WaveformImage");
        waveformImage = imgGO.AddComponent<RawImage>();
        waveformImage.texture = waveformTex;
        var imgLE = imgGO.AddComponent<LayoutElement>();
        imgLE.preferredWidth  = WAVEFORM_W;
        imgLE.preferredHeight = WAVEFORM_H;

        waveformStatsText = CreateText(waveformRowGO, "WaveformStats", BODY_FONT_SIZE, Color.white, FontStyle.Normal);
        var statsLE = waveformStatsText.gameObject.AddComponent<LayoutElement>();
        statsLE.flexibleWidth = 1f;

        // ── Test scenario ──────────────────────────────────────────
        testScenarioText = CreateText(panelGO, "TestScenario", BODY_FONT_SIZE, new Color(0.7f, 0.85f, 1f), FontStyle.Italic);

        // ── Profile line ───────────────────────────────────────────
        profileText = CreateText(panelGO, "ProfileLine", BODY_FONT_SIZE, Color.gray, FontStyle.Normal);

        // ── Control hints ──────────────────────────────────────────
        controlHintsText = CreateText(panelGO, "ControlHints", HINT_FONT_SIZE, Color.gray, FontStyle.Normal);
    }

    // ================================================================
    //  REFRESH (called every LateUpdate when active)
    // ================================================================

    void Refresh()
    {
        TuningDimensionDef dim = tuningManager.CurrentDimension;
        TuningManager.TuningPhase phase      = tuningManager.Phase;

        if (dim == null) { Hide(); return; }

        // ── Panel sizing ───────────────────────────────────────────
        float width = GetFloatField("overlayWidth", DEFAULT_WIDTH);
        panelRect.sizeDelta = new Vector2(width, panelRect.sizeDelta.y);

        // ── Opacity ────────────────────────────────────────────────
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, GetOpacity());

        // ── Title ──────────────────────────────────────────────────
        titleText.text      = $"TUNING: {dim.dimensionName}";
        phaseBadgeText.text = $"[{phase.ToString().ToUpper()}]";

        // ── Variable lines ─────────────────────────────────────────
        variableLinesText.text = BuildVariableLines(dim, phase);

        // ── Waveform ───────────────────────────────────────────────
        bool isSpring = IsSpringDimension(dim);
        waveformRowGO.SetActive(isSpring);

        if (isSpring)
        {
            ReadSpringParams(dim, out float freq, out float zeta);
            UpdateWaveform(freq, zeta);
            waveformStatsText.text = $"\u03c9 = {freq:F2}\n\u03b6 = {zeta:F2}";
        }

        // ── Test scenario ──────────────────────────────────────────
        testScenarioText.text = string.IsNullOrEmpty(dim.testScenario)
            ? ""
            : $"Test: {dim.testScenario}";

        // ── Profile ────────────────────────────────────────────────
        profileText.text = "Profile: Default";

        // ── Control hints ──────────────────────────────────────────
        controlHintsText.text = BuildControlHints(phase);
    }

    // ================================================================
    //  VARIABLE LINES
    // ================================================================

    string BuildVariableLines(TuningDimensionDef dim, TuningManager.TuningPhase phase)
    {
        var sb         = new System.Text.StringBuilder(256);
        int currentIdx = tuningManager.VariableIndex;

        for (int i = 0; i < dim.variables.Length; i++)
        {
            TuningVariable v = dim.variables[i];

            if (i == currentIdx)
            {
                // Active variable: value + progress indicator
                float val = tuningManager.CurrentValue;
                sb.Append($"<color=white>{v.fieldName}: {val:F2}  ");

                if (phase == TuningManager.TuningPhase.Sweep)
                {
                    sb.Append(BuildSweepBar(v, tuningManager.SweepNormalized));
                    // Show logged count and speed multiplier
                    int logCount = tuningManager.LoggedValues != null
                        ? tuningManager.LoggedValues.Count : 0;
                    float speed  = tuningManager.SweepSpeedMultiplier;
                    sb.Append($"\n  logged: {logCount}  speed: {speed:F1}x");
                }
                else if (phase == TuningManager.TuningPhase.AB)
                {
                    sb.Append(BuildABIndicator());
                }

                sb.Append("</color>");
            }
            else if (i < currentIdx)
            {
                float doneVal = ReadFieldValue(v);
                sb.Append($"<color=#888888>{v.fieldName}: {doneVal:F2}  (done)</color>");
            }
            else
            {
                sb.Append($"<color=#666666>{v.fieldName}: \u2014\u2014  (next)</color>");
            }

            if (i < dim.variables.Length - 1)
                sb.Append('\n');
        }
        return sb.ToString();
    }

    string BuildSweepBar(TuningVariable v, float normalized)
    {
        // [min ═══■════ max]
        int filled = Mathf.Clamp(Mathf.RoundToInt(normalized * PROGRESS_BAR_CHARS), 0, PROGRESS_BAR_CHARS);

        var sb = new System.Text.StringBuilder(PROGRESS_BAR_CHARS + 16);
        sb.Append('[');
        sb.Append(v.min.ToString("F0"));
        sb.Append(' ');

        for (int i = 0; i < PROGRESS_BAR_CHARS; i++)
            sb.Append(i == filled ? '\u25a0' : '\u2550'); // ■ or ═

        sb.Append(' ');
        sb.Append(v.max.ToString("F0"));
        sb.Append(']');
        return sb.ToString();
    }

    string BuildABIndicator()
    {
        bool  showA = tuningManager.ShowingA;
        float timer = tuningManager.ABSwapTimer;
        float lo    = tuningManager.ABLo;
        float hi    = tuningManager.ABHi;

        // [A ■ · · B]  or  [A · · ■ B]
        string indicator = showA
            ? "[A \u25a0 \u00b7 \u00b7 B]"   // ■ near A
            : "[A \u00b7 \u00b7 \u25a0 B]";   // ■ near B

        string label = showA ? "showing A" : "showing B";

        return $"{indicator}  {timer:F1}s\n  range [{lo:F2}\u2013{hi:F2}]  \u25b2 {label}";
    }

    // ================================================================
    //  SPRING DETECTION & READING
    // ================================================================

    bool IsSpringDimension(TuningDimensionDef dim)
    {
        for (int i = 0; i < dim.variables.Length; i++)
        {
            string fn = dim.variables[i].fieldName;
            if (fn.Contains("Frequency") || fn.Contains("DampingRatio"))
                return true;
        }
        return false;
    }

    void ReadSpringParams(TuningDimensionDef dim, out float frequency, out float dampingRatio)
    {
        frequency    = 10f;
        dampingRatio = 0.5f;

        for (int i = 0; i < dim.variables.Length; i++)
        {
            TuningVariable v = dim.variables[i];
            if (v.fieldName.Contains("Frequency"))
                frequency = ReadFieldValue(v);
            else if (v.fieldName.Contains("DampingRatio"))
                dampingRatio = ReadFieldValue(v);
        }
    }

    float ReadFieldValue(TuningVariable v)
    {
        if (v.targetConfig == null) return 0f;

        FieldInfo field = v.targetConfig.GetType().GetField(v.fieldName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return 0f;

        object val = field.GetValue(v.targetConfig);
        if (val is float f) return f;
        if (val is int n)   return n;
        return 0f;
    }

    // ================================================================
    //  WAVEFORM
    // ================================================================

    void UpdateWaveform(float frequency, float dampingRatio)
    {
        // Skip redundant redraws
        if (Mathf.Approximately(frequency, cachedFrequency) &&
            Mathf.Approximately(dampingRatio, cachedDamping) &&
            cachedSamples != null)
            return;

        cachedFrequency = frequency;
        cachedDamping   = dampingRatio;

        // Guard degenerate values
        if (frequency    < 0.01f) frequency    = 0.01f;
        if (dampingRatio < 0f)    dampingRatio = 0f;

        cachedSamples = SampleStepResponse(frequency, dampingRatio, STEP_SAMPLES);
        RenderWaveformTexture(cachedSamples);
    }

    void RenderWaveformTexture(float[] samples)
    {
        // Clear to background
        waveformTex.SetPixels32(clearPixels);

        // Equilibrium line at y = 1.0
        int eqRow = Mathf.Clamp(
            Mathf.RoundToInt((1f / WAVEFORM_Y_MAX) * (WAVEFORM_H - 1)),
            0, WAVEFORM_H - 1);

        for (int x = 0; x < WAVEFORM_W; x++)
            waveformTex.SetPixel(x, eqRow, equilibColor);

        // Draw curve with gap-filling between adjacent columns
        int count = samples.Length;
        int prevY = -1;

        for (int x = 0; x < WAVEFORM_W; x++)
        {
            // Interpolate sample at this pixel column
            float sIdx = (float)x / (WAVEFORM_W - 1) * (count - 1);
            int   i0   = Mathf.FloorToInt(sIdx);
            int   i1   = Mathf.Min(i0 + 1, count - 1);
            float val  = Mathf.Lerp(samples[i0], samples[i1], sIdx - i0);

            int y = Mathf.Clamp(
                Mathf.RoundToInt((val / WAVEFORM_Y_MAX) * (WAVEFORM_H - 1)),
                0, WAVEFORM_H - 1);

            waveformTex.SetPixel(x, y, curveColor);

            // Fill vertical gap from previous column
            if (prevY >= 0 && prevY != y)
            {
                int lo = Mathf.Min(prevY, y);
                int hi = Mathf.Max(prevY, y);
                for (int fy = lo; fy <= hi; fy++)
                    waveformTex.SetPixel(x, fy, curveColor);
            }
            prevY = y;
        }

        waveformTex.Apply();
    }

    // ================================================================
    //  STEP-RESPONSE SAMPLING
    // ================================================================

    /// <summary>
    /// Computes an analytical step-response curve for a second-order spring system.
    /// Returns y-values where 0 = rest and 1 = equilibrium (may exceed 1 for
    /// underdamped overshoot).
    /// </summary>
    public static float[] SampleStepResponse(float frequency, float dampingRatio, int sampleCount)
    {
        // Time span: enough to show oscillation / settling
        float T;
        if (dampingRatio < 0.1f)
            T = 8f * Mathf.PI / frequency;
        else
            T = 4f / (dampingRatio * frequency);

        float dt      = T / sampleCount;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i * dt;

            if (Mathf.Abs(dampingRatio - 1f) < 0.001f)
            {
                // Critically damped
                samples[i] = 1f - (1f + frequency * t) * Mathf.Exp(-frequency * t);
            }
            else if (dampingRatio < 1f)
            {
                // Underdamped
                float wd  = frequency * Mathf.Sqrt(1f - dampingRatio * dampingRatio);
                float phi = Mathf.Acos(dampingRatio);
                samples[i] = 1f
                    - (Mathf.Exp(-dampingRatio * frequency * t)
                       / Mathf.Sqrt(1f - dampingRatio * dampingRatio))
                      * Mathf.Sin(wd * t + phi);
            }
            else
            {
                // Overdamped
                float disc  = Mathf.Sqrt(dampingRatio * dampingRatio - 1f);
                float s1    = -frequency * (dampingRatio + disc);
                float s2    = -frequency * (dampingRatio - disc);
                float denom = s2 - s1;
                if (Mathf.Abs(denom) < 0.0001f)
                    samples[i] = 1f;
                else
                    samples[i] = 1f - (s2 * Mathf.Exp(s1 * t) - s1 * Mathf.Exp(s2 * t)) / denom;
            }
        }
        return samples;
    }

    // ================================================================
    //  CONTROL HINTS
    // ================================================================

    string BuildControlHints(TuningManager.TuningPhase phase)
    {
        switch (phase)
        {
            case TuningManager.TuningPhase.Sweep:
                return "[Tab]Next  [F5]Log  [Enter]Lock  [Shift]Skip  [[ / ]]Speed";
            case TuningManager.TuningPhase.AB:
                return $"[1]Choose A  [2]Choose B  [Tab]Next  Rnd {tuningManager.ABRoundCount}";
            case TuningManager.TuningPhase.CrossValidation:
                return "[1]Keep Base  [2]Adopt Variant  [Tab]Skip";
            case TuningManager.TuningPhase.Complete:
                return "Tuning complete.  [F9]Save Profile  [`]Exit";
            default:
                return "";
        }
    }

    // ================================================================
    //  SHOW / HIDE
    // ================================================================

    void Show()
    {
        if (!panelGO.activeSelf) panelGO.SetActive(true);
    }

    void Hide()
    {
        if (panelGO != null && panelGO.activeSelf) panelGO.SetActive(false);
    }

    // ================================================================
    //  HELPERS
    // ================================================================

    float GetOpacity()
    {
        return GetFloatField("overlayOpacity", 0.85f);
    }

    /// <summary>
    /// Reads a float field from tuningManager by name via reflection.
    /// Searches both public and serialized-private fields.
    /// Returns the fallback if the manager is null or the field is missing.
    /// </summary>
    float GetFloatField(string fieldName, float fallback)
    {
        if (tuningManager == null) return fallback;

        FieldInfo fi = tuningManager.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null) return fallback;

        object val = fi.GetValue(tuningManager);
        if (val is float f) return f;
        return fallback;
    }

    void AnchorTopRight(RectTransform rt)
    {
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-MARGIN, -MARGIN);
        rt.sizeDelta        = new Vector2(DEFAULT_WIDTH, 0f); // height from ContentSizeFitter
    }

    GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    Text CreateText(GameObject parent, string name, int fontSize, Color color, FontStyle style)
    {
        var go   = CreateChild(parent, name);
        var text = go.AddComponent<Text>();

        // Unity 6 built-in font
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

        text.fontSize           = fontSize;
        text.fontStyle          = style;
        text.color              = color;
        text.alignment          = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow   = VerticalWrapMode.Truncate;
        text.supportRichText    = true;
        text.raycastTarget      = false;
        return text;
    }
}
