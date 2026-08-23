using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ProceduralRect))]
    public class ProceduralRectEditor : Editor
    {
        ProceduralRect rTarget;

        // Base Graphic
        SerializedProperty sprite;
        SerializedProperty scaleMode;
        SerializedProperty color;
        SerializedProperty raycastMode;
        SerializedProperty raycastTarget;
        SerializedProperty raycastPadding;
        SerializedProperty maskable;

        // Clipping
        SerializedProperty clipMethod;
        SerializedProperty clipOrigin;
        SerializedProperty clipAmount;
        SerializedProperty clipClockwise;

        // Fill
        SerializedProperty fillCenter;
        SerializedProperty softness;
        SerializedProperty fillColorMode;
        SerializedProperty fillColor;
        SerializedProperty fillGradient;
        SerializedProperty fillGradientAngle;
        SerializedProperty fillGradientZoom;
        SerializedProperty fillGradientReverse;

        // Outline
        SerializedProperty outlineWidth;
        SerializedProperty outlineColorMode;
        SerializedProperty outlineColor;
        SerializedProperty outlineGradient;
        SerializedProperty outlineGradientAngle;
        SerializedProperty outlineGradientZoom;
        SerializedProperty outlineGradientReverse;

        // Corners
        SerializedProperty radiusMode;
        SerializedProperty independentCorners;
        SerializedProperty squircleCorners;
        SerializedProperty cornerRadius;
        SerializedProperty radiusSyncMode;
        SerializedProperty radiusSyncTarget;

        // Inner Shadow
        SerializedProperty innerShadowOffset, innerShadowSize, innerShadowSoftness;
        SerializedProperty innerShadowColorMode, innerShadowColor, innerShadowGradient;
        SerializedProperty innerShadowGradientAngle, innerShadowGradientZoom, innerShadowGradientReverse;

        // Outer Shadow
        SerializedProperty outerShadowOffset, outerShadowSize, outerShadowSoftness;
        SerializedProperty outerShadowColorMode, outerShadowColor, outerShadowGradient;
        SerializedProperty outerShadowGradientAngle, outerShadowGradientZoom, outerShadowGradientReverse;

        static readonly string[] HorizontalOriginLabels = { "Left", "Right" };
        static readonly string[] VerticalOriginLabels = { "Bottom", "Top" };
        static readonly string[] RadialOriginLabels = { "Bottom", "Right", "Top", "Left" };

        void OnEnable()
        {
            rTarget = (ProceduralRect)target;

            sprite = serializedObject.FindProperty("sprite");
            scaleMode = serializedObject.FindProperty("scaleMode");
            color = serializedObject.FindProperty("m_Color");
            raycastMode = serializedObject.FindProperty("raycastMode");
            raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            maskable = serializedObject.FindProperty("m_Maskable");

            clipMethod = serializedObject.FindProperty("clipMethod");
            clipOrigin = serializedObject.FindProperty("clipOrigin");
            clipAmount = serializedObject.FindProperty("clipAmount");
            clipClockwise = serializedObject.FindProperty("clipClockwise");

            fillCenter = serializedObject.FindProperty("fillCenter");
            softness = serializedObject.FindProperty("softness");
            fillColorMode = serializedObject.FindProperty("fillColorMode");
            fillColor = serializedObject.FindProperty("fillColor");
            fillGradient = serializedObject.FindProperty("fillGradient");
            fillGradientAngle = serializedObject.FindProperty("fillGradientAngle");
            fillGradientZoom = serializedObject.FindProperty("fillGradientZoom");
            fillGradientReverse = serializedObject.FindProperty("fillGradientReverse");

            outlineWidth = serializedObject.FindProperty("outlineWidth");
            outlineColorMode = serializedObject.FindProperty("outlineColorMode");
            outlineColor = serializedObject.FindProperty("outlineColor");
            outlineGradient = serializedObject.FindProperty("outlineGradient");
            outlineGradientAngle = serializedObject.FindProperty("outlineGradientAngle");
            outlineGradientZoom = serializedObject.FindProperty("outlineGradientZoom");
            outlineGradientReverse = serializedObject.FindProperty("outlineGradientReverse");

            radiusMode = serializedObject.FindProperty("radiusMode");
            independentCorners = serializedObject.FindProperty("independentCorners");
            squircleCorners = serializedObject.FindProperty("squircleCorners");
            cornerRadius = serializedObject.FindProperty("cornerRadius");
            radiusSyncMode = serializedObject.FindProperty("radiusSyncMode");
            radiusSyncTarget = serializedObject.FindProperty("radiusSyncTarget");

            innerShadowOffset = serializedObject.FindProperty("innerShadowOffset");
            innerShadowSize = serializedObject.FindProperty("innerShadowSize");
            innerShadowSoftness = serializedObject.FindProperty("innerShadowSoftness");
            innerShadowColorMode = serializedObject.FindProperty("innerShadowColorMode");
            innerShadowColor = serializedObject.FindProperty("innerShadowColor");
            innerShadowGradient = serializedObject.FindProperty("innerShadowGradient");
            innerShadowGradientAngle = serializedObject.FindProperty("innerShadowGradientAngle");
            innerShadowGradientZoom = serializedObject.FindProperty("innerShadowGradientZoom");
            innerShadowGradientReverse = serializedObject.FindProperty("innerShadowGradientReverse");

            outerShadowOffset = serializedObject.FindProperty("outerShadowOffset");
            outerShadowSize = serializedObject.FindProperty("outerShadowSize");
            outerShadowSoftness = serializedObject.FindProperty("outerShadowSoftness");
            outerShadowColorMode = serializedObject.FindProperty("outerShadowColorMode");
            outerShadowColor = serializedObject.FindProperty("outerShadowColor");
            outerShadowGradient = serializedObject.FindProperty("outerShadowGradient");
            outerShadowGradientAngle = serializedObject.FindProperty("outerShadowGradientAngle");
            outerShadowGradientZoom = serializedObject.FindProperty("outerShadowGradientZoom");
            outerShadowGradientReverse = serializedObject.FindProperty("outerShadowGradientReverse");

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

            DrawBaseSection();
            DrawFillSection();
            DrawOutlineSection();
            DrawCornersSection();
            DrawInnerShadowSection();
            DrawOuterShadowSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawBaseSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.graphicFoldout, "Graphic", EvoEditorGUI.GetIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(sprite, "Source Image", null, true, true, true);
                    EvoEditorGUI.DrawProperty(color, "Base Color", null, true, true, true);
                    if (sprite.objectReferenceValue != null) { EvoEditorGUI.DrawProperty(scaleMode, "Scale Mode", null, true, true, true); }

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(clipMethod, "Clip Method", null, false, false);

                        var method = (ProceduralRect.ClipMethod)clipMethod.intValue;
                        if (method != ProceduralRect.ClipMethod.None)
                        {
                            GUILayout.Space(1);
                            EvoEditorGUI.BeginContainer(3);
                            {
                                string[] labels = method switch
                                {
                                    ProceduralRect.ClipMethod.Horizontal => HorizontalOriginLabels,
                                    ProceduralRect.ClipMethod.Vertical => VerticalOriginLabels,
                                    ProceduralRect.ClipMethod.Radial360 => RadialOriginLabels,
                                    _ => HorizontalOriginLabels,
                                };

                                clipOrigin.intValue = Mathf.Clamp(clipOrigin.intValue, 0, labels.Length - 1);
                                clipOrigin.intValue = EvoEditorGUI.DrawDropdown(clipOrigin.intValue, labels, "Origin");
                                EvoEditorGUI.DrawProperty(clipAmount, "Amount", null, true, true);
                                EvoEditorGUI.DrawToggle(clipClockwise, "Clockwise", null, false);
                            }
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawProperty(raycastMode, "Raycast Mode", null, false, false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            // Safely bind our custom enum visually directly into the underlying Unity UI Hit-Test System
                            raycastTarget.boolValue = raycastMode.intValue != (int)ProceduralRect.RaycastMode.None;
                        }
                        if (raycastTarget.boolValue)
                        {
                            GUILayout.Space(1);
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawArrayProperty(raycastPadding, "Raycast Padding", null, false);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.DrawProperty(softness, "Edge Softness", null, true, true, true);
                    EvoEditorGUI.DrawToggle(maskable, "Maskable", maskable.tooltip, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawFillSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.fillFoldout, "Fill", EvoEditorGUI.GetIcon("UI_Fill")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(fillCenter, "Fill Center", fillCenter.tooltip, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(fillColorMode, "Color Mode", null, false, false);
                        var mode = (ProceduralRect.ColorMode)fillColorMode.intValue;

                        if (mode != ProceduralRect.ColorMode.Base)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            {
                                if (mode == ProceduralRect.ColorMode.Custom)
                                {
                                    EvoEditorGUI.DrawProperty(fillColor, "Color", null, false, true);
                                }
                                else if (mode == ProceduralRect.ColorMode.Gradient)
                                {
                                    EvoEditorGUI.AddLayoutSpace();
                                    EvoEditorGUI.DrawProperty(fillGradient, "Gradient", null, true, true);
                                    EvoEditorGUI.DrawProperty(fillGradientAngle, "Angle", null, true, true);
                                    EvoEditorGUI.DrawProperty(fillGradientZoom, "Zoom", null, true, true);
                                    EvoEditorGUI.DrawToggle(fillGradientReverse, "Reverse", fillGradientReverse.tooltip, false, true);
                                }
                            }
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

        void DrawOutlineSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.outlineFoldout, "Outline", EvoEditorGUI.GetIcon("UI_Outline")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(outlineWidth, "Width", null, false, true, true);

                    if (outlineWidth.floatValue > 0f)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        {
                            EvoEditorGUI.DrawProperty(outlineColorMode, "Color Mode", null, false, false);
                            var mode = (ProceduralRect.ColorMode)outlineColorMode.intValue;

                            if (mode != ProceduralRect.ColorMode.Base)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                {
                                    if (mode == ProceduralRect.ColorMode.Custom)
                                    {
                                        EvoEditorGUI.DrawProperty(outlineColor, "Color", null, false, true);
                                    }
                                    else if (mode == ProceduralRect.ColorMode.Gradient)
                                    {
                                        EvoEditorGUI.DrawProperty(outlineGradient, "Gradient", null, true, true);
                                        EvoEditorGUI.DrawProperty(outlineGradientAngle, "Angle", null, true, true);
                                        EvoEditorGUI.DrawProperty(outlineGradientZoom, "Zoom", null, true, true);
                                        EvoEditorGUI.DrawToggle(outlineGradientReverse, "Reverse", outlineGradientReverse.tooltip, false, true);
                                    }
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

        void DrawCornersSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.cornerRadiusFoldout, "Corner Radius", EvoEditorGUI.GetIcon("UI_Corners")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Draw sync mode
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(radiusSyncMode, "Radius Sync", null, false, false);

                        if (radiusSyncMode.intValue == (int)ProceduralRect.RadiusSyncMode.MatchTarget)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(radiusSyncTarget, "Target", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    // Disable manual inputs if synced
                    GUI.enabled = radiusSyncMode.intValue == (int)ProceduralRect.RadiusSyncMode.None;

                    EvoEditorGUI.DrawToggle(independentCorners, "Independent Corners", independentCorners.tooltip, true, true, true);
                    EvoEditorGUI.DrawToggle(squircleCorners, "Squircle", squircleCorners.tooltip, true, true, true);
                    EvoEditorGUI.DrawProperty(radiusMode, "Unit", null, true, true, true);

                    bool isPct = radiusMode.intValue == (int)ProceduralRect.RadiusMode.Percentage;

                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (!independentCorners.boolValue)
                        {
                            EditorGUI.BeginChangeCheck();
                            float val = cornerRadius.vector4Value.x;

                            if (isPct) { val = EditorGUILayout.Slider(new GUIContent("Radius (%)"), val, 0f, 100f); }
                            else { val = EditorGUILayout.FloatField(new GUIContent("Radius (px)"), val); }

                            if (EditorGUI.EndChangeCheck())
                            {
                                val = isPct ? Mathf.Clamp(val, 0f, 100f) : Mathf.Max(0f, val);
                                cornerRadius.vector4Value = new Vector4(val, val, val, val);
                            }
                        }
                        else
                        {
                            EditorGUILayout.Space(6);

                            // 2x2 Illustrator-style Grid
                            Rect area = GUILayoutUtility.GetRect(0, 48f);
                            float cx = area.center.x;
                            float cy = area.center.y;

                            float fieldW = 50f;
                            float fieldH = EditorGUIUtility.singleLineHeight;
                            float iconW = 24f; // Drag zone width
                            float gapX = 4f; // Space between left/right sides
                            float gapY = 4f; // Space between top/bottom

                            // Top Left [Icon] [Field]
                            Rect rTL_Field = new(cx - gapX - fieldW, cy - gapY - fieldH, fieldW, fieldH);
                            Rect rTL_Icon = new(rTL_Field.x - iconW, rTL_Field.y, iconW, fieldH);

                            // Bottom Left [Icon] [Field]
                            Rect rBL_Field = new(cx - gapX - fieldW, cy + gapY, fieldW, fieldH);
                            Rect rBL_Icon = new(rBL_Field.x - iconW, rBL_Field.y, iconW, fieldH);

                            // Top Right [Field] [Icon]
                            Rect rTR_Field = new(cx + gapX, cy - gapY - fieldH, fieldW, fieldH);
                            Rect rTR_Icon = new(rTR_Field.xMax, rTR_Field.y, iconW, fieldH);

                            // Bottom Right [Field] [Icon]
                            Rect rBR_Field = new(cx + gapX, cy + gapY, fieldW, fieldH);
                            Rect rBR_Icon = new(rBR_Field.xMax, rBR_Field.y, iconW, fieldH);

                            // Vector 4 mapped as X: TL, Y: TR, Z: BR, W: BL
                            DrawDraggableCorner(rTL_Field, rTL_Icon, cornerRadius, 0, isPct); // TL (X)
                            DrawDraggableCorner(rTR_Field, rTR_Icon, cornerRadius, 1, isPct); // TR (Y)
                            DrawDraggableCorner(rBL_Field, rBL_Icon, cornerRadius, 3, isPct); // BL (W)
                            DrawDraggableCorner(rBR_Field, rBR_Icon, cornerRadius, 2, isPct); // BR (Z)

                            EditorGUILayout.Space(6);
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground();

                    GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawInnerShadowSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.innerShadowFoldout, "Inner Shadow", EvoEditorGUI.GetIcon("UI_InnerShadow")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(innerShadowOffset, "Offset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(innerShadowSize, "Size", null, true, true, true);
                    EvoEditorGUI.DrawProperty(innerShadowSoftness, "Softness", null, false, true, true);

                    bool hasShadow = innerShadowSize.floatValue != 0f || innerShadowSoftness.floatValue > 0f || innerShadowOffset.vector2Value != Vector2.zero;

                    if (hasShadow)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        {
                            EvoEditorGUI.DrawProperty(innerShadowColorMode, "Color Mode", null, false, false);
                            var mode = (ProceduralRect.ColorMode)innerShadowColorMode.intValue;

                            if (mode != ProceduralRect.ColorMode.Base)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                {
                                    if (mode == ProceduralRect.ColorMode.Custom)
                                    {
                                        EvoEditorGUI.DrawProperty(innerShadowColor, "Color", null, false, true);
                                    }
                                    else if (mode == ProceduralRect.ColorMode.Gradient)
                                    {
                                        EvoEditorGUI.DrawProperty(innerShadowGradient, "Gradient", null, true, true);
                                        EvoEditorGUI.DrawProperty(innerShadowGradientAngle, "Angle", null, true, true);
                                        EvoEditorGUI.DrawProperty(innerShadowGradientZoom, "Zoom", null, true, true);
                                        EvoEditorGUI.DrawToggle(innerShadowGradientReverse, "Reverse", innerShadowGradientReverse.tooltip, false, true);
                                    }
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

        void DrawOuterShadowSection()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref rTarget.outerShadowFoldout, "Outer Shadow", EvoEditorGUI.GetIcon("UI_Shadow")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(outerShadowOffset, "Offset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(outerShadowSize, "Size", null, true, true, true);
                    EvoEditorGUI.DrawProperty(outerShadowSoftness, "Softness", null, false, true, true);

                    bool hasShadow = outerShadowSize.floatValue != 0f || outerShadowSoftness.floatValue > 0f || outerShadowOffset.vector2Value != Vector2.zero;

                    if (hasShadow)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        {
                            EvoEditorGUI.DrawProperty(outerShadowColorMode, "Color Mode", null, false, false);
                            var mode = (ProceduralRect.ColorMode)outerShadowColorMode.intValue;

                            if (mode != ProceduralRect.ColorMode.Base)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                {
                                    if (mode == ProceduralRect.ColorMode.Custom)
                                    {
                                        EvoEditorGUI.DrawProperty(outerShadowColor, "Color", null, false, true);
                                    }
                                    else if (mode == ProceduralRect.ColorMode.Gradient)
                                    {
                                        EvoEditorGUI.DrawProperty(outerShadowGradient, "Gradient", null, true, true);
                                        EvoEditorGUI.DrawProperty(outerShadowGradientAngle, "Angle", null, true, true);
                                        EvoEditorGUI.DrawProperty(outerShadowGradientZoom, "Zoom", null, true, true);
                                        EvoEditorGUI.DrawToggle(outerShadowGradientReverse, "Reverse", outerShadowGradientReverse.tooltip, false, true);
                                    }
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
        }

        void DrawDraggableCorner(Rect fieldRect, Rect iconRect, SerializedProperty prop, int cornerIndex, bool isPct)
        {
            // Setup invisible drag zone
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;

            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.SlideArrow);

            // Fetch the specific Vector4 component value
            Vector4 v = prop.vector4Value;
            float currentVal = v[cornerIndex];

            // Intercept Drag Logic
            switch (e.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (iconRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = controlID;
                        EditorGUIUtility.SetWantsMouseJumping(1); // Allow continuous dragging outside screen
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        float multiplier = 0.5f;
                        if (e.shift) { multiplier = 2f; } // Hold shift for faster scrub
                        if (e.alt) { multiplier = 0.1f; } // Hold alt for fine scrub

                        float newVal = currentVal + e.delta.x * multiplier;
                        v[cornerIndex] = isPct ? Mathf.Clamp(newVal, 0f, 100f) : Mathf.Max(0f, newVal);
                        prop.vector4Value = v;
                        GUI.changed = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        EditorGUIUtility.SetWantsMouseJumping(0);
                        e.Use();
                    }
                    break;
            }

            // Draw crop marks inside the icon rect
            if (e.type == EventType.Repaint)
            {
                Color c = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
                if (GUIUtility.hotControl == controlID) { c = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f); }

                float size = 10f;
                float thick = 1.5f;
                float margin = 6f;
                float cy = iconRect.center.y;

                // Position the icons neatly against their respective float fields
                if (cornerIndex == 0) // TL
                {
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 1) // TR
                {
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin, cy - size / 2, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin + size - thick, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 2) // BR (Z)
                {
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin, cy + size / 2 - thick, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.x + margin + size - thick, cy - size / 2, thick, size), c);
                }
                else if (cornerIndex == 3) // BL (W)
                {
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy + size / 2 - thick, size, thick), c);
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - size - margin, cy - size / 2, thick, size), c);
                }
            }

            // Create custom style to leave padding on the right side for the '%' or 'px' label
            GUIStyle floatStyle = new(EditorStyles.numberField);
            floatStyle.padding.right = 20;

            // Draw float field manually to catch typing changes
            EditorGUI.showMixedValue = prop.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float val = EditorGUI.FloatField(fieldRect, GUIContent.none, currentVal, floatStyle);
            if (EditorGUI.EndChangeCheck())
            {
                v[cornerIndex] = isPct ? Mathf.Clamp(val, 0f, 100f) : Mathf.Max(0f, val);
                prop.vector4Value = v;
            }
            EditorGUI.showMixedValue = false;

            // Overlay the symbol visually inside the field
            Rect unitRect = new(fieldRect.xMax - 22, fieldRect.y, 20, fieldRect.height);
            GUIStyle unitStyle = new(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            unitStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f);
            GUI.Label(unitRect, isPct ? "%" : "px", unitStyle);
        }
    }
}