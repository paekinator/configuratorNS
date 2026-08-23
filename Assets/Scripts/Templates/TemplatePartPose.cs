using UnityEngine;

/// <summary>One part the template planner wants to spawn.</summary>
public struct TemplatePartPose
{
    public string PartId;
    public Vector3 Position;
    public Quaternion Rotation;
    public string Label;

    public TemplatePartPose(string partId, Vector3 position, Quaternion rotation, string label = null)
    {
        PartId = partId;
        Position = position;
        Rotation = rotation;
        Label = label;
    }
}
