using System;
using System.Collections.Generic;

/// <summary>
/// Panel board catalogue math: exact board edge lengths and decomposition of
/// oversized bay openings into produced boards ("iterate and sum down").
///
/// A board of edge size n spans (n + 1) modules; its physical edge is
/// (n + 1) * 88 - 41.283 mm — each end gives up half a 41 mm frame profile
/// and 0.283 mm is the fitting tolerance. Because every board end must rest
/// against frame material, an opening wider than one board is only buildable
/// with a divider frame at each board joint (the joint gap is exactly one
/// 41 mm profile). <see cref="PlanGrid"/> therefore returns per-axis board
/// runs and implies a real divider post/beam at every interior split line;
/// the scene layer inserts those through the merge pipeline.
///
/// Pure math only — no UnityEngine, safe for offline self-tests.
/// </summary>
public static class PanelFill
{
    public const float ToleranceMm = 0.283f;

    /// <summary>Produced board edge sizes (H numbers), ascending.</summary>
    public static readonly int[] EdgeSizes = { 1, 3, 5, 7, 11, 15 };

    /// <summary>Exact physical edge length in millimetres for edge size n.</summary>
    public static float EdgeMm(int n)
        => (n + 1) * CatalogueData.ModuleMm - CatalogueData.ProfileMm - ToleranceMm;

    public static bool IsEdgeSize(int n) => Array.IndexOf(EdgeSizes, n) >= 0;

    /// <summary>
    /// Modules for an inner opening measured between frame faces
    /// (opening = modules * 88 - 41). False when the length is not modular.
    /// </summary>
    public static bool TryModulesFromOpeningMm(float openingMm, out int modules)
    {
        modules = (int)Math.Round((openingMm + CatalogueData.ProfileMm) / CatalogueData.ModuleMm);
        if (modules < 2)
            return false;
        float ideal = modules * CatalogueData.ModuleMm - CatalogueData.ProfileMm;
        return Math.Abs(openingMm - ideal) <= 12f;
    }

    /// <summary>
    /// All ways to write <paramref name="modules"/> as a sum of board spans
    /// (edge + 1). Each result is a non-increasing list of edge sizes; the list
    /// is ordered by preference: fewest boards, then most balanced, then
    /// larger leading sizes. Empty when the span cannot be summed.
    /// </summary>
    public static List<List<int>> Decompositions(int modules)
    {
        var results = new List<List<int>>();
        if (modules < 2 || modules > 64)
            return results;

        var stack = new List<int>();
        Recurse(modules, int.MaxValue, stack, results);
        results.Sort(CompareRuns);
        return results;
    }

    static void Recurse(int remaining, int maxSize, List<int> stack, List<List<int>> results)
    {
        if (remaining == 0)
        {
            results.Add(new List<int>(stack));
            return;
        }

        for (int i = EdgeSizes.Length - 1; i >= 0; i--)
        {
            int size = EdgeSizes[i];
            if (size > maxSize)
                continue;
            int span = size + 1;
            if (span > remaining)
                continue;
            stack.Add(size);
            Recurse(remaining - span, size, stack, results);
            stack.RemoveAt(stack.Count - 1);
        }
    }

    static int Imbalance(List<int> run) => run[0] - run[run.Count - 1];

    static int CompareRuns(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return a.Count - b.Count;
        int ia = Imbalance(a);
        int ib = Imbalance(b);
        if (ia != ib) return ia - ib;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return b[i] - a[i];
        return 0;
    }

    public struct GridPlan
    {
        /// <summary>Board edge sizes along the width, in placement order.</summary>
        public List<int> Columns;

        /// <summary>Board edge sizes along the height, in placement order.</summary>
        public List<int> Rows;

        /// <summary>Null when the plan is valid.</summary>
        public string FailReason;

        public bool Ok => FailReason == null;
        public int BoardCount => Ok ? Columns.Count * Rows.Count : 0;
        public bool NeedsDividers => Ok && (Columns.Count > 1 || Rows.Count > 1);
    }

    /// <summary>
    /// Plan a full grid of produced boards for an opening spanning
    /// widthModules x heightModules (frame centreline to centreline). Every
    /// board must be one of the produced pairs. A divider frame is implied at
    /// every interior split line: a vertical V(heightModules + 1) post per
    /// column joint and a horizontal H(widthModules - 1) beam per row joint —
    /// their catalogue existence is part of plan validity.
    /// </summary>
    public static GridPlan PlanGrid(int widthModules, int heightModules)
    {
        List<List<int>> colCands = Decompositions(widthModules);
        List<List<int>> rowCands = Decompositions(heightModules);

        if (colCands.Count == 0 || rowCands.Count == 0)
        {
            return new GridPlan
            {
                FailReason = $"no produced boards sum to a {widthModules}x{heightModules} module opening"
            };
        }

        // Enumerate axis-run combinations by global preference: fewest boards,
        // most balanced runs, fewer columns (a beam divider builds simpler
        // than a post divider), then the per-axis preference order.
        var order = new List<(int ci, int ri)>();
        for (int ci = 0; ci < colCands.Count; ci++)
            for (int ri = 0; ri < rowCands.Count; ri++)
                order.Add((ci, ri));

        order.Sort((x, y) =>
        {
            int bx = colCands[x.ci].Count * rowCands[x.ri].Count;
            int by = colCands[y.ci].Count * rowCands[y.ri].Count;
            if (bx != by) return bx - by;
            int imbX = Imbalance(colCands[x.ci]) + Imbalance(rowCands[x.ri]);
            int imbY = Imbalance(colCands[y.ci]) + Imbalance(rowCands[y.ri]);
            if (imbX != imbY) return imbX - imbY;
            if (colCands[x.ci].Count != colCands[y.ci].Count)
                return colCands[x.ci].Count - colCands[y.ci].Count;
            if (x.ci != y.ci) return x.ci - y.ci;
            return x.ri - y.ri;
        });

        string dividerProblem = null;
        foreach ((int ci, int ri) in order)
        {
            List<int> cols = colCands[ci];
            List<int> rows = rowCands[ri];

            if (cols.Count > 1 && !Contains(CatalogueData.VSizes, heightModules + 1))
            {
                dividerProblem ??= $"divider post V{heightModules + 1} is not produced";
                continue;
            }
            if (rows.Count > 1 && !Contains(CatalogueData.HSizes, widthModules - 1))
            {
                dividerProblem ??= $"divider beam H{widthModules - 1} is not produced";
                continue;
            }

            bool allValid = true;
            for (int c = 0; c < cols.Count && allValid; c++)
                for (int r = 0; r < rows.Count && allValid; r++)
                    if (!Catalogue.IsValidPanelPair(cols[c], rows[r]))
                        allValid = false;
            if (!allValid)
                continue;

            return new GridPlan { Columns = cols, Rows = rows };
        }

        return new GridPlan
        {
            FailReason = dividerProblem ??
                $"no produced board combination fits a {widthModules}x{heightModules} module opening"
        };
    }

    static bool Contains(int[] arr, int value)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == value)
                return true;
        return false;
    }
}
