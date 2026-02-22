using UnityEngine;

public class PanelSlotHandle : MonoBehaviour
{
    [Header("Slot Identity")]
    public string slotId;

    [Header("Slot Geometry")]
    public Vector3 corner0;
    public Vector3 corner1;
    public Vector3 corner2;
    public Vector3 corner3;
    public Vector3 normal;          // slot plane normal (unit)
    public Vector3 center;
    public Vector2 sizeXY;          // width/height in slot local space

    [Header("Runtime Objects")]
    public Transform slotTrigger;   // trigger collider (hover)
    public Transform blocker;       // non-trigger collider (blocks frames)

    [Header("Occupancy")]
    public GameObject panelPlus;    // +normal side
    public GameObject panelMinus;   // -normal side

    public bool HasAnyPanel() => panelPlus != null || panelMinus != null;

    [HideInInspector] public Vector3 upAxis = Vector3.up;
}