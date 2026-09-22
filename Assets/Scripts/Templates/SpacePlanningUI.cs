using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Beginner size brief and live fit feedback. Uses the existing Frames/Panels
/// template tools; the brief does not restrict the expert parts workflow.
/// </summary>
public class SpacePlanningUI : MonoBehaviour
{
    const string PreferenceKey = "neospace.space-size.v1";
    const float PanelWidth = 540f;
    static SpacePlanningUI _instance;
    Canvas _canvas;
    BuildController _build;
    RectTransform _panel;
    GameObject _backdrop;
    RectTransform _fitCard;
    TMP_InputField _width, _depth, _height;
    TextMeshProUGUI _message, _fitText;
    TMP_FontAsset _font;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    readonly List<Behaviour> _suppressed = new List<Behaviour>();
    readonly List<LineRenderer> _outline = new List<LineRenderer>();
    GameObject _outlineRoot;
    Material _outlineMaterial;
    SpacePlanningTarget _target;
    bool _hasTarget;
    float _nextMeasure;

    public static bool IsOpen => _instance != null && _instance._panel != null && _instance._panel.gameObject.activeSelf;

    public static bool TryGetTarget(out SpacePlanningTarget target)
    {
        target = _instance != null ? _instance._target : default;
        return _instance != null && _instance._hasTarget;
    }

