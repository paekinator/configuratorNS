using UnityEngine;

/// <summary>
/// Makes sure the camera carries the components the controls card drives:
/// the CAD controller, and the manager that switches between it and the
/// walkthrough controller.
///
/// It used to build the interface as well — the gear button, the controls
/// card, its two scheme buttons and the legend — for scenes saved before
/// those existed, shuffling the top bar's right-hand items 48px aside to make
/// room. None of it could run: the first thing it did was look for a baked
/// Btn_Settings and return if it found one, and the builder has been baking
/// that button (into the utility rail) for a long time. The panel it would
/// have built is baked complete, component and all seven references included.
///
/// This still ensures the camera components, which is not interface work and
/// is worth keeping: the builder wires them when the UI is rebuilt, but a
/// camera swapped out afterwards would otherwise leave the controls card
/// pointing at nothing.
///
/// (The name is now broader than what it does. Left alone deliberately —
/// renames belong in their own commit, not folded into a behaviour change.)
/// </summary>
public static class ControlSettingsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        EnsureCameraComponents();
    }

    static void EnsureCameraComponents()
    {
        var fly = Object.FindFirstObjectByType<FlyCameraController>(FindObjectsInactive.Include);
        GameObject camGo = fly != null ? fly.gameObject
            : Camera.main != null ? Camera.main.gameObject : null;
        if (camGo == null)
            return;

        var cad = camGo.GetComponent<CadCameraController>();
        if (cad == null)
        {
            cad = camGo.AddComponent<CadCameraController>();
            cad.enabled = false;
        }

        if (camGo.GetComponent<CameraControlManager>() == null)
        {
            var manager = camGo.AddComponent<CameraControlManager>();
            manager.walkthroughController = fly;
            manager.cadController = cad;
        }
    }
}
