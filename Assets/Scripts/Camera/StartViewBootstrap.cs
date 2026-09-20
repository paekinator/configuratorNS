using UnityEngine;

/// <summary>
/// Flattens the Main Camera's leftover local offset under the rig (5 up, 10
/// back, extra tilt): both camera controllers move the rig as if it were the
/// eye, so any child offset skews their orbit and look math.
///
/// It no longer sets the START VIEW. It used to put the camera at standing eye
/// height for every scheme, before the scheme was known — which was right for
/// Walkthrough and wrong for the isometric view, where a camera at eye height
/// is inside the build. CameraControlManager now gives each scheme its own way
/// in, and a second owner of the starting pose here would only fight it.
/// </summary>
public static class StartViewBootstrap
{
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
    }
}
