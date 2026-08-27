using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel-mode layer editing: click a placed panel to select it, then drag up
/// or down to move the whole layer — the panel AND the beams that frame it
/// travel together along the posts, snapping only to hole rows where every
/// peg lands on a free hole. Works for wall panels (2 rails) and floor/roof
/// panels (4-beam ring). The move commits on release; the panel re-attaches
/// to the slot the beams form at the new height.
/// </summary>
public class PanelLayerMover : MonoBehaviour
{
    public Camera cam;
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;
    public PanelGhostController panelGhost;
    public TemplateSession templateSession;
    public TemplateGhostPreview ghostPreview;

    PanelInstance _selectedPanel;
    SelectableBeam _selectedHighlight;

    bool _pressOnPanel;
    bool _dragging;
    PanelSlotHandle _slot;
    readonly List<Transform> _beams = new List<Transform>();
    readonly List<SelectableBeam> _beamHighlights = new List<SelectableBeam>();
    readonly List<float> _validDeltas = new List<float>();
    float _pressScreenY;
    float _currentDelta;

    /// <summary>
    /// Layer moving is armed whenever ANY panel tool is in use:
    ///  - Expert: the palette's panel tool (checked via the shared
    ///    BuildController part id, so duplicate PanelGhostController
    ///    instances can't break the gate),
    ///  - Guided: the Panel (T3) template tool.
    /// </summary>
    bool PanelModeActive
    {
        get
        {
            if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
            {
                // Bootstrap order isn't guaranteed; resolve the session lazily.
                if (templateSession == null)
                    templateSession = FindFirstObjectByType<TemplateSession>();
                return templateSession != null &&
                       templateSession.ActiveTool == GuidedTemplateTool.PanelBayT3;
            }

            string panelId = panelGhost != null ? panelGhost.panelPartId : "PANEL";
            if (buildController != null &&
                string.Equals(buildController.currentPartId, panelId,
                    System.StringComparison.OrdinalIgnoreCase))
                return true;

            return panelGhost != null && panelGhost.panelToolEnabled;
        }
    }

    void Update()
    {
        if (StructureClipboard.StampingActive || !PanelModeActive)
        {
            ResetAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ResetAll();
            return;
        }

        if (LeftClickGesture.PressedThisFrame && !LeftClickGesture.PressStartedOverUI)
            HandlePress();

        if (_pressOnPanel && LeftClickGesture.IsDragging && !_dragging)
            BeginDrag();

        if (_dragging)
        {
            if (Input.GetMouseButton(0))
                UpdateDrag();
            else
                EndDrag(commit: true);
        }
        else if (_pressOnPanel && !Input.GetMouseButton(0))
        {
            // Plain click on a panel: it stays selected, waiting for a drag.
            _pressOnPanel = false;
        }
    }

    void HandlePress()
    {
        PanelInstance panel = PanelUnderCursor();
        if (panel == null)
        {
            // Pressing elsewhere drops the current panel selection.
            if (_selectedPanel != null && !LeftClickGesture.IsDragging)
            {
                DeselectPanel();
                SelectionStatus.Set("Panel deselected.", 2f);
            }
            _pressOnPanel = false;
            return;
        }

        // Own this press so the marquee leaves the gesture alone.
        LeftClickGesture.PressClaim = this;
        _pressOnPanel = true;
        _pressScreenY = Input.mousePosition.y;
        SelectPanel(panel);
        SelectionStatus.Set(
            "Panel selected · drag UP or DOWN to move this layer; its beams move with it.");
    }

    PanelInstance PanelUnderCursor()
    {
        if (cam == null)
            return null;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int mask = ~(buildController != null ? buildController.ghostLayerMask.value : 0);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, mask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return null;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Direct hit on a panel — but only the panel on the CAMERA side of
            // its slot may be click-selected. Hitting the far sheet through an
            // empty near side means the user is placing a panel on this side,
            // and claiming that press would swallow the placement click.
            var pi = hit.collider.GetComponentInParent<PanelInstance>();
            if (pi != null)
                return CameraSideOnly(pi);

            // Slot helper colliders (hover trigger + panel blocker) cover the
            // panel area and sit slightly IN FRONT of the panel surface, so
            // they intercept the ray first. Resolve them to the slot's panel
            // on the camera-facing side instead of giving up.
            var slot = hit.collider.GetComponentInParent<PanelSlotHandle>();
            if (slot != null)
            {
                PanelInstance resolved = PanelOnCameraSide(slot);
                if (resolved != null)
                    return resolved;
                continue; // this side is free: the click means "place here"
            }

            // Any other solid geometry (a beam, the floor) occludes panels.
            return null;
        }

