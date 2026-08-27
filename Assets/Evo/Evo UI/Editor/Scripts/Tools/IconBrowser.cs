using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Evo.EditorTools;

namespace Evo.UI.Tools
{
    public class IconBrowser : EditorWindow
    {
        Vector2 scrollPos;
        string searchQuery;
        string selectedCategory;
        List<IconLibrary.Item> searchResults = new();

        // Cache for previews to avoid regenerating every frame
        readonly Dictionary<string, Texture2D> previewCache = new();
        IconLibrary lastLibrary;

        const int PREVIEW_SIZE = 73;
        const int PREVIEW_PADDING = 20;
        const int PREVIEW_LABEL_WIDTH = 72;
        const int SEARCH_PREVIEW_SIZE = 48;
        const int ITEM_SPACING = 5;
        const int MAX_RESULTS_DISPLAY = 100;

        IconLibrary currentLibrary;
        string currentIconID;
        int currentResolution;
        GameObject selectedObject;
        Image targetImage;

        [MenuItem("Tools/Evo UI/Icon Browser", false, 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<IconBrowser>();
            window.titleContent = new GUIContent("Icon Browser", Resources.Load<Texture2D>("Editor Textures/Icon-UI_IconBrowser"));
            window.minSize = new Vector2(320, 410);
            window.Show();
        }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();

        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            previewCache.Clear();

        }

        void OnSelectionChanged()
        {
            selectedObject = Selection.activeGameObject;

            if (selectedObject == null) { targetImage = null; }
            else
            {
                targetImage = selectedObject.GetComponent<Image>();
                if (currentLibrary == null) { currentLibrary = IconLibrary.GetDefault(); }
            }

            Repaint();
        }

        void OnGUI()
        {
            EvoEditorGUI.BeginContainer();

            // Selection Info
            DrawSelectionInfo();

            if (selectedObject == null || targetImage == null)
            {
                EvoEditorGUI.AddLayoutSpace();
                if (GUILayout.Button("Select Default Icon Library", GUILayout.Height(24))) { Selection.activeObject = IconLibrary.GetDefault(); }
                EvoEditorGUI.EndContainer();
                return;
            }

            if (currentLibrary == null)
            {
                EditorGUI.BeginChangeCheck();
                currentLibrary = (IconLibrary)EditorGUILayout.ObjectField("Library", currentLibrary, typeof(IconLibrary), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (currentLibrary != null)
                    {
                        searchResults.Clear();
                        previewCache.Clear();
                        searchQuery = "";
                        selectedCategory = "";
                        PerformSearch();
                    }
                }

                EvoEditorGUI.AddLayoutSpace();
                EvoEditorGUI.DrawInfoBox("Please assign an Icon Library.", EvoEditorGUI.InfoBoxType.Warning);
                EvoEditorGUI.EndContainer();
                return;
            }

            if (lastLibrary != currentLibrary)
            {
                lastLibrary = currentLibrary;
                searchResults.Clear();
                previewCache.Clear();
                searchQuery = "";
                selectedCategory = "";
                PerformSearch();
            }

            if (currentLibrary == null)
            {
                EvoEditorGUI.EndContainer();
                return;
            }

            DrawIconSection();
            DrawBrowseSection();

            EvoEditorGUI.AddLayoutSpace();
            if (GUILayout.Button("Icon Library Importer", GUILayout.Height(24))) { IconLibraryImporter.ShowWindow(); }

            EvoEditorGUI.EndContainer();
        }

