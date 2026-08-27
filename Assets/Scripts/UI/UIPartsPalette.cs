using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPartsPalette : MonoBehaviour
{
    [Header("Refs")]
    public BuildController buildController;
    public PanelGhostController panelGhost;

    [Header("UI")]
    public Transform gridParent;         // Content_Grid
    public Button partButtonPrefab;      // disabled template button

    [Header("Text Styling")]
    public TMP_FontAsset buttonFont;
    public float buttonFontSize = 24f;
    public bool overrideFontSize = false;

    public Color normalTextColor = Color.white;
    public Color selectedTextColor = Color.black;

    [Header("Button Background Alpha")]
    public float selectedAlpha = 0.95f;
    public float unselectedAlpha = 0.60f;

    [Header("Button Background Colors (optional, overrides alpha)")]
    [Tooltip("When enabled, selection swaps the full background color instead of only alpha.")]
    public bool useBgColors = false;
    public Color selectedBgColor = Color.white;
    public Color normalBgColor = Color.white;

    [Header("Thumbnails (optional)")]
    [Tooltip("Sprites are loaded from Resources at this path + part id, e.g. PartThumbs/V5.")]
    public string thumbnailResourceFolder = "PartThumbs";

    [Header("Parts")]
    public List<string> verticalParts = new List<string>
    {
        "V1","V3","V5","V9","V13","V15","V17","V25"
    };

    public List<string> horizontalParts = new List<string>
    {
        "H1","H3","H5","H7","H11","H15","H19","H23"
    };

    // Twist tab list
    public List<string> twistParts = new List<string>
    {
        "T1","T3","T5","T7","T11","T15","T19","T23"
    };

    private readonly List<Button> _spawned = new List<Button>();

    void OnEnable()
    {
        UIInteractionState.OnModeChanged += HandleModeChanged;
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
        UIThemeController.ThemeChanged += HighlightSelected;
    }

    void OnDisable()
    {
        UIInteractionState.OnModeChanged -= HandleModeChanged;
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
        UIThemeController.ThemeChanged -= HighlightSelected;
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        if (experience == UIInteractionState.Experience.Expert)
            Rebuild();
        HighlightSelected();
    }

    void Start()
    {
        Rebuild();
        HighlightSelected();
    }

    void Update()
    {
        // Highlight can change when BuildController currentPartId changes
        HighlightSelected();
    }

    void HandleModeChanged(UIInteractionState.Mode mode)
    {
        HighlightSelected();
    }

    FreePartSession _session;

    FreePartSession Session
    {
        get
        {
            if (_session == null)
                _session = FindFirstObjectByType<FreePartSession>();
            return _session;
        }
    }

    /// <summary>
    /// One button per part TYPE — nobody browsing the palette needs to know
    /// what a "V13" is. The size is chosen on a scale while placing.
    /// </summary>
    void Rebuild()
    {
        if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
            return;

        if (gridParent == null || partButtonPrefab == null)
            return;

        // Hard clear visible children
        for (int i = gridParent.childCount - 1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        _spawned.Clear();

        // Three wide rows read better than a cramped 2-column grid of cards.
        if (gridParent.TryGetComponent(out GridLayoutGroup grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(304f, 96f);
        }

        AddCategoryButton(FreePartKind.Vertical, "Vertical frame",
            "Stands anywhere on the grid", "UI/FrameToolIcon");
        AddCategoryButton(FreePartKind.Horizontal, "Horizontal beam",
            "Starts at a blue ring on a frame", "UI/BeamToolIcon");
        AddCategoryButton(FreePartKind.Twist, "Twist beam",
            "A beam with rotated ends", "UI/TwistToolIcon");
        AddPanelButton();
        AddFinishButton();
        AddPaletteButton();
    }

    /// <summary>
    /// Opens the colour palette popover: themes pairing a panel colour with
    /// a veneer/cap colour, plus individual swatch rows. One palette governs
    /// the whole space.
    /// </summary>
    void AddPaletteButton()
    {
        Button btn = MakeCard("Btn_Palette", "Palette",
            "Colour themes for panels and veneers", "UI/PaletteToolIcon");

        btn.onClick.AddListener(() =>
        {
            FinishPaletteUI.Toggle();
            HighlightSelected();
        });

        _spawned.Add(btn);
    }

    /// <summary>
    /// "Finish" is the outer skin of the build (known internally as veneer) —
    /// one click dresses every frame in veneers and caps, and the dressing
    /// follows the build automatically until toggled off.
    /// </summary>
    void AddFinishButton()
    {
        Button btn = MakeCard("Btn_Finish", "Finish",
            "Dresses the frames in veneers and caps", "UI/FinishToolIcon");

        btn.onClick.AddListener(() =>
        {
            FinishController.Ensure().Toggle();
            HighlightSelected();
        });

        _spawned.Add(btn);
    }

    /// <summary>The Panel tool lives in the same card list as the beam types.</summary>
    void AddPanelButton()
    {
        Button btn = MakeCard("Btn_Panel", "Panel",
            "Fills a bay between frames", "UI/PanelToolIcon");

        btn.onClick.AddListener(() =>
        {
            UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;

            if (PanelGhost == null)
                return;

            bool armed = buildController != null &&
                         string.Equals(buildController.currentPartId, "PANEL",
                             System.StringComparison.OrdinalIgnoreCase);
            if (armed)
                PanelGhost.DisablePanelTool();
            else
                PanelGhost.EnablePanelTool();

            HighlightSelected();
        });

        _spawned.Add(btn);
    }

    PanelGhostController PanelGhost
    {
        get
        {
            if (panelGhost == null)
                panelGhost = FindFirstObjectByType<PanelGhostController>();
            return panelGhost;
        }
    }

    void AddCategoryButton(FreePartKind kind, string title, string caption, string iconResource)
    {
        Button btn = MakeCard($"Btn_{kind}", title, caption, iconResource);

        FreePartKind captured = kind;
        btn.onClick.AddListener(() =>
        {
            UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;

            if (Session != null)
            {
                // Clicking the armed tool again puts it away.
                Session.SetKind(Session.ActiveKind == captured ? FreePartKind.None : captured);
            }

            HighlightSelected();
        });

        _spawned.Add(btn);
    }

    Button MakeCard(string name, string title, string caption, string iconResource)
    {
        var btn = Instantiate(partButtonPrefab, gridParent);
        btn.gameObject.SetActive(true);
        btn.name = name;

        // Restyle the baked size-card layout into a wide row:
        // small icon top-left, title beside it, caption wrapping below.
        var tmp = FindLabel(btn.transform);
        if (tmp != null)
        {
            tmp.text = title;
            if (buttonFont != null)
                tmp.font = buttonFont;
            tmp.fontSize = 16f;
            tmp.color = normalTextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(52f, -10f);
            rt.sizeDelta = new Vector2(-64f, 28f);
        }

        Transform subChild = btn.transform.Find("Sub");
        if (subChild != null && subChild.TryGetComponent(out TextMeshProUGUI sub))
        {
            sub.text = caption;
            sub.fontSize = 11.5f;
            sub.textWrappingMode = TextWrappingModes.Normal;
            sub.overflowMode = TextOverflowModes.Ellipsis;
            sub.alignment = TextAlignmentOptions.TopLeft;

            var rt = sub.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(14f, 8f);
            rt.offsetMax = new Vector2(-12f, -44f);
        }

        Transform thumbChild = btn.transform.Find("Thumb");
        if (thumbChild != null && thumbChild.TryGetComponent(out Image thumb))
        {
            Sprite sprite = Resources.Load<Sprite>(iconResource);
            // Icons that were never baked as assets are drawn at runtime, so
            // every card carries a glyph.
            if (sprite == null && iconResource == "UI/PaletteToolIcon")
                sprite = UIIcons.Get("Palette");
            thumb.sprite = sprite;
            thumbChild.gameObject.SetActive(sprite != null);

            var rt = thumb.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f, -11f);
            rt.sizeDelta = new Vector2(26f, 26f);
        }

        btn.onClick.RemoveAllListeners();
        return btn;
    }

    public void RefreshHighlights() => HighlightSelected();

    void HighlightSelected()
    {
        FreePartKind armed = Session != null ? Session.ActiveKind : FreePartKind.None;
        bool panelArmed = buildController != null &&
                          string.Equals(buildController.currentPartId, "PANEL",
                              System.StringComparison.OrdinalIgnoreCase);
        bool finishOn = FinishController.Instance != null && FinishController.Instance.IsOn;

        for (int i = 0; i < _spawned.Count; i++)
        {
            var b = _spawned[i];
            if (b == null) continue;

            bool active = (armed != FreePartKind.None && b.name == $"Btn_{armed}") ||
                          (panelArmed && b.name == "Btn_Panel") ||
                          (finishOn && b.name == "Btn_Finish") ||
                          (FinishPaletteUI.IsOpen && b.name == "Btn_Palette");

            var img = b.GetComponent<Image>();
            if (img != null)
            {
                if (useBgColors)
                {
                    img.color = active ? selectedBgColor : normalBgColor;
                }
                else
                {
                    var c = img.color;
                    c.a = active ? selectedAlpha : unselectedAlpha;
                    img.color = c;
                }
            }

            var tmp = FindLabel(b.transform);
            if (tmp != null)
                tmp.color = active ? selectedTextColor : normalTextColor;

            Transform subChild = b.transform.Find("Sub");
            if (subChild != null && subChild.TryGetComponent(out TextMeshProUGUI sub))
            {
                Color c = active ? selectedTextColor : normalTextColor;
                c.a = 0.65f;
                sub.color = c;
            }

            Transform thumbChild = b.transform.Find("Thumb");
            if (thumbChild != null && thumbChild.TryGetComponent(out Image thumb))
                thumb.color = active ? selectedTextColor : normalTextColor;
        }
    }

    static TextMeshProUGUI FindLabel(Transform buttonRoot)
    {
        Transform label = buttonRoot.Find("Label");
        if (label != null && label.TryGetComponent(out TextMeshProUGUI tmp))
            return tmp;
        return buttonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
    }

}