using System.Collections.Generic;
using UnityEngine;

public partial class BuildController
{
    private static readonly float[] DefaultVerticalYawAngles = { 0f, 90f, 180f, 270f };

    private GhostPlacementResult ComputeGhostV(string partId, GameObject ghost)
    {
        GetVerticalPlacementSettings(partId, out Vector3 rotationEuler, out float[] yawAngles);

        if (!AttachmentPointSceneQuery.HasAnyPlacedBeam(ghostLayerMask))
        {
            Quaternion firstRotation = Quaternion.Euler(rotationEuler);
            if (!TryPlaceFirstOnFloor(
                    ghost,
                    firstRotation,
                    out Vector3 firstPosition,
                    out string failureReason))
                return InvalidPlacement(failureReason);

            bool isValid = !HasIllegalOverlap(ghost, null, null, out string overlapReason);
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = isValid,
                position = firstPosition,
                rotation = firstRotation,
                debugInfo = isValid
                    ? "First placement OK (V on floor)"
                    : $"First placement blocked: {overlapReason}"
            };
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                placementRayMask,
                QueryTriggerInteraction.Ignore))
            return InvalidPlacement("No placement surface under cursor");

        AttachmentPoint nearestPeg = AttachmentPointSceneQuery.FindNearestFree(
            hit.point,
            v3MaxSnapDistanceToPeg,
            AttachmentPoint.PointRole.Peg,
            ghostLayerMask);

        if (nearestPeg == null)
            return InvalidPlacement("No free peg nearby");

        Transform hostRoot = nearestPeg.transform.root;
        if (hostRoot == null)
            return InvalidPlacement("Peg has no host root");

        // Twist beams require opposite kinds on their two pegs (one V, one H).
        if (TwistPegRejectsAttachment(nearestPeg, true, out string twistReason))
            return InvalidPlacement(twistReason);

        AttachmentPoint[] attachmentPoints =
            ghost.GetComponentsInChildren<AttachmentPoint>(true);
        var holes = new List<AttachmentPoint>();

        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (attachmentPoint != null &&
                attachmentPoint.role == AttachmentPoint.PointRole.Hole &&
                !attachmentPoint.isOccupied)
                holes.Add(attachmentPoint);
        }

        if (holes.Count == 0)
            return InvalidPlacement("V ghost has no free holes");

        GetHostCollisionExceptions(hostRoot, out Transform allowedRoot, out Transform toleranceRoot);

        // Which hole plugs onto the peg follows the cursor: aiming BELOW the
        // host peg drops the frame downwards (highest hole that still keeps
        // it above the floor — hang free, or stand exactly on the ground),
        // aiming above stands it on its BOTTOM hole. A small deadband keeps
        // hovering the beam itself on the default (stand-up) behaviour.
        bool hangDown = hit.point.y < nearestPeg.transform.position.y - NeospaceUnits.Mm(10f);

        // Floor level under the peg: the hard limit for hanging placements.
        float floorY = float.NegativeInfinity;
        if (hangDown &&
            Physics.Raycast(nearestPeg.transform.position + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit floorUnderPeg, 200f, floorMask, QueryTriggerInteraction.Collide))
            floorY = floorUnderPeg.point.y;

        Quaternion baseRotation = Quaternion.Euler(rotationEuler);
        bool found = false;
        Vector3 bestPosition = ghost.transform.position;
        Quaternion bestRotation = ghost.transform.rotation;
        string chosenHoleName = string.Empty;
        string bestInfo = string.Empty;
        string lastReason = "No candidate hole was valid";
        float candidateYEpsilon = Mathf.Max(0f, v3CandidateYEpsilon);

        for (int yawIndex = 0; yawIndex < yawAngles.Length && !found; yawIndex++)
        {
            float yaw = yawAngles[yawIndex];
            Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
            ghost.transform.rotation = rotation;
            Physics.SyncTransforms();

            var candidates = new List<AttachmentPoint>();
            if (hangDown)
            {
                // Hanging: every hole level is a candidate, highest first. The
                // floor check in the loop rejects levels that would push the
                // frame underground, so it settles on the highest legal hole.
                candidates.AddRange(holes);
            }
            else
            {
                float minY = float.PositiveInfinity;
                for (int i = 0; i < holes.Count; i++)
                    minY = Mathf.Min(minY, holes[i].transform.position.y);

                for (int i = 0; i < holes.Count; i++)
                {
                    if (Mathf.Abs(holes[i].transform.position.y - minY) <= candidateYEpsilon)
                        candidates.Add(holes[i]);
                }
            }

            // The peg points out of the host beam's end, so the receiving hole must
            // face back toward the beam. Keep only holes whose outward normal opposes
            // the peg direction; a wrong-side hole would shift the post into the beam.
            Vector3 pegForward = nearestPeg.transform.forward;
            var facing = candidates.FindAll(h =>
                Vector3.Dot(h.transform.forward, -pegForward) > 0.7f);
            if (facing.Count > 0)
                candidates = facing;

            if (hangDown)
            {
                // Highest hole first (deepest drop), proximity as tie-break
                // between the holes that share a level.
                candidates.Sort((left, right) =>
                {
                    float yl = left.transform.position.y;
                    float yr = right.transform.position.y;
                    if (Mathf.Abs(yl - yr) > candidateYEpsilon)
                        return yr.CompareTo(yl);
                    return (left.transform.position - nearestPeg.transform.position).sqrMagnitude.CompareTo(
                        (right.transform.position - nearestPeg.transform.position).sqrMagnitude);
                });
            }
            else
            {
                candidates.Sort((left, right) =>
                    (left.transform.position - nearestPeg.transform.position).sqrMagnitude.CompareTo(
                        (right.transform.position - nearestPeg.transform.position).sqrMagnitude));
            }

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                AttachmentPoint hole = candidates[candidateIndex];
                Vector3 connectorOffset = hole.transform.position - ghost.transform.position;
                Vector3 position = nearestPeg.transform.position - connectorOffset;
                position += nearestPeg.transform.TransformVector(v3PegSnapOffsetLocal);

                ghost.transform.position = position;
                Physics.SyncTransforms();

                // Never let a hanging frame phase through the floor: it either
                // hangs fully above it or stands exactly on it (small slop for
                // the touching case; the next hole level is a whole 88 mm up).
                if (hangDown && floorY > float.NegativeInfinity &&
                    TryGetWorldBoundsForFloor(ghost, out Bounds hangBounds) &&
                    hangBounds.min.y < floorY - NeospaceUnits.Mm(12f))
                {
                    lastReason = "would sink below the floor";
                    continue;
                }

                if (HasIllegalOverlap(
                        ghost,
                        allowedRoot,
                        toleranceRoot,
                        out string overlapReason))
                {
                    lastReason = overlapReason;
                    continue;
                }

                found = true;
                bestPosition = position;
                bestRotation = rotation;
                chosenHoleName = hole.name;
                bestInfo = $"V snap OK yaw={yaw} hole={hole.name}" +
                           (hangDown ? " (dropped below peg)" : string.Empty);
                break;
            }
        }

        if (!found)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = ghost.transform.position,
                rotation = ghost.transform.rotation,
                targetPegInScene = nearestPeg,
                hostRoot = hostRoot,
                debugInfo = $"V snap blocked: {lastReason}"
            };
        }

        return new GhostPlacementResult
        {
            hasPose = true,
            isValid = true,
            position = bestPosition,
            rotation = bestRotation,
            targetPegInScene = nearestPeg,
            hostRoot = hostRoot,
            chosenHoleNameOnBeam = chosenHoleName,
            debugInfo = bestInfo
        };
    }

    private void GetVerticalPlacementSettings(
        string partId,
        out Vector3 rotationEuler,
        out float[] yawAngles)
    {
        rotationEuler = v3RotationEuler;
        yawAngles = v3YawAngles != null && v3YawAngles.Length > 0
            ? v3YawAngles
            : DefaultVerticalYawAngles;

        if (partDatabase == null ||
            !partDatabase.TryGet(partId, out PartDatabase.PartEntry entry) ||
            entry == null ||
            !entry.overrideVPlacement)
            return;

        rotationEuler = entry.vRotationEuler;
        if (entry.vYawAngles != null && entry.vYawAngles.Length > 0)
            yawAngles = entry.vYawAngles;
    }
}
