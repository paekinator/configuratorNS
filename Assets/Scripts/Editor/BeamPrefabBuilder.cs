using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the beam prefabs (V*/H*/T*) from the FBX models in Assets/Models.
///
/// It reproduces the convention used by the original OBJ-based prefabs:
///   - Prefab variant of the model, root named after the part id, on the Beam layer.
///   - Root gets a fitted BoxCollider + SelectableBeam.
///   - H beams: "AP_Peg_A" / "AP_Peg_B" at both ends and N holes per side named
///     "AP_Hole_A1 (i)" / "AP_Hole_B1 (i)" (N = the number in the part id, e.g. H23 = 23).
///   - V posts: N holes on each of the 4 side faces named "AP_SideK_i".
///   - HT* models (horizontal twist) become the T* prefabs, with twistEndId 0/1 on
///     the pegs. If no HT model exists a twist variant is synthesized from the H mesh.
///
/// The H/HT meshes have real peg geometry protruding past both ends. The builder
/// detects the peg length from the mesh cross-section profile and fits the collider,
/// hole pitch and peg attachment points to the beam BODY, so the peg mesh visually
/// sinks into the mating frame's hole instead of pushing the beam out.
///
/// Every attachment point is an empty child with an AttachmentPoint component and a
/// small trigger SphereCollider. Use the scene-view gizmos (green = peg, cyan = hole)
/// to verify the generated points line up with the mesh, then tweak the settings and
/// regenerate as often as you like - prefab GUIDs are preserved on overwrite.
///
/// If a prefab already exists at the target path it is updated in place: everything
/// in it is kept except the AttachmentPoint children, which are stripped and rebuilt.
/// Note this means hand-adjusted attachment point positions are also reset, so untick
/// parts you have already fine-tuned before regenerating.
/// </summary>
public class BeamPrefabBuilder : EditorWindow
{
    [Header("Folders")]
    string _modelsFolder = "Assets/Models";
    string _verticalFolder = "Assets/Prefabs/Vertical";
    string _horizontalFolder = "Assets/Prefabs/Horizontal";
    string _twistFolder = "Assets/Prefabs/TwistBeams";

    bool _generateTwistVariants = true;
    bool _updatePartDatabase = true;

    string _beamLayerName = "Beam";
    Material _selectionMaterial;
    PartDatabase _partDatabase;

    [Header("Attachment point layout (model-local units)")]
    float _apColliderRadius = 0.002f;
    float _holeFaceInset = 0.005f;
    float _pegEndOutset = 0.005f;

    [Tooltip("<= 0 means auto: pitch derived from length, cross-section and hole count.")]
    float _holePitchOverride = -1f;

    [Tooltip("Extra roll (degrees, about the beam length axis) applied to H/T meshes " +
             "after axis conversion. Set to 90 if the drill holes face up/down instead of sideways.")]
    float _hMeshRollDegrees = 0f;

    Vector2 _scroll;
    readonly List<ModelEntry> _models = new List<ModelEntry>();

    class ModelEntry
    {
        public GameObject asset;
        public string partId;
        public BeamKind kind;
        public int holeCount;
        public bool include = true;
    }

    [MenuItem("Tools/Configurator/Beam Prefab Builder")]
    static void Open()
    {
        var win = GetWindow<BeamPrefabBuilder>("Beam Prefab Builder");
        win.minSize = new Vector2(420, 500);
        win.AutoLoadDefaults();
        win.ScanModels();
    }

