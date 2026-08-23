using UnityEngine;

/// <summary>
/// Rotation quantization for configuration codes.
///
/// Fast path: the snap pipeline only ever produces axis-aligned poses, so a
/// rotation is normally one of the 24 orientations of a cube — stored as a
/// single byte. Anything that doesn't match (within tolerance) falls back to
/// euler angles in 0.1° steps, which reproduce the pose to well under the
/// hole-pairing tolerance.
/// </summary>
public static class PoseQuantizer
{
    /// <summary>Max deviation (degrees) for a rotation to count as one of the 24.</summary>
    public const float OrientationToleranceDeg = 0.5f;

    static readonly Quaternion[] Orientations = BuildOrientations();

    /// <summary>
    /// Deterministic order: forward through +X,-X,+Y,-Y,+Z,-Z, up through the
    /// same list, keeping only perpendicular pairs — 24 total. This order is
    /// part of the v1 format; never change it.
    /// </summary>
    static Quaternion[] BuildOrientations()
    {
        Vector3[] axes =
        {
            Vector3.right, Vector3.left,
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back
        };

        var list = new Quaternion[24];
        int n = 0;
        foreach (Vector3 forward in axes)
        foreach (Vector3 up in axes)
        {
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.5f)
                continue;
            list[n++] = Quaternion.LookRotation(forward, up);
        }
        return list;
    }

    public static bool TryGetOrientationIndex(Quaternion rotation, out int index)
    {
        for (int i = 0; i < Orientations.Length; i++)
        {
            if (Quaternion.Angle(rotation, Orientations[i]) <= OrientationToleranceDeg)
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    public static Quaternion FromOrientationIndex(int index) =>
        Orientations[index];

    public static bool IsValidOrientationIndex(int index) =>
        index >= 0 && index < Orientations.Length;

    /// <summary>Euler in 0.1° steps, each wrapped into 0–3599.</summary>
    public static void ToEulerDeci(Quaternion rotation, out int x, out int y, out int z)
    {
        Vector3 e = rotation.eulerAngles;
        x = WrapDeci(e.x);
        y = WrapDeci(e.y);
        z = WrapDeci(e.z);
    }

    public static Quaternion FromEulerDeci(int x, int y, int z) =>
        Quaternion.Euler(x * 0.1f, y * 0.1f, z * 0.1f);

    static int WrapDeci(float degrees)
    {
        int deci = Mathf.RoundToInt(degrees * 10f) % 3600;
        return deci < 0 ? deci + 3600 : deci;
    }

    // ------------------------------------------------------------------
    // Slot axes
    // ------------------------------------------------------------------

    /// <summary>Snap a (roughly axis-aligned) normal to its dominant world axis.</summary>
    public static SlotAxis ToSlotAxis(Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az)
            return normal.x >= 0f ? SlotAxis.PlusX : SlotAxis.MinusX;
        if (ay >= az)
            return normal.y >= 0f ? SlotAxis.PlusY : SlotAxis.MinusY;
        return normal.z >= 0f ? SlotAxis.PlusZ : SlotAxis.MinusZ;
    }

    public static Vector3 FromSlotAxis(SlotAxis axis) => axis switch
    {
        SlotAxis.PlusX => Vector3.right,
        SlotAxis.MinusX => Vector3.left,
        SlotAxis.PlusY => Vector3.up,
        SlotAxis.MinusY => Vector3.down,
        SlotAxis.PlusZ => Vector3.forward,
        _ => Vector3.back
    };
}
