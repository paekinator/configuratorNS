using System;
using System.Collections.Generic;
using UnityEngine;

public class VeneerManager : MonoBehaviour
{
    [Header("Refs")]
    public VeneerPrefabLibrary library;

    [Header("Root")]
    public Transform veneersRoot;

    [Header("Rules / Filters")]
    public bool excludeHorizontalFaces = true;
    [Range(0.0f, 1.0f)] public float horizontalDotThreshold = 0.90f;

    public bool excludeEndFaces = true;
    [Range(0.0f, 1.0f)] public float endFaceDotThreshold = 0.80f;

    [Header("Exposure Probe")]
    public float exposureProbeDistance = 0.05f;
    public float exposureProbeStartOffset = 0.0025f;

    [Header("Placement Offset")]
    public float outwardGap = 0.0015f;

    [Header("Panel Exclusion (Panel-facing sides)")]
    public bool excludePanelFacingSides = true;
    public float panelPlaneMaxDistance = 0.08f;
    public float panelRectPadding = 0.03f;

    [Header("Safety")]
    public bool disableAllVeneerColliders = true;

    [Header("Debug")]
    public bool logSummary = true;

    [ContextMenu("Apply Veneers")]
    public void ApplyVeneers()
    {
        SelectableBeam[] beams = FindAllObjects<SelectableBeam>();

        foreach (SelectableBeam sb in beams)
        {
            if (sb == null) continue;

            foreach (GameObject strip in sb.veneerStrips)
                if (strip != null) strip.SetActive(true);
        }
    //     if (library == null)
    //     {
    //         Debug.LogError("[VeneerManager] No VeneerPrefabLibrary assigned.");
    //         return;
    //     }

    //     EnsureRoot();
    //     ClearVeneersInternal();

    //     List<GameObject> beams = CollectPlacedBeams();
    //     List<PanelOBB> panels = excludePanelFacingSides ? CollectPanels() : new List<PanelOBB>();

    //     if (beams.Count == 0)
    //         Debug.LogWarning("[VeneerManager] No beams found. Check CollectPlacedBeams() filters (BeamConnections, names V/H, Ghost layer).");

    //     int stripsPlaced = 0;

    //     foreach (GameObject beam in beams)
    //     {
    //         if (beam == null) continue;

    //         Bounds rootLocalBounds;
    //         if (!TryGetRootLocalBounds(beam.transform, out rootLocalBounds))
    //             continue;

    //         Vector3 size = rootLocalBounds.size;

    //         Vector3 lengthAxisLocal = Vector3.right;
    //         float length = size.x;
    //         if (size.y >= size.x && size.y >= size.z) { lengthAxisLocal = Vector3.up; length = size.y; }
    //         else if (size.z >= size.x && size.z >= size.y) { lengthAxisLocal = Vector3.forward; length = size.z; }

    //         Vector3 lengthAxisWorld = beam.transform.TransformDirection(lengthAxisLocal).normalized;

    //         float halfLenTarget = Mathf.Max(0.0001f, length * 0.5f);

    //         int bestExteriorIndex = library.FindBestIndex(true, halfLenTarget);
    //         GameObject stripPrefab = library.GetStrip(true, bestExteriorIndex);
    //         float stripLen = library.GetStripLength(true, bestExteriorIndex);

    //         if (stripPrefab == null || stripLen <= 0f)
    //         {
    //             Debug.LogWarning(string.Format("[VeneerManager] Missing/invalid strip prefab length. best={0} prefab={1} len={2}",
    //                 bestExteriorIndex, stripPrefab ? stripPrefab.name : "NULL", stripLen));
    //             continue;
    //         }

    //         Vector3[] faceNormalsLocal =
    //         {
    //             Vector3.right, Vector3.left,
    //             Vector3.up, Vector3.down,
    //             Vector3.forward, Vector3.back
    //         };

    //         for (int i = 0; i < faceNormalsLocal.Length; i++)
    //         {
    //             Vector3 nLocal = faceNormalsLocal[i];
    //             Vector3 nWorld = beam.transform.TransformDirection(nLocal).normalized;

    //             if (excludeHorizontalFaces && Mathf.Abs(Vector3.Dot(nWorld, Vector3.up)) >= horizontalDotThreshold)
    //                 continue;

    //             if (excludeEndFaces && Mathf.Abs(Vector3.Dot(nWorld, lengthAxisWorld)) >= endFaceDotThreshold)
    //                 continue;

    //             Vector3 faceCenterWorld = beam.transform.TransformPoint(
    //                 rootLocalBounds.center + Vector3.Scale(nLocal, rootLocalBounds.extents)
    //             );

    //             if (excludePanelFacingSides && IsFacePanelExcluded(faceCenterWorld, nWorld, panels))
    //                 continue;

    //             if (!IsFaceExposed(beam.transform, faceCenterWorld, nWorld))
    //                 continue;

    //             Vector3 outWorld = nWorld;
    //             Vector3 faceBase = faceCenterWorld + outWorld * outwardGap;

    //             Vector3 c1 = faceBase - lengthAxisWorld * (stripLen * 0.5f);
    //             Vector3 c2 = faceBase + lengthAxisWorld * (stripLen * 0.5f);

    //             PlaceStripHalf(stripPrefab, c1, -lengthAxisWorld, outWorld);
    //             PlaceStripHalf(stripPrefab, c2, lengthAxisWorld, outWorld); // FIX: remove unary '+' (invalid for Vector3)

    //             stripsPlaced += 2;
    //         }
    //     }

    //     if (logSummary)
    //         Debug.Log(string.Format("[VeneerManager] Applied veneers. Beams={0}, Panels={1}, StripsPlaced={2}", beams.Count, panels.Count, stripsPlaced));
    }

