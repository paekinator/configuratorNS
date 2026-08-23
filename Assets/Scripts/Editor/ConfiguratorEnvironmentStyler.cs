using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
    static readonly Color Background = Hex("EAE5DD");   // warm off-white backdrop
    static readonly Color FloorColor = Hex("DAD3C7");   // putty floor, darker than frames
    static readonly Color GridLine = Hex("C9C1B3");     // subtle grid line
    static readonly Color SunColor = Hex("FFF5E8");     // warm white key light
    static readonly Color AmbientSky = Hex("F2EEE7");
    static readonly Color AmbientEquator = Hex("D8D2C7");
    static readonly Color AmbientGround = Hex("B5AC9D");

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
        StyleLight();
        StyleAmbient();
        StyleFloor();

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
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        EditorUtility.SetDirty(cam);
    }

    static void StyleLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;

            Undo.RecordObject(light, "Style Light");
            Undo.RecordObject(light.transform, "Style Light");
            light.color = SunColor;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -32f, 0f);
            EditorUtility.SetDirty(light);
            break;
        }
    }

    static void StyleAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.fog = false;
    }

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

        Texture2D gridTexture = CreateGridTexture();
        Material material = CreateOrUpdateFloorMaterial(gridTexture, FloorSizeMeters(floor), floor.transform);

        Undo.RecordObject(renderer, "Style Floor");
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }

    static float FloorSizeMeters(GameObject floor)
    {
        // Built-in plane is 10x10 units at scale 1.
        return 10f * Mathf.Max(floor.transform.localScale.x, floor.transform.localScale.z);
    }

    static Texture2D CreateGridTexture()
    {
        // One texture tile spans 8 modules (704 mm): a bold line on the tile
        // edge and thin lines at every 88 mm module in between.
        const int size = 256;
        const int pxPerModule = size / ModulesPerMajorLine; // 32 px per module
        const int majorLine = 3;
        const int minorLine = 1;

        // Neutral (white + light gray line) so the material/theme tint decides the
        // final floor color - this lets the runtime dark mode retint the floor.
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 minorCol = new Color32(235, 233, 229, 255);
        Color32 majorCol = new Color32(214, 211, 205, 255);

        // Lines centred on their module coordinates (half the width on each
        // side, wrapping) so snapped parts sit astride the drawn lines.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool major = (x + majorLine / 2) % size < majorLine ||
                         (y + majorLine / 2) % size < majorLine;
            bool minor = (x + minorLine / 2) % pxPerModule < minorLine ||
                         (y + minorLine / 2) % pxPerModule < minorLine;
            pixels[y * size + x] = major ? majorCol : (minor ? minorCol : fill);
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        EnsureFolder(TextureFolder);
        File.WriteAllBytes(GridTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();

        var importer = (TextureImporter)AssetImporter.GetAtPath(GridTexturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
    }

    static Material CreateOrUpdateFloorMaterial(Texture2D gridTexture, float floorSize, Transform floor)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { name = "MAT_Floor" };
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
        }

        float tiles = floorSize / GridTileUnits;

        // Offset the texture so grid lines land on world-origin module multiples —
        // the same 88 mm grid the template tools snap posts to.
        float edgeX = floor.position.x - floorSize * 0.5f;
        float edgeZ = floor.position.z - floorSize * 0.5f;
        var offset = new Vector2(
            Mathf.Repeat(edgeX / GridTileUnits, 1f),
            Mathf.Repeat(edgeZ / GridTileUnits, 1f));

        // The grid texture is neutral; the tint gives the floor its color, and the
        // runtime theme switch overrides it per-mode via a MaterialPropertyBlock.
        material.SetColor("_BaseColor", FloorColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", FloorColor);
        material.SetTexture("_BaseMap", gridTexture);
        material.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
        material.SetTextureOffset("_BaseMap", offset);
        material.SetFloat("_Smoothness", 0.08f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
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
