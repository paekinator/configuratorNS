using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Tools | Parts" tab row at the top of the left panel. Clicking a tab
/// switches the experience (guided tools vs. piece-by-piece parts); the
/// visual state follows <see cref="UIInteractionState"/> so every instance
/// stays in sync no matter which panel is currently visible.
/// (Internal object/field names keep the historical "templates" wording so
/// existing scenes stay wired.)
/// </summary>
public class ExperienceTabs : MonoBehaviour
{
    public Button templatesButton;
    public Button partsButton;
    public Image templatesBg;
    public Image partsBg;
    public TextMeshProUGUI templatesLabel;
    public TextMeshProUGUI partsLabel;
    public Image templatesIcon;
    public Image partsIcon;

    [Header("Colors (legacy: tab colors now come from the theme)")]
    public Color activeBg = new Color(0.149f, 0.133f, 0.118f, 1f);   // ink pill
    public Color inactiveBg = Color.clear;                            // sits on an inset track
    public Color activeText = Color.white;
    public Color inactiveText = new Color(0.561f, 0.533f, 0.502f, 1f);

    void Awake()
    {
        if (templatesButton != null)
            templatesButton.onClick.AddListener(SelectTemplates);
        if (partsButton != null)
            partsButton.onClick.AddListener(SelectParts);
    }

    void OnEnable()
    {
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
        UIThemeController.ThemeChanged += RefreshVisuals;
        HandleExperienceChanged(UIInteractionState.CurrentExperience);
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
        UIThemeController.ThemeChanged -= RefreshVisuals;
    }

    public void SelectTemplates()
    {
        UIInteractionState.CurrentExperience = UIInteractionState.Experience.Guided;
    }

    public void SelectParts()
    {
        UIInteractionState.CurrentExperience = UIInteractionState.Experience.Expert;
    }

    /// <summary>Re-apply active/inactive colors (e.g. after icons were injected).</summary>
    public void RefreshVisuals()
    {
        HandleExperienceChanged(UIInteractionState.CurrentExperience);
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        bool tools = experience == UIInteractionState.Experience.Guided;

        // Same selected-tab language as the top toolbar, taken live from the
        // theme: an ink pill with card-tone text. The old hardcoded light-mode
        // ink stayed near-black in dark mode and vanished into the dark track.
        Color pillBg = UIThemeController.InkColor;
        Color pillText = UIThemeController.CardColor;
        Color idleText = UIThemeController.MutedColor;

        // The inset track behind the tabs: runtime-injected rows are not in
        // the theme's surface list, so keep it on the surface token here.
        if (TryGetComponent(out Image track))
            track.color = UIThemeController.SurfaceColor;

        if (templatesBg != null)
            templatesBg.color = tools ? pillBg : Color.clear;
        if (partsBg != null)
            partsBg.color = tools ? Color.clear : pillBg;
        if (templatesLabel != null)
            templatesLabel.color = tools ? pillText : idleText;
        if (partsLabel != null)
            partsLabel.color = tools ? idleText : pillText;
        if (templatesIcon != null)
            templatesIcon.color = tools ? pillText : idleText;
        if (partsIcon != null)
            partsIcon.color = tools ? idleText : pillText;
    }

    /// <summary>
    /// Style a tab button as "icon + label", updating the label text and
    /// injecting the icon in front of it (idempotent — safe to run on tabs
    /// that were already styled). Returns the icon Image, or null when no
    /// icon sprite was supplied.
    /// </summary>
    public static Image ApplyTabStyle(Button button, string label, Sprite icon)
    {
        if (button == null)
            return null;

        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = label;

        if (icon == null)
            return null;

        var group = button.GetComponent<HorizontalLayoutGroup>();
        if (group == null)
            group = button.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 7f;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        Transform existing = button.transform.Find("Icon");
        Image img;
        if (existing != null)
        {
            img = existing.GetComponent<Image>();
        }
        else
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(button.transform, false);
            go.transform.SetAsFirstSibling();
            img = go.GetComponent<Image>();
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 16f;
            element.preferredHeight = 16f;
        }

        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }
}
