using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The left panel while Space Mode is on: the piece library as placeable
/// cards. It copies the PartsPanel's exact placement and card styling, so
/// swapping panels on mode switch looks seamless. Clicking a row arms the
/// placement ghost; the armed row is highlighted; clicking it again puts
/// the tool down.
/// </summary>
public class SpacePanelUI : MonoBehaviour
{
    public SpaceInteractionController interaction;

    static readonly Color FallbackInk = new Color(0.149f, 0.133f, 0.118f);
    static readonly Color FallbackSurface = new Color(0.953f, 0.937f, 0.914f);
    static readonly Color FallbackCard = Color.white;
    static readonly Color FallbackMuted = new Color(0.561f, 0.533f, 0.502f);
    static readonly Color FallbackAccent = new Color(0.851f, 0.424f, 0.278f);

    Canvas _canvas;
    UIThemeController _theme;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    RectTransform _panel;
    RectTransform _listContent;
    TextMeshProUGUI _emptyLabel;
    readonly List<Texture2D> _thumbnails = new List<Texture2D>();
    readonly Dictionary<string, Image> _rowBackgrounds = new Dictionary<string, Image>();

    Color Ink => _theme != null ? Palette.ink : FallbackInk;
    Color Surface => _theme != null ? Palette.surface : FallbackSurface;
    Color CardColor => _theme != null ? Palette.card : FallbackCard;
    Color Muted => _theme != null ? Palette.muted : FallbackMuted;
    Color Accent => _theme != null ? Palette.accent : FallbackAccent;
    UIThemeController.Palette Palette => _theme.IsDark ? _theme.dark : _theme.light;

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null)
            return;

        _theme = FindFirstObjectByType<UIThemeController>();
        AdoptCardStyle();
        BuildPanel();

        if (interaction != null)
            interaction.ArmedChanged += HighlightArmedRow;
    }

    void OnDestroy()
    {
        if (interaction != null)
            interaction.ArmedChanged -= HighlightArmedRow;
    }

    public void Show()
    {
        if (_panel == null)
            return;
        _panel.gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    void BuildPanel()
    {
        var go = new GameObject("SpacePanel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _panel = (RectTransform)go.transform;

        // Sit exactly where the PartsPanel sits.
        Transform partsPanel = _canvas.transform.Find("PartsPanel");
        if (partsPanel is RectTransform partsRt)
        {
            _panel.anchorMin = partsRt.anchorMin;
            _panel.anchorMax = partsRt.anchorMax;
            _panel.pivot = partsRt.pivot;
            _panel.anchoredPosition = partsRt.anchoredPosition;
            _panel.sizeDelta = partsRt.sizeDelta;
        }
        else
        {
            _panel.anchorMin = new Vector2(0f, 0.5f);
            _panel.anchorMax = new Vector2(0f, 0.5f);
            _panel.pivot = new Vector2(0f, 0.5f);
            _panel.anchoredPosition = new Vector2(24f, 0f);
            _panel.sizeDelta = new Vector2(316f, 720f);
        }

        var img = go.GetComponent<Image>();
        img.color = CardColor;
        StyleCard(img, 1.2f);
        if (_theme != null)
            _theme.cardImages.Add(img);

        UiPolish.SoftShadow(_panel);

        TextMeshProUGUI title = CreateText(_panel, "Title", "My Pieces", 18f, Ink, true);
        PlaceTop(title.rectTransform, 20f, -18f, 220f, 26f);
        if (_theme != null)
            _theme.inkTexts.Add(title);

        TextMeshProUGUI hint = CreateText(_panel, "Hint",
            "Click a piece, then click the floor to place it. Drag placed pieces to move them.",
            11.5f, Muted, false);
        PlaceTop(hint.rectTransform, 20f, -48f, _panel.sizeDelta.x - 40f, 34f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        if (_theme != null)
            _theme.mutedTexts.Add(hint);

        BuildList();
        BuildCopyCodeButton();

        _emptyLabel = CreateText(_panel, "Empty",
            "No pieces yet.\nSwitch to Piece Mode, build something,\nand save it as a piece.",
            12.5f, Muted, false);
        var emptyRt = _emptyLabel.rectTransform;
        emptyRt.anchorMin = new Vector2(0f, 0.5f);
        emptyRt.anchorMax = new Vector2(1f, 0.5f);
        emptyRt.pivot = new Vector2(0.5f, 0.5f);
        emptyRt.anchoredPosition = Vector2.zero;
        emptyRt.sizeDelta = new Vector2(-40f, 80f);
        _emptyLabel.alignment = TextAlignmentOptions.Center;
        _emptyLabel.textWrappingMode = TextWrappingModes.Normal;
        if (_theme != null)
            _theme.mutedTexts.Add(_emptyLabel);

        _panel.gameObject.SetActive(false);
    }

    void BuildList()
    {
        var scrollGo = new GameObject("PieceScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(_panel, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(16f, 72f);   // room for "Copy space code"
        scrollRt.offsetMax = new Vector2(-16f, -92f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportGo.transform.SetParent(scrollRt, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = Color.clear;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportRt, false);
        _listContent = (RectTransform)contentGo.transform;
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.sizeDelta = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = _listContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
    }

    /// <summary>
    /// The bottom row of the panel: "Copy code" (the shareable NSS1 code of
    /// the current arrangement) beside "Palette" (the colour popover — one
    /// palette governs the whole space, so it lives here too).
    /// </summary>
    void BuildCopyCodeButton()
    {
        float half = (_panel.sizeDelta.x - 40f) * 0.5f;

        Button copy = MakeBottomButton("Btn_CopySpaceCode", "Copy code", 16f, half);
        copy.onClick.AddListener(() =>
        {
            var codeUi = FindFirstObjectByType<ConfigurationCodeUI>();
            if (codeUi != null)
                codeUi.CopySpaceCode();
        });

        Button palette = MakeBottomButton("Btn_Palette", "Palette", 24f + half, half);
        palette.onClick.AddListener(FinishPaletteUI.Toggle);
    }

    Button MakeBottomButton(string name, string text, float x, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_panel, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, 16f);
        rt.sizeDelta = new Vector2(width, 40f);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        StyleCard(img, 1.6f);
        if (_theme != null)
            _theme.surfaceImages.Add(img);

        TextMeshProUGUI label = CreateText(rt, "Text", text, 12.5f, Ink, true);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        if (_theme != null)
            _theme.inkTexts.Add(label);

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        return button;
    }

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    public void Refresh()
    {
        foreach (Texture2D tex in _thumbnails)
            if (tex != null)
                Destroy(tex);
        _thumbnails.Clear();
        _rowBackgrounds.Clear();

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        List<PieceLibrary.PieceRecord> records = PieceLibrary.LoadAll();
        if (_emptyLabel != null)
            _emptyLabel.gameObject.SetActive(records.Count == 0);

        foreach (PieceLibrary.PieceRecord record in records)
            BuildRow(record);

        HighlightArmedRow();
    }

    void BuildRow(PieceLibrary.PieceRecord record)
    {
        var go = new GameObject("Piece_" + record.id,
            typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_listContent, false);

        var bg = go.GetComponent<Image>();
        bg.color = Surface;
        StyleCard(bg, 1.6f);
        _rowBackgrounds[record.id] = bg;

        var element = go.AddComponent<LayoutElement>();
        element.preferredHeight = 74f;

        var rt = (RectTransform)go.transform;

        var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
        thumbGo.transform.SetParent(rt, false);
        var thumbRt = (RectTransform)thumbGo.transform;
        thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(0f, 0.5f);
        thumbRt.pivot = new Vector2(0f, 0.5f);
        thumbRt.anchoredPosition = new Vector2(10f, 0f);
        thumbRt.sizeDelta = new Vector2(84f, 56f);

        var raw = thumbGo.GetComponent<RawImage>();
        raw.raycastTarget = false;
        Texture2D thumb = record.hasThumbnail ? PieceLibrary.LoadThumbnail(record.id) : null;
        if (thumb != null)
        {
            raw.texture = thumb;
            _thumbnails.Add(thumb);
        }
        else
        {
            raw.color = new Color(Ink.r, Ink.g, Ink.b, 0.08f);
        }

        TextMeshProUGUI name = CreateText(rt, "Name", record.name, 13.5f, Ink, true);
        var nameRt = name.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.anchoredPosition = new Vector2(104f, -14f);
        nameRt.sizeDelta = new Vector2(-114f, 20f);
        name.overflowMode = TextOverflowModes.Ellipsis;

        string meta = $"{record.beamCount + record.panelCount} parts";
        if (record.price > 0f)
            meta += $" · ${record.price:N0}";
        TextMeshProUGUI metaText = CreateText(rt, "Meta", meta, 10.5f, Muted, false);
        var metaRt = metaText.rectTransform;
        metaRt.anchorMin = new Vector2(0f, 1f);
        metaRt.anchorMax = new Vector2(1f, 1f);
        metaRt.pivot = new Vector2(0f, 1f);
        metaRt.anchoredPosition = new Vector2(104f, -38f);
        metaRt.sizeDelta = new Vector2(-114f, 16f);
        metaText.overflowMode = TextOverflowModes.Ellipsis;

        Button rowButton = go.GetComponent<Button>();
        UiPolish.HoverTint(rowButton);
        rowButton.onClick.AddListener(() =>
        {
            if (interaction == null)
                return;
            if (interaction.ArmedPieceId == record.id)
                interaction.DisarmPlacement();
            else
                interaction.ArmPlacement(record);
        });
    }

    void HighlightArmedRow()
    {
        string armedId = interaction != null ? interaction.ArmedPieceId : null;
        foreach (KeyValuePair<string, Image> pair in _rowBackgrounds)
        {
            if (pair.Value == null)
                continue;
            pair.Value.color = pair.Key == armedId
                ? new Color(Accent.r, Accent.g, Accent.b, 0.25f)
                : Surface;
        }
    }

    // ------------------------------------------------------------------
    // Style helpers
    // ------------------------------------------------------------------

    void AdoptCardStyle()
    {
        Transform partsPanel = _canvas.transform.Find("PartsPanel");
        if (partsPanel == null)
            return;

        var img = partsPanel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpu = img.pixelsPerUnitMultiplier;
        }

        var text = partsPanel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _font = text.font;
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
