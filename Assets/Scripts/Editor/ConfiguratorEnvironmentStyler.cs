using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot restyle of the 3D environment ("Tools/Configurator/Style Environment")
/// to match the light, warm UI theme:
///   - Camera clears to a warm off-white instead of black.
///   - The directional light becomes soft warm white (it was bright cyan).
///   - Ambient light switches to a light warm trilight so nothing reads murky.
///   - The floor gets a putty-toned material with a module-fitted grid (one
///     cell per 88 mm NEOSPACE module, a bolder line every 8 modules), clearly
///     distinct from the near-white beam frames.
/// </summary>
public static class ConfiguratorEnvironmentStyler
{
    // No Background constant. The camera clears to SceneBackdrop.ClearColor,
    // the average of the gradient's four corners — this file used to keep a
    // private #EAE5DD while UIThemeController cleared to #F2F2F2, so the
    // backdrop changed colour the moment you pressed Play.
    // No floor, sun or ambient colours here either: they are StageLighting's,
    // for the same reason the background became SceneBackdrop's. Every one of
    // them had a second copy in UIThemeController.Palette that overwrote this
    // one on entering Play mode.
    // No GridLine constant either. It was a warm #C9C1B3 with no reader: the
    // grid lines are drawn into the texture as neutral greys by
    // CreateGridTexture and tinted by the floor's own colour. A leftover warm
    // constant in a file that has just had its warmth removed is exactly the
    // thing someone reaches for next time.

    const string TextureFolder = "Assets/UI/Textures";
    const string GridTexturePath = TextureFolder + "/FloorGrid.png";
    const string FloorMaterialPath = "Assets/Materials/MAT_Floor.mat";

    // The scene is modelled at 1 unit = 100 mm, so one 88 mm NEOSPACE module is
    // 0.88 units. Minor grid lines mark single modules (the snap grid used by
    // template posts); a bolder line every 8 modules (704 mm — an H7 span)
    // makes counting bays easy.
    const float ModuleUnits = 0.88f;
    const int ModulesPerMajorLine = 8;
    const float GridTileUnits = ModuleUnits * ModulesPerMajorLine;

    const string BackdropMaterialPath = "Assets/Materials/MAT_Backdrop.mat";
    const string BackdropShaderName = "NEOSPACE/Screen Gradient";

    const string ShadowCatcherMaterialPath = "Assets/Materials/MAT_ShadowCatcher.mat";
    const string ShadowCatcherShaderName = "NEOSPACE/Shadow Catcher";

    /// <summary>
    /// Scale of the ground plane. A Unity plane is 10x10 units, so 100 gives
    /// 1000x1000 — a kilometre-ish square, 500 units in every direction from
    /// the origin, which is 5,681 modules and the camera's own far clip
    /// distance. Effectively no limit for anything anyone will build.
    ///
    /// This is the number that decides how far the grid can reach, which is
    /// not obvious: the ground grid clamps itself to the real plane so that
    /// "there is grid here" never promises ground the placement ray cannot
    /// find. At the old scale of 20 the world stopped 10 m out and no slider
    /// on the grid could see past it.
    ///
    /// The plane is 200 triangles at any size, and the collider with it, so
    /// this costs nothing.
    /// </summary>
    const float GroundPlaneScale = 100f;

    /// <summary>
    /// How far in front of the camera the backdrop quad sits, and how big it
    /// is. Neither number affects what is drawn — the shader writes clip-space
    /// positions and ignores the transform — they exist only to keep the quad
    /// inside the frustum so it is not culled before it can paint.
    /// </summary>
    const float BackdropDistance = 0.5f;
    const float BackdropScale = 0.05f;

    const string WoodTexturePath = TextureFolder + "/PanelWood.png";
    const string PanelMaterialPath = "Assets/Materials/MAT_Panel.mat";
    const string PanelPrefabPath = "Assets/Prefabs/PanelMesh.prefab";

