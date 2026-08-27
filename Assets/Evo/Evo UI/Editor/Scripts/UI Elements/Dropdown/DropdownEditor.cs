using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Dropdown))]
    public class DropdownEditor : Editor
    {
        // Target
        Dropdown dTarget;

        // Object
        SerializedProperty selectedIndex;
        SerializedProperty items;

        // Item Layout
        SerializedProperty itemSpacing;
        SerializedProperty itemHeight;
        SerializedProperty padding;

        // Settings
        SerializedProperty scrollbarPosition;
        SerializedProperty maxHeight;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty closeOnItemSelect;
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
        SerializedProperty onItemSelected;
        SerializedProperty onOpen;
        SerializedProperty onClose;

        void OnEnable()
        {
            dTarget = (Dropdown)target;

            selectedIndex = serializedObject.FindProperty("selectedIndex");
            items = serializedObject.FindProperty("items");

            itemSpacing = serializedObject.FindProperty("itemSpacing");
            itemHeight = serializedObject.FindProperty("itemHeight");
            padding = serializedObject.FindProperty("padding");

            closeOnClickOutside = serializedObject.FindProperty("closeOnClickOutside");
            maxHeight = serializedObject.FindProperty("maxHeight");
            blockUIWhileOpen = serializedObject.FindProperty("blockUIWhileOpen");
            closeOnItemSelect = serializedObject.FindProperty("closeOnItemSelect");
            scrollbarPosition = serializedObject.FindProperty("scrollbarPosition");
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

            onItemSelected = serializedObject.FindProperty("onItemSelected");
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
                        if (dTarget.selectedIndex < 0 || dTarget.items.Count <= 0) { EvoEditorGUI.DrawLabel(" Selected Item: None"); }
                        else if (dTarget.items.Count > 0 && !string.IsNullOrEmpty(dTarget.items[dTarget.selectedIndex].label)) { EvoEditorGUI.DrawLabel($" Selected Item: {dTarget.items[dTarget.selectedIndex].label}"); }
                        else if (dTarget.items.Count > 0 && string.IsNullOrEmpty(dTarget.items[dTarget.selectedIndex].label)) { EvoEditorGUI.DrawLabel($" Selected Item: #{dTarget.selectedIndex} (no label)"); }
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawSlider(selectedIndex, -1, dTarget.items.Count > 0 ? dTarget.items.Count - 1 : 0, "Index:", false, false, labelWidth: 40);
                        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
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
                    EvoEditorGUI.DrawToggle(closeOnItemSelect, "Close On Item Select", null, true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnClickOutside, "Close On Click Outside", null, true, true, true);
                    
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

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, dTarget.gameObject, addSpace: false);
#endif
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
                    EvoEditorGUI.DrawProperty(onItemSelected, "On Item Selected", null, true, false);
                    EvoEditorGUI.DrawProperty(onOpen, "On Open", null, true, false);
                    EvoEditorGUI.DrawProperty(onClose, "On Close", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}