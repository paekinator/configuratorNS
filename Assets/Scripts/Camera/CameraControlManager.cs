using System;
using UnityEngine;

public enum CameraControlScheme
{
    /// <summary>WASD + right-mouse look (the original game-like controls).</summary>
    Walkthrough = 0,

    /// <summary>Rhino-style CAD navigation: orbit / pan / zoom on the mouse.</summary>
    Cad = 1,
}

/// <summary>
/// Switches between the walkthrough and CAD camera control schemes and
/// remembers the choice across sessions. Lives on the camera next to the
/// two controllers; exactly one of them is enabled at a time.
/// </summary>
[DisallowMultipleComponent]
public class CameraControlManager : MonoBehaviour
{
    const string PrefKey = "Neospace.CameraControlScheme";

    public FlyCameraController walkthroughController;
    public CadCameraController cadController;

    public CameraControlScheme Scheme { get; private set; } = CameraControlScheme.Walkthrough;

    public static event Action<CameraControlScheme> OnSchemeChanged;

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
        Apply((CameraControlScheme)PlayerPrefs.GetInt(PrefKey, (int)CameraControlScheme.Walkthrough), save: false);
    }

    public void SetWalkthrough() => Apply(CameraControlScheme.Walkthrough, save: true);

    public void SetCad() => Apply(CameraControlScheme.Cad, save: true);

    void Apply(CameraControlScheme scheme, bool save)
    {
        Scheme = scheme;

        if (walkthroughController != null)
            walkthroughController.enabled = scheme == CameraControlScheme.Walkthrough;
        if (cadController != null)
            cadController.enabled = scheme == CameraControlScheme.Cad;

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
}
