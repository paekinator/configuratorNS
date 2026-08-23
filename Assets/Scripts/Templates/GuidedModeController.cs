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
    public GameObject expertPartsPanel;
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
        HandleExperienceChanged(UIInteractionState.CurrentExperience);
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
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

    public void SelectPostsTool()
    {
        EnsureGuided();
        templateSession?.SetTool(GuidedTemplateTool.PostsT1);
        RefreshHint();
    }

    public void SelectConnectorsTool()
    {
        EnsureGuided();
        templateSession?.SetTool(GuidedTemplateTool.ConnectorsT2);
        RefreshHint();
    }

    public void SelectPanelBayTool()
    {
        EnsureGuided();
        templateSession?.SetTool(GuidedTemplateTool.PanelBayT3);
        RefreshHint();
    }

    void EnsureGuided()
    {
        if (UIInteractionState.CurrentExperience != UIInteractionState.Experience.Guided)
            SetGuided();
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        bool guided = experience == UIInteractionState.Experience.Guided;

        if (expertPartsPanel != null)
            expertPartsPanel.SetActive(!guided);
        if (guidedToolsPanel != null)
            guidedToolsPanel.SetActive(guided);

        if (guided)
        {
            if (buildController != null)
                buildController.SetCurrentPart(null);
            if (panelGhost != null)
                panelGhost.DisablePanelTool();
            if (templateSession != null && templateSession.ActiveTool == GuidedTemplateTool.None)
                templateSession.SetTool(GuidedTemplateTool.PostsT1);
        }
        else
        {
            templateSession?.SoftReset();
        }

        RefreshHint();
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
