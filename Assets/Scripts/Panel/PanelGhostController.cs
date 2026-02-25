using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PanelGhostController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public PanelSlotManager slotManager;
    public BuildController buildController;

    [Header("Raycast")]
    public LayerMask slotTriggerMask;

    [Header("Ghost")]
    public GameObject panelGhostPrefab;
    public int ghostLayer;
    public Material validMat;
    public Material invalidMat;

    [Header("Tool State")]
    public bool panelToolEnabled = false;

    [Header("UI Blocking")]
    public bool blockWhenPointerOverUI = true;

    [Header("Panel PartId")]
    public string panelPartId = "PANEL";

    private GameObject _ghost;
    private Renderer[] _ghostRenderers;
    private string _cachedFramePartId;

    void Start()
    {
        EnsureGhost();
        ApplyPanelToolState(false);
    }

    void Update()
    {
        // If user changed part away from PANEL (by clicking V/H in palette), auto-disable panel tool.
        if (panelToolEnabled && buildController != null && buildController.currentPartId != panelPartId)
        {
            panelToolEnabled = false;
            HideGhost();
            PushStatus(false, false, "Panel tool disabled (part changed)");
            return;
        }

        if (!panelToolEnabled)
        {
            HideGhost();
            return;
        }

        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Build)
        {
            HideGhost();
            PushStatus(false, false, "Panel tool requires Build mode");
            return;
        }

        if (buildController == null || buildController.currentPartId != panelPartId)
        {
            HideGhost();
            PushStatus(false, false, "Panel tool not selected");
            return;
        }

        if (cam == null || slotManager == null)
        {
            HideGhost();
            PushStatus(false, false, "Missing Camera or SlotManager");
            return;
        }

        if (blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HideGhost();
            PushStatus(false, false, "Pointer over UI");
            return;
        }

        EnsureGhost();

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, slotTriggerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            HideGhost();
            PushStatus(false, false, "Move cursor onto a closed slot");
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PanelSlotHandle slot = null;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            var candidate = h.collider.GetComponentInParent<PanelSlotHandle>();
            if (candidate != null)
            {
                slot = candidate;
                break;
            }
        }

        if (slot == null)
        {
            HideGhost();
            PushStatus(false, false, "No slot hit (panel may be blocking). Check slotTriggerMask layers.");
            return;
        }

        // Determine side based on camera position relative to slot plane
        Vector3 n = slot.normal.normalized;
        Vector3 toCam = (cam.transform.position - slot.center);
        float d = Vector3.Dot(toCam, n);
        int side = (d >= 0f) ? +1 : -1;

        bool canPlace = slotManager.CanPlacePanel(slot, side);

        float innerW = Mathf.Max(0.01f, slot.sizeXY.x );
        float innerH = Mathf.Max(0.01f, slot.sizeXY.y );

        float offset = (slotManager.frameThickness * 0.5f) +
                       (slotManager.panelThickness * 0.5f) +
                       slotManager.panelGap +
                       slotManager.panelOutset;

        Vector3 pos = slot.center + n * (side > 0 ? offset : -offset);

        Vector3 yAxis = slot.upAxis.normalized;
        Quaternion rot = Quaternion.LookRotation(n, (slot.corner3 - slot.corner0).normalized);
        
        _ghost.transform.SetPositionAndRotation(pos, rot);

        // Visual thickness only if your ghost prefab has depth
        _ghost.transform.localScale = new Vector3(innerW, innerH, slotManager.panelThickness);

        _ghost.SetActive(true);
        ApplyGhostMaterial(canPlace ? validMat : invalidMat);

        string sideLabel = (side > 0) ? "+side" : "-side";
        string info = canPlace
            ? $"Panel OK | {slot.slotId} | {sideLabel} | {innerW:0.###} x {innerH:0.###}"
            : $"Panel BLOCKED (this side occupied) | {slot.slotId} | {sideLabel}";
        PushStatus(true, canPlace, info);

        // Place panel with Left Click only (no right-click delete anymore)
        if (canPlace && Input.GetMouseButtonDown(0))
            slotManager.PlacePanel(slot, side);
    }

    // ---------------------------
    // UI BUTTON API (call these from your Panel UI button)
    // ---------------------------
    public void EnablePanelTool()
    {
        panelToolEnabled = true;
        ApplyPanelToolState(true);
    }

    public void DisablePanelTool()
    {
        panelToolEnabled = false;
        ApplyPanelToolState(false);
    }

    public void TogglePanelTool()
    {
        panelToolEnabled = !panelToolEnabled;
        ApplyPanelToolState(panelToolEnabled);
    }

    // ---------------------------
    // Internal
    // ---------------------------
    void ApplyPanelToolState(bool enabled)
    {
        if (buildController == null) return;

        if (enabled)
        {
            // Cache last frame selection only if it looks like V/H
            if (!string.IsNullOrEmpty(buildController.currentPartId) &&
                (buildController.currentPartId.StartsWith("V") || buildController.currentPartId.StartsWith("H")))
            {
                _cachedFramePartId = buildController.currentPartId;
            }

            UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;
            buildController.SetCurrentPart(panelPartId);

            PushStatus(false, false, "Panel tool enabled. Hover a closed slot.");
        }
        else
        {
            HideGhost();

            // Only restore cached frame id if we are currently on PANEL
            if (buildController.currentPartId == panelPartId && !string.IsNullOrEmpty(_cachedFramePartId))
                buildController.SetCurrentPart(_cachedFramePartId);

            PushStatus(false, false, "Panel tool disabled.");
        }
    }

    void EnsureGhost()
    {
        if (_ghost != null) return;
        if (panelGhostPrefab == null) return;

        _ghost = Instantiate(panelGhostPrefab);
        _ghost.name = "PanelGhost";
        SetLayerRecursively(_ghost, ghostLayer);

        var cols = _ghost.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
        _ghost.SetActive(false);
    }

    void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
    }

    void ApplyGhostMaterial(Material m)
    {
        if (m == null || _ghostRenderers == null) return;
        for (int i = 0; i < _ghostRenderers.Length; i++)
        {
            if (_ghostRenderers[i] == null) continue;
            _ghostRenderers[i].sharedMaterial = m;
        }
    }

    void PushStatus(bool hasPose, bool isValid, string info)
    {
        if (buildController == null) return;

        var res = new BuildController.GhostPlacementResult
        {
            hasPose = hasPose,
            isValid = isValid,
            debugInfo = info
        };
        buildController.SetGhostResult(res, isValid);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}