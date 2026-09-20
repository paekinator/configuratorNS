using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gets the dock out of the way, so the scene can be looked at.
///
/// The control existed long before the behaviour did: the builder has been
/// baking a chevron above the dock for several revisions, and clicking it did
/// nothing at all. This is that click.
///
/// WHAT MOVES. The dock slides straight down until its top edge reaches the
/// bottom of the screen, and everything in the band above it — the collapse
/// control, the Pro | Lite switch, the status pill, the keyboard hint — rides
/// the same distance, so the row stays together and lands just above the
/// bottom edge. The status and hint pills hide while collapsed. One offset is
/// added to positions that are otherwise left exactly as the builder baked
/// them, which is why this cannot drift out of step with the builder.
///
/// WHAT RIDES ALONG is found by RULE, not from a list: any direct child of the
/// canvas whose bottom edge sits on the band baseline. A list would be a
/// second place to remember the band's membership, and the next control added
/// to the band would simply be left behind — visibly, and only in the
/// collapsed state, which is the kind of fault that survives a long time.
/// </summary>
public class DockCollapse : MonoBehaviour
{
    [Tooltip("The dock. Assigned by the UI builder.")]
    public RectTransform dock;

    [Tooltip("The chevron; rotated to point the other way while collapsed.")]
    public RectTransform icon;

    /// <summary>Seconds for the slide, in unscaled time.</summary>
    const float Duration = 0.22f;

    /// <summary>
    /// How close to the band baseline a canvas child has to sit to count as
    /// part of the band. Generous: the point is to catch anything a person
    /// placed in the row by eye, not to demand an exact number.
    /// </summary>
    const float BandTolerance = 8f;

    readonly List<RectTransform> _riders = new List<RectTransform>();
    readonly List<Vector2> _openPositions = new List<Vector2>();

    bool _collapsed;
    float _t;          // 0 = open, 1 = collapsed
    bool _animating;
    GameObject _statusPill, _hintPill;
    bool _statusWasActive, _hintWasActive;

    /// <summary>Whether the dock is currently out of the way.</summary>
    public bool Collapsed => _collapsed;

    /// <summary>
    /// How far everything is currently displaced from where the builder baked
    /// it: 0 when open, negative while collapsing or collapsed. Published so
    /// UIWiringCheck can test the band's layout in play mode without having to
    /// know whether the dock happens to be down at that moment.
    /// </summary>
    public float Offset { get; private set; }

    void Awake()
    {
        CollectRiders();

        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(Toggle);
    }

    /// <summary>
    /// The dock, plus every band control. Positions are captured here so the
    /// open state is whatever the builder baked, never a number repeated in
    /// this file.
    /// </summary>
    void CollectRiders()
    {
        _riders.Clear();
        _openPositions.Clear();

        if (dock != null)
            Add(dock);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        _statusPill = UIChrome.FindPanel(canvas.transform, "StatusPill")?.gameObject;
        _hintPill = UIChrome.FindPanel(canvas.transform, "HintPill")?.gameObject;

        foreach (Transform child in canvas.transform)
        {
            if (!(child is RectTransform rt) || rt == dock)
                continue;

            // Bottom-anchored, sitting on the band baseline. Panels anchored
            // to the top or stretched over the whole canvas are not in the
            // band and must not move with it.
            if (!Mathf.Approximately(rt.anchorMin.y, 0f) || !Mathf.Approximately(rt.anchorMax.y, 0f))
                continue;
            if (Mathf.Abs(rt.anchoredPosition.y - UIChrome.BandY) > BandTolerance)
                continue;

            Add(rt);
        }

        void Add(RectTransform rt)
        {
            _riders.Add(rt);
            _openPositions.Add(rt.anchoredPosition);
        }
    }

    public void Toggle() => SetCollapsed(!_collapsed);

    public void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;

        _collapsed = collapsed;
        _animating = true;
        SetPillVisibility(_statusPill, collapsed, ref _statusWasActive);
        SetPillVisibility(_hintPill, collapsed, ref _hintWasActive);
    }

    static void SetPillVisibility(GameObject pill, bool collapsed, ref bool wasActive)
    {
        if (pill == null) return;
        if (collapsed)
        {
            wasActive = pill.activeSelf;
            pill.SetActive(false);
        }
        else pill.SetActive(wasActive);
    }

    void Update()
    {
        if (!_animating)
            return;

        float target = _collapsed ? 1f : 0f;
        // Unscaled: a paused or slowed timeScale must not leave the dock
        // stranded halfway down.
        _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / Duration);
        if (Mathf.Approximately(_t, target))
            _animating = false;

        Apply();
    }

    void Apply()
    {
        // Smoothstep rather than linear: the slide decelerates into both ends,
        // which is what the rest of this UI's motion does (see UIActionStack).
        float eased = _t * _t * (3f - 2f * _t);
        Offset = -UIChrome.CollapseDrop * eased;

        for (int i = 0; i < _riders.Count; i++)
        {
            RectTransform rt = _riders[i];
            if (rt == null)
                continue;

            Vector2 open = _openPositions[i];
            rt.anchoredPosition = new Vector2(open.x, open.y + Offset);
        }

        if (icon != null)
            icon.localEulerAngles = new Vector3(0f, 0f, 180f * eased);
    }
}