        return null;
    }

    /// <summary>The slot's panel facing the camera — never the far side.</summary>
    PanelInstance PanelOnCameraSide(PanelSlotHandle slot)
    {
        Vector3 n = slot.normal.sqrMagnitude > 1e-6f ? slot.normal.normalized : Vector3.up;
        bool camOnPlus = Vector3.Dot(cam.transform.position - slot.center, n) >= 0f;

        GameObject panel = camOnPlus ? slot.panelPlus : slot.panelMinus;
        return panel != null ? panel.GetComponent<PanelInstance>() : null;
    }

    /// <summary>Keeps a directly hit panel only when it faces the camera in its slot.</summary>
    PanelInstance CameraSideOnly(PanelInstance pi)
    {
        if (panelSlotManager == null ||
            !panelSlotManager.TryGetSlot(pi.slotId, out PanelSlotHandle slot) || slot == null)
            return pi; // orphan panel: there is nothing to place there anyway

        return PanelOnCameraSide(slot) == pi ? pi : null;
    }

    void SelectPanel(PanelInstance panel)
    {
        if (_selectedPanel == panel)
            return;

        DeselectPanel();
        _selectedPanel = panel;

        _selectedHighlight = panel.GetComponent<SelectableBeam>();
        if (_selectedHighlight == null)
            _selectedHighlight = panel.gameObject.AddComponent<SelectableBeam>();
        if (_selectedHighlight.selectionMaterial == null)
        {
            foreach (SelectableBeam sel in FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
            {
                if (sel != null && sel.selectionMaterial != null)
                {
                    _selectedHighlight.selectionMaterial = sel.selectionMaterial;
                    break;
                }
            }
        }
        _selectedHighlight.SetSelected(true);
    }

    void DeselectPanel()
    {
        if (_selectedHighlight != null)
            _selectedHighlight.SetSelected(false);
        _selectedHighlight = null;
        _selectedPanel = null;
    }

    // ------------------------------------------------------------------
    // Drag lifecycle
    // ------------------------------------------------------------------

    void BeginDrag()
    {
        _dragging = true;
        _currentDelta = 0f;
        _beams.Clear();
        _validDeltas.Clear();
        _slot = null;

        if (_selectedPanel == null || panelSlotManager == null)
        {
            AbortDrag("No panel selected.");
            return;
        }

        if (!panelSlotManager.TryGetSlot(_selectedPanel.slotId, out _slot) || _slot == null)
        {
            AbortDrag("This panel has no live slot · move the beams around it first.");
            return;
        }

        int beamSearchMask = ~(buildController != null ? buildController.ghostLayerMask.value : 0);
        if (!CollectBoundaryBeams(_slot, _beams, beamSearchMask))
        {
            AbortDrag("Couldn't find the beams framing this panel.");
            return;
        }

        if (!BeamSlideRules.CollectValidDeltas(_beams, _validDeltas))
        {
            AbortDrag("These beams aren't connected to frames, so the layer can't slide.");
            return;
        }

        if (_validDeltas.Count <= 1)
        {
            AbortDrag("No other free hole rows on these frames · the layer can't move.");
            return;
        }

        foreach (Transform beam in _beams)
        {
            var sel = beam.GetComponent<SelectableBeam>();
            if (sel != null)
            {
                sel.SetSelected(true);
                _beamHighlights.Add(sel);
            }
        }
    }

    void AbortDrag(string reason)
    {
        _dragging = false;
        _pressOnPanel = false;
        SelectionStatus.Set(reason, 5f);
    }

    void UpdateDrag()
    {
        // Screen-space vertical motion → world metres at the slot's depth.
        float pixels = Input.mousePosition.y - _pressScreenY;
        float dist = Vector3.Distance(cam.transform.position, _slot.center);
        float worldPerPixel = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
        float rawDelta = pixels * worldPerPixel;

        // Snap to the nearest valid hole-row offset.
        float best = 0f;
        float bestErr = float.PositiveInfinity;
        foreach (float delta in _validDeltas)
        {
            float err = Mathf.Abs(delta - rawDelta);
            if (err < bestErr)
            {
                bestErr = err;
                best = delta;
            }
        }
        _currentDelta = best;

        if (ghostPreview != null)
        {
            if (Mathf.Abs(_currentDelta) > 0.0001f)
                ghostPreview.Show(BeamPosesAt(_currentDelta));
            else
                ghostPreview.Hide();
        }

        float mm = _currentDelta / Mathf.Max(NeospaceUnits.UnitsPerMm, 1e-9f);
        int modules = Mathf.RoundToInt(Mathf.Abs(mm) / 88f);
        SelectionStatus.Set(Mathf.Abs(_currentDelta) > 0.0001f
            ? $"Move layer {(mm >= 0 ? "up" : "down")} {Mathf.Abs(mm):0} mm ({modules} module{(modules == 1 ? "" : "s")}) · release to apply"
            : "Drag further up or down to reach the next free hole row");
    }

    void EndDrag(bool commit)
    {
        bool moved = commit && Mathf.Abs(_currentDelta) > 0.0001f;
        float delta = _currentDelta;

        _dragging = false;
        _pressOnPanel = false;
        _currentDelta = 0f;

        foreach (SelectableBeam sel in _beamHighlights)
        {
            if (sel != null)
                sel.SetSelected(false);
        }
        _beamHighlights.Clear();

        if (ghostPreview != null)
            ghostPreview.Hide();

        if (moved)
            CommitMove(delta);
        else
            SelectionStatus.Set("Layer unchanged.", 3f);

        DeselectPanel();
    }

    void CommitMove(float delta)
    {
        if (_slot == null)
        {
            SelectionStatus.Set("The panel's slot disappeared · nothing was moved.", 4f);
            return;
        }

        bool hadPlus = _slot.panelPlus != null;
        bool hadMinus = _slot.panelMinus != null;
        Vector3 oldCenter = _slot.center;
        Vector3 oldNormal = _slot.normal;

        panelSlotManager.RemovePanel(_slot, +1);
        panelSlotManager.RemovePanel(_slot, -1);

        foreach (Transform beam in _beams)
        {
            if (beam == null)
                continue;
            var conn = beam.GetComponent<BeamConnections>();
            if (conn != null)
                conn.ReleaseAll();
            beam.position += Vector3.up * delta;
        }
        Physics.SyncTransforms();
        panelSlotManager.RebuildConnectionsAndRescanSlots();

        PanelSlotHandle newSlot = StructureClipboard.FindSlotNear(oldCenter + Vector3.up * delta, oldNormal);
        int panelsBack = 0;
        if (newSlot != null)
        {
            bool flipped = Vector3.Dot(newSlot.normal, oldNormal) < 0f;
            int plusSide = flipped ? -1 : 1;
            if (hadPlus && panelSlotManager.CanPlacePanel(newSlot, plusSide) &&
                panelSlotManager.PlacePanel(newSlot, plusSide) != null)
                panelsBack++;
            if (hadMinus && panelSlotManager.CanPlacePanel(newSlot, -plusSide) &&
                panelSlotManager.PlacePanel(newSlot, -plusSide) != null)
                panelsBack++;
        }

        BuildHistory.NotifyChanged();

        float mm = Mathf.Abs(delta) / Mathf.Max(NeospaceUnits.UnitsPerMm, 1e-9f);
        SelectionStatus.Set(
            $"Layer moved {(delta >= 0 ? "up" : "down")} {mm:0} mm with {_beams.Count} beams" +
            (panelsBack > 0 ? $" and {panelsBack} panel{(panelsBack == 1 ? "" : "s")}." : "."), 5f);
    }

    void ResetAll()
    {
        if (_dragging)
            EndDrag(commit: false);
        if (_selectedPanel != null)
            DeselectPanel();
        _pressOnPanel = false;
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// The H beams framing a slot: wall slots have a bottom and a top rail,
    /// floor slots a 4-beam ring. Slot corners sit on the beams' center lines,
    /// so the beam nearest to each edge midpoint is the frame beam.
    /// </summary>
    static bool CollectBoundaryBeams(PanelSlotHandle slot, List<Transform> beams, int layerMask)
    {
        bool isWall = slot.slotId != null && slot.slotId.StartsWith("WALL", System.StringComparison.Ordinal);

        var midpoints = new List<Vector3>();
        if (isWall)
        {
            midpoints.Add((slot.corner0 + slot.corner1) * 0.5f); // bottom rail
            midpoints.Add((slot.corner2 + slot.corner3) * 0.5f); // top rail
        }
        else
        {
            midpoints.Add((slot.corner0 + slot.corner1) * 0.5f);
            midpoints.Add((slot.corner1 + slot.corner2) * 0.5f);
            midpoints.Add((slot.corner2 + slot.corner3) * 0.5f);
            midpoints.Add((slot.corner3 + slot.corner0) * 0.5f);
        }

        float radius = NeospaceUnits.ModuleMeters * 0.45f;
        foreach (Vector3 mid in midpoints)
        {
            Transform best = null;
            float bestDist = float.PositiveInfinity;

            foreach (Collider col in Physics.OverlapSphere(mid, radius, layerMask, QueryTriggerInteraction.Ignore))
            {
                Transform root = col.transform.root;
                if (root == null || !BeamPartUtility.IsHorizontalLike(root.name))
                    continue;

                float dist = Vector3.Distance(col.ClosestPoint(mid), mid);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = root;
                }
            }

            if (best == null)
                return false;
            if (!beams.Contains(best))
                beams.Add(best);
        }

        return beams.Count > 0;
    }

    List<TemplatePartPose> BeamPosesAt(float delta)
    {
        var poses = new List<TemplatePartPose>(_beams.Count);
        foreach (Transform beam in _beams)
        {
            if (beam == null)
                continue;
            string id = StructureClipboard.CleanPartId(beam.name);
            if (id == null)
                continue;
            poses.Add(new TemplatePartPose(
                id, beam.position + Vector3.up * delta, beam.rotation, "layer move"));
        }
        return poses;
    }
}
