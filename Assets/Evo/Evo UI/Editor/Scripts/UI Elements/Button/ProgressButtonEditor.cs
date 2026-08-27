using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ProgressButton))]
    public class ProgressButtonEditor : SelectableEditor
    {
        // Target
        ProgressButton pbTarget;

        // Progress Settings
        SerializedProperty holdDuration;
        SerializedProperty resetDuration;
        SerializedProperty progressCurve;
        SerializedProperty resetCurve;
        SerializedProperty stayOnComplete;
        SerializedProperty completeStateDuration;

        // Visual / Settings
        SerializedProperty progressTransitionDuration;
        SerializedProperty animationType;
        SerializedProperty animationCurve;
        SerializedProperty scaleFrom;
        SerializedProperty slideOffset;

        SerializedProperty progressFill;
        SerializedProperty fillMethod;
        SerializedProperty fillOrigin;
        SerializedProperty clockwise;

        SerializedProperty interactable;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;
        SerializedProperty navigation;

        // References
        SerializedProperty normalStateCG;
        SerializedProperty inProgressStateCG;
        SerializedProperty completeStateCG;
        SerializedProperty disabledCG;
        SerializedProperty normalCG;
        SerializedProperty highlightedCG;
        SerializedProperty pressedCG;
        SerializedProperty selectedCG;

        // Events
        SerializedProperty onClick;
        SerializedProperty onProgressStart;
        SerializedProperty onProgressUpdate;
        SerializedProperty onComplete;
        SerializedProperty onCancel;

        protected override void OnEnable()
        {
            pbTarget = (ProgressButton)target;

            // Progress
            holdDuration = serializedObject.FindProperty("holdDuration");
            resetDuration = serializedObject.FindProperty("resetDuration");
            progressCurve = serializedObject.FindProperty("progressCurve");
            resetCurve = serializedObject.FindProperty("resetCurve");
            stayOnComplete = serializedObject.FindProperty("stayOnComplete");
            completeStateDuration = serializedObject.FindProperty("completeStateDuration");

            // Animation
            progressTransitionDuration = serializedObject.FindProperty("progressTransitionDuration");
            animationType = serializedObject.FindProperty("animationType");
            animationCurve = serializedObject.FindProperty("animationCurve");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            slideOffset = serializedObject.FindProperty("slideOffset");

            // Indicator
            progressFill = serializedObject.FindProperty("progressFill");
            fillMethod = serializedObject.FindProperty("fillMethod");
            fillOrigin = serializedObject.FindProperty("fillOrigin");
            clockwise = serializedObject.FindProperty("clockwise");

            // Settings
            interactable = serializedObject.FindProperty("m_Interactable");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            interactionState = serializedObject.FindProperty("interactionState");
            navigation = serializedObject.FindProperty("m_Navigation");

            // References
            normalStateCG = serializedObject.FindProperty("normalStateCG");
            inProgressStateCG = serializedObject.FindProperty("inProgressStateCG");
            completeStateCG = serializedObject.FindProperty("completeStateCG");
            disabledCG = serializedObject.FindProperty("disabledCG");
            normalCG = serializedObject.FindProperty("normalCG");
            highlightedCG = serializedObject.FindProperty("highlightedCG");
            pressedCG = serializedObject.FindProperty("pressedCG");
            selectedCG = serializedObject.FindProperty("selectedCG");

            // Events
            onClick = serializedObject.FindProperty("onClick");
            onProgressStart = serializedObject.FindProperty("onProgressStart");
            onProgressUpdate = serializedObject.FindProperty("onProgressUpdate");
            onComplete = serializedObject.FindProperty("onComplete");
            onCancel = serializedObject.FindProperty("onCancel");

            EvoEditorGUI.RegisterEditor(this);
            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
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

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawProgressSection();
            DrawSettingsSection();
            DrawStyleSection();
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawProgressSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.progressFoldout, "Progress", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(holdDuration, "Hold Duration", "Time required to hold button.", true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Completion", 3);
                    {
                        EvoEditorGUI.DrawToggle(stayOnComplete, "Stay On Complete", null, false, true);
                        if (!stayOnComplete.boolValue)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(completeStateDuration, "Show Duration", "How long to show the complete state.", false, true);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Curves", 3);
                    {
                        EvoEditorGUI.DrawProperty(resetDuration, "Reset Duration", "Reset animation duration.", true, true, false);
                        EvoEditorGUI.DrawProperty(resetCurve, "Reset Curve", null, true, true);
                        EvoEditorGUI.DrawProperty(progressCurve, "Progress Curve", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Indicator", 3);
                    {
                        EvoEditorGUI.DrawProperty(progressFill, "Fill Image", null, true, true);
                        if (progressFill.objectReferenceValue != null)
                        {
                            EvoEditorGUI.DrawProperty(fillMethod, "Method", null, true, true);
                            EvoEditorGUI.DrawProperty(fillOrigin, "Origin", null, true, true);
                            EvoEditorGUI.DrawToggle(clockwise, "Clockwise", null, false, true);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();

                EvoEditorGUI.DrawToggle(interactable, "Interactable", null, true, true, true);
                EvoEditorGUI.DrawProperty(transitionDuration, "Transition Duration", "Sets the fade transition duration.", true, true, true);
                EvoEditorGUI.DrawProperty(interactionState, "Interaction State", null, true, true, true);

                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.DrawProperty(animationType, "State Animation", "Animation for switching states.", false, false);
                if (animationType.enumValueIndex != 0)
                {
                    EvoEditorGUI.BeginContainer(3);
                    EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, true, true);
                    EvoEditorGUI.DrawProperty(progressTransitionDuration, "Duration", null, false, true);
                    if (animationType.enumValueIndex == 1) // Scale
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.DrawProperty(scaleFrom, "Scale From", null, false, true);
                    }
                    else if (animationType.enumValueIndex == 2) // Slide
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.DrawProperty(slideOffset, "Offset", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                }
                EvoEditorGUI.EndVerticalBackground();

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    InteractiveEditor.DrawRippleEffect(serializedObject);
                    InteractiveEditor.DrawTrailEffect(serializedObject);
                    InteractiveEditor.DrawSoundEffects(serializedObject, Interactive.GetSFXFields());
                    InteractiveEditor.DrawCursorType(serializedObject, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(navigation, "Navigation Mode", null, false, false);
                    UINavigationEditor.DrawVisualizeButton();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();

                // State Groups
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.BeginContainer("State Objects", 3);
                EvoEditorGUI.DrawProperty(normalStateCG, "Normal State", null, true, true, false);
                EvoEditorGUI.DrawProperty(inProgressStateCG, "In Progress", null, true, true, false);
                EvoEditorGUI.DrawProperty(completeStateCG, "Complete", null, false, true, false);
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground();

                EvoEditorGUI.AddLayoutSpace();

                // Interactive Groups
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.BeginContainer("Interaction Objects", 3);
                EvoEditorGUI.DrawProperty(disabledCG, "Disabled", null, true, true, false);
                EvoEditorGUI.DrawProperty(normalCG, "Normal", null, true, true, false);
                EvoEditorGUI.DrawProperty(highlightedCG, "Highlighted", null, true, true, false);
                EvoEditorGUI.DrawProperty(pressedCG, "Pressed", null, true, true, false);
                EvoEditorGUI.DrawProperty(selectedCG, "Selected", null, false, true, false);
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground();

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();

                EvoEditorGUI.DrawProperty(onClick, "On Click", null, true, false);
                EvoEditorGUI.DrawProperty(onProgressStart, "On Progress Start", null, true, false);
                EvoEditorGUI.DrawProperty(onProgressUpdate, "On Progress Update", null, true, false);
                EvoEditorGUI.DrawProperty(onComplete, "On Complete", null, true, false);
                EvoEditorGUI.DrawProperty(onCancel, "On Cancel", null, false, false);

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}