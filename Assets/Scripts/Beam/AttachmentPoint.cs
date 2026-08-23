using System;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentPoint : MonoBehaviour
{
    public enum PointRole { Hole, Peg }

    // ------------------------------------------------------------------
    // Live registry — the WebGL-friendly replacement for the scene-wide
    // FindObjectsByType scans that used to run every frame. Every enabled
    // point registers itself; StructureVersion bumps whenever a point
    // appears or disappears (i.e. a part was spawned or destroyed), so
    // change-detectors can compare one integer instead of rescanning the
    // world. Removal is swap-based: O(1) even for mass deletes.
    // ------------------------------------------------------------------

    public static readonly List<AttachmentPoint> Live = new List<AttachmentPoint>();

    /// <summary>Bumped on every register/unregister (spawn/destroy of parts).</summary>
    public static int StructureVersion { get; private set; } = 1;

    int _liveIndex = -1;

    [Header("Role")]
    public PointRole role = PointRole.Hole;

    [Header("Occupancy (runtime)")]
    public bool isOccupied;
    public GameObject occupant;

    // Used by PanelSlotManager pairing rebuild (runtime)
    public AttachmentPoint pairedWith;

    [Header("Owner Type (auto if unknown)")]
    [Tooltip("Set explicitly if you want. If left as Unknown, it will be inferred from root name (V*/H*/T*).")]
    public BeamKind ownerBeamKind = BeamKind.Unknown;

    [Header("Twist End Group (Twist only)")]
    [Tooltip("For Twist beams only: which end group this AP belongs to. 0 or 1. Leave -1 for non-twist APs.")]
    public int twistEndId = -1;

    [Header("Gizmos")]
    [Tooltip("Radius of the editor gizmo sphere drawn at this attachment point.")]
    public float gizmoRadius = 0.03f;

    void OnEnable()
    {
        // Runtime-only state MUST be reset so prefabs can't accidentally serialize occupancy.
        isOccupied = false;
        occupant = null;
        pairedWith = null;

        // Auto-infer owner kind from root name if not explicitly set.
        if (ownerBeamKind == BeamKind.Unknown)
            ownerBeamKind = InferOwnerKindFromRootName();

        _liveIndex = Live.Count;
        Live.Add(this);
        StructureVersion++;
    }

    void OnDisable()
    {
        if (_liveIndex < 0)
            return;
        int last = Live.Count - 1;
        AttachmentPoint moved = Live[last];
        Live[_liveIndex] = moved;
        moved._liveIndex = _liveIndex;
        Live.RemoveAt(last);
        _liveIndex = -1;
        StructureVersion++;
    }

    BeamKind InferOwnerKindFromRootName()
    {
        Transform r = transform.root;
        if (r == null) return BeamKind.Unknown;
        return BeamPartUtility.GetKind(r.name);
    }

#if UNITY_EDITOR
    // Visualizes pegs and holes while editing prefabs / scenes:
    //   Pegs  = green sphere + arrow along local forward
    //   Holes = cyan wire sphere
    //   Red   = occupied (runtime)
    void OnDrawGizmos()
    {
        float r = Mathf.Max(0.001f, gizmoRadius);
        Vector3 p = transform.position;

        if (isOccupied)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(p, r);
            return;
        }

        if (role == PointRole.Peg)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(p, r * 0.6f);
            Gizmos.DrawRay(p, transform.forward * (r * 3f));
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p, r);
        }
    }

    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(transform.position, name);
    }
#endif
}
