using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Build/save guidance and a local quote handoff; no external commerce service.</summary>
public class WorkflowReviewUI : MonoBehaviour
{
    Canvas _canvas;
    UIThemeController _theme;
    RectTransform _dialog;
    GameObject _backdrop;
    TMP_InputField _body;
    TextMeshProUGUI _title, _feedback;
    Button _download;
    bool _quote;
    readonly System.Collections.Generic.List<Behaviour> _paused = new System.Collections.Generic.List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var build = FindFirstObjectByType<BuildController>();
        if (build != null && FindFirstObjectByType<WorkflowReviewUI>() == null)
            build.gameObject.AddComponent<WorkflowReviewUI>();
    }

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        _theme = FindFirstObjectByType<UIThemeController>();
        var menu = FindFirstObjectByType<UITopBarMenu>();
        if (_canvas == null || menu == null || menu.panel == null) return;
        AddMenuItem(menu, "Btn_SpaceSize", "Set space size", SpacePlanningUI.Show);
        AddMenuItem(menu, "Btn_PartsPrices", "Parts & prices", ShowParts);
        AddMenuItem(menu, "Btn_BuildGuide", "Build & save guide", ShowGuide);
        AddMenuItem(menu, "Btn_Quote", "Prepare quote", ShowQuote);
        ArrangeMenu(menu);
    }

    void Update()
    {
        if (_dialog != null && _dialog.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void ShowGuide()
    {
        Show("Build, save and review", "1. SET YOUR SPACE\nChoose Set space size from the menu and enter width, depth and height. The remaining fit compares the design's overall dimensions with that space; allow additional installation clearance.\n\n" +
            "2. BUILD\nUse Tools for guided frames and panels, or Parts to place individual beams. Press Esc to put a tool down. Ctrl+Z (Cmd+Z) undoes a change.\n\n" +
            "3. FINISH\nTurn Finish on to add veneers and caps. Open Palette to choose panel and frame-dressing colours. Colours on screen are previews; confirm samples before ordering.\n\n" +
            "4. SAVE\nOpen My Pieces, name your design and choose Save as piece. Pieces are stored on this device/browser. Keep a separate copy of the piece's ID code for a portable backup. Local data can be lost if browser storage is cleared.\n\n" +
            "5. REOPEN\nUse Open in My Pieces, or Load code from the menu. Opening a piece replaces the current build and can be undone. Check its parts, panels and finish before continuing.\n\n" +
            "6. REVIEW\nClick the parts/price total or choose Parts & prices from the menu to review frames, panels, veneers, caps and feet. Prices are placeholders. In Piece Mode, choose Prepare quote to copy or download the design and its parts list for your supplier; checkout is not connected.", false);
    }

    /// <summary>Open the current bill of parts from the menu or summary pill.</summary>
    public void ShowParts()
    {
        try
        {
            var stats = CurrentStats();
            stats.RefreshNow();
            Show("Parts & prices", QuoteSummary.CreateParts(stats.Summary), false,
                "Copy the current parts list. All prices are placeholder estimates.");
        }
        catch (Exception e)
        {
            SelectionStatus.Set("Could not show parts: " + e.Message, 6f);
        }
    }

    public void ShowQuote()
    {
        if (SpaceModeController.Active)
        {
            SelectionStatus.Set("Prepare quotes in Piece Mode. Export your space code from the Space panel for an arrangement review.", 6f);
            return;
        }
        if (BuildHistory.Instance != null && BuildHistory.Instance.IsRestoring)
        {
            SelectionStatus.Set("Wait for the current change to finish before preparing a quote.", 4f);
            return;
        }
        try
        {
            var build = FindFirstObjectByType<BuildController>();
            var restorer = FindFirstObjectByType<ConfigurationRestorer>();
            if (restorer != null && restorer.IsRunning)
                throw new InvalidOperationException("Wait for the design to finish loading.");
            ConfigurationModel model = ConfigurationCapture.Capture(build);
            var stats = CurrentStats();
            stats.RefreshNow();
            string summary = QuoteSummary.Create(model, stats.Summary,
                FinishStyle.PanelSwatch.Label, FinishStyle.DressingSwatch.Label, DateTime.UtcNow);
            if (SpacePlanningUI.TryGetTarget(out SpacePlanningTarget target))
                summary += $"\nAVAILABLE SPACE BRIEF\nWidth {target.WidthMm} mm x depth {target.DepthMm} mm x height {target.HeightMm} mm.\nOverall size only; supplier must confirm installation clearances.\n";
            Show("Review quote request", summary, true);
        }
        catch (Exception e)
        {
            SelectionStatus.Set("Could not prepare quote: " + e.Message, 6f);
        }
    }

    static UIBuildStats CurrentStats()
    {
        if (BuildHistory.Instance != null && BuildHistory.Instance.IsRestoring)
            throw new InvalidOperationException("Wait for the current change to finish.");
        var restorer = FindFirstObjectByType<ConfigurationRestorer>();
        if (restorer != null && restorer.IsRunning)
            throw new InvalidOperationException("Wait for the design to finish loading.");
        var stats = FindFirstObjectByType<UIBuildStats>();
        if (stats == null) throw new InvalidOperationException("The parts summary is not available.");
        return stats;
    }

    void Show(string title, string body, bool quote, string feedback = null)
    {
        if (_dialog == null) BuildDialog();
        if (_dialog == null) return;
        _quote = quote;
        _title.text = title;
        _body.text = body;
        _body.caretPosition = 0;
        _download.gameObject.SetActive(quote);
        _feedback.text = feedback ?? (quote ? "Review, then copy or download for your supplier." : "Your design stays open while you read.");
        _backdrop.SetActive(true);
        _dialog.gameObject.SetActive(true);
        _backdrop.transform.SetAsLastSibling();
        _dialog.SetAsLastSibling();
        PauseWorkspaceInput();
    }

    void PauseWorkspaceInput()
    {
        if (_paused.Count > 0) return;
        // A UI backdrop intercepts pointer clicks, but Unity keyboard shortcuts still
        // reach selection, undo and space controls unless their input loops are paused.
        foreach (Behaviour control in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!control.enabled) continue;
            bool workspaceInput = control is FlyCameraController || control is CadCameraController ||
                control is OrbitCamera || control is ConfiguratorCameraController ||
                control is BuildController || control is GhostController || control is FreePartSession ||
                control is TemplateSession || control is SelectionManager || control is MarqueeSelectionController ||
                control is StructureClipboard || control is BeamResizeSession || control is PanelLayerMover ||
                control is BuildHistory || control is SpaceModeController || control is SpaceInteractionController;
            if (!workspaceInput) continue;
            control.enabled = false;
            _paused.Add(control);
        }
    }

    void Close()
    {
        if (_dialog != null) _dialog.gameObject.SetActive(false);
        if (_backdrop != null) _backdrop.SetActive(false);
        foreach (Behaviour control in _paused) if (control != null) control.enabled = true;
        _paused.Clear();
    }

    void OnDisable() => Close();
    void OnDestroy()
    {
        if (_dialog != null) Destroy(_dialog.gameObject);
        if (_backdrop != null) Destroy(_backdrop);
    }

    void Download()
    {
        if (!_quote) return;
        string fileName = "neospace-quote-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt";
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NeoDownloadQuote(fileName, _body.text);
            _feedback.text = "Download requested. Check your browser's downloads.";
#else
            string directory = Path.Combine(Application.persistentDataPath, "Quotes");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, _body.text, new System.Text.UTF8Encoding(false));
            _feedback.text = "Saved to " + path;
#endif
        }
        catch (Exception e) { _feedback.text = "Download failed: " + e.Message + ". You can still copy the text."; }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void NeoDownloadQuote(string filename, string content);
