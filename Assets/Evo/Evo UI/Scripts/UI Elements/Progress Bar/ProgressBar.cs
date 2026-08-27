using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/progress-bar")]
    [AddComponentMenu("Evo/UI/UI Elements/Progress Bar")]
    public class ProgressBar : MonoBehaviour
    {
        [EvoHeader("Bar Settings", Constants.CustomEditorID)]
        [SerializeField] private float value = 50f;
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool invokeAtStart;
        [SerializeField] private bool isVertical;
        [Range(-1000f, 1000f)] public float displayMultiplier = 1f;
        public DisplayFormat displayFormat = DisplayFormat.Fixed0;
        public string textFormat = "{0}";

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public bool enableSmoothing = true;
        [Range(0.05f, 4)] public float smoothingDuration = 0.3f;
        [SerializeField] private AnimationCurve smoothingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("References", Constants.CustomEditorID)]
        public RectTransform fillRect;
        public TMP_Text valueText;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public ProgressBarEvent onValueChanged = new();

        public enum DisplayFormat
        {
            // Fixed-point (F)
            [InspectorName("Fixed - No Decimals (0)")] Fixed0,
            [InspectorName("Fixed - 1 Decimal (0.0)")] Fixed1,
            [InspectorName("Fixed - 2 Decimals (0.00)")] Fixed2,
            [InspectorName("Fixed - 3 Decimals (0.000)")] Fixed3,
            [InspectorName("Fixed - 4 Decimals (0.0000)")] Fixed4,
            [InspectorName("Fixed - 5 Decimals (0.00000)")] Fixed5,

            // Number with thousands separator (N)
            [InspectorName("Number - No Decimals (1,234)")] Number0,
            [InspectorName("Number - 1 Decimal (1,234.5)")] Number1,
            [InspectorName("Number - 2 Decimals (1,234.56)")] Number2,
            [InspectorName("Number - 3 Decimals (1,234.567)")] Number3
        }

        // Cache
        Image fillImage;

        // String formatting caches
        string cachedCompositeFormat;
        string lastTextFormat;
        DisplayFormat lastDisplayFormat;

        // Helpers
        bool isAnimating;
        bool useImageFill;
        float previousValue;
        float animationStartValue;
        float animationTargetValue;
        float currentVisualValue; // Track exactly where the animation is right now
        Coroutine animationCoroutine;

        [System.Serializable] public class ProgressBarEvent : UnityEvent<float> { }

        // Properties
        public float Value
        {
            get { return value; }
            set { SetValue(value); }
        }

        public float MinValue
        {
            get { return minValue; }
            set
            {
                minValue = value;
                UpdateDisplay();
            }
        }

        public float MaxValue
        {
            get { return maxValue; }
            set
            {
                maxValue = value;
                UpdateDisplay();
            }
        }

        void Awake()
        {
            previousValue = value;
            currentVisualValue = value;
            CacheFillComponents();
        }

        void Start()
        {
            // Update display on start
            UpdateDisplay();

            // Invoke on start if enabled
            if (invokeAtStart) { onValueChanged?.Invoke(value); }
        }

        void OnEnable() => UpdateDisplay();

        void OnDisable()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            isAnimating = false;
        }

        void CacheFillComponents()
        {
            if (fillRect == null)
                return;

            // Only update if not cached or if the transform reference changed at runtime
            if (fillImage == null || fillImage.rectTransform != fillRect)
            {
                fillImage = fillRect.GetComponent<Image>();
                useImageFill = fillImage != null && fillImage.type == Image.Type.Filled;
            }
        }

        void AnimateToValue(float targetValue)
        {
            if (!gameObject.activeInHierarchy)
            {
                UpdateDisplay();
                return;
            }

            // Use the current visual location as start if currently animating, otherwise previous value
            animationStartValue = isAnimating ? currentVisualValue : previousValue;
            animationTargetValue = targetValue;
            isAnimating = true;

            if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
            animationCoroutine = StartCoroutine(AnimateValueCoroutine());
        }

        void UpdateDisplay()
        {
            CacheFillComponents();
            UpdateFillRect();
            UpdateText();
        }

        void UpdateFillRect()
        {
            if (fillRect == null)
                return;

            float displayValue = isAnimating ? GetCurrentAnimatedValue() : value;
            float normalizedDisplay = 0f;

            // InverseLerp already clamps the result between 0 and 1. No need for Mathf.Clamp01
            if (!Mathf.Approximately(MinValue, MaxValue)) { normalizedDisplay = Mathf.InverseLerp(MinValue, MaxValue, displayValue); }

            // Use Image.fillAmount for filled images
            if (useImageFill && fillImage != null) { fillImage.fillAmount = normalizedDisplay; }
            else if (!useImageFill)
            {
                if (isVertical) { fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, normalizedDisplay); }
                else { fillRect.anchorMax = new Vector2(normalizedDisplay, fillRect.anchorMax.y); }
            }
        }

        void UpdateText()
        {
            if (valueText == null)
                return;

            float displayValue = isAnimating ? GetCurrentAnimatedValue() : value;
            string newText = GetFormattedText(displayValue);

            // Only assign to text if it changed
            if (valueText.text != newText) { valueText.text = newText; }
        }

        float GetCurrentAnimatedValue() => isAnimating ? currentVisualValue : value;

        string GetFormatString()
        {
            return displayFormat switch
            {
                // Fixed-point
                DisplayFormat.Fixed0 => "F0",
                DisplayFormat.Fixed1 => "F1",
                DisplayFormat.Fixed2 => "F2",
                DisplayFormat.Fixed3 => "F3",
                DisplayFormat.Fixed4 => "F4",
                DisplayFormat.Fixed5 => "F5",

                // Number with thousands
                DisplayFormat.Number0 => "N0",
                DisplayFormat.Number1 => "N1",
                DisplayFormat.Number2 => "N2",
                DisplayFormat.Number3 => "N3",

                // Default
                _ => "F0",
            };
        }

        /// <summary>
        /// Generates a composite string format.
        /// </summary>
        string GetFormattedText(float sValue)
        {
            float finalDisplayValue = sValue * displayMultiplier;

            // Rebuild the cached composite format only if settings changed
            if (cachedCompositeFormat == null || lastTextFormat != textFormat || lastDisplayFormat != displayFormat)
            {
                lastTextFormat = textFormat ?? string.Empty;
                lastDisplayFormat = displayFormat;

                string numFormat = GetFormatString();
                string tf = string.IsNullOrEmpty(lastTextFormat) ? "{0}" : lastTextFormat;

                // Force {0} placeholder if missing
                if (!tf.Contains("{0}")) { tf += " {0}"; }

                try
                {
                    // Convert "HP: {0}" to "HP: {0:F2}" so string.Format can do it all at once
                    cachedCompositeFormat = tf.Replace("{0}", "{0:" + numFormat + "}");

                    // Test if the format is valid
                    _ = string.Format(cachedCompositeFormat, 0f);
                }
                catch (System.FormatException)
                {
                    // Fallback if the user typed something invalid (like an unescaped { or {1})
                    // Escape braces so string.Format treats it as plain text and doesn't crash
                    string safeFormat = (lastTextFormat ?? string.Empty).Replace("{", "{{").Replace("}", "}}");
                    cachedCompositeFormat = safeFormat + " {0:" + numFormat + "}";
                }
            }

            return string.Format(cachedCompositeFormat, finalDisplayValue);
        }

        public void SetValue(float newValue)
        {
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            if (value != clampedValue)
            {
                previousValue = value; // Store the old value
                value = clampedValue;

                if (enableSmoothing) { AnimateToValue(value); }
                else
                {
                    isAnimating = false;
                    currentVisualValue = value;
                    UpdateDisplay();
                }

                onValueChanged?.Invoke(value);
            }
        }

        public void SetValueWithoutNotify(float newValue)
        {
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            if (value != clampedValue)
            {
                previousValue = value; // Store the old value
                value = clampedValue;

                if (enableSmoothing) { AnimateToValue(value); }
                else
                {
                    isAnimating = false;
                    currentVisualValue = value;
                    UpdateDisplay();
                }
            }
        }

        public void SetValueInstant(float newValue)
        {
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            previousValue = clampedValue;
            value = clampedValue;
            currentVisualValue = clampedValue;

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            isAnimating = false;
            UpdateDisplay();
            onValueChanged?.Invoke(value);
        }

        IEnumerator AnimateValueCoroutine()
        {
            float elapsedTime = 0f;

            while (elapsedTime < smoothingDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float normalizedTime = elapsedTime / smoothingDuration;
                float curveValue = smoothingCurve.Evaluate(normalizedTime);

                // Ensure we cache the newly interpolated visual progress locally
                currentVisualValue = Mathf.Lerp(animationStartValue, animationTargetValue, curveValue);

                float normalizedDisplay = 0f;
                if (!Mathf.Approximately(MinValue, MaxValue)) { normalizedDisplay = Mathf.InverseLerp(MinValue, MaxValue, currentVisualValue); }

                // Update fill rect
                if (fillRect != null)
                {
                    if (useImageFill && fillImage != null) { fillImage.fillAmount = normalizedDisplay; }
                    else if (!useImageFill)
                    {
                        if (isVertical) { fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, normalizedDisplay); }
                        else { fillRect.anchorMax = new Vector2(normalizedDisplay, fillRect.anchorMax.y); }
                    }
                }

                // Update text
                if (valueText != null)
                {
                    string newText = GetFormattedText(currentVisualValue);
                    if (valueText.text != newText) { valueText.text = newText; }
                }

                yield return null;
            }

            currentVisualValue = animationTargetValue;
            isAnimating = false;
            animationCoroutine = null;
            UpdateDisplay();
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Ensure min <= max
            if (minValue > maxValue) { minValue = maxValue; }

            // Clamp value to valid range
            value = Mathf.Clamp(value, minValue, maxValue);

            // Clamp multiplier to reasonable values
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);

            // Clamp duration to reasonable values
            smoothingDuration = Mathf.Max(smoothingDuration, 0.01f);

            // Update display in editor when values change safely
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                UpdateDisplay();
        }
#endif
    }
}