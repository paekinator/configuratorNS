using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "My Pieces" — save the current build as a named piece and bring pieces
/// back for editing. A piece is a configuration code plus metadata
/// (<see cref="PieceLibrary"/>), never a mesh dump.
///
/// UI: a "Pieces" top-bar button toggles a card panel (styled by adopting
/// the baked panel sprite/font, like the selection popup does) holding a
/// name field + "Save as piece", and a scrolling list of saved pieces —
/// thumbnail, name, metadata, with Open / Overwrite / Delete per row.
/// Overwrite and Delete ask for a second click ("Sure?") before acting.
///
/// Open REPLACES the current build (the shared restore path) and counts as
/// one undo step, so Ctrl/Cmd+Z brings the previous build back.
/// </summary>
public class PieceUI : MonoBehaviour
{
    public BuildController buildController;

    // Style fallbacks (light palette); live values come from UIThemeController.
    static readonly Color FallbackInk = new Color(0.149f, 0.133f, 0.118f);
    static readonly Color FallbackSurface = new Color(0.953f, 0.937f, 0.914f);
    static readonly Color FallbackCard = Color.white;
    static readonly Color FallbackAccent = new Color(0.851f, 0.424f, 0.278f);
    static readonly Color FallbackMuted = new Color(0.561f, 0.533f, 0.502f);
    static readonly Color Danger = new Color(0.749f, 0.290f, 0.251f);   // #BF4A40, matches the theme

    Canvas _canvas;
    UIThemeController _theme;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    RectTransform _panel;
    RectTransform _listContent;
    TMP_InputField _nameInput;
    TextMeshProUGUI _emptyLabel;

    readonly List<Texture2D> _thumbnails = new List<Texture2D>();
    readonly List<Behaviour> _suppressedCameraControls = new List<Behaviour>();
    string _openPieceId;

