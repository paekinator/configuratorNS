using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene adapter between placed frames and the pure <see cref="Overlap"/> core
/// (Rhino longer-wins rules). Builds lightweight records from the live build,
/// asks the core for a decision, and reports it; the caller decides whether to
/// apply it. Nothing here runs unless the Guided longer-wins feature flag is
/// on — Expert physics validation (<c>PlacementCollisionValidator</c>) and
/// Space Mode (which merges via <c>SpaceMerge</c>) are untouched.
/// </summary>
public static class FrameOverlapResolver
{
    /// <summary>One placed frame reduced to identity + skeleton span.</summary>
    public struct FrameRecord
    {
        public Transform Root;
        public string PartId;      // Unity id, e.g. "V13", "H7", "T7"
        public int Size;
        public Vector3 Center;     // world, metres (skeleton centre)
        public Vector3 EndA;       // world, metres (skeleton joint ends)
        public Vector3 EndB;
        public Vector3 LengthAxis; // world unit direction of the skeleton span
        public Vector3 LocalY;     // world direction of the frame's local Y (roll)
    }

    /// <summary>A resolved decision for one planned part, not yet applied.</summary>
    public struct Decision
    {
        public Overlap.Mode Mode;
        public string Note;
        public List<Transform> ToDelete;
    }

    /// <summary>
    /// Longer-wins decision for a just-instantiated (not yet committed) V post
    /// against every placed V frame in the scene. Report only — call
    /// <see cref="ApplyDeletes"/> to actually remove superseded frames.
    /// </summary>
    public static Decision ResolveVertical(GameObject plannedInstance, int ghostLayerMask)
    {
        var decision = new Decision { Mode = Overlap.Mode.Insert, ToDelete = new List<Transform>() };
        if (plannedInstance == null)
            return decision;

        if (!TryBuildRecord(plannedInstance.transform, out FrameRecord planned) ||
            !BeamPartUtility.IsVertical(planned.PartId))
            return decision;

        var roots = new List<Transform>();
        var records = new List<Overlap.VFrameRecord>();
        foreach (FrameRecord existing in CollectFrames(ghostLayerMask, plannedInstance.transform))
        {
            if (!BeamPartUtility.IsVertical(existing.PartId))
                continue;
            roots.Add(existing.Root);
            records.Add(new Overlap.VFrameRecord(
                NeospaceUnits.ToMm(existing.Center.x),
                NeospaceUnits.ToMm(existing.Center.z),
                NeospaceUnits.ToMm(existing.Center.y),
                existing.Size));
        }

        Overlap.Resolution resolution = Overlap.ResolveVertical(
            planned.Size,
            NeospaceUnits.ToMm(planned.Center.x),
            NeospaceUnits.ToMm(planned.Center.z),
            NeospaceUnits.ToMm(planned.Center.y),
            records);

        decision.Mode = resolution.Mode;
        decision.Note = resolution.Note;
        for (int i = 0; i < resolution.DeleteIndices.Count; i++)
            decision.ToDelete.Add(roots[resolution.DeleteIndices[i]]);

        Log(planned.PartId, decision);
        return decision;
    }

    /// <summary>
    /// Remove frames superseded by a longer one. Deactivates before Destroy so
    /// physics queries later this frame no longer see their colliders. Only the
    /// commit path may call this — never a ghost preview.
    /// </summary>
    public static int ApplyDeletes(Decision decision)
    {
        return ApplyDeletes(decision, null);
    }

    /// <summary>
    /// Same, but when <paramref name="journal"/> is given the superseded frames
    /// are only deactivated and recorded — the caller destroys them when the
    /// whole merge commits, or reactivates them when it aborts.
    /// </summary>
    public static int ApplyDeletes(Decision decision, List<GameObject> journal)
    {
        int removed = 0;
        if (decision.ToDelete == null)
            return removed;

        for (int i = 0; i < decision.ToDelete.Count; i++)
        {
            Transform root = decision.ToDelete[i];
            if (root == null)
                continue;
            root.gameObject.SetActive(false);
            if (journal != null)
                journal.Add(root.gameObject);
            else
                Object.Destroy(root.gameObject);
            removed++;
        }

        if (removed > 0)
            Physics.SyncTransforms();
        return removed;
    }

    /// <summary>
    /// All placed frames (V posts and H/HT connectors) as span records,
    /// skipping ghost layers and <paramref name="exclude"/>.
    /// </summary>
    public static List<FrameRecord> CollectFrames(int ghostLayerMask, Transform exclude = null)
    {
        var result = new List<FrameRecord>();
        foreach (BeamConnections conn in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            Transform root = conn.transform.root;
            if (root == exclude || !root.gameObject.activeInHierarchy)
                continue;
            if ((ghostLayerMask & (1 << root.gameObject.layer)) != 0)
                continue;
            if (TryBuildRecord(root, out FrameRecord record))
                result.Add(record);
        }
        return result;
    }

