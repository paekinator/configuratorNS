using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coaxial overlap detection and longer-wins resolution, ported from Rhino
/// neospace_rhino/overlap.py. Pure maths in millimetres — no GameObjects, no
/// Physics, no BuildController. Owner decision 2026-07-20:
///   - same axis, equal spans              → reuse existing (SKIP)
///   - existing longer contains new        → reuse existing (SKIP)
///   - new longer contains existing        → delete existing, INSERT new
///   - partial overlap (neither contains)  → BLOCK
/// Disjoint spans on the same axis (stacked structures) are always allowed.
/// </summary>
public static class Overlap
{
    /// <summary>Same-axis test for vertical frames (Rhino XY_TOL).</summary>
    public const float XyToleranceMm = 0.5f;

    /// <summary>Span endpoint comparison (Rhino Z_TOL / SPAN_TOL).</summary>
    public const float SpanToleranceMm = 0.5f;

    /// <summary>Max distance from the span line to count as coaxial (Rhino 1.0 mm).</summary>
    public const float LineToleranceMm = 1.0f;

    public enum Mode { Insert, Skip, Block }

    public enum SpanClass { None, Equal, InsideNew, CoversNew, Partial }

    /// <summary>An existing V frame reduced to its axis and skeleton span.</summary>
    public readonly struct VFrameRecord
    {
        /// <summary>First horizontal axis coordinate (Unity world X), mm.</summary>
        public readonly float PlanXMm;

        /// <summary>Second horizontal axis coordinate (Unity world Z), mm.</summary>
        public readonly float PlanYMm;

        /// <summary>Skeleton centre along the vertical axis, mm.</summary>
        public readonly float CenterHeightMm;

        public readonly int Size;

        public VFrameRecord(float planXMm, float planYMm, float centerHeightMm, int size)
        {
            PlanXMm = planXMm;
            PlanYMm = planYMm;
            CenterHeightMm = centerHeightMm;
            Size = size;
        }
    }

    /// <summary>An existing H/HT connector reduced to its joint-end span.</summary>
    public readonly struct ConnectorRecord
    {
        public readonly Vector3 End1Mm;
        public readonly Vector3 End2Mm;

        /// <summary>Catalogue name, e.g. "H7" or "HT7" (not the Unity "T7" id).</summary>
        public readonly string Name;

        /// <summary>World direction of the frame's local Y (its roll), unit length.</summary>
        public readonly Vector3 LocalY;

        public ConnectorRecord(Vector3 end1Mm, Vector3 end2Mm, string name, Vector3 localY)
        {
            End1Mm = end1Mm;
            End2Mm = end2Mm;
            Name = name;
            LocalY = localY;
        }
    }

    /// <summary>Outcome of a longer-wins resolution.</summary>
    public readonly struct Resolution
    {
        public readonly Mode Mode;

        /// <summary>Indices into the caller's existing-frames list to delete before inserting.</summary>
        public readonly List<int> DeleteIndices;

        /// <summary>Human-readable reason for Skip/Block (null on plain Insert).</summary>
        public readonly string Note;

        public Resolution(Mode mode, List<int> deleteIndices, string note)
        {
            Mode = mode;
            DeleteIndices = deleteIndices ?? new List<int>();
            Note = note;
        }
    }

    /// <summary>
    /// Resolve a planned V insertion against existing coaxial V frames.
    /// Insert → delete the frames at <see cref="Resolution.DeleteIndices"/> first;
    /// Skip → an equal/longer frame already covers the span;
    /// Block → unsupported partial overlap.
    /// </summary>
    public static Resolution ResolveVertical(int newSize, float planXMm, float planYMm,
        float centerHeightMm, IReadOnlyList<VFrameRecord> existing)
    {
        var toDelete = new List<int>();
        float newHalf = Skeleton.VSkeletonLength(newSize) * 0.5f;
        float nz1 = centerHeightMm - newHalf;
        float nz2 = centerHeightMm + newHalf;

        for (int i = 0; i < existing.Count; i++)
        {
            VFrameRecord e = existing[i];
            if (System.Math.Abs(e.PlanXMm - planXMm) > XyToleranceMm)
                continue;
            if (System.Math.Abs(e.PlanYMm - planYMm) > XyToleranceMm)
                continue;

            float existingHalf = Skeleton.VSkeletonLength(e.Size) * 0.5f;
            float ez1 = e.CenterHeightMm - existingHalf;
            float ez2 = e.CenterHeightMm + existingHalf;

            if (ez2 < nz1 - SpanToleranceMm || nz2 < ez1 - SpanToleranceMm)
                continue; // disjoint spans on the same axis: allowed

            bool existingContains = ez1 <= nz1 + SpanToleranceMm && ez2 >= nz2 - SpanToleranceMm;
            bool newContains = nz1 <= ez1 + SpanToleranceMm && nz2 >= ez2 - SpanToleranceMm;

            if (existingContains)
            {
                // Equal or longer existing frame already provides this span.
                return new Resolution(Mode.Skip, null, "covered by existing V" + e.Size);
            }
            if (newContains)
            {
                toDelete.Add(i);
                continue;
            }
            return new Resolution(Mode.Block, null, "partially overlaps existing V" + e.Size);
        }

        return new Resolution(Mode.Insert, toDelete, null);
    }

