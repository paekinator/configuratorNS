using TMPro;
using UnityEngine;

public class UIStatusBar : MonoBehaviour
{
    public BuildController buildController;
    public TextMeshProUGUI statusText;

    void Update()
    {
        if (statusText == null) return;

        if (buildController == null)
        {
            statusText.text = "No BuildController linked.";
            return;
        }

        // If not building, show mode hint
        if (UIInteractionState.CurrentMode != UIInteractionState.Mode.Build || string.IsNullOrEmpty(buildController.currentPartId))
        {
            statusText.text = $"Mode: {UIInteractionState.CurrentMode}  |  Click a part to Build";
            return;
        }

        buildController.GetGhostStatus(out bool hasPose, out bool isValid, out string info);

        if (!hasPose)
        {
            statusText.text = $"Build: {buildController.currentPartId}  |  Move cursor to a valid surface/point";
            return;
        }

        statusText.text = isValid
            ? $"Build: {buildController.currentPartId}  |  {info}"
            : $"Build: {buildController.currentPartId}  |  {info}";
    }
}