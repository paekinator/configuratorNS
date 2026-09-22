using System.Globalization;
using UnityEngine;

/// <summary>The available overall size, in millimetres, for a consumer build.</summary>
[System.Serializable]
public struct SpacePlanningTarget
{
    public int WidthMm;
    public int DepthMm;
    public int HeightMm;

    public bool IsValid => InRange(WidthMm) && InRange(DepthMm) && InRange(HeightMm);
    public Vector3 SizeMm => new Vector3(WidthMm, HeightMm, DepthMm);

    public static bool TryParse(string width, string depth, string height,
        out SpacePlanningTarget target, out string error)
    {
        target = default;
        error = null;
        if (!int.TryParse(width, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
            !int.TryParse(depth, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) ||
            !int.TryParse(height, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
            !InRange(w) || !InRange(d) || !InRange(h))
        {
            error = "Enter whole millimetres from 1 to 50,000 for each dimension.";
            return false;
        }
        target = new SpacePlanningTarget { WidthMm = w, DepthMm = d, HeightMm = h };
        return true;
    }

    public Vector3 RemainingMm(float width, float depth, float height)
    {
        return new Vector3(WidthMm - width, HeightMm - height, DepthMm - depth);
    }

    public bool Fits(float width, float depth, float height)
    {
        if (!IsValid || !FiniteNonnegative(width) || !FiniteNonnegative(depth) || !FiniteNonnegative(height))
            return false;
        Vector3 remaining = RemainingMm(width, depth, height);
        // Less than one tenth of a millimetre only absorbs renderer bounds
        // floating-point error; do not round real overruns into a fit.
        return remaining.x >= -0.1f && remaining.y >= -0.1f && remaining.z >= -0.1f;
    }

    static bool InRange(int mm) => mm >= 1 && mm <= 50000;
    static bool FiniteNonnegative(float mm) => mm >= 0f && !float.IsNaN(mm) && !float.IsInfinity(mm);
}

public static class SpacePlanningTargetSelfTest
{
    public static int RunAll(out System.Collections.Generic.List<string> failures)
    {
        var failed = new System.Collections.Generic.List<string>();
        failures = failed;
        int total = 0;
        void Check(bool passed, string message) { total++; if (!passed) failed.Add(message); }
        Check(SpacePlanningTarget.TryParse("1200", "450", "1800", out var target, out _), "Valid whole mm parse.");
        Check(target.Fits(1200f, 450f, 1800f), "Exact overall dimensions fit.");
        Check(!target.Fits(1201f, 450f, 1800f), "Width overrun does not fit.");
        Check(!target.Fits(1200f, 451f, 1800f), "Depth overrun does not fit.");
        Check(!target.Fits(1200f, 450f, 1801f), "Height overrun does not fit.");
        Check(target.RemainingMm(1000f, 400f, 1600f) == new Vector3(200f, 200f, 50f), "Remaining axes preserve width/height/depth mapping.");
        Check(!target.Fits(float.NaN, 450f, 1800f), "Non-finite measurements are rejected.");
        Check(!SpacePlanningTarget.TryParse("", "450", "1800", out _, out _), "Blank input rejected.");
        Check(!SpacePlanningTarget.TryParse("1200", "-450", "1800", out _, out _), "Negative input rejected.");
        Check(!SpacePlanningTarget.TryParse("1200.5", "450", "1800", out _, out _), "Fractional mm rejected clearly.");
        Check(!SpacePlanningTarget.TryParse("1200", "450", "50001", out _, out _), "Out-of-range input rejected.");
        Check(!SpacePlanningTarget.TryParse("0", "450", "1800", out _, out _), "Zero input rejected.");
        return total;
    }
}
