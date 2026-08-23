using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The colour palette popover: theme chips that set a coordinated pair, and
/// two swatch rows ("Panels", "Veneers &amp; caps") for adjusting each slot
/// individually. Opened from the Palette card in the Parts panel (build
/// mode) and the Palette button in the Space panel — same popover, same
/// global selection (<see cref="FinishStyle"/>).
///
/// Built at runtime in the house style: it adopts the PartsPanel's card
/// sprite and font, docks flush beside the left panel, and registers its
/// surfaces with the theme controller so dark mode restyles it like every
/// other panel.
/// </summary>
public class FinishPaletteUI : MonoBehaviour
{
    const float PanelWidth = 340f;
    const float PanelHeight = 408f;
    const float Margin = 18f;

    static FinishPaletteUI _instance;

    Canvas _canvas;
    UIThemeController _theme;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    RectTransform _panel;
    readonly System.Collections.Generic.Dictionary<string, Image> _themeChips =
        new System.Collections.Generic.Dictionary<string, Image>();
    readonly System.Collections.Generic.Dictionary<string, Image> _panelRings =
        new System.Collections.Generic.Dictionary<string, Image>();
    readonly System.Collections.Generic.Dictionary<string, Image> _dressingRings =
        new System.Collections.Generic.Dictionary<string, Image>();

    Color Ink => _theme != null ? Palette.ink : new Color(0.149f, 0.133f, 0.118f);
    Color Surface => _theme != null ? Palette.surface : new Color(0.953f, 0.937f, 0.914f);
    Color CardColor => _theme != null ? Palette.card : Color.white;
    Color Muted => _theme != null ? Palette.muted : new Color(0.561f, 0.533f, 0.502f);
    Color Accent => _theme != null ? Palette.accent : new Color(0.851f, 0.424f, 0.278f);
    UIThemeController.Palette Palette => _theme.IsDark ? _theme.dark : _theme.light;

    // ------------------------------------------------------------------
    // Entry
    // ------------------------------------------------------------------

