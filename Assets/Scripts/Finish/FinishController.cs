using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-click "Finish": dresses the placed frames in veneers and caps
/// (<see cref="FinishGenerator"/>), and keeps them fresh — while the toggle
/// is on, any change to the structure (placing, moving, undo, template
/// stamps, clipboard merges, Space piece merges) regenerates the finishing
/// automatically.
///
/// Build mode: the toggle dresses the live build, and the choice persists
/// across play sessions. Space Mode: ALWAYS dressed — placed pieces are
/// finished furniture — covering the frozen parts inside every piece
/// instance, beams the space merge split and panels it re-cut, so the
/// finish always matches the current frame reality. Regeneration holds
/// while a piece is being dragged and lands right after the merge resolves.
///
/// Finish parts are pure dressing: no colliders, no part identity, parented
/// under one root — so selection, physics validation, history snapshots,
/// configuration codes, and piece harvesting never see them.
///
/// Models load from Resources/Finish (the veneer and cap FBX assets). Their
/// axes are measured, not assumed: the longest extent is the length, the
/// thinnest the plate normal — robust against FBX axis conversion.
/// </summary>
public class FinishController : MonoBehaviour
{
    const string ResourceFolder = "Finish";
    const float PollInterval = 0.35f;

    public static FinishController Instance { get; private set; }

    /// <summary>
    /// The controller always exists (Space Mode dresses pieces without any
    /// toggle). Build mode always starts bare: the Finish toggle is a
    /// deliberate per-session step, exactly like the Rhino tools.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    public bool IsOn { get; private set; }

    /// <summary>Changes when the installed dressing is regenerated or removed.</summary>
    public static int PartsVersion { get; private set; }

    /// <summary>
    /// Synchronize before counting or saving. Does not create history entries;
    /// an in-progress restore or Space drag must finish before its dressing does.
    /// </summary>
    public void RefreshNow()
    {
        if (BuildHistory.Instance != null && BuildHistory.Instance.IsRestoring) return;
        if (SpaceModeController.Active && SpaceBusy()) return;
        if (!EffectiveOn)
        {
            if (_wasEffective)
            {
                _wasEffective = false;
                ClearParts();
                _desiredFloorDrop = 0f;
                ApplyFloorDrop(0f);
            }
            return;
        }
        _wasEffective = true;
        bool spaceMode = SpaceModeController.Active;
        if (_lastSpaceMode != spaceMode || _root == null || StructureFingerprint() != _lastFingerprint)
        {
            _lastSpaceMode = spaceMode;
            Regenerate(announce: false);
        }
    }

    /// <summary>
    /// Count only successfully installed model roots. Old objects awaiting
    /// Destroy and unloaded/unavailable models never contribute to the bill.
    /// </summary>
    public void CollectUsedParts(Dictionary<string, int> quantities)
    {
        if (_root == null || !_root.gameObject.activeInHierarchy) return;
        foreach (Transform part in _root)
        {
            if (!part.gameObject.activeInHierarchy) continue;
            quantities.TryGetValue(part.name, out int count);
            quantities[part.name] = count + 1;
        }
    }

    /// <summary>
    /// Space Mode is ALWAYS dressed: placed pieces are finished furniture,
    /// not bare frames, so the finish there doesn't depend on the toggle.
    /// The toggle governs Build mode only.
    /// </summary>
    bool EffectiveOn => IsOn || SpaceModeController.Active;

    bool _wasEffective;

    BuildController _buildController;
    SpaceInteractionController _spaceInteraction;
    Transform _root;
    float _nextPoll;
    int _lastFingerprint;
    bool _lastSpaceMode;

    struct ModelInfo
    {
        public GameObject Prefab;
        public Quaternion BaseRotation;  // prefab root rotation (axis conversion)
        public Vector3 LengthAxis;       // world axis of max extent at base pose
        public Vector3 NormalAxis;       // world axis of min extent at base pose
        public Vector3 CenterOffset;     // bounds centre at base pose
        public float HalfLength;         // half extent along LengthAxis
        public float HalfWidth;          // half extent across the visible plate
        public float HalfThickness;      // half extent along NormalAxis
    }

