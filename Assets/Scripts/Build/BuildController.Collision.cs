using System.Collections.Generic;
using UnityEngine;

public partial class BuildController
{
    private bool HasIllegalOverlap(
        GameObject instance,
        Transform allowedRoot,
        Transform toleranceRoot,
        out string reason)
    {
        return PlacementCollisionValidator.HasIllegalOverlap(
            instance,
            allowedRoot,
            toleranceRoot,
            floorMask,
            ghostLayerMask,
            panelBlockerMask,
            overlapMargin,
            hostPenetrationTolerance,
            out reason);
    }

    /// <summary>
    /// Overlap check for parts that were MOVED by the selection gizmo: same
    /// rules as placement with no host exceptions — flush peg-in-hole contacts
    /// with mated neighbors stay legal, real clipping does not.
    /// Pass <paramref name="ignoreRoots"/> so other parts in the same move
    /// are not treated as obstacles.
    /// </summary>
    public bool MovedPartOverlaps(
        GameObject instance,
        HashSet<Transform> ignoreRoots,
        out string reason)
    {
        return PlacementCollisionValidator.HasIllegalOverlap(
            instance,
            null,
            null,
            floorMask,
            ghostLayerMask,
            panelBlockerMask,
            overlapMargin,
            hostPenetrationTolerance,
            out reason,
            ignoreRoots);
    }

    public bool MovedPartOverlaps(GameObject instance, out string reason)
    {
        return MovedPartOverlaps(instance, null, out reason);
    }

    private void GetHostCollisionExceptions(
        Transform hostRoot,
        out Transform allowedRoot,
        out Transform toleranceRoot)
    {
        allowedRoot = strictNoOverlap ? null : hostRoot;
        toleranceRoot = strictNoOverlap ? hostRoot : null;
    }
}