    public static void Toggle()
    {
        if (_instance == null)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;
            var go = new GameObject("FinishPaletteUI");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<FinishPaletteUI>();
            _instance.Build(canvas);
            _instance._panel.gameObject.SetActive(true);
            _instance.PositionPanel();
            return;
        }
        bool show = !_instance._panel.gameObject.activeSelf;
        _instance._panel.gameObject.SetActive(show);
        if (show)
            _instance.PositionPanel();
    }

    public static void Hide()
    {
        if (_instance != null && _instance._panel != null)
            _instance._panel.gameObject.SetActive(false);
    }

    public static bool IsOpen =>
        _instance != null && _instance._panel != null && _instance._panel.gameObject.activeSelf;

    void OnEnable() => FinishStyle.Changed += RefreshSelection;

    void OnDisable() => FinishStyle.Changed -= RefreshSelection;

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    void Build(Canvas canvas)
    {
        _canvas = canvas;
        _theme = FindFirstObjectByType<UIThemeController>();

        Transform partsPanel = _canvas.transform.Find("PartsPanel");
        if (partsPanel != null)
        {
            var pImg = partsPanel.GetComponent<Image>();
            if (pImg != null && pImg.sprite != null)
            {
                _cardSprite = pImg.sprite;
                _cardPpu = pImg.pixelsPerUnitMultiplier;
            }
            var text = partsPanel.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null)
                _font = text.font;
        }

        var go = new GameObject("PalettePanel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        _panel = (RectTransform)go.transform;
        _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        PositionPanel();

        var img = go.GetComponent<Image>();
        img.color = CardColor;
        StyleCard(img, 1.2f);
        if (_theme != null)
            _theme.cardImages.Add(img);

        UiPolish.SoftShadow(_panel);

        TextMeshProUGUI title = CreateText(_panel, "Title", "Colour palette", 18f, Ink, true);
        PlaceTop(title.rectTransform, 20f, -20f, 220f, 24f);
        RegisterInk(title);

        BuildCloseButton();

        TextMeshProUGUI hint = CreateText(_panel, "Hint",
            "Panels carry the colour, veneers outline it.", 11.5f, Muted, false);
        PlaceTop(hint.rectTransform, 20f, -48f, PanelWidth - 40f, 16f);
        RegisterMuted(hint);

        BuildThemeChips();
        BuildSwatchRow("Panels", -264f, panels: true);
        BuildSwatchRow("Veneers & caps", -336f, panels: false);

        RefreshSelection();
    }

    /// <summary>
    /// Dock the popover beside the left panel with a small gutter, BOTTOM
    /// edges aligned — right next to the Palette buttons that open it (the
    /// bottom row of the Space panel, the bottom cards of the Parts panel).
    /// Recomputed on every open so it tracks the panel of the current mode.
    /// </summary>
    void PositionPanel()
    {
        float panelRight = 360f;  // left panel: x 24 + width 336
        float panelBottom = 24f;  // left panel's bottom margin

        if (_canvas != null && _canvas.transform.Find("PartsPanel") is RectTransform partsRt)
        {
            panelRight = partsRt.offsetMax.x;
            panelBottom = partsRt.offsetMin.y;
        }

        _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 0f);
        _panel.pivot = new Vector2(0f, 0f);
        _panel.anchoredPosition = new Vector2(panelRight + 12f, panelBottom);
    }

    void BuildCloseButton()
    {
        var go = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-14f, -14f);
        rt.sizeDelta = new Vector2(28f, 28f);

        var bg = go.GetComponent<Image>();
        bg.color = Surface;
        StyleCard(bg, 1.4f);
        if (_theme != null)
            _theme.surfaceImages.Add(bg);

        // "×" not "✕": the dingbat is missing from the UI font.
        TextMeshProUGUI label = CreateText(rt, "Label", "×", 16f, Muted, false);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Midline;
        RegisterMuted(label);

        var button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        button.onClick.AddListener(Hide);
    }

    void BuildThemeChips()
    {
        CreateSectionLabel("ThemesLabel", "THEMES", -78f);

        const float chipWidth = 148f;
        const float chipHeight = 44f;

        for (int i = 0; i < FinishStyle.Themes.Length; i++)
        {
            FinishStyle.Theme theme = FinishStyle.Themes[i];
            float x = Margin + (i % 2) * (chipWidth + 8f);
            float y = -98f - (i / 2) * (chipHeight + 8f);

            var chipGo = new GameObject("Theme_" + theme.Id,
                typeof(RectTransform), typeof(Image), typeof(Button));
            chipGo.transform.SetParent(_panel, false);
            var rt = (RectTransform)chipGo.transform;
            PlaceTop(rt, x, y, chipWidth, chipHeight);

            var bg = chipGo.GetComponent<Image>();
            bg.color = Surface;
            StyleCard(bg, 1.8f);
            _themeChips[theme.Id] = bg;

            // Two colour dots preview the pair: panel field + dressing dot.
            AddDot(rt, "PanelDot", new Vector2(12f, -13f), 18f,
                FinishStyle.Themes[i].PanelSwatch);
            AddDot(rt, "DressDot", new Vector2(24f, -19f), 12f,
                FinishStyle.Themes[i].DressingSwatch);

            TextMeshProUGUI name = CreateText(rt, "Name", theme.Label, 12f, Ink, true);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.offsetMin = new Vector2(46f, 0f);
            nameRt.offsetMax = new Vector2(-8f, 0f);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            RegisterInk(name);

            var button = chipGo.GetComponent<Button>();
            UiPolish.HoverTint(button);

            FinishStyle.Theme captured = theme;
            button.onClick.AddListener(() =>
            {
                FinishStyle.SetTheme(captured);
                SelectionStatus.Set($"Theme applied: {captured.Label}", 3f);
            });
        }
    }

    void AddDot(RectTransform parent, string name, Vector2 pos, float size, string swatchId)
    {
        var dotGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(parent, false);
        var rt = (RectTransform)dotGo.transform;
        PlaceTop(rt, pos.x, pos.y, size, size);
        var img = dotGo.GetComponent<Image>();
        img.color = SwatchColor(swatchId);
        img.raycastTarget = false;
        StyleCard(img, 6f);
        var outline = dotGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.10f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    void BuildSwatchRow(string caption, float y, bool panels)
    {
        CreateSectionLabel(caption + "Label", caption.ToUpperInvariant(), y);

        // Slot = accent selection ring (behind) + the colour swatch on top.
        const float slotSize = 34f;
        const float swatchInset = 4f;
        float rowY = y - 20f;

        int count = 0;
        foreach (FinishStyle.Swatch s in FinishStyle.Swatches)
            if (panels || !s.PanelOnly)
                count++;

        // Justify the row: first swatch on the left margin, last on the right.
        float step = count > 1 ? (PanelWidth - 2f * Margin - slotSize) / (count - 1) : 0f;

        float x = Margin;
        foreach (FinishStyle.Swatch swatch in FinishStyle.Swatches)
        {
            if (!panels && swatch.PanelOnly)
                continue;

            var slotGo = new GameObject((panels ? "Panel_" : "Dress_") + swatch.Id,
                typeof(RectTransform), typeof(Button));
            slotGo.transform.SetParent(_panel, false);
            var slotRt = (RectTransform)slotGo.transform;
            PlaceTop(slotRt, x, rowY, slotSize, slotSize);
            x += step;

            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(slotRt, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero;
            ringRt.offsetMax = Vector2.zero;
            var ring = ringGo.GetComponent<Image>();
            ring.color = Accent;
            ring.raycastTarget = false;
            StyleCard(ring, 2.2f);
            ring.enabled = false;
            if (_theme != null)
                _theme.accentImages.Add(ring);
            (panels ? _panelRings : _dressingRings)[swatch.Id] = ring;

            var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatchGo.transform.SetParent(slotRt, false);
            var swatchRt = (RectTransform)swatchGo.transform;
            swatchRt.anchorMin = Vector2.zero;
            swatchRt.anchorMax = Vector2.one;
            swatchRt.offsetMin = new Vector2(swatchInset, swatchInset);
            swatchRt.offsetMax = new Vector2(-swatchInset, -swatchInset);
            var img = swatchGo.GetComponent<Image>();
            img.color = swatch.Color;
            StyleCard(img, 4f);
            var outline = swatchGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.10f);
            outline.effectDistance = new Vector2(1f, -1f);

            var button = slotGo.GetComponent<Button>();
            button.targetGraphic = img;
            UiPolish.HoverTint(button);

            string captured = swatch.Id;
            string capturedLabel = swatch.Label;
            button.onClick.AddListener(() =>
            {
                if (panels)
                {
                    FinishStyle.SetPanelSwatch(captured);
                    SelectionStatus.Set($"Panels: {capturedLabel}.", 2.5f);
                }
                else
                {
                    FinishStyle.SetDressingSwatch(captured);
                    SelectionStatus.Set($"Veneers & caps: {capturedLabel}.", 2.5f);
                }
            });
        }
    }

    void CreateSectionLabel(string name, string caption, float y)
    {
        TextMeshProUGUI label = CreateText(_panel, name, caption, 10.5f, Muted, true);
        PlaceTop(label.rectTransform, 20f, y, 220f, 14f);
        label.characterSpacing = 6f;
        RegisterMuted(label);
    }

    // ------------------------------------------------------------------
    // Selection highlight
    // ------------------------------------------------------------------

    void RefreshSelection()
    {
        bool hasTheme = FinishStyle.TryCurrentTheme(out FinishStyle.Theme current);

        foreach (var pair in _themeChips)
        {
            if (pair.Value == null)
                continue;
            bool active = hasTheme && pair.Key == current.Id;
            pair.Value.color = active
                ? new Color(Accent.r, Accent.g, Accent.b, 0.22f)
                : Surface;
        }

        HighlightRow(_panelRings, FinishStyle.PanelSwatchId);
        HighlightRow(_dressingRings, FinishStyle.DressingSwatchId);
    }

    static void HighlightRow(System.Collections.Generic.Dictionary<string, Image> row, string selectedId)
    {
        foreach (var pair in row)
        {
            if (pair.Value == null)
                continue;
            pair.Value.enabled = pair.Key == selectedId;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static Color SwatchColor(string id)
    {
        foreach (FinishStyle.Swatch s in FinishStyle.Swatches)
            if (s.Id == id)
                return s.Color;
        return Color.white;
    }

    void RegisterInk(TextMeshProUGUI text)
    {
        if (_theme != null)
            _theme.inkTexts.Add(text);
    }

    void RegisterMuted(TextMeshProUGUI text)
    {
        if (_theme != null)
            _theme.mutedTexts.Add(text);
    }

    void StyleCard(Image img, float ppu)
    {
        if (_cardSprite == null)
            return;
        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpu * ppu;
    }

    TextMeshProUGUI CreateText(RectTransform parent, string name, string value,
        float size, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        if (_font != null)
            tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        if (bold)
            tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    static void PlaceTop(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
