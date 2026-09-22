using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Add to each dock sidebar to share the same hierarchy and spacing.</summary>
public sealed class UIDockColumnStyle : MonoBehaviour
{
    public TMP_FontAsset regularFont;
    public TMP_FontAsset emphasisFont;
    [Min(8)] public float headingSize = 12f;
    [Min(8)] public float groupSize = 11.5f;
    [Min(8)] public float rowSize = 11f;
    [Min(24)] public float rowHeight = 38f;
    public float rowGap = 4f;
    public float sideInset = 16f;

    void OnEnable() => Apply();

    [ContextMenu("Apply column hierarchy")]
    public void Apply()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            string parentName = text.transform.parent.name;
            if (text.transform.parent == transform &&
                (text.name == "Label" || text.name == "ColumnHeading" || text.name == "Title"))
            {
                text.fontSize = headingSize;
                text.fontStyle = FontStyles.Bold;
                text.fontWeight = FontWeight.Bold;
                if (emphasisFont != null) text.font = emphasisFont;
                text.characterSpacing = 8f;
                text.color = UIChrome.DockDimText;
                var rt = text.rectTransform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(sideInset + 12f, -20f);
                rt.sizeDelta = new Vector2(-2f * (sideInset + 12f), 18f);
            }
            else if (parentName.StartsWith("CollectionHeader") && text.name == "Label")
            {
                text.fontSize = groupSize;
                text.fontStyle = FontStyles.Normal;
                text.fontWeight = FontWeight.SemiBold;
                if (emphasisFont != null) text.font = emphasisFont;
                text.characterSpacing = 6f;
                text.color = UIChrome.DockDimText;
            }
            else if (text.name == "Label" && (parentName.StartsWith("Btn_Build_") ||
                     parentName.StartsWith("CollectionRow") || parentName.StartsWith("Collection_")))
            {
                bool group = parentName.StartsWith("Btn_Build_");
                text.fontSize = group ? groupSize : rowSize;
                text.fontStyle = FontStyles.Normal;
                text.fontWeight = group ? FontWeight.SemiBold : FontWeight.Regular;
                if (group && emphasisFont != null) text.font = emphasisFont;
                else if (regularFont != null) text.font = regularFont;
                text.characterSpacing = 0;
                text.color = UIChrome.DockText;
                text.rectTransform.offsetMin = new Vector2(12f, 0);
            }
            else if (text.name == "Count")
            {
                text.fontSize = rowSize;
                text.fontStyle = FontStyles.Normal;
                text.fontWeight = FontWeight.Regular;
                if (regularFont != null) text.font = regularFont;
                text.color = UIChrome.DockDimText;
            }
            else if (text.transform.parent == transform && text.name == "Blurb")
            {
                text.fontSize = rowSize;
                text.fontStyle = FontStyles.Normal;
                text.fontWeight = FontWeight.Regular;
                if (regularFont != null) text.font = regularFont;
                text.color = UIChrome.DockText;
            }
            else continue;
            text.enableAutoSizing = false;
        }

        int index = 0;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Btn_Build_"))
            {
                var rt = (RectTransform)child;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(sideInset, -44f - index++ * (rowHeight + rowGap));
                rt.sizeDelta = new Vector2(-2 * sideInset, rowHeight);
            }
            else if (child.name == "CollectionScroll")
            {
                var rt = (RectTransform)child;
                rt.offsetMin = new Vector2(sideInset, 14);
                rt.offsetMax = new Vector2(-sideInset, -44);
                var layout = child.GetComponentInChildren<VerticalLayoutGroup>(true);
                if (layout != null) layout.spacing = rowGap;
            }
            else if (child.name == "CollectionRowTemplate" || child.name == "AddCollectionRowTemplate")
            {
                var layout = child.GetComponent<LayoutElement>();
                if (layout != null) layout.preferredHeight = rowHeight;
            }
        }
        var tabs = GetComponent<BuildColumnTabs>();
        if (tabs != null) tabs.RefreshCurrent();
    }
}
