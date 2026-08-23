using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    public class Styler
    {
        // Cache
        static StylerPreset cachedDefaultPreset;
        public static readonly List<StylerObject> RegisteredObjects = new();

        // Buffer for zero-allocation component fetching
        static readonly List<IStylerHandler> handlerBuffer = new();

        // Clear for FEPM
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RegisteredObjects.Clear();
            cachedDefaultPreset = null;
        }

        /// <summary>
        /// Identify the item type.
        /// </summary>
        public enum ItemType
        {
            Audio,
            Color,
            Font,
            Sprite,
            Gradient,
            TextStyle
        }

        /// <summary>
        /// Used in StylerPreset to set the update mode.
        /// </summary>
        public enum UpdateMode
        {
            Always = 0,     // Updates every frame in both Editor and Play mode
            Adaptive = 1,   // Updates every frame in the Editor, but only once in Play mode
        }

        /// <summary>
        /// Used to specify AudioClip values. 
        /// </summary>
        [System.Serializable]
        public class AudioItem
        {
            public string itemID;
            public AudioClip audioAsset;
#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public AudioItem(string id = "", AudioClip asset = null)
            {
                itemID = id;
                audioAsset = asset;
            }
        }

        /// <summary>
        /// Used to specify Color values. 
        /// </summary>
        [System.Serializable]
        public class ColorItem
        {
            public string itemID;
            public Color colorValue;
#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public ColorItem(string id = "", Color color = default)
            {
                itemID = id;
                colorValue = color;
            }
        }

        /// <summary>
        /// Used to specify TMP Font values. 
        /// </summary>
        [System.Serializable]
        public class FontItem
        {
            public string itemID;
            public TMP_FontAsset fontAsset;
#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public FontItem(string id = "", TMP_FontAsset asset = null)
            {
                itemID = id;
                fontAsset = asset;
            }
        }

        /// <summary>
        /// Used to specify Text Style values and overrides.
        /// </summary>
        [System.Serializable]
        public class TextStyleItem
        {
            public string itemID;

            [Tooltip("Override TMP text size settings.")]
            public bool applySize = false;
            [Min(0f)] public float fontSize = 36f;
            public bool enableAutoSizing = false;
            [Min(0f)] public float fontSizeMin = 18f;
            [Min(0f)] public float fontSizeMax = 72f;
            public float characterWidthAdjustment = 0f;
            public float lineSpacingAdjustment = 0f;

            [Tooltip("Override TMP alignment settings.")]
            public bool applyAlignment = false;
            public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;

            [Tooltip("Override TMP wrapping and overflow settings.")]
            public bool applyWrappingAndOverflow = false;
            public bool enableWordWrapping = true;
            public TextOverflowModes overflowMode = TextOverflowModes.Overflow;

            [Tooltip("Override TMP spacing settings.")]
            public bool applySpacing = false;
            public float characterSpacing = 0f;
            public float wordSpacing = 0f;
            public float lineSpacing = 0f;
            public float paragraphSpacing = 0f;

            [Tooltip("Override TMP margin settings.")]
            public bool applyMargin = false;
            public Vector4 margin = Vector4.zero;

            [Tooltip("Override TMP font style settings.")]
            public bool applyFontStyle = false;
            public FontStyles fontStyle = FontStyles.Normal;

#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public TextStyleItem(string id = "")
            {
                itemID = id;
            }
        }

        /// <summary>
        /// Used to specify Sprite values and optional Image Component overrides. 
        /// </summary>
        [System.Serializable]
        public class SpriteItem
        {
            public string itemID;
            public Sprite spriteAsset;

            [Tooltip("Override Image component settings when applying this sprite.")]
            public bool applyImageSettings = false;
            public Image.Type imageType = Image.Type.Simple;
            public bool preserveAspect = false;
            public float pixelsPerUnitMultiplier = 1f;
            public Image.FillMethod fillMethod = Image.FillMethod.Radial360;
            public int fillOrigin = 0;
            public float fillAmount = 1f;
            public bool fillClockwise = true;

#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public SpriteItem(string id = "", Sprite asset = null)
            {
                itemID = id;
                spriteAsset = asset;
            }
        }

        /// <summary>
        /// Used to specify Gradient values. 
        /// </summary>
        [System.Serializable]
        public class GradientItem
        {
            public string itemID;
            public Gradient gradientValue;
#if UNITY_EDITOR
            [System.NonSerialized] public bool isExpanded = true;
#endif
            public GradientItem(string id = "", Gradient gradient = null)
            {
                itemID = id;
                gradientValue = gradient ?? new Gradient();
            }
        }

        /// <summary>
        /// Gets the default preset. 
        /// Prioritizes the preset defined in the config file. Falls back to default.
        /// </summary>
        public static StylerPreset GetDefaultPreset(bool generateLog = true, bool bypassCache = false)
        {
            if (!bypassCache && cachedDefaultPreset != null)
                return cachedDefaultPreset;

            // Try to load from config
            TextAsset config = Resources.Load<TextAsset>(Constants.StylerConfigPath);
            if (config != null && !string.IsNullOrEmpty(config.text))
            {
                string path = config.text.Trim();
                StylerPreset preset = Resources.Load<StylerPreset>(path);
                if (preset != null)
                {
                    cachedDefaultPreset = preset;
                    return cachedDefaultPreset;
                }
            }

            // Try the expected default path (fallback)
            StylerPreset defaultPreset = Resources.Load<StylerPreset>(Constants.StylerFallbackPath);
            if (defaultPreset != null)
            {
                cachedDefaultPreset = defaultPreset;
                return cachedDefaultPreset;
            }

            if (generateLog)
                Debug.LogWarning($"[Styler] No default preset found.");

            return null;
        }

        /// <summary>
        /// Get an audio clip from the provided mapping based on the specified source.
        /// </summary>
        public static AudioClip GetAudio(StylingSource source, AudioMapping mapping, StylerPreset stylerPreset)
        {
            // Early return in case source is set to none
            if (source == StylingSource.None)
                return null;

            // Check for StylerPreset
            if (source == StylingSource.StylerPreset && stylerPreset != null)
            {
                if (stylerPreset.TryGetAudio(mapping.stylerID, out var clip))
                    return clip;
            }

            // Fallback to custom audio
            return mapping.audioClip;
        }

        /// <summary>
        /// Get a color from the provided mapping based on the specified source.
        /// </summary>
        public static Color GetColor(StylingSource source, ColorMapping mapping, StylerPreset stylerPreset)
        {
            // Early return in case source is set to none
            if (source == StylingSource.None)
                return Color.clear;

            // Check for StylerPreset
            if (source == StylingSource.StylerPreset && stylerPreset != null)
            {
                // If stylerID is empty or null, return transparent
                if (string.IsNullOrEmpty(mapping.stylerID))
                    return Color.clear;

                if (stylerPreset.TryGetColor(mapping.stylerID, out var color))
                    return color;
            }

            // Fallback to custom color
            return mapping.color;
        }

        /// <summary>
        /// Get a TMP font from the provided mapping based on the specified source.
        /// </summary>
        public static TMP_FontAsset GetFont(StylingSource source, FontMapping mapping, StylerPreset stylerPreset)
        {
            // Early return in case source is set to none
            if (source == StylingSource.None)
                return null;

            // Check for StylerPreset
            if (source == StylingSource.StylerPreset && stylerPreset != null)
            {
                if (stylerPreset.TryGetFont(mapping.stylerID, out var font))
                    return font;
            }

            // Fallback to custom font
            return mapping.font;
        }

        /// <summary>
        /// Get a sprite from the provided mapping based on the specified source.
        /// </summary>
        public static Sprite GetSprite(StylingSource source, SpriteMapping mapping, StylerPreset stylerPreset)
        {
            // Early return in case source is set to none
            if (source == StylingSource.None)
                return null;

            // Check for StylerPreset
            if (source == StylingSource.StylerPreset && stylerPreset != null)
            {
                if (stylerPreset.TryGetSprite(mapping.stylerID, out var sprite))
                    return sprite;
            }

            // Fallback to custom sprite
            return mapping.sprite;
        }

        /// <summary>
        /// Get a gradient from the provided mapping based on the specified source.
        /// </summary>
        public static Gradient GetGradient(StylingSource source, GradientMapping mapping, StylerPreset stylerPreset)
        {
            // Early return in case source is set to none
            if (source == StylingSource.None)
                return null;

            // Check for StylerPreset
            if (source == StylingSource.StylerPreset && stylerPreset != null)
            {
                if (stylerPreset.TryGetGradient(mapping.stylerID, out var gradient))
                    return gradient;
            }

            // Fallback to custom gradient
            return mapping.gradient;
        }

        /// <summary>
        /// Helper method to update the cached default preset.
        /// </summary>
        public static void UpdateCachedDefaultPreset(StylerPreset newDefaultPreset)
        {
            if (newDefaultPreset != null)
                cachedDefaultPreset = newDefaultPreset;
        }

        /// <summary>
        /// Call this method to replace the preset asset globally.
        /// </summary>
        public static void ApplyPreset(StylerPreset newPreset, bool updateCache = true)
        {
            if (newPreset == null)
            {
                Debug.LogWarning($"[Styler] No preset specified when calling ApplyPreset. Operation cancelled.");
                return;
            }

            if (updateCache)
                UpdateCachedDefaultPreset(newPreset);

#if UNITY_6000_4_OR_NEWER
            var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
#else
            var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            int count = 0;
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is IStylerHandler target)
                {
                    target.Preset = newPreset;
                    count++;
                }
            }

            Debug.Log($"[Styler] Preset '{newPreset.name}' successfully set on {count} objects.");
        }

        /// <summary>
        /// Call this method to replace the preset asset for active objects.
        /// Faster than ApplyPreset, but will only apply to registered StylerObject references.
        /// </summary>
        public static void ApplyPresetToRegisteredObjects(StylerPreset newPreset, bool updateCache = true)
        {
            if (newPreset == null)
            {
                Debug.LogWarning($"[Styler] No preset specified when calling ApplyPreset. Operation cancelled.");
                return;
            }

            if (updateCache)
                UpdateCachedDefaultPreset(newPreset);

            // Iterate backwards so if an object removes itself, it doesn't break the loop
            for (int i = RegisteredObjects.Count - 1; i >= 0; i--)
            {
                var obj = RegisteredObjects[i];

                // In case Unity destroyed the object but list didn't update yet
                if (obj == null)
                {
                    RegisteredObjects.RemoveAt(i);
                    continue;
                }

                obj.Preset = newPreset;
            }

            Debug.Log($"[Styler] Preset '{newPreset.name}' successfully set on {RegisteredObjects.Count} registered objects.");
        }

        /// <summary>
        /// Call this method to apply the given preset to specified IStylerHandler target.
        /// </summary>
        public static void ApplyPresetTo(StylerPreset preset, IStylerHandler target)
        {
            if (preset != null && target != null)
                target.Preset = preset;
        }

        /// <summary>
        /// Call this method to apply the given preset to specified parent and optionally childs.
        /// </summary>
        public static void ApplyPresetTo(StylerPreset preset, GameObject target, bool includeChildren = true)
        {
            if (preset == null || target == null)
                return;

            // Clear the buffer to ensure no leftover references
            handlerBuffer.Clear();

            // Use the non-allocating list overloads
            if (includeChildren) { target.GetComponentsInChildren(true, handlerBuffer); }
            else { target.GetComponents(handlerBuffer); }

            // Iterate through the buffer
            int count = handlerBuffer.Count;
            for (int i = 0; i < count; i++)
                handlerBuffer[i].Preset = preset;
        }

        /// <summary>
        /// Call this method to apply the default preset to specified IStylerHandler target.
        /// </summary>
        public static void ApplyDefaultPresetTo(IStylerHandler target)
        {
            if (target == null)
                return;

            StylerPreset defaultPreset = GetDefaultPreset();
            if (defaultPreset == null)
            {
                Debug.LogWarning($"[Styler] Cannot apply default preset to {target} because no default preset was found.");
                return;
            }

            target.Preset = defaultPreset;
        }

        /// <summary>
        /// Call this method to apply the given preset to specified parent and optionally childs.
        /// </summary>
        public static void ApplyDefaultPresetTo(GameObject target, bool includeChildren = true)
        {
            if (target == null)
                return;

            StylerPreset defaultPreset = GetDefaultPreset();
            if (defaultPreset == null)
            {
                Debug.LogWarning($"[Styler] Cannot apply default preset to {target.name} because no default preset was found.");
                return;
            }

            ApplyPresetTo(defaultPreset, target, includeChildren);
        }
    }
}