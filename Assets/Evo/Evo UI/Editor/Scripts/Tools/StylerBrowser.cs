using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Tools
{
    using Button = UnityEngine.UIElements.Button;

    public class StylerPresetBrowser : EditorWindow, IEvoEditorGUIHandler
    {
        readonly List<StylerPreset> presets = new();
        StylerPreset selectedPreset;

        VisualElement detailsPanel;
        VisualElement headerContainer;
        VisualElement scrollShadow;
        ScrollView detailsScrollView;
        ToolbarMenu presetMenu;
        Texture2D shadowTexture;

        const int ActionItemSize = 24;
        const float ToolbarHeight = 30f;
        const float WindowMargin = 5f;
        const float ActionSpacing = 5f;

        [MenuItem("Tools/Evo UI/Styler Browser", false, 11)]
        public static void OpenWindow()
        {
            var window = GetWindow<StylerPresetBrowser>();
            window.titleContent = new GUIContent("Styler Browser", Resources.Load<Texture2D>("Editor Textures/Icon-UI_StylerBrowser"));
            window.minSize = new Vector2(300, 300);
            window.RefreshPresetList();
        }

        void OnEnable()
        {
            ConstructWindow();
            RefreshPresetList();
            Selection.selectionChanged += OnSelectionChange;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChange;
            if (shadowTexture != null) { DestroyImmediate(shadowTexture); }
        }

        void Update()
        {
            // Repaint on hover for hover effects
            if (mouseOverWindow == this) { Repaint(); }

            // Auto-refresh if the list is lost (e.g., after compilation or assembly reload)
            if (presets == null || presets.Count == 0) { RefreshPresetList(); }
        }

        void OnSelectionChange()
        {
            // Only rebuild UI if the selection is relevant
            if (Selection.activeGameObject != null || Selection.activeObject == null)
            {
                // Defer UI update to next frame to avoid layout errors during selection event
                rootVisualElement.schedule.Execute(UpdateDetailsPanel);
            }
        }

        void ConstructWindow()
        {
            rootVisualElement.Clear();

            // Toolbar
            var toolbar = new Toolbar();
            toolbar.style.height = ToolbarHeight;
            toolbar.style.marginRight = -1;
            toolbar.style.alignItems = Align.Center;

            // Spacer
            toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            // Refresh Button
            toolbar.Add(CreateToolbarButton("Refresh", "Refresh", RefreshPresetList));

            // Preset Selector Dropdown
            presetMenu = new ToolbarMenu
            {
                text = "Selected Preset: <b>None</b> ",
                style =
                {
                    width = StyleKeyword.Auto,
                    minWidth = 120,
                    alignSelf = Align.Stretch,
                    marginRight = -1,
                    paddingLeft = 10,
                    paddingRight = 9,
                }
            };
            toolbar.Add(presetMenu);

            var arrowIcon = presetMenu.Q(className: "unity-toolbar-menu__arrow");
            if (arrowIcon != null) { arrowIcon.style.marginLeft = 6; }

            rootVisualElement.Add(toolbar);

            // Main Content Area
            detailsPanel = new VisualElement();
            detailsPanel.style.flexGrow = 1;

            // Fixed Header Area
            headerContainer = new VisualElement();
            headerContainer.style.marginLeft = headerContainer.style.marginRight = WindowMargin;

            // Track geometry to position shadow correctly under header
            headerContainer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (scrollShadow != null)
                    scrollShadow.style.top = evt.newRect.y + evt.newRect.height;
            });
            detailsPanel.Add(headerContainer);

            // Scrollable Content Area (Embedded Inspector)
            detailsScrollView = new ScrollView();
            detailsScrollView.style.flexGrow = 1;
            detailsScrollView.style.marginLeft = detailsScrollView.style.marginRight = WindowMargin;
            detailsScrollView.verticalScroller.valueChanged += OnScrollChanged;
            detailsPanel.Add(detailsScrollView);

            // Add Shadow (last so it renders on top)
            scrollShadow = new VisualElement();
            scrollShadow.style.position = Position.Absolute;
            scrollShadow.style.left = 0;
            scrollShadow.style.right = 0;
            scrollShadow.style.height = 15;
            scrollShadow.style.opacity = 0; // Hidden by default
            scrollShadow.pickingMode = PickingMode.Ignore;
            scrollShadow.style.backgroundImage = GenerateShadowTexture();
            detailsPanel.Add(scrollShadow);

            rootVisualElement.Add(detailsPanel);
        }

        Texture2D GenerateShadowTexture()
        {
            if (shadowTexture != null)
                return shadowTexture;

            shadowTexture = new Texture2D(1, 16, TextureFormat.ARGB32, false)
            {
                alphaIsTransparency = true,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < 16; y++)
            {
                float alpha = (y / 15f) * 0.3f;
                shadowTexture.SetPixel(0, y, new Color(0, 0, 0, alpha));
            }

            shadowTexture.Apply();
            return shadowTexture;
        }

        void OnScrollChanged(float value)
        {
            if (scrollShadow == null)
                return;

            scrollShadow.style.opacity = value > 1f ? 1f : 0f; // Show shadow when scrolled down
        }

        ToolbarButton CreateToolbarButton(string text, string iconName, Action onClick)
        {
            var btn = new ToolbarButton(onClick) { focusable = false };
            btn.style.alignSelf = Align.Stretch;
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                var iconImage = new Image { image = icon };
                iconImage.style.width = 14;
                iconImage.style.height = 14;
                iconImage.style.marginLeft = 2;
                iconImage.style.marginRight = 5;
                btn.Add(iconImage);
            }

            btn.Add(new Label(text));
            return btn;
        }

        void RefreshPresetList()
        {
            presets.Clear();
            string[] guids = AssetDatabase.FindAssets("t:StylerPreset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StylerPreset>(path);
                if (asset != null) { presets.Add(asset); }
            }

            // Selection recovery
            if (selectedPreset != null)
            {
                if (!presets.Contains(selectedPreset))
                {
                    // Try recovering by name
                    var recovered = presets.FirstOrDefault(p => p.name == selectedPreset.name);
                    selectedPreset = recovered != null ? recovered : (presets.Count > 0 ? presets[0] : null);
                }
            }
            else if (presets.Count > 0)
            {
                selectedPreset = presets[0];
            }

            UpdateDetailsPanel();
            UpdatePresetDropdown();
        }

        void UpdatePresetDropdown()
        {
            if (presetMenu == null)
                return;

            presetMenu.menu.MenuItems().Clear();
            for (int i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                if (preset == null) { continue; }

                presetMenu.menu.AppendAction(preset.name,
                    (action) =>
                    {
                        SelectPreset(preset);
                    },
                    (action) =>
                    {
                        return (selectedPreset == preset) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                    });
            }

            string presetName = selectedPreset != null ? selectedPreset.name : "None";
            string label = $"Selected Preset: <b>{presetName}</b>";
            if (presetMenu.text != label) { presetMenu.text = label; }
        }

        void SelectPreset(StylerPreset preset)
        {
            if (selectedPreset == preset)
                return;

            selectedPreset = preset;
            UpdateDetailsPanel();
            UpdatePresetDropdown();
        }

        void UpdateDetailsPanel()
        {
            if (detailsScrollView == null || headerContainer == null)
                return;

            headerContainer.Clear();
            detailsScrollView.Clear();

            // Reset shadow state
            if (scrollShadow != null) { scrollShadow.style.opacity = 0; }

            // Check for preset
            if (selectedPreset == null)
            {
                var emptyLabel = new Label();
                if (presets.Count == 0) { emptyLabel.text = "No Styler Preset found in the project."; }
                else { emptyLabel.text = "Select a preset to see actions."; }
                emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                emptyLabel.style.marginTop = WindowMargin;
                headerContainer.Add(emptyLabel);
                return;
            }

            // Top Row: Asset Name Field + File Action Buttons
            var headerTopRow = new VisualElement();
            headerTopRow.style.flexDirection = FlexDirection.Row;
            headerTopRow.style.marginTop = (WindowMargin * 2) - 2;
            headerTopRow.style.marginBottom = ActionSpacing;

            var renameField = new TextField { value = selectedPreset.name };
            renameField.style.fontSize = 14;
            renameField.style.height = ActionItemSize;
            renameField.style.flexGrow = 1;
            renameField.style.marginRight = ActionSpacing;
            renameField.style.unityFontStyleAndWeight = FontStyle.Bold;
            renameField.RegisterCallback<FocusOutEvent>(evt => RenamePreset(selectedPreset, renameField.value));
            headerTopRow.Add(renameField);

            // Icon Action Buttons next to the text field
            var openBtn = CreateIconOnlyButton("UnityEditor.InspectorWindow", "Open in Inspector", () => Selection.activeObject = selectedPreset);
            var dupBtn = CreateIconOnlyButton("CreateAddNew", "Duplicate Preset", () => DuplicatePreset(selectedPreset));
            var delBtn = CreateIconOnlyButton("Cancel@2x", "Delete Preset", () => DeletePreset(selectedPreset));

            headerTopRow.Add(openBtn);
            headerTopRow.Add(dupBtn);
            headerTopRow.Add(delBtn);

            headerContainer.Add(headerTopRow);

            // Apply Actions Row (Horizontal Layout)
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;

            // Global Apply Button
            var applyAllBtn = new Button
            {
                text = "Apply To ▾",
                focusable = false
            };
            applyAllBtn.style.flexGrow = NewMethod();
            applyAllBtn.style.height = ActionItemSize;
            applyAllBtn.clicked += () => ShowApplyAllMenu(applyAllBtn);
            actionsRow.Add(applyAllBtn);

            // Selection Apply Actions Split-Button
            GameObject[] activeObjs = Selection.gameObjects;
            if (activeObjs != null && activeObjs.Length > 0)
            {
                var selectedHandlers = activeObjs
                    .Select(obj => obj.GetComponent<IStylerHandler>())
                    .Where(h => h != null)
                    .ToList();

                var childHandlers = new List<IStylerHandler>();

                foreach (var obj in activeObjs)
                    childHandlers.AddRange(obj.GetComponentsInChildren<IStylerHandler>(true));

                childHandlers = childHandlers.Distinct().ToList();

                var selectedSplitGroup = new VisualElement();
                selectedSplitGroup.style.flexDirection = FlexDirection.Row;
                selectedSplitGroup.style.flexGrow = 1;
                selectedSplitGroup.style.flexShrink = 0;
                // selectedSplitGroup.style.marginLeft = ActionSpacing;

                string label = activeObjs.Length == 1 ? $"Apply to '{activeObjs[0].name}'" : $"Apply to Selected ({selectedHandlers.Count})";
                var applySelectedBtn = CreateIconButton(label, "GameObject On Icon", () => ApplyPresetToMultiple(selectedHandlers));
                applySelectedBtn.style.flexGrow = 1;
                applySelectedBtn.style.height = ActionItemSize;

                // Style to look like a split button (merge with arrow)
                applySelectedBtn.style.borderTopRightRadius = 0;
                applySelectedBtn.style.borderBottomRightRadius = 0;
                applySelectedBtn.style.borderRightWidth = 0;
                applySelectedBtn.style.marginRight = 0;

                if (selectedHandlers.Count == 0)
                    applySelectedBtn.SetEnabled(false);

                selectedSplitGroup.Add(applySelectedBtn);

                var arrowBtn = new Button(() => ShowSelectedChildrenMenu(selectedSplitGroup, childHandlers))
                {
                    text = "▾"
                };
                arrowBtn.style.width = 24;
                arrowBtn.style.height = ActionItemSize;
                arrowBtn.style.borderTopLeftRadius = 0;
                arrowBtn.style.borderBottomLeftRadius = 0;
                arrowBtn.style.paddingLeft = 0;
                arrowBtn.style.paddingRight = 0;
                arrowBtn.style.marginLeft = 0;

                selectedSplitGroup.Add(arrowBtn);
                actionsRow.Add(selectedSplitGroup);
            }

            headerContainer.Add(actionsRow);

            // Separator
            var sepContainer = new VisualElement();
            sepContainer.style.flexDirection = FlexDirection.Row;
            sepContainer.style.alignItems = Align.Center;
            sepContainer.style.marginTop = ActionSpacing * 2;
            sepContainer.style.marginBottom = ActionSpacing;

            var sepIcon = new Image { image = EditorGUIUtility.IconContent("Preset.Context").image };
            sepIcon.style.width = 16;
            sepIcon.style.height = 16;
            sepIcon.style.marginLeft = 1;
            sepIcon.style.marginRight = 2;
            sepContainer.Add(sepIcon);

            var sepLabel = new Label("Preset Properties");
            sepLabel.style.fontSize = 13;
            sepContainer.Add(sepLabel);

            var sepLine = new VisualElement();
            sepLine.style.flexGrow = 1;
            sepLine.style.height = 1;
            sepLine.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            sepLine.style.marginLeft = ActionSpacing;
            sepLine.style.marginRight = WindowMargin;
            sepContainer.Add(sepLine);

            headerContainer.Add(sepContainer);

            // Embedded Inspector
            var serializedPreset = new SerializedObject(selectedPreset);
            var inspector = new InspectorElement(serializedPreset);
            inspector.style.marginTop = -10;
            inspector.style.marginLeft = inspector.style.marginRight = -10;
            detailsScrollView.Add(inspector);

            var bottomSpacer = new VisualElement { style = { height = 30 } };
            detailsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            detailsScrollView.Add(bottomSpacer);
        }

        static int NewMethod() => 1;

        void ShowApplyAllMenu(VisualElement anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Apply to Scene"), false, ApplyPresetToScene);
            menu.AddItem(new GUIContent("Apply to All Prefabs"), false, ApplyPresetGlobally);
            menu.DropDown(anchor.worldBound);
        }

        void ShowSelectedChildrenMenu(VisualElement anchor, List<IStylerHandler> childHandlers)
        {
            var menu = new GenericMenu();

            if (childHandlers != null && childHandlers.Count > 0)
                menu.AddItem(new GUIContent($"Apply to Selected and Children ({childHandlers.Count})"), false, () => ApplyPresetToMultiple(childHandlers));
            else
                menu.AddDisabledItem(new GUIContent("No Stylers found in children"));

            menu.DropDown(anchor.worldBound);
        }

        Button CreateIconButton(string text, string iconName, Action onClick)
        {
            var btn = new Button(onClick) { focusable = false };
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                var iconImage = new Image { image = icon };
                iconImage.style.width = 14;
                iconImage.style.height = 14;
                iconImage.style.marginLeft = 2;
                iconImage.style.marginRight = 5;
                btn.Add(iconImage);
            }

            btn.Add(new Label(text));
            return btn;
        }

        Button CreateIconOnlyButton(string iconName, string tooltip, Action onClick)
        {
            var btn = new Button(onClick) { focusable = false, tooltip = tooltip };
            btn.style.width = ActionItemSize;
            btn.style.height = ActionItemSize;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;
            btn.style.marginLeft = 2; // Spacing between the icon buttons

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                var iconImage = new Image { image = icon };
                iconImage.style.width = 16;
                iconImage.style.height = 16;
                btn.Add(iconImage);
            }

            return btn;
        }

        void ApplyPresetToScene()
        {
            if (selectedPreset == null)
                return;

            if (!EditorUtility.DisplayDialog("Apply Preset to Scene",
                $"Are you sure you want to apply '{selectedPreset.name}' to all Styler objects in the open scene?",
                "Apply", "Cancel"))
            {
                return;
            }

#if UNITY_6000_4_OR_NEWER
            var allHandlers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .OfType<IStylerHandler>()
                .ToList();
#else
            var allHandlers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<IStylerHandler>()
                .ToList();
#endif
            ApplyPresetToMultiple(allHandlers);
        }

        void ApplyPresetGlobally()
        {
            if (selectedPreset == null)
                return;

            if (!EditorUtility.DisplayDialog("Apply Globally to Project",
                $"WARNING: This will scan and apply '{selectedPreset.name}' to EVERY prefab in your entire project that uses a Styler component." +
                $"\n\nThis can take a moment for large projects. Do you want to continue?",
                "Apply to Entire Project", "Cancel"))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            List<IStylerHandler> handlersToModify = new();

            try
            {
                int total = guids.Length;
                for (int i = 0; i < total; i++)
                {
                    if (i % 50 == 0)
                        EditorUtility.DisplayProgressBar("Scanning Prefabs", $"Checking prefab {i}/{total}...", (float)i / total);

                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        var handlers = prefab.GetComponentsInChildren<IStylerHandler>(true);
                        if (handlers.Length > 0) { handlersToModify.AddRange(handlers); }
                    }
                }

                if (handlersToModify.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("Applying Presets", $"Applying to {handlersToModify.Count} objects...", 1f);

                    Undo.SetCurrentGroupName($"Apply Global Preset '{selectedPreset.name}'");
                    int undoGroup = Undo.GetCurrentGroup();

                    var undoObjects = handlersToModify.Where(h => h is Component).Select(h => h as Component).ToArray();
                    Undo.RecordObjects(undoObjects, "Apply Global Preset");

                    foreach (var handler in handlersToModify)
                    {
                        handler.Preset = selectedPreset;
                        if (handler is Component comp) { EditorUtility.SetDirty(comp); } // SetDirty ensured
                    }

                    Undo.CollapseUndoOperations(undoGroup);
                    AssetDatabase.SaveAssets();

                    Debug.Log($"[Styler] Successfully applied '{selectedPreset.name}' globally to {handlersToModify.Count} prefab objects.");
                }
                else
                {
                    Debug.Log($"[Styler] No prefabs found in the project containing a Styler component.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void ApplyPresetToMultiple(List<IStylerHandler> handlers)
        {
            if (handlers == null || handlers.Count == 0 || selectedPreset == null)
                return;

            // Register Undo for all objects at once
            var undoObjects = handlers.Where(h => h is Component).Select(h => h as Component).ToArray();
            Undo.RecordObjects(undoObjects, $"Apply Styler Preset '{selectedPreset.name}'");

            // Apply to all
            foreach (var handler in handlers)
            {
                handler.Preset = selectedPreset;
                if (handler is Component comp) { EditorUtility.SetDirty(comp); }
            }

            Debug.Log($"[Styler] Applied '{selectedPreset.name}' to {handlers.Count} object(s).");
        }

        void RenamePreset(StylerPreset asset, string newName)
        {
            if (asset == null || string.IsNullOrEmpty(newName) || asset.name == newName)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string result = AssetDatabase.RenameAsset(path, newName);
            if (string.IsNullOrEmpty(result))
            {
                AssetDatabase.SaveAssets();
                RefreshPresetList();
            }
        }

        void DuplicatePreset(StylerPreset asset)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CopyAsset(path, newPath);
            AssetDatabase.SaveAssets();

            RefreshPresetList();

            var newAsset = AssetDatabase.LoadAssetAtPath<StylerPreset>(newPath);
            if (newAsset) { SelectPreset(newAsset); }
        }

        void DeletePreset(StylerPreset asset)
        {
            if (asset == null)
                return;

            if (EditorUtility.DisplayDialog("Delete Styler Preset", $"Are you sure you want to delete {asset.name}?", "Delete", "Cancel"))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
                selectedPreset = null;
                RefreshPresetList();
            }
        }
    }
}