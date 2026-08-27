using System;
using System.Collections.Generic;
using UnityEngine;

public partial class BuildController
{
    private GhostPlacementResult ComputeGhostH(string partId, GameObject ghost)
    {
        if (!AttachmentPointSceneQuery.HasAnyPlacedBeam(ghostLayerMask))
        {
            Quaternion baseRotation = Quaternion.Euler(h3RotationEuler);
            Quaternion firstRotation =
                Quaternion.LookRotation(Vector3.forward, Vector3.up) * baseRotation;

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
                    ? "First placement OK (H/T on floor)"
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

        return ComputeGhostHAtPoint(partId, ghost, hit.point);
    }

    /// <summary>
    /// Same strict H/T placement pipeline as the mouse ghost, but driven by an
    /// explicit world point. Used by Guided template tools so batch placements
    /// obey the exact same hole/peg + face + overlap rules as Expert mode.
    /// </summary>
    public GhostPlacementResult ComputeGhostHAtPoint(string partId, GameObject ghost, Vector3 snapPoint)
    {
        AttachmentPointSceneQuery.FindNearestFreeHoleAndPeg(
            snapPoint,
            h3MaxSnapDistance,
            ghostLayerMask,
            out AttachmentPoint nearestHole,
            out AttachmentPoint nearestPeg);

        if (nearestHole == null && nearestPeg == null)
            return InvalidPlacement("No free hole/peg nearby");

        bool useHoleHost = ShouldUseHoleHost(snapPoint, nearestHole, nearestPeg);
        AttachmentPoint hostPoint = useHoleHost ? nearestHole : nearestPeg;
        Transform hostRoot = hostPoint.transform.root;

        if (hostRoot == null)
            return InvalidPlacement("Connector has no host root");

        // Twist beams require opposite kinds on their two pegs (one V, one H).
        if (!useHoleHost &&
            TwistPegRejectsAttachment(nearestPeg, false, out string twistReason))
            return InvalidPlacement(twistReason);

        HostKind hostKind = InferHostKindFromRoot(hostRoot);
        int faceIndex = ConnectorNameUtility.GetFaceIndex(hostPoint.name);
        bool usedFallbackFace = faceIndex == 0;
        if (usedFallbackFace)
            faceIndex = 2;

        Vector3 faceOutLocal = GetSideOutLocal(hostKind, faceIndex);
        Vector3 faceOut = hostRoot.TransformDirection(faceOutLocal).normalized;
        Vector3 up = Vector3.up;
        Vector3 beamDirection = Vector3.Cross(up, faceOut);

        if (beamDirection.sqrMagnitude < 1e-6f)
        {
            // Top/bottom hole: face points up. Twist Peg A attaches to H and
            // can land here; the span runs perpendicular to the host beam.
            Vector3 hostAlong = HostPlanAxis(hostRoot);
            beamDirection = Vector3.Cross(faceOut, hostAlong);
            if (beamDirection.sqrMagnitude < 1e-6f)
                beamDirection = Vector3.Cross(faceOut, Vector3.right);
        }

        beamDirection.Normalize();
        if (beamDirection.sqrMagnitude < 1e-6f)
        {
            return InvalidPlacement(
                $"Bad face mapping (beam direction is zero). host={hostKind} face={faceIndex}");
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(beamDirection, up) *
            Quaternion.Euler(h3RotationEuler) *
            Quaternion.Euler(GetFaceEulerOffset(hostKind, faceIndex));

        // H into an H hole: roll 90° around the beam's local Z. The peg is
        // re-fitted to the host hole after this rotation (see the trial
        // loop), so gizmos stay on the joint instead of swinging off it.
        const float HOnHHoleRollZ = 90f;
        bool hOnHHole = useHoleHost &&
                        hostKind == HostKind.H &&
                        BeamPartUtility.IsHorizontal(partId);
        if (hOnHHole)
            targetRotation *= Quaternion.Euler(0f, 0f, HOnHHoleRollZ);

        AttachmentPoint.PointRole neededRole = useHoleHost
            ? AttachmentPoint.PointRole.Peg
            : AttachmentPoint.PointRole.Hole;

        var ownConnectors = CollectFreeConnectors(ghost, neededRole);
        if (BeamPartUtility.IsTwist(partId) && neededRole == AttachmentPoint.PointRole.Peg)
            ownConnectors.RemoveAll(peg => !TwistPegMatchesHost(peg, hostKind));
        if (ownConnectors.Count == 0)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = hostPoint.transform.position,
                rotation = targetRotation,
                targetHoleInScene = useHoleHost ? nearestHole : null,
                targetPegInScene = useHoleHost ? null : nearestPeg,
                hostRoot = hostRoot,
                debugInfo = useHoleHost
                    ? "H/T ghost has no free pegs"
                    : "H/T ghost has no free holes"
            };
        }

        ownConnectors.Sort(CompareConnectorsByName);
        LogConnectorOrder("sorted own connectors", neededRole, ownConnectors);

        List<AttachmentPoint> trialConnectors = BuildTrialConnectorOrder(
            ownConnectors,
            hostPoint,
            useHoleHost);
        LogConnectorOrder($"trial order (useHoleHost={useHoleHost})", neededRole, trialConnectors);

        GetHostCollisionExceptions(hostRoot, out Transform allowedRoot, out Transform toleranceRoot);

        // A regular H can pick Peg A or Peg B, which is a 180° end swap.
        // A twist is locked to one peg (B on V, A on H), so two of the four
        // post faces used to aim the body into the host and fail overlap.
        // Extra 180° flips keep that peg on the hole and send the body out.
        var rotations = new List<Quaternion>(4) { targetRotation };
        if (hOnHHole)
        {
            // Opposite roll if +90 intersects the host.
            Quaternion unrolled =
                Quaternion.LookRotation(beamDirection, up) *
                Quaternion.Euler(h3RotationEuler) *
                Quaternion.Euler(GetFaceEulerOffset(hostKind, faceIndex));
            rotations.Add(unrolled * Quaternion.Euler(0f, 0f, -HOnHHoleRollZ));
        }
        else if (BeamPartUtility.IsTwist(partId))
        {
            rotations.Add(Quaternion.AngleAxis(180f, Vector3.up) * targetRotation);
            if (faceOut.sqrMagnitude > 1e-8f)
                rotations.Add(Quaternion.AngleAxis(180f, faceOut) * targetRotation);
            if (beamDirection.sqrMagnitude > 1e-8f)
                rotations.Add(Quaternion.AngleAxis(180f, beamDirection) * targetRotation);
        }

        bool placed = false;
        string lastReason = "No connector candidate was valid";
        Vector3 bestPosition = ghost.transform.position;
        Quaternion bestRotation = targetRotation;
        string chosenPegName = string.Empty;
        string chosenHoleName = string.Empty;

        for (int r = 0; r < rotations.Count && !placed; r++)
        {
            Quaternion rotation = rotations[r];
            Vector3 along = (BeamPartUtility.IsTwist(partId) && r == 1)
                ? -beamDirection
                : beamDirection;

            ghost.transform.rotation = rotation;
            Physics.SyncTransforms();

            for (int i = 0; i < trialConnectors.Count; i++)
            {
                AttachmentPoint ownConnector = trialConnectors[i];
                Vector3 connectorOffset =
                    ownConnector.transform.position - ghost.transform.position;
                Vector3 basePosition = hostPoint.transform.position - connectorOffset;
                Vector3 position =
                    basePosition + faceOut * h3DepthOffset + along * h3LateralOffset;

                ghost.transform.position = position;
                Physics.SyncTransforms();

                if (HasIllegalOverlap(
                        ghost,
                        allowedRoot,
                        toleranceRoot,
                        out string overlapReason))
                {
                    lastReason = overlapReason;
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"ComputeGhostH: blocked connector={ownConnector.name} " +
                            $"rot={r} trialIndex={i} reason={overlapReason}");
                    }
                    continue;
                }

                placed = true;
                bestPosition = position;
                bestRotation = rotation;
                if (useHoleHost)
                    chosenPegName = ownConnector.name;
                else
                    chosenHoleName = ownConnector.name;

                if (debugLogs)
                {
                    Debug.Log(
                        $"ComputeGhostH: SUCCESS connector={ownConnector.name} " +
                        $"rot={r} trialIndex={i}");
                }
                break;
            }
        }

        string faceLabel = usedFallbackFace ? $"{faceIndex} (fallback)" : faceIndex.ToString();
        if (!placed)
        {
            return new GhostPlacementResult
            {
                hasPose = true,
                isValid = false,
                position = ghost.transform.position,
                rotation = bestRotation,
                targetHoleInScene = useHoleHost ? nearestHole : null,
                targetPegInScene = useHoleHost ? null : nearestPeg,
                hostRoot = hostRoot,
                debugInfo =
                    $"H/T blocked via {(useHoleHost ? "hole" : "peg")} " +
                    $"face={faceLabel} host={hostKind}: {lastReason}"
            };
        }

        return new GhostPlacementResult
        {
            hasPose = true,
            isValid = true,
            position = bestPosition,
            rotation = bestRotation,
            targetHoleInScene = useHoleHost ? nearestHole : null,
            targetPegInScene = useHoleHost ? null : nearestPeg,
            hostRoot = hostRoot,
            chosenPegNameOnBeam = chosenPegName,
            chosenHoleNameOnBeam = chosenHoleName,
            debugInfo = useHoleHost
                ? $"H/T OK via hole face={faceLabel} host={hostKind} peg={chosenPegName}" +
                  (hOnHHole ? " (H-on-H roll)" : "")
                : $"H/T OK via peg face={faceLabel} host={hostKind} hole={chosenHoleName}"
        };
    }

    private static bool ShouldUseHoleHost(
        Vector3 clickPoint,
        AttachmentPoint nearestHole,
        AttachmentPoint nearestPeg)
    {
        if (nearestHole == null)
            return false;
        if (nearestPeg == null)
            return true;

        float holeDistanceSquared =
            (nearestHole.transform.position - clickPoint).sqrMagnitude;
        float pegDistanceSquared =
            (nearestPeg.transform.position - clickPoint).sqrMagnitude;
        return holeDistanceSquared <= pegDistanceSquared;
    }

    private static List<AttachmentPoint> CollectFreeConnectors(
        GameObject instance,
        AttachmentPoint.PointRole role)
    {
        AttachmentPoint[] attachmentPoints =
            instance.GetComponentsInChildren<AttachmentPoint>(true);
        var result = new List<AttachmentPoint>();

        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (attachmentPoint != null &&
                attachmentPoint.role == role &&
                !attachmentPoint.isOccupied)
                result.Add(attachmentPoint);
        }

        return result;
    }

    private List<AttachmentPoint> BuildTrialConnectorOrder(
        List<AttachmentPoint> sortedConnectors,
        AttachmentPoint hostPoint,
        bool useHoleHost)
    {
        if (useHoleHost || sortedConnectors.Count <= 1)
            return sortedConnectors;

        var groups = new Dictionary<string, List<AttachmentPoint>>(StringComparer.Ordinal);
        for (int i = 0; i < sortedConnectors.Count; i++)
        {
            AttachmentPoint connector = sortedConnectors[i];
            string key = ConnectorNameUtility.GetGroupKey(connector != null ? connector.name : null);

            if (!groups.TryGetValue(key, out List<AttachmentPoint> group))
            {
                group = new List<AttachmentPoint>();
                groups[key] = group;
            }

            group.Add(connector);
        }

        var orderedGroups = new List<List<AttachmentPoint>>(groups.Values);
        orderedGroups.Sort((left, right) =>
        {
            AttachmentPoint leftCenter = left[(left.Count - 1) / 2];
            AttachmentPoint rightCenter = right[(right.Count - 1) / 2];

            float leftDistance = leftCenter != null
                ? (leftCenter.transform.position - hostPoint.transform.position).sqrMagnitude
                : float.MaxValue;
            float rightDistance = rightCenter != null
                ? (rightCenter.transform.position - hostPoint.transform.position).sqrMagnitude
                : float.MaxValue;

            int distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
                return distanceComparison;

            string leftName = leftCenter != null ? leftCenter.name : string.Empty;
            string rightName = rightCenter != null ? rightCenter.name : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        });

        var result = new List<AttachmentPoint>(sortedConnectors.Count);
        for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
        {
            List<AttachmentPoint> group = orderedGroups[groupIndex];
            int middle = (group.Count - 1) / 2;
            result.Add(group[middle]);

            for (int offset = 1; offset < group.Count; offset++)
            {
                int left = middle - offset;
                if (left >= 0)
                    result.Add(group[left]);

                int right = middle + offset;
                if (right < group.Count)
                    result.Add(group[right]);

                if (left < 0 && right >= group.Count)
                    break;
            }
        }

        if (debugLogs)
        {
            var groupDescriptions = new List<string>();
            foreach (KeyValuePair<string, List<AttachmentPoint>> pair in groups)
                groupDescriptions.Add($"{pair.Key}:{pair.Value.Count}");

            Debug.Log($"ComputeGhostH: grouped connectors = [{string.Join(", ", groupDescriptions)}]");
        }

        return result;
    }

    private static int CompareConnectorsByName(AttachmentPoint left, AttachmentPoint right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        string leftGroup = ConnectorNameUtility.GetGroupKey(leftName);
        string rightGroup = ConnectorNameUtility.GetGroupKey(rightName);

        int groupComparison = string.Compare(leftGroup, rightGroup, StringComparison.Ordinal);
        if (groupComparison != 0)
            return groupComparison;

        int indexComparison = ConnectorNameUtility.GetOrderIndex(leftName).CompareTo(
            ConnectorNameUtility.GetOrderIndex(rightName));
        return indexComparison != 0
            ? indexComparison
            : string.Compare(leftName, rightName, StringComparison.Ordinal);
    }

    private void LogConnectorOrder(
        string label,
        AttachmentPoint.PointRole role,
        List<AttachmentPoint> connectors)
    {
        if (!debugLogs)
            return;

        string names = string.Join(", ", connectors.ConvertAll(connector =>
        {
            string connectorName = connector != null ? connector.name : "null";
            return $"{connectorName}->idx:{ConnectorNameUtility.GetOrderIndex(connectorName)}";
        }));

        Debug.Log($"ComputeGhostH: {label} ({role}) = [{names}]");
    }

    static Vector3 HostPlanAxis(Transform hostRoot)
    {
        if (hostRoot == null)
            return Vector3.forward;
        if (FrameOverlapResolver.TryBuildRecord(hostRoot, out FrameOverlapResolver.FrameRecord rec))
        {
            Vector3 axis = rec.LengthAxis;
            axis.y = 0f;
            if (axis.sqrMagnitude > 1e-6f)
                return axis.normalized;
        }
        Vector3 fwd = hostRoot.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
    }

    /// <summary>
    /// Twist peg identity is fixed: Peg A always sits on an H/T host,
    /// Peg B always sits on a V host. Using the other peg put the HT mesh
    /// on the wrong member.
    /// </summary>
    private static bool TwistPegMatchesHost(AttachmentPoint peg, HostKind hostKind)
    {
        if (peg == null)
            return false;
        bool wantA = hostKind == HostKind.H || hostKind == HostKind.T;
        return wantA ? BeamPartUtility.IsTwistPegA(peg) : BeamPartUtility.IsTwistPegB(peg);
    }
}
