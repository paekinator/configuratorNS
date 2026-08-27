using UnityEngine;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Pages))]
    public class PaginatorEditor : Editor
    {
        // Target
        Pages tTarget;

        // References
        SerializedProperty container;
        SerializedProperty indicator;

        // Pages
        SerializedProperty defaultPageIndex;
        SerializedProperty pages;

        // Settings
        SerializedProperty disableInvisiblePages;
        SerializedProperty useUnscaledTime;
        SerializedProperty interruptTransitions;
        SerializedProperty autoHandleNestedScrolling;

        // Swipe Settings
        SerializedProperty swipeThreshold;
        SerializedProperty velocityThreshold;
        SerializedProperty elasticResistance;
        SerializedProperty swipeDirection;

        // Animation Settings
        SerializedProperty transitionCurve;
        SerializedProperty transitionDuration;
        SerializedProperty pageSpacing;

        // Events
        SerializedProperty onPageChanged;

        void OnEnable()
        {
            tTarget = (Pages)target;

            container = serializedObject.FindProperty("container");
            indicator = serializedObject.FindProperty("indicator");

            defaultPageIndex = serializedObject.FindProperty("defaultPageIndex");
            pages = serializedObject.FindProperty("pages");

            disableInvisiblePages = serializedObject.FindProperty("disableInvisiblePages");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            interruptTransitions = serializedObject.FindProperty("interruptTransitions");
            autoHandleNestedScrolling = serializedObject.FindProperty("autoHandleNestedScrolling");

            swipeThreshold = serializedObject.FindProperty("swipeThreshold");
            velocityThreshold = serializedObject.FindProperty("velocityThreshold");
            elasticResistance = serializedObject.FindProperty("elasticResistance");
            swipeDirection = serializedObject.FindProperty("swipeDirection");

            transitionCurve = serializedObject.FindProperty("transitionCurve");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            pageSpacing = serializedObject.FindProperty("pageSpacing");

            onPageChanged = serializedObject.FindProperty("onPageChanged");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
        }

        public override void OnInspectorGUI()
        {
            if (!EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID))
            {
                DrawDefaultInspector();
            }
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

            DrawPagesSection();
            DrawReferencesSection();
            DrawSettingsSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawPagesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.objectFoldout, "Pages", EvoEditorGUI.GetIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (Application.isPlaying)
                        {
                            if (tTarget.CurrentPageIndex == -1 || tTarget.pages.Count == 0)
                            {
                                EvoEditorGUI.DrawLabel(" Current Page: None");
                            }
                            else
                            {
                                string pageInfo = tTarget.pages[tTarget.CurrentPageIndex].pageID;
                                if (string.IsNullOrEmpty(pageInfo)) pageInfo = "No ID";
                                EvoEditorGUI.DrawLabel($" Current Page: {pageInfo} (#{tTarget.CurrentPageIndex})");
                            }
                        }
                        else
                        {
                            if (tTarget.pages.Count > 0 && !string.IsNullOrEmpty(tTarget.pages[defaultPageIndex.intValue].pageID))
                            {
                                EvoEditorGUI.DrawLabel($" Selected Page: {tTarget.pages[defaultPageIndex.intValue].pageID}");
                            }
                            else if (tTarget.pages.Count > 0 && string.IsNullOrEmpty(tTarget.pages[defaultPageIndex.intValue].pageID))
                            {
                                EvoEditorGUI.DrawLabel($" Selected Page: #{defaultPageIndex.intValue} (no ID)");
                            }
                            else
                            {
                                EvoEditorGUI.DrawLabel(" Selected Page: None");
                            }

                            EditorGUI.BeginChangeCheck();
                            EvoEditorGUI.DrawSlider(defaultPageIndex, 0, tTarget.pages.Count > 0 ? tTarget.pages.Count - 1 : 0, "Default Page:", false, false, labelWidth: 82);
                            if (EditorGUI.EndChangeCheck())
                            {
                                serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.DrawArrayProperty(pages, "Pages", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(container, "Container", "Parent RectTransform that holds all pages.", true, true, true);
                    EvoEditorGUI.DrawProperty(indicator, "Indicator", "Optional indicator that follows page transitions.", false, true, true);  
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
                    EvoEditorGUI.DrawToggle(disableInvisiblePages, "Disable Invisible Pages", null, true, true, true);
                    EvoEditorGUI.DrawToggle(interruptTransitions, "Interrupt Transitions", "Allow dragging during transitions (interrupt animations).", true, true, true);
                    EvoEditorGUI.DrawToggle(autoHandleNestedScrolling, "Auto Handle Nested Scrolling", "Fixes scrolling issues when page content contains a ScrollRect.", true, true, true);
                    EvoEditorGUI.DrawProperty(pageSpacing, "Page Spacing", "Extra spacing between pages", true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.BeginContainer("Animation Settings", 3);
                        EvoEditorGUI.DrawProperty(transitionCurve, "Transition Curve", null, true, true);
                        EvoEditorGUI.DrawProperty(transitionDuration, "Transition Duration", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.BeginContainer("Swipe Settings", 3);
                        EvoEditorGUI.DrawProperty(swipeThreshold, "Swipe Threshold", "Minimum drag distance (as % of container width) to snap to next page.");
                        EvoEditorGUI.DrawProperty(elasticResistance, "Elastic Resistance", "Elastic resistance when dragging beyond bounds.");
                        EvoEditorGUI.DrawProperty(velocityThreshold, "Velocity Threshold", "Velocity threshold for quick swipes.");
                        EvoEditorGUI.DrawProperty(swipeDirection, "Swipe Direction", null, false);
                        EvoEditorGUI.EndContainer();
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
                EvoEditorGUI.DrawProperty(onPageChanged, "On Page Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}