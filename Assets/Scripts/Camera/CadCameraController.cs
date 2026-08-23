using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Professional CAD-style viewport navigation, modelled on Rhino's perspective
/// viewport with the universal middle-mouse conventions layered in:
///
///   Right-drag            orbit (turntable around the point under the cursor)
///   Shift + right-drag    pan (grab the model, 1:1 with the cursor)
///   Ctrl + right-drag     zoom (drag up = in, down = out)
///   Middle-drag           pan
///   Scroll wheel          zoom toward the cursor
///   Double middle-click   zoom extents
///   F                     zoom extents
///
/// The left mouse button is never touched, so all build/select tools keep
/// working. The cursor is never locked or hidden. Attach to the Camera.
/// </summary>
[DisallowMultipleComponent]
public class CadCameraController : MonoBehaviour
{
    [Header("Picking")]
    [Tooltip("What orbit/zoom target picking can hit. Leave as Everything to include the floor.")]
    public LayerMask pickMask = ~0;

    [Header("Orbit")]
    [Tooltip("Degrees of rotation per unit of mouse movement.")]
    public float orbitSensitivity = 3.2f;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Zoom")]
    [Tooltip("Distance multiplier applied per scroll notch when zooming in (0.9 = 10% closer).")]
    [Range(0.5f, 0.99f)] public float wheelZoomStep = 0.88f;
    [Tooltip("Zoom speed for Ctrl + right-drag.")]
    public float dragZoomSensitivity = 0.35f;
    public float minDistance = 0.25f;
    public float maxDistance = 200f;

    [Header("Zoom extents")]
    public KeyCode fitKey = KeyCode.F;
    [Tooltip("Extra margin around the model when fitting the view.")]
    public float fitPadding = 1.25f;
    [Tooltip("Max delay between middle clicks to count as a double-click.")]
    public float doubleClickTime = 0.32f;

    enum DragMode { None, Orbit, Pan, Zoom }

    Camera _cam;
    DragMode _mode = DragMode.None;

    // Turntable state. Position is NOT derived from these; orbit rotates the
    // camera around the pivot by the delta, so the pivot may sit off-axis
    // (CAD apps orbit about the geometry under the cursor, not screen center).
    float _yaw;
    float _pitch;
    Vector3 _pivot;

    // Pan drag: world point grabbed at drag start + the fixed plane it lives on.
    Vector3 _grabPoint;
    Plane _grabPlane;

    float _lastMiddleClickTime = -10f;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
            _cam = Camera.main;

        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizePitch(e.x);

