using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPartsPalette : MonoBehaviour
{
    [Header("Refs")]
    public BuildController buildController;

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
        UIInteractionState.OnTabChanged += HandleTabChanged;
        UIInteractionState.OnModeChanged += HandleModeChanged;
    }

    void OnDisable()
    {
        UIInteractionState.OnTabChanged -= HandleTabChanged;
        UIInteractionState.OnModeChanged -= HandleModeChanged;
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

    void HandleTabChanged(UIInteractionState.Tab tab)
    {
        Rebuild();
        HighlightSelected();
    }

    void HandleModeChanged(UIInteractionState.Mode mode)
    {
        HighlightSelected();
    }

    void Rebuild()
    {
        if (gridParent == null || partButtonPrefab == null)
            return;

        // Hard clear visible children
        for (int i = gridParent.childCount - 1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        _spawned.Clear();

        List<string> list = GetListForCurrentTab();

        foreach (var id in list)
        {
            var btn = Instantiate(partButtonPrefab, gridParent);
            btn.gameObject.SetActive(true);
            btn.name = $"Btn_{id}";

            // Text setup
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = id;

                if (buttonFont != null)
                    tmp.font = buttonFont;

                if (overrideFontSize && buttonFontSize > 0f)
                    tmp.fontSize = buttonFontSize;

                tmp.color = normalTextColor;
            }

            btn.onClick.RemoveAllListeners();
            string capturedId = id;
            btn.onClick.AddListener(() =>
            {
                UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;

                if (buildController != null)
                    buildController.SetCurrentPart(capturedId);

                HighlightSelected();
            });

            _spawned.Add(btn);
        }
    }

    List<string> GetListForCurrentTab()
    {
        switch (UIInteractionState.CurrentTab)
        {
            case UIInteractionState.Tab.Vertical:
                return verticalParts;
            case UIInteractionState.Tab.Horizontal:
                return horizontalParts;
            case UIInteractionState.Tab.Twist:
                return twistParts;
            default:
                return verticalParts;
        }
    }

    void HighlightSelected()
    {
        if (buildController == null) return;

        for (int i = 0; i < _spawned.Count; i++)
        {
            var b = _spawned[i];
            if (b == null) continue;

            bool active = (UIInteractionState.CurrentMode == UIInteractionState.Mode.Build &&
                           b.name == $"Btn_{buildController.currentPartId}");

            // Background alpha
            var img = b.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = active ? selectedAlpha : unselectedAlpha;
                img.color = c;
            }

            // Text color
            var tmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.color = active ? selectedTextColor : normalTextColor;
            }
        }
    }
}