using Evo.EditorTools;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CustomEditor(typeof(ListView))]
    public class ListViewEditor : Editor
    {
        // Target
        ListView listView;

        // Properties
        SerializedProperty columnsProp;
        SerializedProperty rowsProp;
        SerializedProperty listViewStyleProp;
        SerializedProperty stylingSourceProp;
        SerializedProperty stylerPresetProp;
        SerializedProperty styleMappingsProp;
        SerializedProperty parentRect;

        // Scrolling Properties
        SerializedProperty enableScrollingProp;
        SerializedProperty verticalScrollbarProp;
        SerializedProperty scrollSensitivityProp;

        // Cached values
        bool needsRefresh = false;
        bool isStylerMode = false;
        int cachedColumnCount = 0;
        int cachedRowCount = 0;

        // GUI state cache
        GUIContent[] columnLabels;
        bool labelsNeedUpdate = true;

        void OnEnable()
        {
            listView = (ListView)target;

            columnsProp = serializedObject.FindProperty("columns");
            rowsProp = serializedObject.FindProperty("rows");
            listViewStyleProp = serializedObject.FindProperty("style");
            stylingSourceProp = serializedObject.FindProperty("stylingSource");
            stylerPresetProp = serializedObject.FindProperty("stylerPreset");
            styleMappingsProp = serializedObject.FindProperty("styleMapping");
            parentRect = serializedObject.FindProperty("parentRect");

            enableScrollingProp = serializedObject.FindProperty("enableScrolling");
            verticalScrollbarProp = serializedObject.FindProperty("verticalScrollbar");
            scrollSensitivityProp = serializedObject.FindProperty("scrollSensitivity");

            EvoEditorGUI.RegisterEditor(this);

            // Cache initial state
            UpdateCachedState();

            if (!Application.isPlaying && listView.transform.childCount == 0 && Selection.activeGameObject == listView.gameObject)
            {
                EditorRefresh();
            }
        }

        void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
        }

        void UpdateCachedState()
        {
            bool wasStylerMode = isStylerMode;
            int oldColumnCount = cachedColumnCount;

            isStylerMode = stylingSourceProp.enumValueIndex == 1 && listView.stylerPreset != null;
            cachedColumnCount = columnsProp.arraySize;
            cachedRowCount = rowsProp.arraySize;

            // Only rebuild labels if column count changed
            if (oldColumnCount != cachedColumnCount || wasStylerMode != isStylerMode)
            {
                labelsNeedUpdate = true;
            }
        }

        void RebuildColumnLabels()
        {
            if (!labelsNeedUpdate || columnsProp == null)
                return;

            columnLabels = new GUIContent[cachedColumnCount];
            for (int i = 0; i < cachedColumnCount; i++)
            {
                var columnProp = columnsProp.GetArrayElementAtIndex(i);
                var columnNameProp = columnProp.FindPropertyRelative("columnName");
                string name = string.IsNullOrEmpty(columnNameProp.stringValue) ? $"Column {i + 1}" : columnNameProp.stringValue;
                columnLabels[i] = new GUIContent(name);
            }
            labelsNeedUpdate = false;
        }

        public override void OnInspectorGUI()
        {
            DrawCustomGUI();
            EvoEditorGUI.HandleInspectorGUI();
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();

            // Cache state once at the start
            UpdateCachedState();
            RebuildColumnLabels();

            EvoEditorGUI.BeginCenteredInspector();

            EvoEditorGUI.DrawInfoBox("List View is currently in preview. Future updates may introduce major changes.");
            GUILayout.Space(4);

            DrawControlButtons();
            DrawColumnsSection();
            DrawRowsSection();
            DrawStyleSection();
            DrawReferencesSection();
            DrawCSVControls();

            EvoEditorGUI.EndCenteredInspector();

            // Change detection and refresh
            bool hasChanges = serializedObject.ApplyModifiedProperties();
            if (hasChanges)
            {
                labelsNeedUpdate = true;
                if (listView.autoRefresh) { needsRefresh = true; }
            }

            if (needsRefresh && listView.autoRefresh)
            {
                EditorApplication.delayCall += EditorRefresh;
                needsRefresh = false;
            }
        }

        void DrawControlButtons()
        {
            GUILayout.BeginHorizontal();
            {
                listView.autoRefresh = EvoEditorGUI.DrawToggle(listView.autoRefresh, "Auto Refresh");
                GUILayout.Space(1);
                if (EvoEditorGUI.DrawButton("Refresh", width: 60)) { listView.Initialize(); listView.Refresh(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(1);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawColumnsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref listView.groupFoldout, $"Columns ({cachedColumnCount})", EvoEditorGUI.GetIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    int visibleColumns = Mathf.Min(columnsProp.arraySize, cachedColumnCount);
                    for (int i = 0; i < visibleColumns; i++)
                    {
                        DrawColumnElement(i);
                    }
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Add Column", "Add", height: 22, iconSize: 8, revertBackgroundColor: true))
                    {
                        AddColumn();
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawColumnElement(int index)
        {
            SerializedProperty columnProp = columnsProp.GetArrayElementAtIndex(index);
            SerializedProperty columnNameProp = columnProp.FindPropertyRelative("columnName");

            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                // Use cached label
                string displayName = columnLabels != null && index < columnLabels.Length ?
                    columnLabels[index].text : $"Column {index + 1}";

                if (EvoEditorGUI.DrawButton(displayName, columnProp.isExpanded ? "Minimize" : "Expand",
                    height: 24, normalColor: Color.clear, iconSize: 8,
                    textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Left))
                {
                    columnProp.isExpanded = !columnProp.isExpanded;
                }

                // Column controls - move left/right and delete
                if (!Application.isPlaying)
                {
                    GUI.enabled = index > 0;
                    if (EvoEditorGUI.DrawButton(text: "←", tooltip: "Move column left", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Column Left");
                        MoveColumnLeft(index);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;

                    GUI.enabled = index < cachedColumnCount - 1;
                    if (EvoEditorGUI.DrawButton(text: "→", tooltip: "Move column right", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Column Right");
                        MoveColumnRight(index);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;
                }

                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete column", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    if (EditorUtility.DisplayDialog("Delete Column",
                        $"Are you sure you want to delete '{displayName}'?", "Yes", "No"))
                    {
                        RemoveColumn(index);

                        serializedObject.ApplyModifiedProperties();
                        labelsNeedUpdate = true;

                        UpdateCachedState();
                        EditorRefresh();

                        GUIUtility.ExitGUI();
                        return;
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (columnProp.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    EvoEditorGUI.DrawProperty(columnProp.FindPropertyRelative("columnIcon"), "Icon");
                    EvoEditorGUI.DrawProperty(columnNameProp, "Name");
                    EvoEditorGUI.DrawProperty(columnProp.FindPropertyRelative("alignment"), "Alignment");

                    EvoEditorGUI.BeginVerticalBackground();
                    SerializedProperty useFlexibleWidthProp = columnProp.FindPropertyRelative("useFlexibleWidth");
                    EvoEditorGUI.DrawToggle(useFlexibleWidthProp, "Flexible Width", "Column will auto-size to fill available space.",
                        false, true, true, bypassNormalBackground: true);
                    if (!useFlexibleWidthProp.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(columnProp.FindPropertyRelative("width"), "Width", null, false, true, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(index < cachedColumnCount - 1);
        }

        void DrawRowsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref listView.rowFoldout, $"Rows ({cachedRowCount})", EvoEditorGUI.GetIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    int visibleRows = Mathf.Min(rowsProp.arraySize, cachedRowCount);
                    for (int i = 0; i < visibleRows; i++)
                    {
                        DrawRowElement(i);
                    }
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Add Row", "Add", height: 22, iconSize: 8, revertBackgroundColor: true))
                    {
                        AddRow();
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawRowElement(int index)
        {
            SerializedProperty rowProp = rowsProp.GetArrayElementAtIndex(index);

            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                if (EvoEditorGUI.DrawButton($"Row {index + 1}", rowProp.isExpanded ? "Minimize" : "Expand",
                    height: 24, normalColor: Color.clear, iconSize: 8,
                    textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Left))
                {
                    rowProp.isExpanded = !rowProp.isExpanded;
                }

                if (!Application.isPlaying)
                {
                    GUI.enabled = index > 0;
                    if (EvoEditorGUI.DrawButton(text: "↑", tooltip: "Move row up", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Row Up");
                        MoveRowUp(index);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;

                    GUI.enabled = index < cachedRowCount - 1;
                    if (EvoEditorGUI.DrawButton(text: "↓", tooltip: "Move row down", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Row Down");
                        MoveRowDown(index);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;
                }

                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete row", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    RemoveRow(index);

                    serializedObject.ApplyModifiedProperties();
                    labelsNeedUpdate = true;

                    UpdateCachedState();
                    EditorRefresh();

                    GUIUtility.ExitGUI();
                    return;
                }
            }
            GUILayout.EndHorizontal();

            if (rowProp.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    // Only ensure consistency once per row when expanded
                    EnsureRowDataConsistency(rowProp, cachedColumnCount);

                    SerializedProperty valuesProp = rowProp.FindPropertyRelative("values");
                    SerializedProperty iconsProp = rowProp.FindPropertyRelative("icons");
                    SerializedProperty customObjectsProp = rowProp.FindPropertyRelative("customObjects");

                    // Draw cells - limit to actual column count
                    int cellCount = Mathf.Min(cachedColumnCount, valuesProp.arraySize);
                    for (int j = 0; j < cellCount; j++)
                    {
                        DrawCellData(j, valuesProp, iconsProp, customObjectsProp);
                        if (j < cellCount - 1) { GUILayout.Space(3); }
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(index < cachedRowCount - 1);
        }

        void DrawCellData(int index, SerializedProperty valuesProp, SerializedProperty iconsProp, SerializedProperty customObjectsProp)
        {
            // Use cached column name instead of fetching every frame
            string columnName = columnLabels != null && index < columnLabels.Length ?
                columnLabels[index].text : $"Column {index + 1}";

            SerializedProperty valueProp = valuesProp.GetArrayElementAtIndex(index);
            SerializedProperty iconProp = iconsProp.GetArrayElementAtIndex(index);
            SerializedProperty customObjectProp = customObjectsProp.GetArrayElementAtIndex(index);

            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.BeginContainer(columnName, 3);

            // If custom object is assigned, suggest that text/icon might be ignored
            GUI.enabled = customObjectProp.objectReferenceValue == null;
            EvoEditorGUI.DrawProperty(iconProp, "Icon", null, true, true, true);
            EvoEditorGUI.DrawProperty(valueProp, "Label", null, true, true, true);
            GUI.enabled = true;

            EvoEditorGUI.DrawProperty(customObjectProp, "Custom Object", "Custom GameObject to use for this cell.", false, true, true);

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref listView.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(stylingSourceProp, "Styling Source", "Controls styling source for colors and fonts.", false, false);
                    if (stylingSourceProp.enumValueIndex == 1)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(stylerPresetProp, "Styler Preset", null, false, true, false);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    DrawListViewStyleSection();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawListViewStyleSection()
        {
            if (listViewStyleProp == null)
                return;

            // Use cached isStylerMode
            bool useStyler = isStylerMode;

            // Text Settings
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Text Settings", 3);

            if (useStyler)
            {
                DrawStylerFontDropdown("Header Font", ListViewStyle.Type.HeaderFont);
                DrawStylerFontDropdown("Row Font", ListViewStyle.Type.RowFont);
            }
            else
            {
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerFont"), "Header Font");
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowFont"), "Row Font");
            }

            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerFontSize"), "Header Font Size");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowFontSize"), "Row Font Size");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerFontStyle"), "Header Font Style");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowFontStyle"), "Row Font Style");

            if (useStyler)
            {
                DrawStylerColorDropdown("Header Text Color", ListViewStyle.Type.HeaderTextColor);
                DrawStylerColorDropdown("Row Text Color", ListViewStyle.Type.RowTextColor, false);
            }
            else
            {
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerTextColor"), "Header Text Color");
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowTextColor"), "Row Text Color", null, false);
            }

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground(true);

            // Background Settings
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Background Settings", 3);

            if (useStyler)
            {
                DrawStylerColorDropdown("Header Color", ListViewStyle.Type.HeaderBackgroundColor);
                DrawStylerColorDropdown("Row Color", ListViewStyle.Type.RowBackgroundColor);
            }
            else
            {
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerBackgroundColor"), "Header Color");
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowBackgroundColor"), "Row Color");
            }

            EvoEditorGUI.BeginVerticalBackground();
            var bgSpriteProp = listViewStyleProp.FindPropertyRelative("backgroundSprite");
            EvoEditorGUI.DrawProperty(bgSpriteProp, "Background Sprite", addSpace: false, customBackground: false);
            if (bgSpriteProp.objectReferenceValue != null)
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("ppuMultiplier"), "PPU Multiplier", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground(true);

            EvoEditorGUI.BeginVerticalBackground();
            var useAlternatingProp = listViewStyleProp.FindPropertyRelative("useAlternatingRowColor");
            EvoEditorGUI.DrawToggle(useAlternatingProp, "Alternating Row Color", null, false, true, false, bypassNormalBackground: true);
            if (useAlternatingProp.boolValue)
            {
                EvoEditorGUI.BeginContainer(3);
                if (useStyler) { DrawStylerColorDropdown("Alternating Row Color", ListViewStyle.Type.AlternatingRowColor, false, true); }
                else { EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("alternatingRowColor"), "Color", null, false, true, true); }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground(true);

            EvoEditorGUI.BeginVerticalBackground();
            var showBorderProp = listViewStyleProp.FindPropertyRelative("showBorder");
            EvoEditorGUI.DrawToggle(showBorderProp, "Show Outline", null, false, true, false, bypassNormalBackground: true);
            if (showBorderProp.boolValue)
            {
                EvoEditorGUI.BeginContainer(3);
                if (useStyler) { DrawStylerColorDropdown("Color", ListViewStyle.Type.BorderColor, true, true); }
                else { EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("borderColor"), "Color", null, true, true, true); }
                EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("borderWidth"), "Width", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground(true);

            // Layout Settings
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Layout Settings", 3);
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("headerHeight"), "Header Height");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowHeight"), "Row Height");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("iconSize"), "Icon Size");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("columnSpacing"), "Column Spacing");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("rowSpacing"), "Row Spacing");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("contentSpacing"), "Content Spacing");
            EvoEditorGUI.DrawProperty(listViewStyleProp.FindPropertyRelative("contentPadding"), "Content Padding", null, false, hasFoldout: true);
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground(true);

            // Scroll Settings
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawToggle(enableScrollingProp, "Enable Scrolling", "Wraps rows in a ScrollView while keeping the header fixed.", false, true, false, bypassNormalBackground: true);
            if (enableScrollingProp.boolValue)
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(verticalScrollbarProp, "Scrollbar", "Optional reference to a UI Scrollbar.", true);
                EvoEditorGUI.DrawProperty(scrollSensitivityProp, "Sensitivity", "Scroll wheel sensitivity.", false);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawStylerColorDropdown(string label, ListViewStyle.Type styleType, bool addSpace = true, bool revertColor = false)
        {
            var mapping = GetOrCreateStyleMapping(styleType);
            var colorIDProp = mapping.FindPropertyRelative("colorID");
            StylerEditor.DrawItemDropdown(stylerPresetProp, colorIDProp, Styler.ItemType.Color, label, addSpace, true, revertColor);
        }

        void DrawStylerFontDropdown(string label, ListViewStyle.Type styleType, bool addSpace = true)
        {
            var mapping = GetOrCreateStyleMapping(styleType);
            var fontIDProp = mapping.FindPropertyRelative("fontID");
            StylerEditor.DrawItemDropdown(stylerPresetProp, fontIDProp, Styler.ItemType.Font, label, addSpace);
        }

        void DrawCSVControls()
        {
            GUILayout.BeginHorizontal();
            {
                if (EvoEditorGUI.DrawButton("Import CSV", "Import", height: 24, iconSize: 8))
                {
                    string path = EditorUtility.OpenFilePanel("Import CSV", "", "csv");

                    if (string.IsNullOrEmpty(path))
                    {
                        EvoEditorGUI.BeginCenteredInspector();
                        return;
                    }

                    EvoEditorGUI.BeginCenteredInspector();
                    EvoEditorGUI.BeginCenteredInspector();

                    ImportOptionsWindow.ShowWindow(listView, path);
                }

                EvoEditorGUI.AddLayoutSpace();

                GUI.enabled = listView.columns.Count > 0 && listView.rows.Count > 0;
                if (EvoEditorGUI.DrawButton("Export CSV", "Export", height: 24, iconSize: 8))
                {
                    string defaultName = string.IsNullOrEmpty(listView.gameObject.name)
                        ? "ListView_Export.csv"
                        : $"{listView.gameObject.name}_Export.csv";
                    string path = EditorUtility.SaveFilePanel("Export CSV", "", defaultName, "csv");

                    if (string.IsNullOrEmpty(path))
                    {
                        EvoEditorGUI.BeginCenteredInspector();
                        return;
                    }

                    EvoEditorGUI.BeginCenteredInspector();
                    EvoEditorGUI.BeginCenteredInspector();

                    ExportOptionsWindow.ShowWindow(listView, path);
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref listView.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(parentRect, "Parent Rect",
                        "Optional: RectTransform to automatically resize based on ListView total height.", false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void EnsureRowDataConsistency(SerializedProperty rowProp, int targetCount)
        {
            SerializedProperty valuesProp = rowProp.FindPropertyRelative("values");
            SerializedProperty iconsProp = rowProp.FindPropertyRelative("icons");
            SerializedProperty customObjectsProp = rowProp.FindPropertyRelative("customObjects");

            // Batch resize - only if size differs
            if (valuesProp.arraySize != targetCount)
            {
                int oldSize = valuesProp.arraySize;
                valuesProp.arraySize = targetCount;

                // Initialize only new elements
                for (int i = oldSize; i < targetCount; i++)
                {
                    valuesProp.GetArrayElementAtIndex(i).stringValue = "";
                }
            }

            if (iconsProp.arraySize != targetCount)
            {
                int oldSize = iconsProp.arraySize;
                iconsProp.arraySize = targetCount;

                // Initialize only new elements
                for (int i = oldSize; i < targetCount; i++)
                {
                    iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                }
            }

            if (customObjectsProp.arraySize != targetCount)
            {
                int oldSize = customObjectsProp.arraySize;
                customObjectsProp.arraySize = targetCount;

                // Initialize only new elements
                for (int i = oldSize; i < targetCount; i++)
                {
                    customObjectsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                }
            }
        }

        void AddColumn()
        {
            Undo.RecordObject(listView, "Add Column");

            int newIndex = columnsProp.arraySize;
            columnsProp.arraySize = newIndex + 1;
            SerializedProperty newColumn = columnsProp.GetArrayElementAtIndex(newIndex);

            newColumn.FindPropertyRelative("columnName").stringValue = $"Column {newIndex + 1}";
            newColumn.FindPropertyRelative("width").floatValue = 100f;
            newColumn.FindPropertyRelative("useFlexibleWidth").boolValue = true;
            newColumn.FindPropertyRelative("alignment").enumValueIndex = (int)TextAnchor.MiddleCenter;

            // Only update expanded rows
            for (int i = 0; i < rowsProp.arraySize; i++)
            {
                SerializedProperty rowProp = rowsProp.GetArrayElementAtIndex(i);
                if (rowProp.isExpanded)
                {
                    EnsureRowDataConsistency(rowProp, columnsProp.arraySize);
                }
            }

            labelsNeedUpdate = true;
            EditorRefresh();
        }

        void RemoveColumn(int index)
        {
            if (index < 0 || index >= columnsProp.arraySize)
                return;

            Undo.RecordObject(listView, "Remove Column");
            columnsProp.DeleteArrayElementAtIndex(index);

            // Only update expanded rows
            for (int i = 0; i < rowsProp.arraySize; i++)
            {
                SerializedProperty rowProp = rowsProp.GetArrayElementAtIndex(i);
                if (rowProp.isExpanded)
                {
                    SerializedProperty valuesProp = rowProp.FindPropertyRelative("values");
                    SerializedProperty iconsProp = rowProp.FindPropertyRelative("icons");
                    SerializedProperty customObjectsProp = rowProp.FindPropertyRelative("customObjects");

                    if (index < valuesProp.arraySize) { valuesProp.DeleteArrayElementAtIndex(index); }
                    if (index < iconsProp.arraySize) { iconsProp.DeleteArrayElementAtIndex(index); }
                    if (index < customObjectsProp.arraySize) { customObjectsProp.DeleteArrayElementAtIndex(index); }
                }
            }

            labelsNeedUpdate = true;
            EditorRefresh();
        }

        void AddRow()
        {
            Undo.RecordObject(listView, "Add Row");

            int newIndex = rowsProp.arraySize;
            rowsProp.arraySize = newIndex + 1;
            SerializedProperty newRow = rowsProp.GetArrayElementAtIndex(newIndex);

            EnsureRowDataConsistency(newRow, cachedColumnCount);
            EditorRefresh();
        }

        void RemoveRow(int index)
        {
            if (index < 0 || index >= rowsProp.arraySize)
                return;

            Undo.RecordObject(listView, "Remove Row");
            rowsProp.DeleteArrayElementAtIndex(index);
            EditorRefresh();
        }

        void MoveColumnLeft(int index)
        {
            if (index <= 0 || index >= listView.columns.Count)
                return;

            // Swap columns
            (listView.columns[index - 1], listView.columns[index]) = (listView.columns[index], listView.columns[index - 1]);

            // Swap corresponding data in all rows
            foreach (var row in listView.rows)
            {
                if (index < row.values.Count)
                {
                    (row.values[index - 1], row.values[index]) = (row.values[index], row.values[index - 1]);
                }
                if (index < row.icons.Count)
                {
                    (row.icons[index - 1], row.icons[index]) = (row.icons[index], row.icons[index - 1]);
                }
                if (index < row.customObjects.Count)
                {
                    (row.customObjects[index - 1], row.customObjects[index]) = (row.customObjects[index], row.customObjects[index - 1]);
                }
            }

            listView.isDirty = true;
            labelsNeedUpdate = true;
            EditorRefresh();
        }

        void MoveColumnRight(int index)
        {
            if (index < 0 || index >= listView.columns.Count - 1)
                return;

            // Swap columns
            (listView.columns[index + 1], listView.columns[index]) = (listView.columns[index], listView.columns[index + 1]);

            // Swap corresponding data in all rows
            foreach (var row in listView.rows)
            {
                if (index + 1 < row.values.Count)
                {
                    (row.values[index + 1], row.values[index]) = (row.values[index], row.values[index + 1]);
                }
                if (index + 1 < row.icons.Count)
                {
                    (row.icons[index + 1], row.icons[index]) = (row.icons[index], row.icons[index + 1]);
                }
                if (index + 1 < row.customObjects.Count)
                {
                    (row.customObjects[index + 1], row.customObjects[index]) = (row.customObjects[index], row.customObjects[index + 1]);
                }
            }

            listView.isDirty = true;
            labelsNeedUpdate = true;
            EditorRefresh();
        }

        void MoveRowUp(int index)
        {
            if (index <= 0 || index >= listView.rows.Count)
                return;

            (listView.rows[index - 1], listView.rows[index]) = (listView.rows[index], listView.rows[index - 1]);
            listView.isDirty = true;
            EditorRefresh();
        }

        void MoveRowDown(int index)
        {
            if (index < 0 || index >= listView.rows.Count - 1)
                return;

            (listView.rows[index + 1], listView.rows[index]) = (listView.rows[index], listView.rows[index + 1]);
            listView.isDirty = true;
            EditorRefresh();
        }

        void EditorRefresh()
        {
            if (listView != null && listView.gameObject != null)
            {
                try
                {
                    listView.Refresh();
                    EditorUtility.SetDirty(listView);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"ListView refresh failed: {ex.Message}");
                }
            }
        }

        SerializedProperty GetOrCreateStyleMapping(ListViewStyle.Type styleType)
        {
            // Find existing mapping
            for (int i = 0; i < styleMappingsProp.arraySize; i++)
            {
                var mapping = styleMappingsProp.GetArrayElementAtIndex(i);
                var styleTypeProp = mapping.FindPropertyRelative("type");
                if (styleTypeProp.enumValueIndex == (int)styleType) { return mapping; }
            }

            // Create new mapping
            styleMappingsProp.arraySize++;
            var newMapping = styleMappingsProp.GetArrayElementAtIndex(styleMappingsProp.arraySize - 1);
            newMapping.FindPropertyRelative("type").enumValueIndex = (int)styleType;
            newMapping.FindPropertyRelative("colorID").stringValue = "";
            newMapping.FindPropertyRelative("fontID").stringValue = "";

            return newMapping;
        }

        public class ImportOptionsWindow : EditorWindow
        {
            ListView targetListView;
            string filePath;
            bool hasHeaders = true;
            bool clearExisting = false;

            public static void ShowWindow(ListView listView, string path)
            {
                ImportOptionsWindow window = GetWindow<ImportOptionsWindow>(true, "CSV Import Options", true);
                window.targetListView = listView;
                window.filePath = path;
                window.minSize = new Vector2(320, 150);
                window.maxSize = new Vector2(320, 150);
                window.ShowUtility();
            }

            void OnGUI()
            {
                EditorGUILayout.LabelField("Import Options", EditorStyles.boldLabel);

                hasHeaders = EditorGUILayout.Toggle(
                    new GUIContent("First Row is Headers", "Use the first row as column headers."),
                    hasHeaders
                );

                clearExisting = EditorGUILayout.Toggle(
                    new GUIContent("Clear Existing Data", "Remove all existing columns and rows before import."),
                    clearExisting
                );

                GUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    clearExisting
                        ? "This will replace all current data."
                        : "New columns will be appended if they don't match existing ones.",
                    MessageType.Info
                );

                GUILayout.FlexibleSpace();

                EvoEditorGUI.BeginContainer();
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Import")) { PerformImport(); Close(); }
                    if (GUILayout.Button("Cancel")) { Close(); }
                    GUILayout.EndHorizontal();
                }
                EvoEditorGUI.EndContainer();
            }

            void PerformImport()
            {
                if (targetListView == null)
                {
                    Debug.LogError("Target ListView is null!");
                    return;
                }

                Undo.RecordObject(targetListView, "Import CSV");

                // Temporarily disable auto-refresh to prevent double refresh
                bool wasAutoRefresh = targetListView.autoRefresh;
                targetListView.autoRefresh = false;

                targetListView.LoadFromCSVFile(filePath, hasHeaders, clearExisting);

                // Restore auto-refresh and do a single manual refresh
                targetListView.autoRefresh = wasAutoRefresh;
                targetListView.Refresh();

                EditorUtility.SetDirty(targetListView);
            }
        }

        public class ExportOptionsWindow : EditorWindow
        {
            ListView targetListView;
            string filePath;
            bool includeHeaders = true;

            public static void ShowWindow(ListView listView, string path)
            {
                ExportOptionsWindow window = GetWindow<ExportOptionsWindow>(true, "CSV Export Options", true);
                window.targetListView = listView;
                window.filePath = path;
                window.minSize = new Vector2(320, 130);
                window.maxSize = new Vector2(320, 130);
                window.ShowUtility();
            }

            void OnGUI()
            {
                EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);

                includeHeaders = EditorGUILayout.Toggle(
                    new GUIContent("Include Headers", "Export column names as the first row."),
                    includeHeaders
                );

                GUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    $"Exporting {targetListView.rows.Count} rows and {targetListView.columns.Count} columns.",
                    MessageType.Info
                );

                GUILayout.FlexibleSpace();

                EvoEditorGUI.BeginContainer();
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Export")) { PerformExport(); Close(); }
                    if (GUILayout.Button("Cancel")) { Close(); }
                    GUILayout.EndHorizontal();
                }
                EvoEditorGUI.EndContainer();
            }

            void PerformExport()
            {
                if (targetListView == null)
                {
                    Debug.LogError("Target ListView is null!");
                    return;
                }

                targetListView.SaveToCSVFile(filePath, includeHeaders);
            }
        }
    }
}