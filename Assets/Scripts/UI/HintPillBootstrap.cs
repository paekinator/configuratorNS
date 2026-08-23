using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the bottom-right shortcut pill tidy in every scene, including ones
/// baked with older copy: short text, the pill sized to hug the text exactly,
/// the same height and baseline as the status pill, and the whole pill hidden
/// when a narrow window would run it into the status pill.
/// </summary>
public class HintPillBootstrap : MonoBehaviour
{
    const string HintText = "Arrows nudge 88 mm  ·  Esc drops the tool  ·  Ctrl+Z undo";
    const float SidePadding = 20f;

    RectTransform _pill;
    RectTransform _status;
    Image _pillImage;
    Image _statusImage;
    CanvasGroup _group;
    int _lastWidth = -1;
    int _lastHeight = -1;
    float _lastStatusWidth = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<HintPillBootstrap>() != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform pill = canvas != null ? canvas.transform.Find("HintPill") : null;
        if (pill == null)
            return;

        var host = pill.gameObject.AddComponent<HintPillBootstrap>();
        host._pill = (RectTransform)pill;
        host._status = canvas.transform.Find("StatusPill") as RectTransform;
        host._group = pill.gameObject.GetComponent<CanvasGroup>();
        if (host._group == null)
            host._group = pill.gameObject.AddComponent<CanvasGroup>();
        host.Restyle();
    }

    void Restyle()
    {
        var text = _pill.Find("Txt_Hint")?.GetComponent<TextMeshProUGUI>();
        if (text == null)
            return;

        text.text = HintText;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Midline;

        // Baked scenes carry a stale fixed width; hug the text instead, and
        // sit on the status pill's baseline so the bottom edge reads as one
        // aligned row.
        float textWidth = Mathf.Ceil(text.GetPreferredValues(HintText).x);
        _pill.anchorMin = _pill.anchorMax = new Vector2(1f, 0f);
        _pill.pivot = new Vector2(1f, 0f);
        _pill.anchoredPosition = new Vector2(-24f, 12f);
        _pill.sizeDelta = new Vector2(textWidth + SidePadding * 2f, 38f);

        // Dress the pill like the status pill (solid card, same rounding,
        // soft shadow) instead of the old translucent white that read as
        // loose text floating on the floor.
        _pillImage = _pill.GetComponent<Image>();
        _statusImage = _status != null ? _status.GetComponent<Image>() : null;
        if (_pillImage != null && _statusImage != null)
        {
            if (_statusImage.sprite != null)
            {
                _pillImage.sprite = _statusImage.sprite;
                _pillImage.type = _statusImage.type;
                _pillImage.pixelsPerUnitMultiplier = _statusImage.pixelsPerUnitMultiplier;
            }
            _pillImage.color = _statusImage.color;
        }
        if (_pill.Find("Shadow") == null)
            UiPolish.SoftShadow(_pill, 0.6f);
    }

    void LateUpdate()
    {
        // Follow the status pill through theme changes (light/dark swap the
        // card color at runtime).
        if (_pillImage != null && _statusImage != null && _pillImage.color != _statusImage.color)
            _pillImage.color = _statusImage.color;

        // The status pill resizes with its messages, so both the window and
        // the pill can move the collision boundary.
        float statusWidth = _status != null ? _status.sizeDelta.x : 0f;
        if (Screen.width == _lastWidth && Screen.height == _lastHeight &&
            Mathf.Approximately(statusWidth, _lastStatusWidth))
            return;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        _lastStatusWidth = statusWidth;
        UpdateVisibility();
    }

    /// <summary>Hide the pill instead of letting it collide with the status pill.</summary>
    void UpdateVisibility()
    {
        bool visible = true;
        if (_status != null && _status.gameObject.activeInHierarchy)
        {
            var pillCorners = new Vector3[4];
            var statusCorners = new Vector3[4];
            _pill.GetWorldCorners(pillCorners);
            _status.GetWorldCorners(statusCorners);

            // pillCorners[0] is the pill's bottom-left, statusCorners[2] the
            // status pill's top-right; both sit on the bottom edge.
            visible = pillCorners[0].x > statusCorners[2].x + 12f;
        }

        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
    }
}
