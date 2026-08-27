using Evo.EditorTools;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [CustomEditor(typeof(Scrollbar))]
    [CanEditMultipleObjects]
    public class ScrollbarEditor : SelectableEditor
    {
        // Target
        Scrollbar sTarget;

        // Settings
        SerializedProperty interactable;
        SerializedProperty value;
        SerializedProperty size;
        SerializedProperty numberOfSteps;
        SerializedProperty direction;
        SerializedProperty smoothDuration;
        SerializedProperty smoothCurve;
        SerializedProperty autoHide;
        SerializedProperty hideAlpha;
        SerializedProperty hideTimer;
        SerializedProperty hideDuration;

        // Style
        SerializedProperty transition;
        SerializedProperty targetGraphic;
        SerializedProperty colorBlock;
        SerializedProperty spriteState;
        SerializedProperty animTrigger;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty handleRect;
        SerializedProperty minArrow;
        SerializedProperty maxArrow;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            sTarget = (Scrollbar)target;

            interactable = serializedObject.FindProperty("m_Interactable");
            value = serializedObject.FindProperty("m_Value");
            size = serializedObject.FindProperty("m_Size");
            numberOfSteps = serializedObject.FindProperty("m_NumberOfSteps");
            direction = serializedObject.FindProperty("m_Direction");
            smoothDuration = serializedObject.FindProperty("smoothDuration");
            smoothCurve = serializedObject.FindProperty("smoothCurve");

            transition = serializedObject.FindProperty("m_Transition");
            targetGraphic = serializedObject.FindProperty("m_TargetGraphic");
            colorBlock = serializedObject.FindProperty("m_Colors");
            spriteState = serializedObject.FindProperty("m_SpriteState");
            animTrigger = serializedObject.FindProperty("m_AnimationTriggers");
            
            navigation = serializedObject.FindProperty("m_Navigation");

            handleRect = serializedObject.FindProperty("m_HandleRect");
            minArrow = serializedObject.FindProperty("minArrow");
            maxArrow = serializedObject.FindProperty("maxArrow");

            autoHide = serializedObject.FindProperty("autoHide");
            hideAlpha = serializedObject.FindProperty("hideAlpha");
            hideDuration = serializedObject.FindProperty("hideDuration");
            hideTimer = serializedObject.FindProperty("hideTimer");

            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);

            // Prepare for UI navigation flow
            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable()
        {
            // Unregister from hover repaints
            EvoEditorGUI.UnregisterEditor(this);

            // Remove from UI navigation flow
            UINavigationEditor.RemoveFromVisualize(this);
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

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawSettingsSection();
            DrawStyleSection();
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(interactable, "Interactable", "Sets whether the slider is interactable or not.", true, true, true);
                    EvoEditorGUI.DrawProperty(value, "Value", null, true, true, true);
                    EvoEditorGUI.DrawProperty(size, "Size", "Sets the handle size. If the object is connected to a ScrollRect, it will be handled automatically.", true, true, true);
                    EvoEditorGUI.DrawProperty(numberOfSteps, "Number Of Steps", null, true, true, true);
                    EvoEditorGUI.DrawProperty(direction, "Direction", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(autoHide, "Auto Hide", "Auto hide scrollbar in case of no interaction.", false, true, true, bypassNormalBackground: true);
                    if (autoHide.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(hideAlpha, "Alpha", null, true);
                        EvoEditorGUI.DrawProperty(hideDuration, "Duration", null, true);
                        EvoEditorGUI.DrawProperty(hideTimer, "Timer", null, false);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Smooth Scrolling", 3);
                    {
                        EvoEditorGUI.DrawProperty(smoothDuration, "Duration", null, true);
                        EvoEditorGUI.DrawProperty(smoothCurve, "Animation Curve", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(transition, "Transition Mode", null, false, false);
                    if (transition.enumValueIndex == 1 || transition.enumValueIndex == 2)
                    {
                        ++EditorGUI.indentLevel;
                        EditorGUILayout.PropertyField(targetGraphic);
                        switch (transition.enumValueIndex)
                        {
                            case 1:
                                EditorGUILayout.PropertyField(colorBlock);
                                break;
                            case 2:
                                EditorGUILayout.PropertyField(spriteState);
                                break;
                            case 3:
                                EditorGUILayout.PropertyField(animTrigger);
                                break;
                        }
                        --EditorGUI.indentLevel;
                    }
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

            if (EvoEditorGUI.DrawFoldout(ref sTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.DrawProperty(navigation, "Navigation Mode", null, false, false);
                UINavigationEditor.DrawVisualizeButton();
                EvoEditorGUI.EndVerticalBackground();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(handleRect, "Handle Rect", null, true, true, true);
                EvoEditorGUI.DrawProperty(minArrow, "Min Arrow", null, true, true, true);
                EvoEditorGUI.DrawProperty(maxArrow, "Max Arrow", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}