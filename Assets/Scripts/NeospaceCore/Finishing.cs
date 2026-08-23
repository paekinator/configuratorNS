using System.Collections.Generic;

/// <summary>
/// Finishing coverability logic ported from Rhino neospace/finishing.py.
/// Pure maths, no scene access.
///
/// A U-channel is covered by veneers running between Cap Sides. A single
/// veneer of size n spans (n + 1) modular intervals. A channel section of
/// L intervals is coverable only if L can be tiled by available veneer
/// spans; when too long for one veneer it is split with a Cap Side.
/// </summary>
public static class Finishing
{
    /// <summary>
    /// Interval spans a single veneer can fill (veneer size n -> n + 1).
    /// Current catalogue (sizes 1-15) -> every span from 2 to 16.
    /// </summary>
    public static List<int> VeneerSpans()
    {
        var set = new SortedSet<int>();
        foreach (int n in CatalogueData.VeneerLengths)
            set.Add(n + 1);
        return new List<int>(set);
    }

    /// <summary>
    /// True when a section of <paramref name="intervals"/> modules can be
    /// tiled by veneers. Computed from the catalogue's spans (Rhino's rule:
    /// a section is a sum of available spans) rather than hardcoded, so it
    /// tracks catalogue changes. With sizes 1-15 present, everything from
    /// 2 intervals up is coverable; only a 1-interval section is not.
    /// </summary>
    public static bool Coverable(int intervals)
    {
        if (intervals < 2)
            return false;
        return CoverSegment(intervals) != null;
    }

    /// <summary>
    /// Veneer sizes (shorter first) that tile <paramref name="intervals"/>
    /// modules, or null when no combination fits. Consecutive veneers are
    /// separated by a Cap Side. Prefers the fewest veneers, then the most
    /// balanced pair (closest lengths).
    /// </summary>
    public static List<int> CoverSegment(int intervals)
    {
        List<int> spans = VeneerSpans();
        var spanSet = new HashSet<int>(spans);

        if (spanSet.Contains(intervals))
            return new List<int> { intervals - 1 };

        // Two veneers, most balanced.
        int bestLo = -1, bestHi = -1;
        foreach (int a in spans)
        {
            int b = intervals - a;
            if (!spanSet.Contains(b))
                continue;
            int lo = System.Math.Min(a, b), hi = System.Math.Max(a, b);
            if (bestLo < 0 || (hi - lo) < (bestHi - bestLo))
            {
                bestLo = lo;
                bestHi = hi;
            }
        }
        if (bestLo >= 0)
            return new List<int> { bestLo - 1, bestHi - 1 };

        // Three or more veneers (fewest-count breadth-first search); rare.
        var reach = new Dictionary<int, List<int>> { [0] = new List<int>() };
        var frontier = new List<int> { 0 };
        while (frontier.Count > 0)
        {
            var next = new List<int>();
            foreach (int cur in frontier)
            {
                foreach (int s in spans)
                {
                    int v = cur + s;
                    if (v == intervals)
                    {
                        var sizes = new List<int>(reach[cur]) { s };
                        sizes.Sort();
                        for (int i = 0; i < sizes.Count; i++)
                            sizes[i] -= 1;
                        return sizes;
                    }
                    if (v < intervals && !reach.ContainsKey(v))
                    {
                        reach[v] = new List<int>(reach[cur]) { s };
                        next.Add(v);
                    }
                }
            }
            frontier = next;
        }
        return null;
    }

    /// <summary>
    /// Sorted divider hole indices for a V channel: the two end holes
    /// (0 and <paramref name="topHole"/>) plus every connection level.
    /// </summary>
    public static List<int> DividerLevels(int topHole, IEnumerable<int> connectionLevels)
    {
        var set = new SortedSet<int> { 0, topHole };
        if (connectionLevels != null)
            foreach (int k in connectionLevels)
                set.Add(k);
        return new List<int>(set);
    }

    public struct Segment
    {
        public int LoHole;
        public int HiHole;
        public int Intervals;
    }

    /// <summary>Segments between consecutive dividers of a V channel.</summary>
    public static List<Segment> ChannelSegments(int topHole, IEnumerable<int> connectionLevels)
    {
        List<int> d = DividerLevels(topHole, connectionLevels);
        var result = new List<Segment>(d.Count - 1);
        for (int i = 0; i + 1 < d.Count; i++)
            result.Add(new Segment { LoHole = d[i], HiHole = d[i + 1], Intervals = d[i + 1] - d[i] });
        return result;
    }

    /// <summary>
    /// True when every segment of the channel is coverable.
    /// topHole = index of the top hole (n - 1 for a V frame of size n).
    /// </summary>
    public static bool ChannelCoverable(int topHole, IEnumerable<int> connectionLevels)
    {
        foreach (Segment s in ChannelSegments(topHole, connectionLevels))
            if (!Coverable(s.Intervals))
                return false;
        return true;
    }
}
