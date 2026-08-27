using UnityEngine;
using UnityEngine.EventSystems;

public class ConfiguratorCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Tooltip("Optional: what counts as 'focusable'. If empty, raycast hits everything.")]
    public LayerMask focusMask = ~0;

    [Tooltip("Optional: what counts as 'ground plane' for focus when nothing is hit.")]
    public LayerMask groundMask = ~0;

    [Header("Orbit")]
    public float orbitSensitivity = 3.5f;
    public float orbitDamp = 12f;
    public bool invertY = false;

    [Header("Pan")]
    public float panSensitivity = 0.012f;   // scaled by distance
    public float panDamp = 14f;

    [Header("Zoom")]
    public float zoomSensitivity = 4.0f;
    public float zoomDamp = 14f;
    public float minDistance = 1.0f;
    public float maxDistance = 80.0f;
    public bool zoomToMouse = true;

    [Header("Focus")]
    public float focusDamp = 14f;
    public float focusPadding = 1.2f; // multiplier for bounds distance
    public KeyCode focusKey = KeyCode.F;

    [Header("Input")]
    public int orbitMouseButton = 1; // RMB
    public int panMouseButton = 2;   // MMB
    public bool shiftRmbPanFallback = true;

    // Internal state
    private Transform _pivot;
    private float _distance;

    private Vector2 _targetOrbit;     // yaw (x), pitch (y)
    private Vector2 _currentOrbit;

    private Vector3 _targetPivotPos;
    private Vector3 _pivotVel;

    private float _targetDistance;
    private float _distanceVel;

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        _pivot = transform;
        _targetPivotPos = _pivot.position;

        // Initialize orbit from current rig rotation
        Vector3 e = _pivot.rotation.eulerAngles;
        _currentOrbit = new Vector2(e.y, NormalizePitch(e.x));
        _targetOrbit = _currentOrbit;

        // Initialize distance from camera local position (assuming camera is on -Z)
        if (cam != null)
        {
            _distance = cam.transform.localPosition.magnitude;
            _targetDistance = _distance;
        }
        else
        {
            _distance = 10f;
            _targetDistance = 10f;
        }
    }

    void Update()
    {
        if (cam == null) return;

        // If mouse over UI, ignore camera movement (prevents fights with buttons/scroll views)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // Still allow focus key if you want, but usually better to block it too.
            // We'll allow focus if pressed and mouse isn't over UI.
        }

        HandleFocus();
        HandleOrbitPanZoom();
        ApplySmoothing();
        ApplyCameraLocalPos();
    }

    void HandleFocus()
    {
        if (!Input.GetKeyDown(focusKey)) return;

        // Don't focus while hovering over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Raycast for focus target
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, focusMask, QueryTriggerInteraction.Ignore))
        {
            // Try focus bounds if there's a renderer group
            Bounds b;
            if (TryGetBounds(hit.collider.transform, out b))
            {
                FocusBounds(b);
                return;
            }

            // Fallback: focus to hit point
            _targetPivotPos = hit.point;
            return;
        }

        // If nothing hit, focus on ground point under mouse
        if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask, QueryTriggerInteraction.Ignore))
        {
            _targetPivotPos = groundHit.point;
        }
    }

    void HandleOrbitPanZoom()
    {
        // If pointer over UI, block orbit/pan/zoom input
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        bool orbitHeld = Input.GetMouseButton(orbitMouseButton);
        bool panHeld = Input.GetMouseButton(panMouseButton);

        // Optional fallback: Shift+RMB = pan (useful for trackpads)
        if (shiftRmbPanFallback && Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButton(orbitMouseButton))
        {
            orbitHeld = false;
            panHeld = true;
        }

        // Orbit (RMB drag)
        if (orbitHeld)
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            float signY = invertY ? 1f : -1f;

            _targetOrbit.x += mx * orbitSensitivity;
            _targetOrbit.y += my * orbitSensitivity * signY;

            _targetOrbit.y = Mathf.Clamp(_targetOrbit.y, -10f, 85f);
        }

        // Pan (MMB drag)
        if (panHeld)
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            // Scale pan by distance to pivot so it feels consistent
            float scale = Mathf.Max(_distance, 0.01f) * panSensitivity;

            // Right/Up in world from camera orientation
            Vector3 right = cam.transform.right;
            Vector3 up = cam.transform.up;

            Vector3 delta = (-right * mx - up * my) * scale;
            _targetPivotPos += delta;
        }

        // Zoom (scroll)
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float zoomAmount = scroll * zoomSensitivity;

            // Zoom to mouse: shift pivot toward the point under cursor when zooming in
            if (zoomToMouse)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, focusMask, QueryTriggerInteraction.Ignore))
                {
                    // Move pivot slightly toward hit point based on zoom direction
                    Vector3 toHit = hit.point - _targetPivotPos;
                    _targetPivotPos += toHit * (zoomAmount * 0.02f);
                }
            }

            _targetDistance = Mathf.Clamp(_targetDistance - zoomAmount, minDistance, maxDistance);
        }
    }

    void ApplySmoothing()
    {
        // Smooth pivot position
        _pivot.position = Vector3.SmoothDamp(_pivot.position, _targetPivotPos, ref _pivotVel, 1f / focusDamp);

        // Smooth orbit rotation
        _currentOrbit.x = Mathf.LerpAngle(_currentOrbit.x, _targetOrbit.x, 1f - Mathf.Exp(-orbitDamp * Time.deltaTime));
        _currentOrbit.y = Mathf.Lerp(_currentOrbit.y, _targetOrbit.y, 1f - Mathf.Exp(-orbitDamp * Time.deltaTime));
        _pivot.rotation = Quaternion.Euler(_currentOrbit.y, _currentOrbit.x, 0f);

        // Smooth zoom distance
        _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVel, 1f / zoomDamp);
        _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
    }

    void ApplyCameraLocalPos()
    {
        // Keep camera on local -Z axis at distance
        Vector3 localDir = new Vector3(0f, 0f, -1f);
        cam.transform.localPosition = localDir * _distance;
        cam.transform.localRotation = Quaternion.identity;
    }

    void FocusBounds(Bounds b)
    {
        _targetPivotPos = b.center;

        // Calculate distance so bounds fit vertically in view
        float radius = b.extents.magnitude * focusPadding;
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float fitDist = radius / Mathf.Tan(fovRad * 0.5f);

        _targetDistance = Mathf.Clamp(fitDist, minDistance, maxDistance);
    }

    bool TryGetBounds(Transform t, out Bounds bounds)
    {
        var rends = t.GetComponentsInParent<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            // Try children too
            rends = t.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                bounds = new Bounds(t.position, Vector3.one);
                return false;
            }
        }

        bounds = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            bounds.Encapsulate(rends[i].bounds);

        return true;
    }

    static float NormalizePitch(float pitch)
    {
        // Convert 0..360 into -180..180 then clamp later
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }
}