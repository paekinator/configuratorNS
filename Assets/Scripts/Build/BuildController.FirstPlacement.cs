using UnityEngine;

public partial class BuildController
{
    private bool TryPlaceFirstOnFloor(
        GameObject ghost,
        Quaternion rotation,
        out Vector3 position,
        out string reason)
    {
        position = Vector3.zero;
        reason = string.Empty;

        if (ghost == null)
        {
            reason = "Missing ghost instance";
            return false;
        }

        if (cam == null)
        {
            reason = "Missing Camera";
            return false;
        }

        Ray floorRay = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                floorRay,
                out RaycastHit floorHit,
                500f,
                floorMask,
                QueryTriggerInteraction.Collide))
        {
            reason = "No floor hit (check floorMask + collider)";
            return false;
        }

        // Same 88 mm module grid the Guided templates snap to, so a structure
        // started with the Parts tool always lines up with template work.
        Vector3 snapped = T1PostsPlanner.SnapGround(floorHit.point);

        position = floorHit.point;
        position.y = floorHit.point.y;
        ghost.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (TryGetWorldBoundsForFloor(ghost, out Bounds bounds))
        {
            // Align the part's footprint CENTER — not its pivot — to the
            // snapped grid point, so the ghost sits right under the cursor
            // even for prefabs with off-centre pivots (H/T beams).
            position.x += snapped.x - bounds.center.x;
            position.z += snapped.z - bounds.center.z;

            float targetMinimumY = floorHit.point.y + Mathf.Max(0f, firstSurfaceClearance);
            float deltaY = targetMinimumY - bounds.min.y;

            if (!float.IsNaN(deltaY) && !float.IsInfinity(deltaY))
                position.y += deltaY;
        }
        else
        {
            position.x = snapped.x;
            position.z = snapped.z;
            position.y = floorHit.point.y + Mathf.Max(0f, firstSurfaceClearance);
        }

        ghost.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        return true;
    }

    private static bool TryGetWorldBoundsForFloor(GameObject instance, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            AddBounds(renderer.bounds, ref bounds, ref hasBounds);
        }

        if (hasBounds)
            return true;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            AddBounds(collider.bounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void AddBounds(Bounds candidate, ref Bounds combined, ref bool hasBounds)
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
}
