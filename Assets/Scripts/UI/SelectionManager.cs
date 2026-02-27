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