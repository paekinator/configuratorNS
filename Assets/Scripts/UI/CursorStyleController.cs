using UnityEngine;

/// <summary>
/// Hover cursor feedback on WebGL: the browser's own pointer (hand) over
/// anything clickable, its text beam over input fields, the default arrow
/// everywhere else. Uses the native CSS cursor so it looks exactly like the
/// rest of the web.
///
/// In the editor and on desktop the OS cursors can't be reached without
/// shipping custom textures, so the cursor is left untouched there.
/// </summary>
public class CursorStyleController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (FindFirstObjectByType<CursorStyleController>() == null)
            new GameObject("CursorStyleController").AddComponent<CursorStyleController>();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void NeoSetCursor(string style);

    readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _hits =
        new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    UnityEngine.EventSystems.PointerEventData _pointerData;
    string _current = "default";

    void Update()
    {
        // While the fly camera locks the cursor the browser hides it anyway.
        if (Cursor.lockState == CursorLockMode.Locked)
            return;

        string want = WantedCursor();
        if (want == _current)
            return;
        _current = want;
        NeoSetCursor(want);
    }

    string WantedCursor()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
            return "default";

        if (_pointerData == null)
            _pointerData = new UnityEngine.EventSystems.PointerEventData(es);
        _pointerData.position = Input.mousePosition;

        _hits.Clear();
        es.RaycastAll(_pointerData, _hits);
        if (_hits.Count == 0)
            return "default";

        GameObject top = _hits[0].gameObject;

        var input = top.GetComponentInParent<TMPro.TMP_InputField>();
        if (input != null && input.interactable)
            return "text";

        var selectable = top.GetComponentInParent<UnityEngine.UI.Selectable>();
        if (selectable != null && selectable.interactable)
            return "pointer";

        return "default";
    }
#endif
}
