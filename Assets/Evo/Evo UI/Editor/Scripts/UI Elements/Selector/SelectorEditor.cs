using Evo.EditorTools;
using UnityEditor;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CustomEditor(typeof(Selector))]
    public class SelectorEditor : Editor
    {
        // Target
        Selector sTarget;

        // Object
        SerializedProperty selectedIndex;
        SerializedProperty items;

        // Settings
        SerializedProperty loop;
        SerializedProperty invokeAtStart;

        // Animation
        SerializedProperty isHorizontal;
        SerializedProperty invertAnimationDirection;
        SerializedProperty slideOffset;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // Indicator
        SerializedProperty selectedIndicatorAlpha;
        SerializedProperty unselectedIndicatorAlpha;
        SerializedProperty indicatorFadeDuration;

        // References
        SerializedProperty contentParent;
        SerializedProperty contentCanvasGroup;
        SerializedProperty textObject;
        SerializedProperty iconObject;
        SerializedProperty indicatorParent;
        SerializedProperty indicatorPrefab;
        SerializedProperty prevButton;
        SerializedProperty nextButton;

        // Events
        SerializedProperty onSelectionChanged;

        void OnEnable()
        {
            sTarget = (Selector)target;

            selectedIndex = serializedObject.FindProperty("selectedIndex");
            items = serializedObject.FindProperty("items");

            loop = serializedObject.FindProperty("loop");
            invokeAtStart = serializedObject.FindProperty("invokeAtStart");

            isHorizontal = serializedObject.FindProperty("isHorizontal");
            invertAnimationDirection = serializedObject.FindProperty("invertAnimationDirection");
            slideOffset = serializedObject.FindProperty("slideOffset");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            selectedIndicatorAlpha = serializedObject.FindProperty("selectedIndicatorAlpha");
            unselectedIndicatorAlpha = serializedObject.FindProperty("unselectedIndicatorAlpha");
            indicatorFadeDuration = serializedObject.FindProperty("indicatorFadeDuration");

            contentParent = serializedObject.FindProperty("contentParent");
            contentCanvasGroup = serializedObject.FindProperty("contentCanvasGroup");
            textObject = serializedObject.FindProperty("textObject");
            iconObject = serializedObject.FindProperty("iconObject");
            indicatorParent = serializedObject.FindProperty("indicatorParent");
            indicatorPrefab = serializedObject.FindProperty("indicatorPrefab");
            prevButton = serializedObject.FindProperty("prevButton");
            nextButton = serializedObject.FindProperty("nextButton");

            onSelectionChanged = serializedObject.FindProperty("onSelectionChanged");

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
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawItemsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.itemsFoldout, "Items", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (sTarget.items.Count < 0 || sTarget.items.Count <= 0) { EvoEditorGUI.DrawLabel(" Selected Item: None"); }
                        else if (sTarget.items.Count > 0 && !string.IsNullOrEmpty(sTarget.items[sTarget.selectedIndex].label)) { EvoEditorGUI.DrawLabel($" Selected Item: {sTarget.items[sTarget.selectedIndex].label}"); }
                        else if (sTarget.items.Count > 0 && string.IsNullOrEmpty(sTarget.items[sTarget.selectedIndex].label)) { EvoEditorGUI.DrawLabel($" Selected Item: #{sTarget.selectedIndex} (no label)"); }
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawSlider(selectedIndex, 0, sTarget.items.Count > 0 ? sTarget.items.Count - 1 : 0, "Index:", false, false, labelWidth: 40);
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

            if (EvoEditorGUI.DrawFoldout(ref sTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(loop, "Loop", null, true, true, true);
                    EvoEditorGUI.DrawToggle(invokeAtStart, "Invoke At Start", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Animation", padding: 3);
                    {
                        EvoEditorGUI.DrawToggle(isHorizontal, "Is Horizontal", null, true, true);
                        EvoEditorGUI.DrawToggle(invertAnimationDirection, "Invert Direction", null, true, true);
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, true, true);
                        EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Indicator", padding: 3);
                    {
                        EvoEditorGUI.DrawProperty(selectedIndicatorAlpha, "Selected Alpha", null, true, true);
                        EvoEditorGUI.DrawProperty(unselectedIndicatorAlpha, "Unselected Alpha", null, true, true);
                        EvoEditorGUI.DrawProperty(indicatorFadeDuration, "Fade Duration", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, sTarget.gameObject, addSpace: false);
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

            if (EvoEditorGUI.DrawFoldout(ref sTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(contentParent, "Content Parent", null, true, true, true);
                    EvoEditorGUI.DrawProperty(contentCanvasGroup, "Content Canvas Group", null, true, true, true);
                    EvoEditorGUI.DrawProperty(textObject, "Text Object", null, true, true, true);
                    EvoEditorGUI.DrawProperty(iconObject, "Icon Object", null, true, true, true);
                    EvoEditorGUI.DrawProperty(indicatorParent, "Indicator Parent", null, true, true, true);
                    EvoEditorGUI.DrawProperty(indicatorPrefab, "Indicator Prefab", null, true, true, true);
                    EvoEditorGUI.DrawProperty(prevButton, "Prev Button", null, true, true, true);
                    EvoEditorGUI.DrawProperty(nextButton, "Next Button", null, false, true, true);
                }
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
                {
                    EvoEditorGUI.DrawProperty(onSelectionChanged, "On Selection Changed", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}