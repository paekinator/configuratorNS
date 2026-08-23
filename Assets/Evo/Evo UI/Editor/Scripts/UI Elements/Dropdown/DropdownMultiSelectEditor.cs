using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(DropdownMultiSelect))]
    public class DropdownMultiSelectEditor : Editor
    {
        // Target
        DropdownMultiSelect dTarget;

        // Object
        SerializedProperty items;

        // Header
        SerializedProperty headerPlaceholder;
        SerializedProperty headerFormat;
        SerializedProperty maxDisplayCount;
        SerializedProperty countSuffix;

        // Item Layout
        SerializedProperty itemSpacing;
        SerializedProperty itemHeight;
        SerializedProperty padding;

        // Settings
        SerializedProperty scrollbarPosition;
        SerializedProperty maxHeight;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty closeOnClickOutside;
        SerializedProperty rotateArrow;
        SerializedProperty arrowRotation;

        // Animation
        SerializedProperty animationType;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // References
        SerializedProperty itemPrefab;
        SerializedProperty itemParent;
        SerializedProperty headerButton;
        SerializedProperty headerArrow;
        SerializedProperty scrollRect;
        SerializedProperty canvasGroup;

        // Events
        SerializedProperty onItemToggled;
        SerializedProperty onSelectionChanged;
        SerializedProperty onOpen;
        SerializedProperty onClose;

        void OnEnable()
        {
            dTarget = (DropdownMultiSelect)target;

            items = serializedObject.FindProperty("items");

            headerPlaceholder = serializedObject.FindProperty("headerPlaceholder");
            headerFormat = serializedObject.FindProperty("headerFormat");
            maxDisplayCount = serializedObject.FindProperty("maxDisplayCount");
            countSuffix = serializedObject.FindProperty("countSuffix");

            itemSpacing = serializedObject.FindProperty("itemSpacing");
            itemHeight = serializedObject.FindProperty("itemHeight");
            padding = serializedObject.FindProperty("padding");

            scrollbarPosition = serializedObject.FindProperty("scrollbarPosition");
            maxHeight = serializedObject.FindProperty("maxHeight");
            blockUIWhileOpen = serializedObject.FindProperty("blockUIWhileOpen");
            closeOnClickOutside = serializedObject.FindProperty("closeOnClickOutside");
            rotateArrow = serializedObject.FindProperty("rotateArrow");
            arrowRotation = serializedObject.FindProperty("arrowRotation");

            animationType = serializedObject.FindProperty("animationType");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            itemPrefab = serializedObject.FindProperty("itemPrefab");
            itemParent = serializedObject.FindProperty("itemParent");
            headerButton = serializedObject.FindProperty("headerButton");
            headerArrow = serializedObject.FindProperty("headerArrow");
            scrollRect = serializedObject.FindProperty("scrollRect");
            canvasGroup = serializedObject.FindProperty("canvasGroup");

            onItemToggled = serializedObject.FindProperty("onItemToggled");
            onSelectionChanged = serializedObject.FindProperty("onSelectionChanged");
            onOpen = serializedObject.FindProperty("onOpen");
            onClose = serializedObject.FindProperty("onClose");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            // Unregister from hover repaints
            EvoEditorGUI.UnregisterEditor(this);
        }

        public override void OnInspectorGUI()
        {
            if (!EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID)) { DrawDefaultInspector(); }
            else
            {
                DrawCustomGUI();
                EvoEditorGUI.HandleInspectorGUI();
            }
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawItemsSection();
            DrawSettingsSection();
            if (headerButton.objectReferenceValue != null) { DrawNavigationSection(); }
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawItemsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dTarget.itemsFoldout, "Items", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (!Application.isPlaying || dTarget.SelectedIndices.Count == 0)
                        {
                            EvoEditorGUI.DrawLabel(" Selected Items: None");
                        }
                        else
                        {
                            int count = dTarget.SelectedIndices.Count;
                            EvoEditorGUI.DrawLabel($" Selected Items: {count} item{(count != 1 ? "s" : "")}");
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.DrawArrayProperty(items, "Items", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(scrollbarPosition, "Scrollbar Position", null, true, true, true);
                    EvoEditorGUI.DrawToggle(blockUIWhileOpen, "Block UI While Open", null, true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnClickOutside, "Close On Click Outside", null, true, true, true);

                    // Header format sub-section
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Header Format", padding: 3);
                    {
                        EvoEditorGUI.DrawProperty(headerPlaceholder, "Placeholder", null, true, true);
                        EvoEditorGUI.DrawProperty(headerFormat, "Format", null, true, true);

                        // maxDisplayCount is only meaningful for CommaSeparated and Adaptive
                        if (headerFormat.enumValueIndex != 1)
                        {
                            EvoEditorGUI.DrawProperty(maxDisplayCount, "Max Display Count", null, true, true);
                        }

                        EvoEditorGUI.DrawProperty(countSuffix, "Count Suffix", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(rotateArrow, "Rotate Arrow", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (rotateArrow.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(arrowRotation, "Arrow Rotation", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Layout", padding: 3);
                    {
                        EvoEditorGUI.DrawProperty(maxHeight, "Max Height", null, true, true);
                        EvoEditorGUI.DrawProperty(itemHeight, "Item Height", null, true, true);
                        EvoEditorGUI.DrawProperty(itemSpacing, "Item Spacing", null, true, true);
                        EvoEditorGUI.DrawArrayProperty(padding, "Item Padding", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                    EvoEditorGUI.BeginContainer(3);
                    EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true, true);
                    if (animationType.enumValueIndex != 0) { EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false, true); }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawInfoBox("UI Navigation is handled by the header button.", revertBackgroundColor: true);
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Select Header Button", revertBackgroundColor: true))
                    {
                        dTarget.headerButton.navigationFoldout = true;
                        Selection.activeObject = headerButton.objectReferenceValue;
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(itemPrefab, "Item Prefab", null, true, true, true);
                    EvoEditorGUI.DrawProperty(itemParent, "Item Parent", null, true, true, true);
                    EvoEditorGUI.DrawProperty(headerButton, "Header Button", null, true, true, true);
                    EvoEditorGUI.DrawProperty(headerArrow, "Header Arrow", null, true, true, true);
                    EvoEditorGUI.DrawProperty(scrollRect, "Scroll Rect", null, true, true, true);
                    EvoEditorGUI.DrawProperty(canvasGroup, "Canvas Group", null, true, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onItemToggled, "On Item Toggled", null, true, false);
                    EvoEditorGUI.DrawProperty(onSelectionChanged, "On Selection Changed", null, true, false);
                    EvoEditorGUI.DrawProperty(onOpen, "On Open", null, true, false);
                    EvoEditorGUI.DrawProperty(onClose, "On Close", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}