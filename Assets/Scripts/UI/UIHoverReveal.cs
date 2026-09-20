using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fades a decoration in while the pointer is over a control, and out again
/// when it leaves: the underline beneath a dock tab's title, the outline
/// around a Tools/Parts row.
///
/// Both are a hidden Graphic that this reveals, rather than a colour change on
/// the control itself. The dock's tabs and rows already use their background
/// colour to show which one is SELECTED, so hovering had nowhere left to go
/// without the two states muddling each other.
///
/// The animation is driven from Update with unscaled time. It has to be
/// unscaled: the configurator is a tool, and if anything ever pauses the game
/// clock the interface must still respond.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIHoverReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>The decoration to reveal. Starts hidden.</summary>
    public Graphic target;

    /// <summary>Opacity at full reveal.</summary>
    public float shownAlpha = 1f;

    /// <summary>Seconds for a full fade in or out.</summary>
    public float seconds = 0.12f;

    /// <summary>
    /// Also sweep the decoration out horizontally from its centre, for an
    /// underline that draws itself in rather than simply appearing.
    /// </summary>
    public bool sweepHorizontally;

    /// <summary>
    /// While true the decoration stays hidden however the pointer moves.
    ///
    /// Set by whichever control owns the selection — DockTabs for the dock's
    /// tabs, BuildColumnTabs for the Tools/Parts rows — so the SELECTED one
    /// does not also offer a hover. It already shows its state through its
    /// background; a second cue on top only muddles which is which.
    ///
    /// Not serialized: it is live state, not a setting, and re-deriving it on
    /// every refresh is what keeps it honest after a rebuild.
    /// </summary>
    public bool Suppressed { get; set; }

    float _shown;      // 0 hidden, 1 revealed
    bool _wanted;

    void OnEnable()
    {
        // No animation on the way in: a control that is already under the
        // pointer when its page opens should not flash.
        _wanted = false;
        _shown = 0f;
        Apply();
    }

    void OnDisable()
    {
        _wanted = false;
        _shown = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => _wanted = true;

    public void OnPointerExit(PointerEventData eventData) => _wanted = false;

    void Update()
    {
        // Suppressed fades OUT rather than cutting: a tab selected while the
        // pointer is still on it should retract its underline, not blink.
        float goal = _wanted && !Suppressed ? 1f : 0f;
        if (Mathf.Approximately(_shown, goal))
            return;

        float step = seconds > 0.001f ? Time.unscaledDeltaTime / seconds : 1f;
        _shown = Mathf.MoveTowards(_shown, goal, step);
        Apply();
    }

    void Apply()
    {
        if (target == null)
            return;

        Color c = target.color;
        c.a = shownAlpha * _shown;
        target.color = c;

        if (sweepHorizontally)
        {
            Vector3 s = target.rectTransform.localScale;
            // Never exactly zero: a zero-width rect makes the layout rebuild
            // do needless work and can leave a 1px artefact on some drivers.
            s.x = Mathf.Max(0.001f, _shown);
            target.rectTransform.localScale = s;
        }
    }
}
