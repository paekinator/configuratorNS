using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public LayerMask floorMask;
    public LayerMask placementRayMask = ~0;      // must exclude Ghost
    public LayerMask ghostLayerMask;            // set to Ghost layer
    public LayerMask panelBlockerMask;          // set to PanelBlocker layer
    public PartDatabase partDatabase;

    [Header("Grid (first placement)")]
    public float gridStep = 0.1f;

    [Header("First placement: place lowest point on BuildSurface")]
    public float firstSurfaceClearance = 0.001f;

    [Header("V Placement (applies to ALL V for now)")]
    public Vector3 v3RotationEuler = new Vector3(90f, 0f, 0f);
    public float v3MaxSnapDistanceToPeg = 0.8f;
    public Vector3 v3PegSnapOffsetLocal = Vector3.zero;
    public float v3CandidateYEpsilon = 0.001f;
    public float[] v3YawAngles = new float[] { 0f, 90f, 180f, 270f };

    [Header("H Placement (applies to ALL H + T for now)")]
    public Vector3 h3RotationEuler = new Vector3(180f, 90f, 0f);
    public float h3MaxSnapDistance = 0.5f;
    public float h3DepthOffset = 0f;
    public float h3LateralOffset = 0f;

    // -------------------------------------------------------
    // HOST FACE MAPPING (LOCAL) for hole-named faces AP_SideN_*
    // -------------------------------------------------------

    [Header("Hole Face Mapping (LOCAL) - V HOST")]
    [Tooltip("Outward LOCAL direction for AP_SideN_* holes, when the HOST root is a V beam.")]
    public Vector3 vHostSide1OutLocal = Vector3.right;
    public Vector3 vHostSide2OutLocal = Vector3.left;
    public Vector3 vHostSide3OutLocal = Vector3.forward;
    public Vector3 vHostSide4OutLocal = Vector3.back;

    [Header("Hole Face Mapping (LOCAL) - H HOST")]
    [Tooltip("Outward LOCAL direction for AP_SideN_* holes, when the HOST root is an H beam.")]
    public Vector3 hHostSide1OutLocal = Vector3.right;
    public Vector3 hHostSide2OutLocal = Vector3.left;
    public Vector3 hHostSide3OutLocal = Vector3.forward;
    public Vector3 hHostSide4OutLocal = Vector3.back;

    [Header("Hole Face Mapping (LOCAL) - T HOST")]
    [Tooltip("Outward LOCAL direction for AP_SideN_* holes, when the HOST root is a T (Twist) beam.")]
    public Vector3 tHostSide1OutLocal = Vector3.right;
    public Vector3 tHostSide2OutLocal = Vector3.left;
    public Vector3 tHostSide3OutLocal = Vector3.forward;
    public Vector3 tHostSide4OutLocal = Vector3.back;

    [Header("Face Rotation Offsets (per hole face) - V HOST")]
    [Tooltip("Euler offsets applied AFTER LookRotation(dir, up) * h3RotationEuler, when HOST is V.")]
    public Vector3 vHostFace1EulerOffset = Vector3.zero;
    public Vector3 vHostFace2EulerOffset = Vector3.zero;
    public Vector3 vHostFace3EulerOffset = Vector3.zero;
    public Vector3 vHostFace4EulerOffset = Vector3.zero;

    [Header("Face Rotation Offsets (per hole face) - H HOST")]
    [Tooltip("Euler offsets applied AFTER LookRotation(dir, up) * h3RotationEuler, when HOST is H.")]
    public Vector3 hHostFace1EulerOffset = Vector3.zero;
    public Vector3 hHostFace2EulerOffset = Vector3.zero;
    public Vector3 hHostFace3EulerOffset = Vector3.zero;
    public Vector3 hHostFace4EulerOffset = Vector3.zero;

    [Header("Face Rotation Offsets (per hole face) - T HOST")]
    [Tooltip("Euler offsets applied AFTER LookRotation(dir, up) * h3RotationEuler, when HOST is T (Twist).")]
    public Vector3 tHostFace1EulerOffset = Vector3.zero;
    public Vector3 tHostFace2EulerOffset = Vector3.zero;
    public Vector3 tHostFace3EulerOffset = Vector3.zero;
    public Vector3 tHostFace4EulerOffset = Vector3.zero;

    [Header("Strict No-Overlap")]
    [Tooltip("If TRUE, we do NOT ignore collisions with the host beam (prevents 'through-beam' placement).")]
    public bool strictNoOverlap = true;

    [Tooltip("Allow *tiny* penetration ONLY with the host root colliders (for connector tolerance). 0.001~0.003 usually.")]
    public float hostPenetrationTolerance = 0.002f;

    [Header("Collision / Overlap")]
    public float overlapMargin = 0.005f;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("Panels (optional)")]
    public PanelSlotManager panelSlotManager;

    [HideInInspector] public string currentPartId;

    private GhostPlacementResult _lastGhostResult;
    private bool _lastGhostValid;

    // -----------------------------
    // Host classification for mapping
    // -----------------------------
    private enum HostKind { Unknown = 0, V = 1, H = 2, T = 3 }

    public void SetCurrentPart(string partId)
    {
        currentPartId = partId;
        if (debugLogs) Debug.Log("Selected part: [" + partId + "]");
    }

    public void SetGhostResult(GhostPlacementResult res, bool isValid)
    {
        _lastGhostResult = res;
        _lastGhostValid = isValid;
    }

    [Serializable]
    public struct GhostPlacementResult
    {
        public bool hasPose;
        public bool isValid;

        public Vector3 position;
        public Quaternion rotation;

        public AttachmentPoint targetHoleInScene;
        public AttachmentPoint targetPegInScene;

        public Transform hostRoot; // root that owns the hole/peg we are snapping to
        public string chosenPegNameOnBeam;
        public string debugInfo;
    }

    public void CommitPlacementFromGhost(string partId, GhostPlacementResult res)
    {
        if (!res.hasPose || !res.isValid) return;

        if (IsVertical(partId))
        {
            PlaceRealVFromGhost(partId, res);
        }
        else if (IsHorizontal(partId) || IsTwist(partId))
        {
            // Twist behaves exactly like Horizontal
            PlaceRealHFromGhost(partId, res);
        }
    }

    public GhostPlacementResult ComputeGhostPlacement(string partId, GameObject ghostInstance)
    {
        if (IsVertical(partId))
            return ComputeGhostV(ghostInstance);

        if (IsHorizontal(partId) || IsTwist(partId))
            return ComputeGhostH(partId, ghostInstance);

        return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = "Unknown partId type" };
    }

    // -------------------------------------------------------
    // FIRST PLACEMENT (ANY BEAM): floor grid + bottom aligned
    // -------------------------------------------------------
    bool TryPlaceFirstOnFloor(GameObject ghost, Quaternion rot, out Vector3 pos, out string reason)
    {
        reason = "";
        pos = Vector3.zero;

        if (cam == null)
        {
            reason = "Missing Camera";
            return false;
        }

        Ray floorRay = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(floorRay, out RaycastHit floorHit, 500f, floorMask, QueryTriggerInteraction.Collide))
        {
            reason = "No floor hit (check floorMask + collider)";
            return false;
        }

        pos = floorHit.point;
        pos.x = Mathf.Round(pos.x / gridStep) * gridStep;
        pos.z = Mathf.Round(pos.z / gridStep) * gridStep;

        // Start at surface Y
        pos.y = floorHit.point.y;

        ghost.transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();

        // Use RENDERER bounds first for floor alignment
        if (TryGetWorldBoundsForFloor(ghost, out Bounds b))
        {
            float minY = b.min.y;
            float delta = (floorHit.point.y + firstSurfaceClearance) - minY;

            // Only lift if needed
            if (delta > 0f)
            {
                pos.y += delta;
                ghost.transform.SetPositionAndRotation(pos, rot);
                Physics.SyncTransforms();
            }
        }
        else
        {
            // fallback (rare)
            pos.y = floorHit.point.y + firstSurfaceClearance;
            ghost.transform.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
        }

        return true;
    }

    GhostPlacementResult ComputeGhostV(GameObject ghost)
    {
        bool hasAnyBeamAlready = HasAnyPlacedBeam();

        if (!hasAnyBeamAlready)
        {
            Quaternion rot = Quaternion.Euler(v3RotationEuler);

            if (!TryPlaceFirstOnFloor(ghost, rot, out Vector3 pos, out string fail))
                return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = fail };

            bool ok = !HasIllegalOverlap(ghost, null, null, out string reason);

            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = ok,
                position = pos,
                rotation = rot,
                debugInfo = ok ? "First placement OK (V on floor)" : $"First placement blocked: {reason}"
            };
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, placementRayMask, QueryTriggerInteraction.Ignore))
            return new GhostPlacementResult { hasPose = false, isValid = false };

        Vector3 clickPoint = hit.point;

        AttachmentPoint nearestPeg = FindNearestFreePeg(clickPoint, v3MaxSnapDistanceToPeg);
        if (nearestPeg == null)
            return new GhostPlacementResult { hasPose = false, isValid = false };

        Transform hostRoot = nearestPeg.transform.root;

        Quaternion baseRot = Quaternion.Euler(v3RotationEuler);

        AttachmentPoint[] aps = ghost.GetComponentsInChildren<AttachmentPoint>(true);
        List<AttachmentPoint> holes = new List<AttachmentPoint>();
        for (int i = 0; i < aps.Length; i++)
            if (aps[i] != null && aps[i].role == AttachmentPoint.PointRole.Hole)
                holes.Add(aps[i]);

        if (holes.Count == 0)
            return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = "V ghost has no holes" };

        bool found = false;
        Vector3 bestPos = ghost.transform.position;
        Quaternion bestRot = ghost.transform.rotation;
        string bestInfo = "";
        string lastReason = "";

        for (int yi = 0; yi < v3YawAngles.Length && !found; yi++)
        {
            float yaw = v3YawAngles[yi];
            Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
            ghost.transform.rotation = rot;
            Physics.SyncTransforms();

            float minY = float.PositiveInfinity;
            for (int i = 0; i < holes.Count; i++)
                minY = Mathf.Min(minY, holes[i].transform.position.y);

            List<AttachmentPoint> candidates = new List<AttachmentPoint>();
            for (int i = 0; i < holes.Count; i++)
                if (Mathf.Abs(holes[i].transform.position.y - minY) <= v3CandidateYEpsilon)
                    candidates.Add(holes[i]);

            candidates.Sort((a, b) =>
                Vector3.Distance(a.transform.position, nearestPeg.transform.position)
                    .CompareTo(Vector3.Distance(b.transform.position, nearestPeg.transform.position)));

            for (int ci = 0; ci < candidates.Count; ci++)
            {
                AttachmentPoint hole = candidates[ci];

                Vector3 holeWorld = hole.transform.position;
                Vector3 offset = holeWorld - ghost.transform.position;
                Vector3 pos = nearestPeg.transform.position - offset;

                pos += nearestPeg.transform.TransformVector(v3PegSnapOffsetLocal);

                ghost.transform.position = pos;
                Physics.SyncTransforms();

                if (!HasIllegalOverlap(ghost, null, null, out string reason))
                {
                    found = true;
                    bestPos = pos;
                    bestRot = rot;
                    bestInfo = $"V snap OK yaw={yaw} hole={hole.name}";
                    break;
                }
                else
                {
                    lastReason = reason;
                }
            }
        }

        if (!found)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = ghost.transform.position,
                rotation = ghost.transform.rotation,
                targetPegInScene = nearestPeg,
                hostRoot = hostRoot,
                debugInfo = $"V snap blocked: {lastReason}"
            };
        }

        return new GhostPlacementResult
        {
            hasPose = true,
            isValid = true,
            position = bestPos,
            rotation = bestRot,
            targetPegInScene = nearestPeg,
            hostRoot = hostRoot,
            debugInfo = bestInfo
        };
    }

    GhostPlacementResult ComputeGhostH(string partId, GameObject ghost)
    {
        bool hasAnyBeamAlready = HasAnyPlacedBeam();

        if (!hasAnyBeamAlready)
        {
            Quaternion baseRot = Quaternion.Euler(h3RotationEuler);
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, Vector3.up) * baseRot;

            if (!TryPlaceFirstOnFloor(ghost, rot, out Vector3 pos, out string fail))
                return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = fail };

            bool ok = !HasIllegalOverlap(ghost, null, null, out string reason);

            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = ok,
                position = pos,
                rotation = rot,
                debugInfo = ok ? "First placement OK (H/T on floor)" : $"First placement blocked: {reason}"
            };
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, placementRayMask, QueryTriggerInteraction.Ignore))
            return new GhostPlacementResult { hasPose = false, isValid = false };

        Vector3 clickPoint = hit.point;

        AttachmentPoint nearestHole = FindNearestFreeHole(clickPoint, h3MaxSnapDistance);
        if (nearestHole == null)
            return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = "No free hole nearby" };

        Transform hostRoot = nearestHole.transform.root;
        if (hostRoot == null)
            return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = "Hole has no root" };

        HostKind hostKind = InferHostKindFromRoot(hostRoot);

        int faceIndex = FaceIndexFromHoleName(nearestHole.name);
        if (faceIndex == 0) faceIndex = 2;

        Vector3 faceOutLocal = GetSideOutLocal(hostKind, faceIndex);
        Vector3 faceOut = hostRoot.TransformDirection(faceOutLocal).normalized;

        Vector3 up = Vector3.up;
        Vector3 beamDir = Vector3.Cross(up, faceOut).normalized;
        if (beamDir.sqrMagnitude < 1e-6f)
            return new GhostPlacementResult { hasPose = false, isValid = false, debugInfo = $"Bad faceOut (beamDir zero). host={hostKind} face={faceIndex}" };

        Quaternion baseRot2 = Quaternion.Euler(h3RotationEuler);
        Vector3 faceEulerOffset = GetFaceEulerOffset(hostKind, faceIndex);

        Quaternion targetRot = Quaternion.LookRotation(beamDir, up) * baseRot2 * Quaternion.Euler(faceEulerOffset);
        // [TWIST] Fix: when placing a T (Twist) onto a V host, faces 2 and 4 are flipped.
        // Apply a 180-degree yaw correction ONLY for Twist on V-host faces 2/4.
        if (IsTwist(partId) && hostKind == HostKind.V && (faceIndex == 2 || faceIndex == 4))
        {
            targetRot = Quaternion.AngleAxis(180f, Vector3.up) * targetRot;
        }

        AttachmentPoint[] apOnH = ghost.GetComponentsInChildren<AttachmentPoint>(true);
        List<AttachmentPoint> pegs = new List<AttachmentPoint>();
        for (int i = 0; i < apOnH.Length; i++)
            if (apOnH[i] != null && apOnH[i].role == AttachmentPoint.PointRole.Peg && !apOnH[i].isOccupied)
                pegs.Add(apOnH[i]);

        if (pegs.Count == 0)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = nearestHole.transform.position,
                rotation = targetRot,
                targetHoleInScene = nearestHole,
                hostRoot = hostRoot,
                debugInfo = "H/T ghost has no free pegs"
            };
        }

        ghost.transform.rotation = targetRot;
        Physics.SyncTransforms();

        pegs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        bool placed = false;
        string lastReason = "";
        Vector3 bestPos = ghost.transform.position;
        string chosenPegName = "";

        for (int i = 0; i < pegs.Count; i++)
        {
            var peg = pegs[i];

            Vector3 pegWorld = peg.transform.position;
            Vector3 offset = pegWorld - ghost.transform.position;
            Vector3 basePos = nearestHole.transform.position - offset;

            Vector3 depthAdjust = faceOut * h3DepthOffset;
            Vector3 lateralAdjust = beamDir * h3LateralOffset;

            Vector3 pos = basePos + depthAdjust + lateralAdjust;

            ghost.transform.position = pos;
            Physics.SyncTransforms();

            // Strict overlap, but allow tiny penetration with hostRoot
            Transform allowedRoot = strictNoOverlap ? null : hostRoot;
            Transform toleranceRoot = strictNoOverlap ? hostRoot : null;

            if (!HasIllegalOverlap(ghost, allowedRoot, toleranceRoot, out string reason))
            {
                placed = true;
                bestPos = pos;
                chosenPegName = peg.name;
                break;
            }
            else
            {
                lastReason = reason;
            }
        }

        if (!placed)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = ghost.transform.position,
                rotation = ghost.transform.rotation,
                targetHoleInScene = nearestHole,
                hostRoot = hostRoot,
                debugInfo = $"H/T blocked face={faceIndex} host={hostKind}: {lastReason}"
            };
        }

        return new GhostPlacementResult
        {
            hasPose = true,
            isValid = true,
            position = bestPos,
            rotation = ghost.transform.rotation,
            targetHoleInScene = nearestHole,
            hostRoot = hostRoot,
            chosenPegNameOnBeam = chosenPegName,
            debugInfo = $"H/T OK face={faceIndex} host={hostKind} peg={chosenPegName}"
        };
    }

    // -----------------------------
    // Host kind + mapping selection
    // -----------------------------
    HostKind InferHostKindFromRoot(Transform root)
    {
        if (root == null) return HostKind.Unknown;
        string n = root.name;
        if (string.IsNullOrEmpty(n)) return HostKind.Unknown;

        if (n.StartsWith("V", StringComparison.OrdinalIgnoreCase)) return HostKind.V;
        if (n.StartsWith("H", StringComparison.OrdinalIgnoreCase)) return HostKind.H;
        if (n.StartsWith("T", StringComparison.OrdinalIgnoreCase)) return HostKind.T;
        return HostKind.Unknown;
    }

    Vector3 GetSideOutLocal(HostKind hostKind, int faceIndex)
    {
        switch (hostKind)
        {
            case HostKind.V:
                switch (faceIndex)
                {
                    case 1: return vHostSide1OutLocal;
                    case 2: return vHostSide2OutLocal;
                    case 3: return vHostSide3OutLocal;
                    case 4: return vHostSide4OutLocal;
                    default: return vHostSide2OutLocal;
                }

            case HostKind.H:
                switch (faceIndex)
                {
                    case 1: return hHostSide1OutLocal;
                    case 2: return hHostSide2OutLocal;
                    case 3: return hHostSide3OutLocal;
                    case 4: return hHostSide4OutLocal;
                    default: return hHostSide2OutLocal;
                }

            case HostKind.T:
                switch (faceIndex)
                {
                    case 1: return tHostSide1OutLocal;
                    case 2: return tHostSide2OutLocal;
                    case 3: return tHostSide3OutLocal;
                    case 4: return tHostSide4OutLocal;
                    default: return tHostSide2OutLocal;
                }

            default:
                // Safe fallback: treat unknown like H/T, since your twist behaves like H
                switch (faceIndex)
                {
                    case 1: return hHostSide1OutLocal;
                    case 2: return hHostSide2OutLocal;
                    case 3: return hHostSide3OutLocal;
                    case 4: return hHostSide4OutLocal;
                    default: return hHostSide2OutLocal;
                }
        }
    }

    Vector3 GetFaceEulerOffset(HostKind hostKind, int faceIndex)
    {
        switch (hostKind)
        {
            case HostKind.V:
                switch (faceIndex)
                {
                    case 1: return vHostFace1EulerOffset;
                    case 2: return vHostFace2EulerOffset;
                    case 3: return vHostFace3EulerOffset;
                    case 4: return vHostFace4EulerOffset;
                    default: return Vector3.zero;
                }

            case HostKind.H:
                switch (faceIndex)
                {
                    case 1: return hHostFace1EulerOffset;
                    case 2: return hHostFace2EulerOffset;
                    case 3: return hHostFace3EulerOffset;
                    case 4: return hHostFace4EulerOffset;
                    default: return Vector3.zero;
                }

            case HostKind.T:
                switch (faceIndex)
                {
                    case 1: return tHostFace1EulerOffset;
                    case 2: return tHostFace2EulerOffset;
                    case 3: return tHostFace3EulerOffset;
                    case 4: return tHostFace4EulerOffset;
                    default: return Vector3.zero;
                }

            default:
                return Vector3.zero;
        }
    }

    int FaceIndexFromHoleName(string holeName)
    {
        if (string.IsNullOrEmpty(holeName)) return 0;

        int idx = holeName.IndexOf("AP_Side", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 0;

        int digitPos = idx + "AP_Side".Length;
        if (digitPos >= holeName.Length) return 0;

        char c = holeName[digitPos];
        if (c < '1' || c > '4') return 0;

        return (int)(c - '0');
    }

    void PlaceRealVFromGhost(string partId, GhostPlacementResult res)
    {
        GameObject prefab = (partDatabase != null) ? partDatabase.GetRealPrefab(partId) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"BuildController: No realPrefab found for {partId}");
            return;
        }

        GameObject instance = Instantiate(prefab, res.position, res.rotation);

        if (HasIllegalOverlap(instance, null, null, out string reason))
        {
            if (debugLogs) Debug.Log($"Blocked REAL V placement: {reason}");
            Destroy(instance);
            return;
        }

        var conn = instance.GetComponent<BeamConnections>();
        if (conn == null) conn = instance.AddComponent<BeamConnections>();

        if (res.targetPegInScene != null)
        {
            res.targetPegInScene.isOccupied = true;
            res.targetPegInScene.occupant = instance;
            conn.RegisterOccupiedScenePoint(res.targetPegInScene);
        }

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        if (debugLogs) Debug.Log($"Placed REAL {partId}. {res.debugInfo}");
    }

    void PlaceRealHFromGhost(string partId, GhostPlacementResult res)
    {
        GameObject prefab = (partDatabase != null) ? partDatabase.GetRealPrefab(partId) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"BuildController: No realPrefab found for {partId}");
            return;
        }

        GameObject instance = Instantiate(prefab, res.position, res.rotation);

        Transform allowedRoot = strictNoOverlap ? null : res.hostRoot;
        Transform toleranceRoot = strictNoOverlap ? res.hostRoot : null;

        if (HasIllegalOverlap(instance, allowedRoot, toleranceRoot, out string reason))
        {
            if (debugLogs) Debug.Log($"Blocked REAL H/T placement: {reason}");
            Destroy(instance);
            return;
        }

        var conn = instance.GetComponent<BeamConnections>();
        if (conn == null) conn = instance.AddComponent<BeamConnections>();

        if (res.targetHoleInScene != null)
        {
            res.targetHoleInScene.isOccupied = true;
            res.targetHoleInScene.occupant = instance;
            conn.RegisterOccupiedScenePoint(res.targetHoleInScene);
        }

        // Mark the chosen peg on the placed beam as occupied (so it can't be reused)
        if (!string.IsNullOrEmpty(res.chosenPegNameOnBeam))
        {
            AttachmentPoint[] apOnH = instance.GetComponentsInChildren<AttachmentPoint>(true);
            for (int i = 0; i < apOnH.Length; i++)
            {
                if (apOnH[i] == null) continue;
                if (apOnH[i].role != AttachmentPoint.PointRole.Peg) continue;
                if (apOnH[i].name != res.chosenPegNameOnBeam) continue;

                apOnH[i].isOccupied = true;
                apOnH[i].occupant = instance;
                break;
            }
        }

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        if (debugLogs) Debug.Log($"Placed REAL {partId}. {res.debugInfo}");
    }

    AttachmentPoint FindNearestFreePeg(Vector3 clickPoint, float maxDist)
    {
        AttachmentPoint[] aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);

        AttachmentPoint nearest = null;
        float best = Mathf.Infinity;

        foreach (var ap in aps)
        {
            if (ap == null) continue;

            Transform root = ap.transform.root;
            if (root == null) continue;
            if (IsInMask(root.gameObject.layer, ghostLayerMask)) continue;

            if (ap.role != AttachmentPoint.PointRole.Peg) continue;
            if (ap.isOccupied) continue;

            float d = Vector3.Distance(ap.transform.position, clickPoint);
            if (d < best)
            {
                best = d;
                nearest = ap;
            }
        }

        if (nearest == null || best > maxDist)
            return null;

        return nearest;
    }

    AttachmentPoint FindNearestFreeHole(Vector3 clickPoint, float maxDist)
    {
        AttachmentPoint[] aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);

        AttachmentPoint nearest = null;
        float best = Mathf.Infinity;

        foreach (var ap in aps)
        {
            if (ap == null) continue;

            Transform root = ap.transform.root;
            if (root == null) continue;
            if (IsInMask(root.gameObject.layer, ghostLayerMask)) continue;

            if (ap.role != AttachmentPoint.PointRole.Hole) continue;
            if (ap.isOccupied) continue;

            float d = Vector3.Distance(ap.transform.position, clickPoint);
            if (d < best)
            {
                best = d;
                nearest = ap;
            }
        }

        if (nearest == null || best > maxDist)
            return null;

        return nearest;
    }

    bool HasAnyPlacedBeam()
    {
        AttachmentPoint[] aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < aps.Length; i++)
        {
            var ap = aps[i];
            if (ap == null) continue;

            Transform root = ap.transform.root;
            if (root == null) continue;

            if (IsInMask(root.gameObject.layer, ghostLayerMask)) continue;

            string rn = root.name;
            if (rn.StartsWith("V", StringComparison.OrdinalIgnoreCase) ||
                rn.StartsWith("H", StringComparison.OrdinalIgnoreCase) ||
                rn.StartsWith("T", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // allowedRoot: fully ignore overlaps with this root (only when strictNoOverlap=false)
    // toleranceRoot: allow tiny penetration with this root (only when strictNoOverlap=true)
    bool HasIllegalOverlap(GameObject instance, Transform allowedRoot, Transform toleranceRoot, out string reason)
    {
        reason = "";

        Transform instanceRoot = instance.transform.root;

        Collider[] ownCols = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownCols.Length; i++) ownCols[i].enabled = false;

        Renderer[] rends = instance.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0)
        {
            for (int i = 0; i < ownCols.Length; i++) ownCols[i].enabled = true;
            return false;
        }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        Vector3 center = b.center;
        Vector3 halfExtents = b.extents - Vector3.one * overlapMargin;

        halfExtents.x = Mathf.Max(halfExtents.x, 0.0005f);
        halfExtents.y = Mathf.Max(halfExtents.y, 0.0005f);
        halfExtents.z = Mathf.Max(halfExtents.z, 0.0005f);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        // re-enable our colliders for penetration testing
        for (int i = 0; i < ownCols.Length; i++) ownCols[i].enabled = true;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            if (IsInMask(hit.gameObject.layer, panelBlockerMask))
            {
                reason = $"panel blocker: {hit.name}";
                return true;
            }

            if (IsInMask(hit.gameObject.layer, ghostLayerMask))
                continue;

            Transform hitRoot = hit.transform.root;

            if (hitRoot == instanceRoot)
                continue;

            if (allowedRoot != null && hitRoot == allowedRoot)
                continue;

            if (IsFloorLayer(hit.gameObject.layer))
                continue;

            if (hit.GetComponentInParent<AttachmentPoint>() != null)
                continue;

            // Host tolerance: allow only tiny penetration with hostRoot
            if (toleranceRoot != null && hitRoot == toleranceRoot)
            {
                if (PenetratesBeyondTolerance(ownCols, hit, hostPenetrationTolerance, out float worstDepth))
                {
                    reason = $"host penetration {worstDepth:F4} > tol {hostPenetrationTolerance:F4} on {hit.name}";
                    return true;
                }
                else
                {
                    continue;
                }
            }

            reason = $"own={instance.name} hit={hit.name} hitRoot={hitRoot.name}";
            return true;
        }

        return false;
    }

    bool PenetratesBeyondTolerance(Collider[] ownCols, Collider other, float tolerance, out float worstDepth)
    {
        worstDepth = 0f;

        if (ownCols == null || other == null) return false;

        for (int i = 0; i < ownCols.Length; i++)
        {
            Collider a = ownCols[i];
            if (a == null) continue;
            if (!a.enabled) continue;

            if (Physics.ComputePenetration(
                    a, a.transform.position, a.transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 dir, out float dist))
            {
                worstDepth = Mathf.Max(worstDepth, dist);
                if (dist > tolerance)
                    return true;
            }
        }

        return false;
    }

    bool IsFloorLayer(int layer) => (floorMask.value & (1 << layer)) != 0;
    static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    static bool IsVertical(string partId) =>
        !string.IsNullOrEmpty(partId) && partId.StartsWith("V", StringComparison.OrdinalIgnoreCase);

    static bool IsHorizontal(string partId) =>
        !string.IsNullOrEmpty(partId) && partId.StartsWith("H", StringComparison.OrdinalIgnoreCase);

    static bool IsTwist(string partId) =>
        !string.IsNullOrEmpty(partId) && partId.StartsWith("T", StringComparison.OrdinalIgnoreCase);

    public void GetGhostStatus(out bool hasPose, out bool isValid, out string info)
    {
        hasPose = _lastGhostResult.hasPose;
        isValid = _lastGhostValid;
        info = string.IsNullOrEmpty(_lastGhostResult.debugInfo) ? "(no info)" : _lastGhostResult.debugInfo;
    }

    static bool TryGetWorldBoundsForFloor(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasAny = false;

        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;

            if (!hasAny)
            {
                bounds = rends[i].bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(rends[i].bounds);
            }
        }

        if (hasAny) return true;

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;

            if (!hasAny)
            {
                bounds = cols[i].bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(cols[i].bounds);
            }
        }

        return hasAny;
    }

    static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        bool hasAny = false;

        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            Bounds cb = cols[i].bounds;

            if (!hasAny)
            {
                bounds = cb;
                hasAny = true;
            }
            else bounds.Encapsulate(cb);
        }
        if (hasAny) return true;

        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            if (!hasAny)
            {
                bounds = rends[i].bounds;
                hasAny = true;
            }
            else bounds.Encapsulate(rends[i].bounds);
        }

        return hasAny;
    }
}