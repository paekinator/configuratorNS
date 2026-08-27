using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading configuration codes in the live app.
///
///  - LOAD CODE (top bar) opens a dialog with a text box: paste any code and
///    press Load. Piece codes ("NS1-…") open in Piece Mode, space codes
///    ("NSS1-…") open in Space Mode — the app switches modes automatically.
///    Loading replaces what is there (one undo step). Invalid codes show
///    their error inside the dialog and never touch the scene.
///  - Getting a code is contextual: the ID button on any row of My Pieces and
///    the Space panel's "Copy code" button open a centered share window
///    (<see cref="ShowShareDialog"/>) with the code in a selectable box and a
///    Copy button. The window exists because silent clipboard writes are
///    blocked in WebGL builds.
///
/// Buttons are wired from the baked scene when present, or cloned from the
/// history buttons in older scenes; a leftover baked "Share code" button is
/// removed at runtime.
/// </summary>
public class ConfigurationCodeUI : MonoBehaviour
{
    public BuildController buildController;

    static readonly Color Danger = new Color(0.749f, 0.290f, 0.251f);   // #BF4A40, matches the theme

    Canvas _canvas;
    UIThemeController _theme;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    RectTransform _dialog;
    GameObject _backdrop;
    TMP_InputField _codeInput;
    TextMeshProUGUI _errorLabel;

    // Share window: shows a code in a selectable box with a Copy button.
    RectTransform _shareDialog;
    TextMeshProUGUI _shareTitle;
    TMP_InputField _shareInput;
    TextMeshProUGUI _shareFeedback;

    readonly List<Behaviour> _suppressedCameraControls = new List<Behaviour>();

    const string ShareHintText = "Anyone can load it with \"Load code\".";

