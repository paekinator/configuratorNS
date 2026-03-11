using System;
using System.Collections.Generic;
using UnityEngine;

public class PanelSlotManager : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Tooltip("Mask containing ONLY the Ghost layer. Used to ignore ghost APs.")]
    public LayerMask ghostLayerMask;

    [Header("Prefabs")]
    public GameObject panelPrefab;

    [Header("Parents")]
    public Transform slotsRoot;
    public Transform panelsRoot;

    [Header("Layers")]
    public int slotTriggerLayer;
    public int panelLayer;
    public int panelBlockerLayer;

    [Header("Thickness / Offsets")]
    public float frameThickness = 0.05f;
    public float panelThickness = 0.001f;
    public float panelGap = 0.01f;
    public float panelOutset = 0.05f;

    [Header("Panel Fit (Inner Opening)")]
    public float panelInsetX = 0.00f;
    public float panelInsetY = 0.00f;

    [Header("Slot Detection Tolerances")]
    public float parallelDotThreshold = 0.005f;     // parallel check
    public float perpendicularDotMax = 0.15f;       // abs(dot) <= this means perpendicular-ish
    [Tooltip("Minimum vertical gap between two horizontal spans to form a wall slot.")]
    public float minWallVerticalGap = 0.01f;
    [Tooltip("Maximum allowed span-length mismatch when pairing wall spans.")]
    public float maxWallSpanLengthDelta = 0.05f;
    [Tooltip("A span is considered floor-ish when abs(dir.y) <= this value.")]
    public float maxFloorSpanDirY = 0.15f;
    [Tooltip("Minimum H span length considered during graph build. Lower this to detect smaller openings.")]
    public float minSpanLength = 0.02f;
    [Tooltip("Minimum wall slot width/height. Lower this to allow smaller wall rectangles.")]
    public float minWallSlotSize = 0.02f;
    [Tooltip("Minimum floor slot width/height. Lower this to allow smaller floor/roof rectangles.")]
    public float minFloorSlotSize = 0.02f;
    [Tooltip("Splitter chord epsilon = max(splitterEpsilonMin, nodeQuantize * splitterEpsilonScale). Lower values allow smaller floor rectangles.")]
    public float splitterEpsilonScale = 2.0f;
    public float splitterEpsilonMin = 0.002f;

    [Header("Peg/Hole Proximity Match (PAIRING)")]
    [Tooltip("Peg<->Hole match max distance (meters). If alignment isn't perfect, try 0.03~0.06 temporarily.")]
    public float pegToHoleMatchDistance = 0.02f;

    [Header("Floor/Roof Detection")]
    [Tooltip("Height tolerance for grouping floor/roof spans. If you get 0 floor slots, raise to 0.05~0.10.")]
    public float floorYBucketSize = 0.02f; // 2cm tolerance, not a strict bucket


    [Header("Node Quantization (CORNER keys)")]
    [Tooltip("Quantization size for corner keys (meters). Try 0.01 (1cm). If corners drift, try 0.02.")]
    public float nodeQuantize = 0.001f;


    [Header("Scan Settings")]
    public bool scanEveryFrame = true;

    [Header("Panel Persistence")]
    [Tooltip("If true, panels are preserved when a slot temporarily disappears during rescans, and will reattach when the slot returns.")]
    public bool preservePanelsOnSlotLoss = true;

    [Header("Debug")]
    public bool debug = true;

    private readonly Dictionary<string, PanelSlotHandle> _slots = new Dictionary<string, PanelSlotHandle>();
    private readonly HashSet<string> _seen = new HashSet<string>();

    void Awake()
    {
        if (slotsRoot == null)
        {
            var go = new GameObject("SlotsRoot");
            slotsRoot = go.transform;
        }

        if (panelsRoot == null)
        {
            var go = new GameObject("PanelsRoot");
            panelsRoot = go.transform;
        }
    }

    void Update()
    {
        if (scanEveryFrame)
        {
            RebuildConnectionsFromProximity();
            ScanAllSlots();
        }
    }

    // ---------------------------
    // Public API
    // ---------------------------
    public bool TryGetSlot(string slotId, out PanelSlotHandle slot)
    {
        if (string.IsNullOrEmpty(slotId))
        {
            slot = null;
            return false;
        }
        return _slots.TryGetValue(slotId, out slot) && slot != null;
    }

    public bool CanPlacePanel(PanelSlotHandle slot, int side)
    {
        if (slot == null) return false;
        return (side > 0) ? slot.panelPlus == null : slot.panelMinus == null;
    }

    public GameObject PlacePanel(PanelSlotHandle slot, int side)
    {
        if (panelPrefab == null || slot == null) return null;

        side = (side >= 0) ? 1 : -1;
        if (!CanPlacePanel(slot, side)) return null;

        // Match ghost basis by using slot trigger rotation when available.
        Quaternion rot = (slot.slotTrigger != null) ? slot.slotTrigger.rotation : Quaternion.LookRotation(slot.normal, slot.upAxis);

        Vector3 n = (slot.normal.sqrMagnitude > 1e-6f) ? slot.normal.normalized : (rot * Vector3.forward);

        float innerW = Mathf.Max(0.01f, slot.sizeXY.x - 0.4f);
        float innerH = Mathf.Max(0.01f, slot.sizeXY.y - 0.4f);

        Debug.Log($"2innerW={innerW} innerH={innerH}  slot.sizeXY={slot.sizeXY}  frameThick={frameThickness} insetX={panelInsetX} insetY={panelInsetY}");


        float offset = panelOutset + panelGap + (panelThickness * 0.5f);
        Vector3 pos = slot.center + n * (side > 0 ? offset : -offset);

        var panel = Instantiate(panelPrefab, pos, rot, panelsRoot);
        SetLayerRecursively(panel, panelLayer);

        panel.transform.localScale = new Vector3(innerW, innerH, panelThickness);

        var pi = panel.GetComponent<PanelInstance>();
        if (pi == null) pi = panel.AddComponent<PanelInstance>();
        pi.slotId = slot.slotId;
        pi.side = side;

        if (side > 0) slot.panelPlus = panel;
        else slot.panelMinus = panel;

        if (slot.blocker != null) slot.blocker.gameObject.SetActive(slot.HasAnyPanel());

        return panel;
    }

    public void RemovePanel(PanelSlotHandle slot, int side)
    {
        if (slot == null) return;

        if (side > 0)
        {
            if (slot.panelPlus != null) Destroy(slot.panelPlus);
            slot.panelPlus = null;
        }
        else
        {
            if (slot.panelMinus != null) Destroy(slot.panelMinus);
            slot.panelMinus = null;
        }

        if (slot.blocker != null) slot.blocker.gameObject.SetActive(slot.HasAnyPanel());
    }

    public void RebuildConnectionsAndRescanSlots()
    {
        RebuildConnectionsFromProximity();
        ScanAllSlots();
    }

    // ---------------------------
    // SLOT SCAN ENTRY
    // ---------------------------
    public void ScanAllSlots()
    {
        _seen.Clear();

        var spans = CollectHSpansFromPairedConnections();

        var wallGeoms = DetectWallSlotsFromSpans(spans);
        for (int i = 0; i < wallGeoms.Count; i++)
            UpsertSlot(wallGeoms[i]);

        // Floor/roof slots (H-only rectangles). Axis-aligned (grid) configurations supported.
        var floorGeoms = DetectFloorSlotsFromSpans(spans);
        for (int i = 0; i < floorGeoms.Count; i++)
            UpsertSlot(floorGeoms[i]);

        RemoveUnseenSlots();

        foreach (var kv in _slots)
        {
            var h = kv.Value;
            if (h == null) continue;
            if (h.blocker != null) h.blocker.gameObject.SetActive(h.HasAnyPanel());
        }
    }

    // ---------------------------
    // Pairing (Peg <-> Hole)
    // ---------------------------
    void RebuildConnectionsFromProximity()
    {
        var aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);

        var pegs = new List<AttachmentPoint>();
        var holes = new List<AttachmentPoint>();

        for (int i = 0; i < aps.Length; i++)
        {
            var ap = aps[i];
            if (ap == null) continue;

            Transform root = ap.transform.root;
            if (root == null) continue;

            if (IsInMask(root.gameObject.layer, ghostLayerMask)) continue;

            ap.isOccupied = false;
            ap.occupant = null;
            ap.pairedWith = null;

            if (ap.role == AttachmentPoint.PointRole.Peg) pegs.Add(ap);
            else holes.Add(ap);
        }

        float maxD = Mathf.Max(0.0001f, pegToHoleMatchDistance);
        float maxD2 = maxD * maxD;

        var candidates = new List<(AttachmentPoint peg, AttachmentPoint hole, float d2)>();

        for (int p = 0; p < pegs.Count; p++)
        {
            var peg = pegs[p];
            if (peg == null) continue;

            Vector3 pegPos = peg.transform.position;

            for (int h = 0; h < holes.Count; h++)
            {
                var hole = holes[h];
                if (hole == null) continue;

                if (hole.transform.root == peg.transform.root) continue;

                Vector3 holePos = hole.transform.position;
                float d2 = (pegPos - holePos).sqrMagnitude;
                if (d2 <= maxD2)
                    candidates.Add((peg, hole, d2));
            }
        }

        candidates.Sort((a, b) => a.d2.CompareTo(b.d2));

        int pairs = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.peg == null || c.hole == null) continue;

            if (c.peg.pairedWith != null) continue;
            if (c.hole.pairedWith != null) continue;

            c.peg.pairedWith = c.hole;
            c.hole.pairedWith = c.peg;

            c.peg.isOccupied = true;
            c.hole.isOccupied = true;

            c.peg.occupant = c.peg.transform.root != null ? c.peg.transform.root.gameObject : null;
            c.hole.occupant = c.hole.transform.root != null ? c.hole.transform.root.gameObject : null;

            pairs++;
        }

        if (debug)
            Debug.Log($"[Connections] AP pairs={pairs} (cand={candidates.Count}, maxD={pegToHoleMatchDistance:0.000})");
    }

    // ---------------------------
    // Detection Helpers
    // ---------------------------

    struct Endpoint
    {
        public Transform nodeRoot;     // for WALL detection (V roots)
        public Vector3Int key;         // for FLOOR detection (corner keys)
        public Vector3 point;          // corner world point
    }

    struct HSpan
    {
        public Transform hRoot;
        public Endpoint a;
        public Endpoint b;
        public Vector3 dir;
        public float length;
        public float avgY;
    }

    struct SlotGeom
    {
        public string id;
        public Vector3 c0, c1, c2, c3;
        public Vector3 normal;
        public Vector3 upAxis;
        public Vector3 center;
        public Vector2 sizeXY;
    }

    static bool IsVRoot(Transform t) =>
        t != null && t.name.StartsWith("V", StringComparison.OrdinalIgnoreCase);

    static bool IsHRoot(Transform t) =>
        t != null && t.name.StartsWith("H", StringComparison.OrdinalIgnoreCase);

    static string ExtractParenToken(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        int open = name.LastIndexOf('(');
        int close = name.LastIndexOf(')');
        if (open < 0 || close <= open) return null;

        string token = name.Substring(open + 1, close - open - 1).Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    Dictionary<string, Vector3> BuildMidTokenMap(Transform beamRoot)
    {
        var map = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        if (beamRoot == null) return map;

        var all = beamRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;

            string n = t.name;
            if (string.IsNullOrEmpty(n)) continue;
            if (!n.StartsWith("mid", StringComparison.OrdinalIgnoreCase)) continue;

            string token = ExtractParenToken(n);
            if (string.IsNullOrEmpty(token)) continue;

            if (!map.ContainsKey(token))
                map[token] = t.position;
        }

        return map;
    }

    Vector3Int KeyFromWorld(Vector3 p)
    {
        float q = Mathf.Max(0.0001f, nodeQuantize);
        // FLOOR corner identity uses XZ only. Height separation is handled by avgY bucketing.
        return new Vector3Int(
            Mathf.RoundToInt(p.x / q),
            0,
            Mathf.RoundToInt(p.z / q)
        );
    }

    static bool SameKey(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;

    // Stable slot IDs based on quantized corners.
    // This prevents panels from being destroyed when nearby beams are added/removed but the opening is unchanged.
    string BuildCornerHashId(string prefix, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float q = Mathf.Max(0.0001f, nodeQuantize);

        Vector3Int K(Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x / q),
            Mathf.RoundToInt(p.y / q),
            Mathf.RoundToInt(p.z / q)
        );

        var ks = new Vector3Int[] { K(p0), K(p1), K(p2), K(p3) };
        Array.Sort(ks, (a, b) =>
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        });

        // Use a string ID so it is deterministic across runs.
        return $"{prefix}_{ks[0].x},{ks[0].y},{ks[0].z}|{ks[1].x},{ks[1].y},{ks[1].z}|{ks[2].x},{ks[2].y},{ks[2].z}|{ks[3].x},{ks[3].y},{ks[3].z}";
    }

    // ---------------------------
    // Build H spans from pairing (occupied APs on H roots)
    // ---------------------------
    List<HSpan> CollectHSpansFromPairedConnections()
    {
        var aps = FindObjectsByType<AttachmentPoint>(FindObjectsSortMode.None);

        var hApsByHRoot = new Dictionary<Transform, List<AttachmentPoint>>();

        for (int i = 0; i < aps.Length; i++)
        {
            var ap = aps[i];
            if (ap == null) continue;

            Transform root = ap.transform.root;
            if (root == null) continue;
            if (!IsHRoot(root)) continue;

            if (IsInMask(root.gameObject.layer, ghostLayerMask)) continue;

            if (!hApsByHRoot.TryGetValue(root, out var list))
            {
                list = new List<AttachmentPoint>();
                hApsByHRoot[root] = list;
            }
            list.Add(ap);
        }

        var spans = new List<HSpan>();
        var midTokenMapByRoot = new Dictionary<Transform, Dictionary<string, Vector3>>();

        Vector3 ResolveApPoint(AttachmentPoint ap)
        {
            if (ap == null) return Vector3.zero;

            Vector3 pos = ap.transform.position;
            Transform root = ap.transform.root;
            if (root == null) return pos;

            string token = ExtractParenToken(ap.transform.name);
            if (string.IsNullOrEmpty(token)) return pos;

            if (!midTokenMapByRoot.TryGetValue(root, out var map))
            {
                map = BuildMidTokenMap(root);
                midTokenMapByRoot[root] = map;
            }

            if (map.TryGetValue(token, out var midPos))
                return midPos;

            return pos;
        }

        AttachmentPoint ResolveHoleApForCorner(AttachmentPoint ap)
        {
            if (ap == null || ap.pairedWith == null) return null;

            if (ap.role == AttachmentPoint.PointRole.Hole)
                return ap;

            if (ap.role == AttachmentPoint.PointRole.Peg && ap.pairedWith.role == AttachmentPoint.PointRole.Hole)
                return ap.pairedWith;

            return null;
        }

        foreach (var kv in hApsByHRoot)
        {
            Transform hRoot = kv.Key;
            var apsOnH = kv.Value;
            if (hRoot == null || apsOnH == null || apsOnH.Count == 0) continue;

            // Dedup endpoints by quantized corner key.
            // If multiple APs land on same corner, average their join positions.
            var endpointByKey = new Dictionary<Vector3Int, (Endpoint ep, int count)>();

            for (int a = 0; a < apsOnH.Count; a++)
            {
                var ap = apsOnH[a];
                if (ap == null) continue;
                if (!ap.isOccupied) continue;
                if (ap.pairedWith == null) continue;

                var holeAp = ResolveHoleApForCorner(ap);
                if (holeAp == null) continue;

                Transform otherRoot = ap.pairedWith.transform.root;
                if (otherRoot == null) continue;
                if (otherRoot == hRoot) continue;

                // Use HOLE-only position for corner generation (never peg position).
                // If a matching mid(token) exists, ResolveApPoint() will use that midpoint object.
                Vector3 join = ResolveApPoint(holeAp);

                // If this endpoint connects to a V post, snap XZ to the V post pivot.
                // This merges face-offset joins (front/back vs left/right) into a single logical corner.
                if (IsVRoot(otherRoot))
                {
                    Vector3 vPos = otherRoot.position;
                    join.x = vPos.x;
                    join.z = vPos.z;
                }

                Vector3Int k = KeyFromWorld(join);

                var ep = new Endpoint
                {
                    nodeRoot = otherRoot, // wall detection uses this; floor uses key/point
                    key = k,
                    point = join
                };

                if (endpointByKey.TryGetValue(k, out var existing))
                {
                    // average join positions to reduce jitter
                    var eep = existing.ep;
                    int cnt = existing.count + 1;
                    eep.point = (eep.point * existing.count + join) / cnt;
                    endpointByKey[k] = (eep, cnt);
                }
                else
                {
                    endpointByKey[k] = (ep, 1);
                }
            }

            var endpoints = new List<Endpoint>();
            foreach (var e in endpointByKey.Values)
                endpoints.Add(e.ep);

            if (endpoints.Count < 2) continue;

            // Determine a sorting axis along the beam:
            // Use farthest pair direction as the axis (robust even if beam rotated).
            Endpoint farA = endpoints[0], farB = endpoints[1];
            float best = -1f;
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    float d2 = (endpoints[i].point - endpoints[j].point).sqrMagnitude;
                    if (d2 > best)
                    {
                        best = d2;
                        farA = endpoints[i];
                        farB = endpoints[j];
                    }
                }
            }

            Vector3 axis = (farB.point - farA.point);
            if (axis.sqrMagnitude < 1e-6f)
            {
                // fallback if everything collapsed (rare)
                axis = hRoot.forward;
            }
            axis.Normalize();

            // Sort endpoints along axis
            endpoints.Sort((e1, e2) =>
            {
                float t1 = Vector3.Dot(e1.point, axis);
                float t2 = Vector3.Dot(e2.point, axis);
                return t1.CompareTo(t2);
            });

            // Create spans between ADJACENT endpoints (this is the critical fix)
            for (int i = 0; i < endpoints.Count - 1; i++)
            {
                Endpoint A = endpoints[i];
                Endpoint B = endpoints[i + 1];

                if (SameKey(A.key, B.key)) continue;

                float len = Vector3.Distance(A.point, B.point);
                if (len < minSpanLength) continue;

                Vector3 dir = (B.point - A.point).normalized;
                float avgY = (A.point.y + B.point.y) * 0.5f;

                spans.Add(new HSpan
                {
                    hRoot = hRoot,
                    a = A,
                    b = B,
                    dir = dir,
                    length = len,
                    avgY = avgY
                });

                if (debug)
                {
                    string na = A.nodeRoot != null ? A.nodeRoot.name : "null";
                    string nb = B.nodeRoot != null ? B.nodeRoot.name : "null";
                    Debug.Log($"[PanelSlot][SPAN] {hRoot.name}: A={na}  B={nb}  len={len:F3} avgY={avgY:F4}");
                }
            }
        }

        if (debug)
            Debug.Log($"[PanelSlot] H spans found={spans.Count}");

        return spans;
    }

    // ---------------------------
    // WALL SLOTS (kept as you had)
    // ---------------------------
    List<SlotGeom> DetectWallSlotsFromSpans(List<HSpan> spans)
    {
        var result = new List<SlotGeom>();
        var groups = new Dictionary<(int, int), List<HSpan>>();

        for (int i = 0; i < spans.Count; i++)
        {
            var s = spans[i];
            if (s.a.nodeRoot == null || s.b.nodeRoot == null) continue;

            if (!IsVRoot(s.a.nodeRoot) || !IsVRoot(s.b.nodeRoot))
                continue;

            int idA = s.a.nodeRoot.GetInstanceID();
            int idB = s.b.nodeRoot.GetInstanceID();

            int lo = Mathf.Min(idA, idB);
            int hi = Mathf.Max(idA, idB);
            var key = (lo, hi);

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<HSpan>();
                groups[key] = list;
            }
            list.Add(s);
        }

        foreach (var kv in groups)
        {
            var list = kv.Value;
            if (list == null || list.Count < 2) continue;

            list.Sort((a, b) => a.avgY.CompareTo(b.avgY));

            for (int i = 0; i < list.Count - 1; i++)
            {
                HSpan low = list[i];
                HSpan high = list[i + 1];

                float dy = Mathf.Abs(high.avgY - low.avgY);
                if (dy < minWallVerticalGap) continue;

                float dot = Mathf.Abs(Vector3.Dot(low.dir, high.dir));
                if (dot < parallelDotThreshold) continue;

                if (Mathf.Abs(low.length - high.length) > maxWallSpanLengthDelta) continue;

                if (!TryMatchByV(low, high, out Vector3 lowA, out Vector3 lowB, out Vector3 highA, out Vector3 highB))
                    continue;

                Vector3 widthDir = (lowB - lowA).normalized;
                float width = Vector3.Distance(lowA, lowB);
                float height = Vector3.Distance(lowA, highA);

                float qSize = Mathf.Max(0.0001f, nodeQuantize);
                width = Mathf.Round(width / qSize) * qSize;
                height = Mathf.Round(height / qSize) * qSize;

                if (width < minWallSlotSize || height < minWallSlotSize) continue;

                Vector3 normal = Vector3.Cross(widthDir, Vector3.up).normalized;
                if (normal.sqrMagnitude < 0.001f) continue;

                Vector3 center = (lowA + lowB + highA + highB) / 4f;

                // Stable ID from corners (prevents panels being destroyed when additional beams are placed).
                string id = BuildCornerHashId("WALL", lowA, lowB, highB, highA);

                result.Add(new SlotGeom
                {
                    id = id,
                    c0 = lowA,
                    c1 = lowB,
                    c2 = highB,
                    c3 = highA,
                    normal = normal,
                    upAxis = Vector3.up,
                    center = center,
                    sizeXY = new Vector2(width, height)
                });
            }
        }

        if (debug)
            Debug.Log($"[PanelSlot] Wall rectangles found={result.Count}");

        return result;
    }

    bool TryMatchByV(HSpan low, HSpan high, out Vector3 lowA, out Vector3 lowB, out Vector3 highA, out Vector3 highB)
    {
        Transform vA = low.a.nodeRoot;
        Transform vB = low.b.nodeRoot;

        lowA = low.a.point;
        lowB = low.b.point;

        highA = Vector3.zero;
        highB = Vector3.zero;

        bool gotA = false, gotB = false;

        if (high.a.nodeRoot == vA) { highA = high.a.point; gotA = true; }
        else if (high.b.nodeRoot == vA) { highA = high.b.point; gotA = true; }

        if (high.a.nodeRoot == vB) { highB = high.a.point; gotB = true; }
        else if (high.b.nodeRoot == vB) { highB = high.b.point; gotB = true; }

        if (!gotA || !gotB) return false;

        if (low.a.nodeRoot != vA)
        {
            var tmp = lowA; lowA = lowB; lowB = tmp;
        }

        return true;
    }

    // ---------------------------
    // FLOOR/ROOF SLOTS — robust grouping + stable corners + splitter rejection
    // ---------------------------
    List<SlotGeom> DetectFloorSlotsFromSpans(List<HSpan> spans)
    {
        var result = new List<SlotGeom>();

        // Cluster spans by quantized avgY bucket (deterministic).
        // This avoids drifting averages and boundary migration between clusters.
        float tolY = Mathf.Max(0.0001f, floorYBucketSize);

        var buckets = new Dictionary<int, List<HSpan>>();
        var bucketAvg = new Dictionary<int, float>();

        for (int i = 0; i < spans.Count; i++)
        {
            var s = spans[i];

            // Only consider spans that are basically horizontal (floor-ish).
            // If you later support sloped roofs, remove this filter.
            if (Mathf.Abs(s.dir.y) > maxFloorSpanDirY) continue;

            int bucket = Mathf.RoundToInt(s.avgY / tolY);
            if (!buckets.TryGetValue(bucket, out var list))
            {
                list = new List<HSpan>();
                buckets[bucket] = list;
                bucketAvg[bucket] = s.avgY;
            }
            list.Add(s);

            // Track average only for debug visibility (bucket membership is stable).
            bucketAvg[bucket] = (bucketAvg[bucket] * (list.Count - 1) + s.avgY) / list.Count;
        }

        // Make cluster ordering stable across scans.
        var bucketKeys = new List<int>(buckets.Keys);
        bucketKeys.Sort();

        if (debug)
            Debug.Log($"[PanelSlot] Floor buckets={bucketKeys.Count} (tolY={tolY:0.000})");
        if (debug)
        {
            for (int c = 0; c < bucketKeys.Count; c++)
            {
                int b = bucketKeys[c];
                Debug.Log($"[PanelSlot]  - Bucket {b}: spans={buckets[b].Count} avgY={bucketAvg[b]:F4}");
            }
        }

        for (int clusterIndex = 0; clusterIndex < bucketKeys.Count; clusterIndex++)
        {
            int bucketKey = bucketKeys[clusterIndex];
            var spansAtY = buckets[bucketKey];
            if (spansAtY == null || spansAtY.Count < 4) continue;

            // Node world positions (by corner key)
            var nodeWorld = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < spansAtY.Count; i++)
            {
                var s = spansAtY[i];
                if (!nodeWorld.ContainsKey(s.a.key)) nodeWorld[s.a.key] = s.a.point;
                if (!nodeWorld.ContainsKey(s.b.key)) nodeWorld[s.b.key] = s.b.point;
            }
            if (debug)
                Debug.Log($"[PanelSlot]  Cluster {clusterIndex}: nodes={nodeWorld.Count} spans={spansAtY.Count}");

            // Build undirected graph of corner keys
            var neighbors = new Dictionary<Vector3Int, List<Vector3Int>>();
            var edgeExists = new HashSet<(Vector3Int, Vector3Int)>();

            (Vector3Int, Vector3Int) NormEdge(Vector3Int u, Vector3Int v)
            {
                if (u.x != v.x) return (u.x < v.x) ? (u, v) : (v, u);
                if (u.y != v.y) return (u.y < v.y) ? (u, v) : (v, u);
                return (u.z < v.z) ? (u, v) : (v, u);
            }

            void AddNeighbor(Vector3Int u, Vector3Int v)
            {
                if (!neighbors.TryGetValue(u, out var nu)) { nu = new List<Vector3Int>(); neighbors[u] = nu; }
                if (!nu.Contains(v)) nu.Add(v);
            }

            for (int i = 0; i < spansAtY.Count; i++)
            {
                var s = spansAtY[i];
                if (SameKey(s.a.key, s.b.key)) continue;

                edgeExists.Add(NormEdge(s.a.key, s.b.key));
                AddNeighbor(s.a.key, s.b.key);
                AddNeighbor(s.b.key, s.a.key);
            }

            // --- Walked graph (shortcut edges) ---
            // Adding a beam connection along a boundary splits a side into multiple spans (intermediate nodes),
            // which breaks the simple 4-cycle. To keep the same slot stable, we create shortcut edges by walking
            // along collinear chains (even through branch nodes) to the farthest reachable node in that direction.
            var baseNeighbors = neighbors;
            var baseEdgeExists = edgeExists;

            var walkNeighbors = new Dictionary<Vector3Int, List<Vector3Int>>();
            var walkEdgeExists = new HashSet<(Vector3Int, Vector3Int)>();

            void AddWalkNeighbor(Vector3Int u, Vector3Int v)
            {
                if (!walkNeighbors.TryGetValue(u, out var nu)) { nu = new List<Vector3Int>(); walkNeighbors[u] = nu; }
                if (!nu.Contains(v)) nu.Add(v);
            }

            void AddWalkEdge(Vector3Int u, Vector3Int v)
            {
                if (SameKey(u, v)) return;
                var e = NormEdge(u, v);
                if (walkEdgeExists.Add(e))
                {
                    AddWalkNeighbor(u, v);
                    AddWalkNeighbor(v, u);
                }
            }

            // Seed walked graph with all original edges (never worse than base).
            foreach (var e in baseEdgeExists)
                AddWalkEdge(e.Item1, e.Item2);

            Vector3 FlatDir(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                d.y = 0f;
                return (d.sqrMagnitude < 1e-6f) ? Vector3.zero : d.normalized;
            }

            // Helper: find the farthest node from `start` in the direction of `next` by walking collinear spans.
            Vector3Int WalkFarthestCollinear(Vector3Int start, Vector3Int next)
            {
                if (!nodeWorld.TryGetValue(start, out var sPos)) return next;
                if (!nodeWorld.TryGetValue(next, out var nPos)) return next;

                Vector3 dir = FlatDir(sPos, nPos);
                if (dir == Vector3.zero) return next;

                Vector3Int prev = start;
                Vector3Int cur = next;
                Vector3 curDir = dir;

                // Walk until we can't continue in (approximately) the same direction.
                for (int steps = 0; steps < 64; steps++)
                {
                    if (!baseNeighbors.TryGetValue(cur, out var nCur) || nCur == null || nCur.Count == 0)
                        break;

                    Vector3Int bestNext = default;
                    bool hasBest = false;
                    float bestDot = -1f;

                    for (int i = 0; i < nCur.Count; i++)
                    {
                        var cand = nCur[i];
                        if (SameKey(cand, prev)) continue;
                        if (!nodeWorld.TryGetValue(cand, out var cPos)) continue;

                        Vector3 d = FlatDir(nodeWorld[cur], cPos);
                        if (d == Vector3.zero) continue;

                        float dot = Mathf.Abs(Vector3.Dot(curDir, d));
                        if (dot >= parallelDotThreshold && dot > bestDot)
                        {
                            bestDot = dot;
                            bestNext = cand;
                            hasBest = true;
                        }
                    }

                    if (!hasBest) break;

                    prev = cur;
                    cur = bestNext;

                    if (!nodeWorld.TryGetValue(prev, out var pPos) || !nodeWorld.TryGetValue(cur, out var curPos))
                        break;

                    Vector3 newDir = FlatDir(pPos, curPos);
                    if (newDir == Vector3.zero) break;
                    curDir = newDir;
                }

                return cur;
            }

            // Build shortcut edges: from each node, for each neighbor direction, connect to farthest collinear endpoint.
            // BUT: do NOT create shortcuts that skip over a branch node (degree >= 3), as that would
            // merge multiple adjacent rectangles into one big overlapping slot.
            foreach (var kvDir in baseNeighbors)
            {
                var u = kvDir.Key;
                var nu = kvDir.Value;
                if (nu == null) continue;

                for (int i = 0; i < nu.Count; i++)
                {
                    var v = nu[i];
                    // Only walk if v is a simple pass-through node (degree == 2)
                    if (!baseNeighbors.TryGetValue(v, out var nv) || nv == null || nv.Count != 2)
                    {
                        // v is a branch or endpoint, so stop here (do not skip over it)
                        AddWalkEdge(u, v);
                        continue;
                    }

                    var far = WalkFarthestCollinear(u, v);
                    AddWalkEdge(u, far);
                }
            }

            // Use walked graph for rectangle finding.
            neighbors = walkNeighbors;
            edgeExists = walkEdgeExists;

            bool HasEdge(Vector3Int u, Vector3Int v) => edgeExists.Contains(NormEdge(u, v));

            // Dedup using sorted key strings
            var seenRects = new HashSet<string>();

            // Deterministic floor-rectangle canonicalization.
            // Produces the same ordered corners + in-plane up axis for the same physical rectangle,
            // regardless of which corner was used as the starting node during discovery.
            void CanonicalizeFloorRectDeterministic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                                                    out Vector3 c0, out Vector3 c1, out Vector3 c2, out Vector3 c3,
                                                    out float width, out float height, out Vector3 upAxis)
            {
                string FVec(Vector3 v) => $"({v.x:F4},{v.y:F4},{v.z:F4})";

                Vector3[] pts = new Vector3[] { p0, p1, p2, p3 };

                // Flatten Y to remove drift
                float y = (p0.y + p1.y + p2.y + p3.y) * 0.25f;
                for (int i = 0; i < 4; i++) pts[i].y = y;

                // Build all pair distances in XZ
                var pairs = new List<(int i, int j, float len, Vector3 dir)>();
                for (int i = 0; i < 4; i++)
                {
                    for (int j = i + 1; j < 4; j++)
                    {
                        Vector3 d = pts[j] - pts[i];
                        d.y = 0f;
                        float len = d.magnitude;
                        if (len > 1e-5f)
                            pairs.Add((i, j, len, d / len));
                    }
                }
                pairs.Sort((a, b) => a.len.CompareTo(b.len));

                if (pairs.Count < 4)
                {
                    // Degenerate case - just pick stable ordering
                    Array.Sort(pts, (a, b) =>
                    {
                        if (Mathf.Abs(a.x - b.x) > 0.001f) return a.x.CompareTo(b.x);
                        return a.z.CompareTo(b.z);
                    });
                    c0 = pts[0]; c1 = pts[1]; c2 = pts[2]; c3 = pts[3];
                    width = Vector3.Distance(c0, c1);
                    height = Vector3.Distance(c0, c3);
                    upAxis = Vector3.forward;
                    if (debug)
                    {
                        Debug.Log(
                            $"[PanelSlot][CANON-FLOOR][DEGEN] p0={FVec(p0)} p1={FVec(p1)} p2={FVec(p2)} p3={FVec(p3)} " +
                            $"-> c0={FVec(c0)} c1={FVec(c1)} c2={FVec(c2)} c3={FVec(c3)} w={width:F4} h={height:F4}"
                        );
                    }

                    return;
                }

                // The 4 shortest pairs are the rectangle sides (two lengths, each repeated twice).
                float len1 = pairs[0].len;
                float len2 = len1;

                float lenBucketTol = Mathf.Max(0.02f, nodeQuantize * 3.0f);
                for (int k = 1; k < 4; k++)
                {
                    if (Mathf.Abs(pairs[k].len - len1) > lenBucketTol)
                    {
                        len2 = pairs[k].len;
                        break;
                    }
                }

                // Representative directions for each length bucket
                Vector3 dir1 = Vector3.zero;
                Vector3 dir2 = Vector3.zero;

                float dirPickTol = Mathf.Max(0.02f, nodeQuantize * 3.0f);
                for (int k = 0; k < 4; k++)
                {
                    if (dir1 == Vector3.zero && Mathf.Abs(pairs[k].len - len1) <= dirPickTol)
                        dir1 = pairs[k].dir;
                    else if (dir2 == Vector3.zero && Mathf.Abs(pairs[k].len - len2) <= dirPickTol)
                        dir2 = pairs[k].dir;
                }

                if (dir1 == Vector3.zero) dir1 = Vector3.right;
                if (dir2 == Vector3.zero) dir2 = Vector3.forward;

                // Ensure perpendicular
                dir2 = dir2 - dir1 * Vector3.Dot(dir2, dir1);
                if (dir2.sqrMagnitude < 1e-6f) dir2 = Vector3.Cross(Vector3.up, dir1);
                dir2.Normalize();

                // Choose width as the direction more aligned with world +X axis
                Vector3 widthDir, heightDir;
                float widthLen, heightLen;

                if (Mathf.Abs(Vector3.Dot(dir1, Vector3.right)) >= Mathf.Abs(Vector3.Dot(dir2, Vector3.right)))
                {
                    widthDir = dir1;
                    heightDir = dir2;
                    widthLen = len1;
                    heightLen = len2;
                }
                else
                {
                    widthDir = dir2;
                    heightDir = dir1;
                    widthLen = len2;
                    heightLen = len1;
                }

                // Force width direction to point toward +X (or +Z if ambiguous)
                if (Vector3.Dot(widthDir, Vector3.right) < -0.01f)
                    widthDir = -widthDir;
                else if (Mathf.Abs(Vector3.Dot(widthDir, Vector3.right)) < 0.1f && Vector3.Dot(widthDir, Vector3.forward) < -0.01f)
                    widthDir = -widthDir;

                // Make heightDir perpendicular and ensure right-handed with +Y normal
                heightDir = heightDir - widthDir * Vector3.Dot(heightDir, widthDir);
                if (heightDir.sqrMagnitude < 1e-6f) heightDir = Vector3.Cross(Vector3.up, widthDir);
                heightDir.Normalize();

                if (Vector3.Dot(Vector3.Cross(widthDir, heightDir), Vector3.up) < 0f)
                    heightDir = -heightDir;

                // Project points to (widthDir, heightDir) and pick corners by min/max
                float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
                float minV = float.PositiveInfinity, maxV = float.NegativeInfinity;

                float[] us = new float[4];
                float[] vs = new float[4];

                for (int i = 0; i < 4; i++)
                {
                    us[i] = Vector3.Dot(pts[i], widthDir);
                    vs[i] = Vector3.Dot(pts[i], heightDir);
                    if (us[i] < minU) minU = us[i];
                    if (us[i] > maxU) maxU = us[i];
                    if (vs[i] < minV) minV = vs[i];
                    if (vs[i] > maxV) maxV = vs[i];
                }

                int PickCorner(float targetU, float targetV)
                {
                    int best = 0;
                    float bestErr = float.PositiveInfinity;
                    for (int i = 0; i < 4; i++)
                    {
                        float err = Mathf.Abs(us[i] - targetU) + Mathf.Abs(vs[i] - targetV);
                        if (err < bestErr)
                        {
                            bestErr = err;
                            best = i;
                        }
                    }
                    return best;
                }

                int i00 = PickCorner(minU, minV);
                int i10 = PickCorner(maxU, minV);
                int i11 = PickCorner(maxU, maxV);
                int i01 = PickCorner(minU, maxV);

                // Assign corners: c0 = origin (minU, minV), c1 = +width, c3 = +height, c2 = opposite
                c0 = pts[i00];
                c1 = pts[i10];
                c2 = pts[i11];
                c3 = pts[i01];

                width = widthLen;
                height = heightLen;

                // KEY: upAxis is the true geometric height direction of the rectangle (in-plane), not derived from corner labels.
                upAxis = heightDir;

                if (debug)
                {
                    Debug.Log(
                        $"[PanelSlot][CANON-FLOOR] p0={FVec(p0)} p1={FVec(p1)} p2={FVec(p2)} p3={FVec(p3)} " +
                        $"-> c0={FVec(c0)} c1={FVec(c1)} c2={FVec(c2)} c3={FVec(c3)} w={width:F4} h={height:F4} up={FVec(upAxis)}"
                    );
                }
            }

            foreach (var kvN in neighbors)
            {
                var aKey = kvN.Key;
                if (!neighbors.TryGetValue(aKey, out var nA) || nA.Count < 2) continue;
                if (!nodeWorld.TryGetValue(aKey, out var aPos)) continue;

                for (int i = 0; i < nA.Count; i++)
                {
                    var bKey = nA[i];
                    if (!nodeWorld.TryGetValue(bKey, out var bPos)) continue;

                    Vector3 ab = FlatDir(aPos, bPos);
                    if (ab == Vector3.zero) continue;

                    for (int j = 0; j < nA.Count; j++)
                    {
                        if (j == i) continue;
                        var dKey = nA[j];
                        if (!nodeWorld.TryGetValue(dKey, out var dPos)) continue;

                        Vector3 ad = FlatDir(aPos, dPos);
                        if (ad == Vector3.zero) continue;

                        float perpDot = Mathf.Abs(Vector3.Dot(ab, ad));
                        if (perpDot > perpendicularDotMax) continue;

                        if (!neighbors.TryGetValue(bKey, out var nB) || !neighbors.TryGetValue(dKey, out var nD))
                            continue;

                        for (int k = 0; k < nB.Count; k++)
                        {
                            var cKey = nB[k];
                            if (SameKey(cKey, aKey)) continue;
                            if (!nD.Contains(cKey)) continue;

                            if (!HasEdge(bKey, cKey) || !HasEdge(dKey, cKey)) continue;
                            if (!nodeWorld.TryGetValue(cKey, out var cPos)) continue;

                            Vector3 dc = FlatDir(dPos, cPos);
                            Vector3 bc = FlatDir(bPos, cPos);
                            if (dc == Vector3.zero || bc == Vector3.zero) continue;

                            float par1 = Mathf.Abs(Vector3.Dot(ab, dc));
                            float par2 = Mathf.Abs(Vector3.Dot(ad, bc));
                            // if (par1 < parallelDotThreshold) continue;
                            // if (par2 < parallelDotThreshold) continue;

                            // stable corners
                            CanonicalizeFloorRectDeterministic(aPos, bPos, cPos, dPos,
                                out Vector3 c0, out Vector3 c1, out Vector3 c2, out Vector3 c3,
                                out float width, out float height, out Vector3 computedUpAxis);

                            // FLOOR rotation is forced deterministic in world (normal=+Y, upAxis=+Z).
                            // Therefore, also make FLOOR geometry/size axis-aligned so panel scaling matches rotation.
                            float yFlat = bucketAvg[bucketKey];

                            // Use quantized corner keys directly so equal grid spans always produce equal sizeXY.
                            int minXKey = Mathf.Min(Mathf.Min(aKey.x, bKey.x), Mathf.Min(cKey.x, dKey.x));
                            int maxXKey = Mathf.Max(Mathf.Max(aKey.x, bKey.x), Mathf.Max(cKey.x, dKey.x));
                            int minZKey = Mathf.Min(Mathf.Min(aKey.z, bKey.z), Mathf.Min(cKey.z, dKey.z));
                            int maxZKey = Mathf.Max(Mathf.Max(aKey.z, bKey.z), Mathf.Max(cKey.z, dKey.z));

                            float q = Mathf.Max(0.0001f, nodeQuantize);
                            float minX = minXKey * q;
                            float maxX = maxXKey * q;
                            float minZ = minZKey * q;
                            float maxZ = maxZKey * q;

                            c0 = new Vector3(minX, yFlat, minZ);
                            c1 = new Vector3(maxX, yFlat, minZ);
                            c2 = new Vector3(maxX, yFlat, maxZ);
                            c3 = new Vector3(minX, yFlat, maxZ);

                            width = Mathf.Abs(maxXKey - minXKey) * q;
                            height = Mathf.Abs(maxZKey - minZKey) * q;

                            if (debug)
                            {
                                Debug.Log(
                                    $"[PanelSlot][FLOOR-SIZE] bucket={bucketKey} keys=({aKey.x},{aKey.z})|({bKey.x},{bKey.z})|({cKey.x},{cKey.z})|({dKey.x},{dKey.z}) " +
                                    $"xKey=[{minXKey}..{maxXKey}] zKey=[{minZKey}..{maxZKey}] q={q:0.###} " +
                                    $"boundsX=[{minX:0.###}..{maxX:0.###}] boundsZ=[{minZ:0.###}..{maxZ:0.###}] sizeXY=({width:0.###},{height:0.###})"
                                );
                            }

                            if (width < minFloorSlotSize || height < minFloorSlotSize) continue;

                            string K(Vector3Int kk) => $"{kk.x},{kk.y},{kk.z}";
                            string[] parts = new string[]
                            {
                                K(aKey), K(bKey), K(cKey), K(dKey)
                            };
                            Array.Sort(parts);
                            string norm = string.Join("|", parts);
                            if (seenRects.Contains(norm)) continue;
                            seenRects.Add(norm);

                            // Reject the outer big rectangle when there is an internal splitter chord.
                            float eps2 = Mathf.Max(splitterEpsilonMin, nodeQuantize * splitterEpsilonScale);
                            if (HasSplitterChord(c0, c1, c3, width, height, spansAtY, eps2))
                                continue;

                            // For floor/roof rectangles we keep the normal perfectly vertical.
                            Vector3 normal2 = Vector3.up;

                            Vector3 center2 = (c0 + c1 + c2 + c3) * 0.25f;

                            // Stable ID from corners.
                            string id2 = BuildCornerHashId("FLOOR", c0, c1, c2, c3);

                            result.Add(new SlotGeom
                            {
                                id = id2,
                                c0 = c0,
                                c1 = c1,
                                c2 = c2,
                                c3 = c3,
                                normal = normal2,
                                upAxis = Vector3.forward,
                                center = center2,
                                sizeXY = new Vector2(width, height)
                            });
                        }
                    }
                }
            }
        }

        if (debug)
            Debug.Log($"[PanelSlot] Floor/roof rectangles found={result.Count}");

        return result;
    }



    // ---------------------------
    // Slot lifecycle
    // ---------------------------

    void ApplyPanelToSlot(GameObject panel, PanelSlotHandle slot, int side)
    {
        if (panel == null || slot == null) return;

        side = (side >= 0) ? 1 : -1;

        // Use slot trigger rotation when available (matches ghost basis)
        Quaternion rot = (slot.slotTrigger != null) ? slot.slotTrigger.rotation : Quaternion.LookRotation(slot.normal, slot.upAxis);
        Vector3 n = (slot.normal.sqrMagnitude > 1e-6f) ? slot.normal.normalized : (rot * Vector3.forward);

        float innerW = Mathf.Max(0.01f, slot.sizeXY.x - 1f * (frameThickness + panelInsetX));
        float innerH = Mathf.Max(0.01f, slot.sizeXY.y + 0.5f * (frameThickness + panelInsetY));

        Debug.Log($"1innerW={innerW} innerH={innerH}  slot.sizeXY={slot.sizeXY}  frameThick={frameThickness} insetX={panelInsetX} insetY={panelInsetY}");

        float offset = panelOutset + panelGap + (panelThickness * 0.5f);
        Vector3 pos = slot.center + n * (side > 0 ? offset : -offset);

        panel.transform.SetPositionAndRotation(pos, rot);
        panel.transform.localScale = new Vector3(innerW, innerH, panelThickness);

        var pi = panel.GetComponent<PanelInstance>();
        if (pi == null) pi = panel.AddComponent<PanelInstance>();
        pi.slotId = slot.slotId;
        pi.side = side;

        // Ensure preserved panels remain under panelsRoot
        if (panelsRoot != null && panel.transform.parent != panelsRoot)
            panel.transform.SetParent(panelsRoot, true);
    }

    void TryReattachExistingPanels(PanelSlotHandle slot)
    {
        if (slot == null || panelsRoot == null) return;

        // Find preserved panels that claim this slotId
        var pis = panelsRoot.GetComponentsInChildren<PanelInstance>(true);
        for (int i = 0; i < pis.Length; i++)
        {
            var pi = pis[i];
            if (pi == null) continue;
            if (!string.Equals(pi.slotId, slot.slotId, StringComparison.Ordinal)) continue;

            if (pi.side > 0)
            {
                slot.panelPlus = pi.gameObject;
                ApplyPanelToSlot(pi.gameObject, slot, 1);
            }
            else
            {
                slot.panelMinus = pi.gameObject;
                ApplyPanelToSlot(pi.gameObject, slot, -1);
            }
        }

        if (slot.blocker != null) slot.blocker.gameObject.SetActive(slot.HasAnyPanel());
    }
    void UpsertSlot(SlotGeom g)
    {
        if (string.IsNullOrEmpty(g.id)) return;

        _seen.Add(g.id);

        if (!_slots.TryGetValue(g.id, out var handle) || handle == null)
        {
            handle = CreateSlotHandle(g);
            _slots[g.id] = handle;
        }
        else
        {
            UpdateSlotHandle(handle, g);
        }
    }

    PanelSlotHandle CreateSlotHandle(SlotGeom g)
    {
        var root = new GameObject($"Slot_{g.id}");
        root.transform.SetParent(slotsRoot, false);

        var h = root.AddComponent<PanelSlotHandle>();
        h.slotId = g.id;

        var triggerGO = new GameObject("SlotTrigger");
        triggerGO.transform.SetParent(root.transform, false);
        triggerGO.layer = slotTriggerLayer;

        var triggerCol = triggerGO.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;

        var blockerGO = new GameObject("PanelBlocker");
        blockerGO.transform.SetParent(root.transform, false);
        blockerGO.layer = panelBlockerLayer;

        var blockerCol = blockerGO.AddComponent<BoxCollider>();
        blockerCol.isTrigger = false;
        blockerGO.SetActive(false);

        h.slotTrigger = triggerGO.transform;
        h.blocker = blockerGO.transform;

        UpdateSlotHandle(h, g);

        if (preservePanelsOnSlotLoss)
            TryReattachExistingPanels(h);

        return h;
    }

    void UpdateSlotHandle(PanelSlotHandle h, SlotGeom g)
    {
        h.corner0 = g.c0;
        h.corner1 = g.c1;
        h.corner2 = g.c2;
        h.corner3 = g.c3;

        h.normal = g.normal.sqrMagnitude > 1e-6f ? g.normal.normalized : Vector3.up;
        h.center = g.center;
        var minRectSize = GetMinRectSize(g.c0, g.c1, g.c2, g.c3);
        h.sizeXY = new Vector2(
            Mathf.Max(0.01f, minRectSize.x),
            Mathf.Max(0.01f, minRectSize.y)
        );
        h.upAxis = g.upAxis.sqrMagnitude > 1e-6f ? g.upAxis.normalized : Vector3.forward;

        // FLOOR/ROOF slots: keep rotation deterministic in-world to avoid flip/jitter and
        // prevent rotated floor trigger/blocker colliders from overlapping nearby wall slots.
        if (!string.IsNullOrEmpty(h.slotId) && h.slotId.StartsWith("FLOOR_", StringComparison.Ordinal))
        {
            h.normal = Vector3.up;
            h.upAxis = Vector3.forward;
        }

        Quaternion rot = Quaternion.LookRotation(h.normal, h.upAxis);

        float thickness = frameThickness + 0.10f;
        // Keep FLOOR slot triggers thinner to reduce overlap with nearby wall-slot triggers.
        if (!string.IsNullOrEmpty(h.slotId) && h.slotId.StartsWith("FLOOR_", StringComparison.Ordinal))
        {
            thickness = Mathf.Max(0.02f, panelThickness + 2f * (panelGap + panelOutset));
        }

        if (h.slotTrigger != null)
        {
            h.slotTrigger.SetPositionAndRotation(g.center, rot);
            var bc = h.slotTrigger.GetComponent<BoxCollider>();
            if (bc != null) bc.size = new Vector3(g.sizeXY.x, g.sizeXY.y, thickness);
        }

        if (h.blocker != null)
        {
            h.blocker.SetPositionAndRotation(g.center, rot);
            var bc = h.blocker.GetComponent<BoxCollider>();
            if (bc != null) bc.size = new Vector3(g.sizeXY.x, g.sizeXY.y, thickness);
        }

        if (h.blocker != null) h.blocker.gameObject.SetActive(h.HasAnyPanel());
    }

    static Vector2 GetMinRectSize(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
    {
        float w0 = Vector3.Distance(c0, c1);
        float w1 = Vector3.Distance(c3, c2);
        float h0 = Vector3.Distance(c0, c3);
        float h1 = Vector3.Distance(c1, c2);
        Debug.Log($"[PanelSlot][MIN-SIZE] c0={c0:F4} c1={c1:F4} c2={c2:F4} c3={c3:F4}");

        float minWidth = Mathf.Min(w0, w1);
        float minHeight = Mathf.Min(h0, h1);

        Debug.Log($"[PanelSlot][MIN-SIZE] w0={w0:F4} w1={w1:F4} h0={h0:F4} h1={h1:F4} -> minW={minWidth:F4} minH={minHeight:F4}");

        return new Vector2(minWidth, minHeight);
    }

    void RemoveUnseenSlots()
    {
        var toRemove = new List<string>();
        foreach (var kv in _slots)
        {
            if (!_seen.Contains(kv.Key))
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            string id = toRemove[i];
            if (!_slots.TryGetValue(id, out var h) || h == null) continue;

            if (!preservePanelsOnSlotLoss)
            {
                if (h.panelPlus != null) Destroy(h.panelPlus);
                if (h.panelMinus != null) Destroy(h.panelMinus);
            }
            else
            {
                // Preserve panels so temporary slot disappearance doesn't delete placed panels.
                // Keep them under panelsRoot and leave their PanelInstance.slotId intact for reattachment.
                if (h.panelPlus != null)
                {
                    if (panelsRoot != null && h.panelPlus.transform.parent != panelsRoot)
                        h.panelPlus.transform.SetParent(panelsRoot, true);
                    h.panelPlus = null;
                }
                if (h.panelMinus != null)
                {
                    if (panelsRoot != null && h.panelMinus.transform.parent != panelsRoot)
                        h.panelMinus.transform.SetParent(panelsRoot, true);
                    h.panelMinus = null;
                }
            }

            Destroy(h.gameObject);
            _slots.Remove(id);
        }
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        foreach (var kv in _slots)
        {
            var s = kv.Value;
            if (s == null) continue;

            Gizmos.DrawLine(s.corner0, s.corner1);
            Gizmos.DrawLine(s.corner1, s.corner2);
            Gizmos.DrawLine(s.corner2, s.corner3);
            Gizmos.DrawLine(s.corner3, s.corner0);

            Gizmos.DrawRay(s.center, s.normal * 0.3f);
        }
    }

    static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    // Splitter chord detector (your original)
    bool HasSplitterChord(Vector3 a, Vector3 b, Vector3 d, float width, float height, List<HSpan> spansAtY, float eps)
    {
        Vector3 U = (b - a); U.y = 0f; if (U.sqrMagnitude < 1e-6f) return false; U.Normalize();
        Vector3 V = (d - a); V.y = 0f; if (V.sqrMagnitude < 1e-6f) return false; V.Normalize();

        Vector2 ToUV(Vector3 p)
        {
            Vector3 ap = p - a;
            ap.y = 0f;
            return new Vector2(Vector3.Dot(ap, U), Vector3.Dot(ap, V));
        }

        int EdgeOf(Vector2 uv)
        {
            bool onLeft = Mathf.Abs(uv.x - 0f) <= eps && uv.y >= -eps && uv.y <= height + eps;
            bool onRight = Mathf.Abs(uv.x - width) <= eps && uv.y >= -eps && uv.y <= height + eps;
            bool onBottom = Mathf.Abs(uv.y - 0f) <= eps && uv.x >= -eps && uv.x <= width + eps;
            bool onTop = Mathf.Abs(uv.y - height) <= eps && uv.x >= -eps && uv.x <= width + eps;

            if (onLeft) return 1;
            if (onRight) return 2;
            if (onBottom) return 3;
            if (onTop) return 4;
            return 0;
        }

        for (int i = 0; i < spansAtY.Count; i++)
        {
            var s = spansAtY[i];

            Vector2 p0 = ToUV(s.a.point);
            Vector2 p1 = ToUV(s.b.point);

            int e0 = EdgeOf(p0);
            int e1 = EdgeOf(p1);
            if (e0 == 0 || e1 == 0) continue;

            if (e0 == e1) continue;

            bool leftRight = ((e0 == 1 && e1 == 2) || (e0 == 2 && e1 == 1));
            bool bottomTop = ((e0 == 3 && e1 == 4) || (e0 == 4 && e1 == 3));

            if (leftRight)
            {
                if (p0.y > eps && p0.y < height - eps && p1.y > eps && p1.y < height - eps)
                    return true;
            }
            else if (bottomTop)
            {
                if (p0.x > eps && p0.x < width - eps && p1.x > eps && p1.x < width - eps)
                    return true;
            }
        }

        return false;
    }
}