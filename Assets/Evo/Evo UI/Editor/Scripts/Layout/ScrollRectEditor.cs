using Evo.EditorTools;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScrollRect), true)]
    public class ScrollRectEditor : Editor
    {
        // Target
        ScrollRect srTarget;

        // Native properties
        SerializedProperty content;
        SerializedProperty horizontal;
        SerializedProperty vertical;
        SerializedProperty movementType;
        SerializedProperty elasticity;
        SerializedProperty inertia;
        SerializedProperty decelerationRate;
        SerializedProperty scrollSensitivity;
        SerializedProperty viewport;
        SerializedProperty horizontalScrollbar;
        SerializedProperty horizontalScrollbarVisibility;
        SerializedProperty horizontalScrollbarSpacing;
        SerializedProperty verticalScrollbar;
        SerializedProperty verticalScrollbarVisibility;
        SerializedProperty verticalScrollbarSpacing;
        SerializedProperty onValueChanged;

        // Snapping
        SerializedProperty enableSnapping;
        SerializedProperty snapDuration;
        SerializedProperty snapCurve;
        SerializedProperty disableUnfocused;

        // Scaling
        SerializedProperty enableScaling;
        SerializedProperty scaleDistance;
        SerializedProperty minScale;

        // Fading
        SerializedProperty enableFading;
        SerializedProperty fadeDistance;
        SerializedProperty minAlpha;

        // Events
        SerializedProperty onItemFocused;

        void OnEnable()
        {
            srTarget = (ScrollRect)target;

            content = serializedObject.FindProperty("m_Content");
            horizontal = serializedObject.FindProperty("m_Horizontal");
            vertical = serializedObject.FindProperty("m_Vertical");
            movementType = serializedObject.FindProperty("m_MovementType");
            elasticity = serializedObject.FindProperty("m_Elasticity");
            inertia = serializedObject.FindProperty("m_Inertia");
            decelerationRate = serializedObject.FindProperty("m_DecelerationRate");
            scrollSensitivity = serializedObject.FindProperty("m_ScrollSensitivity");
            viewport = serializedObject.FindProperty("m_Viewport");
            horizontalScrollbar = serializedObject.FindProperty("m_HorizontalScrollbar");
            horizontalScrollbarVisibility = serializedObject.FindProperty("m_HorizontalScrollbarVisibility");
            horizontalScrollbarSpacing = serializedObject.FindProperty("m_HorizontalScrollbarSpacing");
            verticalScrollbar = serializedObject.FindProperty("m_VerticalScrollbar");
            verticalScrollbarVisibility = serializedObject.FindProperty("m_VerticalScrollbarVisibility");
            verticalScrollbarSpacing = serializedObject.FindProperty("m_VerticalScrollbarSpacing");
            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

            enableSnapping = serializedObject.FindProperty("enableSnapping");
            snapDuration = serializedObject.FindProperty("snapDuration");
            snapCurve = serializedObject.FindProperty("snapCurve");
            disableUnfocused = serializedObject.FindProperty("disableUnfocused");

            enableScaling = serializedObject.FindProperty("enableScaling");
            scaleDistance = serializedObject.FindProperty("scaleDistance");
            minScale = serializedObject.FindProperty("minScale");

            enableFading = serializedObject.FindProperty("enableFading");
            fadeDistance = serializedObject.FindProperty("fadeDistance");
            minAlpha = serializedObject.FindProperty("minAlpha");

            onItemFocused = serializedObject.FindProperty("onItemFocused");

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

            DrawSettingsSection();
            DrawReferencesSection();
            DrawStyleSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref srTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(scrollSensitivity, "Scroll Sensitivity", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(movementType, "Movement Type", null, false, false);
                        if (movementType.enumValueIndex == 1)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(elasticity, "Elasticity", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(horizontal, "Is Horizontal", null, true, true, true);
                    EvoEditorGUI.DrawToggle(vertical, "Is Vertical", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(inertia, "Inertia", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (inertia.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(decelerationRate, "Deceleration Rate", null, false, true);
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

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref srTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(enableSnapping, "Enable Snapping", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (enableSnapping.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(snapDuration, "Snap Duration", null, true, true);
                            EvoEditorGUI.DrawProperty(snapCurve, "Snap Curve", null, true, true);
                            EvoEditorGUI.DrawToggle(disableUnfocused, "Disable Unfocused", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(enableFading, "Fading", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (enableFading.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(minAlpha, "Min Alpha", null, true, true);
                            EvoEditorGUI.DrawProperty(fadeDistance, "Fade Distance", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(enableScaling, "Scaling", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (enableScaling.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(minScale, "Min Scale", null, true, true);
                            EvoEditorGUI.DrawProperty(scaleDistance, "Scale Distance", null, false, true);
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

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref srTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(content, "Content", null, true, true, true);
                    EvoEditorGUI.DrawProperty(viewport, "Viewport", null, false, true, true);
                    if (horizontal.boolValue)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        {
                            EvoEditorGUI.DrawProperty(horizontalScrollbar, "Horizontal Scrollbar", null, false, false);
                            if (horizontalScrollbar.objectReferenceValue != null)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                EvoEditorGUI.DrawProperty(horizontalScrollbarVisibility, "Visibility", null, false, true);
                                if (verticalScrollbarVisibility.enumValueIndex == 2)
                                {
                                    EvoEditorGUI.AddLayoutSpace();
                                    EvoEditorGUI.DrawProperty(horizontalScrollbarSpacing, "Spacing", null, false, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                    }
                    if (vertical.boolValue)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        {
                            EvoEditorGUI.DrawProperty(verticalScrollbar, "Vertical Scrollbar", null, false, false);
                            if (verticalScrollbar.objectReferenceValue != null)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                EvoEditorGUI.DrawProperty(verticalScrollbarVisibility, "Visibility", null, false, true);
                                if (verticalScrollbarVisibility.enumValueIndex == 2)
                                {
                                    EvoEditorGUI.AddLayoutSpace();
                                    EvoEditorGUI.DrawProperty(verticalScrollbarSpacing, "Spacing", null, false, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref srTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                    EvoEditorGUI.DrawProperty(onItemFocused, "On Item Focused", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}