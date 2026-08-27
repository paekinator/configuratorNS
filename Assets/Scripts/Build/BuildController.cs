using System;
using UnityEngine;

/// <summary>
/// Unity-facing facade for beam placement. The implementation is split across
/// focused partial-class files under Assets/Scripts/Build so the serialized
/// component and its existing scene references remain unchanged.
/// </summary>
public partial class BuildController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public LayerMask floorMask;
    public LayerMask placementRayMask = ~0;      // must exclude Ghost
    public LayerMask ghostLayerMask;            // set to Ghost layer
    public LayerMask panelBlockerMask;          // set to PanelBlocker layer
    public PartDatabase partDatabase;

    // First placement snaps to the shared 88 mm module grid (T1PostsPlanner.SnapGround).

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
    [Tooltip("If TRUE, collisions with the host are checked with only the configured connector tolerance.")]
    public bool strictNoOverlap = true;

    [Tooltip("Allow tiny penetration only with host colliders, for connector tolerance.")]
    public float hostPenetrationTolerance = 0.002f;

    [Header("Collision / Overlap")]
    public float overlapMargin = 0.005f;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("Panels (optional)")]
    public PanelSlotManager panelSlotManager;

    // NonSerialized: a picked part must never be baked into the scene —
    // the app has to start with a plain cursor and no tool armed.
    [HideInInspector, NonSerialized] public string currentPartId;

    private GhostPlacementResult _lastGhostResult;
    private bool _lastGhostValid;

    [Serializable]
    public struct GhostPlacementResult
    {
        public bool hasPose;
        public bool isValid;

        public Vector3 position;
        public Quaternion rotation;

        public AttachmentPoint targetHoleInScene;
        public AttachmentPoint targetPegInScene;

        public Transform hostRoot;
        public string chosenPegNameOnBeam;
        public string chosenHoleNameOnBeam;
        public string debugInfo;
    }

    public void SetCurrentPart(string partId)
    {
        currentPartId = string.IsNullOrWhiteSpace(partId) ? null : partId.Trim();

        if (debugLogs)
            Debug.Log($"Selected part: [{currentPartId}]");
    }

    public void SetGhostResult(GhostPlacementResult result, bool isValid)
    {
        result.isValid = isValid;
        _lastGhostResult = result;
        _lastGhostValid = isValid;
    }

    public GhostPlacementResult ComputeGhostPlacement(string partId, GameObject ghostInstance)
    {
        if (ghostInstance == null)
            return InvalidPlacement("Missing ghost instance");

        if (cam == null)
            return InvalidPlacement("Missing Camera");

        string normalizedPartId = string.IsNullOrWhiteSpace(partId) ? string.Empty : partId.Trim();

        if (BeamPartUtility.IsVertical(normalizedPartId))
            return ComputeGhostV(normalizedPartId, ghostInstance);

        if (BeamPartUtility.IsHorizontalLike(normalizedPartId))
            return ComputeGhostH(normalizedPartId, ghostInstance);

        return InvalidPlacement($"Unknown partId type: {normalizedPartId}");
    }

    public void CommitPlacementFromGhost(string partId, GhostPlacementResult result)
    {
        if (!result.hasPose || !result.isValid)
            return;

        string normalizedPartId = string.IsNullOrWhiteSpace(partId) ? string.Empty : partId.Trim();
        if (!BeamPartUtility.IsBeam(normalizedPartId))
        {
            if (debugLogs)
                Debug.LogWarning($"BuildController: cannot commit unknown partId [{normalizedPartId}].");
            return;
        }

        PlaceRealBeamFromGhost(normalizedPartId, result);
    }

    public void GetGhostStatus(out bool hasPose, out bool isValid, out string info)
    {
        hasPose = _lastGhostResult.hasPose;
        isValid = _lastGhostValid;
        info = string.IsNullOrEmpty(_lastGhostResult.debugInfo)
            ? "(no info)"
            : _lastGhostResult.debugInfo;
    }

    private static GhostPlacementResult InvalidPlacement(string reason)
    {
        return new GhostPlacementResult
        {
            hasPose = false,
            isValid = false,
            debugInfo = reason
        };
    }
}
