#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Real-scene finishing integration checks. Run last, after WorkflowUIRegression.
/// Uses the real V9, panel prefab and generated FBX renderers; overlap checks
/// independently intersect their actual world bounds while the fixture is axis
/// aligned. Rotation checks verify refresh, without claiming rotated AABB accuracy.
/// No asset, preference, piece-library, clipboard or download operation is used.
/// </summary>
public static class FinishRuntimeRegression
{
    const float PollWaitSeconds = 0.45f;
    const string OutputDirectory = "Logs/finish-runtime";

    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        var notes = new List<string>();
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            failure("Finish runtime: " + label);
            notes.Add("FAIL " + label);
        }

        var build = Object.FindFirstObjectByType<BuildController>();
        var history = Object.FindFirstObjectByType<BuildHistory>();
        var finish = FinishController.Ensure();
        GameObject vPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vertical/V9.prefab");
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PanelMesh.prefab");
        MethodInfo fingerprint = typeof(FinishController).GetMethod("StructureFingerprint", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo lastFingerprint = typeof(FinishController).GetField("_lastFingerprint", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(build != null && history != null && vPrefab != null && panelPrefab != null,
            "The real scene, V9 and panel prefabs must be available.");
        Check(fingerprint != null && lastFingerprint != null, "The geometry fingerprint must be inspectable for refresh verification.");
        if (build == null || history == null || vPrefab == null || panelPrefab == null || fingerprint == null || lastFingerprint == null)
            yield break;

        bool previousSuspended = BuildHistory.Suspended;
        BuildHistory.Suspended = false;
        GameObject frameObject = null, panelObject = null;
        try
        {
            SpacePlanningUI.Hide();
            if (SpaceModeController.Active) Object.FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
            Object.FindFirstObjectByType<TemplateSession>()?.SetTool(GuidedTemplateTool.None);
            build.SetCurrentPart(null);
            finish.SetOn(false, announce: false);
            history.ClearAll();
            yield return WaitForPoll();

            // Use the configured upright rotation, rather than the identity
            // pose used by count-only tests, so rendered bounds are meaningful.
            Quaternion upright = Quaternion.Euler(build.v3RotationEuler);
            if (build.partDatabase.TryGet("V9", out var entry) && entry.overrideVPlacement)
                upright = Quaternion.Euler(entry.vRotationEuler);
            var placed = build.PlacePartsBatch(new List<TemplatePartPose>
            {
                new TemplatePartPose("V9", Vector3.zero, upright, "finish-runtime")
            }, seatVerticalsOnFloor: true, validateOverlap: false);
            Check(placed.Placed == 1, "The actual V9 fixture must place once.");
            if (placed.Instances == null || placed.Instances.Count != 1) yield break;
            frameObject = placed.Instances[0];
            yield return WaitForPoll();
            Check(FrameOverlapResolver.TryBuildRecord(frameObject.transform, out var frame), "The actual V9 must produce a frame record.");
            Check(FrameOverlapResolver.TryWorldBounds(frameObject.transform, out Bounds frameBounds) &&
                NeospaceUnits.ToMm(frameBounds.size.y) > 700f, "The actual V9 mesh must stand upright for axis-aligned geometry checks.");
            Check(!finish.IsOn && FinishParts().Count == 0, "Finish off must leave no generated dressing, even after placing a frame.");
            Capture("01-finish-off.png", frameObject, null, notes);

            finish.SetOn(true, announce: false);
            yield return WaitForPoll();
            List<GameObject> baselineParts = FinishParts();
            int baselineLong = CountModel(baselineParts, "Veneer H7");
            Check(finish.IsOn && baselineParts.Count > 0, "Finish on must instantiate dressing for the actual V9.");
            Check(baselineLong == 4, "The unpanelled V9 should have four full-length H7 veneers.");
            Check(HasActualFbxMeshes(baselineParts), "Generated dressing must contain the supplied Resources/Finish FBX meshes.");
            Check(DressingHasNoEnabledInputs(baselineParts), "Generated FBX cameras, lights and colliders must remain disabled.");
            Capture("02-finish-on-no-panel.png", frameObject, null, notes);
            notes.Add($"No panel: {baselineParts.Count} finish roots, {RendererCount(baselineParts)} mesh renderers, {baselineLong} H7 veneers.");

            panelObject = MakeShelf(panelPrefab, frame.Center);
            yield return WaitForPoll(); // No NotifyChanged: live geometry polling owns this refresh.
            List<GameObject> shelfParts = FinishParts();
            Bounds panelBounds = panelObject.GetComponent<Renderer>().bounds;
            float panelMinMm = NeospaceUnits.ToMm(panelBounds.min.y - frame.Center.y);
            float panelMaxMm = NeospaceUnits.ToMm(panelBounds.max.y - frame.Center.y);
            Check(Mathf.Abs(panelMinMm - 20f) < 0.01f && Mathf.Abs(panelMaxMm - 21f) < 0.01f,
                "The live shelf must occupy hole +20.5 mm, with exactly 1 mm thickness.");
            List<string> collisions = RenderedOverlaps(shelfParts, panelBounds);
            Check(collisions.Count == 0, "Actual FBX bounds must not intersect the thin shelf: " + string.Join(", ", collisions));
            Check(CountModel(shelfParts, "Veneer H7") < baselineLong,
                "Adding the thin shelf must replace the crossing full-length veneers.");
            Check(HasVeneerAt(shelfParts, frame.Center, Vector3.right, -176f) &&
                  HasVeneerAt(shelfParts, frame.Center, Vector3.right, 176f),
                "Real +X veneers must remain above and below the shelf.");
            Check(HasVeneerAt(shelfParts, frame.Center, Vector3.forward, -176f) &&
                  HasVeneerAt(shelfParts, frame.Center, Vector3.forward, 176f),
                "Real +Z veneers must remain above and below the shelf.");
            Check(HasVeneerAt(shelfParts, frame.Center, Vector3.left, 0f) &&
                  HasVeneerAt(shelfParts, frame.Center, Vector3.back, 0f),
                "The outward channels must keep their long veneers.");
            Capture("03-finish-on-thin-shelf.png", frameObject, panelObject, notes);
            notes.Add($"Thin shelf: {shelfParts.Count} finish roots, {CountModel(shelfParts, "Veneer H7")} H7 veneers, {collisions.Count} rendered overlaps.");

            Object.Destroy(panelObject);
            panelObject = null;
            yield return WaitForPoll();
            List<GameObject> afterDelete = FinishParts();
            Check(CountModel(afterDelete, "Veneer H7") == baselineLong,
                "Deleting the live panel must restore all four full-length veneers.");
            Check(afterDelete.Count == baselineParts.Count, "Deleting the panel must restore the original number of finish roots.");
            Capture("04-after-panel-delete.png", frameObject, null, notes);

            panelObject = MakeShelf(panelPrefab, frame.Center);
            yield return WaitForPoll();
            Vector3 fixedPanelCentre = panelObject.transform.position;
            int beforeResize = (int)fingerprint.Invoke(finish, null);
            List<GameObject> preResizeParts = FinishParts();
            Vector3 scale = panelObject.transform.localScale;
            scale.x = NeospaceUnits.Mm(200f); // Same centre; its nearest edge now clears the post.
            panelObject.transform.localScale = scale;
            int resizedFingerprint = (int)fingerprint.Invoke(finish, null);
            Check(panelObject.transform.position == fixedPanelCentre && resizedFingerprint != beforeResize,
                "Resizing a centered panel must change the geometry fingerprint.");
            yield return WaitForPoll();
            Check((int)lastFingerprint.GetValue(finish) == resizedFingerprint && AllDestroyed(preResizeParts),
                "A centered panel resize must regenerate dressing without a placement notification.");
            Check(CountModel(FinishParts(), "Veneer H7") == baselineLong,
                "Shrinking the centered panel clear of the post must restore long veneers.");
            Check(RenderedOverlaps(FinishParts(), panelObject.GetComponent<Renderer>().bounds).Count == 0,
                "The resized axis-aligned shelf must not intersect actual finish renderers.");

            int beforePanelRotation = (int)fingerprint.Invoke(finish, null);
            List<GameObject> prePanelRotationParts = FinishParts();
            panelObject.transform.rotation = Quaternion.AngleAxis(37f, Vector3.up) * panelObject.transform.rotation;
            int rotatedPanelFingerprint = (int)fingerprint.Invoke(finish, null);
            Check(panelObject.transform.position == fixedPanelCentre && rotatedPanelFingerprint != beforePanelRotation,
                "Rotating a centered panel must change the geometry fingerprint.");
            yield return WaitForPoll();
            Check((int)lastFingerprint.GetValue(finish) == rotatedPanelFingerprint && AllDestroyed(prePanelRotationParts),
                "A centered panel rotation must regenerate dressing without a placement notification.");

            // Rotation about the actual frame centre keeps its position input
            // invariant but changes the channel directions consumed by finish.
            Object.Destroy(panelObject);
            panelObject = null;
            yield return WaitForPoll();
            FrameOverlapResolver.TryBuildRecord(frameObject.transform, out frame);
            Vector3 fixedFrameCentre = frame.Center;
            int beforeFrameRotation = (int)fingerprint.Invoke(finish, null);
            List<GameObject> preFrameRotationParts = FinishParts();
            frameObject.transform.RotateAround(fixedFrameCentre, Vector3.up, 37f);
            int rotatedFrameFingerprint = (int)fingerprint.Invoke(finish, null);
            FrameOverlapResolver.TryBuildRecord(frameObject.transform, out var rotatedFrame);
            Check((rotatedFrame.Center - fixedFrameCentre).magnitude < NeospaceUnits.Mm(0.1f),
                "Frame yaw fixture must preserve its geometric centre.");
            Check(rotatedFrameFingerprint != beforeFrameRotation, "Frame rotation about its centre must change the geometry fingerprint.");
            yield return WaitForPoll();
            Check((int)lastFingerprint.GetValue(finish) == rotatedFrameFingerprint && AllDestroyed(preFrameRotationParts),
                "A frame rotated in place must regenerate dressing without a placement notification.");

            finish.SetOn(false, announce: false);
            yield return WaitForPoll();
            Check(!finish.IsOn && FinishParts().Count == 0, "Turning finish off after geometry changes must remove all dressing.");
        }
        finally
        {
            finish.SetOn(false, announce: false);
            if (panelObject != null) Object.Destroy(panelObject);
            if (frameObject != null) Object.Destroy(frameObject);
            BuildHistory.Suspended = previousSuspended;
            notes.Insert(0, $"Finish runtime: {checks} checks, {failed} failures.");
            notes.Add("Geometry intersections use actual axis-aligned FBX renderer bounds with a 0.005 mm overlap threshold. Rotation cases verify regeneration only.");
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllText(Path.Combine(OutputDirectory, "results.txt"), string.Join("\n", notes));
            }
            catch (Exception e) { Debug.LogWarning("Finish runtime report could not be written: " + e.Message); }
        }
        yield return null;
        Debug.Log($"Finish runtime regression: {checks} checks, {failed} failures; evidence in {OutputDirectory}.");
    }

    static GameObject MakeShelf(GameObject prefab, Vector3 frameCentre)
    {
        var shelf = Object.Instantiate(prefab);
        shelf.name = "FinishRuntimeThinShelf";
        shelf.transform.SetPositionAndRotation(frameCentre + NeospaceUnits.Mm(1f) * new Vector3(352f, 20.5f, 352f),
            Quaternion.Euler(90f, 0f, 0f));
        // Physical H7 board inset from the two 704 mm bay centre lines.
        // Its near edges lie at +20.6415 mm, outside the post body but close
        // enough to catch the supplied veneers' real profile overhang.
        shelf.transform.localScale = NeospaceUnits.Mm(1f) * new Vector3(PanelFill.EdgeMm(7), PanelFill.EdgeMm(7), 1f);
        if (shelf.GetComponent<PanelInstance>() == null) shelf.AddComponent<PanelInstance>();
        foreach (Collider collider in shelf.GetComponentsInChildren<Collider>()) collider.enabled = false;
        return shelf;
    }

    static IEnumerator WaitForPoll()
    {
        yield return new WaitForSecondsRealtime(PollWaitSeconds);
        yield return null; // Deferred Destroy from the regeneration must finish.
    }

    static List<GameObject> FinishParts()
    {
        var parts = new List<GameObject>();
        GameObject root = GameObject.Find("FinishRoot");
        if (root == null) return parts;
        foreach (Transform child in root.transform)
            if (child.gameObject.activeInHierarchy) parts.Add(child.gameObject);
        return parts;
    }

    static List<string> RenderedOverlaps(List<GameObject> parts, Bounds panel)
    {
        var collisions = new List<string>();
        float epsilon = NeospaceUnits.Mm(0.005f);
        foreach (GameObject part in parts)
        foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.enabled) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Bounds mesh = renderer.bounds;
            Vector3 overlap = Vector3.Min(mesh.max, panel.max) - Vector3.Max(mesh.min, panel.min);
            if (overlap.x > epsilon && overlap.y > epsilon && overlap.z > epsilon)
                collisions.Add(part.name + "/" + renderer.name + $" ({NeospaceUnits.ToMm(overlap.x):F4}, {NeospaceUnits.ToMm(overlap.y):F4}, {NeospaceUnits.ToMm(overlap.z):F4} mm)");
        }
        return collisions;
    }

    static bool HasVeneerAt(List<GameObject> parts, Vector3 frameCentre, Vector3 face, float heightMm)
    {
        Vector3 sideways = Vector3.Cross(Vector3.up, face);
        float height = frameCentre.y + NeospaceUnits.Mm(heightMm);
        foreach (GameObject part in parts)
        {
            if (!part.name.StartsWith("Veneer ", StringComparison.Ordinal)) continue;
            foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds bounds = renderer.bounds;
                Vector3 to = bounds.center - frameCentre;
                if (Vector3.Dot(to, face) < NeospaceUnits.Mm(20f) || Mathf.Abs(Vector3.Dot(to, sideways)) > NeospaceUnits.Mm(5f)) continue;
                if (height >= bounds.min.y && height <= bounds.max.y) return true;
            }
        }
        return false;
    }

    static bool HasActualFbxMeshes(List<GameObject> parts)
    {
        int count = 0;
        foreach (GameObject part in parts)
        foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!path.StartsWith("Assets/Resources/Finish/", StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return false;
            // Bounds are available on imported meshes even with Read/Write off.
            if (filter.sharedMesh.bounds.size.sqrMagnitude <= 0f) return false;
            count++;
        }
        return count > 0;
    }

    static bool DressingHasNoEnabledInputs(List<GameObject> parts)
    {
        foreach (GameObject part in parts)
        {
            foreach (Camera camera in part.GetComponentsInChildren<Camera>(true)) if (camera.enabled) return false;
            foreach (Light light in part.GetComponentsInChildren<Light>(true)) if (light.enabled) return false;
            foreach (Collider collider in part.GetComponentsInChildren<Collider>(true)) if (collider.enabled) return false;
        }
        return true;
    }

    static int CountModel(List<GameObject> parts, string model) => parts.FindAll(p => p != null && p.name == model).Count;
    static int RendererCount(List<GameObject> parts)
    {
        int count = 0;
        foreach (GameObject part in parts) count += part.GetComponentsInChildren<MeshRenderer>().Length;
        return count;
    }
    static bool AllDestroyed(List<GameObject> parts) => parts.Count > 0 && parts.TrueForAll(part => part == null);

    static void Capture(string fileName, GameObject frame, GameObject panel, List<string> notes)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            notes.Add(fileName + ": skipped (null graphics device).");
            return;
        }
        const int width = 1024, height = 768, captureLayer = 31;
        var changedLayers = new Dictionary<GameObject, int>();
        GameObject cameraObject = null;
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var subjects = FinishParts();
            subjects.Add(frame);
            if (panel != null) subjects.Add(panel);
            bool any = false;
            Bounds bounds = default;
            foreach (GameObject subject in subjects)
            {
                foreach (Transform transform in subject.GetComponentsInChildren<Transform>(true))
                    if (!changedLayers.ContainsKey(transform.gameObject))
                    {
                        changedLayers.Add(transform.gameObject, transform.gameObject.layer);
                        transform.gameObject.layer = captureLayer;
                    }
                foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            cameraObject = new GameObject("FinishRegressionEvidenceCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << captureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.13f);
            camera.orthographic = true;
            camera.aspect = width / (float)height;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            camera.orthographicSize = Mathf.Max(1f, extent * 1.5f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.transform.position = bounds.center + new Vector3(1.5f, 0.8f, 1.7f).normalized * Mathf.Max(10f, extent * 4f);
            camera.transform.LookAt(bounds.center);
            target = new RenderTexture(width, height, 24);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), image.EncodeToPNG());
            notes.Add(fileName + ": rendered actual FBX fixture at 1024 × 768.");
        }
        catch (Exception e) { notes.Add(fileName + ": capture unavailable: " + e.Message); }
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