#endif

    void BuildDialog()
    {
        if (_canvas == null) return;
        _backdrop = new GameObject("ReviewBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdrop.transform.SetParent(_canvas.transform, false);
        Stretch((RectTransform)_backdrop.transform, Vector2.zero, Vector2.zero);
        _backdrop.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);
        _backdrop.GetComponent<Button>().onClick.AddListener(Close);
        var go = new GameObject("WorkflowReview", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _dialog = (RectTransform)go.transform;
        _dialog.anchorMin = new Vector2(0.18f, 0.12f);
        _dialog.anchorMax = new Vector2(0.82f, 0.88f);
        _dialog.offsetMin = _dialog.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = UIThemeController.CardColor;
        if (_theme != null) _theme.RegisterCard(go.GetComponent<Image>());
        _title = Text(_dialog, "Title", "", 21);
        Place(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -58), new Vector2(-88, -20));
        Button close = Button(_dialog, "Close", Close);
        Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-78, -54), new Vector2(-18, -18));

        var inputGo = new GameObject("ReviewText", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(_dialog, false);
        Stretch((RectTransform)inputGo.transform, new Vector2(24, 104), new Vector2(-24, -72));
        inputGo.GetComponent<Image>().color = UIThemeController.SurfaceColor;
        if (_theme != null) _theme.RegisterSurface(inputGo.GetComponent<Image>());
        _body = inputGo.GetComponent<TMP_InputField>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(inputGo.transform, false);
        Stretch((RectTransform)viewport.transform, new Vector2(14, 10), new Vector2(-14, -10));
        var bodyText = Text(viewport.transform, "Text", "", 14);
        Stretch(bodyText.rectTransform, Vector2.zero, Vector2.zero);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.richText = false;
        _body.textViewport = (RectTransform)viewport.transform;
        _body.textComponent = bodyText;
        _body.lineType = TMP_InputField.LineType.MultiLineNewline;
        _body.readOnly = true;
        _body.scrollSensitivity = 24;
        _body.onFocusSelectAll = false;
        _feedback = Text(_dialog, "Feedback", "", 11);
        Place(_feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(24, 62), new Vector2(-24, 96));
        _feedback.textWrappingMode = TextWrappingModes.Normal;
        _feedback.richText = false;
        var copy = Button(_dialog, "Copy text", () => {
            NativeClipboard.Copy(_body.text);
            _feedback.text = "Copy requested. If blocked, select the text and press Ctrl+C (Cmd+C).";
        });
        Place((RectTransform)copy.transform, Vector2.zero, Vector2.zero, new Vector2(24, 18), new Vector2(146, 54));
        _download = Button(_dialog, "Download .txt", Download);
        Place((RectTransform)_download.transform, Vector2.zero, Vector2.zero, new Vector2(158, 18), new Vector2(296, 54));
    }

    TextMeshProUGUI Text(Transform parent, string name, string value, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        var donor = _canvas.GetComponentInChildren<TextMeshProUGUI>();
        if (donor != null) text.font = donor.font;
        text.text = value; text.fontSize = size; text.color = UIThemeController.InkColor;
        if (_theme != null) _theme.RegisterInkText(text);
        text.raycastTarget = false;
        return text;
    }

    Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = UIThemeController.SurfaceColor;
        if (_theme != null) _theme.RegisterSurface(go.GetComponent<Image>());
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
        UiPolish.HoverTint(button);
        var text = Text(go.transform, "Label", label, 13);
        text.alignment = TextAlignmentOptions.Midline;
        Stretch(text.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
        return button;
    }

    void AddMenuItem(UITopBarMenu menu, string name, string label, UnityEngine.Events.UnityAction action)
    {
        if (menu.panel.transform.Find(name) != null) return;
        Button button = Button(menu.panel.transform, label, () => { menu.Close(); action(); });
        button.name = name;
    }

    static void ArrangeMenu(UITopBarMenu menu)
    {
        Transform root = menu.panel.transform;
        float y = -8;
        foreach (string name in new[] { "Btn_LoadCode", "Btn_SpaceSize", "Btn_PartsPrices", "Btn_BuildGuide", "Btn_Quote", "Btn_ClearAll" })
        {
            if (!(root.Find(name) is RectTransform row)) continue;
            Place(row, new Vector2(0, 1), Vector2.one, new Vector2(8, y - 44), new Vector2(-8, y));
            y -= 48;
        }
        Transform divider = root.Find("Divider");
        if (divider != null) divider.gameObject.SetActive(false);
        ((RectTransform)root).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -y + 4);
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max) => Place(rt, Vector2.zero, Vector2.one, min, max);
    static void Place(RectTransform rt, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
    {
        rt.anchorMin = a; rt.anchorMax = b; rt.offsetMin = min; rt.offsetMax = max;
    }
}
