using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selection for placed parts (beams and panels) plus the floating action
/// card. One universal rule works in both the Tools and Parts tabs:
///   • click  = place / pick while a tool is armed (handled by the placement
///     tools); with NO tool armed, a click selects the part under the cursor
///   • drag   = draw a selection rectangle over the scene
/// Both roads end at a small card next to the selection with the available
/// actions: Copy (hand off to StructureClipboard), Delete, Deselect.
/// </summary>
public class MarqueeSelectionController : MonoBehaviour
{
    public Camera cam;
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;
    public StructureClipboard clipboard;
    public BeamResizeSession resize;

    // Theme tokens, read live so the marquee and action card follow the
    // light/dark palette instead of carrying their own blue.
    static Color Accent => UIThemeController.AccentColor;
    static Color Ink => UIThemeController.InkColor;
    static Color Surface => UIThemeController.CardColor;
    static Color Danger => UIThemeController.DangerColor;

    readonly List<SelectableBeam> _selected = new List<SelectableBeam>();

    public static MarqueeSelectionController Instance { get; private set; }

    void OnEnable()
    {
        Instance = this;
        UIThemeController.ThemeChanged += HandleThemeChanged;
    }

    void OnDisable()
    {
        UIThemeController.ThemeChanged -= HandleThemeChanged;
        if (Instance == this)
            Instance = null;
    }

    void HandleThemeChanged()
    {
        if (_popup == null)
            return;
        Vector2 pos = _popup.anchoredPosition;
        bool show = _popup.gameObject.activeSelf;
        Destroy(_popup.gameObject);
        _popup = null;
        if (!show)
            return;
        BuildPopup();
        _popup.gameObject.SetActive(true);
        _popup.anchoredPosition = pos;
        _popup.SetAsLastSibling();
    }

    /// <summary>
    /// One intent at a time: arming any placement tool dismisses whatever was
    /// pending — the selection and its action card, a copied structure
    /// mid-stamp, and a running length adjustment.
    /// </summary>
    public static void CancelPending()
    {
        if (Instance != null)
        {
            Instance.ClearSelection();
            Instance.HidePopup();
        }
        if (StructureClipboard.Active != null && StructureClipboard.Active.IsActive)
            StructureClipboard.Active.Clear();
        if (BeamResizeSession.Instance != null && BeamResizeSession.Instance.IsActive)
            BeamResizeSession.Instance.Cancel();
    }

    bool _marqueeActive;
    Vector2 _startScreen;
    TemplateSession _session;
    FreePartSession _freeSession;

    Canvas _canvas;
    RectTransform _rect;
    RectTransform _popup;
    TextMeshProUGUI _popupLabel;
    Material _fallbackSelectionMaterial;
    TMP_FontAsset _font;
    Sprite _cardSprite;
    float _cardPpu = 1f;

    public IReadOnlyList<SelectableBeam> Selected => _selected;

