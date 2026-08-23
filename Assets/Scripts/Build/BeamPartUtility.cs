using System;
using UnityEngine;

/// <summary>
/// Centralizes the V/H/T part-id convention and twist peg identity
/// (Peg A attaches to H / Peg B attaches to V).
/// </summary>
internal static class BeamPartUtility
{
    public static bool IsVertical(string partId)
    {
        return StartsWith(partId, 'V');
    }

    public static bool IsHorizontal(string partId)
    {
        return StartsWith(partId, 'H');
    }

    public static bool IsTwist(string partId)
    {
        return StartsWith(partId, 'T');
    }

    public static bool IsHorizontalLike(string partId)
    {
        return IsHorizontal(partId) || IsTwist(partId);
    }

    public static bool IsBeam(string partId)
    {
        return IsVertical(partId) || IsHorizontalLike(partId);
    }

    public static BeamKind GetKind(string partId)
    {
        if (IsVertical(partId)) return BeamKind.V;
        if (IsHorizontal(partId)) return BeamKind.H;
        if (IsTwist(partId)) return BeamKind.TwistH;
        return BeamKind.Unknown;
    }

    /// <summary>
    /// Peg A always attaches to a horizontal frame (H).
    /// Peg B always attaches to a vertical post (V).
    /// </summary>
    public static bool IsTwistPegB(AttachmentPoint peg)
    {
        if (peg == null)
            return false;
        if (peg.twistEndId == 1)
            return true;
        if (peg.twistEndId == 0)
            return false;
        string n = peg.name;
        return !string.IsNullOrEmpty(n) &&
               n.IndexOf("Peg_B", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsTwistPegA(AttachmentPoint peg)
    {
        if (peg == null)
            return false;
        if (peg.twistEndId == 0)
            return true;
        if (peg.twistEndId == 1)
            return false;
        string n = peg.name;
        return !string.IsNullOrEmpty(n) &&
               n.IndexOf("Peg_A", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsTwistRoot(Transform root)
    {
        if (root == null)
            return false;
        return IsTwist(root.name);
    }

    private static bool StartsWith(string value, char prefix)
    {
        return !string.IsNullOrEmpty(value) &&
               char.ToUpperInvariant(value[0]) == char.ToUpperInvariant(prefix);
    }
}
