using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Tooltip))]
    public class TooltipEditor : Editor
    {
        // Target
        Tooltip tooltipTarget;

        // Content
        SerializedProperty tooltipPreset;
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;

        // Settings
        SerializedProperty showDelay;
        SerializedProperty followCursor;
        SerializedProperty is3DObject;
        SerializedProperty maxWidth;
        SerializedProperty movementSmoothing;

        // Animation
        SerializedProperty animationType;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty slideOffset;
        SerializedProperty scaleFrom;

        // Position & Offset
        SerializedProperty offsetPosition;
        SerializedProperty customOffset;
        SerializedProperty offsetDistance;
        SerializedProperty screenEdgePadding;

        // References
        SerializedProperty customContent;
        SerializedProperty tooltipCanvas;

        // Events
        SerializedProperty onShow;
        SerializedProperty onHide;

        void OnEnable()
        {
            tooltipTarget = (Tooltip)target;

            tooltipPreset = serializedObject.FindProperty("tooltipPreset");
            icon = serializedObject.FindProperty("icon");
            title = serializedObject.FindProperty("title");
            description = serializedObject.FindProperty("description");

            showDelay = serializedObject.FindProperty("showDelay");
            followCursor = serializedObject.FindProperty("followCursor");
            is3DObject = serializedObject.FindProperty("is3DObject");
            maxWidth = serializedObject.FindProperty("maxWidth");
            movementSmoothing = serializedObject.FindProperty("movementSmoothing");
         
            animationType = serializedObject.FindProperty("animationType");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            slideOffset = serializedObject.FindProperty("slideOffset");

            offsetPosition = serializedObject.FindProperty("offsetPosition");
            customOffset = serializedObject.FindProperty("customOffset");
            offsetDistance = serializedObject.FindProperty("offsetDistance");
            screenEdgePadding = serializedObject.FindProperty("screenEdgePadding");

            customContent = serializedObject.FindProperty("customContent");
            tooltipCanvas = serializedObject.FindProperty("tooltipCanvas");

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

            if (EvoEditorGUI.DrawFoldout(ref tooltipTarget.contentFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    GUI.enabled = customContent.objectReferenceValue == null;
                    EvoEditorGUI.DrawProperty(tooltipPreset, "Preset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(icon, "Icon", null, true, true, true);
                    EvoEditorGUI.DrawProperty(title, "Title", null, true, true, true);
                    EvoEditorGUI.DrawProperty(description, "Description", null, true, true, true);
#if EVO_LOCALIZATION
                    string[] exProps = new string[] { "titleKey", "descriptionKey" };
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, tooltipTarget.gameObject, null, exProps);
#endif
                    GUI.enabled = true;
                    EvoEditorGUI.DrawProperty(customContent, "Custom Content", "Instantiates a custom prefab instead of the default content.", false, true, true);
                    if (customContent.objectReferenceValue != null)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.DrawInfoBox("Custom Content is assigned. The attached object will be used to generate the tooltip content.", revertBackgroundColor: true);
                    }
                }
                    EvoEditorGUI.EndContainer();
            }
 
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tooltipTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(followCursor, "Follow Cursor", null, true, true, true);
                    EvoEditorGUI.DrawToggle(is3DObject, "Is 3D Object", null, true, true, true);
                    EvoEditorGUI.DrawProperty(maxWidth, "Max Width", "Sets the max width of the tooltip rect.", true, true, true);
                    EvoEditorGUI.DrawProperty(showDelay, "Show Delay", "Sets whether the tooltip is displayed immediately or after a delay (in seconds).", true, true, true);
                    EvoEditorGUI.DrawProperty(movementSmoothing, "Movement Smoothing", "Add position lerping to smooth the transition. Set it to 0 to make it snappy.", true, true, true);
                    EvoEditorGUI.DrawProperty(screenEdgePadding, "Screen Edge Padding", "Add extra padding when the tooltip is near the edge of the screen.", true, true, true);

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
                        else if (animationType.enumValueIndex == 3)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
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

            if (EvoEditorGUI.DrawFoldout(ref tooltipTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(tooltipCanvas, "Tooltip Canvas", "The canvas on which the tooltip object will be rendered. If left unset, a default canvas will be created at runtime.", false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tooltipTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
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