    public static void Show()
    {
        if (_instance == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var host = new GameObject("SpacePlanningUI", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)host.transform);
            _instance = host.AddComponent<SpacePlanningUI>();
            _instance.Build(canvas);
        }
        _instance.Open();
    }

    public static void Hide()
    {
        if (_instance == null) return;
        _instance._panel.gameObject.SetActive(false);
        _instance._backdrop.SetActive(false);
        _instance.SuppressControls(false);
    }

    void OnEnable() { UIThemeController.ThemeChanged += Restyle; }
    void OnDisable() { UIThemeController.ThemeChanged -= Restyle; SuppressControls(false); }
    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_outlineRoot != null) Destroy(_outlineRoot);
        if (_outlineMaterial != null) Destroy(_outlineMaterial);
    }

    void Build(Canvas canvas)
    {
        _canvas = canvas;
        _build = FindFirstObjectByType<BuildController>();
        Transform existing = canvas.transform.Find("PartsPanel");
        if (existing != null)
        {
            var image = existing.GetComponent<Image>();
            if (image != null) { _cardSprite = image.sprite; _cardPpu = image.pixelsPerUnitMultiplier; }
            _font = existing.GetComponentInChildren<TMP_Text>(true)?.font;
        }
        _backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdrop.transform.SetParent(transform, false);
        Stretch((RectTransform)_backdrop.transform);
        _backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
        _backdrop.GetComponent<Button>().onClick.AddListener(Hide);

        _panel = Card("SpaceSizeDialog", transform);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(PanelWidth, 492f);
        Text(_panel, "Title", "What space would you like to fill?", 21f, 24f, -22f, 460f, 34f);
        Text(_panel, "Intro", "Measure the available width, depth and height. Then combine Frames and Panels to make your design.",
            13f, 24f, -67f, 492f, 46f);
        _width = Input("Width", 24f, "1200");
        _depth = Input("Depth", 194f, "450");
        _height = Input("Height", 364f, "1800");
        _message = Text(_panel, "Feedback", "Whole millimetres, including any clearance you want to leave.",
            12f, 24f, -210f, 492f, 45f);
        Text(_panel, "Steps", "1  Frames: pick a base, span, depth and height.\n2  Panels: connect the frame holes to fill a bay.\n3  Check the fit, save your design and prepare a quote.",
            13f, 24f, -266f, 492f, 70f);
        Button(_panel, "Apply", "Apply size", 24f, -352f, 150f, Apply);
        Button(_panel, "Frames", "Build frames", 194f, -352f, 150f, () => UseTool(GuidedTemplateTool.PostsT1));
        Button(_panel, "Panels", "Add panels", 364f, -352f, 150f, () => UseTool(GuidedTemplateTool.PanelBayT3));
        Text(_panel, "Note", "The outline follows your build. Fit compares overall dimensions; allow for access, walls and installation. Size setup stays on this device and is separate from shared design codes.",
            10.5f, 24f, -404f, 492f, 43f);
        Button(_panel, "Close", "Close", 414f, -449f, 100f, Hide, height: 28f);
        Button(_panel, "ClearTarget", "Clear size check", 24f, -449f, 150f, ClearTarget, height: 28f);

        _fitCard = Card("SpaceFitReadout", transform);
        _fitCard.anchorMin = _fitCard.anchorMax = new Vector2(1f, 0f);
        _fitCard.pivot = new Vector2(1f, 0f);
        _fitCard.anchoredPosition = new Vector2(-24f, 106f);
        _fitCard.sizeDelta = new Vector2(354f, 94f);
        _fitText = Text(_fitCard, "Fit", "", 11.5f, 14f, -10f, 326f, 72f);
        _fitCard.gameObject.AddComponent<Button>().onClick.AddListener(Show);
        _fitCard.gameObject.SetActive(false);
        _panel.gameObject.SetActive(false);
        _backdrop.SetActive(false);

        if (PlayerPrefs.HasKey(PreferenceKey))
        {
            try
            {
                var last = JsonUtility.FromJson<SpacePlanningTarget>(PlayerPrefs.GetString(PreferenceKey));
                if (last.IsValid) SetInputs(last);
            }
            catch (System.Exception) { /* Invalid last-input preference is ignored. */ }
        }
        Restyle();
    }

    void Open()
    {
        if (_hasTarget) SetInputs(_target);
        _message.text = "Whole millimetres, including any clearance you want to leave.";
        _message.color = UIThemeController.MutedColor;
        transform.SetAsLastSibling();
        _backdrop.SetActive(true);
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        SuppressControls(true);
        PositionResponsive();
    }

    bool SetTarget()
    {
        if (!SpacePlanningTarget.TryParse(_width.text, _depth.text, _height.text, out var target, out string error))
        {
            _message.text = error;
            _message.color = UIThemeController.DangerColor;
            return false;
        }
        _target = target;
        _hasTarget = true;
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(target));
        PlayerPrefs.Save();
        EnsureOutline();
        _nextMeasure = 0f;
        RefreshFit();
        return true;
    }

    void Apply() { if (SetTarget()) Hide(); }

    void UseTool(GuidedTemplateTool tool)
    {
        if (!SetTarget()) return;
        if (SpaceModeController.Active)
            FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
        UIInteractionState.CurrentExperience = UIInteractionState.Experience.Guided;
        UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;
        var session = FindFirstObjectByType<TemplateSession>();
        if (session == null)
        {
            _message.text = "Template tools are unavailable in this scene. Your size has been applied.";
            _message.color = UIThemeController.DangerColor;
            return;
        }
        session.SetTool(tool);
        Hide();
    }

    void ClearTarget()
    {
        _hasTarget = false;
        _fitCard.gameObject.SetActive(false);
        if (_outlineRoot != null) _outlineRoot.SetActive(false);
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        Hide();
    }

    void SetInputs(SpacePlanningTarget target)
    {
        _width.text = target.WidthMm.ToString(CultureInfo.InvariantCulture);
        _depth.text = target.DepthMm.ToString(CultureInfo.InvariantCulture);
        _height.text = target.HeightMm.ToString(CultureInfo.InvariantCulture);
    }

    void Update()
    {
        if (_panel == null) return;
        if (IsOpen)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) Hide();
            PositionResponsive();
        }
        if (_hasTarget && Time.unscaledTime >= _nextMeasure &&
            (BuildHistory.Instance == null || !BuildHistory.Instance.IsRestoring))
        {
            _nextMeasure = Time.unscaledTime + 0.25f;
            RefreshFit();
        }
    }

    void RefreshFit()
    {
        bool any = StructureBounds.TryCompute(_build, out var bounds);
        _fitCard.gameObject.SetActive(_hasTarget);
        if (!_hasTarget) return;
        bool fits = !any || _target.Fits(bounds.WidthMm, bounds.DepthMm, bounds.HeightMm);
        string brief = $"SPACE  {_target.WidthMm} × {_target.DepthMm} × {_target.HeightMm} mm · W × D × H";
        if (!any)
            _fitText.text = brief + "\nChoose Frames or Panels to begin.\nClick here to edit the space size.";
        else
        {
            Vector3 remaining = _target.RemainingMm(bounds.WidthMm, bounds.DepthMm, bounds.HeightMm);
            _fitText.text = brief + "\n" + (fits ? "Within size" : "Exceeds space") +
                $" · Build {Mathf.CeilToInt(bounds.WidthMm)} × {Mathf.CeilToInt(bounds.DepthMm)} × {Mathf.CeilToInt(bounds.HeightMm)} mm\n" +
                $"W {Margin(remaining.x)} · D {Margin(remaining.z)} · H {Margin(remaining.y)}";
        }
        _fitText.color = fits ? UIThemeController.InkColor : UIThemeController.DangerColor;
        Vector3 centre = any ? bounds.GroundCenter : Vector3.zero;
        DrawOutline(centre, fits);
    }

    static string Margin(float remaining) => remaining < -0.1f
        ? $"{Mathf.CeilToInt(-remaining)} over"
        : $"{Mathf.FloorToInt(Mathf.Max(0f, remaining))} left";

    void EnsureOutline()
    {
        if (_outlineRoot != null) { _outlineRoot.SetActive(true); return; }
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        _outlineMaterial = new Material(shader);
        _outlineRoot = new GameObject("AvailableSpaceOutline");
        _outlineRoot.layer = 2; // Ignore Raycast; no colliders or selectable parts.
        for (int i = 0; i < 12; i++)
        {
            var segment = new GameObject("Edge" + i);
            segment.layer = 2;
            segment.transform.SetParent(_outlineRoot.transform, false);
            var line = segment.AddComponent<LineRenderer>();
            line.sharedMaterial = _outlineMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = NeospaceUnits.Mm(2f);
            _outline.Add(line);
        }
    }

    void DrawOutline(Vector3 groundCentre, bool fits)
    {
        if (_outline.Count != 12) return;
        float w = NeospaceUnits.Mm(_target.WidthMm) * 0.5f;
        float d = NeospaceUnits.Mm(_target.DepthMm) * 0.5f;
        float h = NeospaceUnits.Mm(_target.HeightMm);
        Vector3[] corners =
        {
            new Vector3(-w, 0f, -d), new Vector3(w, 0f, -d), new Vector3(w, 0f, d), new Vector3(-w, 0f, d),
            new Vector3(-w, h, -d), new Vector3(w, h, -d), new Vector3(w, h, d), new Vector3(-w, h, d)
        };
        Color color = fits ? UIThemeController.AccentColor : UIThemeController.DangerColor;
        color.a = 0.45f;
        for (int i = 0; i < 4; i++)
        {
            SetEdge(i, i, (i + 1) % 4);
            SetEdge(i + 4, i + 4, (i + 1) % 4 + 4);
            SetEdge(i + 8, i, i + 4);
        }
        void SetEdge(int edge, int a, int b)
        {
            LineRenderer line = _outline[edge];
            line.SetPosition(0, groundCentre + corners[a]);
            line.SetPosition(1, groundCentre + corners[b]);
            line.startColor = line.endColor = color;
        }
    }

    void SuppressControls(bool suppress)
    {
        if (suppress)
        {
            if (_suppressed.Count > 0) return;
            foreach (Behaviour controller in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (controller == null || !controller.enabled) continue;
                bool workspaceInput = controller is FlyCameraController || controller is CadCameraController ||
                    controller is OrbitCamera || controller is ConfiguratorCameraController ||
                    controller is BuildController || controller is GhostController || controller is FreePartSession ||
                    controller is TemplateSession || controller is SelectionManager || controller is MarqueeSelectionController ||
                    controller is StructureClipboard || controller is BeamResizeSession || controller is PanelLayerMover ||
                    controller is BuildHistory || controller is SpaceModeController || controller is SpaceInteractionController;
                if (!workspaceInput) continue;
                _suppressed.Add(controller);
                controller.enabled = false;
            }
        }
        else
        {
            foreach (Behaviour controller in _suppressed) if (controller != null) controller.enabled = true;
            _suppressed.Clear();
        }
    }

    void PositionResponsive()
    {
        if (_canvas == null) return;
        Rect rect = ((RectTransform)_canvas.transform).rect;
        float scale = Mathf.Min(1f, (rect.width - 24f) / PanelWidth, (rect.height - 24f) / 492f);
        _panel.localScale = Vector3.one * Mathf.Max(0.2f, scale);
    }

    void Restyle()
    {
        if (_panel == null) return;
        foreach (Image image in GetComponentsInChildren<Image>(true))
            if (image.gameObject != _backdrop) image.color = UIThemeController.CardColor;
        foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            text.color = UIThemeController.InkColor;
        foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
        {
            input.GetComponent<Image>().color = UIThemeController.SurfaceColor;
            input.caretColor = UIThemeController.InkColor;
        }
        foreach (Button button in _panel.GetComponentsInChildren<Button>(true))
        {
            bool primary = button.name == "Btn_Frames";
            button.GetComponent<Image>().color = primary ? UIThemeController.AccentColor : UIThemeController.SurfaceColor;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = primary ? Color.white : UIThemeController.InkColor;
        }
        _nextMeasure = 0f;
    }

    RectTransform Card(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = UIThemeController.CardColor;
        if (_cardSprite != null)
        {
            image.sprite = _cardSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = _cardPpu;
        }
        return (RectTransform)go.transform;
    }

    TextMeshProUGUI Text(RectTransform parent, string name, string value, float size,
        float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = size;
        text.color = UIThemeController.InkColor;
        text.text = value;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        Place(text.rectTransform, x, y, width, height);
        return text;
    }

    TMP_InputField Input(string label, float x, string initial)
    {
        Text(_panel, label + "Label", label + " (mm)", 12f, x, -131f, 150f, 20f);
        RectTransform rt = Card(label + "Input", _panel);
        Place(rt, x, -158f, 150f, 42f);
        var input = rt.gameObject.AddComponent<TMP_InputField>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(rt, false);
        var area = (RectTransform)viewport.transform;
        Stretch(area);
        area.offsetMin = new Vector2(10f, 4f);
        area.offsetMax = new Vector2(-10f, -4f);
        var value = Text(area, "Value", "", 18f, 0f, 0f, 0f, 0f);
        Stretch(value.rectTransform);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        input.textComponent = value;
        input.textViewport = area;
        input.targetGraphic = rt.GetComponent<Image>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 5;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = initial;
        input.customCaretColor = true;
        return input;
    }

    void Button(RectTransform parent, string name, string label, float x, float y, float width,
        UnityEngine.Events.UnityAction action, float height = 38f)
    {
        RectTransform rt = Card("Btn_" + name, parent);
        Place(rt, x, y, width, height);
        var button = rt.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        var labelText = Text(rt, "Label", label, 12.5f, 0f, 0f, 0f, 0f);
        Stretch(labelText.rectTransform);
        labelText.alignment = TextAlignmentOptions.Center;
        UiPolish.HoverTint(button);
    }

    static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
