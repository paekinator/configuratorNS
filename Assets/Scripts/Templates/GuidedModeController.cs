using UnityEngine;

/// <summary>
/// Owns Expert ↔ Guided experience switching. Expert keeps the existing Build/Select
/// palette flow; Guided shows template tools and disables piece-by-piece part selection.
/// </summary>
public class GuidedModeController : MonoBehaviour
{
    [Header("Refs")]
    public BuildController buildController;
    public TemplateSession templateSession;
    public PanelGhostController panelGhost;
    /// <summary>
    /// Kept only so RefreshToolHighlight can find the two tool buttons under
    /// it. This does NOT control whether the panel is shown — DockTabs does,
    /// and the PartsPanel reference that sat beside this is gone entirely.
    /// </summary>
    public GameObject guidedToolsPanel;

    [Header("Optional status")]
    public TMPro.TextMeshProUGUI guidedHintText;

    void Awake()
    {
        if (buildController == null)
            buildController = FindFirstObjectByType<BuildController>();
        if (templateSession == null)
            templateSession = GetComponent<TemplateSession>() ??
                              FindFirstObjectByType<TemplateSession>();
        if (panelGhost == null)
            panelGhost = FindFirstObjectByType<PanelGhostController>();
    }

    void OnEnable()
    {
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
        UIThemeController.ThemeChanged += RefreshToolHighlight;
        HandleExperienceChanged(UIInteractionState.CurrentExperience);
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
        UIThemeController.ThemeChanged -= RefreshToolHighlight;
    }

    public void SetExpert()
    {
        UIInteractionState.CurrentExperience = UIInteractionState.Experience.Expert;
    }

    public void SetGuided()
    {
        UIInteractionState.CurrentExperience = UIInteractionState.Experience.Guided;
    }

    public void ToggleExperience()
    {
        UIInteractionState.CurrentExperience =
            UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided
                ? UIInteractionState.Experience.Expert
                : UIInteractionState.Experience.Guided;
    }

    public void SelectPostsTool() => ToggleTool(GuidedTemplateTool.PostsT1);

    public void SelectConnectorsTool() => ToggleTool(GuidedTemplateTool.ConnectorsT2);

    public void SelectPanelBayTool() => ToggleTool(GuidedTemplateTool.PanelBayT3);

    /// <summary>
    /// Same as the Parts-tab beam cards: first click arms the tool, clicking
    /// the armed tool again puts it down.
    /// </summary>
    void ToggleTool(GuidedTemplateTool tool)
    {
        EnsureGuided();
        if (templateSession == null)
            return;

        bool putAway = templateSession.ActiveTool == tool;
        ActiveInteraction.Exit();
        if (!putAway)
            templateSession.SetTool(tool);
        RefreshHint();
        RefreshToolHighlight();
    }

    void EnsureGuided()
    {
        if (UIInteractionState.CurrentExperience != UIInteractionState.Experience.Guided)
            SetGuided();
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        bool guided = experience == UIInteractionState.Experience.Guided;

        // It used to show and hide the two panels here. DockTabs owns that
        // now — it listens to this same event, and it alone knows whether the
        // Build tab is even the one on screen.
        //
        // Two owners, and this one was blind to the tab: the call ran from
        // OnEnable, so anything that disabled and re-enabled this controller
        // put the Tools panel back on screen over whatever tab you were
        // looking at. The block picker sleeps these controllers while it is
        // armed and wakes them afterwards, which is exactly that — and the
        // Tools cards reappeared on top of the Blocks gallery.
        //
        // The panel references stay: RefreshToolHighlight finds the two tool
        // buttons through guidedToolsPanel.
        if (guided)
        {
            if (buildController != null)
                buildController.SetCurrentPart(null);
            if (panelGhost != null)
                panelGhost.DisablePanelTool();
        }
        else
        {
            templateSession?.SoftReset();
        }

        RefreshHint();
        RefreshToolHighlight();
    }

    void Update()
    {
        if (UIInteractionState.CurrentExperience != UIInteractionState.Experience.Guided)
            return;
        RefreshHint();
        RefreshToolHighlight();
    }

    bool _hintFitConfigured;

    void RefreshHint()
    {
        if (guidedHintText == null || templateSession == null)
            return;

        // Guidance copy varies a lot in length; it must never spill past the
        // hint box. Wrap, auto-shrink, then ellipsize as a last resort.
        if (!_hintFitConfigured)
        {
            _hintFitConfigured = true;
            guidedHintText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            guidedHintText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            guidedHintText.enableAutoSizing = true;
            guidedHintText.fontSizeMax = guidedHintText.fontSize;
            guidedHintText.fontSizeMin = 9.5f;
        }

        guidedHintText.text = templateSession.StatusMessage;
    }

    // ------------------------------------------------------------------
    // Armed-tool highlight: the active tool's card fills with the accent
    // color so picking up / putting down a tool (Esc) is always visible.
    // ------------------------------------------------------------------

    UIThemeController _highlightTheme;
    Transform _t1Button, _t3Button;
    bool _highlightLookupDone;

    void RefreshToolHighlight()
    {
        if (!_highlightLookupDone && guidedToolsPanel != null)
        {
            _highlightLookupDone = true;
            _t1Button = guidedToolsPanel.transform.Find("Btn_T1_Posts");
            _t3Button = guidedToolsPanel.transform.Find("Btn_T3_PanelBay");
            _highlightTheme = FindFirstObjectByType<UIThemeController>();
        }

        GuidedTemplateTool active = templateSession != null
            ? templateSession.ActiveTool
            : GuidedTemplateTool.None;

        Paint(_t1Button, active == GuidedTemplateTool.PostsT1);
        Paint(_t3Button, active == GuidedTemplateTool.PanelBayT3);
    }

    void Paint(Transform button, bool selected)
    {
        if (button == null)
            return;

        var palette = _highlightTheme != null
            ? (_highlightTheme.IsDark ? _highlightTheme.dark : _highlightTheme.light)
            : null;
        Color accent = palette != null ? palette.accent : new Color(0.851f, 0.424f, 0.278f);
        Color surface = palette != null ? palette.surface : new Color(0.953f, 0.937f, 0.914f);
        Color ink = palette != null ? palette.ink : new Color(0.149f, 0.133f, 0.118f);
        Color muted = palette != null ? palette.muted : new Color(0.561f, 0.533f, 0.502f);

        if (button.TryGetComponent(out UnityEngine.UI.Image bg))
            bg.color = selected ? accent : surface;

        Transform label = button.Find("Label");
        UIToolCardOutline.Apply(button, selected);
        if (label != null && label.TryGetComponent(out TMPro.TextMeshProUGUI title))
            title.color = selected ? Color.white : ink;

        Transform caption = button.Find("Caption");
        if (caption != null && caption.TryGetComponent(out TMPro.TextMeshProUGUI sub))
            sub.color = selected ? new Color(1f, 1f, 1f, 0.8f) : muted;

        Transform icon = button.Find("Icon");
        if (icon != null && icon.TryGetComponent(out UnityEngine.UI.Image iconImg))
            iconImg.color = selected ? Color.white : ink;
    }
}
