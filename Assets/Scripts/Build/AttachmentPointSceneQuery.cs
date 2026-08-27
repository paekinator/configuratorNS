using UnityEngine;

/// <summary>
/// Read-only scene queries for available beam connectors.
/// </summary>
internal static class AttachmentPointSceneQuery
{
    public static AttachmentPoint FindNearestFree(
        Vector3 point,
        float maxDistance,
        AttachmentPoint.PointRole role,
        LayerMask ghostLayerMask)
    {
        AttachmentPoint nearest = null;
        float bestDistanceSquared = Mathf.Infinity;
        float maxDistanceSquared = Mathf.Max(0f, maxDistance);
        maxDistanceSquared *= maxDistanceSquared;

        var attachmentPoints = AttachmentPoint.Live;

        for (int i = 0; i < attachmentPoints.Count; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (!IsAvailableScenePoint(attachmentPoint, role, ghostLayerMask))
                continue;

            float distanceSquared =
                (attachmentPoint.transform.position - point).sqrMagnitude;

            if (distanceSquared > maxDistanceSquared || distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            nearest = attachmentPoint;
        }

        return nearest;
    }

    public static void FindNearestFreeHoleAndPeg(
        Vector3 point,
        float maxDistance,
        LayerMask ghostLayerMask,
        out AttachmentPoint nearestHole,
        out AttachmentPoint nearestPeg)
    {
        nearestHole = null;
        nearestPeg = null;

        float bestHoleDistanceSquared = Mathf.Infinity;
        float bestPegDistanceSquared = Mathf.Infinity;
        float maxDistanceSquared = Mathf.Max(0f, maxDistance);
        maxDistanceSquared *= maxDistanceSquared;

        var attachmentPoints = AttachmentPoint.Live;

        for (int i = 0; i < attachmentPoints.Count; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (!IsAvailableScenePoint(attachmentPoint, attachmentPoint.role, ghostLayerMask))
                continue;

            float distanceSquared =
                (attachmentPoint.transform.position - point).sqrMagnitude;
            if (distanceSquared > maxDistanceSquared)
                continue;

            if (attachmentPoint.role == AttachmentPoint.PointRole.Hole &&
                distanceSquared < bestHoleDistanceSquared)
            {
                bestHoleDistanceSquared = distanceSquared;
                nearestHole = attachmentPoint;
            }
            else if (attachmentPoint.role == AttachmentPoint.PointRole.Peg &&
                     distanceSquared < bestPegDistanceSquared)
            {
                bestPegDistanceSquared = distanceSquared;
                nearestPeg = attachmentPoint;
            }
        }
    }

    public static bool HasAnyPlacedBeam(LayerMask ghostLayerMask)
    {
        var attachmentPoints = AttachmentPoint.Live;

        for (int i = 0; i < attachmentPoints.Count; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (attachmentPoint == null)
                continue;

            Transform root = attachmentPoint.transform.root;
            if (root == null || IsInMask(root.gameObject.layer, ghostLayerMask))
                continue;

            if (BeamPartUtility.IsBeam(root.name) ||
                attachmentPoint.ownerBeamKind != BeamKind.Unknown)
                return true;
        }

        return false;
    }

    private static bool IsAvailableScenePoint(
        AttachmentPoint attachmentPoint,
        AttachmentPoint.PointRole expectedRole,
        LayerMask ghostLayerMask)
    {
        if (attachmentPoint == null ||
            attachmentPoint.role != expectedRole ||
            attachmentPoint.isOccupied)
            return false;

        Transform root = attachmentPoint.transform.root;
        return root != null && !IsInMask(root.gameObject.layer, ghostLayerMask);
    }

    private static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
