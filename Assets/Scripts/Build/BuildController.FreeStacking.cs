using System.Collections.Generic;
using UnityEngine;

public partial class BuildController
{
    /// <summary>
    /// Place a vertical frame seated on (or hanging from) a specific free peg
    /// WITHOUT the cursor-driven ghost: the category Part tools anchor on a
    /// peg first and choose the size on a scale afterwards. Runs the exact
    /// same candidate-hole + yaw + floor + overlap pipeline as the Expert
    /// ghost, then commits through the normal placement path (occupancy
    /// pairing, slot rescan, undo step).
    /// </summary>
    public bool TryStackVerticalOnPeg(
        string partId, AttachmentPoint peg, bool hangDown, out string message)
    {
        message = string.Empty;

        if (partDatabase == null)
        {
            message = "Missing PartDatabase";
            return false;
        }
        if (peg == null || peg.isOccupied)
        {
            message = "That peg is no longer free.";
            return false;
        }
        Transform hostRoot = peg.transform.root;
        if (hostRoot == null)
        {
            message = "Peg has no host root";
            return false;
        }
        if (TwistPegRejectsAttachment(peg, true, out string twistReason))
        {
            message = twistReason;
            return false;
        }

        GameObject ghostPrefab = partDatabase.GetGhostPrefabOrFallback(partId);
        if (ghostPrefab == null)
        {
            message = $"No prefab for {partId}";
            return false;
        }

        GetVerticalPlacementSettings(partId, out Vector3 rotationEuler, out float[] yawAngles);

        GameObject ghost = Instantiate(ghostPrefab);
        ghost.name = $"{partId}_StackProbe";
        int ghostLayer = FirstLayerIndex(ghostLayerMask);
        if (ghostLayer >= 0)
            SetLayerRecursively(ghost.transform, ghostLayer);

        try
        {
            AttachmentPoint[] all = ghost.GetComponentsInChildren<AttachmentPoint>(true);
            var holes = new List<AttachmentPoint>();
            foreach (AttachmentPoint ap in all)
            {
                if (ap != null && ap.role == AttachmentPoint.PointRole.Hole && !ap.isOccupied)
                    holes.Add(ap);
            }
            if (holes.Count == 0)
            {
                message = $"{partId} has no free holes";
                return false;
            }

            float floorY = float.NegativeInfinity;
            if (hangDown &&
                Physics.Raycast(peg.transform.position + Vector3.up * 0.1f, Vector3.down,
                    out RaycastHit floorUnderPeg, 200f, floorMask, QueryTriggerInteraction.Collide))
                floorY = floorUnderPeg.point.y;

            GetHostCollisionExceptions(hostRoot, out Transform allowedRoot, out Transform toleranceRoot);

            Quaternion baseRotation = Quaternion.Euler(rotationEuler);
            float candidateYEpsilon = Mathf.Max(0f, v3CandidateYEpsilon);
            string lastReason = "No candidate hole was valid";

            float[] yaws = yawAngles != null && yawAngles.Length > 0
                ? yawAngles
                : new[] { 0f, 90f, 180f, 270f };

            foreach (float yaw in yaws)
            {
                Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
                ghost.transform.rotation = rotation;
                Physics.SyncTransforms();

                var candidates = new List<AttachmentPoint>();
                if (hangDown)
                {
                    candidates.AddRange(holes);
                }
                else
                {
                    float minY = float.PositiveInfinity;
                    foreach (AttachmentPoint h in holes)
                        minY = Mathf.Min(minY, h.transform.position.y);
                    foreach (AttachmentPoint h in holes)
                    {
                        if (Mathf.Abs(h.transform.position.y - minY) <= candidateYEpsilon)
                            candidates.Add(h);
                    }
                }

                // Only holes facing back toward the host beam can receive its peg.
                Vector3 pegForward = peg.transform.forward;
                var facing = candidates.FindAll(h =>
                    Vector3.Dot(h.transform.forward, -pegForward) > 0.7f);
                if (facing.Count > 0)
                    candidates = facing;

                if (hangDown)
                {
                    candidates.Sort((left, right) =>
                    {
                        float yl = left.transform.position.y;
                        float yr = right.transform.position.y;
                        if (Mathf.Abs(yl - yr) > candidateYEpsilon)
                            return yr.CompareTo(yl);
                        return (left.transform.position - peg.transform.position).sqrMagnitude.CompareTo(
                            (right.transform.position - peg.transform.position).sqrMagnitude);
                    });
                }
                else
                {
                    candidates.Sort((left, right) =>
                        (left.transform.position - peg.transform.position).sqrMagnitude.CompareTo(
                            (right.transform.position - peg.transform.position).sqrMagnitude));
                }

                foreach (AttachmentPoint hole in candidates)
                {
                    Vector3 connectorOffset = hole.transform.position - ghost.transform.position;
                    Vector3 position = peg.transform.position - connectorOffset;
                    position += peg.transform.TransformVector(v3PegSnapOffsetLocal);

                    ghost.transform.position = position;
                    Physics.SyncTransforms();

                    if (hangDown && floorY > float.NegativeInfinity &&
                        TryGetWorldBoundsForFloor(ghost, out Bounds hangBounds) &&
                        hangBounds.min.y < floorY - NeospaceUnits.Mm(12f))
                    {
                        lastReason = "it would sink below the floor";
                        continue;
                    }

                    if (HasIllegalOverlap(ghost, allowedRoot, toleranceRoot, out string overlapReason))
                    {
                        lastReason = overlapReason;
                        continue;
                    }

                    var result = new GhostPlacementResult
                    {
                        hasPose = true,
                        isValid = true,
                        position = position,
                        rotation = rotation,
                        targetPegInScene = peg,
                        hostRoot = hostRoot,
                        chosenHoleNameOnBeam = hole.name,
                        debugInfo = $"stacked on {peg.name}" + (hangDown ? " (hanging)" : string.Empty)
                    };

                    PlaceRealBeamFromGhost(partId, result);
                    message = $"{partId} placed";
                    return true;
                }
            }

            message = $"{partId} doesn't fit there ({lastReason}).";
            return false;
        }
        finally
        {
            Destroy(ghost);
        }
    }
}
