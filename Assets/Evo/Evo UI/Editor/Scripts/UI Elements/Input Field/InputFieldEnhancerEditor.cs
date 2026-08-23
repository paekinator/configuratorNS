using Evo.EditorTools;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CustomEditor(typeof(InputFieldEnhancer))]
    public class InputFieldEnhancerEditor : Editor
    {
        // Target
        InputFieldEnhancer ifTarget;

        // Settings
        SerializedProperty clearAfterSubmit;
        SerializedProperty deselectOnEndEdit;
        SerializedProperty handleShiftEnter;
        SerializedProperty animationType;
        SerializedProperty slideOffset;
        SerializedProperty fadeAlpha;
        SerializedProperty scaleMultiplier;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // References
        SerializedProperty source;
        SerializedProperty interactableObject;

        // Events
        SerializedProperty onSubmit;

        // Helpers
        bool sourceFoldout = true;


        void OnEnable()
        {
            ifTarget = (InputFieldEnhancer)target;

            clearAfterSubmit = serializedObject.FindProperty("clearAfterSubmit");
            deselectOnEndEdit = serializedObject.FindProperty("deselectOnEndEdit");
            handleShiftEnter = serializedObject.FindProperty("handleShiftEnter");
            animationType = serializedObject.FindProperty("animationType");
            slideOffset = serializedObject.FindProperty("slideOffset");
            fadeAlpha = serializedObject.FindProperty("fadeAlpha");
            scaleMultiplier = serializedObject.FindProperty("scaleMultiplier");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            source = serializedObject.FindProperty("source");
            interactableObject = serializedObject.FindProperty("interactableObject");

            onSubmit = serializedObject.FindProperty("onSubmit");

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

            if (source.objectReferenceValue) { DrawSource(); }
            DrawSettings();
            DrawReferences();
            DrawEvents();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSource()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref sourceFoldout, "Input Field", EvoEditorGUI.GetIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawInfoBox("You can select the input field object to customize the base TMP Input Field options.", revertBackgroundColor: true);
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Configure TMP Input Field", revertBackgroundColor: true))
                    {
                        Selection.activeObject = source.objectReferenceValue;
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref ifTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(clearAfterSubmit, "Clear After Submit", null, true, true, true);
                    EvoEditorGUI.DrawToggle(deselectOnEndEdit, "Deselect On End Edit", "Deselects Input Field from EventSystem.current on end edit.", true, true, true);
                    EvoEditorGUI.DrawToggle(handleShiftEnter, "Handle Shift+Enter", "Changes multi-line behavior for Shift+Enter and submit combo.", true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                        EvoEditorGUI.BeginContainer(3);
                        {
                            if (animationType.enumValueIndex == 0) { EvoEditorGUI.DrawProperty(fadeAlpha, "Fade Alpha", null, false, true); }
                            else if (animationType.enumValueIndex == 1)
                            {
                                EvoEditorGUI.DrawProperty(fadeAlpha, "Fade Alpha", null, true, true);
                                EvoEditorGUI.DrawProperty(scaleMultiplier, "Scale Multiplier", null, false, true);
                            }
                            else if (animationType.enumValueIndex == 2) { EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true); }
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationDuration, "Animation Duration", null, true, true, true);
                    EvoEditorGUI.DrawProperty(animationCurve, "Animation Curve", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref ifTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(source, "Source Input", null, true, true, true);
                    EvoEditorGUI.DrawProperty(interactableObject, "Interactable Object", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEvents()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref ifTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onSubmit, "On Submit", null, true, false);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}