    [ContextMenu("Clear Veneers")]
    public void ClearVeneers()
    {
        EnsureRoot();
        ClearVeneersInternal();
    }

    private void EnsureRoot()
    {
        if (veneersRoot != null) return;
        GameObject go = new GameObject("VeneersRoot");
        veneersRoot = go.transform;
    }

    private void ClearVeneersInternal()
    {
        if (veneersRoot == null) return;
        for (int i = veneersRoot.childCount - 1; i >= 0; i--)
            Destroy(veneersRoot.GetChild(i).gameObject);
    }

    // ------------------------
    // Beam collection
    // ------------------------

    private List<GameObject> CollectPlacedBeams()
    {
        List<GameObject> result = new List<GameObject>();

        BeamConnections[] conns = FindAllObjects<BeamConnections>();
        for (int i = 0; i < conns.Length; i++)
        {
            if (conns[i] == null) continue;
            Transform root = conns[i].transform.root;
            if (root == null) continue;

            if (IsGhostRoot(root)) continue;
            if (!IsBeamRootName(root.name)) continue;

            GameObject go = root.gameObject;
            if (!result.Contains(go)) result.Add(go);
        }

        return result;
    }

    private bool IsGhostRoot(Transform root)
    {
        int ghostLayer = LayerMask.NameToLayer("Ghost");
        return (ghostLayer >= 0 && root.gameObject.layer == ghostLayer);
    }

