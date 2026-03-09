using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Permanent bottom-left hint overlay built entirely in code (no prefab).
/// Displays yellow Comic Sans text on a black panel with a font-size slider.
/// Add to any persistent scene GameObject to activate.
/// </summary>
public class HintOverlay : MonoBehaviour
{
    // ── Constants ──────────────────────────────────────────────────
    const float  MARGIN           = 12f;
    const float  PADDING          = 10f;
    const int    DEFAULT_FONT_SIZE = 20;
    const float  MIN_FONT_SIZE    = 8f;
    const float  MAX_FONT_SIZE    = 64f;
    const string HINT_TEXT        = "r to restart.\nbacktab (`) for some overkill dev tools. have fun :)";

    // ── Inspector ──────────────────────────────────────────────────
    [Range(8, 64)]
    public int fontSize = DEFAULT_FONT_SIZE;

    // ── UI refs ────────────────────────────────────────────────────
    Text   hintText;
    Slider sizeSlider;
    int    lastFontSize;

    // ================================================================
    //  LIFECYCLE
    // ================================================================

    void Awake()
    {
        lastFontSize = fontSize;
        BuildUI();
    }

    void Update()
    {
        // Let the Inspector slider drive live changes in-editor.
        if (fontSize == lastFontSize) return;
        lastFontSize = fontSize;
        if (hintText    != null) hintText.fontSize = fontSize;
        if (sizeSlider  != null) sizeSlider.value  = fontSize;
    }

    // ================================================================
    //  UI CONSTRUCTION
    // ================================================================

    void BuildUI()
    {
        // ── Canvas ─────────────────────────────────────────────────
        var canvasGO = new GameObject("HintOverlayCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel — black, bottom-left ──────────────────────────────
        var panelGO   = CreateChild(canvasGO, "Panel");
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin        = Vector2.zero;
        panelRect.anchorMax        = Vector2.zero;
        panelRect.pivot            = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(MARGIN, MARGIN);

        panelGO.AddComponent<Image>().color = Color.black;

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset((int)PADDING, (int)PADDING, (int)PADDING, (int)PADDING);
        vlg.spacing                = 6f;
        vlg.childAlignment         = TextAnchor.LowerLeft;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // ── Font (Comic Sans, fallback Arial) ──────────────────────
        Font font = Font.CreateDynamicFontFromOSFont("Comic Sans MS", 32);
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 32);

        // ── Hint text ───────────────────────────────────────────────
        var textGO  = CreateChild(panelGO, "HintText");
        hintText    = textGO.AddComponent<Text>();
        hintText.font               = font;
        hintText.fontSize           = fontSize;
        hintText.color              = Color.yellow;
        hintText.text               = HINT_TEXT;
        hintText.alignment          = TextAnchor.LowerLeft;
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hintText.verticalOverflow   = VerticalWrapMode.Overflow;
        hintText.supportRichText    = false;
        hintText.raycastTarget      = false;

        // ── Slider row ──────────────────────────────────────────────
        var sliderRow   = CreateChild(panelGO, "SliderRow");
        var sliderRowLE = sliderRow.AddComponent<LayoutElement>();
        sliderRowLE.preferredHeight = 20f;

        var rowHlg = sliderRow.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing                = 6f;
        rowHlg.childAlignment         = TextAnchor.MiddleLeft;
        rowHlg.childForceExpandWidth  = false;
        rowHlg.childForceExpandHeight = false;
        rowHlg.childControlWidth      = true;
        rowHlg.childControlHeight     = true;

        // Label
        var labelGO = CreateChild(sliderRow, "SizeLabel");
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 38f;

        var label   = labelGO.AddComponent<Text>();
        label.font               = font;
        label.fontSize           = 12;
        label.color              = new Color(0.65f, 0.65f, 0.65f, 1f);
        label.text               = "size:";
        label.alignment          = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow   = VerticalWrapMode.Overflow;
        label.raycastTarget      = false;

        // Slider
        sizeSlider = BuildSlider(sliderRow);
    }

    Slider BuildSlider(GameObject parent)
    {
        var go = CreateChild(parent, "Slider");
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 120f;
        le.preferredHeight = 20f;

        go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);

        // Fill area
        var fillArea = CreateChild(go, "Fill Area");
        var far      = fillArea.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.25f);
        far.anchorMax = new Vector2(1f, 0.75f);
        far.offsetMin = new Vector2(5f, 0f);
        far.offsetMax = new Vector2(-15f, 0f);

        var fill    = CreateChild(fillArea, "Fill");
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.85f, 0f, 1f);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        // Handle slide area
        var handleArea = CreateChild(go, "Handle Slide Area");
        var har        = handleArea.GetComponent<RectTransform>();
        har.anchorMin = Vector2.zero;
        har.anchorMax = Vector2.one;
        har.offsetMin = new Vector2(10f, 0f);
        har.offsetMax = new Vector2(-10f, 0f);

        var handle    = CreateChild(handleArea, "Handle");
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRect  = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);

        var slider          = go.AddComponent<Slider>();
        slider.fillRect     = fillRect;
        slider.handleRect   = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction    = Slider.Direction.LeftToRight;
        slider.minValue     = MIN_FONT_SIZE;
        slider.maxValue     = MAX_FONT_SIZE;
        slider.value        = DEFAULT_FONT_SIZE;
        slider.wholeNumbers = true;

        slider.onValueChanged.AddListener(OnSliderChanged);
        return slider;
    }

    void OnSliderChanged(float val)
    {
        fontSize = (int)val;
        lastFontSize = fontSize;
        if (hintText != null) hintText.fontSize = fontSize;
    }

    // ================================================================
    //  HELPERS
    // ================================================================

    GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }
}
