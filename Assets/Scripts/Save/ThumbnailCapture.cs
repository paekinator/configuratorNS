using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the scene to a small texture for a library card's thumbnail.
///
/// This used to live inside <see cref="PieceLibrary"/>, back when pieces were
/// the only thing that could be saved. Projects need exactly the same picture
/// taken the same way, and "PieceLibrary.CaptureThumbnailFramed" called from
/// the projects panel would read as the projects panel borrowing the block
/// library's plumbing. Nothing in here was ever piece-specific — it is camera
/// work — so it moved out whole rather than being duplicated.
///
/// Neither method writes anything to disk: the caller owns the returned
/// texture and must Destroy it.
/// </summary>
public static class ThumbnailCapture
{
    public const int DefaultWidth = 288;
    public const int DefaultHeight = 192;

    /// <summary>Render the camera's current view (no UI) into a texture.</summary>
    public static Texture2D Plain(Camera cam, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (cam == null)
            return null;

        var rt = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Thumbnails] Capture failed: " + e.Message);
            return null;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// Like <see cref="Framed"/>, but with everything except
    /// <paramref name="keep"/> switched off for the duration of the render.
    ///
    /// Framing alone is not enough to photograph ONE module: the camera still
    /// sees the grid floor, and every other structure that happens to fall
    /// behind it. The card then shows a picture of the whole scene, and three
    /// blocks captured from the same room are three pictures of the same
    /// room. Isolating gives the object on its own, which is what a
    /// catalogue card is.
    ///
    /// Renderers are disabled and put back rather than moved to a hidden
    /// layer: a layer change would have to be undone too, and would disturb
    /// anything else that reads layers — collision, the ghost mask, the
    /// picker's own raycast.
    /// </summary>
    public static Texture2D FramedIsolated(
        Camera reference, Bounds bounds, IEnumerable<Transform> keep,
        int width = DefaultWidth, int height = DefaultHeight)
    {
        var wanted = new HashSet<Renderer>();
        if (keep != null)
        {
            foreach (Transform root in keep)
            {
                if (root == null)
                    continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    wanted.Add(r);
            }
        }

        // Keep the real ground so the isolated block can still cast its
        // contact shadow. The visible grid is supplied separately by
        // IsometricBlock: the live grid contains the 88 mm module island,
        // built-zone outline and hover layer, none of which belongs in a
        // catalogue photograph.
        GroundGridController grid = FindGroundGrid();
        Renderer ground = grid != null ? grid.groundPlane : FindGroundPlane();
        if (ground != null && ground.enabled)
            wanted.Add(ground);

        var hidden = new List<Renderer>();
        // FindObjectsByType excludes HideAndDontSave objects, including the
        // live GroundGrid quad. Include those renderers, but never touch
        // prefab assets or other objects that are not in a loaded scene.
        foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (r == null || !r.gameObject.scene.IsValid() || !r.gameObject.scene.isLoaded ||
                !r.enabled || wanted.Contains(r))
                continue;
            r.enabled = false;
            hidden.Add(r);
        }