    /// <summary>
    /// Classify existing span e1→e2 against planned span pa→pb (all mm).
    /// None = not coaxial or disjoint; spans sharing only an endpoint
    /// (chained connectors) also count as disjoint.
    /// </summary>
    public static SpanClass ClassifySpanOverlap(Vector3 paMm, Vector3 pbMm, Vector3 e1Mm, Vector3 e2Mm)
    {
        float length = Vector3.Distance(paMm, pbMm);
        if (length <= 0f)
            return SpanClass.None;

        Vector3 direction = (pbMm - paMm) / length;

        // Both existing endpoints must lie on the span line.
        if (DistanceToLine(e1Mm, paMm, direction) > LineToleranceMm ||
            DistanceToLine(e2Mm, paMm, direction) > LineToleranceMm)
            return SpanClass.None;

        float t1 = Vector3.Dot(e1Mm - paMm, direction);
        float t2 = Vector3.Dot(e2Mm - paMm, direction);
        float lo = Mathf.Min(t1, t2);
        float hi = Mathf.Max(t1, t2);

        if (hi < SpanToleranceMm || lo > length - SpanToleranceMm)
            return SpanClass.None; // disjoint or only endpoint-touching

        bool eqLo = Mathf.Abs(lo) <= SpanToleranceMm;
        bool eqHi = Mathf.Abs(hi - length) <= SpanToleranceMm;
        if (eqLo && eqHi)
            return SpanClass.Equal;
        if (lo >= -SpanToleranceMm && hi <= length + SpanToleranceMm)
            return SpanClass.InsideNew;
        if (lo <= SpanToleranceMm && hi >= length - SpanToleranceMm)
            return SpanClass.CoversNew;
        return SpanClass.Partial;
    }

    /// <summary>
    /// Longer-wins resolution of a planned connector span pa→pb (all mm).
    /// <paramref name="yOptions"/> lists the local-Y directions that fit this
    /// connection; when given, an equal-span same-name frame is reused only if
    /// its roll matches one of them (a 180° roll is the same physical fit).
    /// </summary>
    public static Resolution ResolveConnectorSpan(Vector3 paMm, Vector3 pbMm, string newName,
        IReadOnlyList<ConnectorRecord> existing, IReadOnlyList<Vector3> yOptions = null)
    {
        var toDelete = new List<int>();

        for (int i = 0; i < existing.Count; i++)
        {
            ConnectorRecord e = existing[i];
            SpanClass cls = ClassifySpanOverlap(paMm, pbMm, e.End1Mm, e.End2Mm);
            if (cls == SpanClass.None)
                continue;

            if (cls == SpanClass.Equal)
            {
                if (e.Name == newName)
                {
                    if (yOptions != null && yOptions.Count > 0)
                    {
                        bool fits = false;
                        for (int y = 0; y < yOptions.Count; y++)
                        {
                            // abs(): a 180-degree roll is the same physical fit
                            if (Mathf.Abs(Vector3.Dot(e.LocalY, yOptions[y])) > 0.99f)
                            {
                                fits = true;
                                break;
                            }
                        }
                        if (!fits)
                        {
                            return new Resolution(Mode.Block, null,
                                e.Name + " already spans here rolled to an angle that cannot engage these frames");
                        }
                    }
                    return new Resolution(Mode.Skip, null, newName + " already exists here");
                }
                return new Resolution(Mode.Block, null,
                    e.Name + " already spans here (cannot mix with " + newName + ")");
            }

            if (cls == SpanClass.InsideNew)
            {
                toDelete.Add(i);
                continue;
            }
            if (cls == SpanClass.CoversNew)
                return new Resolution(Mode.Skip, null, "covered by existing " + e.Name);

            return new Resolution(Mode.Block, null, "partially overlaps existing " + e.Name);
        }

        return new Resolution(Mode.Insert, toDelete, null);
    }

    static float DistanceToLine(Vector3 point, Vector3 origin, Vector3 unitDirection)
    {
        float t = Vector3.Dot(point - origin, unitDirection);
        return Vector3.Distance(point, origin + unitDirection * t);
    }
}