    readonly Dictionary<string, ModelInfo> _models = new Dictionary<string, ModelInfo>();
    readonly HashSet<string> _missingModels = new HashSet<string>();

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    public static FinishController Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("FinishController");
            Instance = go.AddComponent<FinishController>();
        }
        return Instance;
    }

    void Awake()
    {
        Instance = this;
        _buildController = FindFirstObjectByType<BuildController>();
    }

    void OnEnable() { BuildHistory.Changed += OnBuildChanged; }

    void OnDisable() { BuildHistory.Changed -= OnBuildChanged; }

    void OnBuildChanged() { _pollForced = true; }

    void OnDestroy()
    {
        ApplyFloorDrop(0f);
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------
    // Floor drop (feet raise the configuration off the visible ground)
    // ------------------------------------------------------------------

    bool _pollForced = true;

    float _desiredFloorDrop;
    float _floorDrop;
    GameObject _floor;
    Vector3 _floorHome;
    bool _floorHomeKnown;
    GameObject _floorProxy;

    /// <summary>
    /// Sink the visible floor by <paramref name="drop"/> so the feet occupy
    /// the revealed gap. Build logic never moves: an invisible collider
    /// proxy stays at the original ground level, so seating, ghost rays,
    /// and snapping behave exactly as without Finish.
    /// </summary>
    void ApplyFloorDrop(float drop)
    {
        if (Mathf.Approximately(drop, _floorDrop))
            return;

        if (_floor == null)
            _floor = GameObject.Find("GridFloor");
        if (_floor == null)
        {
            _floorDrop = drop;
            return;
        }

        if (!_floorHomeKnown)
        {
            _floorHome = _floor.transform.position;
            _floorHomeKnown = true;
        }

        bool dropped = drop > 0f;

        if (dropped && _floorProxy == null)
        {
            _floorProxy = new GameObject("GridFloorColliderProxy")
            {
                layer = _floor.layer
            };
            Renderer floorRenderer = _floor.GetComponent<Renderer>();
            Bounds b = floorRenderer != null
                ? floorRenderer.bounds
                : new Bounds(_floorHome, new Vector3(200f, 0.01f, 200f));
            _floorProxy.transform.position = new Vector3(b.center.x, b.max.y - 0.005f, b.center.z);
            BoxCollider box = _floorProxy.AddComponent<BoxCollider>();
            box.size = new Vector3(b.size.x, 0.01f, b.size.z);
        }

        _floor.transform.position = _floorHome + Vector3.down * drop;
        foreach (Collider c in _floor.GetComponentsInChildren<Collider>(true))
            c.enabled = !dropped;
        if (_floorProxy != null)
            _floorProxy.SetActive(dropped);

        AdaptiveGridController.FloorVisualOffset = -drop;
        _floorDrop = drop;
        Physics.SyncTransforms();
    }

    // ------------------------------------------------------------------
    // Toggle
    // ------------------------------------------------------------------

    public void Toggle() => SetOn(!IsOn);

    /// <summary>
    /// Poke from the structure side (the space merge calls this after every
    /// re-resolve): skip the poll delay and re-check right away, so the
    /// dressing lands in the same beat as the merge itself.
    /// </summary>
    public static void NotifyStructureChanged()
    {
        if (Instance != null && Instance.EffectiveOn)
        {
            Instance._nextPoll = 0f;
            Instance._pollForced = true;
            // SpaceMerge invokes this after hiding originals and creating
            // replacement parts. Finish that committed layout immediately,
            // including when only Space Mode makes finishing effective.
            if (SpaceModeController.Active) Instance.RefreshNow();
        }
    }

    public void SetOn(bool on, bool announce = true)
    {
        if (IsOn == on)
            return;
        IsOn = on;
        Debug.Log($"[Finish] Toggled {(on ? "ON" : "OFF")} in {(SpaceModeController.Active ? "space" : "build")} mode.");

        _lastFingerprint = 0;
        _nextPoll = 0f;
        _lastSpaceMode = SpaceModeController.Active;

        if (EffectiveOn)
        {
            _wasEffective = true;
            Regenerate(announce: announce && on);
        }
        else
        {
            _wasEffective = false;
            ClearParts();
            _desiredFloorDrop = 0f;
            ApplyFloorDrop(0f);
            if (announce)
                SelectionStatus.Set("Finish removed · frames are bare again.", 3f);
        }
        BuildHistory.NotifyChanged();
    }

    void Update()
    {
        bool spaceMode = SpaceModeController.Active;

        if (!EffectiveOn)
        {
            // Toggle off (or left Space Mode with the toggle off): take the
            // dressing down once, then idle until it is needed again.
            if (_wasEffective)
            {
                _wasEffective = false;
                _lastSpaceMode = spaceMode;
                _lastFingerprint = 0;
                ClearParts();
                _desiredFloorDrop = 0f;
                ApplyFloorDrop(0f);
            }
            return;
        }
        _wasEffective = true;

        // During a drag the merge hasn't landed yet. An armed placement
        // preview stays outside the installed geometry and does not hold this.
        if (spaceMode && SpaceBusy())
            return;

        ApplyFloorDrop(_desiredFloorDrop);

        // A mode switch swaps the dressed structure entirely (live build vs
        // piece instances) — rebuild immediately, don't wait for the poll.
        if (spaceMode != _lastSpaceMode)
        {
            _lastSpaceMode = spaceMode;
            Regenerate(announce: false);
            return;
        }

        if (!_pollForced && Time.unscaledTime < _nextPoll)
            return;
        _nextPoll = Time.unscaledTime + PollInterval;
        _pollForced = false;

        // Direct transform edits and frozen Space geometry do not necessarily
        // publish a placement event. Poll the actual planner inputs as well.
        int fp = StructureFingerprint();
        if (fp != _lastFingerprint)
            Regenerate(announce: false);
    }

    bool SpaceBusy()
    {
        if (_spaceInteraction == null)
            _spaceInteraction = FindFirstObjectByType<SpaceInteractionController>();
        return _spaceInteraction != null && _spaceInteraction.Busy;
    }

    // ------------------------------------------------------------------
    // Generation
    // ------------------------------------------------------------------

    void Regenerate(bool announce)
    {
        // Same planner in both modes, different part source: the live build,
        // or the frozen (merge-resolved) parts inside placed Space pieces.
        bool spaceMode = SpaceModeController.Active;
        CollectFinishInputs(spaceMode, out List<FrameOverlapResolver.FrameRecord> frames,
            out List<FinishGenerator.PanelBox> panels);

        FinishGenerator.Result plan = FinishGenerator.Plan(frames, panels, MeasuredModelDimensions());

        ClearParts();
        EnsureRoot();

        int placed = 0;
        bool anyFeet = false;
        foreach (FinishGenerator.Placement p in plan.Parts)
        {
            if (!Place(p))
                continue;
            placed++;
            if (p.Model == "Foot")
                anyFeet = true;
        }
        PartsVersion++;

        // One breadcrumb per rebuild: which mode fed it, what it saw, what it
        // produced — the first thing to read when dressing looks wrong.
        Debug.Log($"[Finish] Regenerated ({(spaceMode ? "space" : "build")}): " +
                  $"{frames.Count} frames, {panels.Count} panels -> " +
                  $"{plan.Parts.Count} planned, {placed} placed.");

        // Feet lift the physical structure off the ground; here the build's
        // coordinates never move — the visible floor sinks by one foot
        // height instead, so the configuration stands raised on its feet.
        _desiredFloorDrop = anyFeet && TryGetModel("Foot", out ModelInfo foot)
            ? foot.HalfThickness * 2f
            : 0f;
        ApplyFloorDrop(_desiredFloorDrop);

        _lastFingerprint = StructureFingerprint();

        if (frames.Count == 0)
        {
            if (announce)
                SelectionStatus.Set(spaceMode
                    ? "Nothing to finish yet · place some pieces first."
                    : "Nothing to finish yet · place some frames first.", 3f);
            return;
        }

        if (announce || plan.Warnings.Count > 0)
        {
            string msg = $"Finish applied · {placed} veneers and caps.";
            if (plan.Warnings.Count > 0)
                msg += $" {plan.Warnings.Count} section{(plan.Warnings.Count == 1 ? "" : "s")} can't be covered by the veneer sizes.";
            SelectionStatus.Set(msg, announce ? 4f : 3f);
            foreach (string w in plan.Warnings)
                Debug.LogWarning($"[Finish] {w}");
        }
    }

    bool Place(in FinishGenerator.Placement placement)
    {
        if (!TryGetModel(placement.Model, out ModelInfo info))
            return false;

        if (placement.Model == "Foot")
            return PlaceFoot(placement, info);

        // Map the measured local axes onto the requested world directions,
        // then push the plate off the channel face by its half thickness
        // (plus a hair so coplanar surfaces never z-fight).
        Quaternion rot = MapBasis(info.LengthAxis, info.NormalAxis,
            placement.LengthDir, placement.Normal);
        Vector3 pos = placement.Center +
                      placement.Normal * (info.HalfThickness + NeospaceUnits.Mm(0.2f)) -
                      rot * info.CenterOffset;

        GameObject go = Instantiate(info.Prefab, _root);
        go.transform.SetPositionAndRotation(pos, rot * info.BaseRotation);
        go.name = placement.Model;
        MakeDressingOnly(go);
        ApplyDressingMaterial(go);
        return true;
    }

    /// <summary>
    /// Every veneer and cap wears the palette's dressing swatch — the FBX's
    /// own material is replaced wholesale so the outline colour is uniform
    /// across all plate models. The Foot is the one exception: it is always
    /// black, whatever the palette (the object's name picks the material,
    /// so restyling existing parts routes the same way).
    /// </summary>
    static void ApplyDressingMaterial(GameObject go)
    {
        Material mat = go.name == "Foot" ? FinishStyle.FootMaterial : FinishStyle.DressingMaterial;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    /// <summary>Palette changed: recolour the existing dressing in place.</summary>
    public void RestyleDressing()
    {
        if (_root == null)
            return;
        for (int i = 0; i < _root.childCount; i++)
            ApplyDressingMaterial(_root.GetChild(i).gameObject);
    }

    /// <summary>
    /// Finish parts must be pure visuals. CAD-exported FBX files can carry
    /// viewport cameras and lights (the Foot shipped with four Rhino views);
    /// a stray live camera renders over the main one and blacks the screen.
    /// Disable immediately — Destroy alone is deferred a frame.
    /// </summary>
    static void MakeDressingOnly(GameObject go)
    {
        foreach (Camera cam in go.GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
            Destroy(cam.gameObject);
        }
        foreach (Light light in go.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
            Destroy(light.gameObject);
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }

    /// <summary>
    /// The Foot sits UNDER the post, exactly as in Rhino: its top face (the
    /// pivot plane, found from where the bounds centre hangs below the
    /// pivot) meets the post's bottom end, and the visible floor drops by
    /// the foot's height while feet exist — so the whole configuration
    /// stands raised on its feet, matching the physical system.
    /// </summary>
    bool PlaceFoot(in FinishGenerator.Placement placement, in ModelInfo info)
    {
        float alongNormal = Vector3.Dot(info.CenterOffset, info.NormalAxis);
        Vector3 localTop = alongNormal <= 0f ? info.NormalAxis : -info.NormalAxis;

        // placement.Normal is the post's up axis at a grounded bottom.
        Quaternion rot = MapBasis(localTop, info.LengthAxis,
            placement.Normal, placement.LengthDir);

        float height = info.HalfThickness * 2f;
        Vector3 centre = placement.Center - placement.Normal * (height * 0.5f);
        Vector3 pos = centre - rot * info.CenterOffset;

        GameObject go = Instantiate(info.Prefab, _root);
        go.transform.SetPositionAndRotation(pos, rot * info.BaseRotation);
        go.name = placement.Model;
        MakeDressingOnly(go);
        ApplyDressingMaterial(go);
        return true;
    }

    /// <summary>
    /// Rotation mapping <paramref name="localA"/> onto <paramref name="worldA"/>
    /// exactly, and <paramref name="localB"/> onto <paramref name="worldB"/>
    /// by the residual twist around worldA.
    /// </summary>
    static Quaternion MapBasis(Vector3 localA, Vector3 localB, Vector3 worldA, Vector3 worldB)
    {
        Quaternion q1 = Quaternion.FromToRotation(localA, worldA);
        Vector3 b1 = q1 * localB;
        float twist = Vector3.SignedAngle(b1, worldB, worldA);
        return Quaternion.AngleAxis(twist, worldA) * q1;
    }

    void EnsureRoot()
    {
        if (_root == null)
            _root = new GameObject("FinishRoot").transform;
        if (!_root.gameObject.activeSelf)
            _root.gameObject.SetActive(true);
    }

    void ClearParts()
    {
        if (_root == null)
            return;
        PartsVersion++;
        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            GameObject part = _root.GetChild(i).gameObject;
            // Destroy is deferred. Hide the old run immediately so a rebuild
            // cannot draw intersecting old veneers over its replacement.
            part.SetActive(false);
            Destroy(part);
        }
    }

    // ------------------------------------------------------------------
    // Model loading + measurement
    // ------------------------------------------------------------------

    Dictionary<string, FinishGenerator.PartDimensions> MeasuredModelDimensions()
    {
        var dimensions = new Dictionary<string, FinishGenerator.PartDimensions>();
        foreach (int size in CatalogueData.VeneerLengths)
            Add("Veneer H" + size);
        Add("Cap Side");
        Add("Cap End");
        return dimensions;

        void Add(string name)
        {
            if (!TryGetModel(name, out ModelInfo info)) return;
            dimensions[name] = new FinishGenerator.PartDimensions
            {
                HalfLengthMm = NeospaceUnits.ToMm(info.HalfLength),
                HalfWidthMm = NeospaceUnits.ToMm(info.HalfWidth),
                HalfThicknessMm = NeospaceUnits.ToMm(info.HalfThickness)
            };
        }
    }

    bool TryGetModel(string name, out ModelInfo info)
    {
        if (_models.TryGetValue(name, out info))
            return true;
        if (_missingModels.Contains(name))
            return false;

        GameObject prefab = Resources.Load<GameObject>($"{ResourceFolder}/{name}");
        if (prefab == null)
        {
            _missingModels.Add(name);
            Debug.LogWarning($"[Finish] Missing model Resources/{ResourceFolder}/{name}");
            return false;
        }

        // Measure at the prefab's own pose so FBX axis conversion is
        // included: world extents at base rotation give the real axes.
        GameObject temp = Instantiate(prefab);
        temp.name = "__FinishMeasure__";
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.rotation);
        MakeDressingOnly(temp); // embedded cameras render even for one frame

        Renderer[] rends = temp.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
        {
            Destroy(temp);
            _missingModels.Add(name);
            return false;
        }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        Destroy(temp);

        Vector3 e = b.size;
        Vector3 lengthAxis, normalAxis;
        float thickness;
        if (e.x >= e.y && e.x >= e.z) lengthAxis = Vector3.right;
        else if (e.y >= e.z) lengthAxis = Vector3.up;
        else lengthAxis = Vector3.forward;
        if (e.x <= e.y && e.x <= e.z) { normalAxis = Vector3.right; thickness = e.x; }
        else if (e.y <= e.z) { normalAxis = Vector3.up; thickness = e.y; }
        else { normalAxis = Vector3.forward; thickness = e.z; }

        info = new ModelInfo
        {
            Prefab = prefab,
            BaseRotation = prefab.transform.rotation,
            LengthAxis = lengthAxis,
            NormalAxis = normalAxis,
            CenterOffset = b.center,
            HalfLength = Mathf.Abs(Vector3.Dot(e, lengthAxis)) * 0.5f,
            HalfWidth = Mathf.Abs(Vector3.Dot(e, Vector3.Cross(lengthAxis, normalAxis))) * 0.5f,
            HalfThickness = thickness * 0.5f
        };
        _models[name] = info;
        return true;
    }

    // ------------------------------------------------------------------
    // Change detection
    // ------------------------------------------------------------------

    int GhostMask() => _buildController != null ? _buildController.ghostLayerMask.value : 0;

    /// <summary>
    /// Collect exactly the geometry used by the planner, including active merge
    /// results. Duplicate BeamConnections must not contribute the same root twice.
    /// </summary>
    void CollectFinishInputs(bool spaceMode, out List<FrameOverlapResolver.FrameRecord> frames,
        out List<FinishGenerator.PanelBox> panels)
    {
        int ghostMask = GhostMask();
        frames = spaceMode ? FinishGenerator.CollectSpaceFrames() : FrameOverlapResolver.CollectFrames(ghostMask);
        panels = spaceMode ? FinishGenerator.CollectSpacePanels() : FinishGenerator.CollectPanels(ghostMask);
        var seen = new HashSet<Transform>();
        frames.RemoveAll(frame => frame.Root == null || !frame.Root.gameObject.activeInHierarchy || !seen.Add(frame.Root));
    }

    /// <summary>
    /// Hash planner geometry, not just object positions: rotating a frame in
    /// place or resizing a centered panel changes its finishing requirements.
    /// </summary>
    int StructureFingerprint()
    {
        bool spaceMode = SpaceModeController.Active;
        CollectFinishInputs(spaceMode, out List<FrameOverlapResolver.FrameRecord> frames,
            out List<FinishGenerator.PanelBox> panels);

        unchecked
        {
            int hash = (spaceMode ? 0x5A0F1 : 17) * 31 + frames.Count * 397 + panels.Count;
            // Sum independent record hashes so enumeration order cannot cause a
            // rebuild. Counts preserve multiplicity (XOR cancels duplicate pairs).
            foreach (FrameOverlapResolver.FrameRecord frame in frames)
            {
                int part = (frame.PartId?.GetHashCode() ?? 0) * 31 + frame.Size;
                part = VectorHash(part, frame.Center, 1000f);
                part = VectorHash(part, frame.EndA, 1000f);
                part = VectorHash(part, frame.EndB, 1000f);
                part = VectorHash(part, frame.LengthAxis, 10000f);
                part = VectorHash(part, frame.LocalY, 10000f);
                part = VectorHash(part, frame.Root.right, 10000f);
                part = VectorHash(part, frame.Root.lossyScale, 10000f);
                hash += part;
            }
            foreach (FinishGenerator.PanelBox panel in panels)
            {
                int part = VectorHash(7919, panel.Center, 1000f);
                part = VectorHash(part, panel.Normal, 10000f);
                part = VectorHash(part, panel.AxisU, 10000f);
                part = VectorHash(part, panel.AxisV, 10000f);
                part = VectorHash(part, new Vector3(panel.HalfU, panel.HalfV, panel.HalfN), 1000f);
                hash += part;
            }
            return hash;
        }
    }

    static int VectorHash(int seed, Vector3 vector, float precision)
    {
        unchecked
        {
            int hash = seed * 486187739 + Mathf.RoundToInt(vector.x * precision);
            hash = hash * 486187739 + Mathf.RoundToInt(vector.y * precision);
            return hash * 486187739 + Mathf.RoundToInt(vector.z * precision);
        }
    }
}
