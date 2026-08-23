using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared styling for panels that are built in code at runtime, so they get
/// the exact same finish as the editor-baked ones:
///
///  - <see cref="SoftShadow"/> clones the Evo sprite shadow the baked panels
///    use (found via any baked "Shadow" child in the scene), replacing the
///    old hard uGUI Shadow(0,-3) that made injected windows look flat.
///  - <see cref="HoverTint"/> applies one consistent hover/pressed tint.
/// </summary>
public static class UiPolish
{
    static readonly Color ShadowTint = new Color(0.12f, 0.09f, 0.06f, 0.35f);

    static Sprite _shadowSprite;

    /// <summary>
    /// Attach the soft drop shadow used by the editor-built panels. Falls
    /// back to a subtle uGUI Shadow when no donor sprite exists in the scene.
    /// </summary>
    public static void SoftShadow(RectTransform panel, float scale = 1f)
    {
        Sprite sprite = FindShadowSprite(panel);
        if (sprite == null)
        {
            var fallback = panel.gameObject.AddComponent<Shadow>();
            fallback.effectColor = new Color(0f, 0f, 0f, 0.18f);
            fallback.effectDistance = new Vector2(0f, -2f);
            return;
        }

        var go = new GameObject("Shadow", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-22f, -28f) * scale;
        rt.offsetMax = new Vector2(22f, 16f) * scale;
        rt.SetAsFirstSibling();

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = ShadowTint;
        img.raycastTarget = false;
    }

    static Sprite FindShadowSprite(Component context)
    {
        if (_shadowSprite != null)
            return _shadowSprite;

        Canvas canvas = context.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return null;

        // Any baked panel (TopBar, PartsPanel, ...) carries a "Shadow" child
        // with the Evo shadow sprite; use the first one as the donor.
        foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Shadow" && img.sprite != null)
            {
                _shadowSprite = img.sprite;
                break;
            }
        }

        return _shadowSprite;
    }

    /// <summary>One subtle hover/pressed tint for every code-built button.</summary>
    public static void HoverTint(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }
}
