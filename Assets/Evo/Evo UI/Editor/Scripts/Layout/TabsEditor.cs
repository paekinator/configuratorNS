using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Tabs))]
    public class TabsEditor : Editor
    {
        // Target
        Tabs tTarget;

        // Tabs
        SerializedProperty defaultTabIndex;
        SerializedProperty tabs;

        // Settings
        SerializedProperty disableInvisibleTabs;
        SerializedProperty useUnscaledTime;

        // Animation Settings
        SerializedProperty animationType;
        SerializedProperty animationCurve;
        SerializedProperty animationDuration;
        SerializedProperty scaleOutMultiplier;
        SerializedProperty scaleInMultiplier;
        SerializedProperty slideDistance;

        // Title Display
        SerializedProperty titleObject;
        SerializedProperty animateTitleDirectionally;
        SerializedProperty titleSlideCurve;
        SerializedProperty titleSlideDuration;
        SerializedProperty titleChangeDelay;
        SerializedProperty titleSlideOffset;

        // Indicator
        SerializedProperty indicatorObject;
        SerializedProperty indicatorDirection;
        SerializedProperty indicatorAutoSize;
        SerializedProperty indicatorStretch;
        SerializedProperty indicatorCurve;
        SerializedProperty indicatorDuration;

        // Events
        SerializedProperty onTabChanged;

        void OnEnable()
        {
            tTarget = (Tabs)target;

            defaultTabIndex = serializedObject.FindProperty("defaultTabIndex");
            tabs = serializedObject.FindProperty("tabs");

            disableInvisibleTabs = serializedObject.FindProperty("disableInvisibleTabs");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");

            animationType = serializedObject.FindProperty("animationType");
            animationCurve = serializedObject.FindProperty("animationCurve");
            animationDuration = serializedObject.FindProperty("animationDuration");
            scaleOutMultiplier = serializedObject.FindProperty("scaleOutMultiplier");
            scaleInMultiplier = serializedObject.FindProperty("scaleInMultiplier");
            slideDistance = serializedObject.FindProperty("slideDistance");

            titleObject = serializedObject.FindProperty("titleObject");
            animateTitleDirectionally = serializedObject.FindProperty("animateTitleDirectionally");
            titleSlideCurve = serializedObject.FindProperty("titleSlideCurve");
            titleSlideDuration = serializedObject.FindProperty("titleSlideDuration");
            titleChangeDelay = serializedObject.FindProperty("titleChangeDelay");
            titleSlideOffset = serializedObject.FindProperty("titleSlideOffset");

            indicatorObject = serializedObject.FindProperty("indicatorObject");
            indicatorDirection = serializedObject.FindProperty("indicatorDirection");
            indicatorAutoSize = serializedObject.FindProperty("indicatorAutoSize");
            indicatorStretch = serializedObject.FindProperty("indicatorStretch");
            indicatorCurve = serializedObject.FindProperty("indicatorCurve");
            indicatorDuration = serializedObject.FindProperty("indicatorDuration");

            onTabChanged = serializedObject.FindProperty("onTabChanged");

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

            DrawTabsSection();
            DrawSettingsSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawTabsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.objectFoldout, "Tabs", EvoEditorGUI.GetIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (Application.isPlaying)
                        {
                            if (tTarget.CurrentTabIndex == -1 || tTarget.tabs.Count == 0) { EvoEditorGUI.DrawLabel(" Current Tab: None"); }
                            else { EvoEditorGUI.DrawLabel($" Current Tab: {tTarget.tabs[tTarget.CurrentTabIndex].tabID} (#{tTarget.CurrentTabIndex})"); }
                        }
                        else
                        {
                            if (tTarget.tabs.Count > 0 && !string.IsNullOrEmpty(tTarget.tabs[defaultTabIndex.intValue].tabID)) { EvoEditorGUI.DrawLabel($" Selected Tab: {tTarget.tabs[defaultTabIndex.intValue].tabID}"); }
                            else if (tTarget.tabs.Count > 0 && string.IsNullOrEmpty(tTarget.tabs[defaultTabIndex.intValue].tabID)) { EvoEditorGUI.DrawLabel($" Selected Tab: #{defaultTabIndex.intValue} (no ID)"); }
                            else { EvoEditorGUI.DrawLabel(" Selected Tab: None"); }
                            EditorGUI.BeginChangeCheck();
                            EvoEditorGUI.DrawSlider(defaultTabIndex, 0, tTarget.tabs.Count > 0 ? tTarget.tabs.Count - 1 : 0, "Default Tab:", false, false, labelWidth: 75);
                            if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.DrawArrayProperty(tabs, "Tabs", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref tTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.DrawToggle(disableInvisibleTabs, "Disable Invisible Tabs", null, true, true, true);

                    // Animation Settings
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                        if (animationType.enumValueIndex != 0)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, true, true);
                            EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, animationType.enumValueIndex != 1, true);
                            if (animationType.enumValueIndex == 2)
                            {
                                EvoEditorGUI.DrawProperty(scaleOutMultiplier, "Scale Out Multiplier", null, true, true);
                                EvoEditorGUI.DrawProperty(scaleInMultiplier, "Scale In Multiplier ", null, false, true);
                            }
                            else if (animationType.enumValueIndex == 3 || animationType.enumValueIndex == 4)
                            {
                                EvoEditorGUI.DrawProperty(slideDistance, "Slide Distance", null, false, true);
                            }
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    // Title Display
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(titleObject, "Title Object", null, false, false);
                        if (titleObject.objectReferenceValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawToggle(animateTitleDirectionally, "Animate Directionally", null, true, true);
                            EvoEditorGUI.DrawProperty(titleSlideCurve, "Animation Curve", null, true, true);
                            EvoEditorGUI.DrawProperty(titleSlideDuration, "Animation Duration", null, true, true);
                            EvoEditorGUI.DrawProperty(titleChangeDelay, "Change Delay", "Add a delay before setting the new tab title.", true, true);
                            EvoEditorGUI.DrawProperty(titleSlideOffset, "Animation Offset", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    // Indicator
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(indicatorObject, "Indicator Object", null, false, false);
                        if (indicatorObject.objectReferenceValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawToggle(indicatorAutoSize, "Auto Size", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorDirection, "Direction", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorStretch, "Elastic Stretch", "Amount of stretch effect during animation.", true, true);
                            EvoEditorGUI.DrawProperty(indicatorCurve, "Animation Curve", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorDuration, "Animation Duration", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref tTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onTabChanged, "On Tab Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}