using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Tools
{
    /// <summary>
    /// Editor Window to bulk-modify Texture Importer settings,
    /// specifically targeting mipmaps to fix sprite pixelation when scaled-down.
    /// </summary>
    public class SpriteOptimizer : EditorWindow
    {
        // Settings
        bool generateMipmap = true;
        float mipmapBias = -1f;
        TextureImporterMipFilter mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        FilterMode filterMode = FilterMode.Bilinear;
        int anisoLevel = 1;

        // UI State
        Vector2 scrollPosition;
        Texture2D[] selectedTextures = new Texture2D[0];

        [MenuItem("Tools/Evo UI/Sprite Optimizer", false, 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteOptimizer>();
            window.titleContent = new GUIContent("Sprite Optimizer", Resources.Load<Texture2D>("Editor Textures/Icon-UI_SpriteOptimizer"));
            window.minSize = new Vector2(350, 450);
            window.Show();
        }

        void OnEnable()
        {
            OnSelectionChange();
        }

        void OnSelectionChange()
        {
            // Grab all selected assets and resolve them to their main Texture2D.
            // This fixes the issue where selecting child "Sprite" sub-assets wouldn't detect the texture.
            var textures = new HashSet<Texture2D>();

            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) { continue; }

                // Even if the user selected a child Sprite, this grabs the main Texture2D file
                Texture2D tex = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
                if (tex != null) { textures.Add(tex); }
            }

            selectedTextures = textures.ToArray();
            Repaint();
        }

        void OnGUI()
        {
            EvoEditorGUI.BeginContainer();
            
            DrawHeader();
            DrawSettingsPanel();
            DrawSelectionPanel();
            DrawApplyButton();
            
            EvoEditorGUI.EndContainer();
        }

        void DrawHeader()
        {
            EvoEditorGUI.DrawInfoBox("This tool aims to bulk-optimize the given sprites to fix pixelation when scaled down using mipmaps.");
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawSettingsPanel()
        {
            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.BeginContainer();

            GUILayout.Label("Import Settings", EditorStyles.whiteLargeLabel);

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                GUIContent generateLabel = new("Generate Mipmap", "Enable to generate lower resolution textures for scaling down. Required for fixing minification pixelation.");
                generateMipmap = EditorGUILayout.Toggle(generateLabel, generateMipmap);
            }
            EvoEditorGUI.EndHorizontalBackground();

            // Disable mipmap-specific fields if mipmaps are turned off
            GUI.enabled = generateMipmap;

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                GUIContent biasLabel = new("Mipmap Bias", "Negative values make scaled-down sprites sharper but can introduce aliasing. Positive values make them blurrier. -0.5 to -1.0 is usually best for UI.");
                mipmapBias = EditorGUILayout.Slider(biasLabel, mipmapBias, -3f, 3f);
                if (GUILayout.Button("Reset", GUILayout.Width(50))) mipmapBias = -0.5f;
            }
            EvoEditorGUI.EndHorizontalBackground();

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                GUIContent mipFilterLabel = new("Mipmap Filtering", "Box is standard. Kaiser produces sharper results for the mip levels but takes slightly longer to import.");
                mipmapFilter = (TextureImporterMipFilter)EditorGUILayout.EnumPopup(mipFilterLabel, mipmapFilter);
            }
            EvoEditorGUI.EndHorizontalBackground();

            // Re-enable GUI for standard texture settings
            GUI.enabled = true;

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                GUIContent filterLabel = new("Filter Mode", "Bilinear is standard. Point is for pixel art. Trilinear blends mipmap levels for smoother transitions.");
                filterMode = (FilterMode)EditorGUILayout.EnumPopup(filterLabel, filterMode);
            }
            EvoEditorGUI.EndHorizontalBackground();

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                GUIContent anisoLabel = new("Aniso Level", "Increases texture quality when viewed at steep angles. Usually not needed for flat 2D UI (set to 0 or 1).");
                anisoLevel = EditorGUILayout.IntSlider(anisoLabel, anisoLevel, 0, 16);
            }
            EvoEditorGUI.EndHorizontalBackground(false);

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawSelectionPanel()
        {
            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.BeginContainer();

            GUILayout.Label($"Selected Textures: {selectedTextures.Length}", EditorStyles.whiteLargeLabel);

            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                // Always show a browse field to add textures manually
                EditorGUI.BeginChangeCheck();

                // Forcing singleLineHeight prevents Unity from drawing the giant texture thumbnail box
                Texture2D newTex = (Texture2D)EditorGUILayout.ObjectField("Add Texture", null, typeof(Texture2D), false, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (EditorGUI.EndChangeCheck() && newTex != null)
                {
                    var currentList = selectedTextures.ToList();
                    if (!currentList.Contains(newTex))
                    {
                        currentList.Add(newTex);
                        Selection.objects = currentList.ToArray(); // This updates Unity and triggers OnSelectionChange
                    }
                }
            }
            EvoEditorGUI.EndHorizontalBackground();
            EvoEditorGUI.AddLayoutSpace();

            if (selectedTextures.Length == 0)
            {
                EvoEditorGUI.DrawInfoBox("Select textures in the Project window or use the 'Add Texture' field above to begin.", EvoEditorGUI.InfoBoxType.Warning);
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground();
                return;
            }

            // Scrollable list of selected textures
            EvoEditorGUI.BeginHorizontalBackground(true);
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

                int displayLimit = Mathf.Min(selectedTextures.Length, 50); // Prevent lag with huge selections
                for (int i = 0; i < displayLimit; i++)
                {
                    EditorGUI.BeginChangeCheck();

                    // Using GUIContent.none + singleLineHeight makes it a neat, compact list item
                    Texture2D modifiedTex = (Texture2D)EditorGUILayout.ObjectField(GUIContent.none, selectedTextures[i], typeof(Texture2D), false, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                    // Allow to swap or clear (by selecting None) textures directly from the list
                    if (EditorGUI.EndChangeCheck())
                    {
                        var currentList = selectedTextures.ToList();
                        currentList.RemoveAt(i); // Remove the old texture

                        if (modifiedTex != null && !currentList.Contains(modifiedTex)) { currentList.Insert(i, modifiedTex); }
                        Selection.objects = currentList.ToArray(); // Update Unity and trigger OnSelectionChange

                        break;
                    }
                }

                if (selectedTextures.Length > displayLimit)
                {
                    GUILayout.Label($"... and {selectedTextures.Length - displayLimit} more.");
                }

                GUILayout.EndScrollView();
            }
            EvoEditorGUI.EndHorizontalBackground(false);

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawApplyButton()
        {
            EvoEditorGUI.AddPropertySpace();

            GUI.enabled = selectedTextures.Length > 0;
            if (GUILayout.Button($"Apply to {selectedTextures.Length} textures", GUILayout.Height(30))) { ApplySettingsToSelected(); }
            GUI.enabled = true;

            EvoEditorGUI.AddLayoutSpace();
        }

        void ApplySettingsToSelected()
        {
            if (selectedTextures.Length == 0)
                return;

            int total = selectedTextures.Length;
            int processed = 0;

            // StartAssetEditing pauses the Unity Asset Database pipeline.
            // This prevents Unity from recompiling/refreshing after every single texture change,
            // making bulk processing exponentially faster.
            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (Texture2D tex in selectedTextures)
                {
                    string path = AssetDatabase.GetAssetPath(tex);

                    // Show progress bar
                    if (EditorUtility.DisplayCancelableProgressBar("Updating Textures", $"Processing {tex.name}...", (float)processed / total))
                    {
                        Debug.LogWarning("MipMap tuning cancelled by user.");
                        break;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                    if (importer != null)
                    {
                        // Apply settings
                        importer.mipmapEnabled = generateMipmap;
                        importer.mipMapBias = mipmapBias;
                        importer.mipmapFilter = mipmapFilter;
                        importer.filterMode = filterMode;
                        importer.anisoLevel = anisoLevel;

                        // Save the changes
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }

                    processed++;
                }
            }
            finally
            {
                // Ensure these are called, even if an error occurs, otherwise the Editor locks up
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();

                // Refresh to ensure all views are updated
                AssetDatabase.Refresh();
            }

            Debug.Log($"Successfully tuned MipMaps for {processed} textures.");
        }
    }
}