#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real prefab marker poses, production freezing/splitting, and rendered joint coverage.</summary>
public static class FinishSpaceSplitRuntimeRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        var notes = new List<string>();
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            notes.Add(label);
            failure("Space split finishing: " + label);
        }

        var mode = Object.FindFirstObjectByType<SpaceModeController>();
        var build = Object.FindFirstObjectByType<BuildController>();
        var history = Object.FindFirstObjectByType<BuildHistory>();
        var finish = FinishController.Ensure();
        var freeze = typeof(PieceInstanceFactory).GetMethod("Freeze", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mode != null && mode.interaction != null && build != null && build.partDatabase != null && history != null && freeze != null,
            "real scene, catalogue and production freezing method are available");
        if (mode == null || mode.interaction == null || build == null || build.partDatabase == null || history == null || freeze == null)
            yield break;
        Check(!SpaceModeController.Active && mode.interaction.InstanceCount == 0,
            "fixture starts outside Space Mode with an empty arrangement");
        if (SpaceModeController.Active || mode.interaction.InstanceCount != 0) yield break;
        var instances = (List<SpaceInstance>)mode.interaction.Instances;
        var owned = new List<GameObject>();
        bool wasFinished = finish.IsOn;
        bool wasSuspended = BuildHistory.Suspended;
        try
        {
            BuildHistory.Suspended = false;
            build.SetCurrentPart(null);
            finish.SetOn(false, announce: false);
            history.ClearAll();
            yield return null;

            foreach (string id in new[] { "H7", "T7" })
            foreach (float yaw in new[] { 0f, 37f, 90f, 180f, 270f })
            {
                string label = id + " yaw " + yaw + ": ";
                var group = new GameObject("FreezeMarkerFixture");
                owned.Add(group);
                GameObject beam = Spawn(id, group.transform, yaw, new Vector3(0, 372.5f, 0));
                Check(beam != null && FrameOverlapResolver.TryBuildRecord(beam.transform, id, out _), label + "live prefab record exists");
                if (beam == null) continue;
                FrameOverlapResolver.TryBuildRecord(beam.transform, id, out var before);
                Transform pegA = Marker(beam.transform, "AP_Peg_A"), pegB = Marker(beam.transform, "AP_Peg_B");
                Check(pegA != null && pegB != null, label + "actual named peg transforms exist");
                freeze.Invoke(null, new object[] { group });
                Check(beam.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, label + "production Freeze removed interactive scripts");
                bool captured = FrameOverlapResolver.TryBuildRecord(beam.transform, id, out var after);
                Check(captured && SamePose(before, after), label + "freezing preserves the exact center, signed axis and both physical peg endpoints");
                Check(captured && pegA != null && pegB != null && Near(after.EndA, pegA.position) && Near(after.EndB, pegB.position),
                    label + "frozen endpoints retain Peg A/Peg B identity at their actual transforms");
                group.SetActive(false);
                Object.Destroy(group);
            }
            yield return null;
            mode.EnterSpaceMode();
            for (int i = 0; i < 3; i++) yield return null;
            Check(SpaceModeController.Active, "Space Mode enters for actual merge fixtures");
            if (!SpaceModeController.Active) yield break;

            foreach (string id in new[] { "H15", "T15" })
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                string label = id + " split yaw " + yaw + ": ";
                var beamGroup = new GameObject("SplitBeamFixture");
                var postGroup = new GameObject("SplitPostFixture");
                owned.Add(beamGroup); owned.Add(postGroup);
                GameObject original = Spawn(id, beamGroup.transform, yaw, new Vector3(0, 372.5f, 0));
                GameObject post = Spawn("V9", postGroup.transform, 0f, new Vector3(0, 372.5f, 0));
                Check(original != null && post != null, label + "real H/T and V catalogue models load");
                if (original == null || post == null) continue;
                Check(FrameOverlapResolver.TryWorldBounds(post.transform, out Bounds postShape) &&
                    Mathf.Abs(NeospaceUnits.ToMm(postShape.size.y) - Skeleton.VBodyLength(9)) < 0.1f &&
                    Mathf.Abs(NeospaceUnits.ToMm(postShape.size.x) - CatalogueData.ProfileMm) < 0.1f &&
                    Mathf.Abs(NeospaceUnits.ToMm(postShape.size.z) - CatalogueData.ProfileMm) < 0.1f,
                    label + "actual V9 mesh stands upright at 745 mm high with a 41 x 41 mm footprint");
                FrameOverlapResolver.TryBuildRecord(original.transform, id, out var liveBeam);
                FrameOverlapResolver.TryBuildRecord(post.transform, "V9", out var livePost);
                freeze.Invoke(null, new object[] { beamGroup });
                freeze.Invoke(null, new object[] { postGroup });
                instances.Add(beamGroup.AddComponent<SpaceInstance>());
                instances.Add(postGroup.AddComponent<SpaceInstance>());
                SpaceMerge.Apply(instances, build.partDatabase, mode.interaction.transform);
                finish.RefreshNow();
                yield return null;
                Check(!original.activeSelf, label + "production merge hides the divided original");
                var segments = new List<Transform>();
                if (SpaceMerge.DerivedRoot != null)
                    foreach (Transform child in SpaceMerge.DerivedRoot)
                        if (child.gameObject.activeInHierarchy && child.name.StartsWith("Merged_", StringComparison.Ordinal)) segments.Add(child);
                Check(segments.Count == 2, label + "merge produces the two catalogue replacement frames");
                if (id == "T15")
                {
                    Transform twist = segments.Find(s => s.name == "Merged_T7");
                    Transform regular = segments.Find(s => s.name == "Merged_H7");
                    Check(twist != null && regular != null,
                        label + "single twist split retains one T7 and uses H7 at the new post connection");
                    Transform outerPegA = twist != null ? Marker(twist, "AP_Peg_A") : null;
                    Check(outerPegA != null && Near(outerPegA.position, liveBeam.EndA),
                        label + "retained twist Peg A stays at the original H-host connection");
                }
                foreach (Transform segment in segments)
                {
                    string segmentId = segment.name.Substring("Merged_".Length);
                    Transform pegA = Marker(segment, "AP_Peg_A"), pegB = Marker(segment, "AP_Peg_B");
                    bool captured = FrameOverlapResolver.TryBuildRecord(segment, segmentId, out var record);
                    Check(captured && pegA != null && pegB != null && Near(record.EndA, pegA.position) && Near(record.EndB, pegB.position),
                        label + segmentId + " derived record follows its actual stripped marker transforms");
                    if (!captured) continue;
                    Vector3 desired = liveBeam.Center + liveBeam.LengthAxis *
                        (Vector3.Dot(record.Center - liveBeam.Center, liveBeam.LengthAxis) > 0f ? NeospaceUnits.Mm(352f) : -NeospaceUnits.Mm(352f));
                    Check(Near(record.Center, desired), label + segmentId + " geometric center remains on the exact split lattice");
                    foreach (Vector3 face in new[] { Vector3.up, Vector3.down, Vector3.Cross(liveBeam.LengthAxis, Vector3.up).normalized,
                        -Vector3.Cross(liveBeam.LengthAxis, Vector3.up).normalized })
                        Check(HasPlateAt("Veneer ", record.Center, record.LengthAxis, face, 0f),
                            label + segmentId + " keeps rendered veneer at the center of each exposed channel " + face);
                }

                // H halves enter opposite post channels at the same physical
                // locking hole. Their peg transforms stop at the post faces,
                // not at its centre. The free perpendicular channels run on.
                if (id == "H15" || id == "T15")
                {
                    foreach (Vector3 face in new[] { liveBeam.LengthAxis, -liveBeam.LengthAxis })
                    {
                        Check(!HasPlateAt("Cap Side", livePost.Center, Vector3.up, face, 0f),
                            label + "no rendered Cap Side occupies the actual beam/post joint " + face);
                        Check(!HasPlateAt("Veneer ", livePost.Center, Vector3.up, face, 0f) &&
                            HasPlateAt("Veneer ", livePost.Center, Vector3.up, face, -176f) &&
                            HasPlateAt("Veneer ", livePost.Center, Vector3.up, face, 176f),
                            label + "joint-facing veneer stops at the beam but survives above and below " + face);
                    }
                    // Rhino rule: the connection level rings the post, so the
                    // two free perpendicular faces take a Cap Side at that
                    // hole with veneers meeting it from above and below.
                    Vector3 freeFace = Vector3.Cross(liveBeam.LengthAxis, Vector3.up).normalized;
                    Check(HasPlateAt("Cap Side", livePost.Center, Vector3.up, freeFace, 0f) &&
                        HasPlateAt("Cap Side", livePost.Center, Vector3.up, -freeFace, 0f) &&
                        HasPlateAt("Veneer ", livePost.Center, Vector3.up, freeFace, -176f) &&
                        HasPlateAt("Veneer ", livePost.Center, Vector3.up, freeFace, 176f) &&
                        HasPlateAt("Veneer ", livePost.Center, Vector3.up, -freeFace, -176f) &&
                        HasPlateAt("Veneer ", livePost.Center, Vector3.up, -freeFace, 176f),
                        label + "unoccupied perpendicular post faces take a rendered Cap Side at the connection level with veneers either side");
                    List<string> penetrations = VeneerPostPenetrations(post);
                    Check(penetrations.Count == 0, label + "actual veneer mesh bounds do not penetrate the split post body" +
                        (penetrations.Count == 0 ? "" : "\n" + string.Join("\n", penetrations)));
                    if (yaw == 0f)
                        CaptureJoint(id.ToLowerInvariant() + "-split-post-joint.png", livePost.Center, beamGroup, postGroup);
                    else if (penetrations.Count > 0)
                        CaptureJoint(id.ToLowerInvariant() + "-split-yaw-" + yaw + "-penetration.png", livePost.Center, beamGroup, postGroup);
                }
                instances.Clear();
                beamGroup.SetActive(false); postGroup.SetActive(false);
                SpaceMerge.Apply(instances, build.partDatabase, mode.interaction.transform);
                finish.RefreshNow();
                Object.Destroy(beamGroup); Object.Destroy(postGroup);
                yield return null;
            }
        }
        finally
        {
            instances.Clear();
            SpaceMerge.Apply(instances, build.partDatabase, mode.interaction.transform);
            foreach (GameObject go in owned) if (go != null) { go.SetActive(false); Object.Destroy(go); }
            if (SpaceModeController.Active) mode.ExitSpaceMode();
            finish.SetOn(wasFinished, announce: false);
            BuildHistory.Suspended = wasSuspended;
            Directory.CreateDirectory("Logs/space-split-finishing");
            notes.Insert(0, $"Space split finishing: {checks} checks, {failed} failures.");
            File.WriteAllText("Logs/space-split-finishing/results.txt", string.Join("\n", notes));
            Debug.Log(notes[0]);
        }
        yield return null;

        GameObject Spawn(string id, Transform parent, float yaw, Vector3 centerMm)
        {
            GameObject prefab = build.partDatabase.GetRealPrefab(id);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent);
            go.name = id;
            if (BeamPartUtility.IsVertical(id))
            {
                Quaternion upright = Quaternion.Euler(build.v3RotationEuler);
                if (build.partDatabase.TryGet(id, out var entry) && entry.overrideVPlacement)
                    upright = Quaternion.Euler(entry.vRotationEuler);
                go.transform.rotation = upright;
            }
            else
                go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(build.h3RotationEuler);
            if (FrameOverlapResolver.TryBuildRecord(go.transform, id, out var record))
                go.transform.position += centerMm * NeospaceUnits.Mm(1f) - record.Center;
            return go;
        }
    }

    static Transform Marker(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    static bool Near(Vector3 a, Vector3 b) => (a - b).magnitude <= NeospaceUnits.Mm(0.05f);
    static bool SamePose(FrameOverlapResolver.FrameRecord a, FrameOverlapResolver.FrameRecord b) =>
        Near(a.Center, b.Center) && Near(a.EndA, b.EndA) && Near(a.EndB, b.EndB) &&
        Vector3.Dot(a.LengthAxis, b.LengthAxis) > 0.99999f && Vector3.Dot(a.LocalY, b.LocalY) > 0.99999f;

    static bool HasPlateAt(string modelPrefix, Vector3 center, Vector3 axis, Vector3 normal, float offsetMm)
    {
        GameObject root = GameObject.Find("FinishRoot");
        if (root == null) return false;
        Vector3 target = center + axis * NeospaceUnits.Mm(offsetMm) + normal * NeospaceUnits.Mm(21.2f);
        foreach (Transform part in root.transform)
        {
            if (!part.gameObject.activeInHierarchy || !part.name.StartsWith(modelPrefix, StringComparison.Ordinal)) continue;
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;
                string asset = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!asset.StartsWith("Assets/Resources/Finish/", StringComparison.Ordinal)) continue;
                // Test actual mesh-local bounds at a channel surface sample,
                // not an inflated world AABB or a count of planned parts.
                Bounds local = filter.sharedMesh.bounds;
                local.Expand(0.00001f);
                if (local.Contains(filter.transform.InverseTransformPoint(target))) return true;
            }
        }
        return false;
    }

    static List<string> VeneerPostPenetrations(GameObject post)
    {
        var details = new List<string>();
        if (!FrameOverlapResolver.TryWorldBounds(post.transform, out Bounds postBounds))
        {
            details.Add("No rendered post bounds available.");
            return details;
        }
        GameObject root = GameObject.Find("FinishRoot");
        if (root == null) { details.Add("No rendered finishing root available."); return details; }
        float epsilon = NeospaceUnits.Mm(0.005f);
        foreach (Transform part in root.transform)
        {
            if (!part.gameObject.activeInHierarchy || !part.name.StartsWith("Veneer ", StringComparison.Ordinal)) continue;
            foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                Vector3 overlap = Vector3.Min(renderer.bounds.max, postBounds.max) - Vector3.Max(renderer.bounds.min, postBounds.min);
                if (overlap.x > epsilon && overlap.y > epsilon && overlap.z > epsilon)
                    details.Add(part.name + "/" + renderer.name + " penetration mm " + Mm(overlap) +
                        "; plate min/max mm " + Mm(renderer.bounds.min) + "/" + Mm(renderer.bounds.max) +
                        "; post min/max mm " + Mm(postBounds.min) + "/" + Mm(postBounds.max));
            }
        }
        return details;
        static string Mm(Vector3 value) =>
            $"({NeospaceUnits.ToMm(value.x):F4}, {NeospaceUnits.ToMm(value.y):F4}, {NeospaceUnits.ToMm(value.z):F4})";
    }

    static void CaptureJoint(string fileName, Vector3 center, params GameObject[] fixtures)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        const int captureLayer = 31, width = 1200, height = 900;
        var previousLayers = new Dictionary<GameObject, int>();
        var subjects = new List<GameObject>(fixtures);
        if (SpaceMerge.DerivedRoot != null) subjects.Add(SpaceMerge.DerivedRoot.gameObject);
        GameObject dressing = GameObject.Find("FinishRoot");
        if (dressing != null) subjects.Add(dressing);
        GameObject cameraObject = null;
        RenderTexture render = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (GameObject subject in subjects)
                foreach (Transform child in subject.GetComponentsInChildren<Transform>(true))
                    if (!previousLayers.ContainsKey(child.gameObject))
                    {
                        previousLayers.Add(child.gameObject, child.gameObject.layer);
                        child.gameObject.layer = captureLayer;
                    }
            cameraObject = new GameObject("SpaceSplitEvidenceCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << captureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.14f, 0.15f);
            camera.orthographic = true;
            camera.orthographicSize = NeospaceUnits.Mm(240f);
            camera.aspect = width / (float)height;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.transform.position = center + new Vector3(1.6f, 0.9f, 2.1f).normalized * NeospaceUnits.Mm(1800f);
            camera.transform.LookAt(center);
            render = new RenderTexture(width, height, 24);
            render.Create();
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Logs/space-split-finishing");
            File.WriteAllBytes(Path.Combine("Logs/space-split-finishing", fileName), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var entry in previousLayers) if (entry.Key != null) entry.Key.layer = entry.Value;
            if (cameraObject != null)
            {
                cameraObject.GetComponent<Camera>().targetTexture = null;
                Object.Destroy(cameraObject);
            }
            if (render != null) { render.Release(); Object.Destroy(render); }
            if (pixels != null) Object.Destroy(pixels);
        }
    }
}
#endif