    void AutoLoadDefaults()
    {
        if (_selectionMaterial == null)
            _selectionMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MAT_Selected.mat");

        if (_partDatabase == null)
        {
            string guid = FindFirstAssetGuid("t:PartDatabase");
            if (!string.IsNullOrEmpty(guid))
                _partDatabase = AssetDatabase.LoadAssetAtPath<PartDatabase>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    static string FindFirstAssetGuid(string filter)
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        return guids.Length > 0 ? guids[0] : null;
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Generates beam prefabs from the FBX models. Part id comes from the file name: " +
            "V13 = vertical post with 13 holes per side, H9 = horizontal beam with 9 holes per side, " +
            "HT9 = horizontal twist (becomes the T9 prefab). " +
            "After generating, open a prefab and check the gizmos: green = peg, cyan = hole.",
            MessageType.Info);

        EditorGUILayout.LabelField("Folders", EditorStyles.boldLabel);
        _modelsFolder = EditorGUILayout.TextField("Models Folder", _modelsFolder);
        _verticalFolder = EditorGUILayout.TextField("Vertical Output", _verticalFolder);
        _horizontalFolder = EditorGUILayout.TextField("Horizontal Output", _horizontalFolder);
        _twistFolder = EditorGUILayout.TextField("Twist Output", _twistFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        _generateTwistVariants = EditorGUILayout.Toggle(
            new GUIContent("Synthesize Twist Variants",
                "Fallback only: builds a T* prefab from the plain H mesh for sizes " +
                "that have no dedicated HT model."),
            _generateTwistVariants);
        _updatePartDatabase = EditorGUILayout.Toggle("Update Part Database", _updatePartDatabase);
        _beamLayerName = EditorGUILayout.TextField("Beam Layer", _beamLayerName);
        _selectionMaterial = (Material)EditorGUILayout.ObjectField(
            "Selection Material", _selectionMaterial, typeof(Material), false);
        _partDatabase = (PartDatabase)EditorGUILayout.ObjectField(
            "Part Database", _partDatabase, typeof(PartDatabase), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attachment Point Layout (model-local units)", EditorStyles.boldLabel);
        _apColliderRadius = EditorGUILayout.FloatField(
            new GUIContent("AP Collider Radius", "Radius of the tiny trigger sphere on every attachment point."),
            _apColliderRadius);
        _holeFaceInset = EditorGUILayout.FloatField(
            new GUIContent("Hole Face Inset", "How far the hole APs sit inside the side faces."),
            _holeFaceInset);
        _pegEndOutset = EditorGUILayout.FloatField(
            new GUIContent("Peg End Outset", "How far the end pegs stick out past the mesh ends."),
            _pegEndOutset);
        _holePitchOverride = EditorGUILayout.FloatField(
            new GUIContent("Hole Pitch Override", "<= 0 derives the pitch from mesh length and hole count."),
            _holePitchOverride);
        _hMeshRollDegrees = EditorGUILayout.FloatField(
            new GUIContent("H Mesh Roll (deg)", "Extra roll about the length axis for H/T meshes. " +
                "Set to 90 if their drill holes come out facing up/down instead of sideways."),
            _hMeshRollDegrees);

        EditorGUILayout.Space();
        if (GUILayout.Button("Scan Models Folder"))
            ScanModels();

        if (_models.Count > 0)
        {
            EditorGUILayout.LabelField($"Detected Models ({_models.Count})", EditorStyles.boldLabel);
            foreach (ModelEntry entry in _models)
            {
                EditorGUILayout.BeginHorizontal();
                entry.include = EditorGUILayout.ToggleLeft(
                    $"{entry.partId}  ({entry.kind}, {entry.holeCount} holes/side)",
                    entry.include);
                EditorGUILayout.ObjectField(entry.asset, typeof(GameObject), false, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            GUI.enabled = _models.Exists(m => m.include);
            if (GUILayout.Button("Generate Prefabs", GUILayout.Height(32)))
                GenerateAll();
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox("No V*/H*/HT* FBX models found. Check the models folder and press Scan.", MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    void ScanModels()
    {
        _models.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { _modelsFolder });
        var pattern = new Regex(@"^(HT|V|H)(\d+)$", RegexOptions.IgnoreCase);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            Match match = pattern.Match(name);
            if (!match.Success)
                continue;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                continue;

            string prefix = match.Groups[1].Value.ToUpperInvariant();
            string digits = match.Groups[2].Value;

            BeamKind kind = prefix == "V" ? BeamKind.V
                : prefix == "HT" ? BeamKind.TwistH
                : BeamKind.H;

            // HT models produce the T* prefabs (existing runtime twist convention).
            string partId = kind == BeamKind.TwistH ? "T" + digits : prefix + digits;

            _models.Add(new ModelEntry
            {
                asset = asset,
                partId = partId,
                kind = kind,
                holeCount = int.Parse(digits)
            });
        }

        _models.Sort((a, b) =>
        {
            int kindCmp = a.kind.CompareTo(b.kind);
            return kindCmp != 0 ? kindCmp : a.holeCount.CompareTo(b.holeCount);
        });
    }

    void GenerateAll()
    {
        int beamLayer = LayerMask.NameToLayer(_beamLayerName);
        if (beamLayer < 0)
        {
            EditorUtility.DisplayDialog("Beam Prefab Builder",
                $"Layer \"{_beamLayerName}\" does not exist in this project.", "OK");
            return;
        }

        EnsureFolder(_verticalFolder);
        EnsureFolder(_horizontalFolder);
        if (_generateTwistVariants)
            EnsureFolder(_twistFolder);

        var generated = new List<(string partId, GameObject prefab)>();

        // Sizes that have a dedicated HT (twist) model don't need a twist
        // variant synthesized from the plain H mesh.
        var twistModelSizes = new HashSet<int>();
        foreach (ModelEntry entry in _models)
        {
            if (entry.include && entry.kind == BeamKind.TwistH)
                twistModelSizes.Add(entry.holeCount);
        }

        try
        {
            int index = 0;
            foreach (ModelEntry entry in _models)
            {
                if (!entry.include)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Beam Prefab Builder", $"Generating {entry.partId}...", (float)index++ / _models.Count);

                if (entry.kind == BeamKind.V)
                {
                    GameObject prefab = BuildPrefab(entry, BeamKind.V, entry.partId,
                        $"{_verticalFolder}/{entry.partId}.prefab", beamLayer);
                    if (prefab != null)
                        generated.Add((entry.partId, prefab));
                }
                else if (entry.kind == BeamKind.TwistH)
                {
                    GameObject prefab = BuildPrefab(entry, BeamKind.TwistH, entry.partId,
                        $"{_twistFolder}/{entry.partId}.prefab", beamLayer);
                    if (prefab != null)
                        generated.Add((entry.partId, prefab));
                }
                else
                {
                    GameObject prefab = BuildPrefab(entry, BeamKind.H, entry.partId,
                        $"{_horizontalFolder}/{entry.partId}.prefab", beamLayer);
                    if (prefab != null)
                        generated.Add((entry.partId, prefab));

                    if (_generateTwistVariants && !twistModelSizes.Contains(entry.holeCount))
                    {
                        string twistId = "T" + entry.partId.Substring(1);
                        GameObject twist = BuildPrefab(entry, BeamKind.TwistH, twistId,
                            $"{_twistFolder}/{twistId}.prefab", beamLayer);
                        if (twist != null)
                            generated.Add((twistId, twist));
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (_updatePartDatabase && _partDatabase != null)
            UpdatePartDatabase(generated);

        AssetDatabase.SaveAssets();
        Debug.Log($"[BeamPrefabBuilder] Generated {generated.Count} prefabs.");
    }

    GameObject BuildPrefab(ModelEntry entry, BeamKind kind, string partId, string prefabPath, int beamLayer)
    {
        // Prefer the existing prefab at the target path as the starting point so any
        // manual edits (materials, extra children, etc.) survive regeneration.
        // Only the AttachmentPoint children are stripped and rebuilt.
        GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // If the existing prefab was built from a different model (e.g. old T*
        // prefabs that reused the H mesh before the HT models existed), start
        // fresh from the model so the correct mesh is picked up. Saving over the
        // same path still preserves the GUID.
        if (baseAsset != null && !PrefabUsesModelMeshes(baseAsset, entry.asset))
        {
            Debug.Log($"[BeamPrefabBuilder] {partId}: existing prefab uses a different mesh source, " +
                      $"rebuilding from {entry.asset.name}.");
            baseAsset = null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset != null ? baseAsset : entry.asset);
        if (instance == null)
        {
            Debug.LogError($"[BeamPrefabBuilder] Could not instantiate model for {partId}.");
            return null;
        }

        try
        {
            if (baseAsset != null)
            {
                // Unpack the variant layer so previously generated attachment points
                // (which live inside the prefab) can be destroyed and rebuilt.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                foreach (AttachmentPoint ap in instance.GetComponentsInChildren<AttachmentPoint>(true))
                    UnityEngine.Object.DestroyImmediate(ap.gameObject);

                // Undo any axis conversion from a previous run so it is never applied twice.
                ResetChildTransformsToSource(instance.transform, entry.asset.transform);
            }

            instance.name = partId;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetLayerRecursively(instance.transform, beamLayer);

            if (!TryGetRootLocalBounds(instance.transform, out Bounds bounds))
            {
                Debug.LogError($"[BeamPrefabBuilder] {partId}: model has no renderers, skipped.");
                return null;
            }

            // The placement code, host-face mapping and all scene-tuned rotations were
            // built for the original OBJ prefabs, whose beams ran along local +Z with the
            // cross-section on X/Y. The new FBX models are Y-long (V) / X-long (H), so
            // rotate the mesh child nodes to conform to that legacy convention.
            ConformMeshToLegacyAxes(instance.transform, ref bounds, kind, partId);

            // Legacy convention: length along Z, cross-section on X/Y.
            const int lengthAxis = 2, crossA = 0, crossB = 1;

            // The H/HT meshes have real peg geometry protruding past both ends.
            // Everything (collider, hole pitch, peg APs) is laid out on the beam
            // BODY so a mated beam sits flush and its peg sinks into the hole.
            Bounds bodyBounds = MeasureBodyBounds(instance.transform, bounds, out float pegLenA, out float pegLenB);

            if (kind == BeamKind.V)
                AddVerticalAttachmentPoints(instance.transform, bodyBounds, lengthAxis, crossA, crossB, entry.holeCount, beamLayer);
            else
                AddHorizontalAttachmentPoints(instance.transform, bodyBounds, lengthAxis, crossA, crossB, entry.holeCount, kind, beamLayer);

            // Root collider + selectable. The collider covers the body only, so a
            // peg inserted into a neighbor doesn't register as a deep overlap.
            var box = instance.GetComponent<BoxCollider>();
            if (box == null) box = instance.AddComponent<BoxCollider>();
            box.center = bodyBounds.center;
            box.size = bodyBounds.size;

            var selectable = instance.GetComponent<SelectableBeam>();
            if (selectable == null) selectable = instance.AddComponent<SelectableBeam>();
            if (_selectionMaterial != null)
                selectable.selectionMaterial = _selectionMaterial;

            // Overwriting an existing .prefab preserves its GUID, so references stay valid.
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool success);
            if (!success)
            {
                Debug.LogError($"[BeamPrefabBuilder] Failed to save prefab {prefabPath}");
                return null;
            }

            Debug.Log($"[BeamPrefabBuilder] {partId}: saved {prefabPath} " +
                      $"(length axis {AxisName(lengthAxis)}, body size {bodyBounds.size:F4}, " +
                      $"peg protrusion A={pegLenA:F4} B={pegLenB:F4})");
            return prefab;
        }
        finally
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Restores the local transforms of the root's direct children to the values in the
    /// source model asset (matched by name), reverting any previous axis conversion.
    /// </summary>
    static void ResetChildTransformsToSource(Transform root, Transform sourceRoot)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform source = sourceRoot.Find(child.name);
            if (source == null)
                continue;

            child.localPosition = source.localPosition;
            child.localRotation = source.localRotation;
            child.localScale = source.localScale;
        }
    }

    /// <summary>
    /// Rotates the direct children of the prefab root (the FBX node transforms) so the
    /// beam's length runs along local +Z, matching the original OBJ prefab convention.
    /// No-op when the mesh is already Z-long (e.g. on regeneration) or near-cubic (V1/H1).
    /// </summary>
    void ConformMeshToLegacyAxes(Transform root, ref Bounds bounds, BeamKind kind, string partId)
    {
        GetAxes(bounds, out int lengthAxis, out int crossA, out int crossB);

        bool elongated = bounds.size[lengthAxis] > Mathf.Max(bounds.size[crossA], bounds.size[crossB]) * 1.1f;

        // Near-cubic parts (V1/H1) have no measurable length axis, so fall back to the
        // FBX authoring convention: V models are Y-long, H models are X-long.
        int sourceAxis = elongated ? lengthAxis : (kind == BeamKind.V ? 1 : 0);

        Quaternion conform = Quaternion.identity;
        bool rotate = false;

        if (sourceAxis != 2)
        {
            conform = sourceAxis == 1
                ? Quaternion.Euler(90f, 0f, 0f)    // +Y -> +Z (vertical posts)
                : Quaternion.Euler(0f, -90f, 0f);  // +X -> +Z (horizontal beams)
            rotate = true;
        }

        if (kind != BeamKind.V && Mathf.Abs(_hMeshRollDegrees) > 0.01f)
        {
            conform = Quaternion.Euler(0f, 0f, _hMeshRollDegrees) * conform;
            rotate = true;
        }

        if (!rotate)
            return;

        if (root.childCount == 0)
        {
            Debug.LogWarning($"[BeamPrefabBuilder] {partId}: mesh sits on the prefab root, " +
                             "cannot axis-convert. Attachment points may be misaligned.");
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            child.localPosition = conform * child.localPosition;
            child.localRotation = conform * child.localRotation;
        }

        TryGetRootLocalBounds(root, out bounds);

        GetAxes(bounds, out lengthAxis, out _, out _);
        if (elongated && lengthAxis != 2)
            Debug.LogWarning($"[BeamPrefabBuilder] {partId}: mesh is still not Z-long after axis conversion.");
    }

    // ------------------------------------------------------------------
    // Attachment point layout
    // ------------------------------------------------------------------

    void AddVerticalAttachmentPoints(
        Transform root, Bounds bounds, int lengthAxis, int crossA, int crossB, int holeCount, int layer)
    {
        float length = bounds.size[lengthAxis];
        float cross = Mathf.Min(bounds.size[crossA], bounds.size[crossB]);

        // Old convention: first/last hole sit half a cross-section in from the ends,
        // which produces the module pitch naturally (e.g. 0.88 for the original parts).
        float endMargin = cross * 0.5f;
        float pitch = _holePitchOverride > 0f
            ? _holePitchOverride
            : (holeCount > 1 ? (length - cross) / (holeCount - 1) : 0f);

        Vector3 axisDir = AxisVector(lengthAxis);
        float start = bounds.min[lengthAxis] + endMargin;

        // Four side faces: +A, -A, +B, -B (matches AP_Side1..4 host mapping in BuildController).
        var sides = new (int sideIndex, int axis, float sign)[]
        {
            (1, crossA, +1f),
            (2, crossB, +1f),
            (3, crossB, -1f),
            (4, crossA, -1f)
        };

        foreach ((int sideIndex, int axis, float sign) in sides)
        {
            Vector3 outward = AxisVector(axis) * sign;
            float faceOffset = bounds.size[axis] * 0.5f - _holeFaceInset;

            for (int i = 0; i < holeCount; i++)
            {
                float along = holeCount > 1 ? start + pitch * i : bounds.center[lengthAxis];

                Vector3 pos = bounds.center;
                pos[lengthAxis] = along;
                pos[axis] = bounds.center[axis] + sign * faceOffset;

                CreateAttachmentPoint(
                    root,
                    $"AP_Side{sideIndex}_{i + 1:00}",
                    pos,
                    Quaternion.LookRotation(outward, axisDir),
                    AttachmentPoint.PointRole.Hole,
                    BeamKind.V,
                    layer);
            }
        }
    }

    void AddHorizontalAttachmentPoints(
        Transform root, Bounds bounds, int lengthAxis, int crossA, int crossB, int holeCount, BeamKind kind, int layer)
    {
        float length = bounds.size[lengthAxis];
        float cross = Mathf.Min(bounds.size[crossA], bounds.size[crossB]);

        // Old convention: N holes centered along the beam with pitch (L + w) / (N + 1)
        // (measured 0.88 module pitch on the original parts).
        float pitch = _holePitchOverride > 0f
            ? _holePitchOverride
            : (length + cross) / (holeCount + 1);
        float firstOffset = (length - pitch * (holeCount - 1)) * 0.5f;

        Vector3 lengthDir = AxisVector(lengthAxis);

        // Holes go through the sides along crossA; up direction is crossB.
        Vector3 sideDir = AxisVector(crossA);
        Vector3 upDir = AxisVector(crossB);
        float faceOffset = bounds.size[crossA] * 0.5f - _holeFaceInset;

        for (int i = 0; i < holeCount; i++)
        {
            float along = bounds.min[lengthAxis] + firstOffset + pitch * i;

            Vector3 posA = bounds.center;
            posA[lengthAxis] = along;
            posA += sideDir * -faceOffset;

            Vector3 posB = bounds.center;
            posB[lengthAxis] = along;
            posB += sideDir * faceOffset;

            CreateAttachmentPoint(root, $"AP_Hole_A1 ({i + 1})", posA,
                Quaternion.LookRotation(-sideDir, upDir),
                AttachmentPoint.PointRole.Hole, kind, layer);

            CreateAttachmentPoint(root, $"AP_Hole_B1 ({i + 1})", posB,
                Quaternion.LookRotation(sideDir, upDir),
                AttachmentPoint.PointRole.Hole, kind, layer);
        }

        // End pegs (V posts stack onto these; other H beams hook onto them too).
        Vector3 pegA = bounds.center;
        pegA[lengthAxis] = bounds.max[lengthAxis] + _pegEndOutset;
        Vector3 pegB = bounds.center;
        pegB[lengthAxis] = bounds.min[lengthAxis] - _pegEndOutset;

        bool twist = kind == BeamKind.TwistH;
        CreateAttachmentPoint(root, "AP_Peg_A", pegA,
            Quaternion.LookRotation(lengthDir, upDir),
            AttachmentPoint.PointRole.Peg, kind, layer, twist ? 0 : -1);
        CreateAttachmentPoint(root, "AP_Peg_B", pegB,
            Quaternion.LookRotation(-lengthDir, upDir),
            AttachmentPoint.PointRole.Peg, kind, layer, twist ? 1 : -1);
    }

    void CreateAttachmentPoint(
        Transform root, string name, Vector3 localPos, Quaternion localRot,
        AttachmentPoint.PointRole role, BeamKind ownerKind, int layer, int twistEndId = -1)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        var ap = go.AddComponent<AttachmentPoint>();
        ap.role = role;
        ap.ownerBeamKind = ownerKind;
        ap.twistEndId = twistEndId;

        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.0001f, _apColliderRadius);
    }

    // ------------------------------------------------------------------
    // Part database
    // ------------------------------------------------------------------

    void UpdatePartDatabase(List<(string partId, GameObject prefab)> generated)
    {
        Undo.RecordObject(_partDatabase, "Update Part Database");

        // Drop entries whose prefab reference is gone AND that we just regenerated,
        // plus duplicate part ids (the runtime cache warns about those).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _partDatabase.parts.RemoveAll(p =>
        {
            if (p == null) return true;
            string key = p.partId != null ? p.partId.Trim() : string.Empty;
            if (key.Length == 0) return true;
            if (!seen.Add(key)) return true;                      // duplicate id
            if (p.realPrefab == null && p.ghostPrefab == null) return true; // dead reference
            return false;
        });

        foreach ((string partId, GameObject prefab) in generated)
        {
            PartDatabase.PartEntry entry = _partDatabase.parts.Find(p =>
                p != null && string.Equals(p.partId?.Trim(), partId, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                entry = new PartDatabase.PartEntry { partId = partId };
                _partDatabase.parts.Add(entry);
            }

            entry.realPrefab = prefab;

            // Ghost prefabs are optional; the runtime falls back to the real prefab.
            if (entry.ghostPrefab == null || entry.ghostPrefab == entry.realPrefab)
                entry.ghostPrefab = null;
        }

        _partDatabase.parts.Sort((a, b) =>
            string.Compare(a?.partId, b?.partId, StringComparison.OrdinalIgnoreCase));

        _partDatabase.RebuildCache();
        EditorUtility.SetDirty(_partDatabase);
        Debug.Log($"[BeamPrefabBuilder] Part database updated with {generated.Count} entries.");
    }

    // ------------------------------------------------------------------
    // Utilities
    // ------------------------------------------------------------------

    /// <summary>
    /// Trims the full mesh bounds down to the beam body by ignoring the end peg
    /// protrusions. Body vertices reach the full cross-section (the corner edges);
    /// peg vertices have a clearly smaller cross-section, so anything below 75% of
    /// the full cross half-extent is treated as peg/detail geometry.
    /// Assumes the mesh is already conformed to the legacy Z-long convention.
    /// </summary>
    static Bounds MeasureBodyBounds(Transform root, Bounds fullBounds, out float pegLenA, out float pegLenB)
    {
        const int lengthAxis = 2, crossA = 0, crossB = 1;

        float threshold = 0.75f * Mathf.Max(fullBounds.extents[crossA], fullBounds.extents[crossB]);
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null)
                continue;

            Matrix4x4 toRoot = worldToRoot * meshFilter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(vertex);
                float ca = Mathf.Abs(p[crossA] - fullBounds.center[crossA]);
                float cb = Mathf.Abs(p[crossB] - fullBounds.center[crossB]);
                if (Mathf.Max(ca, cb) < threshold)
                    continue;

                min = Mathf.Min(min, p[lengthAxis]);
                max = Mathf.Max(max, p[lengthAxis]);
            }
        }

        if (min >= max)
        {
            pegLenA = 0f;
            pegLenB = 0f;
            return fullBounds;
        }

        pegLenA = Mathf.Max(0f, fullBounds.max[lengthAxis] - max);
        pegLenB = Mathf.Max(0f, min - fullBounds.min[lengthAxis]);

        Bounds body = fullBounds;
        Vector3 center = body.center;
        Vector3 size = body.size;
        center[lengthAxis] = (min + max) * 0.5f;
        size[lengthAxis] = max - min;
        body.center = center;
        body.size = size;
        return body;
    }

    /// <summary>True if any mesh in the prefab comes from the given model asset.</summary>
    static bool PrefabUsesModelMeshes(GameObject prefab, GameObject modelAsset)
    {
        string modelPath = AssetDatabase.GetAssetPath(modelAsset);
        foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh != null &&
                AssetDatabase.GetAssetPath(meshFilter.sharedMesh) == modelPath)
                return true;
        }

        return false;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    static void GetAxes(Bounds bounds, out int lengthAxis, out int crossA, out int crossB)
    {
        Vector3 size = bounds.size;
        lengthAxis = 0;
        if (size.y > size[lengthAxis]) lengthAxis = 1;
        if (size.z > size[lengthAxis]) lengthAxis = 2;

        crossA = (lengthAxis + 1) % 3;
        crossB = (lengthAxis + 2) % 3;

        // Keep a deterministic order: crossA is the smaller axis index.
        if (crossA > crossB)
            (crossA, crossB) = (crossB, crossA);
    }

    static Vector3 AxisVector(int axis)
    {
        switch (axis)
        {
            case 0: return Vector3.right;
            case 1: return Vector3.up;
            default: return Vector3.forward;
        }
    }

    static string AxisName(int axis) => axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

    static bool TryGetRootLocalBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool initialized = false;
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;

            Bounds local = renderer.localBounds;
            Matrix4x4 toRoot = worldToRoot * renderer.transform.localToWorldMatrix;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(sx, sy, sz));
                Vector3 p = toRoot.MultiplyPoint3x4(corner);

                if (!initialized)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        return initialized;
    }
}
