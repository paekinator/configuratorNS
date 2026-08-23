using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(RectDragger))]
    public class RectDraggerEditor : Editor
    {
        RectDragger rTarget;

        // Properties
        SerializedProperty isDraggable;
        SerializedProperty axisConstraint;
        SerializedProperty boundaryType;
        SerializedProperty boundaryRect;
        SerializedProperty allowOutOfBounds;
        SerializedProperty returnDuration;
        SerializedProperty returnCurve;
        SerializedProperty dragSources;

        void OnEnable()
        {
            rTarget = (RectDragger)target;

            isDraggable = serializedObject.FindProperty("isDraggable");
            axisConstraint = serializedObject.FindProperty("axisConstraint");
            boundaryType = serializedObject.FindProperty("boundaryType");
            boundaryRect = serializedObject.FindProperty("boundaryRect");
            allowOutOfBounds = serializedObject.FindProperty("allowOutOfBounds");
            returnDuration = serializedObject.FindProperty("returnDuration");
            returnCurve = serializedObject.FindProperty("returnCurve");
            dragSources = serializedObject.FindProperty("dragSources");

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

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(isDraggable, "Is Draggable", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(allowOutOfBounds, "Allow Going Out Of Bounds", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                    if (allowOutOfBounds.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(returnDuration, "Return Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(returnCurve, "Return Curve", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(axisConstraint, "Axis Constraint", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(boundaryType, "Boundary Type", null, false, false);
                        if (boundaryType.enumValueIndex == 2)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(boundaryRect, "Boundary Rect", null, false, true);
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

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawArrayProperty(dragSources, "Drag Sources", "Add RectTransforms here (e.g., TitleBar, Handle) to trigger the drag. If empty, the whole object is draggable.", false, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}