using UnityEngine;

public class OccupancySanitizer : MonoBehaviour
{
    [Tooltip("If true, runs every frame. If false, runs every N frames.")]
    public bool runEveryFrame = true;

    [Tooltip("If not running every frame, how often to run (in frames).")]
    public int runEveryNFrames = 10;

    int _frame;

    void LateUpdate()
    {
        if (!runEveryFrame)
        {
            _frame++;
            if (_frame % Mathf.Max(1, runEveryNFrames) != 0)
                return;
        }

        var aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < aps.Length; i++)
        {
            var ap = aps[i];
            if (ap == null) continue;

            // Unity destroyed objects compare == null even if the reference exists.
            if (ap.isOccupied && ap.occupant == null)
            {
                ap.isOccupied = false;
                ap.occupant = null;
            }
        }
    }
}