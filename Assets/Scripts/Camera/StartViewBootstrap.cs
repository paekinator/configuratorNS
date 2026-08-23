using UnityEngine;

/// <summary>
/// Puts the starting camera at human eye height, close to the starter grid,
/// instead of the aerial view baked into the scene (15 m up, 20 m back).
/// Seeing the first frames from standing height (1.75 m) at furniture
/// distance makes their real size readable immediately.
///
/// Also flattens the Main Camera's leftover local offset under the rig
/// (5 up, 10 back, extra tilt): both camera controllers move the rig as if
/// it were the eye, so any child offset skews their orbit and look math.
/// </summary>
public static class StartViewBootstrap
{
    // Standing at the near edge of the empty starter patch (880 mm half-span)
    // at eye height, looking down onto the work area. This frames the whole
    // patch: near edge at the bottom of the screen, far edge just above
    // center, no sky — the reference framing picked by the user.
    // NOTE: this world is NOT 1 unit = 1 m — the prefabs are modelled at
    // 1 unit = 100 mm, so all real-world sizes go through NeospaceUnits.
    const float EyeHeightMm = 1750f;
    const float StandBackMm = 1800f;
    const float PitchDegrees = 35f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        var fly = Object.FindFirstObjectByType<FlyCameraController>();
        Transform rig = fly != null ? fly.transform
            : Camera.main != null ? Camera.main.transform : null;
        if (rig == null)
            return;

        Camera cam = rig.GetComponentInChildren<Camera>();
        if (cam != null && cam.transform != rig)
        {
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
        }

        var eye = new Vector3(0f, NeospaceUnits.Mm(EyeHeightMm), -NeospaceUnits.Mm(StandBackMm));
        rig.SetPositionAndRotation(eye, Quaternion.Euler(PitchDegrees, 0f, 0f));

        // Both controllers cached yaw/pitch from the old pose during their
        // own initialization; re-sync so the first look input doesn't snap
        // the view back.
        if (fly != null)
            fly.SyncPoseFromTransform();

        var cad = rig.GetComponent<CadCameraController>();
        if (cad != null && cad.enabled)
        {
            cad.enabled = false;   // OnEnable re-reads the transform
            cad.enabled = true;
        }
    }
}
