using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(QuantitySelector))]
    public class QuantitySelectorEditor : Editor
    {
        // Target
        QuantitySelector sTarget;

        // UI References
        SerializedProperty inputField;
        SerializedProperty increaseButton;
        SerializedProperty decreaseButton;

        // Settings
        SerializedProperty minQuantity;
        SerializedProperty maxQuantity;
        SerializedProperty startQuantity;

        // Animation
        SerializedProperty slideOffset;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // Events
        SerializedProperty onValueChanged;

        void OnEnable()
        {
            sTarget = (QuantitySelector)target;

            inputField = serializedObject.FindProperty("inputField");
            increaseButton = serializedObject.FindProperty("increaseButton");
            decreaseButton = serializedObject.FindProperty("decreaseButton");

            minQuantity = serializedObject.FindProperty("minQuantity");
            maxQuantity = serializedObject.FindProperty("maxQuantity");
            startQuantity = serializedObject.FindProperty("startQuantity");

            slideOffset = serializedObject.FindProperty("slideOffset");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            onValueChanged = serializedObject.FindProperty("onValueChanged");

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

            DrawObjectSection();
            DrawSettingsSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.objectFoldout, "Quantity", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Core Settings
                    EvoEditorGUI.DrawProperty(startQuantity, "Start Quantity", null, true, true, true);
                    EvoEditorGUI.DrawProperty(minQuantity, "Min Quantity", null, true, true, true);
                    EvoEditorGUI.DrawProperty(maxQuantity, "Max Quantity", null, false, true, true);
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
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Animation", padding: 3);
                    {
                        EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, true, true);
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
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

            if (EvoEditorGUI.DrawFoldout(ref sTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(inputField, "Input Field", null, true, true, true);
                    EvoEditorGUI.DrawProperty(increaseButton, "Increase Btn", null, true, true, true);
                    EvoEditorGUI.DrawProperty(decreaseButton, "Decrease Btn", null, false, true, true);
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
                    EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}