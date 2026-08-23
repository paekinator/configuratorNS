using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(MarqueeSelectionContainer))]
    public class MarqueeSelectionContainerEditor : Editor
    {
        // Target
        MarqueeSelectionContainer mscTarget;

        // Properties
        SerializedProperty rectFillColor;
        SerializedProperty rectBorderColor;
        SerializedProperty rectBorderWidth;

        SerializedProperty allowAdditiveSelect;
        SerializedProperty allowDragMove;
        SerializedProperty clickEmptyToDeselect;
        SerializedProperty marqueeDeadZone;
        SerializedProperty dragMoveDeadZone;
        SerializedProperty reparentTargets;

        SerializedProperty rectFadeOutDuration;
        SerializedProperty rectSmoothing;
        SerializedProperty reparentDuration;
        SerializedProperty reparentCurve;

        SerializedProperty include3DObjects;
        SerializedProperty worldCamera;
        SerializedProperty maxRaycastDistance;
        SerializedProperty selectableLayer3D;

        void OnEnable()
        {
            mscTarget = (MarqueeSelectionContainer)target;

            rectFillColor = serializedObject.FindProperty("rectFillColor");
            rectBorderColor = serializedObject.FindProperty("rectBorderColor");
            rectBorderWidth = serializedObject.FindProperty("rectBorderWidth");

            allowAdditiveSelect = serializedObject.FindProperty("allowAdditiveSelect");
            allowDragMove = serializedObject.FindProperty("allowDragMove");
            clickEmptyToDeselect = serializedObject.FindProperty("clickEmptyToDeselect");
            marqueeDeadZone = serializedObject.FindProperty("marqueeDeadZone");
            dragMoveDeadZone = serializedObject.FindProperty("dragMoveDeadZone");
            reparentTargets = serializedObject.FindProperty("reparentTargets");

            rectFadeOutDuration = serializedObject.FindProperty("rectFadeOutDuration");
            rectSmoothing = serializedObject.FindProperty("rectSmoothing");
            reparentDuration = serializedObject.FindProperty("reparentDuration");
            reparentCurve = serializedObject.FindProperty("reparentCurve");

            include3DObjects = serializedObject.FindProperty("include3DObjects");
            worldCamera = serializedObject.FindProperty("worldCamera");
            maxRaycastDistance = serializedObject.FindProperty("maxRaycastDistance");
            selectableLayer3D = serializedObject.FindProperty("selectableLayer3D");

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
            DrawStyle();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref mscTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(allowAdditiveSelect, "Allow Additive Select", null, true, true, true);
                    EvoEditorGUI.DrawToggle(allowDragMove, "Allow Drag Move", null, true, true, true);
                    EvoEditorGUI.DrawToggle(clickEmptyToDeselect, "Click Empty To Deselect", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(include3DObjects, "Include 3D Objects", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                    if (include3DObjects.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(worldCamera, "World Camera", null, true, true);
                        EvoEditorGUI.DrawProperty(maxRaycastDistance, "Max Raycast Distance", null, true, true);
                        EvoEditorGUI.DrawProperty(selectableLayer3D, "Selectable Layer", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.DrawProperty(marqueeDeadZone, "Marquee Dead Zone", null, true, true, true);
                    EvoEditorGUI.DrawProperty(dragMoveDeadZone, "Drag Move Dead Zone", null, true, true, true);
                    EvoEditorGUI.DrawProperty(reparentTargets, "Reparent Targets", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Animation");
                    {
                        EvoEditorGUI.DrawProperty(rectFadeOutDuration, "Rect Fade Out Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(rectSmoothing, "Rect Smoothing", null, true, true);
                        EvoEditorGUI.DrawProperty(reparentDuration, "Reparent Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(reparentCurve, "Reparent Curve", null, false, true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyle()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref mscTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(rectFillColor, "Rect Fill Color", null, true, true, true);
                    EvoEditorGUI.DrawProperty(rectBorderColor, "Rect Border Color", null, true, true, true);
                    EvoEditorGUI.DrawProperty(rectBorderWidth, "Rect Border Width", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}