    private bool IsBeamRootName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.StartsWith("V", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("H", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------
    // Bounds utility (root-local)
    // ------------------------

    private bool TryGetRootLocalBounds(Transform root, out Bounds rootLocalBounds)
    {
        rootLocalBounds = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return false;

        bool inited = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

        for (int r = 0; r < rends.Length; r++)
        {
            Renderer ren = rends[r];
            if (ren == null) continue;

            Bounds lb = ren.localBounds;
            Transform t = ren.transform;
            Matrix4x4 m = rootWorldToLocal * t.localToWorldMatrix;

            Vector3 c = lb.center;
            Vector3 e = lb.extents;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = c + Vector3.Scale(e, new Vector3(sx, sy, sz));
                Vector3 p = m.MultiplyPoint3x4(corner);

                if (!inited) { inited = true; min = max = p; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
        }

        if (!inited) return false;

        rootLocalBounds = new Bounds((min + max) * 0.5f, (max - min));
        return true;
    }

    // ------------------------
    // Exposure test
    // ------------------------

    private bool IsFaceExposed(Transform beamRoot, Vector3 faceCenterWorld, Vector3 faceNormalWorld)
    {
        Vector3 start = faceCenterWorld + faceNormalWorld * exposureProbeStartOffset;

        RaycastHit hit;
        if (Physics.Raycast(start, faceNormalWorld, out hit, exposureProbeDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null) return true;

            if (hit.collider.transform.root == beamRoot) return true;
            if (hit.collider.GetComponentInParent<AttachmentPoint>() != null) return true;
            if (hit.collider.GetComponentInParent<PanelInstance>() != null) return true;

            Transform hr = hit.collider.transform.root;
            if (hr != null && hr.name.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        return true;
    }

    // ------------------------
    // Strip placement
    // ------------------------

    private void PlaceStripHalf(GameObject prefab, Vector3 posWorld, Vector3 forwardWorld, Vector3 outwardWorld)
    {
        Quaternion rot = ComputeStripRotation(forwardWorld, outwardWorld);
        GameObject go = Instantiate(prefab, posWorld, rot, veneersRoot);

        if (disableAllVeneerColliders)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = false;
        }
    }

    private Quaternion ComputeStripRotation(Vector3 forwardWorld, Vector3 outwardWorld)
    {
        forwardWorld.Normalize();
        outwardWorld.Normalize();

        Vector3 rightWorld = Vector3.Cross(outwardWorld, forwardWorld);
        if (rightWorld.sqrMagnitude < 1e-8f)
            rightWorld = Vector3.Cross(Vector3.up, forwardWorld);
        rightWorld.Normalize();

        outwardWorld = Vector3.Cross(forwardWorld, rightWorld).normalized;

        Vector3 localLen = VeneerPrefabLibrary.AxisToVector(library.stripLengthAxis);
        Vector3 localOut = VeneerPrefabLibrary.AxisToVector(library.stripOutwardAxis);

        if (Vector3.Cross(localOut, localLen).sqrMagnitude < 1e-8f)
        {
            localLen = Vector3.forward;
            localOut = Vector3.up;
        }

        Vector3 localRight = Vector3.Cross(localOut, localLen).normalized;

        Matrix4x4 Mlocal = Matrix4x4.identity;
        Mlocal.SetColumn(0, new Vector4(localRight.x, localRight.y, localRight.z, 0));
        Mlocal.SetColumn(1, new Vector4(localOut.x, localOut.y, localOut.z, 0));
        Mlocal.SetColumn(2, new Vector4(localLen.x, localLen.y, localLen.z, 0));

        Matrix4x4 Mworld = Matrix4x4.identity;
        Mworld.SetColumn(0, new Vector4(rightWorld.x, rightWorld.y, rightWorld.z, 0));
        Mworld.SetColumn(1, new Vector4(outwardWorld.x, outwardWorld.y, outwardWorld.z, 0));
        Mworld.SetColumn(2, new Vector4(forwardWorld.x, forwardWorld.y, forwardWorld.z, 0));

        Matrix4x4 R = Mworld * Mlocal.inverse;
        Vector3 f = R.GetColumn(2);
        Vector3 u = R.GetColumn(1);
        if (f.sqrMagnitude < 1e-8f) f = forwardWorld;
        if (u.sqrMagnitude < 1e-8f) u = outwardWorld;

        return Quaternion.LookRotation(f.normalized, u.normalized);
    }

    // ------------------------
    // Panel exclusion (unchanged from your logic)
    // ------------------------

    private struct PanelOBB
    {
        public Vector3 center, normal, right, up;
        public float halfRight, halfUp, halfNormal;
    }

    private List<PanelOBB> CollectPanels()
    {
        List<PanelOBB> result = new List<PanelOBB>();
        PanelInstance[] panels = FindAllObjects<PanelInstance>();

        for (int i = 0; i < panels.Length; i++)
        {
            PanelInstance pi = panels[i];
            if (pi == null) continue;

            PanelOBB obb;
            if (TryGetOBBFromRenderers(pi.transform, out obb))
                result.Add(obb);
        }

        return result;
    }

    private bool TryGetOBBFromRenderers(Transform t, out PanelOBB obb)
    {
        obb = new PanelOBB();

        Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return false;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        Vector3 center = b.center;

        Vector3 n = t.forward.normalized;
        Vector3 r = t.right.normalized;
        Vector3 u = t.up.normalized;

        float maxR = 0f, maxU = 0f, maxN = 0f;

        Vector3 c = b.center;
        Vector3 e = b.extents;

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
        {
            Vector3 corner = c + Vector3.Scale(e, new Vector3(sx, sy, sz));
            Vector3 d = corner - center;

            maxR = Mathf.Max(maxR, Mathf.Abs(Vector3.Dot(d, r)));
            maxU = Mathf.Max(maxU, Mathf.Abs(Vector3.Dot(d, u)));
            maxN = Mathf.Max(maxN, Mathf.Abs(Vector3.Dot(d, n)));
        }

        obb.center = center;
        obb.normal = n;
        obb.right = r;
        obb.up = u;
        obb.halfRight = maxR;
        obb.halfUp = maxU;
        obb.halfNormal = maxN;
        return true;
    }

    private bool IsFacePanelExcluded(Vector3 faceCenter, Vector3 faceNormal, List<PanelOBB> panels)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            PanelOBB p = panels[i];
            Vector3 d = faceCenter - p.center;

            float distPlane = Mathf.Abs(Vector3.Dot(d, p.normal));
            if (distPlane > panelPlaneMaxDistance + p.halfNormal) continue;

            float du = Mathf.Abs(Vector3.Dot(d, p.right));
            float dv = Mathf.Abs(Vector3.Dot(d, p.up));
            if (du > p.halfRight + panelRectPadding) continue;
            if (dv > p.halfUp + panelRectPadding) continue;

            if (Mathf.Abs(Vector3.Dot(faceNormal, p.normal)) < 0.85f) continue;
            if (Vector3.Dot(p.center - faceCenter, faceNormal) <= 0f) continue;

            return true;
        }
        return false;
    }

    // ------------------------
    // Unity-version-safe find helper
    // ------------------------

    private static T[] FindAllObjects<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }
}