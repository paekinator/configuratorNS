using System.Collections.Generic;
using UnityEngine;

public class BeamConnections : MonoBehaviour
{
    // Attachment points (can include BOTH ends) that should be freed if this beam is deleted
    [SerializeField] private List<AttachmentPoint> occupiedScenePoints = new List<AttachmentPoint>();

    public void ClearRegistered()
    {
        occupiedScenePoints.Clear();
    }

    public void RegisterOccupiedScenePoint(AttachmentPoint ap)
    {
        if (ap == null) return;
        if (!occupiedScenePoints.Contains(ap))
            occupiedScenePoints.Add(ap);
    }

    public void ReleaseAll()
    {
        for (int i = 0; i < occupiedScenePoints.Count; i++)
        {
            var ap = occupiedScenePoints[i];
            if (ap == null) continue;

            ap.isOccupied = false;
            ap.occupant = null;
            ap.pairedWith = null;
        }

        occupiedScenePoints.Clear();
    }

    void OnDestroy()
    {
        ReleaseAll();
    }
}