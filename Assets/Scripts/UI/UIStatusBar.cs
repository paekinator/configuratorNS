using TMPro;
using UnityEngine;

public class UIStatusBar : MonoBehaviour
{
    public BuildController buildController;
    public TextMeshProUGUI statusText;

    FreePartSession _freeSession;
    public Color idleDotColor = new Color32(66, 126, 220, 255);
    public Color activeDotColor = new Color32(234, 143, 62, 255);
    static float _flashUntil;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetFlash() => _flashUntil = 0f;
    UnityEngine.UI.Image _dot;
    TemplateSession _guided;
    SpaceInteractionController _space;
    MoveGizmoController _move;
    public static void FlashAction() => _flashUntil = Time.unscaledTime + .32f;
    void OnEnable() => BuildHistory.Changed += FlashAction;
    void OnDisable() => BuildHistory.Changed -= FlashAction;

    void RefreshDot()
    {
        if (_dot == null) _dot = transform.Find("Dot")?.GetComponent<UnityEngine.UI.Image>();
        if (_dot == null) return;
        if (_guided == null) _guided = FindFirstObjectByType<TemplateSession>();
        if (_freeSession == null) _freeSession = FindFirstObjectByType<FreePartSession>();
        if (_space == null) _space = FindFirstObjectByType<SpaceInteractionController>();
        if (_move == null) _move = FindFirstObjectByType<MoveGizmoController>();
        bool active = ModulePickSession.Armed ||
            (SpaceModeController.Active ? _space != null && _space.HasActiveAction :
            (_guided != null && _guided.isActiveAndEnabled && _guided.ActiveTool != GuidedTemplateTool.None) ||
            (_freeSession != null && _freeSession.isActiveAndEnabled && _freeSession.ActiveKind != FreePartKind.None) ||
            (buildController != null && !string.IsNullOrEmpty(buildController.currentPartId)) ||
            (StructureClipboard.Active != null && StructureClipboard.Active.IsActive) ||
            (BeamResizeSession.Instance != null && BeamResizeSession.Instance.IsActive) ||
            (_move != null && _move.IsDragging) ||
            (FinishController.Instance != null && FinishController.Instance.IsOn) || FinishPaletteUI.IsOpen);
        _dot.color = active || Time.unscaledTime < _flashUntil ? activeDotColor : idleDotColor;
    }

    void Awake()
    {
        ConfigureTextFit();
    }

    // Measure in the shared parent space so both outside gaps are equal.
    // Font size follows the hint, never the length of the current message.
    const float TextInset = 58f;
    const float NeighbourClearance = 20f;
    RectTransform _leftNeighbour, _rightNeighbour;
    TMP_Text _hintText;
    readonly Vector3[] _corners = new Vector3[4];

    void ConfigureTextFit()
    {
        if (statusText == null) return;
        statusText.enableAutoSizing = false;
        statusText.characterSpacing = 0f;
        statusText.textWrappingMode = TextWrappingModes.NoWrap;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
    }

