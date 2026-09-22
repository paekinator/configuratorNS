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
/// ORTHOGRAPHIC in this scheme — a true isometric view, entered through
/// EnterIsometric. Every control keeps its meaning; what "zoom" does changes.
/// A perspective camera zooms by moving closer, which in orthographic changes
/// nothing on screen, so zoom there changes the VIEW SIZE instead and the
/// camera's distance becomes a private matter (see MinStandoff). Orbit and pan
/// are the same arithmetic in both projections.
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
    [Tooltip("The lowest the camera may look, in degrees above the ground. "
             + "Orbit is otherwise free, but never from underneath the floor. "
             + "A few degrees rather than zero: exactly edge-on, the whole ground "
             + "collapses into a single line across the screen.")]
    public float groundPitchLimit = 5f;

    /// <summary>
    /// The pitch floor actually applied. A separate field rather than a new
    /// default for minPitch, because minPitch is already saved in the scene at
    /// -89 and a changed default would never reach it.
    /// </summary>
    float MinPitch => Mathf.Max(minPitch, groundPitchLimit);

    [Header("Zoom")]
    [Tooltip("Distance multiplier applied per scroll notch when zooming in (0.9 = 10% closer).")]
    [Range(0.5f, 0.99f)] public float wheelZoomStep = 0.88f;
    [Tooltip("Zoom speed for Ctrl + right-drag.")]
    public float dragZoomSensitivity = 0.35f;
    public float minDistance = 0.25f;
    public float maxDistance = 200f;

    [Header("Isometric (orthographic)")]
    // True isometric: the view axis makes equal angles with all three world
    // axes, so a cube's edges come out the same length on screen. That puts
    // the camera 35.264° down (arctan of 1/√2) and 45° round.
    [Tooltip("Degrees below horizontal when entering the isometric view. "
             + "35.264 is true isometric.")]
    public float isometricPitch = 35.264f;
    [Tooltip("Degrees around when entering the isometric view.")]
    public float isometricYaw = 45f;
    [Tooltip("Half the height of the world shown on screen when the view "
             + "starts, in world units (1 = 100 mm). The orthographic zoom.")]
    public float startViewSize = 12f;
    [Tooltip("Closest zoom: half-height of world on screen, world units.")]
    public float minViewSize = 1f;
    [Tooltip("Absolute furthest zoom: half-height of world on screen, world "
             + "units. The practical limit is usually tighter — see zoomOutPadding.")]
    public float maxViewSize = 150f;
    [Tooltip("Room around the ground frame at the furthest zoom. The frame is the "
             + "empty 88 mm grid before anything is placed, and the built-zone "
             + "outline after. At 1 the frame fits from its WORST angle, which "
             + "already leaves space around it at the isometric one.")]
    public float zoomOutPadding = 1f;
    [Tooltip("How quickly the view settles back inside the zoom limit when the "
             + "zone shrinks under it — the last part removed (1/s). Orbiting "
             + "never moves the limit.")]
    public float zoomLimitSettle = 8f;

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

    // --- Orthographic standoff ---------------------------------------
    //
    // In an orthographic view the camera's distance changes nothing about how
    // big anything looks — zoom is the view SIZE. The distance still matters
    // for two things that pull in opposite directions:
    //
    //   · too close, and the near clip plane slices through the build on the
    //     camera's side;
    //   · too far, and the shadows vanish, because URP measures its shadow
    //     distance (50 units in both pipeline assets) from the CAMERA, not from
    //     what it is looking at. A camera parked a comfortable 150 units back
    //     would have lost every shadow the ground catches.
    //
    // So it sits as close as it safely can: just outside the build, re-checked
    // twice a second as the build grows. Moving it is invisible — that is
    // exactly the property of an orthographic camera being relied on.
    const float MinStandoff = 25f;
    const float StandoffMargin = 5f;
    const float MaxStandoff = 900f;
    const float StandoffPollInterval = 0.5f;
    float _nextStandoffPoll;

    bool Orthographic => _cam != null && _cam.orthographic;

    /// <summary>
    /// The largest distance the camera may stand back. Perspective keeps the
    /// user's maxDistance, which really is a zoom limit there; orthographic
    /// needs room to clear a large build, and its zoom is limited elsewhere.
    /// </summary>
    float DistanceCap => Orthographic ? MaxStandoff : maxDistance;

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
    float _viewCenterY = .5f;
    BuildController _build;
    Bounds _builtBounds;
    bool _hasBuiltBounds;
    float _nextBoundsPoll;
    public Vector3 ViewTarget => _pivot;
    public float ViewYaw => _yaw;
    public float AdaptiveMaxViewSize => ZoomOutLimit();
    [Header("Preset transitions")]
    [Min(.05f)] public float presetTransitionSeconds = 1f;
    public bool IsTransitioning => _transitioning;
    public float RequestedYaw => _transitioning ? _transitionTo.yaw : _yaw;
    bool _transitioning;
    float _transitionElapsed;
    ViewPose _transitionFrom, _transitionTo;

    struct ViewPose
    {
        public float yaw, pitch, distance, size, centerY, heightFraction;
        public Vector3 pivot;
    }

    ViewPose CapturePose() => new ViewPose
    {
        yaw = _yaw, pitch = _pitch, pivot = _pivot, distance = _distance,
        size = _cam.orthographicSize, centerY = _viewCenterY, heightFraction = _presetHeightFraction
    };

    void RestorePose(ViewPose pose)
    {
        _yaw = pose.yaw; _pitch = pose.pitch; _pivot = pose.pivot;
        _distance = pose.distance; _cam.orthographicSize = pose.size;
        _viewCenterY = pose.centerY; _presetHeightFraction = pose.heightFraction;
        ApplyPose();
    }

    void AdvanceTransition()
    {
        _transitionElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_transitionElapsed / Mathf.Max(.05f, presetTransitionSeconds));
        t = t * t * (3f - 2f * t);
        RestorePose(new ViewPose
        {
            yaw = Mathf.LerpAngle(_transitionFrom.yaw, _transitionTo.yaw, t),
            pitch = Mathf.Lerp(_transitionFrom.pitch, _transitionTo.pitch, t),
            pivot = Vector3.Lerp(_transitionFrom.pivot, _transitionTo.pivot, t),
            distance = Mathf.Lerp(_transitionFrom.distance, _transitionTo.distance, t),
            size = Mathf.Lerp(_transitionFrom.size, _transitionTo.size, t),
            centerY = Mathf.Lerp(_transitionFrom.centerY, _transitionTo.centerY, t),
            heightFraction = Mathf.Lerp(_transitionFrom.heightFraction, _transitionTo.heightFraction, t)
        });
        if (_transitionElapsed >= presetTransitionSeconds)
        {
            RestorePose(_transitionTo);
            _transitioning = false;
        }
    }

    void OnDisable() => _transitioning = false;

    void RefreshBuiltBounds(bool force = false)
    {
        if (!force && Time.unscaledTime < _nextBoundsPoll) return;
        _nextBoundsPoll = Time.unscaledTime + .5f;
        if (_build == null) _build = FindFirstObjectByType<BuildController>();
        _hasBuiltBounds = StructureBounds.TryCompute(_build, out var info);
        if (_hasBuiltBounds) _builtBounds = info.WorldBounds;
    }

    /// <summary>Four standard corners plus a true top-down view; mouse navigation stays enabled.</summary>
    public void ShowPreset(float yaw, bool plan = false, bool animate = true)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        ViewPose from = CapturePose();
        _transitioning = false;
        RefreshBuiltBounds(true);
        _cam.orthographic = true;
        _mode = DragMode.None;
        _yaw = yaw;
        _pitch = plan ? 90f : isometricPitch;
        _pivot = _hasBuiltBounds ? _builtBounds.center : Vector3.zero;
        if (!_hasBuiltBounds && GroundGridController.Active != null &&
            GroundGridController.Active.TryGetViewFrame(out Vector2 center, out _))
            _pivot = new Vector3(center.x, GroundHeight, center.y);
        // Centre the build in the usable scene above the dock, preserving the
        // actual build centre as the orbit target rather than moving it away.
        var canvas = GameObject.Find("UI_Canvas")?.GetComponent<Canvas>();
        var dock = canvas != null ? canvas.transform.Find("Dock") as RectTransform : null;
        float bottom = .04f;
        if (dock != null && dock.gameObject.activeInHierarchy)
        {
            var corners = new Vector3[4]; dock.GetWorldCorners(corners);
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            bottom = Mathf.Clamp01(RectTransformUtility.WorldToScreenPoint(camera, corners[1]).y / Screen.height + .06f);
        }
        _viewCenterY = (Mathf.Min(bottom, .7f) + .90f) * .5f;
        _presetHeightFraction = Mathf.Max(.2f, .90f - bottom);
        _distance = MinStandoff;
        _cam.orthographicSize = ZoomOutLimit();
        ApplyPose();
        RefreshStandoff();
        _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, _distance * 2f + 50f);
        if (animate && Application.isPlaying)
        {
            _transitionFrom = from;
            _transitionTo = CapturePose();
            _transitionElapsed = 0f;
            RestorePose(from);
            _transitioning = true;
        }
    }

    float _presetHeightFraction = 1f;

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
        _transitioning = false;
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = Mathf.Clamp(NormalizePitch(e.x), MinPitch, maxPitch);
        SeedTargetAlongView();
    }

    void Update()
    {
        if (_cam == null)
            return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (_transitioning)
        {
            // Navigation takes over from the currently displayed pose. A new
            // preset click likewise starts its transition from that same pose.
            bool navigation = (!overUI && (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)
                || Mathf.Abs(Input.mouseScrollDelta.y) > .001f)) || Input.GetKeyDown(fitKey);
            if (navigation) _transitioning = false;
            else { AdvanceTransition(); return; }
        }

        if (Orthographic)
        {
            if (Time.unscaledTime >= _nextStandoffPoll)
            {
                _nextStandoffPoll = Time.unscaledTime + StandoffPollInterval;
                RefreshStandoff();
            }

            // The limit moves only when the zone does: it grows when a part
            // widens the built zone and shrinks when the last part is removed.
            // A view left outside a limit that has shrunk under it settles back
            // in rather than snapping. Orbiting never moves it.
            float limit = ZoomOutLimit();
            if (_cam.orthographicSize > limit)
            {
                float settle = 1f - Mathf.Exp(-Mathf.Max(zoomLimitSettle, 0.01f) * Time.unscaledDeltaTime);
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, limit, settle);
                ApplyPose();
            }
        }

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
                {
                    float factor = Mathf.Exp(-delta.y * dragZoomSensitivity * 0.1f);
                    if (Orthographic)
                        SetViewSize(_cam.orthographicSize * factor);
                    else
                        ZoomAlongView(factor);
                }
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
        _pitch = Mathf.Clamp(_pitch + dPitch, MinPitch, maxPitch);
        ApplyPose();
    }

    void ApplyPose()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.rotation = rot;
        transform.position = _pivot - rot * Vector3.forward * _distance;
        if (Orthographic)
            transform.position -= rot * Vector3.up * ((_viewCenterY * 2f - 1f) * _cam.orthographicSize);
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

        if (Orthographic)
            SeatPivotOnGround();
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

        if (Orthographic)
        {
            ZoomViewAtCursor(Mathf.Pow(wheelZoomStep, scroll));
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Vector3 target = TryPickNearby(ray, out Vector3 hit)
            ? hit
            : PointOnViewPlane(ray, _pivot);

        ZoomAbout(target, Mathf.Pow(wheelZoomStep, scroll));
    }

    // ------------------------------------------------------------------
    // Orthographic
    // ------------------------------------------------------------------

    /// <summary>
    /// Put the camera into the isometric view, looking at
    /// <paramref name="focus"/>. Called by CameraControlManager when this
    /// scheme is chosen; it owns the projection switch, this owns the pose.
    /// </summary>
    public void EnterIsometric(Vector3 focus, float yaw, float viewSize)
    {
        _viewCenterY = .5f;
        _presetHeightFraction = 1f;
        if (_cam == null)
            _cam = GetComponent<Camera>() != null ? GetComponent<Camera>() : Camera.main;
        if (_cam == null)
            return;

        _cam.orthographic = true;

        _yaw = yaw;
        _pitch = Mathf.Clamp(isometricPitch, MinPitch, maxPitch);
        _pivot = focus;
        _distance = MinStandoff;
        ApplyPose();

        // After the pose, not before: the zoom limit depends on the angle the
        // ground frame is seen from, so the rotation has to be in place first.
        SetViewSize(viewSize);
        SeatPivotOnGround();
        RefreshStandoff();
    }

    void SetViewSize(float size)
    {
        _cam.orthographicSize = Mathf.Clamp(size, minViewSize, ZoomOutLimit());
        ApplyPose();
    }

    /// <summary>
    /// The furthest this view may zoom out: far enough to see the ground frame
    /// whole — the empty 88 mm grid before anything is placed, the built-zone
    /// outline after — with zoomOutPadding of room around it.
    ///
    /// The frame comes from GroundGridController, which draws both, so the
    /// limit and the outline cannot disagree about how big the zone is.
    ///
    /// THE SAME AT EVERY ANGLE. It used to be fitted to the current view: the
    /// frame's corners turned into camera space, so the limit changed as the
    /// view orbited, and whenever it shrank under the current zoom the view
    /// pulled itself in while the user was only turning. Orbiting is not a
    /// request to zoom.
    ///
    /// It is now sized to the frame's half-DIAGONAL, which is the most room a
    /// flat rectangle can take up on screen from any direction: seen
    /// orthographically, no corner is ever further from the centre than that.
    /// A limit that already fits the worst angle has nothing to change for any
    /// other. It moves only when the zone itself changes size — a part placed
    /// or removed — which is the one time it should.
    ///
    /// The height is the tight side on a landscape window; on a portrait one
    /// the width is, so the diagonal is divided by the aspect there. That
    /// follows the window, never the orbit.
    /// </summary>
    float ZoomOutLimit()
    {
        RefreshBuiltBounds();
        float limit = maxViewSize;

        GroundGridController grid = GroundGridController.Active;
        if (grid != null && _cam != null && grid.TryGetViewFrame(out _, out Vector2 half))
        {
            float aspect = Mathf.Max(_cam.aspect, 0.01f);
            float diagonal = half.magnitude;
            float needed = diagonal * Mathf.Max(1f, 1f / aspect);

            limit = Mathf.Min(maxViewSize, needed * Mathf.Max(zoomOutPadding, 0.5f));
        }

        if (_hasBuiltBounds && _cam != null)
        {
            // A sphere around the complete build fits from every orbit angle,
            // including tall structures whose ground footprint is very small.
            float margin = GroundGridController.Active != null
                ? GroundGridController.Active.zonePaddingModules * NeospaceUnits.ModuleMeters : 0f;
            Vector3 reach = _builtBounds.extents + new Vector3(margin, 0, margin);
            float radius = reach.magnitude * Mathf.Max(1.1f, zoomOutPadding);
            limit = radius * Mathf.Max(1f / _presetHeightFraction, 1f / Mathf.Max(.01f, _cam.aspect * .9f));
        }
        else limit /= _presetHeightFraction;

        return Mathf.Max(limit, minViewSize);
    }

    /// <summary>
    /// The ground height, including the drop the finish mode applies to the
    /// floor so the build stands on its feet.
    /// </summary>
    static float GroundHeight => GroundGridController.FloorVisualOffset;

    /// <summary>
    /// Slide the orbit point along the view axis until it is on the ground.
    ///
    /// In orthographic this moves nothing on screen, and it matters a great
    /// deal. Pan and zoom-at-cursor both move the pivot across the VIEW PLANE,
    /// which is tilted, so repeated zooming toward the bottom of the screen
    /// walked it steadily underground. The camera follows the pivot, so it went
    /// down too, and the near clip plane dragged a hard edge across the grid
    /// from further and further up the screen. It also made the orbit swing
    /// around a point buried under the floor.
    /// </summary>
    void SeatPivotOnGround()
    {
        Vector3 forward = transform.forward;
        if (forward.y > -0.01f)
            return;

        _pivot += forward * ((GroundHeight - _pivot.y) / forward.y);
        ApplyPose();
    }

    /// <summary>
    /// Zoom the orthographic view so the point under the cursor stays under
    /// the cursor — the same promise the perspective zoom makes, kept a
    /// different way. Perspective moves the camera toward the point;
    /// orthographic cannot, because moving does not zoom. It changes the view
    /// size, sees where that point has drifted to on screen, and slides the
    /// camera sideways by exactly the drift.
    /// </summary>
    void ZoomViewAtCursor(float factor)
    {
        Vector3 before = PointOnViewPlane(_cam.ScreenPointToRay(Input.mousePosition), _pivot);
        SetViewSize(_cam.orthographicSize * factor);
        Vector3 after = PointOnViewPlane(_cam.ScreenPointToRay(Input.mousePosition), _pivot);

        Vector3 shift = before - after;
        transform.position += shift;
        _pivot += shift;
        SeatPivotOnGround();
    }

    /// <summary>
    /// Keep the orthographic camera just outside the build: far enough that
    /// the near clip plane cannot cut into it, near enough that the shadows
    /// URP measures from the camera still reach it. See MinStandoff.
    /// </summary>
    void RefreshStandoff()
    {
        float needed = MinStandoff;
        if (TryGetSceneBounds(out Bounds b))
        {
            float reach = Vector3.Distance(_pivot, b.center) + b.extents.magnitude;
            needed = Mathf.Max(needed, reach + StandoffMargin);
        }
        needed = Mathf.Min(needed, MaxStandoff);

        if (!Mathf.Approximately(needed, _distance))
        {
            _distance = needed;
            ApplyPose();
        }
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

        _distance = Mathf.Clamp(Vector3.Distance(origin, _pivot), minDistance, DistanceCap);
        SeatTargetOnViewAxis();
    }

    float SeedRange()
    {
        if (TryGetSceneBounds(out Bounds b))
        {
            float toScene = Vector3.Distance(transform.position, b.center) + b.extents.magnitude;
            return Mathf.Clamp(toScene, FallbackDistance, DistanceCap);
        }

        return Mathf.Min(DistanceCap, FallbackDistance * 4f);
    }

    void SeatTargetOnViewAxis()
    {
        Vector3 eye = transform.position;
        if (Orthographic) eye += transform.up * ((_viewCenterY * 2f - 1f) * _cam.orthographicSize);
        float along = Vector3.Dot(_pivot - eye, transform.forward);
        _distance = Mathf.Clamp(along > 0.0001f ? along : _distance, minDistance, DistanceCap);
        _pivot = eye + transform.forward * _distance;
    }

    float PickRange()
    {
        return Mathf.Min(DistanceCap, Mathf.Max(_distance * MaxPickDistanceFactor, minDistance * 8f));
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

        if (Orthographic)
        {
            // The view size is half the HEIGHT shown. A window narrower than
            // it is tall runs out of width first, so the radius is divided by
            // the aspect to make it fit across as well as up.
            float aspect = Mathf.Max(_cam.aspect, 0.01f);
            ApplyPose();
            SetViewSize(radius * Mathf.Max(1f, 1f / aspect));
            SeatPivotOnGround();
            RefreshStandoff();
            return;
        }

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
            // The studio backdrop hangs off the camera and fills the screen.
            // Counted as model, it would drag every "frame what is built"
            // toward the camera and never let the view settle.
            if (SceneBackdrop.IsBackdrop(r))
                continue;
            // The ground grid's quad is resized to the visible ground every
            // frame; counted as model, zoom extents would frame the grid, and
            // the orthographic standoff would chase its own view out to the
            // horizon.
            if (r.GetComponentInParent<GroundGridController>() != null)
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
