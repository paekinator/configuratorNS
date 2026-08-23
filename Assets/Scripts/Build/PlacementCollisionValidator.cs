using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Performs broad-phase bounds checks followed by exact collider penetration
/// checks without changing the caller's collider enabled states.
/// </summary>
internal static class PlacementCollisionValidator
{
    public static bool HasIllegalOverlap(
        GameObject instance,
        Transform allowedRoot,
        Transform toleranceRoot,
        LayerMask floorMask,
        LayerMask ghostLayerMask,
        LayerMask panelBlockerMask,
        float overlapMargin,
        float hostPenetrationTolerance,
        out string reason)
    {
        reason = string.Empty;

        if (instance == null)
        {
            reason = "Missing placement instance";
            return true;
        }

        Collider[] allOwnColliders = instance.GetComponentsInChildren<Collider>(true);
        var activeOwnColliders = new List<Collider>(allOwnColliders.Length);
        for (int i = 0; i < allOwnColliders.Length; i++)
        {
            Collider ownCollider = allOwnColliders[i];
            if (ownCollider != null && ownCollider.enabled)
                activeOwnColliders.Add(ownCollider);
        }

        if (activeOwnColliders.Count == 0)
            return false;

        if (!TryGetBroadPhaseBounds(instance, activeOwnColliders, out Bounds bounds))
            return false;

        Collider[] hits;
        try
        {
            SetEnabled(activeOwnColliders, false);
            Physics.SyncTransforms();

            Vector3 margin = Vector3.one * Mathf.Max(0f, overlapMargin);
            Vector3 halfExtents = bounds.extents - margin;
            halfExtents.x = Mathf.Max(halfExtents.x, 0.0005f);
            halfExtents.y = Mathf.Max(halfExtents.y, 0.0005f);
            halfExtents.z = Mathf.Max(halfExtents.z, 0.0005f);

            hits = Physics.OverlapBox(
                bounds.center,
                halfExtents,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
        }
        finally
        {
            SetEnabled(activeOwnColliders, true);
            Physics.SyncTransforms();
        }

        Transform instanceRoot = instance.transform;
        float safeHostTolerance = Mathf.Max(0f, hostPenetrationTolerance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || IsSameOrChildOf(hit.transform, instanceRoot))
                continue;

            if (allowedRoot != null && IsSameOrChildOf(hit.transform, allowedRoot))
                continue;

            if (IsInMask(hit.gameObject.layer, ghostLayerMask) ||
                IsInMask(hit.gameObject.layer, floorMask))
                continue;

            if (hit.GetComponentInParent<AttachmentPoint>() != null)
                continue;

            if (!TryGetWorstPenetrationDepth(activeOwnColliders, hit, out float worstDepth))
                continue;

            // Flush-fit assemblies produce sub-tolerance contacts with EVERY neighbor
            // they plug into (a beam spanning two posts touches both), so the small
            // penetration tolerance applies to any hit, not only the designated host.
            // This includes panel blockers: a post standing flush on a paneled bay's
            // boundary beam may kiss the blocker by float error, which is legal —
            // only a real intrusion into the bay opening should refuse placement.
            if (worstDepth <= safeHostTolerance)
                continue;

            if (IsInMask(hit.gameObject.layer, panelBlockerMask))
            {
                reason = $"panel blocker: {hit.name} depth={worstDepth:F4}";
                return true;
            }

            if (toleranceRoot != null && IsSameOrChildOf(hit.transform, toleranceRoot))
            {
                reason =
                    $"host penetration {worstDepth:F4} > tol {safeHostTolerance:F4} on {hit.name}";
                return true;
            }

            Transform hitRoot = hit.transform.root;

            // A neighbor the instance is geometrically plugged into (an own connector
            // aligned with one of its opposite-role connectors) is a legal contact,
            // but only for the shallow touch of a flush fit - never real clipping.
            if (worstDepth <= MatedContactMaxDepth && HasMatedConnector(instanceRoot, hitRoot))
                continue;

            reason =
                $"own={instance.name} hit={hit.name} " +
                $"hitRoot={hitRoot.name} depth={worstDepth:F4}";
            return true;
        }

        return false;
    }

    private static bool TryGetBroadPhaseBounds(
        GameObject instance,
        List<Collider> ownColliders,
        out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Encapsulate(renderer.bounds, ref bounds, ref hasBounds);
        }

        for (int i = 0; i < ownColliders.Count; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null)
                continue;

            Encapsulate(ownCollider.bounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void Encapsulate(Bounds candidate, ref Bounds combined, ref bool hasBounds)
    {
        if (candidate.size.sqrMagnitude <= 1e-12f)
            return;

        if (!hasBounds)
        {
            combined = candidate;
            hasBounds = true;
            return;
        }

        combined.Encapsulate(candidate);
    }

    private static bool TryGetWorstPenetrationDepth(
        List<Collider> ownColliders,
        Collider other,
        out float worstDepth)
    {
        worstDepth = 0f;
        if (other == null)
            return false;

        for (int i = 0; i < ownColliders.Count; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null || !ownCollider.enabled)
                continue;

            if (!Physics.ComputePenetration(
                    ownCollider,
                    ownCollider.transform.position,
                    ownCollider.transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out _,
                    out float distance))
                continue;

            worstDepth = Mathf.Max(worstDepth, distance);
        }

        return worstDepth > 0f;
    }

    private static void SetEnabled(List<Collider> colliders, bool enabled)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    /// <summary>Max distance for a peg/hole pair to count as "plugged in".</summary>
    const float MatedConnectorDistance = 0.02f;

    /// <summary>Max penetration allowed with a mated neighbor (flush-fit slack).</summary>
    const float MatedContactMaxDepth = 0.02f;

    static bool HasMatedConnector(Transform instanceRoot, Transform otherRoot)
    {
        if (instanceRoot == null || otherRoot == null)
            return false;

        AttachmentPoint[] own = instanceRoot.GetComponentsInChildren<AttachmentPoint>(true);
        AttachmentPoint[] other = otherRoot.GetComponentsInChildren<AttachmentPoint>(true);
        float maxSqr = MatedConnectorDistance * MatedConnectorDistance;

        for (int i = 0; i < own.Length; i++)
        {
            AttachmentPoint a = own[i];
            if (a == null) continue;

            for (int j = 0; j < other.Length; j++)
            {
                AttachmentPoint b = other[j];
                if (b == null || b.role == a.role) continue;

                if ((a.transform.position - b.transform.position).sqrMagnitude <= maxSqr)
                    return true;
            }
        }

        return false;
    }

    private static bool IsSameOrChildOf(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }

    private static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
