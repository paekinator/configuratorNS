using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ReorderableList))]
    public class ReorderableListEditor : Editor
    {
        // Properties
        SerializedProperty listContainer;
        SerializedProperty canvas;
        SerializedProperty instantSnap;
        SerializedProperty itemSpacing;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty dragAlpha;
        SerializedProperty dragScale;
        SerializedProperty onOrderChanged;

        // Foldout states
        bool settingsFoldout = true;
        bool referencesFoldout = false;
        bool eventsFoldout = false;

        void OnEnable()
        {
            listContainer = serializedObject.FindProperty("listContainer");
            canvas = serializedObject.FindProperty("canvas");
            instantSnap = serializedObject.FindProperty("instantSnap");
            itemSpacing = serializedObject.FindProperty("itemSpacing");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");
            dragAlpha = serializedObject.FindProperty("dragAlpha");
            dragScale = serializedObject.FindProperty("dragScale");
            onOrderChanged = serializedObject.FindProperty("onOrderChanged");

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
            DrawReferences();
            DrawEvents();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(instantSnap, "Instant Snap", null, true, true, true);
                    EvoEditorGUI.DrawProperty(itemSpacing, "Item Spacing", "Used if there's no layout group attached.", true, true, true);
                    EvoEditorGUI.DrawProperty(dragAlpha, "Drag Alpha", "Sets the transparency of the object while it's being dragged.", true, true, true);
                    EvoEditorGUI.DrawProperty(dragScale, "Drag Scale", "Sets the scale of the object while it's being dragged.", true, true, true);
                    GUI.enabled = !instantSnap.boolValue;
                    EvoEditorGUI.DrawProperty(animationDuration, "Animation Duration", null, true, true, true);
                    EvoEditorGUI.DrawProperty(animationCurve, "Animation Curve", null, false, true, true);
                    GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(listContainer, "List Container", null, true, true, true);
                    EvoEditorGUI.DrawProperty(canvas, "Canvas", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEvents()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onOrderChanged, "On Order Changed", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}