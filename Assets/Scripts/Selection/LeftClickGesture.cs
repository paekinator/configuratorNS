using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shared left-mouse gesture arbiter implementing one universal rule:
/// "click acts, drag selects". Placement tools commit on ClickReleased
/// (press and release without moving), while the marquee and other drag
/// tools own presses that travel beyond the threshold. A tool can claim
/// the current press (PressClaim) at press time so other drag consumers
/// leave the gesture alone.
/// </summary>
public static class LeftClickGesture
{
    public const float DragThresholdPx = 8f;

    static int _trackedFrame = -1;
    static bool _pressed;
    static bool _dragged;
    static bool _overUI;
    static Vector3 _downPos;
    static object _claim;

    /// <summary>Set by a drag tool at press time to own the whole gesture.</summary>
    public static object PressClaim
    {
        get { Track(); return _claim; }
        set { Track(); _claim = value; }
    }

    public static Vector3 PressPosition { get { Track(); return _downPos; } }
    public static bool PressStartedOverUI { get { Track(); return _overUI; } }
    public static bool PressedThisFrame { get { Track(); return _pressed && Input.GetMouseButtonDown(0); } }

    /// <summary>Press travelled beyond the threshold and the button is still held.</summary>
    public static bool IsDragging { get { Track(); return _pressed && _dragged && Input.GetMouseButton(0); } }

    /// <summary>Released this frame without ever dragging (a clean click, press not over UI).</summary>
    public static bool ClickReleased
    {
        get { Track(); return _pressed && !_dragged && !_overUI && Input.GetMouseButtonUp(0); }
    }

    /// <summary>Released this frame after dragging (end of a drag gesture, press not over UI).</summary>
    public static bool DragReleased
    {
        get { Track(); return _pressed && _dragged && !_overUI && Input.GetMouseButtonUp(0); }
    }

    static void Track()
    {
        if (Time.frameCount == _trackedFrame)
            return;
        _trackedFrame = Time.frameCount;

        if (Input.GetMouseButtonDown(0))
        {
            _pressed = true;
            _dragged = false;
            _claim = null;
            _downPos = Input.mousePosition;
            _overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
        else if (_pressed && Input.GetMouseButton(0))
        {
            if (!_dragged && (Input.mousePosition - _downPos).magnitude > DragThresholdPx)
                _dragged = true;
        }
        else if (!Input.GetMouseButtonUp(0))
        {
            // Button fully idle: the previous press is consumed.
            _pressed = false;
            _dragged = false;
            _claim = null;
        }
    }
}
