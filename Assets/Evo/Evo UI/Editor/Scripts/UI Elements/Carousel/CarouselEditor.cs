using Evo.EditorTools;
using UnityEditor;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CustomEditor(typeof(Carousel))]
    public class CarouselEditor : Editor
    {
        // Target
        Carousel cTarget;

        // Content
        SerializedProperty currentIndex;
        SerializedProperty items;

        // Settings
        SerializedProperty useUnscaledTime;
        SerializedProperty autoSlide;
        SerializedProperty autoSlideTimer;
        SerializedProperty animationCurve;
        SerializedProperty animationDuration;
        SerializedProperty indicatorShrink;
        SerializedProperty slideOffset;

        // References
        SerializedProperty itemParent;
        SerializedProperty itemPreset;
        SerializedProperty indicatorParent;
        SerializedProperty indicatorPreset;

        void OnEnable()
        {
            cTarget = (Carousel)target;

            currentIndex = serializedObject.FindProperty("currentIndex");
            items = serializedObject.FindProperty("items");

            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            autoSlide = serializedObject.FindProperty("autoSlide");
            autoSlideTimer = serializedObject.FindProperty("autoSlideTimer");
            animationCurve = serializedObject.FindProperty("animationCurve");
            animationDuration = serializedObject.FindProperty("animationDuration");
            indicatorShrink = serializedObject.FindProperty("indicatorShrink");
            slideOffset = serializedObject.FindProperty("slideOffset");

            itemParent = serializedObject.FindProperty("itemParent");
            itemPreset = serializedObject.FindProperty("itemPreset");
            indicatorParent = serializedObject.FindProperty("indicatorParent");
            indicatorPreset = serializedObject.FindProperty("indicatorPreset");

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

            DrawContentSection();
            DrawSettingsSection();
            DrawReferencesSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawContentSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.contentFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (cTarget.currentIndex < 0 || cTarget.items.Count <= 0) { EvoEditorGUI.DrawLabel(" Selected Item: None"); }
                        else if (cTarget.items.Count > 0 && !string.IsNullOrEmpty(cTarget.items[cTarget.currentIndex].title)) { EvoEditorGUI.DrawLabel($" Selected Item: {cTarget.items[cTarget.currentIndex].title}"); }
                        else if (cTarget.items.Count > 0 && string.IsNullOrEmpty(cTarget.items[cTarget.currentIndex].title)) { EvoEditorGUI.DrawLabel($" Selected Item: #{cTarget.currentIndex} (no label)"); }
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawSlider(currentIndex, 0, cTarget.items.Count > 0 ? cTarget.items.Count - 1 : 0, "Index:", false, false, labelWidth: 40);
                        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
                    }
                    EvoEditorGUI.EndHorizontalBackground();
                    EvoEditorGUI.DrawArrayProperty(items, "Items", null, true, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(autoSlide, "Auto Slide", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (autoSlide.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(autoSlideTimer, "Timer", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground();
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, cTarget.gameObject, addSpace: true);
#endif
                    EvoEditorGUI.DrawProperty(animationCurve, "Animation Curve", null, true, true, true);
                    EvoEditorGUI.DrawProperty(animationDuration, "Animation Duration", null, true, true, true);
                    EvoEditorGUI.DrawProperty(indicatorShrink, "Indicator Shrink", null, true, true, true);
                    EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(itemParent, "Item Parent", null, true, true, true);
                EvoEditorGUI.DrawProperty(itemPreset, "Item Preset", null, true, true, true);
                EvoEditorGUI.DrawProperty(indicatorParent, "Indicator Parent", null, true, true, true);
                EvoEditorGUI.DrawProperty(indicatorPreset, "Indicator Preset", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}