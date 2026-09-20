using System;
using UnityEngine;

public enum CameraControlScheme
{
    /// <summary>WASD + right-mouse look, first person, PERSPECTIVE.</summary>
    Walkthrough = 0,

    /// <summary>
    /// Orbit / pan / zoom on the mouse, as an ORTHOGRAPHIC true-isometric view.
    /// The enum value keeps its historical name so saved preferences still
    /// resolve.
    /// </summary>
    Cad = 1,
}

/// <summary>
/// Switches between the walkthrough and isometric camera schemes and remembers
/// the choice across sessions. Lives on the camera rig next to the two
/// controllers; exactly one of them is enabled at a time.
///
/// EACH SCHEME OWNS ITS PROJECTION AND ITS WAY IN. Walkthrough is first person,
/// which only exists in perspective; the isometric scheme is orthographic.
/// Switching therefore changes the projection, and has to put the camera
/// somewhere that makes sense for the new one — an orthographic camera standing
/// at eye height inside the build, or a first-person camera left parked out
/// where the isometric view keeps it, would both be nonsense.
///
/// Both entry poses aim at whatever the previous view was looking at on the
/// ground, so switching is a change of how you look, not of what you look at.
///
/// These poses used to be set by StartViewBootstrap for every scheme alike,
/// before the scheme was even known — a second owner of the start view that
/// could only ever be right for one of them.
/// </summary>
[DisallowMultipleComponent]
public class CameraControlManager : MonoBehaviour
{
    const string PrefKey = "Neospace.CameraControlScheme";

    /// <summary>
    /// For someone who has never chosen: the isometric view. A saved choice
    /// always wins over this.
    /// </summary>
    const CameraControlScheme DefaultScheme = CameraControlScheme.Cad;

    // Walkthrough's way in: standing height, a step back from what was being
    // looked at, looking down onto it. The same framing StartViewBootstrap
    // used to apply to everything.
    // NOTE: this world is NOT 1 unit = 1 m — 1 unit = 100 mm, via NeospaceUnits.
    const float EyeHeightMm = 1750f;
    const float StandBackMm = 1800f;
    const float WalkthroughPitch = 35f;

    public FlyCameraController walkthroughController;
    public CadCameraController cadController;

    public CameraControlScheme Scheme { get; private set; } = DefaultScheme;

    public static event Action<CameraControlScheme> OnSchemeChanged;

    bool _applied;

    void Awake()
    {
        if (walkthroughController == null)
            walkthroughController = GetComponent<FlyCameraController>();
        if (walkthroughController == null)
            walkthroughController = FindFirstObjectByType<FlyCameraController>();

        if (cadController == null)
            cadController = GetComponent<CadCameraController>();
        if (cadController == null)
            cadController = FindFirstObjectByType<CadCameraController>();
    }

    void Start()
    {
        Apply((CameraControlScheme)PlayerPrefs.GetInt(PrefKey, (int)DefaultScheme), save: false);
    }

    public void SetWalkthrough() => Apply(CameraControlScheme.Walkthrough, save: true);

    public void SetCad() => Apply(CameraControlScheme.Cad, save: true);

    void Apply(CameraControlScheme scheme, bool save)
    {
        // Choosing the scheme already in use must not throw the view back to
        // its entry pose — that would punish clicking the button that is lit.
        bool changing = !_applied || scheme != Scheme;

        Camera cam = GetComponentInChildren<Camera>();
        if (cam == null)
            cam = Camera.main;

        // What the current view is looking at, captured BEFORE anything moves.
        // The very first application has no previous view, so it looks at the
        // world centre.
        Vector3 focus = Vector3.zero;
        float yaw = 0f;
        if (_applied && cam != null)
        {
            CameraMath.GroundFocus(cam, 0f, out focus);
            yaw = transform.eulerAngles.y;
        }

        Scheme = scheme;
        _applied = true;

        if (walkthroughController != null)
            walkthroughController.enabled = scheme == CameraControlScheme.Walkthrough;
        if (cadController != null)
            cadController.enabled = scheme == CameraControlScheme.Cad;

        if (changing && cam != null)
        {
            if (scheme == CameraControlScheme.Cad)
                EnterIsometric(focus, yaw);
            else
                EnterWalkthrough(cam, focus, yaw);
        }

        // The walkthrough scheme locks the cursor while looking around; make
        // sure a mid-look switch never leaves it locked.
        if (scheme == CameraControlScheme.Cad)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (save)
        {
            PlayerPrefs.SetInt(PrefKey, (int)scheme);
            PlayerPrefs.Save();
        }

        OnSchemeChanged?.Invoke(scheme);
    }

    void EnterIsometric(Vector3 focus, float previousYaw)
    {
        if (cadController == null)
            return;

        // The isometric corner nearest the way the previous view was facing,
        // so switching over does not spin the room round. The first view has
        // no previous direction and gets the controller's own corner.
        float yaw = _firstIsometric
            ? cadController.isometricYaw
            : NearestIsometricYaw(previousYaw);
        _firstIsometric = false;

        cadController.ShowPreset(yaw, animate: false);
    }

    bool _firstIsometric = true;

    void EnterWalkthrough(Camera cam, Vector3 focus, float yaw)
    {
        cam.orthographic = false;

        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        Vector3 eye = focus
                      + facing * new Vector3(0f, 0f, -NeospaceUnits.Mm(StandBackMm))
                      + Vector3.up * NeospaceUnits.Mm(EyeHeightMm);
        transform.SetPositionAndRotation(eye, Quaternion.Euler(WalkthroughPitch, yaw, 0f));

        // The fly controller caches its look angles; without this the first
        // mouse movement would snap the view back to where it was.
        if (walkthroughController != null)
            walkthroughController.SyncPoseFromTransform();
    }

    /// <summary>The nearest of 45°, 135°, 225°, 315° to a heading.</summary>
    static float NearestIsometricYaw(float yaw)
    {
        return Mathf.Round((yaw - 45f) / 90f) * 90f + 45f;
    }
}
