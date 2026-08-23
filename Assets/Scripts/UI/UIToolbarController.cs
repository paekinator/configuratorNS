using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIToolbarController : MonoBehaviour
{
    [Header("Refs")]
    public BuildController buildController;

    [Header("Mode Buttons")]
    public Button btnBuild;
    public Button btnSelect;

    [Header("Experience (Expert / Guided)")]
    public Button btnGuidedToggle;
    public Image guidedToggleBg;
    public GuidedModeController guidedModeController;

    [Header("Tab Buttons")]
    public Button btnVertical;
    public Button btnHorizontal;
    public Button btnTwist;          // ✅ NEW

    [Header("Optional: Visual")]
    public Image buildBg;
    public Image selectBg;
    public Image verticalBg;
    public Image horizontalBg;
    public Image twistBg;            // ✅ NEW
    public Image expertBg;
    public Image guidedBg;

    public Sprite activeSprite;
    public Sprite inactiveSprite;

    [Header("Optional: Color Highlight (overrides sprite/alpha)")]
    public bool useColorHighlight = false;
    public Color activeBgColor = Color.black;
    public Color inactiveBgColor = Color.clear;
    public Color activeTextColor = Color.white;
    public Color inactiveTextColor = Color.gray;

    [Header("Startup")]
    public bool resetStateOnAwake = true;

    void Awake()
    {
        if (resetStateOnAwake)
            UIInteractionState.ResetDefaults();
    }

    void OnEnable()
    {
        UIInteractionState.OnModeChanged += HandleModeChanged;
        UIInteractionState.OnTabChanged += HandleTabChanged;
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
    }

    void OnDisable()
    {
        UIInteractionState.OnModeChanged -= HandleModeChanged;
        UIInteractionState.OnTabChanged -= HandleTabChanged;
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
    }

    void Start()
    {
        if (btnBuild) btnBuild.onClick.AddListener(() => UIInteractionState.CurrentMode = UIInteractionState.Mode.Build);
        if (btnSelect) btnSelect.onClick.AddListener(() => UIInteractionState.CurrentMode = UIInteractionState.Mode.Select);

        if (btnVertical) btnVertical.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Vertical);
        if (btnHorizontal) btnHorizontal.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Horizontal);
        if (btnTwist) btnTwist.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Twist); // ✅ NEW

        if (btnGuidedToggle)
        {
            btnGuidedToggle.onClick.AddListener(() =>
            {
                if (guidedModeController != null)
                    guidedModeController.ToggleExperience();
                else
                {
                    UIInteractionState.CurrentExperience =
                        UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided
                            ? UIInteractionState.Experience.Expert
                            : UIInteractionState.Experience.Guided;
                }
            });
        }

        RefreshHighlights();
    }

    void HandleModeChanged(UIInteractionState.Mode mode)
    {
        // If not Build mode, clear current part so frame-ghost hides
        if (buildController != null)
        {
            if (mode != UIInteractionState.Mode.Build ||
                UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
                buildController.SetCurrentPart(null);
        }

        RefreshHighlights();
    }

    void HandleTabChanged(UIInteractionState.Tab tab)
    {
        RefreshHighlights();
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        if (experience == UIInteractionState.Experience.Guided && buildController != null)
            buildController.SetCurrentPart(null);

        RefreshHighlights();
    }

    public void RefreshHighlights()
    {
        SetImg(buildBg, UIInteractionState.CurrentMode == UIInteractionState.Mode.Build);
        SetImg(selectBg, UIInteractionState.CurrentMode == UIInteractionState.Mode.Select);

        SetImg(verticalBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Vertical);
        SetImg(horizontalBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Horizontal);
        SetImg(twistBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Twist); // ✅ NEW

        bool guided = UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided;
        SetImg(guidedBg, guided);
        SetImg(expertBg, !guided);
        // Note: guidedToggleBg is intentionally NOT run through SetImg — the toolbar's
        // inactive style is transparent, which would make the toggle invisible in Expert mode.

        if (btnGuidedToggle != null)
        {
            var tmp = btnGuidedToggle.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.text = guided ? "Mode: Guided" : "Mode: Expert";
        }
    }

    void SetImg(Image img, bool active)
    {
        if (img == null) return;

        if (useColorHighlight)
        {
            img.color = active ? activeBgColor : inactiveBgColor;

            var tmp = img.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.color = active ? activeTextColor : inactiveTextColor;
        }
        else if (activeSprite != null && inactiveSprite != null)
        {
            img.sprite = active ? activeSprite : inactiveSprite;
            img.color = Color.white;
        }
        else
        {
            var c = img.color;
            c.a = active ? 0.95f : 0.55f;
            img.color = c;
        }
    }
}