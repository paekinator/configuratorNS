using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Professional CAD-style viewport navigation, modelled on Rhino's perspective
/// viewport with the universal middle-mouse conventions layered in:
///
///   Right-drag            orbit (turntable around the view target)
///   Shift + right-drag    pan (grab the model, 1:1 with the cursor)
///   Ctrl + right-drag     zoom (drag up = in, down = out)
///   Middle-drag           pan
///   Scroll wheel          zoom toward the cursor
///   Double middle-click   zoom extents
///   F                     zoom extents
///
/// The orbit target is the point in front of the camera, not the pixel under
/// the cursor. Re-picking under the mouse made a far floor/sky hit the
/// centre of rotation, so a small drag swung the camera around a distant
/// point and the view orientation fell apart.
///
/// The left mouse button is never touched, so all build/select tools keep
/// working. The cursor is never locked or hidden. Attach to the Camera.
/// </summary>
[DisallowMultipleComponent]
public class CadCameraController : MonoBehaviour
{
    [Header("Picking")]
    [Tooltip("What zoom/pan grabbing can hit. Leave as Everything to include the floor.")]
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

    // Hits farther than this many current-target-distances are treated as
    // empty space. A grazing ray along the floor can otherwise land hundreds
    // of metres away and explode pan/zoom.
    const float MaxPickDistanceFactor = 3f;
    const float FallbackDistance = 10f;

    enum DragMode { None, Orbit, Pan, Zoom }

    Camera _cam;
    DragMode _mode = DragMode.None;

    // Turntable: the camera always looks at _pivot from _distance.
    float _yaw;
    float _pitch;
    Vector3 _pivot;
    float _distance;

    // Pan drag: world point grabbed at drag start + the fixed plane it lives on.
    Vector3 _grabPoint;
    Plane _grabPlane;

    float _lastMiddleClickTime = -10f;
    Vector2 _lastPointer;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
            _cam = Camera.main;

        SyncPoseFromTransform();

        // In case the walkthrough scheme left the cursor locked.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Re-read yaw/pitch from the transform and place the orbit target on
    /// the view axis. Call after an outside script moves the rig.
    /// </summary>
    public void SyncPoseFromTransform()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = Mathf.Clamp(NormalizePitch(e.x), minPitch, maxPitch);
        SeedTargetAlongView();
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

    void BeginOrbit()
    {
        _mode = DragMode.Orbit;
        // Keep the existing target. Only snap it onto the view axis so a
        // leftover off-centre pivot cannot become a new centre of rotation.
        SeatTargetOnViewAxis();
    }

    void BeginPan()
    {
        _mode = DragMode.Pan;

        // Grab nearby geometry so the model tracks the cursor 1:1. Far floor
        // or empty sky uses a view-parallel plane through the orbit target
        // instead of a horizon intersection that would throw the camera.
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        _grabPoint = TryPickNearby(ray, out Vector3 hit)
            ? hit
            : PointOnViewPlane(ray, _pivot);
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
                    ZoomAlongView(Mathf.Exp(-delta.y * dragZoomSensitivity * 0.1f));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Navigation primitives
    // ------------------------------------------------------------------

    /// <summary>Turntable orbit around the view target: yaw about world up, pitch clamped.</summary>
    void OrbitBy(float dYaw, float dPitch)
    {
        _yaw += dYaw;
        _pitch = Mathf.Clamp(_pitch + dPitch, minPitch, maxPitch);
        ApplyPose();
    }

    void ApplyPose()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.rotation = rot;
        transform.position = _pivot - rot * Vector3.forward * _distance;
    }

    /// <summary>Move the camera so the grabbed world point stays under the cursor.</summary>
    void PanToCursor()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!_grabPlane.Raycast(ray, out float t) || t <= 0f)
            return;

        Vector3 delta = _grabPoint - ray.GetPoint(t);
        transform.position += delta;
        _pivot += delta;
    }

    void ZoomAlongView(float factor)
    {
        _distance = Mathf.Clamp(_distance * factor, minDistance, maxDistance);
        ApplyPose();
    }

    /// <summary>
    /// Scale the camera about a world point. A homothety keeps that point
    /// under the cursor while zooming. The orbit target is then re-seated on
    /// the view axis so the next orbit is still a stable turntable.
    /// </summary>
    void ZoomAbout(Vector3 target, float factor)
    {
        float newDistance = Mathf.Clamp(_distance * factor, minDistance, maxDistance);
        factor = _distance > 0.0001f ? newDistance / _distance : 1f;

        transform.position = target + (transform.position - target) * factor;
        _distance = newDistance;
        SeatTargetOnViewAxis();
    }

    void HandleWheelZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Vector3 target = TryPickNearby(ray, out Vector3 hit)
            ? hit
            : PointOnViewPlane(ray, _pivot);

        ZoomAbout(target, Mathf.Pow(wheelZoomStep, scroll));
    }

    // ------------------------------------------------------------------
    // Target / picking
    // ------------------------------------------------------------------

    void SeedTargetAlongView()
    {
        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        float range = SeedRange();

        if (Physics.Raycast(origin, fwd, out RaycastHit hit, range, pickMask,
                QueryTriggerInteraction.Ignore) && hit.distance >= minDistance)
        {
            _pivot = hit.point;
        }
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(new Ray(origin, fwd), out float t)
                 && t >= minDistance && t <= range)
        {
            _pivot = origin + fwd * t;
        }
        else
        {
            float seed = _distance > minDistance ? _distance : FallbackDistance;
            _pivot = origin + fwd * Mathf.Clamp(seed, minDistance, range);
        }

        _distance = Mathf.Clamp(Vector3.Distance(origin, _pivot), minDistance, maxDistance);
        SeatTargetOnViewAxis();
    }

    float SeedRange()
    {
        if (TryGetSceneBounds(out Bounds b))
        {
            float toScene = Vector3.Distance(transform.position, b.center) + b.extents.magnitude;
            return Mathf.Clamp(toScene, FallbackDistance, maxDistance);
        }

        return Mathf.Min(maxDistance, FallbackDistance * 4f);
    }

    void SeatTargetOnViewAxis()
    {
        float along = Vector3.Dot(_pivot - transform.position, transform.forward);
        _distance = Mathf.Clamp(along > 0.0001f ? along : _distance, minDistance, maxDistance);
        _pivot = transform.position + transform.forward * _distance;
    }

    float PickRange()
    {
        return Mathf.Min(maxDistance, Mathf.Max(_distance * MaxPickDistanceFactor, minDistance * 8f));
    }

    bool TryPickNearby(Ray ray, out Vector3 point)
    {
        point = default;
        if (!Physics.Raycast(ray, out RaycastHit hit, PickRange(), pickMask,
                QueryTriggerInteraction.Ignore))
            return false;
        if (hit.distance < minDistance * 0.5f)
            return false;
        point = hit.point;
        return true;
    }

    Vector3 PointOnViewPlane(Ray ray, Vector3 planePoint)
    {
        var plane = new Plane(-transform.forward, planePoint);
        if (plane.Raycast(ray, out float t) && t > 0f)
            return ray.GetPoint(t);
        return planePoint;
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
        _distance = Mathf.Clamp(radius / Mathf.Sin(fov * 0.5f), minDistance, maxDistance);
        ApplyPose();
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
