using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Injects a small ruler button above the fullscreen button (bottom-right
/// corner). It pins the CAD-style dimension annotations: pinned = always
/// visible while something is built, unpinned = the existing behaviour where
/// they flash for a few seconds after a change. The choice persists.
/// </summary>
public static class DimensionsToggleBootstrap
{
    static Image _background;
    static Image _icon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("Btn_Dimensions");
        if (existing == null)
            CreateButton(canvas);
        else
        {
            _background = existing.GetComponent<Image>();
            Transform icon = existing.Find("Icon");
            _icon = icon != null ? icon.GetComponent<Image>() : null;
        }

        UIThemeController.ThemeChanged -= Restyle;
        UIThemeController.ThemeChanged += Restyle;
        Restyle();
    }

    static void CreateButton(Canvas canvas)
    {
        var go = new GameObject("Btn_Dimensions", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-24f, 110f);   // stacked above fullscreen, same 10 px rhythm
        rt.sizeDelta = new Vector2(40f, 40f);

        _background = go.GetComponent<Image>();
        ApplyCardSprite(canvas, _background);

        UiPolish.SoftShadow(rt, scale: 0.45f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(22f, 22f);
        _icon = iconGo.GetComponent<Image>();
        _icon.sprite = UIIcons.Get("Dimension");
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        button.onClick.AddListener(() =>
        {
            StructureDimensionsController.Pinned = !StructureDimensionsController.Pinned;
            Restyle();
        });
    }

    static void Restyle()
    {
        bool on = StructureDimensionsController.Pinned;
        if (_background != null)
            _background.color = on ? UIThemeController.AccentColor : UIThemeController.SurfaceColor;
        if (_icon != null)
            _icon.color = on ? Color.white : UIThemeController.MutedColor;
    }

    static void ApplyCardSprite(Canvas canvas, Image img)
    {
        Transform partsPanel = canvas.transform.Find("PartsPanel");
        var reference = partsPanel != null ? partsPanel.GetComponent<Image>() : null;
        if (reference == null || reference.sprite == null)
            return;

        img.sprite = reference.sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = reference.pixelsPerUnitMultiplier * 1.7f;
    }
}
