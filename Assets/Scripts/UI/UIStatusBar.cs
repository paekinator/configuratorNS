using TMPro;
using UnityEngine;

public class UIStatusBar : MonoBehaviour
{
    public BuildController buildController;
    public TextMeshProUGUI statusText;

    FreePartSession _freeSession;

    void Awake()
    {
        ConfigureTextFit();
    }

    /// <summary>
    /// Long guidance messages must never spill out of the pill: wrap onto a
    /// second line, auto-shrink the font until the text fits the rect, and
    /// ellipsize as a last resort.
    /// </summary>
    void ConfigureTextFit()
    {
        if (statusText == null)
            return;

        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMax = statusText.fontSize;
        statusText.fontSizeMin = 9f;
    }

    string _lastFitText;
    RectTransform _pillRt;

    /// <summary>
    /// Hug the message: a fixed-width pill leaves a long empty box after
    /// short messages. Width follows the text (within limits); longer copy
    /// still wraps, shrinks, and ellipsizes inside the max width.
    /// </summary>
    void LateUpdate()
    {
        if (statusText == null || statusText.text == _lastFitText)
            return;
        _lastFitText = statusText.text;

        if (_pillRt == null)
            _pillRt = transform as RectTransform;
        if (_pillRt == null)
            return;

        // Measure at the full font size: auto-sizing may have shrunk the
        // current size to fit a previous long message.
        float measured = statusText.GetPreferredValues(_lastFitText).x;
        if (statusText.enableAutoSizing && statusText.fontSize > 0.1f)
            measured *= statusText.fontSizeMax / statusText.fontSize;

        // 34 px left inset (accent dot) + 16 px right padding + breathing room.
        float width = Mathf.Clamp(Mathf.Ceil(measured) + 58f, 220f, 540f);
        _pillRt.sizeDelta = new Vector2(width, _pillRt.sizeDelta.y);
    }

    void Update()
    {
        if (statusText == null) return;

        if (buildController == null)
        {
            statusText.text = "No BuildController linked.";
            return;
        }

        // Selection / copy / layer-move guidance always wins while active.
        if (SelectionStatus.TryGet(out string selectionMessage))
        {
            statusText.text = selectionMessage;
            return;
        }

        if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
        {
            var session = FindFirstObjectByType<TemplateSession>();
            if (session == null)
            {
                statusText.text = "Choose a tool from the left panel.";
                return;
            }

            string tool = session.ActiveTool switch
            {
                GuidedTemplateTool.PostsT1 => "Frames",
                GuidedTemplateTool.ConnectorsT2 => "Beams",
                GuidedTemplateTool.PanelBayT3 => "Panels",
                _ => null
            };
            statusText.text = tool == null
                ? session.StatusMessage
                : $"{tool}  |  {session.StatusMessage}";
            return;
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
            statusText.text = toolName == null
                ? _freeSession.StatusMessage
                : $"{toolName}  |  {_freeSession.StatusMessage}";
            return;
        }

        // If not building, show mode hint
        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Build || string.IsNullOrEmpty(buildController.currentPartId))
        {
            statusText.text = "Pick a tool on the left, or click a part to select it";
            return;
        }

        statusText.text = BuildInstruction(buildController);
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
            return "Panel  |  Hover a bay between frames and click to fill it  |  Esc puts the tool away";

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