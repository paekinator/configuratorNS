using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared utility-rail appearance and hover hints.</summary>
[RequireComponent(typeof(Image))]
public class RailButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Retained for existing serialized scenes and persistent click listeners.
    public string watchedPanelName;
    public Graphic icon;
    public float flashSeconds = 0.18f;
    [Tooltip("Short description shown beside the cursor.")]
    public string hint;
    public Color idleIcon = new Color32(155, 155, 155, 255);
    public Color hoverBackground = new Color32(60, 60, 60, 255);
    Image _background;
    bool _hovered;

    void Awake()
    {
        ConfigureGlyph();
        _background = GetComponent<Image>();
        if (icon == null) icon = transform.Find("Icon")?.GetComponent<Graphic>();
        var button = GetComponent<Button>();
        if (button != null) button.transition = Selectable.Transition.None;
        if (button != null) button.onClick.AddListener(UIStatusBar.FlashAction);
        RefreshVisual();
    }

    public void ConfigureGlyph()
    {
        UtilityLineIcon.Glyph glyph;
        switch (name)
        {
            case "Btn_Dimensions": glyph = UtilityLineIcon.Glyph.Dimensions; break;
            case "Btn_Undo": glyph = UtilityLineIcon.Glyph.Undo; break;
            case "Btn_Redo": glyph = UtilityLineIcon.Glyph.Redo; break;
            case "Btn_Fullscreen": glyph = UtilityLineIcon.Glyph.Fullscreen; break;
            default: return;
        }
        var old = transform.Find("Icon");
        if (old == null) return;
        var original = old.GetComponent<Image>();
        if (original != null) original.enabled = false;
        var child = old.Find("LineGlyph");
        if (child == null)
        {
            child = new GameObject("LineGlyph", typeof(RectTransform), typeof(UtilityLineIcon)).transform;
            child.SetParent(old, false);
        }
        // Undo used to mirror its bitmap parent; vector glyphs own direction.
        old.localScale = Vector3.one;
        var rt = (RectTransform)child;
        if (child.GetComponent<CanvasRenderer>() == null) child.gameObject.AddComponent<CanvasRenderer>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var line = child.GetComponent<UtilityLineIcon>();
        line.glyph = glyph; line.raycastTarget = false; line.SetVerticesDirty();
        icon = line;
    }

    void OnEnable() { _hovered = false; RefreshVisual(); }
    void OnDisable() { _hovered = false; CursorTooltip.Hide(this); RefreshVisual(); }
    public void Flash() { } // Hover alone controls the background.

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        RefreshVisual();
        if (!string.IsNullOrEmpty(hint)) CursorTooltip.Show(this, hint);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        CursorTooltip.Hide(this);
        RefreshVisual();
    }

    // Reassert after theme or action scripts update button state.
    void LateUpdate() => RefreshVisual();

    public void RefreshVisual()
    {
        if (_background == null) _background = GetComponent<Image>();
        if (_background != null) _background.color = _hovered ? hoverBackground : Color.clear;
        if (icon != null) icon.color = _hovered ? Color.white : idleIcon;
    }
}
