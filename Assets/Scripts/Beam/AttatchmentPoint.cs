using System;
using UnityEngine;

public class AttachmentPoint : MonoBehaviour
{
    public enum PointRole { Hole, Peg }

    [Header("Role")]
    public PointRole role = PointRole.Hole;

    [Header("Occupancy (runtime)")]
    [NonSerialized] public bool isOccupied;
    [NonSerialized] public GameObject occupant;

    // Used by PanelSlotManager pairing rebuild (runtime)
    [NonSerialized] public AttachmentPoint pairedWith;

    // -----------------------------
    // Twist / Compatibility Metadata
    // -----------------------------
    [Header("Owner Type (auto if unknown)")]
    [Tooltip("Set explicitly if you want. If left as Unknown, it will be inferred from root name (V*/H*/T*).")]
    public BeamKind ownerBeamKind = BeamKind.Unknown;

    [Header("Twist End Group (Twist only)")]
    [Tooltip("For Twist beams only: which end group this AP belongs to. 0 or 1. Leave -1 for non-twist APs.")]
    public int twistEndId = -1;

    void OnEnable()
    {
        // Runtime-only state MUST be reset so prefabs can't accidentally serialize occupancy.
        isOccupied = false;
        occupant = null;
        pairedWith = null;

        // Auto-infer owner kind from root name if not explicitly set.
        if (ownerBeamKind == BeamKind.Unknown)
            ownerBeamKind = InferOwnerKindFromRootName();
    }

    BeamKind InferOwnerKindFromRootName()
    {
        Transform r = transform.root;
        if (r == null) return BeamKind.Unknown;

        string n = r.name;
        if (string.IsNullOrEmpty(n)) return BeamKind.Unknown;

        if (n.StartsWith("V", StringComparison.OrdinalIgnoreCase)) return BeamKind.V;
        if (n.StartsWith("H", StringComparison.OrdinalIgnoreCase)) return BeamKind.H;
        if (n.StartsWith("T", StringComparison.OrdinalIgnoreCase)) return BeamKind.TwistH;
        return BeamKind.Unknown;
    }
}