    void Update()
    {
        // While a copied structure is being stamped, the clipboard owns clicks.
        if (clipboard != null && clipboard.IsActive)
        {
            if (_marqueeActive)
            {
                _marqueeActive = false;
                HideRect();
            }
            if (_selected.Count > 0)
                ClearSelection();
            HidePopup();
            return;
        }

        // While a beam length scale is up, that session owns clicks (including
        // one frame past the commit so the click can't double as a pick).
        if (BeamResizeSession.Busy)
        {
            if (_marqueeActive)
            {
                _marqueeActive = false;
                HideRect();
            }
            HidePopup();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && _selected.Count > 0)
        {
            ClearSelection();
            HidePopup();
            SelectionStatus.Clear();
        }

        if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) && _selected.Count > 0)
        {
            DeleteSelection();
            return;
        }

        // A clean click: with no placement tool armed it selects the part
        // under the cursor (Shift adds to / removes from the selection);
        // a click on empty space deselects everything, unless Shift is held.
        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!PlacementArmed && TryPickPart(out SelectableBeam picked))
            {
                if (additive)
                {
                    // Shift+click toggles the part in the current selection.
                    if (_selected.Contains(picked))
                    {
                        picked.SetSelected(false);
                        _selected.Remove(picked);
                    }
                    else
                    {
                        picked.SetSelected(true);
                        _selected.Add(picked);
                    }
                }
                else
                {
                    ClearSelection();
                    picked.SetSelected(true);
                    _selected.Add(picked);
                }

                if (_selected.Count > 0)
                {
                    ShowPopup(Input.mousePosition);
                    SelectionStatus.Set(_selected.Count == 1
                        ? "1 part selected · Shift+click adds more · Esc deselects"
                        : $"{_selected.Count} parts selected · drag an arrow to move · Esc deselects");
                }
                else
                {
                    HidePopup();
                    SelectionStatus.Clear();
                }
            }
            else if (!additive &&
                     ((_popup != null && _popup.gameObject.activeSelf) || _selected.Count > 0))
            {
                ClearSelection();
                HidePopup();
                SelectionStatus.Clear();
            }
        }

        if (!_marqueeActive && LeftClickGesture.IsDragging &&
            LeftClickGesture.PressClaim == null && !LeftClickGesture.PressStartedOverUI)
        {
            LeftClickGesture.PressClaim = this;
            _marqueeActive = true;
            _startScreen = LeftClickGesture.PressPosition;
            HidePopup();
        }

        if (_marqueeActive)
            UpdateMarquee();
    }

    void UpdateMarquee()
    {
        Vector2 current = Input.mousePosition;
        DrawRect(_startScreen, current);
        ApplyRectSelection(_startScreen, current);

        SelectionStatus.Set(_selected.Count > 0
            ? $"{_selected.Count} {(_selected.Count == 1 ? "part" : "parts")} in selection · release to choose an action"
            : "Drag over parts to select them");

        if (!Input.GetMouseButton(0))
        {
            _marqueeActive = false;
            HideRect();

            if (_selected.Count > 0)
            {
                ShowPopup(current);
                SelectionStatus.Set(_selected.Count == 1
                    ? "1 part selected · Esc deselects"
                    : $"{_selected.Count} parts selected · Esc deselects");
            }
            else
            {
                SelectionStatus.Clear();
            }
        }
    }

    // ------------------------------------------------------------------
    // Selection
    // ------------------------------------------------------------------

    /// <summary>
    /// True while a click means "place / pick" for some tool, so a plain
    /// click must not be hijacked for selection.
    /// </summary>
    public bool PlacementArmed
    {
        get
        {
            if (StructureClipboard.StampingActive)
                return true;

            if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
            {
                if (_session == null)
                    _session = FindFirstObjectByType<TemplateSession>();
                return _session != null && _session.ActiveTool != GuidedTemplateTool.None;
            }

            if (_freeSession == null)
                _freeSession = FindFirstObjectByType<FreePartSession>();
            if (_freeSession != null && _freeSession.ActiveKind != FreePartKind.None)
                return true;

            return buildController != null && !string.IsNullOrEmpty(buildController.currentPartId);
        }
    }

    /// <summary>Part under the cursor: beams directly, panels also through their slot blocker.</summary>
    bool TryPickPart(out SelectableBeam picked)
    {
        picked = null;
        if (cam == null)
            return false;

        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~ghostMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (l, r) => l.distance.CompareTo(r.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;

            var pi = t.GetComponentInParent<PanelInstance>();
            if (pi != null)
            {
                picked = EnsureSelectable(pi.gameObject);
                return true;
            }

            // A panel blocker sits just in front of its panel; resolve to it.
            var slot = t.GetComponentInParent<PanelSlotHandle>();
            if (slot != null)
            {
                GameObject panel = slot.panelPlus != null ? slot.panelPlus : slot.panelMinus;
                if (panel != null)
                {
                    picked = EnsureSelectable(panel);
                    return true;
                }
                continue; // empty slot helper — look through it
            }

            Transform root = t.root;
            if (StructureClipboard.CleanPartId(root.name) != null)
            {
                picked = EnsureSelectable(root.gameObject);
                return true;
            }

            return false; // some other solid (e.g. the floor) occludes the ray
        }

        return false;
    }

    SelectableBeam EnsureSelectable(GameObject go)
    {
        var sel = go.GetComponent<SelectableBeam>();
        if (sel == null)
            sel = go.AddComponent<SelectableBeam>();
        if (sel.selectionMaterial == null)
            sel.selectionMaterial = ResolveFallbackSelectionMaterial();
        return sel;
    }

    void ApplyRectSelection(Vector2 a, Vector2 b)
    {
        if (cam == null)
            return;

        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        var inRect = new HashSet<SelectableBeam>();

        foreach (SelectableBeam sel in CollectSelectableParts())
        {
            Transform root = PartRootOf(sel);
            if ((ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;

            if (!PartOverlapsMarquee(cam, root, min, max))
                continue;

            inRect.Add(sel);
        }

        // Sync: deselect anything that left the rect, select the newcomers.
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            SelectableBeam sel = _selected[i];
            if (sel == null || !inRect.Contains(sel))
            {
                if (sel != null)
                    sel.SetSelected(false);
                _selected.RemoveAt(i);
            }
        }
        foreach (SelectableBeam sel in inRect)
        {
            if (!_selected.Contains(sel))
            {
                sel.SetSelected(true);
                _selected.Add(sel);
            }
        }
    }

    /// <summary>
    /// Every selectable placed part: beam roots (V/H/T) and panels. Panels
    /// placed from a prefab without SelectableBeam get one added on the fly so
    /// they highlight and delete exactly like beams.
    /// </summary>
    List<SelectableBeam> CollectSelectableParts()
    {
        var result = new List<SelectableBeam>();

        foreach (SelectableBeam sel in FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            if (sel == null || !sel.gameObject.activeInHierarchy)
                continue;

            bool isPart = StructureClipboard.CleanPartId(sel.transform.root.name) != null ||
                          sel.GetComponentInParent<PanelInstance>() != null;
            if (!isPart)
                continue;

            if (sel.selectionMaterial == null)
                sel.selectionMaterial = ResolveFallbackSelectionMaterial();
            result.Add(sel);
        }

        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || !pi.gameObject.activeInHierarchy)
                continue;
            if (pi.GetComponent<SelectableBeam>() != null)
                continue;

            var sel = pi.gameObject.AddComponent<SelectableBeam>();
            sel.selectionMaterial = ResolveFallbackSelectionMaterial();
            result.Add(sel);
        }

        return result;
    }

    Material ResolveFallbackSelectionMaterial()
    {
        if (_fallbackSelectionMaterial != null)
            return _fallbackSelectionMaterial;

        foreach (SelectableBeam sel in FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            if (sel != null && sel.selectionMaterial != null)
            {
                _fallbackSelectionMaterial = sel.selectionMaterial;
                break;
            }
        }
        return _fallbackSelectionMaterial;
    }

    /// <summary>
    /// The transform that represents one part. Beams have their SelectableBeam
    /// on the scene-root prefab instance; panels live UNDER PanelsRoot, so the
    /// panel object itself is the part — never its scene root.
    /// </summary>
    public static Transform PartRootOf(SelectableBeam sel)
    {
        var pi = sel.GetComponentInParent<PanelInstance>();
        return pi != null ? pi.transform : sel.transform.root;
    }

    public static Vector3 PartCenter(Transform root)
    {
        if (TryGetPartWorldBounds(root, out Bounds bounds))
            return bounds.center;
        return root.position;
    }

    /// <summary>
    /// True when any part of the object's screen silhouette overlaps the
    /// marquee rectangle — a drag that clips an edge selects the part, not
    /// only a drag that covers its centre.
    /// </summary>
    static bool PartOverlapsMarquee(Camera cam, Transform root, Vector2 rectMin, Vector2 rectMax)
    {
        if (!TryGetPartWorldBounds(root, out Bounds bounds))
            return false;

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool anyInFront = false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 world = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                continue;
            anyInFront = true;
            if (screen.x < minX) minX = screen.x;
            if (screen.y < minY) minY = screen.y;
            if (screen.x > maxX) maxX = screen.x;
            if (screen.y > maxY) maxY = screen.y;
        }

        if (!anyInFront)
            return false;

        return minX <= rectMax.x && maxX >= rectMin.x &&
               minY <= rectMax.y && maxY >= rectMin.y;
    }

    static bool TryGetPartWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        bool any = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;
            if (!any)
            {
                bounds = col.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (any)
            return true;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    /// <summary>Adds a part to the selection (used by the move gizmo to re-select re-seated panels).</summary>
    public SelectableBeam AddToSelection(GameObject go)
    {
        SelectableBeam sel = EnsureSelectable(go);
        if (!_selected.Contains(sel))
        {
            sel.SetSelected(true);
            _selected.Add(sel);
        }
        return sel;
    }

    /// <summary>Drops destroyed entries so callers can iterate a live list.</summary>
    public void PruneSelection()
    {
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            if (_selected[i] == null)
                _selected.RemoveAt(i);
        }
    }

    public void HideActionCard() => HidePopup();
    public void ShowActionCard(Vector2 screenPos) => ShowPopup(screenPos);

    public void ClearSelection()
    {
        foreach (SelectableBeam sel in _selected)
        {
            if (sel != null)
                sel.SetSelected(false);
        }
        _selected.Clear();
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    void OnCopyClicked()
    {
        var roots = new List<Transform>(_selected.Count);
        foreach (SelectableBeam sel in _selected)
        {
            if (sel != null)
                roots.Add(PartRootOf(sel));
        }

        ClearSelection();
        HidePopup();

        int copied = clipboard != null ? clipboard.CopyFromSelection(roots) : 0;
        if (copied == 0)
            SelectionStatus.Set("Nothing copyable in the selection.", 4f);
    }

    void DeleteSelection()
    {
        var toDelete = new List<SelectableBeam>(_selected);
        ClearSelection();
        HidePopup();

        int count = 0;
        foreach (SelectableBeam sel in toDelete)
        {
            if (sel == null)
                continue;

            var pi = sel.GetComponentInParent<PanelInstance>();
            if (pi != null)
            {
                if (panelSlotManager != null &&
                    panelSlotManager.TryGetSlot(pi.slotId, out PanelSlotHandle slot) && slot != null)
                {
                    panelSlotManager.RemovePanel(slot, pi.side);
                }
                else
                {
                    Destroy(pi.gameObject);
                }
                count++;
                continue;
            }

            var conn = sel.GetComponentInParent<BeamConnections>();
            if (conn != null)
                conn.ReleaseAll();
            Destroy(sel.transform.root.gameObject);
            count++;
        }

        if (count > 0 && panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        if (count > 0)
            BuildHistory.NotifyChanged();

        SelectionStatus.Set($"Deleted {count} parts.", 4f);
    }

    void OnDeselectClicked()
    {
        ClearSelection();
        HidePopup();
        SelectionStatus.Clear();
    }

    /// <summary>The Length action applies to exactly one selected vertical frame.</summary>
    bool CanResizeSelection()
    {
        if (_selected.Count != 1 || _selected[0] == null)
            return false;
        if (_selected[0].GetComponentInParent<PanelInstance>() != null)
            return false;
        return BeamResizeSession.CanResize(PartRootOf(_selected[0]));
    }

    void OnLengthClicked()
    {
        if (!CanResizeSelection())
            return;
        Transform root = PartRootOf(_selected[0]);
        ClearSelection();
        HidePopup();
        if (resize != null)
            resize.Begin(root);
    }

    // ------------------------------------------------------------------
    // Marquee rectangle visual
    // ------------------------------------------------------------------

    void DrawRect(Vector2 a, Vector2 b)
    {
        if (!EnsureCanvas())
            return;

        if (_rect == null)
        {
            var go = new GameObject("MarqueeRect", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            _rect = (RectTransform)go.transform;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            go.AddComponent<Outline>().effectDistance = new Vector2(1.25f, 1.25f);
        }

        // Neutral, hairline marquee in the theme ink — restyled every frame
        // so a theme toggle mid-session can't leave it stale.
        var rectImg = _rect.GetComponent<Image>();
        rectImg.color = new Color(Ink.r, Ink.g, Ink.b, 0.07f);
        _rect.GetComponent<Outline>().effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.6f);

        Vector2 localA = ScreenToCanvasLocal(a);
        Vector2 localB = ScreenToCanvasLocal(b);
        Vector2 min = Vector2.Min(localA, localB);
        Vector2 max = Vector2.Max(localA, localB);

        _rect.gameObject.SetActive(true);
        _rect.SetAsLastSibling();
        _rect.anchoredPosition = min;
        _rect.sizeDelta = max - min;
    }

    void HideRect()
    {
        if (_rect != null)
            _rect.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Floating action card
    // ------------------------------------------------------------------

    void ShowPopup(Vector2 screenPos)
    {
        if (!EnsureCanvas())
            return;
        // Colors are baked at build time; a theme toggle just rebuilds the card.
        if (_popup != null && _popupBuiltDark != UIThemeController.IsDarkTheme)
        {
            Destroy(_popup.gameObject);
            _popup = null;
        }
        if (_popup == null)
            BuildPopup();

        if (_popupLabel != null)
            _popupLabel.text = _selected.Count == 1
                ? "1 part selected"
                : $"{_selected.Count} parts selected";

        // The Length button joins the row only for a single vertical frame;
        // the card stretches and the later buttons slide over to make room.
        bool withLength = CanResizeSelection();
        if (_btnLength != null)
            _btnLength.gameObject.SetActive(withLength);
        if (_btnDelete != null)
            _btnDelete.anchoredPosition = new Vector2(withLength ? 150f : 64f, 12f);
        if (_btnDeselect != null)
            _btnDeselect.anchoredPosition = new Vector2(withLength ? 202f : 116f, 12f);
        _popup.sizeDelta = new Vector2(withLength ? 258f : 172f, 96f);

        Vector2 local = ScreenToCanvasLocal(screenPos) + new Vector2(18f, 18f);

        // Clamp inside the canvas so the card is always reachable.
        Rect canvasRect = ((RectTransform)_canvas.transform).rect;
        Vector2 size = _popup.sizeDelta;
        local.x = Mathf.Clamp(local.x, canvasRect.xMin + 8f, canvasRect.xMax - size.x - 8f);
        local.y = Mathf.Clamp(local.y, canvasRect.yMin + 8f, canvasRect.yMax - size.y - 8f);

        _popup.gameObject.SetActive(true);
        _popup.SetAsLastSibling();
        _popup.anchoredPosition = local;
    }

    void HidePopup()
    {
        if (_popup != null)
            _popup.gameObject.SetActive(false);
    }

    bool _popupBuiltDark;

    void BuildPopup()
    {
        _popupBuiltDark = UIThemeController.IsDarkTheme;
        AdoptCardStyle();

        var go = new GameObject("SelectionActions", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _popup = (RectTransform)go.transform;
        _popup.anchorMin = _popup.anchorMax = new Vector2(0.5f, 0.5f);
        _popup.pivot = Vector2.zero;
        _popup.sizeDelta = new Vector2(252f, 96f);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        StyleCard(img, 1.4f);

        UiPolish.SoftShadow(_popup);

        _popupLabel = CreateText(_popup, "Label", "0 parts selected", 13.5f, Ink, true);
        var labelRt = _popupLabel.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.offsetMin = new Vector2(14f, -34f);
        labelRt.offsetMax = new Vector2(-14f, -10f);

        // Copy and Delete read instantly as glyphs (sheets / bin); Length has
        // no unambiguous icon, so it keeps its word.
        CreatePopupButton("Btn_Copy", "Copy", Accent, Color.white, 12f, 44f, OnCopyClicked, "Copy");
        _btnLength = CreatePopupButton("Btn_Length", "Length", Ink, Surface, 64f, 78f, OnLengthClicked);
        _btnDelete = CreatePopupButton("Btn_Delete", "Delete", Danger, Color.white, 64f, 44f, DeleteSelection, "Trash");
        // "×" not "✕": the dingbat is missing from the UI font.
        _btnDeselect = CreatePopupButton("Btn_Deselect", "×", Ink, Surface, 116f, 44f, OnDeselectClicked);

        _popup.gameObject.SetActive(false);
    }

    RectTransform _btnLength;
    RectTransform _btnDelete;
    RectTransform _btnDeselect;

    RectTransform CreatePopupButton(string name, string label, Color bg, Color fg,
        float x, float width, UnityEngine.Events.UnityAction onClick, string icon = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_popup, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, 12f);
        rt.sizeDelta = new Vector2(width, 40f);

        var img = go.GetComponent<Image>();
        img.color = bg;
        StyleCard(img, 1.8f);

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        button.onClick.AddListener(onClick);

        Sprite glyph = icon != null ? UIIcons.Get(icon) : null;
        if (glyph != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(20f, 20f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = glyph;
            iconImg.color = fg;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            return rt;
        }

        var text = CreateText(rt, "Text", label, 14f, fg, true);
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return rt;
    }

    TextMeshProUGUI CreateText(RectTransform parent, string name, string value,
        float size, Color color, bool semiBoldish)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        if (_font != null)
            tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (semiBoldish)
            tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    void StyleCard(Image img, float ppu)
    {
        if (_cardSprite == null)
            return;
        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpu * ppu;
    }

    void AdoptCardStyle()
    {
        Transform partsPanel = _canvas.transform.Find("PartsPanel");
        if (partsPanel == null)
            return;

        var img = partsPanel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpu = img.pixelsPerUnitMultiplier;
        }

        var text = partsPanel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _font = text.font;
    }

    bool EnsureCanvas()
    {
        if (_canvas != null)
            return true;
        _canvas = FindFirstObjectByType<Canvas>();
        return _canvas != null;
    }

    Vector2 ScreenToCanvasLocal(Vector2 screen)
    {
        var canvasRt = (RectTransform)_canvas.transform;
        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, uiCam, out Vector2 local);
        return local;
    }
}