    /// <summary>
    /// Build the span record for one frame root: identity from the part name
    /// (HT7 ↔ T7 via <see cref="Naming"/>), skeleton centre from the rendered
    /// bounds (bodies are end-symmetric, so body centre = skeleton centre),
    /// and joint ends half a skeleton length along the length axis.
    /// </summary>
    public static bool TryBuildRecord(Transform root, out FrameRecord record)
    {
        return TryBuildRecord(root, root != null ? StructureClipboard.CleanPartId(root.name) : null,
            out record);
    }

    /// <summary>
    /// Same, with the part id supplied by the caller — for objects whose
    /// name carries a decoration the id parser doesn't know (e.g. the space
    /// merge's derived "Merged_H7" split segments).
    /// </summary>
    public static bool TryBuildRecord(Transform root, string partId, out FrameRecord record)
    {
        record = default;
        if (root == null)
            return false;

        if (partId == null)
            return false;

        Naming.ParsedName parsed = Naming.Parse(Naming.FromUnityPartId(partId));
        if (parsed == null || parsed.Family != Naming.Frame)
            return false;

        if (!TryWorldBounds(root, out Bounds bounds))
            return false;

        record.Root = root;
        record.PartId = partId;
        record.Size = parsed.SizeInt;
        record.Center = bounds.center;

        float skeletonMeters;
        if (BeamPartUtility.IsVertical(partId))
        {
            record.LengthAxis = Vector3.up;
            skeletonMeters = NeospaceUnits.Mm(Skeleton.VSkeletonLength(parsed.SizeInt));
            Vector3 half = record.LengthAxis * (skeletonMeters * 0.5f);
            record.EndA = record.Center - half;
            record.EndB = record.Center + half;
        }
        else if (TryPegEnds(root, out Vector3 pegA, out Vector3 pegB))
        {
            // Pegs are the real joint ends. An HT mesh is asymmetric, so the
            // renderer AABB is a bad stand-in: it shifts the centre and can
            // point EndA/EndB at neighbouring frames.
            Vector3 axis = pegB - pegA;
            axis.y = 0f;
            record.LengthAxis = axis.sqrMagnitude > 1e-8f
                ? axis.normalized
                : (bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward);
            record.Center = (pegA + pegB) * 0.5f;
            record.EndA = pegA;
            record.EndB = pegB;
        }
        else
        {
            record.LengthAxis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
            skeletonMeters = NeospaceUnits.Mm(Skeleton.HSkeletonLength(parsed.SizeInt));
            Vector3 half = record.LengthAxis * (skeletonMeters * 0.5f);
            record.EndA = record.Center - half;
            record.EndB = record.Center + half;
        }

        record.LocalY = root.up;
        return true;
    }

    /// <summary>Combined world-space renderer bounds of a part root.</summary>
    public static bool TryWorldBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        bounds = default;
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (!any)
            {
                bounds = renderers[i].bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return any;
    }

    /// <summary>
    /// World positions of AP_Peg_A / AP_Peg_B when the prefab has them.
    /// EndA is always Peg A (attaches to H), EndB is always Peg B (attaches to V).
    /// </summary>
    static bool TryPegEnds(Transform root, out Vector3 pegA, out Vector3 pegB)
    {
        pegA = pegB = default;
        AttachmentPoint foundA = null, foundB = null;
        AttachmentPoint[] aps = root.GetComponentsInChildren<AttachmentPoint>(true);
        for (int i = 0; i < aps.Length; i++)
        {
            AttachmentPoint ap = aps[i];
            if (ap == null || ap.role != AttachmentPoint.PointRole.Peg)
                continue;
            if (BeamPartUtility.IsTwistPegA(ap) ||
                (foundA == null && ap.name != null &&
                 ap.name.IndexOf("Peg_A", System.StringComparison.OrdinalIgnoreCase) >= 0))
                foundA = ap;
            else if (BeamPartUtility.IsTwistPegB(ap) ||
                     (foundB == null && ap.name != null &&
                      ap.name.IndexOf("Peg_B", System.StringComparison.OrdinalIgnoreCase) >= 0))
                foundB = ap;
        }

        // Regular H beams also use AP_Peg_A / AP_Peg_B; same lookup works.
        if (foundA == null || foundB == null)
        {
            for (int i = 0; i < aps.Length; i++)
            {
                AttachmentPoint ap = aps[i];
                if (ap == null || ap.role != AttachmentPoint.PointRole.Peg)
                    continue;
                if (foundA == null) foundA = ap;
                else if (foundB == null && ap != foundA) foundB = ap;
            }
        }

        if (foundA == null || foundB == null)
            return false;
        pegA = foundA.transform.position;
        pegB = foundB.transform.position;
        return true;
    }

    static void Log(string partId, Decision decision)
    {
        switch (decision.Mode)
        {
            case Overlap.Mode.Skip:
                Debug.Log($"Overlap resolve: skip {partId} · {decision.Note}");
                break;
            case Overlap.Mode.Block:
                Debug.Log($"Overlap resolve: block {partId} · {decision.Note}");
                break;
            case Overlap.Mode.Insert when decision.ToDelete.Count > 0:
                Debug.Log($"Overlap resolve: {partId} replaces {decision.ToDelete.Count} shorter coaxial frame(s)");
                break;
        }
    }
}
