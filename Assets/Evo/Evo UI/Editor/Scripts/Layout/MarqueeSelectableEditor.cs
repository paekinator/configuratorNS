using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(MarqueeSelectable))]
    public class MarqueeSelectableEditor : Editor
    {
        // Target
        MarqueeSelectable msTarget;

        // Settings
        SerializedProperty interactable;
        SerializedProperty requireSelectionToDrag;
        SerializedProperty selectableType;

        // Interactive Settings
        SerializedProperty targetInteractive;
        SerializedProperty normalState;
        SerializedProperty selectedState;

        // Graphic Settings
        SerializedProperty targetGraphic;

        // 3D Settings
        SerializedProperty targetRenderer;
        SerializedProperty includeChildren;

        // Shared Settings
        SerializedProperty fadeDuration;
        SerializedProperty selectedColor;

        // Events
        SerializedProperty onSelected;
        SerializedProperty onDeselected;

        void OnEnable()
        {
            msTarget = (MarqueeSelectable)target;

            interactable = serializedObject.FindProperty("interactable");
            requireSelectionToDrag = serializedObject.FindProperty("requireSelectionToDrag");
            selectableType = serializedObject.FindProperty("selectableType");

            targetInteractive = serializedObject.FindProperty("targetInteractive");
            normalState = serializedObject.FindProperty("normalState");
            selectedState = serializedObject.FindProperty("selectedState");

            targetGraphic = serializedObject.FindProperty("targetGraphic");

            targetRenderer = serializedObject.FindProperty("targetRenderer");
            includeChildren = serializedObject.FindProperty("includeChildren");

            fadeDuration = serializedObject.FindProperty("fadeDuration");
            selectedColor = serializedObject.FindProperty("selectedColor");

            onSelected = serializedObject.FindProperty("onSelected");
            onDeselected = serializedObject.FindProperty("onDeselected");

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

            DrawSettings();
            DrawEvents();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref msTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(interactable, "Interactable", null, true, true, true);
                    EvoEditorGUI.DrawToggle(requireSelectionToDrag, "Require Selection To Drag", "When enabled, this object can only be drag-moved if it is marquee-selected first.", true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(selectableType, "Selectable Type", null, false, false);
                        EvoEditorGUI.BeginContainer(3);
                        {
                            if (selectableType.enumValueIndex == 0) 
                            { 
                                EvoEditorGUI.DrawProperty(targetInteractive, "Target Interactive", null, true, true);
                                EvoEditorGUI.DrawProperty(normalState, "Normal State", null, true, true);
                                EvoEditorGUI.DrawProperty(selectedState, "Selected State", null, false, true);
                            }
                            else if (selectableType.enumValueIndex == 1)
                            {
                                EvoEditorGUI.DrawProperty(targetGraphic, "Target Graphic", null, true, true);
                                EvoEditorGUI.DrawProperty(selectedColor, "Selected Color", null, true, true);
                                EvoEditorGUI.DrawProperty(fadeDuration, "Fade Duration", null, false, true);
                            }
                            else if (selectableType.enumValueIndex == 2) 
                            {
                                EvoEditorGUI.DrawToggle(includeChildren, "Include Children", null, true, true, true);
                                EvoEditorGUI.DrawProperty(targetRenderer, "Target Renderer", null, true, true);
                                EvoEditorGUI.DrawProperty(selectedColor, "Selected Color", null, true, true);
                                EvoEditorGUI.DrawProperty(fadeDuration, "Fade Duration", null, false, true);
                            }
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEvents()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref msTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onSelected, "On Selected", null, true, false);
                    EvoEditorGUI.DrawProperty(onDeselected, "On Deselected", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}