using UnityEngine;
using UnityEngine.EventSystems;

public class GhostController : MonoBehaviour
{
    [Header("References")]
    public BuildController buildController;
    public PartDatabase partDatabase;

    [Tooltip("Ghost layer mask (should include only the Ghost layer).")]
    public LayerMask ghostLayerMask;

    [Header("Ghost Materials")]
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Behavior")]
    public bool showGhostWhenNoSelection = false;

    private GameObject _ghostInstance;
    private string _activePartId;
    private GameObject _activePrefabUsed;
    private Renderer[] _ghostRenderers;

    void Awake()
    {
        if (partDatabase != null)
            partDatabase.RebuildCache();

        if (buildController != null && buildController.partDatabase == null)
            buildController.partDatabase = partDatabase;
    }

    void Update()
    {
        if (buildController == null)
            return;

        string partId = buildController.currentPartId;

        if (string.IsNullOrEmpty(partId))
        {
            if (!showGhostWhenNoSelection)
                HideGhost();
            return;
        }

        partId = partId.Trim();

        EnsureGhostForPart(partId);

        if (_ghostInstance == null)
            return;

        var res = buildController.ComputeGhostPlacement(partId, _ghostInstance);

        if (!res.hasPose)
        {
            _ghostInstance.SetActive(false);
            ApplyGhostMaterial(false);
            buildController.SetGhostResult(res, false);
            return;
        }

        _ghostInstance.SetActive(true);
        _ghostInstance.transform.SetPositionAndRotation(res.position, res.rotation);

        bool isValid = res.isValid;
        ApplyGhostMaterial(isValid);
        buildController.SetGhostResult(res, isValid);

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (isValid)
            {
                buildController.CommitPlacementFromGhost(partId, res);

                var post = buildController.ComputeGhostPlacement(partId, _ghostInstance);

                if (!post.hasPose)
                {
                    _ghostInstance.SetActive(false);
                    ApplyGhostMaterial(false);
                    buildController.SetGhostResult(post, false);
                    return;
                }

                _ghostInstance.SetActive(true);
                _ghostInstance.transform.SetPositionAndRotation(post.position, post.rotation);
                ApplyGhostMaterial(post.isValid);
                buildController.SetGhostResult(post, post.isValid);
            }
        }
    }

    void EnsureGhostForPart(string partId)
    {
        if (partDatabase == null)
        {
            Debug.LogWarning("GhostController: partDatabase not assigned.");
            HideGhost();
            return;
        }

        partDatabase.RebuildCache();

        GameObject desiredPrefab = partDatabase.GetGhostPrefabOrFallback(partId);

        // Special-case fallback for PANEL: create a runtime ghost so the tool can still pose/validate.
        if (desiredPrefab == null && partId.Equals("PANEL", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureRuntimePanelGhost(partId);
            return;
        }

        if (desiredPrefab == null)
        {
            Debug.LogWarning($"GhostController: No prefab found in PartDatabase for partId={partId}");
            HideGhost();
            return;
        }

        bool needsRecreate =
            _ghostInstance == null ||
            _activePartId != partId ||
            _activePrefabUsed != desiredPrefab;

        if (!needsRecreate)
            return;

        HideGhost();

        _activePartId = partId;
        _activePrefabUsed = desiredPrefab;

        _ghostInstance = Instantiate(desiredPrefab);
        _ghostInstance.name = $"{partId}_GhostInstance";
        _ghostInstance.SetActive(false);

        int ghostLayer = LayerMaskToLayerIndex(ghostLayerMask);
        if (ghostLayer >= 0)
            SetLayerRecursively(_ghostInstance.transform, ghostLayer);

        _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>(true);
        ApplyGhostMaterial(false);

        Debug.Log($"GhostController: partId=[{partId}] usingPrefab=[{desiredPrefab.name}]");
    }

    void EnsureRuntimePanelGhost(string partId)
    {
        bool needsRecreate = _ghostInstance == null || _activePartId != partId || _activePrefabUsed != null;
        if (!needsRecreate) return;

        HideGhost();

        _activePartId = partId;
        _activePrefabUsed = null;

        _ghostInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghostInstance.name = $"{partId}_GhostInstance_Runtime";
        _ghostInstance.SetActive(false);

        // Disable collider so it never interferes with placement/raycasting.
        var col = _ghostInstance.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        int ghostLayer = LayerMaskToLayerIndex(ghostLayerMask);
        if (ghostLayer >= 0)
            _ghostInstance.layer = ghostLayer;

        _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>(true);
        ApplyGhostMaterial(false);

        Debug.Log($"GhostController: partId=[{partId}] usingRuntimeGhost=[Cube]");
    }

    void HideGhost()
    {
        if (_ghostInstance != null)
            Destroy(_ghostInstance);

        _ghostInstance = null;
        _ghostRenderers = null;
        _activePartId = null;
        _activePrefabUsed = null;
    }

    void ApplyGhostMaterial(bool valid)
    {
        if (_ghostRenderers == null) return;

        Material mat = valid ? validMaterial : invalidMaterial;
        if (mat == null) return;

        for (int i = 0; i < _ghostRenderers.Length; i++)
        {
            if (_ghostRenderers[i] == null) continue;
            _ghostRenderers[i].sharedMaterial = mat;
        }
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    static int LayerMaskToLayerIndex(LayerMask mask)
    {
        int v = mask.value;
        if (v == 0) return -1;
        if ((v & (v - 1)) != 0) return -1;

        int idx = 0;
        while (v > 1)
        {
            v >>= 1;
            idx++;
        }
        return idx;
    }
}