    // Aluminum for the beam frames: a cool satin silver that reads clearly
    // against the warm off-white backdrop instead of blending into it.
    static readonly Color BeamColor = Hex("B9BEC4");
    const string BeamMaterialPath = "Assets/Materials/MAT_Beam.mat";
    static readonly string[] BeamPrefabFolders =
    {
        "Assets/Prefabs/Vertical",
        "Assets/Prefabs/Horizontal",
        "Assets/Prefabs/TwistBeams"
    };

    [MenuItem("Tools/Configurator/Apply Beam Aluminum Material")]
    public static void StyleBeams()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { name = "MAT_Beam" };
            AssetDatabase.CreateAsset(material, BeamMaterialPath);
        }

        // Satin (brushed) aluminum: metallic but only moderately smooth, so it
        // shows soft directional highlights instead of mirror reflections
        // (there are no reflection probes in the scene to mirror).
        material.SetColor("_BaseColor", BeamColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", BeamColor);
        material.SetFloat("_Smoothness", 0.55f);
        material.SetFloat("_Metallic", 0.75f);
        EditorUtility.SetDirty(material);

        int prefabCount = 0;
        foreach (string folder in BeamPrefabFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = renderer.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            mats[i] = material;
                        renderer.sharedMaterials = mats;
                    }
                    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                }
                finally
                {
                    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ConfiguratorEnvironmentStyler] Beam aluminum material applied to {prefabCount} prefabs.");
    }

    [MenuItem("Tools/Configurator/Apply Panel Wood Material")]
    public static void StylePanels()
    {
        var woodTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodTexturePath);
        if (woodTexture == null)
        {
            EditorUtility.DisplayDialog("Panel Material", $"Missing texture at {WoodTexturePath}.", "OK");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { name = "MAT_Panel" };
            AssetDatabase.CreateAsset(material, PanelMaterialPath);
        }

        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", woodTexture);
        material.SetFloat("_Smoothness", 0.22f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);

        GameObject prefabRoot = UnityEditor.PrefabUtility.LoadPrefabContents(PanelPrefabPath);
        try
        {
            foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = material;
                renderer.sharedMaterials = mats;
            }
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabRoot, PanelPrefabPath);
        }
        finally
        {
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ConfiguratorEnvironmentStyler] Panel wood material applied.");
    }

    [MenuItem("Tools/Configurator/Style Environment")]
    public static void Style()
    {
        Undo.SetCurrentGroupName("Style Environment");
        int undoGroup = Undo.GetCurrentGroup();

        StyleCamera();
        StyleBackdrop();
        StyleLight();
        StyleAmbient();
        StyleFloor();
        StyleGroundGrid();

        Undo.CollapseUndoOperations(undoGroup);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[ConfiguratorEnvironmentStyler] Environment restyled.");
    }

    static void StyleCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("[EnvironmentStyler] No camera found.");
            return;
        }

        Undo.RecordObject(cam, "Style Camera");
        // Solid colour, not skybox, and kept even though a gradient quad is
        // about to cover every pixel of it: it is the floor under the backdrop.
        // If the quad is missing, culled, or switched off — which is exactly
        // what a thumbnail capture does — the view stays the right colour
        // instead of going black.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SceneBackdrop.ClearColor;
        EditorUtility.SetDirty(cam);

        // Post-processing OFF.
        //
        // It was on, and it was costing colour accuracy for nothing. The
        // backdrop is specified in exact brightness percentages and measured
        // back darker than every one of them — pure white arriving as 78%,
        // which no colour-space or blending mistake can do, because white is
        // white in every space. On top of that the measured centre came back
        // BRIGHTER than the average of the measured corners, which a
        // four-corner blend cannot produce at all. Both are things done to the
        // image after it is drawn.
        //
        // Nothing here wants them. This is a flat, unlit product configurator:
        // no bloom, no depth of field, no tonemapping worth the name, and the
        // camera's antialiasing is already None, so the post pass had nothing
        // left to do but alter colours nobody asked it to alter. Turning it off
        // also removes a full-screen pass from a WebGL build.
        //
        // Tools > Configurator > Report Backdrop Colours measures it both ways
        // round, so this can be checked rather than believed.
        var urp = cam.GetUniversalAdditionalCameraData();
        if (urp != null)
        {
            Undo.RecordObject(urp, "Style Camera");
            urp.renderPostProcessing = false;
            EditorUtility.SetDirty(urp);
        }
    }

    /// <summary>
    /// The studio backdrop: one quad, parented to the camera, carrying the
    /// NEOSPACE/Screen Gradient shader.
    ///
    /// Parented to the camera so it travels with every orbit and zoom without
    /// anything having to move it per frame, and so there is exactly one of it
    /// however many times this menu item is run.
    /// </summary>
    static void StyleBackdrop()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
            return;

        Shader shader = Shader.Find(BackdropShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[EnvironmentStyler] Shader \"" + BackdropShaderName
                             + "\" not found; the view keeps its flat background colour. "
                             + "Check the console for a compile error in "
                             + "Assets/Shaders/NeospaceScreenGradient.shader.");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(BackdropMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_Backdrop" };
            AssetDatabase.CreateAsset(material, BackdropMaterialPath);
        }
        material.shader = shader;
        material.SetColor("_CornerTL", SceneBackdrop.TopLeft);
        material.SetColor("_CornerTR", SceneBackdrop.TopRight);
        material.SetColor("_CornerBL", SceneBackdrop.BottomLeft);
        material.SetColor("_CornerBR", SceneBackdrop.BottomRight);
        EditorUtility.SetDirty(material);

        // Searched for across the scene, not just under the camera. A backdrop
        // that had been dragged out of the camera in the hierarchy would be
        // invisible there (it gates itself on being a camera's child) and this
        // would quietly build a second one beside it.
        SceneBackdrop existing = Object.FindFirstObjectByType<SceneBackdrop>();
        GameObject quad;
        if (existing != null)
        {
            quad = existing.gameObject;
            if (quad.transform.parent != cam.transform)
                Undo.SetTransformParent(quad.transform, cam.transform, "Style Backdrop");
        }
        else
        {
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = SceneBackdrop.ObjectName;
            Undo.RegisterCreatedObjectUndo(quad, "Style Backdrop");
            Undo.SetTransformParent(quad.transform, cam.transform, "Style Backdrop");
        }
        quad.name = SceneBackdrop.ObjectName;

        // A primitive comes with a collider, and this one sits a few
        // centimetres in front of the camera: left on, it would swallow every
        // placement ray, every selection click and every camera pick in the
        // app, from a surface nobody can see.
        foreach (Collider collider in quad.GetComponents<Collider>())
            Undo.DestroyObjectImmediate(collider);

        Undo.RecordObject(quad.transform, "Style Backdrop");
        quad.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + BackdropDistance);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * BackdropScale;

        if (quad.GetComponent<SceneBackdrop>() == null)
            Undo.AddComponent<SceneBackdrop>(quad);

        var renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Style Backdrop");
            renderer.sharedMaterial = material;
            // It is scenery: it neither casts nor receives light of any kind,
            // and lighting it would tint the wash away from the mockup's.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            EditorUtility.SetDirty(renderer);
        }

        AssetDatabase.SaveAssets();
    }

    static void StyleLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;

            Undo.RecordObject(light, "Style Light");
            Undo.RecordObject(light.transform, "Style Light");
            // Colour, intensity, shadow type and shadow strength all come from
            // StageLighting, which UIThemeController also applies at runtime.
            // They used to be written here AND there, which is how the floor
            // ended up one shade in the editor and another in Play mode.
            StageLighting.ApplySun(light);
            light.transform.rotation = Quaternion.Euler(StageLighting.SunAngles);
            EditorUtility.SetDirty(light);
            break;
        }
    }

    static void StyleAmbient()
    {
        StageLighting.ApplyAmbient();
        RenderSettings.fog = false;
    }

    /// <summary>
    /// Put the ground grid's settings somewhere they can be turned.
    ///
    /// EnvironmentBootstrap creates this host at runtime if it is missing, so
    /// the app works without this. But a component that only exists while the
    /// game is playing can only be tuned while the game is playing, and every
    /// value set that way is thrown away on exit — which is a miserable way to
    /// find a number by eye. Baking the host into the scene means the sliders
    /// are there in the inspector before you press Play, and what you set
    /// survives.
    ///
    /// The bootstrap reuses an existing "BuildEnvironment" rather than making
    /// a second, so the two cannot both own it.
    /// </summary>
    static void StyleGroundGrid()
    {
        GameObject host = GameObject.Find("BuildEnvironment");
        if (host == null)
        {
            host = new GameObject("BuildEnvironment");
            Undo.RegisterCreatedObjectUndo(host, "Style Ground Grid");
        }

        var grid = host.GetComponent<GroundGridController>();
        if (grid == null)
            grid = Undo.AddComponent<GroundGridController>(host);

        Undo.RecordObject(grid, "Style Ground Grid");
        // Wired here as well as by the bootstrap, so the island and the hover
        // highlight also work in edit mode — where there is no bootstrap and
        // the sliders are actually used.
        if (grid.buildController == null)
            grid.buildController = Object.FindFirstObjectByType<BuildController>();
        // The one colour that means "this is the thing you are pointing at",
        // shared with the ghost materials and the selection.
        grid.highlightColor = UIThemeController.HighlightColor;

        EditorUtility.SetDirty(grid);
        EditorUtility.SetDirty(host);
    }

    /// <summary>
    /// The ground: a shadow catcher, invisible except where the key light is
    /// blocked.
    ///
    /// It used to draw a tinted grid of its own, and that grid has gone
    /// entirely — CreateGridTexture, CreateOrUpdateFloorMaterial and the
    /// FloorSizeMeters/tiling maths with them. AdaptiveGridController already
    /// drew the real 88 mm grid on a patch above this plane, and was
    /// overwriting this floor's texture at runtime to quieten it down; the
    /// floor was a second, fainter grid that existed only to be suppressed.
    /// One surface draws the grid now, and this one draws the shadow.
    ///
    /// The COLLIDER stays. Placement rays find the ground through it, so the
    /// plane is still very much there — it simply has nothing to look at.
    /// </summary>
    static void StyleFloor()
    {
        GameObject floor = GameObject.Find("GridFloor");
        if (floor == null)
        {
            Debug.LogWarning("[EnvironmentStyler] No GridFloor object found in the scene.");
            return;
        }

        var renderer = floor.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogWarning("[EnvironmentStyler] GridFloor has no MeshRenderer.");
            return;
        }

        Shader shader = Shader.Find(ShadowCatcherShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[EnvironmentStyler] Shader \"" + ShadowCatcherShaderName
                             + "\" not found; the ground keeps whatever material it has. "
                             + "Check the console for a compile error in "
                             + "Assets/Shaders/NeospaceShadowCatcher.shader.");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowCatcherMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_ShadowCatcher" };
            AssetDatabase.CreateAsset(material, ShadowCatcherMaterialPath);
        }
        material.shader = shader;
        material.SetColor("_ShadowColor", StageLighting.GroundShadowColor);
        material.SetFloat("_Strength", StageLighting.GroundShadowOpacity);
        EditorUtility.SetDirty(material);

        // The ground's SIZE, set here rather than left to whatever the scene
        // happens to carry, because the grid's reach is clamped to it and a
        // limit nobody can see is a limit nobody can find. Only X and Z: a
        // plane has no thickness and scaling Y does nothing but confuse.
        Transform floorTf = floor.transform;
        if (!Mathf.Approximately(floorTf.localScale.x, GroundPlaneScale) ||
            !Mathf.Approximately(floorTf.localScale.z, GroundPlaneScale))
        {
            Undo.RecordObject(floorTf, "Style Floor");
            floorTf.localScale = new Vector3(GroundPlaneScale, floorTf.localScale.y, GroundPlaneScale);
        }

        Undo.RecordObject(renderer, "Style Floor");
        renderer.sharedMaterial = material;
        // Receives, never casts: a ground plane casting its own shadow would
        // put a hard edge across the backdrop at the plane's boundary — the
        // one edge this whole approach exists to get rid of.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        EditorUtility.SetDirty(renderer);

        AssetDatabase.SaveAssets();
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

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