        try
        {
            return IsometricBlock(reference, bounds, grid, ground, width, height);
        }
        finally
        {
            // Restores only what WE hid, so a renderer already off for its
            // own reasons stays off.
            foreach (Renderer r in hidden)
                if (r != null)
                    r.enabled = true;
        }
    }

    /// <summary>
    /// Photograph one saved block in the same true-isometric projection used
    /// by the configurator. The orthographic size is solved from the block's
    /// projected bounds, so every block occupies the same useful share of its
    /// card instead of small blocks disappearing and large blocks being cut.
    ///
    /// A temporary ground quad draws only the 704 mm H7 lattice. It deliberately
    /// disables the 88 mm module island, origin emphasis, built-zone outline
    /// and placement hover without touching the live grid's material.
    /// </summary>
    static Texture2D IsometricBlock(
        Camera reference, Bounds bounds, GroundGridController grid, Renderer ground,
        int width, int height)
    {
        if (reference == null)
            return null;
        if (bounds.size.sqrMagnitude < 1e-8f)
            return Plain(reference, width, height);

        var cameraGo = new GameObject("BlockThumbnailCamera");
        cameraGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = cameraGo.AddComponent<Camera>();
        GameObject lattice = null;
        GameObject backdrop = null;

        try
        {
            cam.CopyFrom(reference);
            cam.targetTexture = null;
            cam.enabled = false;
            cam.orthographic = true;
            cam.aspect = Mathf.Max(0.01f, (float)width / Mathf.Max(1, height));

            // Equal world-axis foreshortening: elevation atan(1/sqrt(2)) and
            // a 45-degree turn around Y. Position is otherwise irrelevant to
            // orthographic scale, but it still has to clear the near plane.
            Vector3 focus = bounds.center;
            Vector3 viewDirection = new Vector3(1f, 1f, -1f).normalized;
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            float distance = Mathf.Max(12f, radius * 4f + 4f);
            cam.transform.position = focus + viewDirection * distance;
            cam.transform.LookAt(focus, Vector3.up);

            ProjectedHalfSize(cam, bounds, out float halfWidth, out float halfHeight);

            // About 25% breathing room around the projected object. This is
            // relative to each block, not a fixed zoom, so the card fill stays
            // consistent across the catalogue.
            const float framingPadding = 1.25f;
            cam.orthographicSize = Mathf.Max(
                0.05f,
                Mathf.Max(halfHeight, halfWidth / cam.aspect) * framingPadding);

            float groundY = ground != null
                ? ground.bounds.max.y + GroundGridController.FloorVisualOffset
                : bounds.min.y;

            // The quad only needs to cover this camera's ground footprint.
            // Extra reach lets the shader fade naturally before its mesh ends.
            float latticeHalfSize = Mathf.Max(
                8f,
                cam.orthographicSize * Mathf.Max(1f, cam.aspect) * 3.5f);
            lattice = CreateLatticeOnlyGround(grid, bounds.center, groundY, latticeHalfSize);

            distance = Mathf.Max(distance, latticeHalfSize * 1.5f);
            cam.transform.position = focus + viewDirection * distance;
            cam.transform.LookAt(focus, Vector3.up);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = distance + latticeHalfSize * 3f + radius * 2f;
            cam.backgroundColor = SceneBackdrop.ClearColor;
            backdrop = CreateThumbnailBackdrop(cam);

            return Plain(cam, width, height);
        }
        finally
        {
            DestroyTemporaryRenderable(lattice);
            DestroyTemporaryRenderable(backdrop);
            DestroyTemporary(cameraGo);
        }
    }

    static void ProjectedHalfSize(Camera cam, Bounds bounds,
                                  out float halfWidth, out float halfHeight)
    {
        Vector3 e = bounds.extents;
        halfWidth = 0f;
        halfHeight = 0f;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 offset = new Vector3(e.x * x, e.y * y, e.z * z);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, cam.transform.right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, cam.transform.up)));
        }
    }

    static GroundGridController FindGroundGrid()
    {
        return GroundGridController.Active != null
            ? GroundGridController.Active
            : UnityEngine.Object.FindFirstObjectByType<GroundGridController>();
    }

    static Renderer FindGroundPlane()
    {
        GameObject floor = GameObject.Find("GridFloor");
        return floor != null ? floor.GetComponent<Renderer>() : null;
    }

    static GameObject CreateLatticeOnlyGround(
        GroundGridController grid, Vector3 centre, float groundY, float halfSize)
    {
        Shader shader = Shader.Find("NEOSPACE/Ground Grid");
        if (shader == null)
        {
            Debug.LogWarning("[Thumbnails] Ground-grid shader not found; block card has no lattice.");
            return null;
        }

        // Start from the shader defaults rather than cloning the live material.
        // The live material intentionally contains all three layers, and a
        // catalogue capture should not inherit any of their animated state.
        var material = new Material(shader);
        material.name = "Block Thumbnail Lattice";
        material.hideFlags = HideFlags.HideAndDontSave;

        Vector2 groundCentre = new Vector2(centre.x, centre.z);
        float fadeEnd = halfSize * 0.9f;
        float latticeSpacing = NeospaceUnits.ModuleMeters * 8f;
        material.SetColor("_LineColor", grid != null ? grid.lineColor : new Color(0.42f, 0.42f, 0.42f, 1f));
        material.SetFloat("_Opacity", grid != null ? grid.opacity : 0.3f);
        material.SetFloat("_Spacing", latticeSpacing);
        material.SetFloat("_LineWidthPx", grid != null ? grid.lineWidthPixels : 1.1f);
        material.SetFloat("_ModuleOpacity", 0f);
        // Defensive as well as descriptive: even if a later shader revision
        // accidentally ignores ModuleOpacity, its module layer can only land
        // on the same coarse H7 lines and can never draw 88 mm cells.
        material.SetFloat("_ModuleSpacing", latticeSpacing);
        material.SetFloat("_OriginOpacity", 0f);
        material.SetFloat("_ZoneOpacity", 0f);
        material.SetFloat("_ZoneStrength", 0f);
        material.SetFloat("_HoverOpacity", 0f);
        material.SetFloat("_HoverFillOpacity", 0f);
        material.SetFloat("_HoverStrength", 0f);
        material.SetVector("_FadeCenter", new Vector4(groundCentre.x, groundCentre.y, 0f, 0f));
        material.SetFloat("_FadeStart", halfSize * 0.58f);
        material.SetFloat("_FadeEnd", fadeEnd);
        material.SetVector("_BoundsCenter", new Vector4(groundCentre.x, groundCentre.y, 0f, 0f));
        material.SetVector("_BoundsHalf", new Vector4(halfSize, halfSize, 0f, 0f));
        material.SetFloat("_BoundsFade", Mathf.Max(0.5f, halfSize * 0.08f));

        var go = new GameObject("BlockThumbnailLattice", typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = 2; // Ignore Raycast, like the live lattice.
        go.transform.position = new Vector3(centre.x, groundY + NeospaceUnits.Mm(1f), centre.z);

        var mesh = new Mesh { name = "BlockThumbnailLattice", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-halfSize, 0f, -halfSize),
            new Vector3( halfSize, 0f, -halfSize),
            new Vector3( halfSize, 0f,  halfSize),
            new Vector3(-halfSize, 0f,  halfSize)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfSize * 2f, 0.1f, halfSize * 2f));

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static GameObject CreateThumbnailBackdrop(Camera owner)
    {
        Shader shader = Shader.Find("NEOSPACE/Screen Gradient");
        if (shader == null)
        {
            Debug.LogWarning("[Thumbnails] Screen-gradient shader not found; using its centre colour.");
            return null;
        }

        var material = new Material(shader)
        {
            name = "Block Thumbnail Backdrop",
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetColor("_CornerTL", SceneBackdrop.TopLeft);
        material.SetColor("_CornerTR", SceneBackdrop.TopRight);
        material.SetColor("_CornerBL", SceneBackdrop.BottomLeft);
        material.SetColor("_CornerBR", SceneBackdrop.BottomRight);
        material.SetFloat("_Dither", 1f);

        var go = new GameObject("BlockThumbnailBackdrop", typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(owner.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 1f);

        var mesh = new Mesh { name = "BlockThumbnailBackdrop", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        go.AddComponent<SceneBackdrop>();
        return go;
    }

    static void DestroyTemporaryRenderable(GameObject target)
    {
        if (target == null)
            return;

        var filter = target.GetComponent<MeshFilter>();
        var renderer = target.GetComponent<MeshRenderer>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        Material material = renderer != null ? renderer.sharedMaterial : null;

        DestroyTemporary(target);
        DestroyTemporary(mesh);
        DestroyTemporary(material);
    }

    static void DestroyTemporary(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    /// <summary>
    /// Render the given world bounds from a fixed three-quarter angle using a
    /// throwaway camera cloned from <paramref name="reference"/>. Unlike
    /// <see cref="Plain"/> this does not depend on where the user happens to
    /// be looking, so a wall of saved cards is framed consistently.
    /// Falls back to the reference camera's own view for empty bounds.
    /// </summary>
    public static Texture2D Framed(
        Camera reference, Bounds bounds, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (reference == null)
            return null;
        if (bounds.size.sqrMagnitude < 1e-8f)
            return Plain(reference, width, height);

        var go = new GameObject("ThumbnailCamera");
        go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        try
        {
            cam.CopyFrom(reference);
            cam.targetTexture = null;
            cam.enabled = false;   // render on demand only
            // Always a perspective three-quarter shot, whatever the user is
            // viewing through. CopyFrom carries the isometric scheme's
            // orthographic projection across, and the framing below is FOV
            // arithmetic — it would have produced a card of the wrong size, and
            // a library's cards would have changed look with the camera scheme
            // that happened to be active when each was saved.
            cam.orthographic = false;

            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 25f, 40f);
            float distance = radius * 1.35f / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            Vector3 direction = new Vector3(1f, 0.75f, -1f).normalized;
            cam.transform.position = bounds.center + direction * distance;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;

            return Plain(cam, width, height);
        }
        finally
        {
            UnityEngine.Object.Destroy(go);
        }
    }
}
