using System.Collections.Generic;

/// <summary>
/// Geometry for replacing one H/HT connector with catalogue pieces around new
/// posts (merge splits). <see cref="Division.SegmentSizes"/> decides WHICH
/// piece sizes exist; this adds WHERE the pieces sit so overall geometry is
/// unchanged: each cut swaps 88 mm of skeleton for one 41 mm post profile and
/// the joint gaps — e.g. H15 (body 1367) = H7 + post + H7 = 663 + 41 + 663.
/// All maths in millimetres, pure static, no scene types.
/// </summary>
public static class BeamSplit
{
    public readonly struct Segment
    {
        public readonly int Size;

        /// <summary>Piece centre along the beam axis, mm from the original beam centre.</summary>
        public readonly float CenterOffsetMm;

        public Segment(int size, float centerOffsetMm)
        {
            Size = size;
            CenterOffsetMm = centerOffsetMm;
        }
    }

    /// <summary>
    /// Replacement pieces when beam [n] is cut at the given internal point
    /// indices (1..n, joint ends are 0 and n+1). Returns null when any piece
    /// is not producible — the merge must then be refused, never approximated.
    /// </summary>
    public static List<Segment> Plan(int n, IEnumerable<int> cutPositions)
    {
        var unique = new SortedSet<int>();
        foreach (int k in cutPositions)
            unique.Add(k);

        List<int> sizes = Division.SegmentSizes(n, unique);
        if (sizes == null)
            return null;

        // Catalogue H sizes are odd, so the midpoint index is always integral.
        float mid = (n + 1) * 0.5f;
        var result = new List<Segment>(sizes.Count);

        int prev = 0;
        int index = 0;
        foreach (int bound in unique)
        {
            result.Add(new Segment(sizes[index++], ((prev + bound) * 0.5f - mid) * CatalogueData.ModuleMm));
            prev = bound;
        }
        result.Add(new Segment(sizes[index], ((prev + n + 1) * 0.5f - mid) * CatalogueData.ModuleMm));
        return result;
    }

    /// <summary>
    /// Rebuild a LINE of coaxial beams whose spans partially overlap (merged
    /// structures pushed together with an offset) into one run of catalogue
    /// pieces meeting the posts standing on that line — the physical build a
    /// fitter would make. Inputs are skeleton intervals and post centres in mm
    /// along the line (any shared origin). Returns null with a user-readable
    /// <paramref name="error"/> when the run cannot be built: a coverage gap,
    /// off-module geometry, a junction without a post, or a gap between posts
    /// that no produced piece spans. Segment centres come back relative to the
    /// union midpoint (<paramref name="unionCenterMm"/>).
    /// </summary>
    public static List<Segment> PlanLine(List<(float lo, float hi)> spansMm, List<float> postsMm,
        float toleranceMm, out float unionCenterMm, out string error)
    {
        unionCenterMm = 0f;
        error = null;
        if (spansMm == null || spansMm.Count == 0)
        {
            error = "no beams on the line";
            return null;
        }

        float module = CatalogueData.ModuleMm;
        // Closer to a span end than this = the joint post itself, not interior.
        float endMargin = module * 0.5f;

        // Union and contiguity.
        var sorted = new List<(float lo, float hi)>(spansMm);
        sorted.Sort((x, y) => x.lo.CompareTo(y.lo));
        float lo = sorted[0].lo;
        float hi = sorted[0].hi;
        float cover = sorted[0].hi;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].lo > cover + toleranceMm)
            {
                error = "the beams don't form one continuous run";
                return null;
            }
            cover = System.Math.Max(cover, sorted[i].hi);
            hi = System.Math.Max(hi, sorted[i].hi);
        }
        unionCenterMm = (lo + hi) * 0.5f;

        int modules = (int)System.Math.Round((hi - lo) / module);
        if (modules < 2 || System.Math.Abs((hi - lo) - modules * module) > toleranceMm)
        {
            error = "the merged run doesn't line up with the 88 mm grid";
            return null;
        }
        int n = modules - 1;

        // Every junction interior to the union must carry a post, and every
        // interior post must sit on an exact 88 mm point.
        var cuts = new SortedSet<int>();
        if (postsMm != null)
        {
            foreach (float p in postsMm)
            {
                if (p <= lo + endMargin || p >= hi - endMargin)
                    continue;
                int k = (int)System.Math.Round((p - lo) / module);
                if (k < 1 || k > n || System.Math.Abs(p - (lo + k * module)) > toleranceMm)
                {
                    error = "a post sits off the 88 mm points of the merged run";
                    return null;
                }
                cuts.Add(k);
            }
        }

        foreach ((float sLo, float sHi) in sorted)
        {
            foreach (float e in new[] { sLo, sHi })
            {
                if (e <= lo + endMargin || e >= hi - endMargin)
                    continue;
                if (!HasPostNear(postsMm, e, toleranceMm))
                {
                    error = "a beam ends inside the run with no post there";
                    return null;
                }
            }
        }

        List<int> sizes = Division.SegmentSizes(n, cuts);
        if (sizes == null)
        {
            // Report which piece is missing so the user knows why: the gap
            // between two posts needs a size that is not produced.
            var bounds = new List<int> { 0 };
            bounds.AddRange(cuts);
            bounds.Add(n + 1);
            for (int i = 0; i < bounds.Count - 1; i++)
            {
                int size = bounds[i + 1] - bounds[i] - 1;
                if (!Division.IsValidPieceSize(size))
                {
                    error = size < 1
                        ? "two posts sit right next to each other on the run"
                        : $"the post spacing needs an H{size}, which isn't made · shift by one module";
                    return null;
                }
            }
            error = "the merged run can't be built from catalogue pieces";
            return null;
        }

        return Plan(n, cuts);
    }

    static bool HasPostNear(List<float> postsMm, float positionMm, float toleranceMm)
    {
        if (postsMm == null)
            return false;
        foreach (float p in postsMm)
        {
            if (System.Math.Abs(p - positionMm) <= toleranceMm)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Internal point index for a crossing measured as an axis offset from the
    /// beam centre. False when the offset is outside the internal points or
    /// not within <paramref name="toleranceMm"/> of an exact 88 mm point —
    /// an off-point crossing cannot be built and must be refused.
    /// </summary>
    public static bool TryCutIndexFromOffset(int n, float offsetMm, float toleranceMm, out int cutIndex)
    {
        float mid = (n + 1) * 0.5f;
        cutIndex = (int)System.Math.Round(offsetMm / CatalogueData.ModuleMm + mid);
        if (cutIndex < 1 || cutIndex > n)
            return false;

        float exactMm = (cutIndex - mid) * CatalogueData.ModuleMm;
        return System.Math.Abs(offsetMm - exactMm) <= toleranceMm;
    }
}
