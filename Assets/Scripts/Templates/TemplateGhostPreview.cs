using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live ghost parts for Guided templates: shows semi-transparent copies of the
/// parts a template is about to place (e.g. the V posts of the Posts tool) at
/// their exact final positions. Instances are pooled per part id, stripped of
/// colliders/attachment points, moved to the ghost layer and tinted with the
/// Expert ghost material so they can never interfere with picking or physics.
/// </summary>
public class TemplateGhostPreview : MonoBehaviour
{
    public BuildController buildController;

    Material _ghostMaterial;
    bool _materialResolved;

    readonly Dictionary<string, Stack<GameObject>> _pool = new Dictionary<string, Stack<GameObject>>();
    readonly List<(string id, GameObject go)> _active = new List<(string, GameObject)>();
    readonly Dictionary<GameObject, Renderer[]> _renderers = new Dictionary<GameObject, Renderer[]>();

    /// <summary>Show ghosts for exactly these poses (hides everything else).</summary>
    public void Show(IList<TemplatePartPose> parts)
    {
        Hide();
        if (parts == null)
            return;

        for (int i = 0; i < parts.Count; i++)
        {
            TemplatePartPose pose = parts[i];
            if (string.IsNullOrWhiteSpace(pose.PartId))
                continue;

            string partId = pose.PartId.Trim();
            GameObject go = Rent(partId);
            if (go == null)
                continue;

            go.SetActive(true);
            go.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            // Only posts get floor-seated (same rule as the batch placement);
            // horizontal beams keep their exact height.
            if (BeamPartUtility.IsVertical(partId))
                SeatOnFloor(go, pose.Position, pose.Rotation);
            _active.Add((partId, go));
        }
    }

    public void Hide()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            (string id, GameObject go) = _active[i];
            if (go == null)
                continue;
            go.SetActive(false);
            if (!_pool.TryGetValue(id, out Stack<GameObject> stack))
                _pool[id] = stack = new Stack<GameObject>();
            stack.Push(go);
        }
        _active.Clear();
    }

    GameObject Rent(string partId)
    {
        if (_pool.TryGetValue(partId, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject pooled = stack.Pop();
                if (pooled != null)
                    return pooled;
            }
        }
        return Create(partId);
    }

    GameObject Create(string partId)
    {
        if (buildController == null || buildController.partDatabase == null)
            return null;

        GameObject prefab = buildController.partDatabase.GetGhostPrefabOrFallback(partId);
        if (prefab == null)
            return null;

        GameObject go = Instantiate(prefab, transform);
        go.name = $"{partId}_TemplatePreview";

        // A preview must be completely inert: no physics, no attachment points
        // (slot/hole scans must never see it), no beam bookkeeping. Immediate
        // destruction so no scan can catch them later this frame.
        foreach (AttachmentPoint ap in go.GetComponentsInChildren<AttachmentPoint>(true))
            DestroyImmediate(ap);
        foreach (BeamConnections bc in go.GetComponentsInChildren<BeamConnections>(true))
            DestroyImmediate(bc);
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(col);

        int ghostLayer = FirstLayerIndex(buildController.ghostLayerMask);
        if (ghostLayer >= 0)
            SetLayerRecursively(go.transform, ghostLayer);

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        _renderers[go] = renderers;

        Material mat = ResolveGhostMaterial();
        if (mat != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sharedMaterial = mat;
            }
        }

        go.SetActive(false);
        return go;
    }

    /// <summary>Reuse the Expert ghost's "valid placement" material for a consistent look.</summary>
    Material ResolveGhostMaterial()
    {
        if (_materialResolved)
            return _ghostMaterial;

        _materialResolved = true;
        var ghostController = FindFirstObjectByType<GhostController>(FindObjectsInactive.Include);
        if (ghostController != null)
            _ghostMaterial = ghostController.validMaterial;
        return _ghostMaterial;
    }

    /// <summary>
    /// Same floor seating the batch placement applies to real posts: drop the
    /// renderer bounds onto the floor hit under the planned position.
    /// </summary>
    void SeatOnFloor(GameObject go, Vector3 position, Quaternion rotation)
    {
        if (buildController == null)
            return;

        Vector3 origin = position + Vector3.up * 5f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f,
                buildController.floorMask, QueryTriggerInteraction.Collide))
            return;

        if (!_renderers.TryGetValue(go, out Renderer[] renderers) || renderers == null || renderers.Length == 0)
            return;

        bool any = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
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
        if (!any)
            return;

        float targetMinY = hit.point.y + Mathf.Max(0f, buildController.firstSurfaceClearance);
        float deltaY = targetMinY - bounds.min.y;
        if (float.IsNaN(deltaY) || float.IsInfinity(deltaY))
            return;

        go.transform.SetPositionAndRotation(position + Vector3.up * deltaY, rotation);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    static int FirstLayerIndex(LayerMask mask)
    {
        int v = mask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((v & (1 << i)) != 0)
                return i;
        }
        return -1;
    }
}
