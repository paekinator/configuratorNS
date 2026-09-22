using System;
using UnityEngine;

/// <summary>
/// Finds the positions along a channel at which the physical cross-section
/// of a veneer/cap penetrates a panel. All dimensions and returned offsets
/// are in world units. Touching surfaces alone do not count as penetration.
/// </summary>
public static class FinishPanelMasking
{
    public const float ContactEpsilonMm = 0.001f;
    const double AxisEpsilon = 1e-7;
    const double OrthogonalTolerance = 1e-4;

    /// <summary>
    /// Sweeps a zero-length rectangular cross-section with center
    /// origin + normalized(lengthAxis) * t against the panel OBB. Returns
    /// the open interval (lo, hi) where their interiors overlap. Directions
    /// are normalized internally and must form orthogonal, nonzero bases;
    /// nonfinite inputs and zero/negative physical dimensions return false.
    /// The caller includes any frame-face/thickness offset in origin.
    /// </summary>
    public static bool TrySweep(Vector3 origin, Vector3 lengthAxis,
        Vector3 widthAxis, Vector3 outwardAxis, float halfWidth,
        float halfThickness, FinishGenerator.PanelBox panel,
        out float lo, out float hi)
    {
        lo = hi = 0f;
        if (!Finite(origin) || !Finite(panel.Center) ||
            !Positive(halfWidth) || !Positive(halfThickness) ||
            !Positive(panel.HalfU) || !Positive(panel.HalfV) || !Positive(panel.HalfN))
            return false;

        if (!TryBasis(lengthAxis, widthAxis, outwardAxis, out Vector3 a0, out Vector3 a1, out Vector3 a2) ||
            !TryBasis(panel.AxisU, panel.AxisV, panel.Normal, out Vector3 b0, out Vector3 b1, out Vector3 b2))
            return false;

        double epsilon = NeospaceUnits.Mm(ContactEpsilonMm);
        if (double.IsNaN(epsilon) || double.IsInfinity(epsilon) || epsilon <= 0.0)
            return false;

        double lower = double.NegativeInfinity;
        double upper = double.PositiveInfinity;

        // The complete OBB separating-axis set. The moving box has zero
        // length and nonzero width/thickness. On each axis, its separation
        // is affine in t, so the allowed set is an interval. Intersecting
        // all 15 intervals gives the exact sweep (within contact epsilon).
        for (int i = 0; i < 3; i++)
        {
            Vector3 a = Axis(i, a0, a1, a2);
            Vector3 b = Axis(i, b0, b1, b2);
            if (!Clip(a) || !Clip(b)) return false;
            for (int j = 0; j < 3; j++)
                if (!Clip(Vector3.Cross(a, Axis(j, b0, b1, b2)))) return false;
        }

        if (double.IsInfinity(lower) || double.IsInfinity(upper) ||
            lower < -float.MaxValue || upper > float.MaxValue)
            return false;
        float resultLo = (float)lower;
        float resultHi = (float)upper;
        if (!(resultLo < resultHi)) return false;
        lo = resultLo;
        hi = resultHi;
        return true;

        bool Clip(Vector3 candidate)
        {
            double magnitude = Math.Sqrt(Dot(candidate, candidate));
            if (magnitude < AxisEpsilon) return true; // parallel-axis cross product
            // Calculate normalized projections in double precision; a small
            // contact tolerance should not be inflated by axis length.
            double separation =
                (((double)origin.x - panel.Center.x) * candidate.x +
                 ((double)origin.y - panel.Center.y) * candidate.y +
                 ((double)origin.z - panel.Center.z) * candidate.z) / magnitude;
            double slope = Dot(a0, candidate) / magnitude;
            double radius =
                (halfWidth * Math.Abs(Dot(a1, candidate)) +
                 halfThickness * Math.Abs(Dot(a2, candidate)) +
                 panel.HalfU * Math.Abs(Dot(b0, candidate)) +
                 panel.HalfV * Math.Abs(Dot(b1, candidate)) +
                 panel.HalfN * Math.Abs(Dot(b2, candidate))) / magnitude;
            double interiorRadius = radius - epsilon;
            if (interiorRadius <= 0.0) return false;

            if (Math.Abs(slope) < AxisEpsilon)
                return Math.Abs(separation) < interiorRadius;

            double first = (-interiorRadius - separation) / slope;
            double last = (interiorRadius - separation) / slope;
            if (first > last) { double swap = first; first = last; last = swap; }
            lower = Math.Max(lower, first);
            upper = Math.Min(upper, last);
            return lower < upper;
        }
    }

    /// <summary>Whether a finite part centered at origin penetrates the panel.</summary>
    public static bool Intersects(Vector3 origin, Vector3 lengthAxis,
        Vector3 widthAxis, Vector3 outwardAxis, float halfLength,
        float halfWidth, float halfThickness, FinishGenerator.PanelBox panel)
    {
        return Positive(halfLength) &&
            TrySweep(origin, lengthAxis, widthAxis, outwardAxis, halfWidth,
                halfThickness, panel, out float lo, out float hi) &&
            lo < halfLength && hi > -halfLength;
    }

    static bool TryBasis(Vector3 x, Vector3 y, Vector3 z,
        out Vector3 nx, out Vector3 ny, out Vector3 nz)
    {
        nx = ny = nz = Vector3.zero;
        if (!TryUnit(x, out nx) || !TryUnit(y, out ny) || !TryUnit(z, out nz)) return false;
        return Math.Abs(Dot(nx, ny)) <= OrthogonalTolerance &&
               Math.Abs(Dot(nx, nz)) <= OrthogonalTolerance &&
               Math.Abs(Dot(ny, nz)) <= OrthogonalTolerance;
    }

    static bool TryUnit(Vector3 value, out Vector3 unit)
    {
        unit = Vector3.zero;
        if (!Finite(value)) return false;
        double magnitude = Math.Sqrt(Dot(value, value));
        if (magnitude < AxisEpsilon) return false;
        unit = new Vector3((float)(value.x / magnitude),
            (float)(value.y / magnitude), (float)(value.z / magnitude));
        return true;
    }

    static Vector3 Axis(int i, Vector3 x, Vector3 y, Vector3 z) => i == 0 ? x : i == 1 ? y : z;
    static double Dot(Vector3 a, Vector3 b) =>
        (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
    static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    static bool Positive(float value) => Finite(value) && value > 0f;
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
