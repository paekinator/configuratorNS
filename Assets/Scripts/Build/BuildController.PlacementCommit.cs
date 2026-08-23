using UnityEngine;

public partial class BuildController
{
    private void PlaceRealBeamFromGhost(string partId, GhostPlacementResult result)
    {
        GameObject prefab = partDatabase != null
            ? partDatabase.GetRealPrefab(partId)
            : null;

        if (prefab == null)
        {
            Debug.LogWarning($"BuildController: no real prefab found for {partId}.");
            return;
        }

        AttachmentPoint targetScenePoint = ResolveTargetScenePoint(result);
        if (targetScenePoint != null && targetScenePoint.isOccupied)
        {
            if (debugLogs)
                Debug.Log($"BuildController: target connector {targetScenePoint.name} became occupied.");
            return;
        }

        GameObject instance = Instantiate(prefab, result.position, result.rotation);
        GetHostCollisionExceptions(
            result.hostRoot,
            out Transform allowedRoot,
            out Transform toleranceRoot);

        if (HasIllegalOverlap(instance, allowedRoot, toleranceRoot, out string overlapReason))
        {
            if (debugLogs)
                Debug.Log($"Blocked REAL {partId} placement: {overlapReason}");

            Destroy(instance);
            return;
        }

        var connections = instance.GetComponent<BeamConnections>();
        if (connections == null)
            connections = instance.AddComponent<BeamConnections>();

        if (targetScenePoint != null)
        {
            AttachmentPoint.PointRole requiredOwnRole =
                targetScenePoint.role == AttachmentPoint.PointRole.Hole
                    ? AttachmentPoint.PointRole.Peg
                    : AttachmentPoint.PointRole.Hole;
            string preferredOwnName = requiredOwnRole == AttachmentPoint.PointRole.Peg
                ? result.chosenPegNameOnBeam
                : result.chosenHoleNameOnBeam;

            AttachmentPoint ownPoint = FindBestOwnConnector(
                instance,
                requiredOwnRole,
                preferredOwnName,
                targetScenePoint.transform.position);

            if (ownPoint == null)
            {
                Debug.LogWarning(
                    $"BuildController: placed prefab {prefab.name} has no matching " +
                    $"{requiredOwnRole} connector [{preferredOwnName}]. Placement cancelled.");
                Destroy(instance);
                return;
            }

            PairAttachmentPoints(targetScenePoint, ownPoint, instance);
            connections.RegisterOccupiedScenePoint(targetScenePoint);
        }

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        QueuePromoteHostHToTByPegMix(result.hostRoot);
        BuildHistory.NotifyChanged();

        if (debugLogs)
            Debug.Log($"Placed REAL {partId}. {result.debugInfo}");
    }

    private static AttachmentPoint ResolveTargetScenePoint(GhostPlacementResult result)
    {
        if (result.targetHoleInScene != null)
            return result.targetHoleInScene;
        return result.targetPegInScene;
    }

    private static AttachmentPoint FindBestOwnConnector(
        GameObject instance,
        AttachmentPoint.PointRole role,
        string preferredName,
        Vector3 targetPosition)
    {
        AttachmentPoint[] attachmentPoints =
            instance.GetComponentsInChildren<AttachmentPoint>(true);
        AttachmentPoint nearestNamed = null;
        AttachmentPoint nearestFallback = null;
        float bestNamedDistance = Mathf.Infinity;
        float bestFallbackDistance = Mathf.Infinity;

        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (attachmentPoint == null || attachmentPoint.role != role)
                continue;

            float distance =
                (attachmentPoint.transform.position - targetPosition).sqrMagnitude;
            if (distance < bestFallbackDistance)
            {
                bestFallbackDistance = distance;
                nearestFallback = attachmentPoint;
            }

            if (!string.IsNullOrEmpty(preferredName) &&
                attachmentPoint.name == preferredName &&
                distance < bestNamedDistance)
            {
                bestNamedDistance = distance;
                nearestNamed = attachmentPoint;
            }
        }

        return nearestNamed != null ? nearestNamed : nearestFallback;
    }

    private static void PairAttachmentPoints(
        AttachmentPoint scenePoint,
        AttachmentPoint ownPoint,
        GameObject placedInstance)
    {
        scenePoint.isOccupied = true;
        scenePoint.occupant = placedInstance;
        scenePoint.pairedWith = ownPoint;

        ownPoint.isOccupied = true;
        Transform sceneRoot = scenePoint.transform.root;
        ownPoint.occupant = sceneRoot != null ? sceneRoot.gameObject : null;
        ownPoint.pairedWith = scenePoint;
    }
}
