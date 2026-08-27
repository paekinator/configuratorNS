using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small screen-space pill that follows the mouse cursor, used for placement
/// guidance ("Click to start a frame here") and live measurements
/// ("H7 · 663 mm"). Replaces the old floating 3D world text, which read as
/// haloed billboard type hovering in the scene; this matches the app's card
/// styling and always sits exactly where the user is looking.
///
/// Ownership: several guides exist but only one drives the tooltip at a
/// time. Show() records the caller; Hide() from anyone else is ignored, so
/// an idle guide can't stomp the active one.
/// </summary>
public class CursorTooltip : MonoBehaviour
{
    static CursorTooltip _instance;
    static object _owner;

    RectTransform _rt;
    RectTransform _canvasRt;
    Canvas _canvas;
    Image _bg;
    TextMeshProUGUI _text;

    const float PaddingX = 12f;
    const float Height = 30f;
    static readonly Vector2 CursorOffset = new Vector2(18f, -24f);

    public static void Show(object owner, string text, bool danger = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide(owner);
            return;
        }

        CursorTooltip inst = Ensure();
        if (inst == null)
            return;
        _owner = owner;
        inst.Apply(text, danger);
    }

    public static void Hide(object owner)
    {
        if (_instance == null || (_owner != null && !ReferenceEquals(owner, _owner)))
            return;
        _owner = null;
        _instance.gameObject.SetActive(false);
    }

    static CursorTooltip Ensure()
    {
        if (_instance != null)
            return _instance;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return null;

        var go = new GameObject("CursorTooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        _instance = go.AddComponent<CursorTooltip>();
        _instance.Build(canvas);
        return _instance;
    }

    void Build(Canvas canvas)
    {
        _canvas = canvas;
        _canvasRt = (RectTransform)canvas.transform;
        _rt = (RectTransform)transform;
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0f, 1f);   // hangs below-right of the cursor
        _rt.sizeDelta = new Vector2(120f, Height);

        _bg = GetComponent<Image>();
        _bg.raycastTarget = false;

        // Adopt the shared card sprite so the pill matches every other panel.
        Transform partsPanel = canvas.transform.Find("PartsPanel");
        var reference = partsPanel != null ? partsPanel.GetComponent<Image>() : null;
        if (reference != null && reference.sprite != null)
        {
            _bg.sprite = reference.sprite;
            _bg.type = Image.Type.Sliced;
            _bg.pixelsPerUnitMultiplier = reference.pixelsPerUnitMultiplier * 2.2f;
        }

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(PaddingX, 0f);
        textRt.offsetMax = new Vector2(-PaddingX, 0f);
        _text.fontSize = 12.5f;
        _text.alignment = TextAlignmentOptions.Midline;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;

        var donorText = canvas.GetComponentInChildren<TMP_Text>(true);
        if (donorText != null && donorText.font != null)
            _text.font = donorText.font;

        gameObject.SetActive(false);
    }

    void Apply(string text, bool danger)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        // Guides call Show every frame; only dirty the canvas on real changes.
        if (_text.text != text)
        {
            _text.text = text;
            float width = Mathf.Ceil(_text.GetPreferredValues(text).x) + PaddingX * 2f;
            _rt.sizeDelta = new Vector2(width, Height);
        }
        _text.color = danger ? UIThemeController.DangerColor : UIThemeController.InkColor;

        Color card = UIThemeController.CardColor;
        card.a = 0.96f;
        _bg.color = card;

        Reposition();
    }

    void Update()
    {
        Reposition();
    }

    void Reposition()
    {
        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRt, Input.mousePosition, uiCam, out Vector2 local);

        Vector2 pos = local + CursorOffset;

        // Keep the pill fully on screen (flip above the cursor near the
        // bottom edge, pull left at the right edge).
        Vector2 half = _canvasRt.rect.size * 0.5f;
        Vector2 size = _rt.sizeDelta;
        if (pos.x + size.x > half.x - 8f)
            pos.x = local.x - size.x - 12f;
        if (pos.y - size.y < -half.y + 8f)
            pos.y = local.y + size.y + 6f;

        _rt.anchoredPosition = pos;
    }
}