        void DrawSelectionInfo()
        {
            if (selectedObject == null)
            {
                EvoEditorGUI.DrawInfoBox("No GameObject selected. Please select a GameObject with an Image component.");
            }
            else if (targetImage == null)
            {
                EvoEditorGUI.DrawInfoBox($"Selected object '{selectedObject.name}' has no Image component.", EvoEditorGUI.InfoBoxType.Warning);
            }
            else
            {
                EvoEditorGUI.BeginVerticalBackground();
                EditorGUILayout.LabelField("Selected Object", selectedObject.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Image Component", "Found ✓");
                EvoEditorGUI.EndVerticalBackground(true);
                EvoEditorGUI.AddLayoutSpace();
            }
        }

        void DrawIconSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.BeginContainer();

            EditorGUILayout.BeginHorizontal();
            {
                EvoEditorGUI.BeginVerticalBackground(true, GUILayout.Width(PREVIEW_SIZE), GUILayout.Height(PREVIEW_SIZE));
                {
                    Texture2D preview = null;
                    var sprite = currentLibrary.GetSprite(currentIconID, currentResolution);
                    if (sprite != null) { preview = AssetPreview.GetAssetPreview(sprite); }

                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Space(4);
                    if (preview == null) { GUILayout.Label("Preview"); }
                    else
                    {
                        GUILayout.Label(preview, GUILayout.Width(PREVIEW_SIZE - PREVIEW_PADDING),
                        GUILayout.Height(PREVIEW_SIZE - PREVIEW_PADDING));
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(1);
                    GUILayout.FlexibleSpace();
                }
                EvoEditorGUI.EndVerticalBackground();

                EvoEditorGUI.AddLayoutSpace();

                EditorGUILayout.BeginVertical();
                {
                    EditorGUI.BeginChangeCheck();
                    currentLibrary = EvoEditorGUI.DrawObject(currentLibrary, "Library", allowSceneObjects: false, revertColor: true, labelWidth: PREVIEW_LABEL_WIDTH);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (currentLibrary != null)
                        {
                            searchResults.Clear();
                            previewCache.Clear();
                            PerformSearch();
                        }
                    }

                    EvoEditorGUI.BeginHorizontalBackground(true);
                    EvoEditorGUI.AddPropertySpace();
                    EditorGUILayout.LabelField("Icon ID", GUILayout.Width(PREVIEW_LABEL_WIDTH));
                    EditorGUILayout.LabelField(currentIconID ?? "None", GUILayout.MinWidth(PREVIEW_LABEL_WIDTH));
                    EvoEditorGUI.EndHorizontalBackground();
                    DrawResolutionSelector();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground(true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawBrowseSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.BeginContainer();
            {
                // Search Bar
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.BeginContainer(5);
                {
                    string catLabel = selectedCategory == string.Empty ? "" : $" in {selectedCategory}";
                    EvoEditorGUI.DrawLabel($"Search for icons" + catLabel);
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginChangeCheck();
                        searchQuery = GUILayout.TextField(searchQuery, GUI.skin.FindStyle("ToolbarSearchTextField"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            previewCache.Clear();
                            PerformSearch();
                        }
                        if (!string.IsNullOrEmpty(searchQuery) && GUILayout.Button(new GUIContent("", "Clear"),
                            GUI.skin.FindStyle("ToolbarSearchCancelButton")))
                        {
                            searchQuery = string.Empty;
                            GUI.FocusControl(null);
                            previewCache.Clear();
                            PerformSearch();
                        }
                    }
                    GUILayout.Space(-2);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(-1);
                }
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground(true);

                // Categories
                if (currentLibrary.categories != null && currentLibrary.categories.Count > 0)
                {
                    var categoryOptions = new List<string> { "All Categories" };
                    categoryOptions.AddRange(currentLibrary.categories);

                    int currentCategoryIndex = 0;
                    if (!string.IsNullOrEmpty(selectedCategory))
                    {
                        currentCategoryIndex = currentLibrary.categories.IndexOf(selectedCategory) + 1;
                        if (currentCategoryIndex < 0) { currentCategoryIndex = 0; }
                    }

                    EditorGUI.BeginChangeCheck();
                    int newCategoryIndex = EvoEditorGUI.DrawDropdown(currentCategoryIndex, categoryOptions.ToArray(), "Category",
                        true, true, true, labelWidth: PREVIEW_LABEL_WIDTH);
                    if (EditorGUI.EndChangeCheck())
                    {
                        selectedCategory = newCategoryIndex == 0 ? "" : currentLibrary.categories[newCategoryIndex - 1];
                        previewCache.Clear();
                        PerformSearch();
                    }
                }

                int totalResults = searchResults.Count;
                int displayedResults = Mathf.Min(totalResults, MAX_RESULTS_DISPLAY);

                if (totalResults == 0) { GUILayout.Space(2); EvoEditorGUI.DrawInfoBox("No icons found.", null, true); }
                else
                {
                    GUILayout.Space(-2);
                    if (totalResults > MAX_RESULTS_DISPLAY)
                    {
                        EditorGUILayout.LabelField(
                            $"Showing {displayedResults} of {totalResults} icon(s) - refine search to see more",
                            EditorStyles.miniLabel
                        );
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Found {totalResults} icon(s)", EditorStyles.miniLabel);
                    }

                    scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                    {
                        float scrollViewWidth = position.width - 54;
                        int itemWidth = SEARCH_PREVIEW_SIZE + ITEM_SPACING;
                        int columns = Mathf.Max(1, Mathf.FloorToInt(scrollViewWidth / itemWidth));
                        int rows = Mathf.CeilToInt((float)displayedResults / columns);

                        for (int row = 0; row < rows; row++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                for (int col = 0; col < columns; col++)
                                {
                                    int index = row * columns + col;
                                    if (index >= displayedResults) { break; }

                                    var item = searchResults[index];
                                    DrawIconButton(item);
                                }

                                GUILayout.FlexibleSpace();
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground(true);
        }

        void DrawResolutionSelector()
        {
            var currentItem = currentLibrary.GetItem(currentIconID);
            if (currentItem == null)
            {
                GUI.enabled = false;
                EvoEditorGUI.DrawDropdown(0, new string[] { "None" }, "Resolution", false, true, true, labelWidth: PREVIEW_LABEL_WIDTH);
                GUI.enabled = true;
                return;
            }

            var resolutions = currentItem.GetAvailableResolutions();
            if (resolutions.Count > 0)
            {
                var resOptions = new List<string>();
                for (int i = 0; i < resolutions.Count; i++) { resOptions.Add(resolutions[i] + "px"); }

                int currentIndex = resolutions.IndexOf(currentResolution);
                if (currentIndex < 0) { currentIndex = 0; }

                EditorGUI.BeginChangeCheck();
                int newIndex = EvoEditorGUI.DrawDropdown(currentIndex, resOptions.ToArray(), "Resolution", false, true, true,
                    labelWidth: PREVIEW_LABEL_WIDTH);
                if (EditorGUI.EndChangeCheck())
                {
                    currentResolution = resolutions[newIndex];
                    ApplyIconToTarget();
                }
            }
            else
            {
                GUI.enabled = false;
                EvoEditorGUI.DrawDropdown(0, new string[] { "None" }, "Resolution", false, true, true, labelWidth: PREVIEW_LABEL_WIDTH);
                GUI.enabled = true;
            }
        }

        void DrawIconButton(IconLibrary.Item item)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SEARCH_PREVIEW_SIZE + ITEM_SPACING));

            Texture2D preview = GetCachedPreview(item);

            if (GUILayout.Button(preview != null ? preview : Texture2D.whiteTexture,
                GUILayout.Width(SEARCH_PREVIEW_SIZE), GUILayout.Height(SEARCH_PREVIEW_SIZE)))
            {
                currentIconID = item.ID;

                // Set resolution to first available resolution
                var resolutions = item.GetAvailableResolutions();
                if (resolutions.Count > 0) { currentResolution = resolutions[0]; }
                else { currentResolution = 0; }

                ApplyIconToTarget();
                previewCache.Clear();
            }

            string displayName = item.ID;

            GUIStyle searchItemLabelStyle = new(EditorStyles.label)
            {
#if UNITY_6000_0_OR_NEWER
                clipping = TextClipping.Ellipsis,
#else
                clipping = TextClipping.Clip,
#endif
                alignment = TextAnchor.UpperCenter,
                fontSize = 10
            };

            EditorGUILayout.LabelField(new GUIContent(displayName, "ID: " + displayName), searchItemLabelStyle,
                GUILayout.Width(SEARCH_PREVIEW_SIZE), GUILayout.Height(16));

            EditorGUILayout.EndVertical();
        }

        void ApplyIconToTarget()
        {
            if (targetImage == null || currentLibrary == null || string.IsNullOrEmpty(currentIconID))
                return;

            var sprite = currentLibrary.GetSprite(currentIconID, currentResolution);
            if (sprite != null)
            {
                Undo.RecordObject(targetImage, "Apply Icon");
                targetImage.sprite = sprite;
                EditorUtility.SetDirty(targetImage);
            }
        }

        void PerformSearch()
        {
            if (currentLibrary == null)
            {
                searchResults.Clear();
                return;
            }

            currentLibrary.InvalidateCache();
            searchResults = currentLibrary.Search(searchQuery, selectedCategory);
        }

        Texture2D GetCachedPreview(IconLibrary.Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.ID)) { return null; }
            if (previewCache.TryGetValue(item.ID, out Texture2D cachedPreview)) { return cachedPreview; }

            var sprite = item.GetSprite();
            if (sprite != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(sprite);
                if (preview != null)
                {
                    previewCache[item.ID] = preview;
                    return preview;
                }
            }

            return null;
        }
    }
}