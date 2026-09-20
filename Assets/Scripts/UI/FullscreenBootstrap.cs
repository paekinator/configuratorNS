using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attaches the fullscreen toggle to the rail button the builder baked.
/// Esc always leaves browser fullscreen (the browser owns that key, and the
/// tools use Esc too), so this button is the reliable way in and back out.
///
/// It used to BUILD that button — and only attached the click on the path
/// where it did. So it adopted a baked button happily and then left it inert:
/// a control that looked right and did nothing. The builder bakes it now, in
/// the rail, and this only wires it.
/// </summary>
public static class FullscreenBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform t = UIChrome.FindButton(canvas.transform, "Btn_Fullscreen");
        var button = t != null ? t.GetComponent<Button>() : null;
        if (button == null)
            return;

        // Remove first: a domain reload re-runs this, and the listener would
        // otherwise stack and toggle fullscreen twice per click.
        button.onClick.RemoveListener(Toggle);
        button.onClick.AddListener(Toggle);
    }

    static void Toggle()
    {
        // Called from a click, which counts as a user gesture, so WebGL
        // browsers accept the fullscreen request too.
        Screen.fullScreen = !Screen.fullScreen;
    }
}
