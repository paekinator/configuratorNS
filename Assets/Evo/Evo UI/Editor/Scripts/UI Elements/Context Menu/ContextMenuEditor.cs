using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ContextMenu))]
    public class ContextMenuEditor : Editor
    {
        // Target
        ContextMenu cmTarget;

        // Content
        SerializedProperty menuItems;

        // Settings
        SerializedProperty is3DObject;
        SerializedProperty usePointerPosition;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty closeOnItemClick;
        SerializedProperty closeOnOutsideClick;
        SerializedProperty animationType;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty slideOffset;
        SerializedProperty scaleFrom;
        SerializedProperty triggerButton;
        SerializedProperty dragThreshold;

        // Position & Offset
        SerializedProperty offsetPosition;
        SerializedProperty customOffset;
        SerializedProperty offsetDistance;
        SerializedProperty screenEdgePadding;

        // References
        SerializedProperty menuPreset;
        SerializedProperty targetCanvas;
        SerializedProperty targetCamera;

        // Events
        SerializedProperty onShow;
        SerializedProperty onHide;

        void OnEnable()
        {
            cmTarget = (ContextMenu)target;

            menuItems = serializedObject.FindProperty("menuItems");

            is3DObject = serializedObject.FindProperty("is3DObject");
            usePointerPosition = serializedObject.FindProperty("usePointerPosition");
            blockUIWhileOpen = serializedObject.FindProperty("blockUIWhileOpen");
            closeOnItemClick = serializedObject.FindProperty("closeOnItemClick");
            closeOnOutsideClick = serializedObject.FindProperty("closeOnOutsideClick");
            animationType = serializedObject.FindProperty("animationType");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");
            slideOffset = serializedObject.FindProperty("slideOffset");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            triggerButton = serializedObject.FindProperty("triggerButton");
            dragThreshold = serializedObject.FindProperty("dragThreshold");

            offsetPosition = serializedObject.FindProperty("offsetPosition");
            customOffset = serializedObject.FindProperty("customOffset");
            offsetDistance = serializedObject.FindProperty("offsetDistance");
            screenEdgePadding = serializedObject.FindProperty("screenEdgePadding");

            menuPreset = serializedObject.FindProperty("menuPreset");
            targetCanvas = serializedObject.FindProperty("targetCanvas");
            targetCamera = serializedObject.FindProperty("targetCamera");

            onShow = serializedObject.FindProperty("onShow");
            onHide = serializedObject.FindProperty("onHide");

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
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawContentSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cmTarget.contentFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawArrayProperty(menuItems, "Menu Items", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cmTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(is3DObject, "Is 3D Object", null, true, true, true);
                    EvoEditorGUI.DrawToggle(usePointerPosition, "Use Pointer Position", null, true, true, true);
                    EvoEditorGUI.DrawToggle(blockUIWhileOpen, "Block UI While Open", null, true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnItemClick, "Close On Item Click", null, true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnOutsideClick, "Close On Outside Click", null, true, true, true);
                    EvoEditorGUI.DrawProperty(screenEdgePadding, "Screen Edge Padding", "Add extra padding when the tooltip is near the edge of the screen.", true, true, true);
                    EvoEditorGUI.DrawProperty(dragThreshold, "Drag Threshold", null, true, true, true);
                    EvoEditorGUI.DrawProperty(triggerButton, "Trigger Button", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(offsetPosition, "Offset Position", null, false, false);
                    EvoEditorGUI.BeginContainer(3);
                    {
                        if (offsetPosition.enumValueIndex == 0) { EvoEditorGUI.DrawProperty(customOffset, "Custom Offset", null, false, true); }
                        else { EvoEditorGUI.DrawProperty(offsetDistance, "Distance", null, false, true); }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                    if (animationType.enumValueIndex != 0)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false, true);
                        if (animationType.enumValueIndex == 2)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(scaleFrom, "Scale From", null, false, true);
                        }
                        if (animationType.enumValueIndex == 3)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, cmTarget.gameObject, addSpace: false);
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

            if (EvoEditorGUI.DrawFoldout(ref cmTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(menuPreset, "Menu Preset", null, true, true, true);
                EvoEditorGUI.DrawProperty(targetCanvas, "Target Canvas", null, true, true, true);
                EvoEditorGUI.DrawProperty(targetCamera, "Target Camera", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cmTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onShow, "On Show", null, true, false);
                EvoEditorGUI.DrawProperty(onHide, "On Hide", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}