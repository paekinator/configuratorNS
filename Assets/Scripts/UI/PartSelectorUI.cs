using UnityEngine;

public class PartSelectorUI : MonoBehaviour
{
    public BuildController buildController;  // we'll create this script next

    // This will be called from UI buttons
    public void SelectPart(string partId)
    {
        if (buildController != null)
        {
            buildController.SetCurrentPart(partId);
        }
        else
        {
            Debug.LogWarning("PartSelectorUI has no BuildController assigned.");
        }
    }
}