    Color Ink => _theme != null ? Palette.ink : new Color(0.149f, 0.133f, 0.118f);
    Color Surface => _theme != null ? Palette.surface : new Color(0.953f, 0.937f, 0.914f);
    Color CardColor => _theme != null ? Palette.card : Color.white;
    Color Accent => _theme != null ? Palette.accent : new Color(0.851f, 0.424f, 0.278f);
    Color Muted => _theme != null ? Palette.muted : new Color(0.561f, 0.533f, 0.502f);
    UIThemeController.Palette Palette => _theme.IsDark ? _theme.dark : _theme.light;

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        _theme = FindFirstObjectByType<UIThemeController>();
        AdoptCardStyle();
        InjectTopBarButtons();
    }

    void OnEnable() => UIThemeController.ThemeChanged += OnThemeChanged;

    void OnDisable() => UIThemeController.ThemeChanged -= OnThemeChanged;

    void OnThemeChanged()
    {
        RestyleDialog(_dialog);
        RestyleDialog(_shareDialog);
        RestyleInput(_codeInput);
        RestyleInput(_shareInput);
        if (_shareFeedback != null)
            _shareFeedback.color = Muted;
    }

    void RestyleDialog(RectTransform dialog)
    {
        if (dialog == null)
            return;
        RestyleNamed(dialog, "Btn_Cancel", Surface, Ink);
        RestyleNamed(dialog, "Btn_Paste", Surface, Ink);
        RestyleNamed(dialog, "Btn_Close", Surface, Ink);
        RestyleNamed(dialog, "Btn_Load", Accent, Color.white);
        RestyleNamed(dialog, "Btn_Copy", Accent, Color.white);
    }

    static void RestyleNamed(Transform root, string name, Color bg, Color fg)
    {
        Transform t = root.Find(name);
        if (t == null)
            return;
        if (t.TryGetComponent(out Image img))
            img.color = bg;
        var text = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.color = fg;
    }

    void RestyleInput(TMP_InputField input)
    {
        if (input == null)
            return;
        if (input.TryGetComponent(out Image bg))
            bg.color = Surface;
        if (input.placeholder is TextMeshProUGUI placeholder)
            placeholder.color = Muted;
        if (input.textComponent != null)
            input.textComponent.color = Ink;
        input.caretColor = Ink;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
    }

    void Update()
    {
        bool open = (_dialog != null && _dialog.gameObject.activeSelf) ||
                    (_shareDialog != null && _shareDialog.gameObject.activeSelf);

        if (open && Input.GetKeyDown(KeyCode.Escape))
            CloseAllDialogs();

        // The camera controllers read raw keys; typing/pasting a code must
        // not fly the camera around.
        if (open && _suppressedCameraControls.Count == 0)
            SuppressCameraControls(true);
        else if (!open && _suppressedCameraControls.Count > 0)
            SuppressCameraControls(false);
    }

    void SuppressCameraControls(bool suppress)
    {
        if (suppress)
        {
            // The controllers live on the CameraRig, not the camera itself —
            // find them by type, wherever they are.
            var controllers = new Behaviour[]
            {
                FindFirstObjectByType<FlyCameraController>(),
                FindFirstObjectByType<CadCameraController>()
            };
            foreach (Behaviour b in controllers)
            {
                if (b != null && b.enabled)
                {
                    b.enabled = false;
                    _suppressedCameraControls.Add(b);
                }
            }
        }
        else
        {
            foreach (Behaviour b in _suppressedCameraControls)
                if (b != null)
                    b.enabled = true;
            _suppressedCameraControls.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Space code copy (called by the Space panel's button)
    // ------------------------------------------------------------------

    /// <summary>Show the current Space Mode arrangement as an NSS1 code.</summary>
    public void CopySpaceCode()
    {
        var interaction = FindFirstObjectByType<SpaceInteractionController>();
        if (interaction == null)
            return;

        try
        {
            string code = SpaceCodec.Encode(interaction.CurrentStates());
            int n = interaction.InstanceCount;
            ShowShareDialog($"Space code · {n} piece{(n == 1 ? "" : "s")}", code);
        }
        catch (ConfigurationCodeException e)
        {
            SelectionStatus.Set(e.Message, 5f);
        }
    }

    // ------------------------------------------------------------------
    // Share window: a code in a selectable box, front and center
    // ------------------------------------------------------------------

    /// <summary>
    /// Show a code in a centered window with a Copy button. The code is
    /// pre-selected, so Ctrl+C (Cmd+C) works right away too. This replaces
    /// silent clipboard writes, which browsers block in WebGL builds.
    /// </summary>
    public void ShowShareDialog(string title, string code)
    {
        if (_shareDialog == null)
            BuildShareDialog();
        if (_shareDialog == null)
            return;

        CloseDialog();   // never two dialogs at once

        _shareTitle.text = title;
        _shareInput.text = code;
        _shareFeedback.text = ShareHintText;
        _shareFeedback.color = Muted;

        _backdrop.SetActive(true);
        _shareDialog.gameObject.SetActive(true);
        _backdrop.transform.SetAsLastSibling();
        _shareDialog.SetAsLastSibling();

        // Focus the box with everything selected: Ctrl+C works immediately.
        _shareInput.Select();
        _shareInput.ActivateInputField();
    }

    void OnShareCopyClicked()
    {
        NativeClipboard.Copy(_shareInput.text);
        _shareFeedback.text = "Copied · paste it anywhere with Ctrl+V (Cmd+V).";
        _shareFeedback.color = Accent;

        // Re-select so manual Ctrl+C stays available as a fallback.
        _shareInput.Select();
        _shareInput.ActivateInputField();
    }

    void CloseShareDialog()
    {
        if (_shareDialog != null)
            _shareDialog.gameObject.SetActive(false);
        if (_backdrop != null && (_dialog == null || !_dialog.gameObject.activeSelf))
            _backdrop.SetActive(false);
    }

    void CloseAllDialogs()
    {
        CloseDialog();
        CloseShareDialog();
    }

    // ------------------------------------------------------------------
    // Load dialog
    // ------------------------------------------------------------------

    public void ShowLoadDialog()
    {
        if (_dialog == null)
            BuildDialog();
        if (_dialog == null)
            return;

        CloseShareDialog();   // never two dialogs at once

        // Always open EMPTY — the user pastes explicitly (Cmd/Ctrl+V or the
        // Paste button). Prefilling from the clipboard looked like a mystery
        // code appearing out of nowhere.
        _errorLabel.text = string.Empty;
        _codeInput.text = string.Empty;

        _backdrop.SetActive(true);
        _dialog.gameObject.SetActive(true);
        // Order matters: backdrop below, dialog on top. (Anything else and
        // the full-screen backdrop eats every click meant for the dialog.)
        _backdrop.transform.SetAsLastSibling();
        _dialog.SetAsLastSibling();

        _codeInput.Select();
        _codeInput.ActivateInputField();
    }

    void CloseDialog()
    {
        if (_dialog != null)
            _dialog.gameObject.SetActive(false);
        if (_backdrop != null && (_shareDialog == null || !_shareDialog.gameObject.activeSelf))
            _backdrop.SetActive(false);
    }

    void OnLoadClicked()
    {
        string code = _codeInput != null ? _codeInput.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(code))
        {
            _errorLabel.text = "Paste a code into the box first.";
            return;
        }

        if (!code.StartsWith("NS", System.StringComparison.OrdinalIgnoreCase))
        {
            _errorLabel.text =
                "That doesn't look like a NEOSPACE code · piece codes start " +
                "with NS1-, space codes with NSS1-.";
            return;
        }

        if (SpaceCodec.LooksLikeSpaceCode(code))
        {
            List<SpaceHistory.InstanceState> states;
            try
            {
                states = SpaceCodec.Decode(code);
            }
            catch (ConfigurationCodeException e)
            {
                _errorLabel.text = e.Message;
                return;
            }

            CloseDialog();
            StartCoroutine(LoadSpaceRoutine(states));
            return;
        }

        // Piece code.
        ConfigurationCodeValidation check = ConfigurationCode.Validate(code);
        if (!check.IsValid)
        {
            _errorLabel.text = check.Error;
            return;
        }

        var restorer = ConfigurationCode.GetOrCreateRestorer(buildController);
        if (restorer.IsRunning)
        {
            _errorLabel.text = "Still loading the previous code · one moment.";
            return;
        }

        CloseDialog();
        LoadValidatedPiece(check);
    }

    void LoadValidatedPiece(ConfigurationCodeValidation check)
    {
        foreach (string warning in check.Warnings)
            Debug.LogWarning("[ConfigCode] " + warning);

        // Retired-part warnings matter to the user, not just the console.
        string warningNote = check.Warnings.Count > 0
            ? " Note: " + check.Warnings[0]
            : string.Empty;

        // Piece codes belong to Piece Mode.
        var spaceMode = FindFirstObjectByType<SpaceModeController>();
        if (SpaceModeController.Active && spaceMode != null)
            spaceMode.ExitSpaceMode();

        // Replace, not merge: the decoded configuration becomes the build.
        var restorer = ConfigurationCode.GetOrCreateRestorer(buildController);
        restorer.Restore(check.Model, report =>
        {
            SelectionStatus.Set(
                report.Summary + warningNote +
                " Ctrl+Z (Cmd+Z) restores the previous build.", 8f);
        });
        SelectionStatus.Set("Loading configuration…", 3f);
    }

    IEnumerator LoadSpaceRoutine(List<SpaceHistory.InstanceState> states)
    {
        var spaceMode = FindFirstObjectByType<SpaceModeController>();
        var interaction = FindFirstObjectByType<SpaceInteractionController>();
        var history = FindFirstObjectByType<SpaceHistory>();
        if (spaceMode == null || interaction == null || history == null)
        {
            SelectionStatus.Set("Space Mode is not available in this scene.", 5f);
            yield break;
        }

        // Space codes belong to Space Mode — switch over first.
        if (!SpaceModeController.Active)
        {
            spaceMode.EnterSpaceMode();
            float deadline = Time.unscaledTime + 3f;
            while (!SpaceModeController.Active && Time.unscaledTime < deadline)
                yield return null;
            if (!SpaceModeController.Active)
            {
                SelectionStatus.Set("Could not switch to Space Mode.", 5f);
                yield break;
            }
        }

        SelectionStatus.Set("Loading space…", 0f);

        bool done = false;
        interaction.RebuildFromStates(states, () => done = true);
        while (!done)
            yield return null;

        history.Record(interaction.CurrentStates());
        int n = interaction.InstanceCount;
        SelectionStatus.Set(
            $"Space loaded · {n} piece{(n == 1 ? "" : "s")} placed. " +
            "Ctrl+Z (Cmd+Z) brings the previous space back.", 7f);
    }

    // ------------------------------------------------------------------
    // Dialog UI
    // ------------------------------------------------------------------

    /// <summary>Shared dim backdrop behind either dialog; clicking it closes.</summary>
    void EnsureBackdrop()
    {
        if (_backdrop != null || _canvas == null)
            return;

        _backdrop = new GameObject("DialogBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdrop.transform.SetParent(_canvas.transform, false);
        var backRt = (RectTransform)_backdrop.transform;
        backRt.anchorMin = Vector2.zero;
        backRt.anchorMax = Vector2.one;
        backRt.offsetMin = Vector2.zero;
        backRt.offsetMax = Vector2.zero;
        var backImg = _backdrop.GetComponent<Image>();
        backImg.color = new Color(0f, 0f, 0f, 0.35f);
        var backBtn = _backdrop.GetComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(CloseAllDialogs);
        _backdrop.SetActive(false);
    }

    void BuildDialog()
    {
        if (_canvas == null)
            return;

        EnsureBackdrop();

        var go = new GameObject("LoadCodeDialog", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _dialog = (RectTransform)go.transform;
        _dialog.anchorMin = _dialog.anchorMax = new Vector2(0.5f, 0.5f);
        _dialog.pivot = new Vector2(0.5f, 0.5f);
        _dialog.anchoredPosition = new Vector2(0f, 40f);
        _dialog.sizeDelta = new Vector2(560f, 250f);

        var img = go.GetComponent<Image>();
        img.color = CardColor;
        StyleCard(img, 1.2f);
        if (_theme != null)
            _theme.cardImages.Add(img);

        UiPolish.SoftShadow(_dialog);

        TextMeshProUGUI title = CreateText(_dialog, "Title", "Load a configuration code", 18f, Ink, true);
        PlaceTop(title.rectTransform, 24f, -20f, 400f, 26f);
        if (_theme != null)
            _theme.inkTexts.Add(title);

        TextMeshProUGUI hint = CreateText(_dialog, "Hint",
            "Paste a code below · a piece code (NS1-…) or a whole space (NSS1-…). " +
            "Loading replaces what's on screen; Ctrl+Z (Cmd+Z) brings it back.",
            11.5f, Muted, false);
        PlaceTop(hint.rectTransform, 24f, -50f, 560f - 48f, 34f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        if (_theme != null)
            _theme.mutedTexts.Add(hint);

        _codeInput = CreateInputField(_dialog, "CodeInput", "NS1-… or NSS1-…");
        PlaceTop((RectTransform)_codeInput.transform, 24f, -96f, 560f - 48f, 44f);

        _errorLabel = CreateText(_dialog, "Error", string.Empty, 11.5f, Danger, false);
        PlaceTop(_errorLabel.rectTransform, 24f, -146f, 560f - 48f, 34f);
        _errorLabel.textWrappingMode = TextWrappingModes.Normal;

        // Buttons bottom-right: Cancel · Load
        Button load = CreateButton(_dialog, "Btn_Load", "Load", Accent, Color.white, out _);
        var loadRt = (RectTransform)load.transform;
        loadRt.anchorMin = loadRt.anchorMax = new Vector2(1f, 0f);
        loadRt.pivot = new Vector2(1f, 0f);
        loadRt.anchoredPosition = new Vector2(-24f, 18f);
        loadRt.sizeDelta = new Vector2(96f, 40f);
        load.onClick.AddListener(OnLoadClicked);
        if (_theme != null)
            _theme.accentImages.Add(load.GetComponent<Image>());

        Button cancel = CreateButton(_dialog, "Btn_Cancel", "Cancel", Surface, Ink, out _);
        var cancelRt = (RectTransform)cancel.transform;
        cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(1f, 0f);
        cancelRt.pivot = new Vector2(1f, 0f);
        cancelRt.anchoredPosition = new Vector2(-128f, 18f);
        cancelRt.sizeDelta = new Vector2(88f, 40f);
        cancel.onClick.AddListener(CloseDialog);

        // Bottom-left: pull the clipboard into the box on demand.
        Button paste = CreateButton(_dialog, "Btn_Paste", "Paste", Surface, Ink, out _);
        var pasteRt = (RectTransform)paste.transform;
        pasteRt.anchorMin = pasteRt.anchorMax = new Vector2(0f, 0f);
        pasteRt.pivot = new Vector2(0f, 0f);
        pasteRt.anchoredPosition = new Vector2(24f, 18f);
        pasteRt.sizeDelta = new Vector2(88f, 40f);
        paste.onClick.AddListener(() =>
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip))
            {
                _errorLabel.text = "The clipboard is empty.";
                return;
            }
            _errorLabel.text = string.Empty;
            _codeInput.text = clip.Trim();
            _codeInput.ActivateInputField();
            _codeInput.MoveTextEnd(false);
        });

        // Pressing Enter in the field loads too.
        _codeInput.onSubmit.AddListener(_ => OnLoadClicked());

        _backdrop.SetActive(false);
        _dialog.gameObject.SetActive(false);
    }

    void BuildShareDialog()
    {
        if (_canvas == null)
            return;

        EnsureBackdrop();

        var go = new GameObject("ShareCodeDialog", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _shareDialog = (RectTransform)go.transform;
        _shareDialog.anchorMin = _shareDialog.anchorMax = new Vector2(0.5f, 0.5f);
        _shareDialog.pivot = new Vector2(0.5f, 0.5f);
        _shareDialog.anchoredPosition = new Vector2(0f, 40f);
        _shareDialog.sizeDelta = new Vector2(560f, 216f);

        var img = go.GetComponent<Image>();
        img.color = CardColor;
        StyleCard(img, 1.2f);
        if (_theme != null)
            _theme.cardImages.Add(img);

        UiPolish.SoftShadow(_shareDialog);

        _shareTitle = CreateText(_shareDialog, "Title", "Code", 18f, Ink, true);
        PlaceTop(_shareTitle.rectTransform, 24f, -20f, 560f - 48f, 26f);
        _shareTitle.overflowMode = TextOverflowModes.Ellipsis;
        if (_theme != null)
            _theme.inkTexts.Add(_shareTitle);

        _shareInput = CreateInputField(_shareDialog, "CodeBox", string.Empty);
        PlaceTop((RectTransform)_shareInput.transform, 24f, -60f, 560f - 48f, 44f);
        _shareInput.readOnly = true;          // selectable and copyable, not editable
        _shareInput.onFocusSelectAll = true;

        _shareFeedback = CreateText(_shareDialog, "Feedback", ShareHintText, 11.5f, Muted, false);
        PlaceTop(_shareFeedback.rectTransform, 24f, -112f, 560f - 48f, 30f);
        _shareFeedback.textWrappingMode = TextWrappingModes.Normal;

        Button copy = CreateButton(_shareDialog, "Btn_Copy", "Copy", Accent, Color.white, out _);
        var copyRt = (RectTransform)copy.transform;
        copyRt.anchorMin = copyRt.anchorMax = new Vector2(1f, 0f);
        copyRt.pivot = new Vector2(1f, 0f);
        copyRt.anchoredPosition = new Vector2(-24f, 18f);
        copyRt.sizeDelta = new Vector2(96f, 40f);
        copy.onClick.AddListener(OnShareCopyClicked);
        if (_theme != null)
            _theme.accentImages.Add(copy.GetComponent<Image>());

        Button close = CreateButton(_shareDialog, "Btn_Close", "Close", Surface, Ink, out _);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0f);
        closeRt.pivot = new Vector2(1f, 0f);
        closeRt.anchoredPosition = new Vector2(-128f, 18f);
        closeRt.sizeDelta = new Vector2(88f, 40f);
        close.onClick.AddListener(CloseShareDialog);

        _shareDialog.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Top-bar buttons
    // ------------------------------------------------------------------

    void InjectTopBarButtons()
    {
        Canvas canvas = _canvas != null ? _canvas : FindFirstObjectByType<Canvas>();
        Transform bar = canvas != null ? canvas.transform.Find("TopBar") : null;
        if (bar == null)
            return;

        // The "Share code" button is gone — remove it from scenes that still
        // bake it (codes are copied from My Pieces / the Space panel now).
        Transform legacyShare = bar.Find("Btn_CopyCode");
        if (legacyShare != null)
            Destroy(legacyShare.gameObject);

        // Current builder: "Load code" is an item in the ⋯ overflow menu.
        if (Wire(bar, "MoreMenu/Btn_LoadCode", "Load code", ShowLoadDialog))
        {
            // A leftover top-level pill from the previous layout is redundant.
            Transform legacyLoad = bar.Find("Btn_PasteCode");
            if (legacyLoad != null)
                Destroy(legacyLoad.gameObject);
            return;
        }

        // Previous builder: top-level "Load code" pill.
        if (Wire(bar, "Btn_PasteCode", "Load code", ShowLoadDialog))
            return;

        // Older scene: clone a history button so fonts/sprites/colors match.
        Transform template = bar.Find("Btn_ClearAll");
        if (template == null)
            template = bar.Find("Btn_Undo");
        if (template == null || template.GetComponent<Button>() == null)
            return;

        GameObject clone = Instantiate(template.gameObject, bar);
        clone.name = "Btn_PasteCode";

        var rt = (RectTransform)clone.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(622f, 0f);
        rt.sizeDelta = new Vector2(108f, 40f);

        var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = "Load code";

        var btn = clone.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();   // drop cloned listeners
        btn.onClick.AddListener(ShowLoadDialog);

        if (_theme != null)
        {
            var cloneImg = clone.GetComponent<Image>();
            if (cloneImg != null)
                _theme.surfaceImages.Add(cloneImg);
            if (text != null)
                _theme.inkTexts.Add(text);
        }
    }

    static bool Wire(Transform bar, string name, string label,
        UnityEngine.Events.UnityAction action)
    {
        Transform t = bar.Find(name);
        var btn = t != null ? t.GetComponent<Button>() : null;
        if (btn == null)
            return false;

        var text = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = label;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
        return true;
    }

    // ------------------------------------------------------------------
    // Widget helpers (adopted card style, same pattern as PieceUI)
    // ------------------------------------------------------------------

    void AdoptCardStyle()
    {
        if (_canvas == null)
            return;
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

        text = CreateText((RectTransform)go.transform, "Text", label, 12.5f, fg, true);
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
        input.characterLimit = 0;   // codes can be long
        if (_font != null)
            input.fontAsset = _font;
        input.caretColor = Ink;
        input.customCaretColor = true;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

        return input;
    }

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
