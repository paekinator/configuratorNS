using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "styler")]
    [AddComponentMenu("Evo/UI/Styler/Styler Object")]
    public class StylerObject : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("References", Constants.CustomEditorID)]
        [Tooltip("Defines object's preset source.")]
        public PresetSource presetSource = PresetSource.UserDefined;
        [Tooltip("The preset containing style definitions.")]
        [SerializeField] private StylerPreset preset;
        [UnityEngine.Serialization.FormerlySerializedAs("targetImage")]
        [Tooltip("Graphic component to style")]
        public Graphic targetGraphic;
        [Tooltip("TextMeshPro component to style.")]
        public TMP_Text targetText;
        [Tooltip("ImageGradient component to style.")]
        public ImageGradient targetGradient;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [Tooltip("Type of UI object to style.")]
        public ObjectType objectType = ObjectType.Graphic;
        public string colorID = "Primary";
        public string fontID = "Regular";
        public string textStyleID = "";
        public string gradientID = "";
        public string spriteID = "";
        [Tooltip("Set a current color instead of getting from the preset.")]
        public bool useCustomColor = false;
        [Tooltip("Override the alpha channel with a custom value.")]
        public bool overrideAlpha = false;
        [Tooltip("Alpha value to apply (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)] public float alphaOverride = 1f;

        [EvoHeader("Interaction", Constants.CustomEditorID)]
        [Tooltip("Enable state-based interactions.")]
        public bool enableInteraction = false;
        public Selectable interactableObject;

        [Tooltip("Enable state-based color animation.")]
        public bool enableColorInteraction = true;
        public ColorMapping disabledColor = new() { stylerID = "Primary" };
        public ColorMapping normalColor = new() { stylerID = "Primary" };
        public ColorMapping highlightedColor = new() { stylerID = "Primary" };
        public ColorMapping pressedColor = new() { stylerID = "Primary" };
        public ColorMapping selectedColor = new() { stylerID = "Primary" };

        [Tooltip("Enable state-based font swapping.")]
        public bool enableFontInteraction = false;
        public FontMapping disabledFont = new() { stylerID = "Regular" };
        public FontMapping normalFont = new() { stylerID = "Regular" };
        public FontMapping highlightedFont = new() { stylerID = "Regular" };
        public FontMapping pressedFont = new() { stylerID = "Regular" };
        public FontMapping selectedFont = new() { stylerID = "Regular" };

        // IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value)
                    return;

                preset = value;
                UpdateStyler();
            }
        }

        // Enums
        public enum PresetSource
        {
            [Tooltip("Custom option. Styler Preset can be manually assigned.")]
            UserDefined = 0,

            [Tooltip("Locked to the current default Styler Preset.")]
            Default = 1
        }

        public enum ObjectType
        {
            [Tooltip("Sets the color of the Graphic variable.")]
            Graphic = 0,

            [Tooltip("Sets the font and color of the TMP variable.")]
            TMPText = 1,

            [Tooltip("Sets the sprite and color of the Image variable.")]
            [InspectorName("Image (Sprite)")] Image = 2,

            [Tooltip("Sets the gradient of the ImageGradient variable.")]
            Gradient = 3
        }

        // Cache
        readonly List<string> cachedColorIDs = new();
        readonly List<string> cachedFontIDs = new();
        readonly List<string> cachedTextStyleIDs = new();
        readonly List<string> cachedGradientIDs = new();
        readonly List<string> cachedSpriteIDs = new();
        [System.NonSerialized] Gradient cachedGradientCopy;

        // State
        Coroutine tweenCoroutine;
        InteractionState currentState;
        IStylerInteractable interactableInterface;

        void Awake() => Styler.RegisteredObjects.Add(this);

        void OnEnable()
        {
            tweenCoroutine = null;

            // Subscribe to catch future states
            if (enableInteraction && interactableObject != null)
            {
                interactableInterface = interactableObject as IStylerInteractable;
                if (interactableInterface != null)
                {
                    interactableInterface.OnStateChanged += OnInteractableStateChanged;
                    currentState = interactableInterface.InteractionState;
                }
            }

            // Apply the visuals
            UpdateStyler();
        }

        void OnDisable()
        {
            // Unsubscribe to bypass animating hidden objects
            if (enableInteraction && interactableInterface != null)
                interactableInterface.OnStateChanged -= OnInteractableStateChanged;

            // Cleanup tweens
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;

                if (currentState != InteractionState.Selected && currentState != InteractionState.Disabled)
                    currentState = InteractionState.Normal;
            }
        }

        void OnDestroy() => Styler.RegisteredObjects.Remove(this);

        void CheckComponents()
        {
            switch (objectType)
            {
                case ObjectType.Graphic:
                case ObjectType.Image:
                default:
                    if (targetGraphic == null) { TryGetComponent(out targetGraphic); }
                    break;
                case ObjectType.TMPText:
                    if (targetText == null) { TryGetComponent(out targetText); }
                    break;
                case ObjectType.Gradient:
                    if (targetGradient == null) { TryGetComponent(out targetGradient); }
                    if (targetGraphic == null && targetGradient != null) { targetGradient.TryGetComponent(out targetGraphic); }
                    break;
            }
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (currentState == newState || !gameObject.activeInHierarchy || objectType == ObjectType.Gradient)
                return;

            currentState = newState;
            AnimateToState(newState);
        }

        void AnimateToState(InteractionState state)
        {
            if (objectType == ObjectType.Gradient)
                return;

            if ((objectType == ObjectType.TMPText && targetText == null) ||
                ((objectType == ObjectType.Graphic || objectType == ObjectType.Image) && targetGraphic == null))
                return;

            // Handle Font instantly (no crossfade for fonts)
            if (enableFontInteraction && objectType == ObjectType.TMPText)
            {
                TMP_FontAsset tFont = GetInteractionFont(state, targetText.font);
                if (tFont != null && targetText.font != tFont)
                    targetText.font = tFont;
            }

            // Handle Color transition
            if (enableColorInteraction)
            {
                float tDuration = Mathf.Max(0f, interactableObject is IStylerInteractable interactable ? interactable.TransitionDuration : 0f);
                Graphic tGraphic = objectType == ObjectType.TMPText ? targetText : targetGraphic;
                Color tColor = GetInteractionColor(state, tGraphic.color);

                if (tweenCoroutine != null) { StopCoroutine(tweenCoroutine); }
                tweenCoroutine = StartCoroutine(Utilities.CrossFadeGraphic(tGraphic, tColor, tDuration));
            }
        }

        TMP_FontAsset GetTargetFont(TMP_FontAsset currentFont)
        {
            if (enableInteraction && enableFontInteraction && interactableObject is IStylerInteractable interactable)
                return GetInteractionFont(interactable.InteractionState, currentFont);

            if (string.IsNullOrEmpty(fontID) || preset == null)
                return currentFont;

            if (preset.TryGetFont(fontID, out var baseFont))
                return baseFont;

            return currentFont; // Fallback
        }

        Color GetTargetColor(Color currentColor)
        {
            if (enableInteraction && enableColorInteraction && interactableObject is IStylerInteractable interactable)
                return GetInteractionColor(interactable.InteractionState, currentColor);

            if (string.IsNullOrEmpty(colorID) || preset == null)
                return Color.clear;

            if (preset.TryGetColor(colorID, out Color baseColor))
            {
                if (overrideAlpha)
                    return new Color(baseColor.r, baseColor.g, baseColor.b, alphaOverride);

                return baseColor;
            }

            return currentColor; // Fallback
        }

        TMP_FontAsset GetInteractionFont(InteractionState state, TMP_FontAsset currentFont)
        {
            FontMapping mapping = GetFontMappingForState(state);
            if (mapping == null) { return currentFont; }

            if (preset != null)
            {
                if (string.IsNullOrEmpty(mapping.stylerID))
                    return currentFont;

                if (preset.TryGetFont(mapping.stylerID, out TMP_FontAsset baseFont))
                    return baseFont;

                return mapping.font; // Fallback to custom font if TryGetFont fails
            }

            return mapping.font; // Fallback to manual font mapping if preset is null
        }

        Color GetInteractionColor(InteractionState state, Color currentColor)
        {
            ColorMapping mapping = GetColorMappingForState(state);
            if (mapping == null) { return currentColor; }

            Color targetColor;

            // Use Styler Preset color
            if (!useCustomColor && preset != null)
            {
                if (string.IsNullOrEmpty(mapping.stylerID))
                    targetColor = Color.clear;
                else if (preset.TryGetColor(mapping.stylerID, out Color baseColor))
                    targetColor = baseColor;
                else
                    targetColor = mapping.color; // Fallback to custom color if missing ID or TryGetColor fails
            }
            else
            {
                // Fallback to custom color if useCustomColor is true or preset is null
                targetColor = mapping.color;
            }

            if (overrideAlpha)
                return new Color(targetColor.r, targetColor.g, targetColor.b, alphaOverride);

            return targetColor;
        }

        FontMapping GetFontMappingForState(InteractionState state)
        {
            return state switch
            {
                InteractionState.Disabled => disabledFont,
                InteractionState.Normal => normalFont,
                InteractionState.Highlighted => highlightedFont,
                InteractionState.Pressed => pressedFont,
                InteractionState.Selected => selectedFont,
                _ => null
            };
        }

        ColorMapping GetColorMappingForState(InteractionState state)
        {
            return state switch
            {
                InteractionState.Disabled => disabledColor,
                InteractionState.Normal => normalColor,
                InteractionState.Highlighted => highlightedColor,
                InteractionState.Pressed => pressedColor,
                InteractionState.Selected => selectedColor,
                _ => null
            };
        }

        void ApplyTextStyleSettings(TMP_Text targetText, Styler.TextStyleItem item)
        {
            if (targetText == null || item == null)
                return;

            if (item.applySize)
            {
                if (targetText.enableAutoSizing != item.enableAutoSizing)
                    targetText.enableAutoSizing = item.enableAutoSizing;

                if (!item.enableAutoSizing && targetText.fontSize != item.fontSize) { targetText.fontSize = item.fontSize; }
                else
                {
                    if (targetText.fontSizeMin != item.fontSizeMin)
                        targetText.fontSizeMin = item.fontSizeMin;

                    if (targetText.fontSizeMax != item.fontSizeMax)
                        targetText.fontSizeMax = item.fontSizeMax;

                    if (targetText.characterWidthAdjustment != item.characterWidthAdjustment)
                        targetText.characterWidthAdjustment = item.characterWidthAdjustment;

                    if (targetText.lineSpacingAdjustment != item.lineSpacingAdjustment)
                        targetText.lineSpacingAdjustment = item.lineSpacingAdjustment;
                }
            }

            if (item.applyAlignment && targetText.alignment != item.alignment)
                targetText.alignment = item.alignment;

            if (item.applyWrappingAndOverflow)
            {
#if UNITY_6000_0_OR_NEWER
                TextWrappingModes targetWrapMode = item.enableWordWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                if (targetText.textWrappingMode != targetWrapMode)
                    targetText.textWrappingMode = targetWrapMode;
#else
                if (targetText.enableWordWrapping != item.enableWordWrapping)
                    targetText.enableWordWrapping = item.enableWordWrapping;
#endif
                if (targetText.overflowMode != item.overflowMode)
                    targetText.overflowMode = item.overflowMode;
            }

            if (item.applySpacing)
            {
                if (targetText.characterSpacing != item.characterSpacing)
                    targetText.characterSpacing = item.characterSpacing;

                if (targetText.wordSpacing != item.wordSpacing)
                    targetText.wordSpacing = item.wordSpacing;
              
                if (targetText.lineSpacing != item.lineSpacing)
                    targetText.lineSpacing = item.lineSpacing;
               
                if (targetText.paragraphSpacing != item.paragraphSpacing)
                    targetText.paragraphSpacing = item.paragraphSpacing;
            }

            if (item.applyMargin && targetText.margin != item.margin)
                targetText.margin = item.margin;

            if (item.applyFontStyle && targetText.fontStyle != item.fontStyle)
                targetText.fontStyle = item.fontStyle;
        }

        void ApplySpriteSettings(Image targetImage, Styler.SpriteItem item)
        {
            if (targetImage == null)
                return;

            Sprite targetSprite = item?.spriteAsset;

            if (targetImage.sprite != targetSprite)
                targetImage.sprite = targetSprite;

            if (item != null && item.applyImageSettings)
            {
                if (targetImage.type != item.imageType)
                    targetImage.type = item.imageType;
               
                if (targetImage.preserveAspect != item.preserveAspect)
                    targetImage.preserveAspect = item.preserveAspect;

                if (targetImage.pixelsPerUnitMultiplier != item.pixelsPerUnitMultiplier)
                    targetImage.pixelsPerUnitMultiplier = item.pixelsPerUnitMultiplier;

                if (item.imageType == Image.Type.Filled)
                {
                    if (targetImage.fillMethod != item.fillMethod)
                        targetImage.fillMethod = item.fillMethod;
                    
                    if (targetImage.fillOrigin != item.fillOrigin)
                        targetImage.fillOrigin = item.fillOrigin;

                    if (targetImage.fillAmount != item.fillAmount)
                        targetImage.fillAmount = item.fillAmount;

                    if (targetImage.fillClockwise != item.fillClockwise)
                        targetImage.fillClockwise = item.fillClockwise;
                }
            }
        }

        /// <summary>
        /// Force-updates the styler object. Useful to call when any of its parameters are changed.
        /// </summary>
        public void UpdateStyler()
        {
            if (presetSource == PresetSource.Default)
                preset = Styler.GetDefaultPreset(false);

            CheckComponents();

            if (targetGraphic == null && targetText == null && targetGradient == null)
                return;

            // Handle Gradient completely independently
            if (objectType == ObjectType.Gradient)
            {
                if (preset != null && targetGradient != null && preset.TryGetGradient(gradientID, out var grad))
                {
                    // Reuse the cached Gradient instance instead of instantiating 'new Gradient()' every update
                    cachedGradientCopy ??= new Gradient();
                    cachedGradientCopy.SetKeys(grad.colorKeys, grad.alphaKeys);
                    cachedGradientCopy.mode = grad.mode;

                    targetGradient.SetGradient(cachedGradientCopy);

                    // Force targetGraphic to redraw immediately.
                    if (targetGraphic != null)
                        targetGraphic.SetVerticesDirty();

#if UNITY_EDITOR
                    // Tell Unity the object changed so the Inspector visually updates the gradient block
                    if (!Application.isPlaying)
                        UnityEditor.EditorUtility.SetDirty(targetGradient);
#endif
                }

                // Gradients don't process standard colors or interaction
                return;
            }

            // Handle Fonts, Text Styles & Sprites
            if (objectType == ObjectType.TMPText && targetText != null)
            {
                TMP_FontAsset currentFont = targetText.font;
                TMP_FontAsset targetFont = GetTargetFont(currentFont);
                
                if (targetFont != null && targetText.font != targetFont)
                    targetText.font = targetFont;

                if (!string.IsNullOrEmpty(textStyleID) && preset != null && preset.TryGetTextStyleItem(textStyleID, out var textStyleItem))
                    ApplyTextStyleSettings(targetText, textStyleItem);
            }
            else if (preset != null && objectType == ObjectType.Image && targetGraphic is Image targetImage)
            {
                if (!string.IsNullOrEmpty(spriteID) && preset.TryGetSpriteItem(spriteID, out var spriteItem))
                    ApplySpriteSettings(targetImage, spriteItem);
            }

            // Handle Colors
            if (!enableInteraction && preset == null && !useCustomColor)
                return;

            if (useCustomColor && (!enableInteraction || !enableColorInteraction))
                return; // User is manually managing the graphic's color

            // Stop any active interaction transitions to prevent color fighting
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;
            }

            Color currentColor = objectType == ObjectType.TMPText && targetText != null
                ? targetText.color
                : (targetGraphic != null ? targetGraphic.color : Color.white);

            Color targetColor = GetTargetColor(currentColor);

            if (objectType == ObjectType.TMPText && targetText != null && targetText.color != targetColor)
                targetText.color = targetColor;
            else if (targetGraphic != null && targetGraphic.color != targetColor)
                targetGraphic.color = targetColor;
        }

        public List<string> GetAvailableColorIDs()
        {
            cachedColorIDs.Clear();

            if (preset == null)
                return cachedColorIDs;

            for (int i = 0; i < preset.colorItems.Count; i++)
                cachedColorIDs.Add(preset.colorItems[i].itemID);

            return cachedColorIDs;
        }

        public List<string> GetAvailableFontIDs()
        {
            cachedFontIDs.Clear();

            if (preset == null)
                return cachedFontIDs;

            for (int i = 0; i < preset.fontItems.Count; i++)
                cachedFontIDs.Add(preset.fontItems[i].itemID);

            return cachedFontIDs;
        }

        public List<string> GetAvailableTextStyleIDs()
        {
            cachedTextStyleIDs.Clear();

            if (preset == null)
                return cachedTextStyleIDs;

            for (int i = 0; i < preset.textStyleItems.Count; i++)
                cachedTextStyleIDs.Add(preset.textStyleItems[i].itemID);

            return cachedTextStyleIDs;
        }

        public List<string> GetAvailableSpriteIDs()
        {
            cachedSpriteIDs.Clear();

            if (preset == null)
                return cachedSpriteIDs;

            for (int i = 0; i < preset.spriteItems.Count; i++)
                cachedSpriteIDs.Add(preset.spriteItems[i].itemID);

            return cachedSpriteIDs;
        }

        public List<string> GetAvailableGradientIDs()
        {
            cachedGradientIDs.Clear();

            if (preset == null)
                return cachedGradientIDs;

            for (int i = 0; i < preset.gradientItems.Count; i++)
                cachedGradientIDs.Add(preset.gradientItems[i].itemID);

            return cachedGradientIDs;
        }

        #region Obsolete
        [System.Obsolete("Use UpdateStyler() instead.")]
        public void UpdateStyle() => UpdateStyler();

        [System.Obsolete("Use targetGraphic instead.")]
        public Image targetImage
        {
            get => targetGraphic as Image;
            set => targetGraphic = value;
        }
        #endregion

#if UNITY_EDITOR
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool interactionFoldout = true;

        void Reset()
        {
            CheckComponents();
            preset = Styler.GetDefaultPreset(false);

            if (preset != null)
            {
                if (preset.fontItems.Count > 0 && preset.fontItems[0] != null)
                    fontID = preset.fontItems[0].itemID;

                if (preset.textStyleItems.Count > 0 && preset.textStyleItems[0] != null)
                    textStyleID = preset.textStyleItems[0].itemID;

                if (preset.colorItems.Count > 0 && preset.colorItems[0] != null)
                    colorID = preset.colorItems[0].itemID;
            }
        }

        void OnValidate()
        {
            if (!enabled)
                return;

            UpdateStyler();
        }
#endif
    }
}