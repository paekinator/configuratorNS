using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/radial-slider")]
    [AddComponentMenu("Evo/UI/UI Elements/Radial Slider")]
    public class RadialSlider : Interactive, IDragHandler, IInitializePotentialDragHandler
    {
        [EvoHeader("Slider Settings", Constants.CustomEditorID)]
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private float value = 25f;
        [SerializeField] private bool wholeNumbers;
        [SerializeField] private bool invokeAtStart;

        [EvoHeader("Radial Settings", Constants.CustomEditorID)]
        [Range(10f, 360f)] public float angleRange = 360f;
        [Range(0f, 360f)] public float startAngle = 270f;
        [Range(-50f, 50f)] public float handleOffset = 0f;
        public bool allowContinuousDrag = false;
        public bool clockwise = true;

        [EvoHeader("Formatting", Constants.CustomEditorID)]
        [Range(-1000f, 1000f)] public float displayMultiplier = 1f;
        public Slider.DisplayFormat displayFormat = Slider.DisplayFormat.Fixed0;
        public string textFormat = "{0}";

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private RectTransform handleRect;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_InputField valueInput;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public RadialSliderEvent onValueChanged = new();

        // Helpers
        RectTransform cachedRectTransform;

        [System.Serializable] public class RadialSliderEvent : UnityEvent<float> { }

        public float MinValue
        {
            get => minValue;
            set
            {
                if (minValue != value)
                {
                    minValue = value;
                    Set(this.value);
                    UpdateVisuals();
                }
            }
        }

        public float MaxValue
        {
            get => maxValue;
            set
            {
                if (maxValue != value)
                {
                    maxValue = value;
                    Set(this.value);
                    UpdateVisuals();
                }
            }
        }

        public bool WholeNumbers
        {
            get => wholeNumbers; 
            set
            {
                if (wholeNumbers != value)
                {
                    wholeNumbers = value;
                    Set(this.value);
                    UpdateVisuals();
                }
            }
        }

        public virtual float Value
        {
            get
            {
                if (wholeNumbers)
                    return Mathf.Round(value);

                return value;
            }
            set => Set(value);
        }

        public float NormalizedValue
        {
            get
            {
                if (Mathf.Approximately(minValue, maxValue))
                    return 0;

                return Mathf.InverseLerp(minValue, maxValue, Value);
            }
            set => Value = Mathf.Lerp(minValue, maxValue, value);
        }

        protected override void Awake()
        {
            base.Awake();

            // Cache the RectTransform for performance
            cachedRectTransform = transform as RectTransform;

            // Subscribe to events
            onValueChanged.AddListener(SetText);
        }

        protected override void Start()
        {
            base.Start();

            // Setup input field
            if (valueInput != null)
            {
                valueInput.onEndEdit.AddListener(delegate
                {
                    if (float.TryParse(valueInput.text, out float inputValue))
                        Value = inputValue;
                });
            }

            // Update visuals and invoke on start if enabled
            UpdateVisuals();
            SetText(Value);

            // Trigger events
            if (invokeAtStart) { onValueChanged?.Invoke(Value); }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData))
                return;

            base.OnPointerDown(eventData);

            // Calculate where the user clicked and set the value there immediately
            if (cachedRectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedRectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localCursor))
                {
                    // Calculate the clicked angle
                    float clickedAngle = Mathf.Atan2(localCursor.y, localCursor.x) * Mathf.Rad2Deg;
                    if (clickedAngle < 0) { clickedAngle += 360f; }

                    // Convert to slider angle
                    float angle = clickedAngle - startAngle;
                    if (angle < 0) { angle += 360f; }

                    if (clockwise)
                    {
                        angle = 360f - angle;
                        if (angle >= 360f) { angle -= 360f; }
                    }

                    // Clamp to valid range
                    angle = Mathf.Clamp(angle, 0f, angleRange);

                    // Set the slider to this position immediately
                    float newValue = angle / angleRange;
                    NormalizedValue = newValue;
                }
            }
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!MayDrag(eventData))
                return;

            UpdateDrag(eventData, eventData.pressEventCamera);
        }

        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            // Update cached reference in case it changed
            cachedRectTransform = transform as RectTransform;
            UpdateVisuals();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            // Has value changed and not sent a callback?
            // If not, at least make sure the visuals are up-to-date
            var oldValue = value;
            value = ClampValue(value);
            if (oldValue != value) { onValueChanged?.Invoke(value); }
            UpdateVisuals();
        }

        void UpdateDrag(PointerEventData eventData, Camera cam)
        {
            if (cachedRectTransform != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedRectTransform,
                eventData.position, cam, out Vector2 localCursor))
            {
                // Calculate the current mouse angle
                float currentAngle = Mathf.Atan2(localCursor.y, localCursor.x) * Mathf.Rad2Deg;
                if (currentAngle < 0) { currentAngle += 360f; }

                // Convert to slider angle relative to start angle
                float sliderAngle = currentAngle - startAngle;
                if (sliderAngle < 0) { sliderAngle += 360f; }

                if (clockwise)
                {
                    sliderAngle = 360f - sliderAngle;
                    if (sliderAngle >= 360f) { sliderAngle -= 360f; }
                }

                // Get current slider position in degrees
                float currentSliderAngle = NormalizedValue * angleRange;

                if (allowContinuousDrag)
                {
                    // CONTINUOUS MODE: No limits, free rotation
                    float clampedSliderAngle = Mathf.Clamp(sliderAngle, 0f, angleRange);
                    NormalizedValue = clampedSliderAngle / angleRange;
                }
                else
                {
                    // Check if mouse is in valid range
                    bool mouseInValidRange = sliderAngle >= 0f && sliderAngle <= angleRange;

                    if (!mouseInValidRange)
                        return; // Mouse is outside valid range - don't update at all

                    // Check for wraparound jumps
                    float angleDifference = Mathf.Abs(sliderAngle - currentSliderAngle);
                    if (angleDifference > angleRange * 0.5f)
                        return; // This is a big jump - likely trying to wraparound, block it completely

                    // Update with position matching
                    if (sliderAngle > currentSliderAngle) { NormalizedValue = sliderAngle / angleRange; } // Mouse is ahead
                    else if (sliderAngle < currentSliderAngle) { NormalizedValue = sliderAngle / angleRange; } // Mouse is behind
                }
            }
        }

        void Set(float input, bool sendCallback = true)
        {
            // Clamp the input
            float newValue = Mathf.Clamp(input, minValue, maxValue);
            if (wholeNumbers) { newValue = Mathf.Round(newValue); }

            // If the stepped value doesn't match the last one, it's time to update
            if (value == newValue)
                return;

            value = newValue;
            UpdateVisuals();

            if (sendCallback)
                onValueChanged?.Invoke(newValue);
        }

        void UpdateVisuals()
        {
            UpdateHandle();
            UpdateFill();
        }

        void UpdateHandle()
        {
            if (handleRect == null || cachedRectTransform == null)
                return;

            // Calculate angle based on normalized value
            float targetAngle = NormalizedValue * angleRange;

            // Adjust for clockwise/counterclockwise
            // For clockwise: 0 value = startAngle, max value = startAngle - angleRange
            if (clockwise) { targetAngle = -targetAngle; }

            // Add start angle offset
            targetAngle += startAngle;

            // Convert to radians for position calculation
            float radians = targetAngle * Mathf.Deg2Rad;

            // Calculate base radius from RectTransform size (use the smaller dimension divided by 2)
            Vector2 size = cachedRectTransform.rect.size;
            float baseRadius = Mathf.Min(size.x, size.y) * 0.5f;

            // Add the offset to the base radius
            float finalRadius = baseRadius + handleOffset;

            // Calculate position on circle
            Vector2 handleRectPosition = new(Mathf.Cos(radians) * finalRadius, Mathf.Sin(radians) * finalRadius);
            handleRect.anchoredPosition = handleRectPosition;
            handleRect.localEulerAngles = new Vector3(0, 0, targetAngle);
        }

        void UpdateFill()
        {
            if (fillImage == null)
                return;

            // Set fill amount based on normalized value and angle range
            fillImage.fillAmount = NormalizedValue * (angleRange / 360f);

            // Set fill origin rotation and direction based on clockwise setting
            if (clockwise)
            {
                // For clockwise: fill should go clockwise from start angle
                fillImage.fillClockwise = true;
                fillImage.transform.localEulerAngles = new Vector3(0, 0, startAngle);
            }
            else
            {
                // For counterclockwise: fill should go counterclockwise from start angle
                fillImage.fillClockwise = false;
                fillImage.transform.localEulerAngles = new Vector3(0, 0, startAngle);
            }
        }

        void SetText(float val)
        {
            if (valueInput == null && valueText == null)
                return;

            string formattedText = FormatValue(val);

            if (valueText != null) { valueText.text = formattedText; }
            if (valueInput != null) { valueInput.text = formattedText; }
        }

        bool MayDrag(PointerEventData eventData) => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        float ClampValue(float input)
        {
            float newValue = Mathf.Clamp(input, minValue, maxValue);
            if (wholeNumbers) { newValue = Mathf.Round(newValue); }
            return newValue;
        }

        string FormatValue(float sValue)
        {
            // Apply display multiplier
            float displayValue = sValue * displayMultiplier;

            // Get the format string
            string formatString = GetFormatString();

            // Format the number
            string formattedNumber;

            // If invalid format string, use default
            try { formattedNumber = displayValue.ToString(formatString); }
            catch { formattedNumber = displayValue.ToString("F2"); }

            // If textFormat is empty, use just the number
            if (string.IsNullOrEmpty(textFormat)) { return formattedNumber; }

            // Try to use string.Format, if it fails just append the number
            try { return string.Format(textFormat, formattedNumber); }
            catch (System.FormatException) { return textFormat + " " + formattedNumber; }
        }

        string GetFormatString()
        {
            return displayFormat switch
            {
                // Fixed-point
                Slider.DisplayFormat.Fixed0 => "F0",
                Slider.DisplayFormat.Fixed1 => "F1",
                Slider.DisplayFormat.Fixed2 => "F2",
                Slider.DisplayFormat.Fixed3 => "F3",
                Slider.DisplayFormat.Fixed4 => "F4",
                Slider.DisplayFormat.Fixed5 => "F5",
                // Number with thousands
                Slider.DisplayFormat.Number0 => "N0",
                Slider.DisplayFormat.Number1 => "N1",
                Slider.DisplayFormat.Number2 => "N2",
                Slider.DisplayFormat.Number3 => "N3",
                // Default
                _ => "F0",
            };
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;

        protected override void OnValidate()
        {
            base.OnValidate();

            // Clamp values to valid ranges
            minValue = Mathf.Min(minValue, maxValue);
            maxValue = Mathf.Max(minValue, maxValue);
            value = Mathf.Clamp(value, minValue, maxValue);

            angleRange = Mathf.Clamp(angleRange, 10f, 360f);
            startAngle = Mathf.Clamp(startAngle, 0f, 360f);
            handleOffset = Mathf.Clamp(handleOffset, -100f, 100f);
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);

            // Update display in editor when values change
            if (valueText != null || valueInput != null) { SetText(Value); }

            // Update visuals
            UpdateVisuals();
        }
#endif
    }
}