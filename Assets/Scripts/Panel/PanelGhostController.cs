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

    // Crisp perimeter outline: the translucent slab alone can wash out over a
    // light floor, so a thin theme-ink frame keeps the boundary readable.
    private LineRenderer _edge;
    private Material _edgeMaterial;

    void Start()
    {
        EnsureGhost();
        ApplyPanelToolState(false);
    }

    void Update()
    {
        if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
        {
            if (panelToolEnabled)
                DisablePanelTool();
            HideGhost();
            return;
        }

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

        // A copied structure following the cursor (or a beam length scale) owns clicks.
        if (StructureClipboard.StampingActive || BeamResizeSession.Busy)
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
        Vector3 n = (slot.normal.sqrMagnitude > 1e-6f)
            ? slot.normal.normalized
            : (slot.slotTrigger != null ? slot.slotTrigger.forward : Vector3.forward);
        Vector3 toCam = (cam.transform.position - slot.center);
        int side = (Vector3.Dot(toCam, n) >= 0f) ? +1 : -1;

        bool canPlace = slotManager.CanPlacePanel(slot, side);

        // Preview uses the exact same pose/size math as real panel placement.
        slotManager.GetPanelPlacement(slot, side, out Vector3 pos, out Quaternion rot, out Vector3 scale);

        _ghost.transform.SetPositionAndRotation(pos, rot);
        _ghost.transform.localScale = scale;

        _ghost.SetActive(true);
        ApplyGhostMaterial(canPlace ? validMat : invalidMat);
        ShowEdge(pos, rot, scale, canPlace);

        // Any size may be placed (design freedom); non-catalogue openings
        // carry a "(custom)" note in the panel name.
        string panelName = slotManager.PanelNameForSlot(slot, out _, out _);
        string sideLabel = (side > 0) ? "+side" : "-side";
        string info = canPlace
            ? $"{panelName} | {sideLabel} | {scale.x:0.###} x {scale.y:0.###}"
            : $"Panel BLOCKED (this side occupied) | {slot.slotId} | {sideLabel}";
        PushStatus(true, canPlace, info);

        // Place panel on a clean click release only — dragging is reserved for
        // the marquee selection and the panel layer mover. A claimed press
        // means the click was on an existing panel (selection), not placement.
        if (canPlace && LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
            slotManager.PlacePanel(slot, side);
    }

    // ---------------------------
    // UI BUTTON API (call these from your Panel UI button)
    // ---------------------------
    public void EnablePanelTool()
    {
        // Picking up the panel tool drops whatever was selected/copied first.
        MarqueeSelectionController.CancelPending();

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
            // Cache the last beam selection, including twist beams.
            if (BeamPartUtility.IsBeam(buildController.currentPartId))
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

        if (_edge == null)
        {
            var go = new GameObject("PanelGhostEdge");
            go.layer = ghostLayer;
            _edge = go.AddComponent<LineRenderer>();
            _edgeMaterial = new Material(Shader.Find("Sprites/Default"));
            _edge.sharedMaterial = _edgeMaterial;
            _edge.useWorldSpace = true;
            _edge.loop = true;
            _edge.positionCount = 0;
            _edge.numCornerVertices = 2;
            _edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _edge.receiveShadows = false;
            go.SetActive(false);
        }
    }

    /// <summary>
    /// Outline hugging the ghost's front face (on the camera side, so the
    /// slab can't occlude it), ink when placeable and danger-toned when not.
    /// </summary>
    void ShowEdge(Vector3 pos, Quaternion rot, Vector3 scale, bool valid)
    {
        if (_edge == null) return;

        Vector3 right = rot * Vector3.right * (scale.x * 0.5f);
        Vector3 up = rot * Vector3.up * (scale.y * 0.5f);
        Vector3 n = rot * Vector3.forward;
        float towardCam = (cam != null && Vector3.Dot(cam.transform.position - pos, n) < 0f) ? -1f : 1f;
        Vector3 lift = n * (towardCam * (scale.z * 0.5f + NeospaceUnits.Mm(2f)));

        float width = NeospaceUnits.Mm(6f);
        _edge.startWidth = width;
        _edge.endWidth = width;

        Color c = valid ? UIThemeController.InkColor : UIThemeController.DangerColor;
        c.a = 0.85f;
        _edge.startColor = c;
        _edge.endColor = c;

        _edge.positionCount = 4;
        _edge.SetPosition(0, pos + lift - right - up);
        _edge.SetPosition(1, pos + lift + right - up);
        _edge.SetPosition(2, pos + lift + right + up);
        _edge.SetPosition(3, pos + lift - right + up);
        _edge.gameObject.SetActive(true);
    }

    void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
        if (_edge != null) _edge.gameObject.SetActive(false);
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
