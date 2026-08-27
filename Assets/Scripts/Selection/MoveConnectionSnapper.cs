using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fine-tunes a gizmo move so a free peg/hole on the moving set lands on a
/// free opposite connector in the scene. This is the same join placement
/// uses — no parts are created or destroyed.
/// </summary>
static class MoveConnectionSnapper
{
    const float FacingDot = -0.5f;
    const float HeightSlopMm = 8f;

    /// <summary>
    /// Extra translation that would mate the closest legal pair, or zero if
    /// nothing is within <paramref name="maxDistance"/>.
    /// </summary>
    public static Vector3 ComputeExtra(
        List<Transform> movingRoots,
        bool lockY,
        bool lockXZ,
        float maxDistance,
        LayerMask ghostMask)
    {
        if (movingRoots == null || movingRoots.Count == 0 || maxDistance <= 0f)
            return Vector3.zero;

        float maxSqr = maxDistance * maxDistance;
        float heightSlop = NeospaceUnits.Mm(HeightSlopMm);
        Vector3 best = Vector3.zero;
        float bestSqr = maxSqr;

        var movingAps = new List<AttachmentPoint>();
        foreach (Transform root in movingRoots)
        {
            if (root == null || root.GetComponent<PanelInstance>() != null)
                continue;
            movingAps.AddRange(root.GetComponentsInChildren<AttachmentPoint>(true));
        }

        if (movingAps.Count == 0)
            return Vector3.zero;

        List<AttachmentPoint> live = AttachmentPoint.Live;
        for (int i = 0; i < live.Count; i++)
        {
            AttachmentPoint other = live[i];
            if (other == null || other.isOccupied)
                continue;
            if (IsUnderAny(other.transform, movingRoots))
                continue;
            Transform otherRoot = other.transform.root;
            if (otherRoot == null || IsGhost(otherRoot, ghostMask))
                continue;

            bool otherVertical = BeamPartUtility.IsVertical(otherRoot.name);

            for (int m = 0; m < movingAps.Count; m++)
            {
                AttachmentPoint own = movingAps[m];
                if (own == null || own.role == other.role)
                    continue;

                Transform ownRoot = own.transform.root;
                if (ownRoot == null)
                    continue;
                bool ownVertical = BeamPartUtility.IsVertical(ownRoot.name);

                if (other.role == AttachmentPoint.PointRole.Peg &&
                    TwistRejects(other, ownVertical))
                    continue;
                if (own.role == AttachmentPoint.PointRole.Peg &&
                    TwistRejects(own, otherVertical))
                    continue;

                if (Vector3.Dot(own.transform.forward, other.transform.forward) > FacingDot)
                    continue;

                Vector3 raw = other.transform.position - own.transform.position;
                if (lockY && Mathf.Abs(raw.y) > heightSlop)
                    continue;
                if (lockXZ && (raw.x * raw.x + raw.z * raw.z) > heightSlop * heightSlop)
                    continue;

                Vector3 extra = raw;
                if (lockY) extra.y = 0f;
                if (lockXZ) extra.x = extra.z = 0f;

                float sqr = extra.sqrMagnitude;
                if (sqr < 1e-8f || sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                best = extra;
            }
        }

        return best;
    }

    static bool TwistRejects(AttachmentPoint hostPeg, bool incomingIsVertical)
    {
        if (hostPeg == null)
            return false;

        Transform hostRoot = hostPeg.transform.root;
        bool isTwist = hostPeg.ownerBeamKind == BeamKind.TwistH ||
                       (hostPeg.ownerBeamKind == BeamKind.Unknown &&
                        hostRoot != null && BeamPartUtility.IsTwist(hostRoot.name));
        if (!isTwist)
            return false;

        if (BeamPartUtility.IsTwistPegA(hostPeg) && incomingIsVertical)
            return true;
        if (BeamPartUtility.IsTwistPegB(hostPeg) && !incomingIsVertical)
            return true;

        if (hostRoot == null)
            return false;

        AttachmentPoint[] points = hostRoot.GetComponentsInChildren<AttachmentPoint>(true);
        for (int i = 0; i < points.Length; i++)
        {
            AttachmentPoint otherPeg = points[i];
            if (otherPeg == null ||
                otherPeg == hostPeg ||
                otherPeg.role != AttachmentPoint.PointRole.Peg ||
                !otherPeg.isOccupied ||
                otherPeg.pairedWith == null)
                continue;

            Transform occupantRoot = otherPeg.pairedWith.transform.root;
            BeamKind otherKind = otherPeg.pairedWith.ownerBeamKind;
            if (otherKind == BeamKind.Unknown && occupantRoot != null)
                otherKind = BeamPartUtility.GetKind(occupantRoot.name);
            bool otherIsVertical = otherKind == BeamKind.V;
            if (otherIsVertical == incomingIsVertical)
                return true;
        }

        return false;
    }

    static bool IsUnderAny(Transform t, List<Transform> roots)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root != null && (t == root || t.IsChildOf(root)))
                return true;
        }
        return false;
    }

    static bool IsGhost(Transform root, LayerMask ghostMask)
    {
        return (ghostMask.value & (1 << root.gameObject.layer)) != 0;
    }
}
