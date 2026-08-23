using System.Collections.Generic;

/// <summary>Body / skeleton length and connection-point maths (millimetres).</summary>
public static class Skeleton
{
    public static float VBodyLength(int n) => (n - 1) * CatalogueData.ModuleMm + CatalogueData.ProfileMm;

    public static float HBodyLength(int n) => (n + 1) * CatalogueData.ModuleMm - CatalogueData.ProfileMm;

    public static float VSkeletonLength(int n) => (n - 1) * CatalogueData.ModuleMm;

    public static float HSkeletonLength(int n) => (n + 1) * CatalogueData.ModuleMm;

    public static float VeneerContactLength(int n) => (n + 1) * CatalogueData.ModuleMm - CatalogueData.ProfileMm;

    public static List<float> PointOffsets(int totalPoints)
    {
        var result = new List<float>();
        int t = totalPoints;
        if (t < 1)
            return result;

        float half = (t - 1) / 2f;
        for (int i = 0; i < t; i++)
            result.Add((i - half) * CatalogueData.ModuleMm);
        return result;
    }

    public static List<float> VPointOffsets(int n) => PointOffsets(n);

    public readonly struct HPointOffset
    {
        public readonly float OffsetMm;
        public readonly bool IsJointEnd;

        public HPointOffset(float offsetMm, bool isJointEnd)
        {
            OffsetMm = offsetMm;
            IsJointEnd = isJointEnd;
        }
    }

    public static List<HPointOffset> HPointOffsets(int n)
    {
        List<float> offsets = PointOffsets(n + 2);
        var result = new List<HPointOffset>(offsets.Count);
        int last = offsets.Count - 1;
        for (int i = 0; i < offsets.Count; i++)
            result.Add(new HPointOffset(offsets[i], i == 0 || i == last));
        return result;
    }

    public static int CorrespondingHSize(int vSize) => vSize - 2;
}
