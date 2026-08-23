using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Injects a fullscreen toggle button into the bottom-right corner of the
/// canvas, just above the camera hint pill. Esc always leaves browser
/// fullscreen (that key is owned by the browser, tools use Esc too), so this
/// button is the reliable way in and back out.
/// </summary>
public static class FullscreenBootstrap
{
    static readonly Color Surface = new Color(0.953f, 0.937f, 0.914f);
    static readonly Color Muted = new Color(0.561f, 0.533f, 0.502f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.transform.Find("Btn_Fullscreen") != null)
            return;

        var go = new GameObject("Btn_Fullscreen", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-24f, 60f);   // hint pill tops out at 50, keep a 10 px rhythm
        rt.sizeDelta = new Vector2(40f, 40f);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        ApplyCardSprite(canvas, img);

        UiPolish.SoftShadow(rt, scale: 0.45f);

        Sprite icon = Resources.Load<Sprite>("UI/FullscreenIcon");
        if (icon != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(20f, 20f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = Muted;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        button.onClick.AddListener(Toggle);
    }

    static void Toggle()
    {
        // Called from a click, which counts as a user gesture, so WebGL
        // browsers accept the fullscreen request too.
        Screen.fullScreen = !Screen.fullScreen;
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
