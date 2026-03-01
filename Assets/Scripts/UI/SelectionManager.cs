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
    public PanelSlotManager panelSlotManager; // drag your PanelSlotManager here

    // Multi-selection list (works for beams + panels because both use SelectableBeam)
    private readonly List<SelectableBeam> _selected = new List<SelectableBeam>();

    void Update()
    {
        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Select)
            return;

        // Click to select
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            TrySelectUnderCursor(additive);
        }

        // Delete selected
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            DeleteSelectedAll();

        // Adjust selected H hole assignment
        if (Input.GetKeyDown(KeyCode.E))
            TryCycleSelectedHSingleHoleConnection(+1);
        else if (Input.GetKeyDown(KeyCode.Q))
            TryCycleSelectedHSingleHoleConnection(-1);

        // Same behavior on mouse wheel: up = next, down = previous.
        // Ignore when pointer is over UI to avoid conflicts with scrolling UI panels.
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0f)
                TryCycleSelectedHSingleHoleConnection(+1);
            else if (wheel < 0f)
                TryCycleSelectedHSingleHoleConnection(-1);
        }
    }

    bool TryCycleSelectedHSingleHoleConnection(int direction)
    {
        if (direction == 0) return false;
        if (_selected.Count != 1) return false;

        var selected = _selected[0];
        if (selected == null) return false;

        Transform selectedRoot = selected.transform.root;
        if (!IsHLikeRoot(selectedRoot)) return false;

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
                // Requirement: own pegs must not be connected/paired.
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

            // Requirement: only when connected to another H peg.
            if (!IsHLikeRoot(otherRoot))
                return false;

            connectedHole = ap;
            connectedPeg = pair;
        }

        if (connectedHole == null || connectedPeg == null) return false;
        if (pairedHoleCount != 1) return false;

        string connectedHoleGroup = GetConnectorGroupKey(connectedHole.name);
        if (string.IsNullOrEmpty(connectedHoleGroup))
            return false;

        var candidateHoles = new List<AttachmentPoint>();
        for (int i = 0; i < ownHoles.Count; i++)
        {
            AttachmentPoint hole = ownHoles[i];
            if (hole == null) continue;

            // Stay inside the same hole family (e.g. AP_Hole_A1 (...) only).
            if (!string.Equals(GetConnectorGroupKey(hole.name), connectedHoleGroup, StringComparison.Ordinal))
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

        int step = direction > 0 ? 1 : -1;
        int nextIndex = WrapIndex(currentIndex + step, candidateHoles.Count);
        if (nextIndex == currentIndex)
            return false;

        AttachmentPoint nextHole = candidateHoles[nextIndex];
        if (nextHole == null || nextHole == connectedHole)
            return false;

        Vector3 targetPegPos = connectedPeg.transform.position;
        Vector3 delta = targetPegPos - nextHole.transform.position;
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
        connectedPeg.occupant = connectedPeg.transform.root != null ? connectedPeg.transform.root.gameObject : null;

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        return true;
    }

    static bool IsHLikeRoot(Transform t)
    {
        if (t == null) return false;
        string n = t.name;
        return n.StartsWith("H", StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith("T", StringComparison.OrdinalIgnoreCase);
    }

    static int CompareAttachmentPointsByConnectorName(AttachmentPoint a, AttachmentPoint b)
    {
        string na = a != null ? a.name : string.Empty;
        string nb = b != null ? b.name : string.Empty;

        string ka = GetConnectorGroupKey(na);
        string kb = GetConnectorGroupKey(nb);

        int groupCmp = string.Compare(ka, kb, StringComparison.Ordinal);
        if (groupCmp != 0) return groupCmp;

        int ia = GetConnectorOrderIndex(na);
        int ib = GetConnectorOrderIndex(nb);
        int idxCmp = ia.CompareTo(ib);
        if (idxCmp != 0) return idxCmp;

        return string.Compare(na, nb, StringComparison.Ordinal);
    }

    static int WrapIndex(int idx, int count)
    {
        if (count <= 0) return 0;
        while (idx < 0) idx += count;
        while (idx >= count) idx -= count;
        return idx;
    }

    static string GetConnectorGroupKey(string connectorName)
    {
        if (string.IsNullOrEmpty(connectorName)) return string.Empty;

        int i = connectorName.IndexOf('(');
        if (i < 0) return connectorName.Trim();

        return connectorName.Substring(0, i).Trim();
    }

    static int GetConnectorOrderIndex(string connectorName)
    {
        if (string.IsNullOrEmpty(connectorName)) return int.MinValue;

        int open = connectorName.LastIndexOf('(');
        int close = connectorName.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            string inside = connectorName.Substring(open + 1, close - open - 1).Trim();
            if (int.TryParse(inside, out int parsedInParens))
                return parsedInParens;
        }

        int end = connectorName.Length - 1;
        while (end >= 0 && !char.IsDigit(connectorName[end])) end--;
        if (end < 0) return 0;

        int start = end;
        while (start >= 0 && char.IsDigit(connectorName[start])) start--;
        string digits = connectorName.Substring(start + 1, end - start);

        if (int.TryParse(digits, out int parsedTail))
            return parsedTail;

        return 0;
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
                continue;
            }
            if (panelSlotManager != null)
                panelSlotManager.RebuildConnectionsAndRescanSlots();
            // Otherwise treat as beam
            var conn = sel.GetComponent<BeamConnections>();
            if (conn != null) conn.ReleaseAll();

            Destroy(sel.gameObject.transform.root.gameObject);
        }
    }
}