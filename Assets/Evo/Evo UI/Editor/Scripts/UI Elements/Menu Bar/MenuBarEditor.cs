using Evo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Evo.UI
{
    [CustomEditor(typeof(MenuBar))]
    public class MenuBarEditor : Editor
    {
        // Target
        MenuBar mbTarget;

        // Item List
        SerializedProperty items;

        // Settings
        SerializedProperty openMenuOnHover;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty itemMenuOffset;

        // Animation
        SerializedProperty animationType;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty scaleFrom;
        SerializedProperty slideOffset;

        // References
        SerializedProperty itemParent;
        SerializedProperty itemPreset;
        SerializedProperty contextMenuPreset;

        void OnEnable()
        {
            mbTarget = (MenuBar)target;

            items = serializedObject.FindProperty("items");

            openMenuOnHover = serializedObject.FindProperty("openMenuOnHover");
            blockUIWhileOpen = serializedObject.FindProperty("blockUIWhileOpen");
            itemMenuOffset = serializedObject.FindProperty("itemMenuOffset");

            animationType = serializedObject.FindProperty("animationType");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            slideOffset = serializedObject.FindProperty("slideOffset");

            itemParent = serializedObject.FindProperty("itemParent");
            itemPreset = serializedObject.FindProperty("itemPreset");
            contextMenuPreset = serializedObject.FindProperty("contextMenuPreset");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
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

            DrawItemListSection();
            DrawSettingsSection();
            DrawReferencesSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawItemListSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mbTarget.contentFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawArrayProperty(items, "Items", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mbTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(openMenuOnHover, "Open Menu On Hover", null, true, true, true);
                    EvoEditorGUI.DrawToggle(blockUIWhileOpen, "Block UI While Open", "When enabled, bar items won't require initial click to open the menu.", true, true, true);
                    EvoEditorGUI.DrawProperty(itemMenuOffset, "Item Menu Offset", "Offset position for the dropdown menu relative to each menu item.", true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                    if (animationType.enumValueIndex != 0)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false, true);
                        if (animationType.enumValueIndex == 2)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(scaleFrom, "Scale From", null, false, true);
                        }
                        if (animationType.enumValueIndex == 3)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, mbTarget.gameObject, addSpace: false);
#endif
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mbTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(itemParent, "Item Parent", null, true, true, true);
                EvoEditorGUI.DrawProperty(itemPreset, "Item Preset", null, true, true, true);
                EvoEditorGUI.DrawProperty(contextMenuPreset, "Context Menu Preset", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}