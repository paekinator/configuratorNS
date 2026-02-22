using UnityEngine;
using UnityEngine.UI;

public class UIToolbarController : MonoBehaviour
{
    [Header("Refs")]
    public BuildController buildController;

    [Header("Mode Buttons")]
    public Button btnBuild;
    public Button btnSelect;

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

    public Sprite activeSprite;
    public Sprite inactiveSprite;

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
    }

    void OnDisable()
    {
        UIInteractionState.OnModeChanged -= HandleModeChanged;
        UIInteractionState.OnTabChanged -= HandleTabChanged;
    }

    void Start()
    {
        if (btnBuild) btnBuild.onClick.AddListener(() => UIInteractionState.CurrentMode = UIInteractionState.Mode.Build);
        if (btnSelect) btnSelect.onClick.AddListener(() => UIInteractionState.CurrentMode = UIInteractionState.Mode.Select);

        if (btnVertical) btnVertical.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Vertical);
        if (btnHorizontal) btnHorizontal.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Horizontal);
        if (btnTwist) btnTwist.onClick.AddListener(() => UIInteractionState.CurrentTab = UIInteractionState.Tab.Twist); // ✅ NEW

        RefreshHighlights();
    }

    void HandleModeChanged(UIInteractionState.Mode mode)
    {
        // If not Build mode, clear current part so frame-ghost hides
        if (buildController != null)
        {
            if (mode != UIInteractionState.Mode.Build)
                buildController.SetCurrentPart(null);
        }

        RefreshHighlights();
    }

    void HandleTabChanged(UIInteractionState.Tab tab)
    {
        RefreshHighlights();
    }

    void RefreshHighlights()
    {
        SetImg(buildBg, UIInteractionState.CurrentMode == UIInteractionState.Mode.Build);
        SetImg(selectBg, UIInteractionState.CurrentMode == UIInteractionState.Mode.Select);

        SetImg(verticalBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Vertical);
        SetImg(horizontalBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Horizontal);
        SetImg(twistBg, UIInteractionState.CurrentTab == UIInteractionState.Tab.Twist); // ✅ NEW
    }

    void SetImg(Image img, bool active)
    {
        if (img == null) return;

        if (activeSprite != null && inactiveSprite != null)
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