    public void RefreshLayout()
    {
        if (statusText == null || !(transform is RectTransform pill) ||
            !(pill.parent is RectTransform parent)) return;
        if (_leftNeighbour == null)
            _leftNeighbour = UIChrome.ModeSwitch(parent) as RectTransform;
        if (_rightNeighbour == null)
        {
            _rightNeighbour = UIChrome.FindPanel(parent, "HintPill") as RectTransform;
            if (_rightNeighbour != null)
                _hintText = _rightNeighbour.GetComponentInChildren<TMP_Text>(true);
        }

        ConfigureTextFit();
        statusText.fontSize = _hintText != null ? _hintText.fontSize : 12f;
        float left = parent.rect.xMin + UIChrome.DockInset;
        float right = parent.rect.xMax - UIChrome.DockInset;
        if (_leftNeighbour != null)
        {
            _leftNeighbour.GetWorldCorners(_corners);
            left = parent.InverseTransformPoint(_corners[2]).x;
        }
        var hintGroup = _rightNeighbour != null ? _rightNeighbour.GetComponent<CanvasGroup>() : null;
        if (_rightNeighbour != null && _rightNeighbour.gameObject.activeInHierarchy &&
            (hintGroup == null || hintGroup.alpha > 0f))
        {
            _rightNeighbour.GetWorldCorners(_corners);
            right = parent.InverseTransformPoint(_corners[0]).x;
        }
        float available = Mathf.Max(1f, right - left - 2f * NeighbourClearance);
        float wanted = Mathf.Ceil(statusText.GetPreferredValues(statusText.text).x) + TextInset;
        pill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Min(Mathf.Max(160f, wanted), available));
        // Use local position rather than an assumed screen-centre anchor.
        Vector3 position = pill.localPosition;
        position.x = (left + right) * 0.5f + (pill.pivot.x - 0.5f) * pill.rect.width;
        pill.localPosition = position;
    }

    void LateUpdate() { RefreshLayout(); RefreshDot(); }

    void Update()
    {
        if (statusText == null)
            return;

        // The pill is the only place this message appears. The dock footer
        // briefly mirrored it, which put the same sentence on screen twice;
        // the footer carries the active page's standing description instead.
        statusText.text = Compose();
    }

    /// <summary>
    /// The one message, composed once. Previously each branch wrote straight
    /// to statusText and returned; it returns the string instead so the pill
    /// and the dock footer cannot drift apart.
    /// </summary>
    string Compose()
    {
        if (buildController == null)
            return "No BuildController linked.";

        // Selection / copy / layer-move guidance always wins while active.
        if (SelectionStatus.TryGet(out string selectionMessage))
            return selectionMessage;

        if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
        {
            var session = FindFirstObjectByType<TemplateSession>();
            if (session == null)
                return "Choose a tool from the dock.";

            string tool = session.ActiveTool switch
            {
                GuidedTemplateTool.PostsT1 => "Frames",
                GuidedTemplateTool.ConnectorsT2 => "Beams",
                GuidedTemplateTool.PanelBayT3 => "Panels",
                _ => null
            };
            return tool == null
                ? session.StatusMessage
                : $"{tool}  |  {session.StatusMessage}";
        }

        // Category part tools (Upright / Crossbar / Twist bar) speak for themselves.
        if (_freeSession == null)
            _freeSession = FindFirstObjectByType<FreePartSession>();
        if (_freeSession != null && _freeSession.ActiveKind != FreePartKind.None)
        {
            string toolName = _freeSession.ActiveKind switch
            {
                FreePartKind.Vertical => "Vertical frame",
                FreePartKind.Horizontal => "Horizontal beam",
                FreePartKind.Twist => "Twist beam",
                _ => null
            };
            return toolName == null
                ? _freeSession.StatusMessage
                : $"{toolName}  |  {_freeSession.StatusMessage}";
        }

        // Idle. "on the left" was true of the old vertical panel; the tools
        // are in the dock now, and this same line is shown inside it.
        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Build ||
            string.IsNullOrEmpty(buildController.currentPartId))
            return "Pick a tool, or click a part to select it";

        return BuildInstruction(buildController);
    }

    /// <summary>
    /// Human instructions for the armed Expert part — never raw placement
    /// debug text. Speaks the marker language: ring = free hole,
    /// dot = free peg (the nearest one glows in the accent colour).
    /// </summary>
    static string BuildInstruction(BuildController build)
    {
        string id = build.currentPartId;
        build.GetGhostStatus(out bool hasPose, out bool isValid, out _);

        if (string.Equals(id, "PANEL", System.StringComparison.OrdinalIgnoreCase))
            return "Panel  |  Aim at a bay, or at a corner ring on a frame  |  Esc puts the tool away";

        if (BeamPartUtility.IsVertical(id))
        {
            if (!hasPose)
                return $"{id}  |  Aim at open ground, or at a dot marker (a free peg) to stack on";
            return isValid
                ? $"{id}  |  Click to place the frame  |  Above the peg = standing, below = hanging"
                : $"{id}  |  This spot is blocked · try another peg dot or clear ground";
        }

        if (BeamPartUtility.IsHorizontalLike(id))
        {
            if (!hasPose)
                return $"{id}  |  Aim at open ground, or at a ring marker (a free hole) on a frame";
            return isValid
                ? $"{id}  |  Click to place the beam"
                : $"{id}  |  This spot is blocked · aim at a ring at the height you want";
        }

        return $"{id}  |  Click to place  |  Esc puts the tool away";
    }
}
