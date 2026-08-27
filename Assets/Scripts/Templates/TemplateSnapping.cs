using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Axis-locking and catalogue snapping for Guided preview lines.
/// Lines never run diagonally: ground spans lock to the dominant world axis
/// (exactly how beams are built) and heights lock straight up. Lengths snap to
/// the same H-span / V-height tables the planners and pipeline use.
/// </summary>
public static class TemplateSnapping
{
    /// <summary>Picks closer than this to the anchor mean "same spot" (the skip gesture).</summary>
    public static float SameSpotRadius => NeospaceUnits.Mm(90f); // under the smallest H span (176 mm)

    public struct Result
    {
        public bool HasLine;      // false = nothing to draw (no anchor movement yet)
        public bool IsSameSpot;   // hover collapses onto the anchor (skip gesture)
        public Vector3 End;       // snapped, axis-locked end point
        public Vector3 Axis;      // unit direction of the locked line
        public int Size;          // catalogue size number (H size for spans, V size for heights)
        public string Label;      // e.g. "H7 · 663 mm"
        public List<Vector3> Stops;
        public int ActiveStop;    // index into Stops, -1 when none
    }

    /// <summary>Nominal H length shown to users (matches the price list, e.g. H7 = 663 mm).</summary>
    public static string HLabel(int n) =>
        $"H{n} · {Skeleton.HBodyLength(n):0} mm";

    /// <summary>Nominal V length shown to users (matches the price list, e.g. V9 = 745 mm).</summary>
    public static string VLabel(int n) =>
        $"V{n} · {Skeleton.VBodyLength(n):0} mm";

    /// <summary>
    /// Lock the hover to the dominant horizontal axis from the anchor and snap the
    /// distance to the nearest catalogue H span (post spacing).
    /// </summary>
    public static Result GroundSpan(Vector3 anchor, Vector3 hover)
    {
        var result = new Result { Stops = new List<Vector3>(), ActiveStop = -1 };

        Vector3 delta = hover - anchor;
        delta.y = 0f;

        Vector3 axis;
        float dist;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
        {
            axis = delta.x >= 0f ? Vector3.right : Vector3.left;
            dist = Mathf.Abs(delta.x);
        }
        else
        {
            axis = delta.z >= 0f ? Vector3.forward : Vector3.back;
            dist = Mathf.Abs(delta.z);
        }

        result.HasLine = true;
        result.Axis = axis;

        if (dist < SameSpotRadius)
        {
            result.IsSameSpot = true;
            result.End = anchor;
            result.Label = "Same spot · skip";
            return result;
        }

        List<Catalogue.SnapEntry> table = Catalogue.HSpanSnapTable();
        float bestDiff = float.PositiveInfinity;
        for (int i = 0; i < table.Count; i++)
        {
            float stopDist = NeospaceUnits.Mm(table[i].DistanceMm);
            result.Stops.Add(anchor + axis * stopDist);

            float diff = Mathf.Abs(dist - stopDist);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                result.ActiveStop = i;
                result.Size = table[i].Size;
            }
        }

        result.End = result.Stops[result.ActiveStop];
        result.Label = HLabel(result.Size);
        return result;
    }

    /// <summary>
    /// Lock the hover straight up from the base and snap the height to the nearest
    /// catalogue V size. A floor-level hover snaps to V1 (the shortest post).
    /// </summary>
    public static Result Height(Vector3 basePoint, float hoverY)
    {
        var result = new Result
        {
            Stops = new List<Vector3>(),
            ActiveStop = -1,
            HasLine = true,
            Axis = Vector3.up
        };

        float height = Mathf.Max(0f, hoverY - basePoint.y);

        List<Catalogue.SnapEntry> table = Catalogue.VHeightSnapTable();
        float bestDiff = float.PositiveInfinity;
        for (int i = 0; i < table.Count; i++)
        {
            float stopDist = NeospaceUnits.Mm(table[i].DistanceMm);
            result.Stops.Add(basePoint + Vector3.up * stopDist);

            float diff = Mathf.Abs(height - stopDist);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                result.ActiveStop = i;
                result.Size = table[i].Size;
            }
        }

        result.End = result.Stops[result.ActiveStop];
        result.Label = VLabel(result.Size);
        return result;
    }
}
