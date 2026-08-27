using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public LayerMask selectionRayMask = ~0;

    [Header("Panels")]
    public PanelSlotManager panelSlotManager;

    [Header("Drag Cycling")]
    [Tooltip("Mouse movement in pixels before a drag counts as a cycle step.")]
    public float dragThreshold = 20f;

    // Multi-selection list (works for beams + panels because both use SelectableBeam)
    private readonly List<SelectableBeam> _selected = new List<SelectableBeam>();

    private Vector3 _dragStartPos = Vector3.zero;
    private bool _isDragging = false;

    void Update()
    {
        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Select)
            return;

        // Click to select
        if (Input.GetMouseButtonDown(0))
        {
            _dragStartPos = Input.mousePosition;
            _isDragging = true;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            TrySelectUnderCursor(additive);
        }

        // Drag cycling on mouse hold
        if (Input.GetMouseButton(0) && _isDragging)
        {
            Vector3 dragDelta = Input.mousePosition - _dragStartPos;

            if (dragDelta.magnitude > dragThreshold &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                bool horizontal = Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y);
                int direction = horizontal
                    ? (dragDelta.x > 0 ? 1 : -1)
                    : (dragDelta.y > 0 ? 1 : -1);

                _dragStartPos = Input.mousePosition;
                CycleSelectedConnection(direction);
            }
        }

        if (Input.GetMouseButtonUp(0))
            _isDragging = false;

        // Delete selected
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            DeleteSelectedAll();

        // Cycle selected beam's hole assignment
        if (Input.GetKeyDown(KeyCode.E))
            CycleSelectedConnection(1);
        else if (Input.GetKeyDown(KeyCode.Q))
            CycleSelectedConnection(-1);
    }

    /// <summary>
    /// Tries V-style cycling first, then H-style, like the original Q/E behavior.
    /// </summary>
    void CycleSelectedConnection(int direction)
    {
        if (!TryCycleSingleHoleConnection(direction, verticalSelected: true))
            TryCycleSingleHoleConnection(direction, verticalSelected: false);
    }

    /// <summary>
    /// Moves the selected beam so its single connected hole steps to the next/previous
    /// hole in the same hole family. Works when exactly one beam is selected and that
    /// beam has exactly one paired hole (connected to a peg on another beam).
    /// </summary>
    bool TryCycleSingleHoleConnection(int direction, bool verticalSelected)
    {
        if (direction == 0) return false;
        if (_selected.Count != 1) return false;

        var selected = _selected[0];
        if (selected == null) return false;

        Transform selectedRoot = selected.transform.root;
        bool rootMatches = verticalSelected
            ? BeamPartUtility.IsVertical(selectedRoot.name)
            : BeamPartUtility.IsHorizontalLike(selectedRoot.name);
        if (!rootMatches) return false;

        AttachmentPoint[] aps = selectedRoot.GetComponentsInChildren<AttachmentPoint>(true);
        if (aps == null || aps.Length == 0) return false;

        var ownHoles = new List<AttachmentPoint>();
        AttachmentPoint connectedHole = null;
        AttachmentPoint connectedPeg = null;
        int pairedHoleCount = 0;

        for (int i = 0; i < aps.Length; i++)
        {
            AttachmentPoint ap = aps[i];
            if (ap == null) continue;

            if (ap.role == AttachmentPoint.PointRole.Peg)
            {
                // Own pegs must not be connected; other beams may hang off them.
                if (ap.pairedWith != null)
                    return false;

                continue;
            }

            if (ap.role != AttachmentPoint.PointRole.Hole)
                continue;

            ownHoles.Add(ap);

            if (ap.pairedWith == null)
                continue;

            pairedHoleCount++;
            if (pairedHoleCount > 1)
                return false;

            AttachmentPoint pair = ap.pairedWith;
            if (pair.role != AttachmentPoint.PointRole.Peg)
                return false;

            Transform otherRoot = pair.transform.root;
            if (otherRoot == null || otherRoot == selectedRoot)
                return false;

            // Only cycle along pegs that live on an H/T beam (pegs only exist there).
            if (!BeamPartUtility.IsHorizontalLike(otherRoot.name))
                return false;

            connectedHole = ap;
            connectedPeg = pair;
        }

        if (connectedHole == null || connectedPeg == null) return false;
        if (pairedHoleCount != 1) return false;

        string connectedHoleGroup = ConnectorNameUtility.GetGroupKey(connectedHole.name);
        if (string.IsNullOrEmpty(connectedHoleGroup))
            return false;

        var candidateHoles = new List<AttachmentPoint>();
        for (int i = 0; i < ownHoles.Count; i++)
        {
            AttachmentPoint hole = ownHoles[i];
            if (hole == null) continue;

            // Stay inside the same hole family (e.g. "AP_Hole_A1 (...)" only).
            if (!string.Equals(
                    ConnectorNameUtility.GetGroupKey(hole.name),
                    connectedHoleGroup,
                    StringComparison.Ordinal))
                continue;

            if (hole == connectedHole || hole.pairedWith == null)
                candidateHoles.Add(hole);
        }

        if (candidateHoles.Count <= 1)
            return false;

        candidateHoles.Sort(CompareAttachmentPointsByConnectorName);

        int currentIndex = candidateHoles.IndexOf(connectedHole);
        if (currentIndex < 0)
            return false;

        // Clamped stepping: cycling stops at the first/last hole instead of wrapping.
        int nextIndex = Mathf.Clamp(currentIndex + (direction > 0 ? 1 : -1), 0, candidateHoles.Count - 1);
        if (nextIndex == currentIndex)
            return false;

        AttachmentPoint nextHole = candidateHoles[nextIndex];
        if (nextHole == null || nextHole == connectedHole)
            return false;

        // Shift the whole beam so the next hole lands on the peg.
        Vector3 delta = connectedPeg.transform.position - nextHole.transform.position;
        selectedRoot.position += delta;
        Physics.SyncTransforms();

        connectedHole.pairedWith = null;
        connectedHole.isOccupied = false;
        connectedHole.occupant = null;

        nextHole.pairedWith = connectedPeg;
        nextHole.isOccupied = true;
        nextHole.occupant = selectedRoot.gameObject;

        connectedPeg.pairedWith = nextHole;
        connectedPeg.isOccupied = true;
        connectedPeg.occupant = connectedPeg.transform.root != null
            ? connectedPeg.transform.root.gameObject
            : null;

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        return true;
    }

    static int CompareAttachmentPointsByConnectorName(AttachmentPoint a, AttachmentPoint b)
    {
        string na = a != null ? a.name : string.Empty;
        string nb = b != null ? b.name : string.Empty;

        int groupCmp = string.Compare(
            ConnectorNameUtility.GetGroupKey(na),
            ConnectorNameUtility.GetGroupKey(nb),
            StringComparison.Ordinal);
        if (groupCmp != 0) return groupCmp;

        int idxCmp = ConnectorNameUtility.GetOrderIndex(na)
            .CompareTo(ConnectorNameUtility.GetOrderIndex(nb));
        if (idxCmp != 0) return idxCmp;

        return string.Compare(na, nb, StringComparison.Ordinal);
    }

    void TrySelectUnderCursor(bool additive)
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, selectionRayMask, QueryTriggerInteraction.Ignore))
        {
            if (!additive) ClearSelection();
            return;
        }

        var sel = hit.collider.GetComponentInParent<SelectableBeam>();

        if (sel == null)
        {
            if (!additive) ClearSelection();
            return;
        }

        if (!additive)
        {
            ClearSelection();
            AddSelection(sel);
            return;
        }

        // Shift = toggle
        if (_selected.Contains(sel))
            RemoveSelection(sel);
        else
            AddSelection(sel);
    }

    void AddSelection(SelectableBeam obj)
    {
        if (obj == null) return;
        if (_selected.Contains(obj)) return;

        _selected.Add(obj);
        obj.SetSelected(true);
    }

    void RemoveSelection(SelectableBeam obj)
    {
        if (obj == null) return;
        if (!_selected.Contains(obj)) return;

        obj.SetSelected(false);
        _selected.Remove(obj);
    }

    void ClearSelection()
    {
        for (int i = 0; i < _selected.Count; i++)
        {
            if (_selected[i] != null)
                _selected[i].SetSelected(false);
        }
        _selected.Clear();
    }

    void DeleteSelectedAll()
    {
        if (_selected.Count == 0) return;

        // Copy because Destroy changes objects
        var toDelete = new List<SelectableBeam>(_selected);
        ClearSelection();

        bool deletedAnything = false;

        for (int i = 0; i < toDelete.Count; i++)
        {
            var sel = toDelete[i];
            if (sel == null) continue;

            // If it's a panel, remove via PanelSlotManager so slot occupancy stays correct
            var pi = sel.GetComponent<PanelInstance>();
            if (pi != null)
            {
                if (panelSlotManager != null &&
                    panelSlotManager.TryGetSlot(pi.slotId, out var slot) &&
                    slot != null)
                {
                    panelSlotManager.RemovePanel(slot, pi.side);
                }
                else
                {
                    Destroy(sel.gameObject.transform.root.gameObject);
                }

                deletedAnything = true;
                continue;
            }

            // Otherwise treat as beam
            var conn = sel.GetComponent<BeamConnections>();
            if (conn != null) conn.ReleaseAll();

            Destroy(sel.gameObject.transform.root.gameObject);
            deletedAnything = true;
        }

        if (deletedAnything && panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        if (deletedAnything)
            BuildHistory.NotifyChanged();
    }
}