        // Seed the pivot with whatever is in front of the camera.
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 500f, pickMask,
                QueryTriggerInteraction.Ignore))
            _pivot = hit.point;
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(new Ray(transform.position, transform.forward), out float t))
            _pivot = transform.position + transform.forward * t;
        else
            _pivot = transform.position + transform.forward * 10f;

        // In case the walkthrough scheme left the cursor locked.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (_cam == null)
            return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        HandleDragStartEnd(overUI);
        HandleActiveDrag();

        if (!overUI)
        {
            HandleWheelZoom();
            HandleZoomExtents();
        }
        else if (Input.GetKeyDown(fitKey))
        {
            // F works even with the pointer over UI; it's a keyboard command.
            ZoomExtents();
        }
    }

    // ------------------------------------------------------------------
    // Drag lifecycle
    // ------------------------------------------------------------------

    void HandleDragStartEnd(bool overUI)
    {
        // Drags never START over UI, but once started they continue across it.
        if (!overUI && Input.GetMouseButtonDown(1))
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                _mode = DragMode.Zoom;
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                BeginPan();
            else
                BeginOrbit();
        }

        if (!overUI && Input.GetMouseButtonDown(2))
        {
            if (Time.unscaledTime - _lastMiddleClickTime <= doubleClickTime)
            {
                _lastMiddleClickTime = -10f;
                _mode = DragMode.None;
                ZoomExtents();
            }
            else
            {
                _lastMiddleClickTime = Time.unscaledTime;
                BeginPan();
            }
        }

        bool rmbHeld = Input.GetMouseButton(1);
        bool mmbHeld = Input.GetMouseButton(2);
        if (_mode != DragMode.None && !rmbHeld && !mmbHeld)
            _mode = DragMode.None;

        // Anchor the pointer-delta tracking at the start of any drag.
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            _lastPointer = Input.mousePosition;
    }

    Vector2 _lastPointer;

    void BeginOrbit()
    {
        _mode = DragMode.Orbit;

        // Orbit about the geometry under the cursor (SolidWorks/Fusion feel).
        // Fall back to the ground point, then to the previous pivot.
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, pickMask, QueryTriggerInteraction.Ignore))
            _pivot = hit.point;
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t) && t > 0f)
            _pivot = ray.GetPoint(t);
    }

    void BeginPan()
    {
        _mode = DragMode.Pan;

        // Grab the exact world point under the cursor. Prefer real geometry so
        // the grabbed point tracks the cursor 1:1; otherwise use a view-parallel
        // plane through the pivot.
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, pickMask, QueryTriggerInteraction.Ignore))
            _grabPoint = hit.point;
        else
        {
            var viewPlane = new Plane(-transform.forward, _pivot);
            _grabPoint = viewPlane.Raycast(ray, out float t) ? ray.GetPoint(t) : _pivot;
        }

        _grabPlane = new Plane(-transform.forward, _grabPoint);
    }

    void HandleActiveDrag()
    {
        // Pointer delta in canvas pixels rather than the "Mouse X/Y" axes:
        // the axes receive raw browser deltas on WebGL (scaled up again on
        // high-DPI screens), which made looking around several times faster
        // in the browser than in the editor. mousePosition uses the same
        // units on every platform; the 0.1 factor matches the Input Manager
        // axis sensitivity, so the desktop/editor feel is unchanged.
        Vector2 pointer = Input.mousePosition;
        Vector2 delta = (pointer - _lastPointer) * 0.1f;
        _lastPointer = pointer;

        switch (_mode)
        {
            case DragMode.Orbit:
                OrbitBy(delta.x * orbitSensitivity,
                        -delta.y * orbitSensitivity);
                break;

            case DragMode.Pan:
                PanToCursor();
                break;

            case DragMode.Zoom:
                if (Mathf.Abs(delta.y) > 0.0001f)
                    ZoomAbout(_pivot, Mathf.Exp(-delta.y * dragZoomSensitivity * 0.1f));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Navigation primitives
    // ------------------------------------------------------------------

    /// <summary>Turntable orbit around the pivot: yaw about world up, pitch clamped.</summary>
    void OrbitBy(float dYaw, float dPitch)
    {
        float newPitch = Mathf.Clamp(_pitch + dPitch, minPitch, maxPitch);
        Quaternion oldRot = Quaternion.Euler(_pitch, _yaw, 0f);

        _yaw += dYaw;
        _pitch = newPitch;
        Quaternion newRot = Quaternion.Euler(_pitch, _yaw, 0f);

        // Rotate the camera's offset from the pivot by exactly the rotation
        // delta, so the picked point stays fixed on screen while we orbit.
        Vector3 offset = transform.position - _pivot;
        transform.position = _pivot + newRot * (Quaternion.Inverse(oldRot) * offset);
        transform.rotation = newRot;
    }

    /// <summary>Move the camera so the grabbed world point stays under the cursor.</summary>
    void PanToCursor()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!_grabPlane.Raycast(ray, out float t))
            return;

        Vector3 delta = _grabPoint - ray.GetPoint(t);
        transform.position += delta;
        _pivot += delta;
    }

    /// <summary>
    /// Scale the camera (and pivot) about a world point. A homothety keeps the
    /// target exactly under the cursor while zooming, like every CAD viewport.
    /// </summary>
    void ZoomAbout(Vector3 target, float factor)
    {
        float pivotDistance = Vector3.Distance(transform.position, _pivot);
        float clamped = Mathf.Clamp(pivotDistance * factor, minDistance, maxDistance);
        factor = pivotDistance > 0.0001f ? clamped / pivotDistance : 1f;

        transform.position = target + (transform.position - target) * factor;
        _pivot = target + (_pivot - target) * factor;
    }

    void HandleWheelZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        // Zoom toward whatever is under the cursor; fall back to the ground
        // plane, then the orbit pivot, so empty space still zooms sensibly.
        Vector3 target = _pivot;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, pickMask, QueryTriggerInteraction.Ignore))
            target = hit.point;
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t) && t > 0f)
            target = ray.GetPoint(t);

        ZoomAbout(target, Mathf.Pow(wheelZoomStep, scroll));
    }

    // ------------------------------------------------------------------
    // Zoom extents
    // ------------------------------------------------------------------

    void HandleZoomExtents()
    {
        if (Input.GetKeyDown(fitKey))
            ZoomExtents();
    }

    /// <summary>Frame everything that has been built (ignores the floor grid).</summary>
    public void ZoomExtents()
    {
        if (!TryGetSceneBounds(out Bounds b))
        {
            // Nothing built yet: go to a comfortable home view of the origin.
            b = new Bounds(Vector3.up * 1f, Vector3.one * 4f);
        }

        _pivot = b.center;

        float radius = Mathf.Max(b.extents.magnitude, 0.5f) * fitPadding;
        float fov = _cam.fieldOfView * Mathf.Deg2Rad;
        float distance = Mathf.Clamp(radius / Mathf.Sin(fov * 0.5f), minDistance, maxDistance);

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.rotation = rot;
        transform.position = _pivot - rot * Vector3.forward * distance;
    }

    static bool TryGetSceneBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;
            // Skip UI, the floor grid, and helper lines (preview guides etc.).
            if (r is SpriteRenderer || r is LineRenderer || r.GetComponent<RectTransform>() != null)
                continue;
            if (r.transform.root.name.Contains("GridFloor") || r.name.Contains("GridFloor"))
                continue;
            // Ignore absurdly large renderers (environment, skydome-style meshes).
            if (r.bounds.size.magnitude > 500f)
                continue;

            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    static float NormalizePitch(float pitch)
    {
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }
}
