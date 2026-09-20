using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Says what a control does while the cursor is over it, in the status pill.
///
/// The pill already carries the guidance for whatever mode you are in, so
/// this appends rather than replaces: "Editing My Collections" becomes
/// "Editing My Collections · Remove a collection?" and goes back when you
/// move away. The mode does not vanish from under you just because you
/// passed over a button.
///
/// The base line is owned by whoever set the mode; this only remembers what
/// it said, so nothing has to be told when the mode changes.
/// </summary>
public class UIHoverStatus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Appended after the current status while hovered.")]
    public string hint;

    /// <summary>
    /// What the pill should return to. Set by whoever put the app into this
    /// mode; empty means "clear on exit".
    /// </summary>
    public string baseStatus;

    bool _showing;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(hint))
            return;

        _showing = true;
        SelectionStatus.Set(string.IsNullOrEmpty(baseStatus) ? hint : baseStatus + " · " + hint, 0f);
    }

    public void OnPointerExit(PointerEventData eventData) => Restore();

    // A control that is hidden or destroyed under the cursor — which is what
    // every one of these does, since acting on them rebuilds the list — never
    // receives its exit, and the hint would stick forever.
    void OnDisable()
    {
        if (_showing)
            Restore();
    }

    void Restore()
    {
        if (!_showing)
            return;
        _showing = false;
        SelectionStatus.Set(baseStatus ?? string.Empty, 0f);
    }
}
