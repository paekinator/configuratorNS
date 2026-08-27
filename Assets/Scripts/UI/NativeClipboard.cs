using UnityEngine;

/// <summary>
/// Clipboard writes that actually work everywhere: the browser clipboard API
/// on WebGL (GUIUtility.systemCopyBuffer is a no-op there), the system
/// clipboard in the editor and desktop builds.
/// </summary>
public static class NativeClipboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void NeoCopyText(string text);
#endif

    public static void Copy(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
#if UNITY_WEBGL && !UNITY_EDITOR
        NeoCopyText(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}
