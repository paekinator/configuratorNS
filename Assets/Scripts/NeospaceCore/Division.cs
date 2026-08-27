using System.Collections.Generic;

/// <summary>H/HT connector division into catalogue-valid pieces.</summary>
public static class Division
{
    public static bool IsValidPieceSize(int size)
    {
        return Contains(CatalogueData.HSizes, size) || Contains(CatalogueData.HtSizes, size);
    }

    static bool Contains(int[] arr, int value)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == value)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Piece sizes when an H/HT[n] is cut at the given internal positions.
    /// Returns null if any cut is invalid or any piece is not in the catalogue.
    /// </summary>
    public static List<int> SegmentSizes(int n, IEnumerable<int> cutPositions)
    {
        var unique = new SortedSet<int>();
        foreach (int k in cutPositions)
            unique.Add(k);

        foreach (int k in unique)
        {
            if (k < 1 || k > n)
                return null;
        }

        var bounds = new List<int> { 0 };
        bounds.AddRange(unique);
        bounds.Add(n + 1);

        var sizes = new List<int>();
        for (int i = 0; i < bounds.Count - 1; i++)
        {
            int span = bounds[i + 1] - bounds[i];
            int size = span - 1;
            if (!IsValidPieceSize(size))
                return null;
            sizes.Add(size);
        }
        return sizes;
    }

    public static List<int> DivisiblePoints(int n)
    {
        var result = new List<int>();
        for (int k = 1; k <= n; k++)
        {
            if (SegmentSizes(n, new[] { k }) != null)
                result.Add(k);
        }
        return result;
    }
}
