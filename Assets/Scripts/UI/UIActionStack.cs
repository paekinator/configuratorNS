using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A column of round action buttons that grows out of the control which
/// opened it, and folds back into it.
///
/// The anchor stays put — it is the toggle you pressed, and it becomes the
/// way out. Everything above it starts at the anchor's position at zero
/// scale and travels to its own place, each a little after the one below, so
/// the group reads as unfolding from the button rather than appearing all at
/// once. Folding runs the same way in reverse.
///
/// The animation is not decoration. Four circles appearing instantly over a
/// picture look like part of the picture; watching them come out of the
/// button says where they came from and, when you press it again, where they
/// went.
///
/// Unscaled time throughout: this is interface, and it must not slow down or
/// stop with the scene.
/// </summary>
public class UIActionStack : MonoBehaviour
{
    [Tooltip("The buttons above the anchor, nearest first.")]
    public List<RectTransform> items = new List<RectTransform>();

    [Tooltip("Spacing between one button and the next, in canvas units.")]
    public float step = 30f;

    [Tooltip("Seconds for one button to travel.")]
    public float seconds = 0.16f;

    [Tooltip("Seconds each button waits after the one below it.")]
    public float stagger = 0.035f;

    bool _expanded;
    float _clock;

    /// <summary>Whether the stack is open. Set by the card's edit toggle.</summary>
    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
                return;
            _expanded = value;
            _clock = 0f;
            enabled = true;
        }
    }

    /// <summary>Jump straight to the current state, with no animation.</summary>
    public void Snap()
    {
        _clock = float.MaxValue;
        Apply();
        enabled = false;
    }

    void OnEnable() => Apply();

    void Update()
    {
        _clock += Time.unscaledDeltaTime;
        Apply();

        // Stop once the last one has arrived; there is nothing to do every
        // frame for a stack that is sitting still.
        if (_clock >= seconds + stagger * Mathf.Max(0, items.Count - 1))
            enabled = false;
    }

    void Apply()
    {
        for (int i = 0; i < items.Count; i++)
        {
            RectTransform item = items[i];
            if (item == null)
                continue;

            // Opening, the nearest button leads; closing, it goes last — so
            // the stack folds back into the anchor from the far end rather
            // than collapsing from the button outwards.
            float delay = stagger * (_expanded ? i : items.Count - 1 - i);
            float t = seconds <= 0f ? 1f : Mathf.Clamp01((_clock - delay) / seconds);

            // Smoothstep: it leaves and arrives gently, which reads as one
            // movement rather than four things starting and stopping.
            t = t * t * (3f - 2f * t);
            float amount = _expanded ? t : 1f - t;

            item.anchoredPosition = new Vector2(0f, step * (i + 1) * amount);
            item.localScale = Vector3.one * amount;

            // A fully collapsed button is still a click target at zero scale,
            // so it is switched off rather than merely shrunk.
            bool live = amount > 0.01f;
            if (item.gameObject.activeSelf != live)
                item.gameObject.SetActive(live);
        }
    }
}
