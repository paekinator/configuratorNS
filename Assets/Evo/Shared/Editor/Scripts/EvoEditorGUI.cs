using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    /// <summary>
    /// Interface for working with an EditorWindow that requires EvoEditorGUI repaints.
    /// </summary>
    public interface IEvoEditorGUIHandler { }

    /// <summary>
    /// Methods for rendering custom stuff for EditorGUI.
    /// </summary>
    public class EvoEditorGUI : Editor
    {
        public const string Version = "1.0.1";

        // Styles
        static GUIStyle backgroundStyle;
        static GUIStyle buttonStyle;
        static GUIStyle buttonLabelStyle;
        static GUIStyle toggleStyle;
        static GUIStyle foldoutStyle;
        static GUIStyle foldoutHeaderStyle;
        static GUIStyle foldoutHeaderStyleSimple;
        static GUIStyle headerStyle;
        static GUIStyle infoBoxStyle;
        static GUIStyle infoBoxTextStyle;
        static Texture2D cachedBackgroundTexture;

        // Global customization
        const int LayoutSpace = 3;
        const int ContainerPadding = 5;
        const int PropertyLabelPadding = 5;
        const int PropertyLabelFoldoutPadding = 16;
        static readonly RectOffset BackgroundBorderRadius = new(5, 5, 5, 5);

        // Button customization
        const int ButtonFontSize = 12;
        const int ButtonIconSize = 12;
        const float ButtonContentSpacing = 6f;
        readonly static RectOffset ButtonPadding = new(7, 7, 4, 4);
        public enum ButtonAlignment
        {
            Normal = 0,    // Next to text
            Left = 1,      // Left side of button
            Right = 2      // Right side of button
        }

        // Button colors - for dark theme
        readonly static Color ButtonColorNormal = new(0f, 0f, 0f, 0.2f);
        readonly static Color ButtonColorHover = new(1f, 1f, 1f, 0.1f);
        readonly static Color ButtonColorPress = new(1f, 1f, 1f, 0.05f);

        // Button colors - for light theme
        readonly static Color ButtonColorNormalLight = new(0f, 0f, 0f, 0.15f);
        readonly static Color ButtonColorHoverLight = new(0f, 0f, 0f, 0.1f);
        readonly static Color ButtonColorPressLight = new(0f, 0f, 0f, 0.1f);

        // Header customization
        const float HeaderIconSize = 14.5f;
        const int HeaderFontSize = 14;
        const float HeaderTextSpacing = 2f; // Only used with DrawTextWithSpacing variant
        const float HeaderSpaceBefore = 18f;
        readonly static RectOffset HeaderMargin = new(0, 0, 2, 0);

        // Foldout customization
        const float FoldoutIconSize = 14.5f;
        const int FoldoutFontSize = 14;
        const int FoldoutArrowLeftPadding = 21;
        const float FoldoutTextSpacing = 2f; // Only used with DrawTextWithSpacing variant
        const float FoldoutSpaceBetween = 3f;
        readonly static RectOffset FoldoutPadding = new(8, 8, 7, 7);

        // InfoBox customization
        const int InfoboxIconSize = 18;
        const int InfoboxFontSize = 11;
        const int InfoboxContentSpacing = 6;
        const float InfoboxMinHeight = 36f;
        readonly static RectOffset InfoboxPadding = new(8, 6, 5, 5);
        public enum InfoBoxType { Default = 0, Warning = 1, Error = 2 }

        // InfoBox colors - for dark theme
        readonly static Color InfoboxColorDefault = new(0f, 0f, 0f, 0.2f);
        readonly static Color InfoboxColorDefaultReverse = new(1f, 1f, 1f, 0.1f);
        readonly static Color InfoboxColorWarning = new(0.8f, 0.6f, 0.1f, 0.2f);
        readonly static Color InfoboxColorError = new(0.8f, 0.2f, 0.2f, 0.2f);

        // InfoBox colors - for light theme
        readonly static Color InfoboxColorDefaultLight = new(0f, 0f, 0f, 0.1f);
        readonly static Color InfoboxColorDefaultLightReverse = new(1f, 1f, 1f, 0.2f);
        readonly static Color InfoboxColorWarningLight = new(1f, 0.8f, 0.2f, 0.3f);
        readonly static Color InfoboxColorErrorLight = new(1f, 0.3f, 0.3f, 0.3f);

        // Customization - for dark theme
        readonly static Color ContentColor = Color.white;
        readonly static Color BackgroundColor = new(0f, 0f, 0f, 0.2f);
        readonly static Color BackgroundColorHover = new(1f, 1f, 1f, 0.1f);
        readonly static Color BackgroundColorReverse = new(1f, 1f, 1f, 0.08f);
        readonly static Color BackgroundColorReverseHover = new(1f, 1f, 1f, 0.16f);

        // Customization - for light theme
        readonly static Color ContentColorLight = Color.black;
        readonly static Color BackgroundColorLight = new(0f, 0f, 0f, 0.1f);
        readonly static Color BackgroundColorLightHover = new(1f, 1f, 1f, 0.15f);
        readonly static Color BackgroundColorReverseLight = new(1f, 1f, 1f, 0.2f);
        readonly static Color BackgroundColorReverseLightHover = new(0f, 0f, 0f, 0.05f);

        // Color caching
        struct ColorCache
        {
            public Color contentColor;
            public Color backgroundColor;
            public Color backgroundColorHover;
            public Color backgroundColorReverse;
            public Color backgroundColorReverseHover;
            public Color buttonColorNormal;
            public Color buttonColorHover;
            public Color buttonColorPress;
            public Color infoboxColorDefault;
            public Color infoboxColorDefaultReverse;
            public Color infoboxColorWarning;
            public Color infoboxColorError;
        }
        static ColorCache? darkThemeCache;
        static ColorCache? lightThemeCache;
        static bool lastProSkinState;

        // Editor repaint
        readonly static HashSet<Editor> registeredEditors = new();
        static double lastRepaintTime = 0;
        static bool needsRepaint = false;
        static bool updateHandlerRegistered = false;
        const double RepaintInterval = 0.016; // Repaint at ~60 FPS
        const double RuntimeRepaintInterval = 0.032;

        // Window state
        static EditorWindow cachedMouseOverWindow;
        static EditorWindow cachedFocusedWindow;
        static bool cachedMouseOverValidWindow;
        static bool cachedFocusedIsValidWindow;
        static double lastWindowCheckTime;
        const double WindowCheckInverval = 0.1; // ~10fps for window checks

        // Caching
        static readonly Dictionary<EditorWindow, bool> inspectorTypeCache = new();
        static readonly Dictionary<string, GUIContent> contentCache = new();
        static readonly Dictionary<string, Texture2D> iconCache = new();
        static readonly Dictionary<string, Font> fontCache = new();
        const bool LimitCacheSize = true;
        const int MaxContentCacheSize = 500;
        const int MaxInspectorCacheSize = 50;
        const int MaxIconCacheSize = 100;
        const int MaxFontCacheSize = 5;

        #region Initialization
        static EvoEditorGUI()
        {
            lastProSkinState = EditorGUIUtility.isProSkin;

            EditorApplication.update += CheckForSkinChange;
            EditorApplication.quitting += OnQuit;
        }

        static void OnQuit()
        {
            EditorApplication.update -= CheckForSkinChange;
            EditorApplication.update -= HandleRepaints;
        }

        // Check for skin changes to invalidate color cache
        static void CheckForSkinChange()
        {
            if (lastProSkinState != EditorGUIUtility.isProSkin)
            {
                lastProSkinState = EditorGUIUtility.isProSkin;
                InvalidateStyles();
            }
        }

        // Force styles to be recreated when skin changes
        static void InvalidateStyles()
        {
            toggleStyle = null;
            foldoutStyle = null;
            foldoutHeaderStyle = null;
            foldoutHeaderStyleSimple = null;
            backgroundStyle = null;
            headerStyle = null;
            cachedBackgroundTexture = null;
            buttonStyle = null;
            buttonLabelStyle = null;
            infoBoxStyle = null;
            infoBoxTextStyle = null;

            inspectorTypeCache.Clear();
            contentCache.Clear();
            iconCache.Clear();
            fontCache.Clear();
        }

        static void InitializeBackgroundStyle()
        {
            if (backgroundStyle != null)
                return;

            Texture2D bg = GetBackgroundTexture();

            backgroundStyle = new GUIStyle()
            {
                normal = { background = bg },
                border = BackgroundBorderRadius
            };
        }

        static void InitializeToggleStyle()
        {
            if (toggleStyle != null)
                return;

            Texture2D toggleOff = Resources.Load<Texture2D>("Editor Textures/Toggle-Off");
            Texture2D toggleOn = Resources.Load<Texture2D>("Editor Textures/Toggle-On");

            Color tColor = GetContentColor();

            toggleStyle = new GUIStyle()
            {
                fixedWidth = 24,
                fixedHeight = 18,
                overflow = new RectOffset(4, -4, -2, -2),
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(-33, 33, 2, 2),
                alignment = TextAnchor.MiddleRight,
                normal = { background = toggleOff, textColor = tColor },
                hover = { background = toggleOff, textColor = tColor },
                active = { background = toggleOff, textColor = tColor },
                onNormal = { background = toggleOn, textColor = tColor },
                onHover = { background = toggleOn, textColor = tColor },
                onActive = { background = toggleOn, textColor = tColor }
            };
        }

        static void InitializeFoldoutStyle()
        {
            foldoutStyle ??= new GUIStyle()
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = FoldoutPadding
            };

            foldoutHeaderStyle ??= new GUIStyle()
            {
                font = GetFont("Medium"),
                fontSize = FoldoutFontSize,
                normal = { textColor = GetContentColor() },
                hover = { textColor = GetContentColor() },
                alignment = TextAnchor.MiddleLeft,
            };

            foldoutHeaderStyleSimple ??= new GUIStyle()
            {
                fontSize = FoldoutFontSize,
                normal = { textColor = GetContentColor() },
                hover = { textColor = GetContentColor() },
                alignment = TextAnchor.MiddleLeft,
            };
        }

        static void InitializeHeaderStyle()
        {
            if (headerStyle != null)
                return;

            headerStyle = new GUIStyle()
            {
                font = GetFont("Medium"),
                fontSize = HeaderFontSize,
                normal = { textColor = GetContentColor() },
                hover = { textColor = GetContentColor() },
                alignment = TextAnchor.MiddleLeft,
                margin = HeaderMargin
            };
        }

        static void InitializeButtonStyle()
        {
            if (buttonStyle != null)
                return;

            Texture2D bg = GetBackgroundTexture();

            buttonStyle = new GUIStyle()
            {
                normal = { background = bg },
                hover = { background = bg },
                active = { background = bg },
                border = BackgroundBorderRadius,
                padding = ButtonPadding,
                alignment = TextAnchor.MiddleCenter
            };

            buttonLabelStyle = new GUIStyle()
            {
                // font = GetFont("Regular"),
                fontSize = ButtonFontSize,
                normal = { textColor = GetContentColor() },
                hover = { textColor = GetContentColor() },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        static void InitializeInfoBoxStyle()
        {
            if (infoBoxStyle != null)
                return;

            Texture2D bg = GetBackgroundTexture();

            infoBoxStyle = new GUIStyle()
            {
                normal = { background = bg },
                border = BackgroundBorderRadius,
                padding = InfoboxPadding,
                wordWrap = true,
            };

            infoBoxTextStyle = new GUIStyle()
            {
                fontSize = InfoboxFontSize,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = GetContentColor() },
                hover = { textColor = GetContentColor() }
            };
        }
        #endregion

        #region Get Methods
        static Texture2D GetBackgroundTexture()
        {
            if (cachedBackgroundTexture == null)
            {
                cachedBackgroundTexture = Resources.Load<Texture2D>("Editor Textures/EditorBackground-Default");
                if (cachedBackgroundTexture == null) { cachedBackgroundTexture = EditorGUIUtility.whiteTexture; }
            }

            return cachedBackgroundTexture;
        }

        static ColorCache GetCurrentThemeCache()
        {
            if (EditorGUIUtility.isProSkin)
            {
                if (!darkThemeCache.HasValue)
                {
                    darkThemeCache = new ColorCache
                    {
                        contentColor = ContentColor,
                        backgroundColor = BackgroundColor,
                        backgroundColorHover = BackgroundColorHover,
                        backgroundColorReverse = BackgroundColorReverse,
                        backgroundColorReverseHover = BackgroundColorReverseHover,
                        buttonColorNormal = ButtonColorNormal,
                        buttonColorHover = ButtonColorHover,
                        buttonColorPress = ButtonColorPress,
                        infoboxColorDefault = InfoboxColorDefault,
                        infoboxColorDefaultReverse = InfoboxColorDefaultReverse,
                        infoboxColorWarning = InfoboxColorWarning,
                        infoboxColorError = InfoboxColorError
                    };
                }
                return darkThemeCache.Value;
            }

            else
            {
                if (!lightThemeCache.HasValue)
                {
                    lightThemeCache = new ColorCache
                    {
                        contentColor = ContentColorLight,
                        backgroundColor = BackgroundColorLight,
                        backgroundColorHover = BackgroundColorLightHover,
                        backgroundColorReverse = BackgroundColorReverseLight,
                        backgroundColorReverseHover = BackgroundColorReverseLightHover,
                        buttonColorNormal = ButtonColorNormalLight,
                        buttonColorHover = ButtonColorHoverLight,
                        buttonColorPress = ButtonColorPressLight,
                        infoboxColorDefault = InfoboxColorDefaultLight,
                        infoboxColorDefaultReverse = InfoboxColorDefaultLightReverse,
                        infoboxColorWarning = InfoboxColorWarningLight,
                        infoboxColorError = InfoboxColorErrorLight
                    };
                }
                return lightThemeCache.Value;
            }
        }

        static GUIContent GetCachedContent(string text, string tooltip = null)
        {
            string path = tooltip == null ? text : $"{text}|{tooltip}";
            if (!contentCache.TryGetValue(path, out GUIContent content))
            {
                content = new GUIContent(text, tooltip);
                contentCache[path] = content;

                if (LimitCacheSize && contentCache.Count > MaxContentCacheSize)
                {
                    var keysToRemove = contentCache.Keys.Take(contentCache.Count / 2).ToList();
                    foreach (var key in keysToRemove) { contentCache.Remove(key); }
                }
            }
            return content;
        }

        public static Texture2D GetIcon(string icon, bool bypassPrefix = false)
        {
            string path = bypassPrefix ? $"Editor Textures/{icon}" : $"Editor Textures/Icon-{icon}";
            if (!iconCache.TryGetValue(path, out var texture))
            {
                texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    iconCache[path] = texture;

                    if (LimitCacheSize && iconCache.Count > MaxIconCacheSize)
                    {
                        var keysToRemove = iconCache.Keys.Take(iconCache.Count / 2).ToList();
                        foreach (var key in keysToRemove) { iconCache.Remove(key); }
                    }

                }
            }
            return texture;

        }

        public static Font GetFont(string fontName, bool bypassPrefix = false)
        {
            string path = bypassPrefix ? $"Editor Fonts/{fontName}" : $"Editor Fonts/EditorFont-{fontName}";
            if (!fontCache.TryGetValue(path, out Font font))
            {
                font = Resources.Load<Font>(path);
                if (font != null)
                {
                    fontCache[path] = font;

                    if (LimitCacheSize && fontCache.Count > MaxFontCacheSize)
                    {
                        var keysToRemove = fontCache.Keys.Take(fontCache.Count / 2).ToList();
                        foreach (var key in keysToRemove) { fontCache.Remove(key); }
                    }
                }
            }
            return font;
        }

        public static Color GetContentColor() => GetCurrentThemeCache().contentColor;

        public static Color GetBackgroundColor(bool revert = false, bool hover = false)
        {
            var cache = GetCurrentThemeCache();

            if (!hover)
                return revert ? cache.backgroundColorReverse : cache.backgroundColor;

            return revert ? cache.backgroundColorReverseHover : cache.backgroundColorHover;
        }

        public static Color GetButtonColor(bool hover = false, bool pressed = false)
        {
            var cache = GetCurrentThemeCache();

            if (pressed)
                return cache.buttonColorPress;

            if (hover)
                return cache.buttonColorHover;

            return cache.buttonColorNormal;
        }

        public static Color GetInfoBoxBackgroundColor(InfoBoxType type, bool revertColor = false)
        {
            var cache = GetCurrentThemeCache();

            return type switch
            {
                InfoBoxType.Warning => cache.infoboxColorWarning,
                InfoBoxType.Error => cache.infoboxColorError,
                InfoBoxType.Default => revertColor ? cache.infoboxColorDefaultReverse : cache.infoboxColorDefault,
                _ => revertColor ? cache.infoboxColorDefaultReverse : cache.infoboxColorDefault
            };
        }
        #endregion

        #region Layout & Spacing Methods
        public static void BeginHorizontalBackground(bool revertColor = false, params GUILayoutOption[] options)
        {
            InitializeBackgroundStyle();

            Color cachedGUIColor = GUI.color;
            GUI.color = GetBackgroundColor(revertColor);
            GUILayout.BeginHorizontal(backgroundStyle, options);
            GUI.color = cachedGUIColor;
        }

        public static void EndHorizontalBackground(bool addSpace = true)
        {
            GUILayout.EndHorizontal();

            if (addSpace)
                AddLayoutSpace();
        }

        public static void BeginVerticalBackground(bool isContainerItem = false, params GUILayoutOption[] options)
        {
            // Initialize custom style only once
            InitializeBackgroundStyle();

            Color cachedGUIColor = GUI.color;                   // Cache GUI color
            GUI.color = GetBackgroundColor(isContainerItem);    // Use new color system
            GUILayout.BeginVertical(backgroundStyle, options);  // Begin layout
            GUI.color = cachedGUIColor;                         // Revert GUI color for other content
        }

        public static void EndVerticalBackground(bool addSpace = false)
        {
            GUILayout.EndVertical();

            if (addSpace)
                AddLayoutSpace();
        }

        public static void BeginCenteredInspector(bool addMoreSpace = false)
        {
            GUILayout.BeginHorizontal(new GUIStyle()
            {
                padding = addMoreSpace ? new RectOffset(6, 8, 7, 0) : new RectOffset(-8, 3, 3, 0)
            });
            GUILayout.Space(-10);
            GUILayout.BeginVertical();
        }

        public static void EndCenteredInspector()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        public static void BeginContainer(string label, int padding = ContainerPadding, bool compactHeader = true)
        {
            GUILayout.BeginVertical(new GUIStyle()
            {
                padding = new RectOffset(padding, padding, compactHeader ? 0 : padding, padding)
            });

            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                DrawLabel(label);
                GUILayout.EndHorizontal();
                GUILayout.Space(1);
            }
        }

        public static void BeginContainer(int padding = ContainerPadding, bool compactHeader = true) => BeginContainer(null, padding, compactHeader);

        public static void BeginContainer() => BeginContainer(null, ContainerPadding, false);

        public static void EndContainer() => GUILayout.EndVertical();

        public static void AddFoldoutSpace() => GUILayout.Space(FoldoutSpaceBetween);

        public static void AddLayoutSpace() => GUILayout.Space(LayoutSpace);

        public static void AddPropertySpace() => GUILayout.Space(PropertyLabelPadding);
        #endregion

        #region Draw Methods
        public static void DrawProperty(SerializedProperty property, string label, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, bool hasFoldout = false, int labelWidth = 0)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0) { EditorGUIUtility.labelWidth = labelWidth; }

            if (!hasFoldout) { GUILayout.Space(PropertyLabelPadding); }
            else { GUILayout.Space(PropertyLabelFoldoutPadding); }

            EditorGUILayout.PropertyField(property, GetCachedContent(label, tooltip));
            EndHorizontalBackground(addSpace);

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        public static T DrawObject<T>(T obj, string label, string tooltip = null, bool allowSceneObjects = true,
            bool addSpace = true, bool customBackground = true, bool revertColor = false, int labelWidth = 0) where T : Object
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0) { EditorGUIUtility.labelWidth = labelWidth; }

            GUILayout.Space(PropertyLabelPadding);

            T result = (T)EditorGUILayout.ObjectField(GetCachedContent(label, tooltip), obj, typeof(T), allowSceneObjects);

            EndHorizontalBackground(addSpace);
            EditorGUIUtility.labelWidth = originalLabelWidth;

            return result;
        }

        public static int DrawDropdown(int selectedIndex, string[] options, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0) { EditorGUIUtility.labelWidth = labelWidth; }

            GUILayout.Space(PropertyLabelPadding);

            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, options, EditorStyles.popup);

            EndHorizontalBackground(addSpace);
            EditorGUIUtility.labelWidth = originalLabelWidth;

            return selectedIndex;
        }

        public static void DrawSlider(SerializedProperty intValue, int leftValue, int rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0) { EditorGUIUtility.labelWidth = labelWidth; }
            if (!string.IsNullOrEmpty(label)) { GUILayout.Space(PropertyLabelPadding + 1); }

            EditorGUILayout.IntSlider(intValue, leftValue, rightValue, label);
            EndHorizontalBackground(addSpace);

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        public static int DrawSlider(int value, int leftValue, int rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }
            if (!string.IsNullOrEmpty(label)) { GUILayout.Space(PropertyLabelPadding + 1); }

            int tempValue = EditorGUILayout.IntSlider(label, value, leftValue, rightValue);
            EndHorizontalBackground(addSpace);

            return tempValue;
        }

        public static void DrawSlider(SerializedProperty floatValue, float leftValue, float rightValue, string label = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0) { EditorGUIUtility.labelWidth = labelWidth; }
            if (!string.IsNullOrEmpty(label)) { GUILayout.Space(PropertyLabelPadding + 1); }

            EditorGUILayout.Slider(floatValue, leftValue, rightValue, label);
            EndHorizontalBackground(addSpace);

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        public static float DrawSlider(float value, float leftValue, float rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            float tempValue = EditorGUILayout.Slider(label, value, leftValue, rightValue);
            EndHorizontalBackground(addSpace);

            return tempValue;
        }

        public static void DrawArrayProperty(SerializedProperty property, string label, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            if (!customBackground) { GUILayout.BeginHorizontal(); }
            else { BeginHorizontalBackground(revertColor); }

            GUILayout.Space(PropertyLabelFoldoutPadding + 2);
            EditorGUILayout.PropertyField(property, GetCachedContent(label, tooltip));
            GUILayout.Space(7);
            EndHorizontalBackground(addSpace);
        }

        public static void DrawToggle(SerializedProperty property, string title, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, string onLabel = "On", string offLabel = "Off", bool bypassNormalBackground = false)
        {
            // Initialize custom style
            InitializeToggleStyle();

            // Get the rect for the background
            Rect backgroundRect = GUILayoutUtility.GetRect(
                GetCachedContent(title, tooltip),
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight + 4)
            );

            // Handle hover and background manually
            bool isHovering = false;
            if (CanHoverInspector() && backgroundRect.Contains(Event.current.mousePosition))
            {
                isHovering = true;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    EditorGUI.BeginChangeCheck();
                    property.boolValue = !property.boolValue;
                    if (EditorGUI.EndChangeCheck()) { property.serializedObject.ApplyModifiedProperties(); }
                    Event.current.Use();
                }
            }
            if (customBackground)
            {
                // Ensure background style is ready
                InitializeBackgroundStyle();

                Color cachedColor = GUI.color;
                GUI.color = GetBackgroundColor(revertColor, isHovering);
                if (isHovering) { GUI.Box(backgroundRect, GUIContent.none, backgroundStyle); }
                else if (!isHovering && !bypassNormalBackground) { GUI.Box(backgroundRect, GUIContent.none, backgroundStyle); }
                GUI.color = cachedColor;
            }

            // Calculate label rect from backgroundRect
            Rect labelRect = new(backgroundRect.x + (PropertyLabelPadding - 1), backgroundRect.y, backgroundRect.width - 50, backgroundRect.height);
            GUI.Label(labelRect, new GUIContent(title, tooltip));

            // Draw the toggle - position it relative to backgroundRect
            Rect toggleRect = new(backgroundRect.xMax - 26, backgroundRect.y + (backgroundRect.height - 18) / 2, 24, 18);
            property.boolValue = GUI.Toggle(toggleRect, property.boolValue, new GUIContent(property.boolValue ? onLabel : offLabel), toggleStyle);

            if (addSpace) { GUILayout.Space(LayoutSpace); }
        }

        public static bool DrawToggle(bool value, string title, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, string onLabel = "On", string offLabel = "Off", bool bypassNormalBackground = false)
        {
            // Initialize custom style
            InitializeToggleStyle();

            // Get the rect for the background
            Rect backgroundRect = GUILayoutUtility.GetRect(
                GetCachedContent(title, tooltip),
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight + 4)
            );

            // Handle hover and background manually
            bool isHovering = false;
            if (CanHoverInspector() && backgroundRect.Contains(Event.current.mousePosition))
            {
                isHovering = true;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    value = !value;
                    Event.current.Use();
                }
            }
            if (customBackground)
            {
                // Ensure background style is ready
                InitializeBackgroundStyle();

                Color cachedColor = GUI.color;
                GUI.color = GetBackgroundColor(revertColor, isHovering);
                if (isHovering) { GUI.Box(backgroundRect, GUIContent.none, backgroundStyle); }
                else if (!isHovering && !bypassNormalBackground) { GUI.Box(backgroundRect, GUIContent.none, backgroundStyle); }
                GUI.color = cachedColor;
            }

            // Calculate label rect from backgroundRect
            Rect labelRect = new(backgroundRect.x + (PropertyLabelPadding - 1), backgroundRect.y, backgroundRect.width - 50, backgroundRect.height);
            GUI.Label(labelRect, new GUIContent(title, tooltip));

            // Draw the toggle - position it relative to backgroundRect
            Rect toggleRect = new(backgroundRect.xMax - 26, backgroundRect.y + (backgroundRect.height - 18) / 2, 24, 18);
            value = GUI.Toggle(toggleRect, value, new GUIContent(value ? onLabel : offLabel), toggleStyle);

            if (addSpace)
                GUILayout.Space(LayoutSpace);

            return value;
        }

        public static void DrawLabel(string text, params GUILayoutOption[] options)
        {
            EditorGUILayout.LabelField(new GUIContent(text), options);
        }

        public static void DrawIcon(string iconName, string tooltip = null, float width = 18, float height = 18, bool expandHeight = false)
        {
            EditorGUILayout.LabelField(new GUIContent(GetIcon(iconName), tooltip),
                GUILayout.Width(width), GUILayout.Height(height), GUILayout.ExpandHeight(expandHeight));
        }

        public static void DrawIcon(Texture2D icon, string tooltip = null, float width = 18, float height = 18, bool expandHeight = false)
        {
            EditorGUILayout.LabelField(new GUIContent(icon, tooltip),
                GUILayout.Width(width), GUILayout.Height(height), GUILayout.ExpandHeight(expandHeight));
        }

        public static void DrawHeader(string title, Texture2D icon = null, bool addSpaceBefore = false)
        {
            // Use cached header style
            InitializeHeaderStyle();

            if (addSpaceBefore) { GUILayout.Space(HeaderSpaceBefore); }
            GUILayout.BeginHorizontal();
            Rect startRect = GUILayoutUtility.GetRect(GetCachedContent(title), headerStyle);

            if (icon != null)
            {
                Rect iconRect = new(startRect.x, startRect.y + (startRect.height - HeaderIconSize) / 2, HeaderIconSize, HeaderIconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            }

            // Draw text with custom spacing
            startRect.x += icon != null ? HeaderIconSize + 6 : 0; // Adjust for the icon offset
            DrawTextWithSpacing(title.ToUpper(), headerStyle, HeaderTextSpacing, startRect);

            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        public static void DrawHeader(string title, string iconName, bool addSpaceBefore = false)
        {
            DrawHeader(title, GetIcon(iconName), addSpaceBefore);
        }

        public static void DrawInfoBox(string message, InfoBoxType type = InfoBoxType.Default, string customIcon = null, bool revertBackgroundColor = false)
        {
            // Initialize style
            InitializeInfoBoxStyle();

            // Get background color
            Color backgroundColor = GetInfoBoxBackgroundColor(type, revertBackgroundColor);

            // Determine which icon to use
            Texture2D icon;
            if (!string.IsNullOrEmpty(customIcon)) { icon = GetIcon(customIcon); }
            else
            {
                icon = type switch
                {
                    InfoBoxType.Warning => GetIcon("InfoBox-Warning", true),
                    InfoBoxType.Error => GetIcon("InfoBox-Error", true),
                    InfoBoxType.Default => GetIcon("InfoBox-Default", true),
                    _ => GetIcon("InfoBox-Default", true)
                };
            }

            // Cache GUI colors
            Color cachedGUIColor = GUI.color;

            // Calculate the height needed for the text
            GUIContent textContent = new(message);
            float textWidth = EditorGUIUtility.currentViewWidth - InfoboxPadding.left - InfoboxPadding.right - 31; // Account for margins
            if (icon != null) { textWidth -= InfoboxIconSize; }

            // Create a temporary style for height calculation
            GUIStyle tempStyle = new(infoBoxTextStyle) { wordWrap = true };
            float textHeight = tempStyle.CalcHeight(textContent, textWidth);
            float boxHeight = Mathf.Max(InfoboxMinHeight, textHeight + InfoboxPadding.top + InfoboxPadding.bottom);

            // Set background color
            GUI.color = backgroundColor;

            // Get the rect for the info box
            Rect boxRect = GUILayoutUtility.GetRect(0, boxHeight, infoBoxStyle, GUILayout.ExpandWidth(true));

            // Draw the background
            GUI.Box(boxRect, GUIContent.none, infoBoxStyle);

            // Reset GUI color for content
            GUI.color = cachedGUIColor;

            // Calculate content area (inside padding)
            Rect contentRect = new(
                boxRect.x + InfoboxPadding.left,
                boxRect.y + InfoboxPadding.top,
                boxRect.width - InfoboxPadding.left - InfoboxPadding.right,
                boxRect.height - InfoboxPadding.top - InfoboxPadding.bottom
            );

            // Draw icon if present
            float textStartX = contentRect.x;
            if (icon != null)
            {
                Rect iconRect = new(
                    contentRect.x,
                    contentRect.y + (contentRect.height - InfoboxIconSize) / 2,
                    InfoboxIconSize,
                    InfoboxIconSize
                );

                // Use content color for icon
                GUI.color = GetContentColor();
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.color = cachedGUIColor;

                textStartX += InfoboxIconSize + InfoboxContentSpacing;
            }

            // Draw text
            Rect textRect = new(
                textStartX,
                contentRect.y,
                contentRect.width - (textStartX - contentRect.x),
                contentRect.height
            );

            // Use content color for text (already set in style initialization)
            GUI.Label(textRect, message, infoBoxTextStyle);

            // Restore original color
            GUI.color = cachedGUIColor;
        }

        public static void DrawInfoBox(string message, string customIcon, bool revertColor = false)
        {
            DrawInfoBox(message, InfoBoxType.Default, customIcon, revertColor);
        }

        public static bool DrawButton(string text, string icon, string tooltip = null, float width = 0, float height = 0,
            int fontSize = ButtonFontSize, int iconSize = ButtonIconSize, ButtonAlignment iconAlignment = ButtonAlignment.Normal,
            TextAnchor textAlignment = TextAnchor.MiddleCenter, bool reverseAlignment = false, float contentSpacing = ButtonContentSpacing,
            Color? normalColor = null, Color? hoverColor = null, Color? pressColor = null, bool bypassAdaptiveColor = false, bool revertBackgroundColor = false)
        {
            return DrawButton(text, string.IsNullOrEmpty(icon) ? null : GetIcon(icon), tooltip, width, height, fontSize, iconSize, iconAlignment,
                textAlignment, reverseAlignment, contentSpacing, normalColor, hoverColor, pressColor, bypassAdaptiveColor, revertBackgroundColor);
        }

        public static bool DrawButton(string text, Texture2D iconSrc = null, string tooltip = null, float width = 0, float height = 0,
            int fontSize = ButtonFontSize, int iconSize = ButtonIconSize, ButtonAlignment iconAlignment = ButtonAlignment.Normal,
            TextAnchor textAlignment = TextAnchor.MiddleCenter, bool reverseAlignment = false, float contentSpacing = ButtonContentSpacing,
             Color? normalColor = null, Color? hoverColor = null, Color? pressColor = null, bool bypassAdaptiveColor = false, bool revertBackgroundColor = false)
        {
            // Initialize button style
            InitializeButtonStyle();

            // Create custom label style with fontSize and textAlignment applied
            GUIStyle customLabelStyle = new(buttonLabelStyle)
            {
                fontSize = fontSize,
                alignment = textAlignment
            };

            // Calculate button dimensions using the custom font size
            Vector2 textSize = customLabelStyle.CalcSize(new GUIContent(text)); // Use text-only for size calculation

            float buttonHeight = height > 0 ? height : EditorGUIUtility.singleLineHeight + 4;

            // Calculate minimum width needed for content
            float minWidth = textSize.x + ButtonPadding.left + ButtonPadding.right;
            if (iconSrc != null) { minWidth += iconSize + contentSpacing; }

            // Get colors - use reverse background colors if specified, otherwise use button colors
            Color normal, hover, press;

            if (revertBackgroundColor)
            {
                normal = normalColor ?? GetBackgroundColor(revert: true);
                hover = hoverColor ?? GetBackgroundColor(revert: true, hover: true);
                // For pressed state with reverse colors, use a slightly darker version of hover
                press = pressColor ?? (GetBackgroundColor(revert: true, hover: true) * 0.8f);
            }
            else
            {
                normal = normalColor ?? GetButtonColor();
                hover = hoverColor ?? GetButtonColor(hover: true);
                press = pressColor ?? GetButtonColor(pressed: true);
            }

            // Cache GUI color
            Color cachedGUIColor = GUI.color;

            // Create a temporary button style for this button
            GUIStyle tempButtonStyle = new(buttonStyle);

            // Get the button rect - expand width if not specified, otherwise use fixed width
            Rect buttonRect;
            if (width > 0) { buttonRect = GUILayoutUtility.GetRect(width, buttonHeight, GUILayout.Width(width), GUILayout.Height(buttonHeight)); }
            else { buttonRect = GUILayoutUtility.GetRect(minWidth, buttonHeight, GUILayout.MinWidth(minWidth), GUILayout.ExpandWidth(true), GUILayout.Height(buttonHeight)); }

            // Smart hover detection for buttons - works when inspector is visible and not blocked
            bool isHover = false;
            bool isPressed = false;

            if (CanHoverInspector())
            {
                isHover = buttonRect.Contains(Event.current.mousePosition);
                isPressed = isHover && (Event.current.type == EventType.MouseDown || (Event.current.button == 0 && GUIUtility.hotControl != 0));
            }

            // Set the appropriate color
            Color currentColor;
            if (isPressed) { currentColor = press; }
            else if (isHover) { currentColor = hover; }
            else { currentColor = normal; }

            // Draw the button background with the current color
            GUI.color = currentColor;
            bool wasClicked = GUI.Button(buttonRect, new GUIContent("", tooltip), tempButtonStyle); // Add tooltip to button

            // Restore GUI color for content
            GUI.color = cachedGUIColor;

            // Set content colors if not bypassing adaptive color
            if (!bypassAdaptiveColor) { GUI.color = GetContentColor(); }

            // Draw content on top of the button
            if (iconSrc != null)
            {
                Vector2 iSize = new(iconSize, iconSize);

                // Check if we have text to display
                bool hasText = !string.IsNullOrEmpty(text);

                if (!hasText)
                {
                    // Icon only - center it in the button
                    Rect iconRect = new(
                        buttonRect.x + (buttonRect.width - iSize.x) / 2,
                        buttonRect.y + (buttonRect.height - iSize.y) / 2,
                        iSize.x,
                        iSize.y
                    );
                    GUI.DrawTexture(iconRect, iconSrc, ScaleMode.StretchToFill);
                }

                else
                {
                    // Icon + text layout
                    float totalContentWidth = textSize.x + iSize.x + contentSpacing;
                    Rect iconRect, textRect;

                    switch (iconAlignment)
                    {
                        case ButtonAlignment.Left:
                            iconRect = new Rect(buttonRect.x + ButtonPadding.left, buttonRect.y + (buttonRect.height - iSize.y) / 2, iSize.x, iSize.y);
                            textRect = new Rect(buttonRect.x + ButtonPadding.left + iSize.x + contentSpacing, buttonRect.y,
                                buttonRect.width - ButtonPadding.left - ButtonPadding.right - iSize.x - contentSpacing, buttonRect.height);
                            break;

                        case ButtonAlignment.Right:
                            iconRect = new Rect(buttonRect.xMax - ButtonPadding.right - iSize.x, buttonRect.y + (buttonRect.height - iSize.y) / 2, iSize.x, iSize.y);
                            textRect = new Rect(buttonRect.x + ButtonPadding.left, buttonRect.y,
                                buttonRect.width - ButtonPadding.left - ButtonPadding.right - iSize.x - contentSpacing, buttonRect.height);
                            break;

                        default: // Normal alignment (centered)
                            float startX = buttonRect.x + (buttonRect.width - totalContentWidth) / 2;
                            if (!reverseAlignment)
                            {
                                iconRect = new Rect(startX, buttonRect.y + (buttonRect.height - iSize.y) / 2, iSize.x, iSize.y);
                                textRect = new Rect(startX + iSize.x + contentSpacing, buttonRect.y, textSize.x, buttonRect.height);
                            }
                            else
                            {
                                textRect = new Rect(startX, buttonRect.y, textSize.x, buttonRect.height);
                                iconRect = new Rect(startX + textSize.x + contentSpacing, buttonRect.y + (buttonRect.height - iSize.y) / 2, iSize.x, iSize.y);
                            }
                            break;
                    }

                    GUI.DrawTexture(iconRect, iconSrc, ScaleMode.StretchToFill);
                    GUI.Label(textRect, new GUIContent(text, tooltip), customLabelStyle); // Add tooltip to label
                }
            }

            else
            {
                // No icon, just centered text
                customLabelStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(buttonRect, new GUIContent(text, tooltip), customLabelStyle); // Add tooltip to label
            }

            // Restore original GUI color
            GUI.color = cachedGUIColor;

            return wasClicked;
        }

        public static bool DrawFoldout(ref bool foldout, string title, Texture2D icon = null, Texture2D customArrow = null, bool useSpecialText = true)
        {
            // Initialize custom styles
            InitializeFoldoutStyle();

            // Get the rect for the background
            Rect backgroundRect = GUILayoutUtility.GetRect(GetCachedContent(title), foldoutStyle, GUILayout.ExpandWidth(true));

            // Handle hover and background manually
            bool isHovering = false;
            if (CanHoverInspector() && backgroundRect.Contains(Event.current.mousePosition))
            {
                isHovering = true;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    foldout = !foldout;
                    Event.current.Use();
                }
            }
            if (isHovering)
            {
                // Ensure style is initialized
                InitializeBackgroundStyle();
                Color cachedColor = GUI.color;
                GUI.color = GetBackgroundColor(false, isHovering);
                GUI.Box(backgroundRect, GUIContent.none, backgroundStyle);
                GUI.color = cachedColor;
            }

            // Draw icon if provided
            if (icon != null)
            {
                Rect iconRect = new(backgroundRect.x + 27, backgroundRect.y + (backgroundRect.height - FoldoutIconSize) / 2, FoldoutIconSize, FoldoutIconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            // Calculate and draw text
            Rect textRect = new(backgroundRect.x + (icon != null ? 48 : 26), backgroundRect.y, backgroundRect.width - 40, backgroundRect.height);
            if (!useSpecialText) { GUI.Label(textRect, title, foldoutHeaderStyleSimple); }
            else { DrawTextWithSpacing(title.ToUpper(), foldoutHeaderStyle, FoldoutTextSpacing, textRect); }

            // Calculate foldout icon position
            Rect foldoutRect = new(
                backgroundRect.xMin + FoldoutArrowLeftPadding, // Pixels from left edge
                backgroundRect.y + (backgroundRect.height - FoldoutIconSize) / 2, FoldoutIconSize, FoldoutIconSize);

            // Draw foldout arrow
            if (customArrow == null) { foldout = EditorGUI.Foldout(foldoutRect, foldout, GUIContent.none); }
            else
            {
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(foldout ? 180 : 0, new Vector2(foldoutRect.center.x, foldoutRect.center.y));
                GUI.DrawTexture(foldoutRect, customArrow);
                GUI.matrix = matrix;

                // Handle click for custom icon
                if (Event.current.type == EventType.MouseDown && foldoutRect.Contains(Event.current.mousePosition))
                {
                    foldout = !foldout;
                    Event.current.Use();
                    GUI.changed = true;
                }
            }

            return foldout;
        }

        public static void DrawSeparator(float height = 1.5f)
        {
            // Cache GUI color
            Color cachedGUIColor = GUI.color;

            // Use cached colors for separator
            Color contentColor = EditorGUIUtility.isProSkin ? new(1f, 1f, 1f, 0.2f) : new(0f, 0f, 0f, 0.25f);

            // Start drawing GUI color
            GUI.color = contentColor;

            GUILayout.BeginHorizontal(new GUIStyle()
            {
                fixedHeight = height,
                normal = { background = EditorGUIUtility.whiteTexture }
            });

            // Revert GUI color for other content
            GUI.color = cachedGUIColor;
            GUILayout.EndHorizontal();
        }

        static void DrawTextWithSpacing(string text, GUIStyle style, float spacing, Rect startRect)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Vector2 position = new(startRect.x, startRect.y); // Start position
            foreach (char c in text)
            {
                string charString = c.ToString();
                Vector2 charSize = style.CalcSize(new GUIContent(charString));
                GUI.Label(new Rect(position.x, position.y, charSize.x, startRect.height), charString, style);
                position.x += charSize.x + spacing; // Increment position with spacing
            }
        }
        #endregion

        #region Editor Hover Repaint
        /// <summary>
        /// Register an editor for hover repaints. Call this in OnEnable().
        /// </summary>
        public static void RegisterEditor(Editor editor)
        {
            if (editor == null)
                return;

            registeredEditors.Add(editor);

            if (!updateHandlerRegistered)
            {
                EditorApplication.update += HandleRepaints;
                updateHandlerRegistered = true;
            }
        }

        /// <summary>
        /// Unregister an editor from hover repaints. Call this in OnDisable().
        /// </summary>
        public static void UnregisterEditor(Editor editor)
        {
            registeredEditors.Remove(editor);

            if (registeredEditors.Count == 0 && updateHandlerRegistered)
            {
                EditorApplication.update -= HandleRepaints;
                updateHandlerRegistered = false;
            }
        }

        /// <summary>
        /// Request a repaint for hover effects. Called internally by hover-sensitive methods.
        /// </summary>
        static void RequestRepaint()
        {
            if (registeredEditors.Count > 10)
                return; // Safety valve

            needsRepaint = true;
        }

        /// <summary>
        /// Handle throttled hover repaints for all registered editors.
        /// </summary>
        static void HandleRepaints()
        {
            if (needsRepaint && EditorApplication.timeSinceStartup - lastRepaintTime > (EditorApplication.isPlaying ? RuntimeRepaintInterval : RepaintInterval))
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                needsRepaint = false;

                // Only repaint editors that are likely visible
                if (EditorWindow.focusedWindow != null)
                {
                    foreach (var editor in registeredEditors)
                    {
                        if (editor != null)
                            editor.Repaint();
                    }
                }

                // Clean up null references
                registeredEditors.RemoveWhere(editor => editor == null);
            }
        }

        /// <summary>
        /// Trigger hover effects.
        /// </summary>
        public static void HandleInspectorGUI()
        {
            if (CanHoverInspector())
                RequestRepaint();
        }
        #endregion

        #region Window Detection
        static bool IsValidWindow(EditorWindow window)
        {
            if (window == null) { return false; }
            if (!inspectorTypeCache.TryGetValue(window, out bool isInspector))
            {
                string typeName = window.GetType().Name;
                isInspector = typeName == "InspectorWindow" ||
                              window.titleContent.text.Contains("Inspector") ||
                              window is IEvoEditorGUIHandler;

                inspectorTypeCache[window] = isInspector;

                if (LimitCacheSize && inspectorTypeCache.Count > MaxInspectorCacheSize)
                {
                    // Find disposed windows
                    var disposedWindows = new List<EditorWindow>();
                    foreach (var win in inspectorTypeCache.Keys)
                    {
                        if (win == null || !win)
                            disposedWindows.Add(win);
                    }

                    // Remove disposed windows
                    foreach (var dw in disposedWindows) { inspectorTypeCache.Remove(dw); }

                    var keysToRemove = inspectorTypeCache.Keys.Take(inspectorTypeCache.Count / 2).ToList();
                    foreach (var key in keysToRemove) { inspectorTypeCache.Remove(key); }
                }
            }

            return isInspector;
        }

        static void UpdateWindowCache()
        {
            double currentTime = EditorApplication.timeSinceStartup;

            // Only update cache at intervals, not every frame
            if (currentTime - lastWindowCheckTime < WindowCheckInverval)
                return;

            lastWindowCheckTime = currentTime;

            cachedMouseOverWindow = EditorWindow.mouseOverWindow;
            cachedFocusedWindow = EditorWindow.focusedWindow;
            cachedMouseOverValidWindow = IsValidWindow(cachedMouseOverWindow);
            cachedFocusedIsValidWindow = IsValidWindow(cachedFocusedWindow);
        }

        static bool CanHoverInspector()
        {
            UpdateWindowCache();

            // Use cached values
            return cachedMouseOverValidWindow ||
                   (cachedFocusedIsValidWindow && (cachedMouseOverWindow == null || cachedMouseOverWindow == cachedFocusedWindow));
        }
        #endregion
    }
}