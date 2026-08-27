using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/slider")]
    [AddComponentMenu("Evo/UI/UI Elements/Slider")]
    public class Slider : UnityEngine.UI.Slider, IStylerInteractable
    {
        [EvoHeader("References", Constants.CustomEditorID)]
        public TMP_Text valueText;
        public TMP_InputField valueInput;
        [SerializeField] private CanvasGroup highlightedCG;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool invokeAtStart;

        [EvoHeader("Formatting", Constants.CustomEditorID)]
        [Range(-1000f, 1000f)] public float displayMultiplier = 1f;
        public DisplayFormat displayFormat = DisplayFormat.Fixed0;
        public string textFormat = "{0}";

        [EvoHeader("Animation", Constants.CustomEditorID)]
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 2f)] private float transitionDuration = 0.1f;
        [SerializeField, Range(0f, 2f)] private float highlightedScale = 1.25f;
        [SerializeField, Range(0f, 2f)] private float pressedScale = 1.15f;

        // State tracking
        bool isPointerOver;
        bool isPressed;
        Coroutine scaleCoroutine;
        Coroutine highlightCoroutine;
        Vector3 originalScale = Vector3.one;

        // String formatting caches
        string cachedCompositeFormat;
        string lastTextFormat;
        DisplayFormat lastDisplayFormat;

        // IStylerInteractable Implementation
        public event System.Action<InteractionState> OnStateChanged;
        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = value;
        }
        public InteractionState InteractionState => currentInteractionState;
        InteractionState currentInteractionState = InteractionState.Normal;

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

        protected override void Awake()
        {
            base.Awake();

            if (!Application.isPlaying)
                return;

            if (handleRect != null)
                originalScale = handleRect.localScale;

            onValueChanged.AddListener(SetText);
            Utilities.AddRaycastGraphic(gameObject);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (highlightedCG != null)
                highlightedCG.alpha = 0;
        }

        protected override void Start()
        {
            // Update text on start
            SetText(value);

            // Invoke on start if enabled. Replaced delegate with direct method to avoid GC allocation
            if (valueInput != null) { valueInput.onEndEdit.AddListener(OnInputEndEdit); }
            if (invokeAtStart) { onValueChanged?.Invoke(value); }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (valueInput != null)
                valueInput.onEndEdit.RemoveListener(OnInputEndEdit);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            InteractionState newState = state switch
            {
                SelectionState.Normal => InteractionState.Normal,
                SelectionState.Highlighted => InteractionState.Highlighted,
                SelectionState.Pressed => InteractionState.Pressed,
                SelectionState.Selected => InteractionState.Selected,
                SelectionState.Disabled => InteractionState.Disabled,
                _ => InteractionState.Normal
            };

            if (currentInteractionState != newState)
            {
                currentInteractionState = newState;
                OnStateChanged?.Invoke(currentInteractionState);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (!IsInteractable())
                return;

            isPointerOver = true;
            UpdateAnimationState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (!IsInteractable())
                return;

            isPointerOver = false;
            UpdateAnimationState();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!IsInteractable())
                return;

            isPressed = true;
            UpdateAnimationState();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (!IsInteractable())
                return;

            isPressed = false;
            UpdateAnimationState();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (!IsInteractable())
                return;

            isPointerOver = true;
            UpdateAnimationState();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (!IsInteractable())
                return;

            isPointerOver = false;
            UpdateAnimationState();
        }

        void OnInputEndEdit(string text)
        {
            if (float.TryParse(text, out float result))
            {
                float rawValue = (displayMultiplier != 0f) ? result / displayMultiplier : result;
                value = rawValue;
            }
        }

        void UpdateAnimationState()
        {
            float targetScale = isPressed ? pressedScale : (isPointerOver ? highlightedScale : 1f);
            float targetAlpha = (isPressed || isPointerOver) ? 1f : 0f;

            AnimateHandleScale(targetScale);
            AnimateHighlight(targetAlpha);
        }

        void AnimateHandleScale(float targetScale)
        {
            if (handleRect == null)
                return;

            if (scaleCoroutine != null) { StopCoroutine(scaleCoroutine); }
            scaleCoroutine = StartCoroutine(ScaleHandleCoroutine(targetScale));
        }

        void AnimateHighlight(float targetAlpha)
        {
            if (highlightedCG == null)
                return;

            if (highlightCoroutine != null) { StopCoroutine(highlightCoroutine); }
            highlightCoroutine = StartCoroutine(Utilities.CrossFadeCanvasGroup(highlightedCG, targetAlpha, transitionDuration));
        }

        IEnumerator ScaleHandleCoroutine(float targetScale)
        {
            Vector3 startScale = handleRect.localScale;
            Vector3 endScale = originalScale * targetScale;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(Mathf.Clamp01(elapsed / transitionDuration));

                handleRect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                yield return null;
            }

            handleRect.localScale = endScale;
        }

        public void SetText(float val)
        {
            if (valueInput == null && valueText == null)
                return;

            string formattedText = GetFormattedText(val);

            // Only assign to text if it changed
            if (valueText != null && valueText.text != formattedText) { valueText.text = formattedText; }
            if (valueInput != null && valueInput.text != formattedText) { valueInput.SetTextWithoutNotify(formattedText); }
        }

        /// <summary>
        /// Generates a composite string format.
        /// </summary>
        public string GetFormattedText(float sValue)
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
                if (!tf.Contains("{0}"))
                    tf += " {0}";

                try
                {
                    // Convert "Vol: {0}" to "Vol: {0:F2}" so string.Format can do it all at once
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

        public string FormatValue(float sValue) => GetFormattedText(sValue);

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

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        protected override void OnValidate()
        {
            base.OnValidate();

            // Update display in editor when values change safely
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;

            // Clamp multiplier to reasonable values
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            if (valueText != null || valueInput != null)
                SetText(value);
        }
#endif
    }
}