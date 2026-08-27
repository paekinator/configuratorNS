using UnityEngine;

public class PanelInstance : MonoBehaviour
{
    /// <summary>
    /// Bumped whenever a panel appears or disappears, so pollers (styling,
    /// finish fingerprint) can compare one integer instead of scanning the
    /// scene every interval.
    /// </summary>
    public static int Version { get; private set; } = 1;

    public string slotId;
    public int side; // +1 = +normal, -1 = -normal

    // Catalogue board identity ("Panel H{sizeA}xH{sizeB}", sizeA >= sizeB).
    // Zero when the opening does not map to a produced board.
    public int sizeA;
    public int sizeB;

    void OnEnable() { Version++; }
    void OnDisable() { Version++; }
}