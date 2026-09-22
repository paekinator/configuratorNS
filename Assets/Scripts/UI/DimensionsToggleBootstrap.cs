using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attaches the dimensions pin to the rail button the builder baked, and keeps
/// it lit while pinned. Pinned = the CAD-style annotations stay visible
/// whenever something is built; unpinned = they flash for a few seconds after
/// a change. The choice persists.
///
/// It used to BUILD that button — and only attached the click on the path
/// where it did. So it adopted a baked button, styled it correctly, and left
/// it inert. The builder bakes it now and this only wires it.
///
/// Two things stay this script's responsibility, both because the builder
/// cannot do them:
///   - the icon sprite, which UIIcons draws procedurally at runtime and so
///     cannot be saved into a scene;
///   - the button's colours, which follow Pinned rather than an open panel.
///     That is why the baked button carries no RailButtonVisual: the pair
///     below would have had two owners.
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

        Transform t = UIChrome.FindButton(canvas.transform, "Btn_Dimensions");
        if (t == null)
            return;

        _background = t.GetComponent<Image>();

        Transform icon = t.Find("Icon");
        _icon = icon != null ? icon.GetComponent<Image>() : null;
        if (_icon != null && _icon.sprite == null)
            _icon.sprite = UIIcons.Get("Dimension");

        var button = t.GetComponent<Button>();
        if (button != null)
        {
            // A named method rather than a lambda, so the remove actually
            // matches: a domain reload re-runs this, and an anonymous listener
            // could never be taken off again.
            button.onClick.RemoveListener(TogglePinned);
            button.onClick.AddListener(TogglePinned);
        }

        UIThemeController.ThemeChanged -= Restyle;
        UIThemeController.ThemeChanged += Restyle;
        Restyle();
    }

    static void TogglePinned()
    {
        StructureDimensionsController.Pinned = !StructureDimensionsController.Pinned;
        Restyle();
    }

    static void Restyle()
    {
        if (_background != null && _background.TryGetComponent(out RailButtonVisual visual))
        {
            visual.RefreshVisual();
            return;
        }
        bool on = StructureDimensionsController.Pinned;
        if (_background != null)
            _background.color = on ? UIThemeController.AccentColor : UIThemeController.SurfaceColor;
        if (_icon != null)
            _icon.color = on ? Color.white : UIThemeController.MutedColor;
    }
}
