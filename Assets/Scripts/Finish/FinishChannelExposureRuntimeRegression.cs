#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Run after FinishRuntimeRegression in the isolated play-mode validation scene.
/// Checks real H/T and PanelMesh prefabs plus generated FBX mesh bounds; no
/// planner/masking implementation is used to decide expected channel coverage.
/// The existing transverse V-post shelf suite remains independently required.
/// </summary>
public static class FinishChannelExposureRuntimeRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            failure("Channel exposure runtime: " + label);
        }

        var build = Object.FindFirstObjectByType<BuildController>();
        var history = Object.FindFirstObjectByType<BuildHistory>();
        var finish = FinishController.Ensure();
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PanelMesh.prefab");
        Check(build != null && history != null && panelPrefab != null,
            "real configurator scene and PanelMesh prefab must be available");
        if (build == null || history == null || panelPrefab == null) yield break;

        bool previousSuspended = BuildHistory.Suspended;
        GameObject frameObject = null;
        var boards = new List<GameObject>();
        try
        {
            BuildHistory.Suspended = false;
            SpacePlanningUI.Hide();
            if (SpaceModeController.Active) Object.FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
            Object.FindFirstObjectByType<TemplateSession>()?.SetTool(GuidedTemplateTool.None);
            build.SetCurrentPart(null);
            finish.SetOn(false, announce: false);
            history.ClearAll();
            yield return WaitForPoll();

            foreach (string id in new[] { "H7", "H23", "T7" })
            foreach (float yaw in new[] { 0f, 37f })
            {
                string prefix = id + " at " + yaw + " degrees: ";
                Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(build.h3RotationEuler);
                var placed = build.PlacePartsBatch(new List<TemplatePartPose>
                {
                    new TemplatePartPose(id, Vector3.up * NeospaceUnits.Mm(800f), rotation, "channel-exposure-runtime")
                }, seatVerticalsOnFloor: false, validateOverlap: false);
                Check(placed.Placed == 1 && placed.Instances != null && placed.Instances.Count == 1,
                    prefix + "actual beam prefab places exactly once");
                if (placed.Instances == null || placed.Instances.Count != 1) yield break;
                frameObject = placed.Instances[0];
                Check(FrameOverlapResolver.TryBuildRecord(frameObject.transform, out var frame),
                    prefix + "actual frame record must exist");
                Vector3 inward = Vector3.Cross(frame.LengthAxis, Vector3.up).normalized;
                Check(Mathf.Abs(Vector3.Dot(frame.LengthAxis, Vector3.up)) < 0.001f &&
                      Mathf.Abs(Vector3.Dot(frame.LocalY.normalized, Vector3.up)) > 0.999f,
                    prefix + "beam must lie horizontally with exposed top and bottom channels");
                finish.SetOn(true, announce: false);
                yield return WaitForPoll();

                var bare = Measure(frame, inward);
                Check(FullCoverage(bare, 0, frame.Size) && FullCoverage(bare, 1, frame.Size) &&
                      FullCoverage(bare, 2, frame.Size) && FullCoverage(bare, 3, frame.Size),
                    prefix + "bare beam has all four complete channels using actual FBX lengths");
                Check(bare.Count > 0 && bare.TrueForAll(p => p.IsActualFbx),
                    prefix + "channel dressing comes from the supplied finish FBX meshes");

                foreach (int side in new[] { 1, -1, 0 })
                {
                    string state = side > 0 ? "upper board" : side < 0 ? "lower board" : "both boards";
                    if (side >= 0) boards.Add(MakeBoard(panelPrefab, frame, inward, 1));
                    if (side <= 0) boards.Add(MakeBoard(panelPrefab, frame, inward, -1));
                    yield return WaitForPoll(); // Exercise the live polling path, without manual regeneration.
                    bool exactBoards = true;
                    foreach (GameObject board in boards)
                    {
                        Bounds mesh = board.GetComponent<MeshFilter>().sharedMesh.bounds;
                        float thickness = NeospaceUnits.ToMm(mesh.size.z * Mathf.Abs(board.transform.lossyScale.z));
                        float offset = NeospaceUnits.ToMm(Vector3.Dot(board.GetComponent<Renderer>().bounds.center - frame.Center, Vector3.up));
                        exactBoards &= Mathf.Abs(thickness - 1f) < 0.01f && Mathf.Abs(Mathf.Abs(offset) - 20.5f) < 0.01f;
                    }
                    Check(exactBoards, prefix + state + "uses actual 1 mm PanelMesh at +/-20.5 mm");

                    var covered = Measure(frame, inward);
                    Check(Signature(covered, 0) == Signature(bare, 0), prefix + state + "preserves the full rendered top channel");
                    Check(Signature(covered, 1) == Signature(bare, 1), prefix + state + "preserves the full rendered bottom channel");
                    Check(Signature(covered, 3) == Signature(bare, 3), prefix + state + "preserves the outward rendered channel");
                    Check(Signature(covered, 2).Length == 0, prefix + state + "hides the inward panel-facing rendered channel");
                    if (id == "H7" && yaw == 0f && side == 0)
                    {
                        Capture("h7-paired-panels-above.png", frameObject, boards, frame, inward, 1f);
                        Capture("h7-paired-panels-below.png", frameObject, boards, frame, inward, -1f);
                    }
                    foreach (GameObject board in boards) Object.Destroy(board);
                    boards.Clear();
                    yield return WaitForPoll();
                    var restored = Measure(frame, inward);
                    Check(Signature(restored, 0) == Signature(bare, 0) && Signature(restored, 1) == Signature(bare, 1) &&
                          Signature(restored, 2) == Signature(bare, 2) && Signature(restored, 3) == Signature(bare, 3),
                        prefix + state + "deletion restores all four exact rendered channel runs");
                }
                finish.SetOn(false, announce: false);
                Object.Destroy(frameObject);
                frameObject = null;
                yield return WaitForPoll();
            }
        }
        finally
        {
            finish.SetOn(false, announce: false);
            foreach (GameObject board in boards) if (board != null) Object.Destroy(board);
            if (frameObject != null) Object.Destroy(frameObject);
            BuildHistory.Suspended = previousSuspended;
            Debug.Log($"Channel exposure runtime regression: {checks} checks, {failed} failures.");
        }
        yield return null;
    }

    static GameObject MakeBoard(GameObject prefab, FrameOverlapResolver.FrameRecord frame, Vector3 inward, int side)
    {
        var board = Object.Instantiate(prefab);
        board.name = "ChannelExposurePanel";
        board.transform.SetPositionAndRotation(
            frame.Center + inward * NeospaceUnits.Mm(352f) + Vector3.up * NeospaceUnits.Mm(side * 20.5f),
            Quaternion.LookRotation(Vector3.up, inward));
        board.transform.localScale = NeospaceUnits.Mm(1f) * new Vector3(PanelFill.EdgeMm(frame.Size), PanelFill.EdgeMm(7), 1f);
        if (board.GetComponent<PanelInstance>() == null) board.AddComponent<PanelInstance>();
        foreach (Collider collider in board.GetComponentsInChildren<Collider>()) collider.enabled = false;
        return board;
    }

    struct Plate
    {
        public string Model;
        public int Face;
        public float OffsetMm, LengthMm;
        public bool IsActualFbx;
    }

    static List<Plate> Measure(FrameOverlapResolver.FrameRecord frame, Vector3 inward)
    {
        var plates = new List<Plate>();
        GameObject root = GameObject.Find("FinishRoot");
        if (root == null) return plates;
        foreach (Transform part in root.transform)
        {
            if (!part.gameObject.activeInHierarchy ||
                (part.name != "Cap Side" && !part.name.StartsWith("Veneer H", StringComparison.Ordinal))) continue;
            bool any = false, actualFbx = true;
            Vector3 min = Vector3.one * float.PositiveInfinity;
            Vector3 max = Vector3.one * float.NegativeInfinity;
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                actualFbx &= path.StartsWith("Assets/Resources/Finish/", StringComparison.OrdinalIgnoreCase) &&
                             path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
                Bounds b = filter.sharedMesh.bounds;
                // Transform the eight mesh-local bound corners directly into
                // the beam basis. World AABBs would inflate rotated run lengths.
                for (int i = 0; i < 8; i++)
                {
                    Vector3 local = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 delta = filter.transform.TransformPoint(local) - frame.Center;
                    Vector3 projected = new Vector3(Vector3.Dot(delta, frame.LengthAxis), delta.y, Vector3.Dot(delta, inward));
                    min = Vector3.Min(min, projected);
                    max = Vector3.Max(max, projected);
                }
                any = true;
            }
            if (!any) continue;
            Vector3 center = (min + max) * 0.5f;
            int face = Mathf.Abs(center.y) >= Mathf.Abs(center.z) ? (center.y > 0f ? 0 : 1) : (center.z > 0f ? 2 : 3);
            plates.Add(new Plate
            {
                Model = part.name, Face = face, OffsetMm = NeospaceUnits.ToMm(center.x),
                LengthMm = NeospaceUnits.ToMm(max.x - min.x), IsActualFbx = actualFbx
            });
        }
        return plates;
    }

    static bool FullCoverage(List<Plate> plates, int face, int size)
    {
        int modules = 0;
        foreach (Plate plate in plates)
        {
            if (plate.Face != face || !plate.Model.StartsWith("Veneer H", StringComparison.Ordinal)) continue;
            int n = int.Parse(plate.Model.Substring(8));
            modules += n + 1;
            if (Mathf.Abs(plate.LengthMm - (Skeleton.VeneerContactLength(n) - 2.08575f)) > 0.1f) return false;
        }
        return modules == size + 1;
    }

    static string Signature(List<Plate> plates, int face)
    {
        var rows = new List<string>();
        foreach (Plate plate in plates)
            if (plate.Face == face)
                rows.Add(plate.Model + "@" + Mathf.RoundToInt(plate.OffsetMm * 10f) + "/" + Mathf.RoundToInt(plate.LengthMm * 10f));
        rows.Sort(StringComparer.Ordinal);
        return string.Join(";", rows);
    }

    static IEnumerator WaitForPoll()
    {
        yield return new WaitForSecondsRealtime(0.45f);
        yield return null;
    }

    static void Capture(string fileName, GameObject frameObject, List<GameObject> boards,
        FrameOverlapResolver.FrameRecord frame, Vector3 inward, float verticalSide)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log("Channel exposure evidence " + fileName + ": skipped (null graphics device).");
            return;
        }
        const int width = 1280, height = 960, captureLayer = 31;
        const string directory = "Logs/channel-exposure-runtime";
        var changedLayers = new Dictionary<GameObject, int>();
        GameObject cameraObject = null;
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var subjects = new List<GameObject>(boards) { frameObject };
            GameObject finishRoot = GameObject.Find("FinishRoot");
            if (finishRoot != null) subjects.Add(finishRoot);
            bool any = false;
            Bounds bounds = default;
            foreach (GameObject subject in subjects)
            {
                foreach (Transform child in subject.GetComponentsInChildren<Transform>(true))
                    if (!changedLayers.ContainsKey(child.gameObject))
                    {
                        changedLayers.Add(child.gameObject, child.gameObject.layer);
                        child.gameObject.layer = captureLayer;
                    }
                foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            cameraObject = new GameObject("ChannelExposureEvidenceCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << captureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.13f);
            camera.orthographic = true;
            camera.aspect = width / (float)height;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            camera.orthographicSize = Mathf.Max(1f, extent * 1.3f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            Vector3 direction = (frame.LengthAxis * 0.8f - inward * 1.7f + Vector3.up * (verticalSide * 1.3f)).normalized;
            camera.transform.position = bounds.center + direction * Mathf.Max(10f, extent * 4f);
            camera.transform.LookAt(bounds.center);
            target = new RenderTexture(width, height, 24);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), image.EncodeToPNG());
            Debug.Log("Channel exposure evidence: " + Path.Combine(directory, fileName));
        }
        catch (Exception e) { Debug.LogWarning("Channel exposure evidence unavailable: " + e.Message); }
        finally
        {
            RenderTexture.active = previous;
            foreach (var entry in changedLayers) if (entry.Key != null) entry.Key.layer = entry.Value;
            if (cameraObject != null)
            {
                cameraObject.GetComponent<Camera>().targetTexture = null;
                Object.Destroy(cameraObject);
            }
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (image != null) Object.Destroy(image);
        }
    }
}
#endif
