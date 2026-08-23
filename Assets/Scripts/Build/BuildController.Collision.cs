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
    /// </summary>
    public bool MovedPartOverlaps(GameObject instance, out string reason)
    {
        return HasIllegalOverlap(instance, null, null, out reason);
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
