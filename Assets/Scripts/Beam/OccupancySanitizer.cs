using UnityEngine;

public class OccupancySanitizer : MonoBehaviour
{
    [Tooltip("Unused (legacy). Sanitizing is change-driven: stale occupancy can only appear when an occupant was destroyed, which bumps AttachmentPoint.StructureVersion.")]
    public bool runEveryFrame = true;

    [Tooltip("Unused (see Run Every Frame).")]
    public int runEveryNFrames = 10;

    int _seenVersion;

    void LateUpdate()
    {
        // A stale occupant reference can only appear when something was
        // destroyed — and every destroyed part unregisters its attachment
        // points, bumping the structure version. Idle frames cost one compare.
        if (_seenVersion == AttachmentPoint.StructureVersion)
            return;
        _seenVersion = AttachmentPoint.StructureVersion;

        var aps = AttachmentPoint.Live;
        for (int i = 0; i < aps.Count; i++)
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