    Color Ink => _theme != null ? CurrentPalette.ink : FallbackInk;
    Color Surface => _theme != null ? CurrentPalette.surface : FallbackSurface;
    Color CardColor => _theme != null ? CurrentPalette.card : FallbackCard;
    Color Accent => _theme != null ? CurrentPalette.accent : FallbackAccent;
    Color Muted => _theme != null ? CurrentPalette.muted : FallbackMuted;
    UIThemeController.Palette CurrentPalette => _theme.IsDark ? _theme.dark : _theme.light;

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null)
            return;

        _theme = FindFirstObjectByType<UIThemeController>();
        AdoptCardStyle();
        InjectTopBarButton();
        BuildPanel();
    }

    void Update()
    {
        if (_panel != null && _panel.gameObject.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
            _panel.gameObject.SetActive(false);

        SuppressCameraWhileTyping();
    }

    /// <summary>
    /// The camera controllers read raw keys (WASD/QE), so typing a piece name
    /// would fly the camera around. While the name field has focus, disable
    /// whatever camera controllers are active and restore them afterwards.
    /// </summary>
    void SuppressCameraWhileTyping()
    {
        bool typing = _nameInput != null && _nameInput.isFocused;

        if (typing && _suppressedCameraControls.Count == 0)
        {
            Camera cam = buildController != null && buildController.cam != null
                ? buildController.cam
                : Camera.main;
            if (cam == null)
                return;

            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.enabled && mb.GetType().Name.Contains("Camera"))
                {
                    mb.enabled = false;
                    _suppressedCameraControls.Add(mb);
                }
            }
        }
        else if (!typing && _suppressedCameraControls.Count > 0)
        {
            foreach (Behaviour b in _suppressedCameraControls)
                if (b != null)
                    b.enabled = true;
            _suppressedCameraControls.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    void SaveNewPiece()
    {
        ConfigurationModel model = ConfigurationCapture.Capture(buildController);
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
        {
            SelectionStatus.Set("The grid is empty · build something before saving a piece.", 4f);
            return;
        }

        string name = _nameInput != null ? _nameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(name))
            name = $"Piece {PieceLibrary.LoadAll().Count + 1}";

        var record = new PieceLibrary.PieceRecord
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = name,
            createdUtc = PieceLibrary.NowUtc()
        };

        FillFromCurrentBuild(record, model);
        PieceLibrary.Save(record);
        _openPieceId = record.id;

        // The name went into the saved piece; a stale name in the box would
        // silently mislabel the next save.
        if (_nameInput != null)
            _nameInput.text = string.Empty;

        Refresh();
        SelectionStatus.Set($"Saved piece \"{record.name}\".", 4f);
    }

    void OverwritePiece(PieceLibrary.PieceRecord record)
    {
        ConfigurationModel model = ConfigurationCapture.Capture(buildController);
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
        {
            SelectionStatus.Set("The grid is empty · nothing to overwrite the piece with.", 4f);
            return;
        }

        FillFromCurrentBuild(record, model);
        PieceLibrary.Save(record);
        _openPieceId = record.id;

        Refresh();
        SelectionStatus.Set($"Updated \"{record.name}\" to match the current build.", 4f);
    }

    /// <summary>Code + metadata + fresh thumbnail from the live scene.</summary>
    void FillFromCurrentBuild(PieceLibrary.PieceRecord record, ConfigurationModel model)
    {
        record.code = ConfigurationCode.Encode(model);
        record.modifiedUtc = PieceLibrary.NowUtc();
        record.beamCount = model.Beams.Count;
        record.panelCount = model.Panels.Count;

        bool hasBounds = StructureBounds.TryCompute(buildController, out StructureBounds.Info info);
        if (hasBounds)
        {
            record.widthMm = Mathf.RoundToInt(info.WidthMm);
            record.depthMm = Mathf.RoundToInt(info.DepthMm);
            record.heightMm = Mathf.RoundToInt(info.HeightMm);
        }

        var stats = FindFirstObjectByType<UIBuildStats>();
        record.price = stats != null ? stats.TotalPrice : 0f;

        Camera cam = buildController != null && buildController.cam != null
            ? buildController.cam
            : Camera.main;

        // Framed capture: consistent 3/4 view of the structure, independent of
        // where the user's camera happens to point. Plain view as fallback.
        Texture2D thumb = hasBounds
            ? PieceLibrary.CaptureThumbnailFramed(cam, info.WorldBounds)
            : PieceLibrary.CaptureThumbnail(cam);
        if (thumb != null)
        {
            PieceLibrary.SaveThumbnail(record.id, thumb);
            record.hasThumbnail = true;
            Destroy(thumb);
        }
    }

    void OpenPiece(PieceLibrary.PieceRecord record)
    {
        ConfigurationCodeValidation check = ConfigurationCode.Validate(record.code);
        if (!check.IsValid)
        {
            SelectionStatus.Set($"\"{record.name}\" can't be opened: {check.Error}", 7f);
            return;
        }

        var restorer = ConfigurationCode.GetOrCreateRestorer(buildController);
        if (restorer.IsRunning)
        {
            SelectionStatus.Set("Still loading · one moment.", 3f);
            return;
        }

        _openPieceId = record.id;
        if (_nameInput != null)
            _nameInput.text = record.name;

        restorer.Restore(check.Model, report =>
        {
            SelectionStatus.Set(
                $"Opened \"{record.name}\" · {report.Summary} Ctrl+Z (Cmd+Z) restores the previous build.", 7f);

            // Older pieces (or failed captures) may have no thumbnail; the
            // piece is now fully rebuilt in the scene, so this is the perfect
            // moment to backfill one.
            if (!record.hasThumbnail)
                BackfillThumbnail(record);
        });

        _panel.gameObject.SetActive(false);
    }

    void BackfillThumbnail(PieceLibrary.PieceRecord record)
    {
        if (!StructureBounds.TryCompute(buildController, out StructureBounds.Info info))
            return;

        Camera cam = buildController != null && buildController.cam != null
            ? buildController.cam
            : Camera.main;
        Texture2D thumb = PieceLibrary.CaptureThumbnailFramed(cam, info.WorldBounds);
        if (thumb == null)
            return;

        PieceLibrary.SaveThumbnail(record.id, thumb);
        Destroy(thumb);
        record.hasThumbnail = true;
        PieceLibrary.Save(record);
    }

    void DeletePiece(PieceLibrary.PieceRecord record)
    {
        PieceLibrary.Delete(record.id);
        if (_openPieceId == record.id)
            _openPieceId = null;
        Refresh();
        SelectionStatus.Set($"Deleted piece \"{record.name}\".", 4f);
    }

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    void TogglePanel()
    {
        if (_panel == null)
            return;
        bool show = !_panel.gameObject.activeSelf;
        _panel.gameObject.SetActive(show);
        if (show)
        {
            _panel.SetAsLastSibling();
            Refresh();
        }
    }

    void BuildPanel()
    {
        var go = new GameObject("PiecesPanel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _panel = (RectTransform)go.transform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 1f);
        _panel.pivot = new Vector2(1f, 1f);
        _panel.anchoredPosition = new Vector2(-24f, -96f);
        _panel.sizeDelta = new Vector2(372f, 560f);

        var img = go.GetComponent<Image>();
        img.color = CardColor;
        StyleCard(img, 1.2f);
        if (_theme != null)
            _theme.cardImages.Add(img);

        UiPolish.SoftShadow(_panel);

        TextMeshProUGUI title = CreateText(_panel, "Title", "My Pieces", 18f, Ink, true);
        PlaceTop(title.rectTransform, 20f, -18f, 200f, 26f);
        if (_theme != null)
            _theme.inkTexts.Add(title);

        TextMeshProUGUI hint = CreateText(_panel, "Hint",
            "A piece is one saved furniture item. Open replaces the current build (undoable).",
            11f, Muted, false);
        PlaceTop(hint.rectTransform, 20f, -46f, 332f, 30f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        if (_theme != null)
            _theme.mutedTexts.Add(hint);

        // Close (×) — multiplication sign, not U+2715: the dingbat is missing
        // from the UI font and rendered as a placeholder box.
        Button close = CreateButton(_panel, "Btn_Close", "×", Surface, Ink, out _);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(32f, 32f);
        close.onClick.AddListener(() => _panel.gameObject.SetActive(false));

        // Save row: name field + button
        _nameInput = CreateInputField(_panel, "NameInput", "Name this piece…");
        var inputRt = (RectTransform)_nameInput.transform;
        PlaceTop(inputRt, 16f, -84f, 372f - 16f - 118f - 8f - 16f, 40f);

        Button save = CreateButton(_panel, "Btn_SavePiece", "Save as piece", Accent, Color.white, out _);
        var saveRt = (RectTransform)save.transform;
        saveRt.anchorMin = saveRt.anchorMax = new Vector2(1f, 1f);
        saveRt.pivot = new Vector2(1f, 1f);
        saveRt.anchoredPosition = new Vector2(-16f, -84f);
        saveRt.sizeDelta = new Vector2(118f, 40f);
        save.onClick.AddListener(SaveNewPiece);
        if (_theme != null)
            _theme.accentImages.Add(save.GetComponent<Image>());

        BuildList();

        _emptyLabel = CreateText(_panel, "Empty",
            "No pieces yet.\nBuild something and press \"Save as piece\".",
            12.5f, Muted, false);
        var emptyRt = _emptyLabel.rectTransform;
        emptyRt.anchorMin = new Vector2(0f, 0f);
        emptyRt.anchorMax = new Vector2(1f, 0f);
        emptyRt.pivot = new Vector2(0.5f, 0f);
        emptyRt.anchoredPosition = new Vector2(0f, 200f);
        emptyRt.sizeDelta = new Vector2(-40f, 60f);
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
        scrollRt.offsetMin = new Vector2(16f, 16f);
        scrollRt.offsetMax = new Vector2(-16f, -136f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportGo.transform.SetParent(scrollRt, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        var viewportImg = viewportGo.GetComponent<Image>();
        viewportImg.color = Color.clear;   // raycast surface for drag-scrolling

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportRt, false);
        _listContent = (RectTransform)contentGo.transform;
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.sizeDelta = new Vector2(0f, 0f);

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

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    void Refresh()
    {
        foreach (Texture2D tex in _thumbnails)
            if (tex != null)
                Destroy(tex);
        _thumbnails.Clear();

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        List<PieceLibrary.PieceRecord> records = PieceLibrary.LoadAll();
        if (_emptyLabel != null)
            _emptyLabel.gameObject.SetActive(records.Count == 0);

        foreach (PieceLibrary.PieceRecord record in records)
            BuildRow(record);
    }

    void BuildRow(PieceLibrary.PieceRecord record)
    {
        var go = new GameObject("Piece_" + record.id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_listContent, false);

        var bg = go.GetComponent<Image>();
        bg.color = Surface;
        StyleCard(bg, 1.6f);

        var element = go.AddComponent<LayoutElement>();
        element.preferredHeight = 64f;

        var rt = (RectTransform)go.transform;

        // Thumbnail
        var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
        thumbGo.transform.SetParent(rt, false);
        var thumbRt = (RectTransform)thumbGo.transform;
        thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(0f, 0.5f);
        thumbRt.pivot = new Vector2(0f, 0.5f);
        thumbRt.anchoredPosition = new Vector2(10f, 0f);
        thumbRt.sizeDelta = new Vector2(72f, 48f);

        var raw = thumbGo.GetComponent<RawImage>();
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

        // Name + metadata
        TextMeshProUGUI name = CreateText(rt, "Name", record.name, 13.5f, Ink, true);
        var nameRt = name.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.anchoredPosition = new Vector2(92f, -9f);
        nameRt.sizeDelta = new Vector2(-92f - 186f, 20f);
        name.overflowMode = TextOverflowModes.Ellipsis;

        int parts = record.beamCount + record.panelCount;
        string meta = $"{parts} parts · {record.widthMm}×{record.depthMm}×{record.heightMm} mm";
        if (record.price > 0f)
            meta += $" · ${record.price:N0}";
        TextMeshProUGUI metaText = CreateText(rt, "Meta", meta, 10.5f, Muted, false);
        var metaRt = metaText.rectTransform;
        metaRt.anchorMin = new Vector2(0f, 1f);
        metaRt.anchorMax = new Vector2(1f, 1f);
        metaRt.pivot = new Vector2(0f, 1f);
        metaRt.anchoredPosition = new Vector2(92f, -31f);
        metaRt.sizeDelta = new Vector2(-92f - 186f, 16f);
        metaText.overflowMode = TextOverflowModes.Ellipsis;

        // Actions: ID (copy code) · Open · Update (overwrite) · × (delete).
        // Plain text labels — the old ⟳ / ✕ dingbats are missing from the UI
        // font and rendered as placeholder boxes.
        Button id = CreateButton(rt, "Btn_Id", "ID", Surface, Ink, out _);
        PlaceRowButton(id, -152f, 30f);
        id.onClick.AddListener(() =>
        {
            // A centered window with the code in a selectable box: silent
            // clipboard writes are blocked in WebGL builds.
            var codeUi = FindFirstObjectByType<ConfigurationCodeUI>();
            if (codeUi != null)
                codeUi.ShowShareDialog($"Code for \"{record.name}\"", record.code);
        });

        Button open = CreateButton(rt, "Btn_Open", "Open", Accent, Color.white, out _);
        PlaceRowButton(open, -96f, 52f);
        open.onClick.AddListener(() => OpenPiece(record));

        Button overwrite = CreateButton(rt, "Btn_Overwrite", "Update", Surface, Ink, out TextMeshProUGUI owLabel);
        owLabel.fontSize = 11f;
        PlaceRowButton(overwrite, -40f, 52f);
        MakeConfirming(overwrite, owLabel, "Update", () => OverwritePiece(record));

        Button delete = CreateButton(rt, "Btn_Delete", "×", Surface, Danger, out TextMeshProUGUI delLabel);
        PlaceRowButton(delete, -10f, 26f);
        MakeConfirming(delete, delLabel, "×", () => DeletePiece(record));
    }

    static void PlaceRowButton(Button btn, float right, float width)
    {
        var rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(right, 0f);
        rt.sizeDelta = new Vector2(width, 30f);
    }

    /// <summary>First click arms the button ("Sure?"), second click acts.</summary>
    void MakeConfirming(Button btn, TextMeshProUGUI label, string idleText, System.Action action)
    {
        bool armed = false;
        Coroutine disarm = null;
        float idleSize = label.fontSize;

        btn.onClick.AddListener(() =>
        {
            if (!armed)
            {
                armed = true;
                label.text = "Sure?";
                label.fontSize = 10f;
                disarm = StartCoroutine(Disarm());
            }
            else
            {
                if (disarm != null)
                    StopCoroutine(disarm);
                action();
            }
        });

        IEnumerator Disarm()
        {
            yield return new WaitForSeconds(2.5f);
            armed = false;
            label.text = idleText;
            label.fontSize = idleSize;
        }
    }

    // ------------------------------------------------------------------
    // Top bar
    // ------------------------------------------------------------------

    void InjectTopBarButton()
    {
        Transform bar = _canvas.transform.Find("TopBar");
        if (bar == null)
            return;

        // Baked by the current UI builder: just wire it.
        Transform baked = bar.Find("Btn_Pieces");
        if (baked != null && baked.TryGetComponent(out Button bakedBtn))
        {
            bakedBtn.onClick.RemoveListener(TogglePanel);
            bakedBtn.onClick.AddListener(TogglePanel);
            return;
        }

        // Older scene: clone a history button for a matching look.
        Transform template = bar.Find("Btn_ClearAll");
        if (template == null)
            template = bar.Find("Btn_Undo");
        if (template == null || template.GetComponent<Button>() == null)
            return;

        GameObject go = Instantiate(template.gameObject, bar);
        go.name = "Btn_Pieces";

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(848f, 0f);
        rt.sizeDelta = new Vector2(88f, 40f);

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = "Pieces";

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(TogglePanel);

        if (_theme != null)
        {
            var img = go.GetComponent<Image>();
            if (img != null)
                _theme.surfaceImages.Add(img);
            if (text != null)
                _theme.inkTexts.Add(text);
        }
    }

    // ------------------------------------------------------------------
    // Widget helpers (adopted card style, like the selection popup)
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

    Button CreateButton(RectTransform parent, string name, string label,
        Color bg, Color fg, out TextMeshProUGUI text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = bg;
        StyleCard(img, 1.8f);

        text = CreateText((RectTransform)go.transform, "Text", label, 12f, fg, true);
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        return button;
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

    TMP_InputField CreateInputField(RectTransform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);

        var bg = go.GetComponent<Image>();
        bg.color = Surface;
        StyleCard(bg, 1.8f);

        var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaGo.transform.SetParent(go.transform, false);
        var areaRt = (RectTransform)areaGo.transform;
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(12f, 6f);
        areaRt.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI placeholderText = CreateText(areaRt, "Placeholder", placeholder, 13f, Muted, false);
        StretchToParent(placeholderText.rectTransform);
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderText.fontStyle = FontStyles.Italic;

        TextMeshProUGUI valueText = CreateText(areaRt, "Text", string.Empty, 13f, Ink, false);
        StretchToParent(valueText.rectTransform);
        valueText.alignment = TextAlignmentOptions.MidlineLeft;

        var input = go.GetComponent<TMP_InputField>();
        input.targetGraphic = bg;
        input.textViewport = areaRt;
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 48;
        if (_font != null)
            input.fontAsset = _font;
        input.caretColor = Ink;
        input.customCaretColor = true;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

        return input;
    }

    /// <summary>Anchor to the panel's top-left; y is negative-down.</summary>
    static void PlaceTop(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
