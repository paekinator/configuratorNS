using System.Collections;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "animation/counter")]
    [AddComponentMenu("Evo/UI/Animation/Counter")]
    public class Counter : MonoBehaviour
    {
        [EvoHeader("References", Constants.CustomEditorID)]
        public TextMeshProUGUI textObject;

        [EvoHeader("Value Settings", Constants.CustomEditorID)]
        public float value = 100f;
        public string textFormat = "${0}";
        public DisplayFormat displayFormat = DisplayFormat.Number0;

        [EvoHeader("Animation Settings", Constants.CustomEditorID)]
        [SerializeField] private bool animateOnEnable = false;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Range(0.01f, 20f)] private float counterDuration = 1f;
        [SerializeField, Range(0f, 5f)] private float delay = 0f;
        [SerializeField] private AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0.5f, 0f));

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
            [InspectorName("Number - 3 Decimals (1,234.567)")] Number3,
        }

        // Runtime variables
        float currentValue;
        Coroutine counterCoroutine;

        // Caching for GC optimization
        string cachedFormatString;
        string cachedTextFormat;

        void Awake()
        {
            if (textObject == null)
                TryGetComponent(out textObject);

            UpdateCachedFormats();
        }

        void Start()
        {
            // Try to parse current text value if it exists
            TryParseCurrentValue();

            // Set initial display
            UpdateDisplay(currentValue);
        }

        void OnEnable()
        {
            UpdateCachedFormats();

            // Animate to target on start if enabled
            if (animateOnEnable && value != currentValue) { AnimateToTarget(); }
            else if (!animateOnEnable && value != currentValue) { SetValueInstant(value); }
        }

        void OnDisable()
        {
            if (counterCoroutine != null)
            {
                StopCoroutine(counterCoroutine);
                currentValue = value;
                UpdateDisplay(currentValue);
                counterCoroutine = null;
            }
        }

        void TryParseCurrentValue()
        {
            if (textObject != null && !string.IsNullOrEmpty(textObject.text))
            {
                string cleanText = textObject.text;

                // Try to extract number from formatted text
                // Remove common prefixes/suffixes and separators
                cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"[^\d\.\-\+]", "");

                // Reverse the display multiplier if it was applied
                if (float.TryParse(cleanText, out float parsedValue)) { currentValue = parsedValue; }
            }
        }

        void UpdateCachedFormats()
        {
            cachedFormatString = GetFormatString();

            if (string.IsNullOrEmpty(textFormat)) { cachedTextFormat = string.Empty; }
            else { cachedTextFormat = textFormat.Contains("{0}") ? textFormat : textFormat + " {0}"; }
        }

        void AnimateToTarget()
        {
            UpdateCachedFormats();

            if (counterCoroutine != null) { StopCoroutine(counterCoroutine); }
            if (gameObject.activeInHierarchy) { counterCoroutine = StartCoroutine(AnimateCoroutine()); }
            else
            {
                currentValue = value;
                UpdateDisplay(currentValue);
            }
        }

        void UpdateDisplay(float value)
        {
            if (textObject == null)
                return;

            // Get formatted string
            string formattedText = FormatValue(value);

            // Set formatted text
            textObject.text = formattedText;
        }

        string FormatValue(float sValue)
        {
            // Format the number using cached format
            string formattedNumber = sValue.ToString(cachedFormatString);

            // If textFormat is empty, use just the number
            if (string.IsNullOrEmpty(cachedTextFormat)) { return formattedNumber; }

            // Apply pre-computed cached text format
            return string.Format(cachedTextFormat, formattedNumber);
        }

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

        IEnumerator AnimateCoroutine()
        {
            if (delay > 0)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            float elapsedTime = 0f;
            float startValue = currentValue;
            float endValue = value;

            while (elapsedTime < counterDuration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / counterDuration);

                // Apply easing using smoothstep for better smoothness, then apply curve
                float smoothTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
                float curveValue = animationCurve.Evaluate(smoothTime);

                // Lerp between start and end values
                currentValue = Mathf.Lerp(startValue, endValue, curveValue);
                UpdateDisplay(currentValue);

                yield return null;
            }

            // Ensure end at exactly the target value
            currentValue = endValue;
            UpdateDisplay(currentValue);
            counterCoroutine = null;
        }

        public void SetValue(float newValue)
        {
            value = newValue;

            if (gameObject.activeInHierarchy) { AnimateToTarget(); }
            else
            {
                currentValue = value;
                UpdateDisplay(currentValue);
            }
        }

        public void SetValue(int newValue) => SetValue((float)newValue);

        public void SetValueInstant(float newValue)
        {
            if (counterCoroutine != null)
            {
                StopCoroutine(counterCoroutine);
                counterCoroutine = null;
            }

            value = newValue;
            currentValue = newValue;
            UpdateCachedFormats();
            UpdateDisplay(currentValue);
        }

        public void SetValueInstant(int newValue) => SetValueInstant((float)newValue);

        public void AddToValue(float amount) => SetValue(value + amount);

        public void SubtractFromValue(float amount) => SetValue(value - amount);

#if UNITY_EDITOR
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool animationFoldout = true;
        [HideInInspector] public bool formattingFoldout = true;
        [HideInInspector] public bool valueFoldout = true;

        void OnValidate()
        {
            if (textObject == null)
                TryGetComponent(out textObject);

            UpdateCachedFormats();

            if (textObject != null && Application.isPlaying) { UpdateDisplay(currentValue); }
            else if (textObject != null && !Application.isPlaying)
            {
                // In editor, show the target value formatted
                string previewText = FormatValue(value);
                textObject.text = previewText;
            }
        }
#endif
    }
}