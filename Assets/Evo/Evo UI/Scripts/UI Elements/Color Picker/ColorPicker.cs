using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/color-picker")]
    [AddComponentMenu("Evo/UI/UI Elements/Color Picker")]
    public class ColorPicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool resetOnRightClick = true;
        public Color startColor = Color.white;
        [SerializeField] private ColorPickerType pickerType = ColorPickerType.Radial;
        [SerializeField] private TextureSize wheelSize = TextureSize.Size512;

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private Image colorWheel;
        [SerializeField] private Image colorPreview;
        [SerializeField] private RectTransform colorSelector;
        public TMP_InputField hexInput;
        public Slider hueSlider;
        [SerializeField] private ImageGradient hueGradient;
        public Slider saturationSlider;
        [SerializeField] private ImageGradient saturationGradient;
        public Slider brightnessSlider;
        [SerializeField] private ImageGradient brightnessGradient;
        public Slider opacitySlider;
        [SerializeField] private ImageGradient opacityGradient;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<Color> onColorChanged = new();

        // Helpers
        Color currentColor = Color.white;
        float currentHue = 0;
        float currentSaturation = 1;
        float currentBrightness = 1;
        float currentAlpha = 1;
        bool isUpdatingUI = false;
        bool isDraggingWheel = false;

        // Cache
        RectTransform wheelRect;
        Camera eventCamera;

        // Constants
        const float BrightnessThreshold = 0.01f;
        static readonly Vector2 HalfVector = Vector2.one * 0.5f;

        // Enums
        public enum ColorPickerType { None, Radial, Square }
        public enum TextureSize { Size64 = 64, Size128 = 128, Size256 = 256, Size512 = 512, Size1024 = 1024 }

        void Awake()
        {
            if (colorWheel != null)
                wheelRect = colorWheel.rectTransform;

#if UNITY_EDITOR
            // Generate wheel in editor if requested
            if (!Application.isPlaying && pickerType != ColorPickerType.None)
            {
                // Ensure wheelRect is cached for editor
                if (colorWheel != null && wheelRect == null)
                    wheelRect = colorWheel.rectTransform;

                lastPickerType = pickerType;
                lastStartColor = startColor;
                lastWheelSize = wheelSize;
                GenerateColorWheel();
                UpdatePositionFromColor();
            }
#endif
        }

        void Start()
        {
            if (pickerType != ColorPickerType.None)
                GenerateColorWheel();

            SetupSliders();
            SetupHexInput();
            SetColor(startColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (colorWheel == null || pickerType == ColorPickerType.None)
                return;

            if (resetOnRightClick && eventData.button == PointerEventData.InputButton.Right) 
            { 
                SetColor(startColor);
                return; 
            }

            // Check if the pointer is on the color wheel
            if (eventData.pointerEnter == colorWheel.gameObject 
                || colorWheel.gameObject.transform.IsChildOf(eventData.pointerEnter.transform))
            {
                isDraggingWheel = true;
                eventCamera = eventData.pressEventCamera;
                HandleColorWheelInput(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDraggingWheel && colorWheel != null)
            {
                HandleColorWheelInput(eventData.position);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isDraggingWheel = false;
            eventCamera = null;
        }

        void GenerateColorWheel()
        {
            if (colorWheel != null)
            {
                SetupColorWheel();
            }
        }

        void SetupColorWheel()
        {
            if (pickerType == ColorPickerType.Radial) { CreateRadialColorWheel(); }
            else { CreateSquareColorPicker(); }
        }

        void CreateRadialColorWheel()
        {
            int size = (int)wheelSize;
            Texture2D wheelTexture = new(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            Vector2 center = new(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            float radiusSquared = radius * radius;

            // Pre-calculate values
            float invRadius = 1f / radius;
            float radiusMinusOne = radius - 1f;

            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                float dy = y - center.y;
                float dySquared = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float distanceSquared = dx * dx + dySquared;

                    // Early out for pixels outside circle
                    if (distanceSquared > radiusSquared)
                    {
                        pixels[yOffset + x] = Color.clear;
                        continue;
                    }

                    float distance = Mathf.Sqrt(distanceSquared);

                    // Anti-aliasing
                    float alpha = 1f;
                    if (distance > radiusMinusOne) { alpha = radius - distance; }

                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) { angle += 360; }

                    float hue = angle / 360f;
                    float saturation = distance * invRadius;

                    Color32 color = Color.HSVToRGB(hue, saturation, 1f);
                    color.a = (byte)(alpha * 255);
                    pixels[yOffset + x] = color;
                }
            }

            wheelTexture.SetPixels32(pixels);
            wheelTexture.Apply();
            wheelTexture.filterMode = FilterMode.Bilinear;

            // Destroy old sprite to prevent memory leak
            if (colorWheel.sprite != null && !Application.isPlaying) { DestroyImmediate(colorWheel.sprite); }

            // Create color wheel sprite
            colorWheel.sprite = Sprite.Create(wheelTexture, new Rect(0, 0, size, size), HalfVector);
        }

        void CreateSquareColorPicker()
        {
            int size = (int)wheelSize;
            Texture2D squareTexture = new(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            float invSize = 1f / (size - 1);

            for (int y = 0; y < size; y++)
            {
                float saturation = y * invSize;
                int yOffset = y * size;

                for (int x = 0; x < size; x++)
                {
                    float hue = x * invSize;
                    pixels[yOffset + x] = Color.HSVToRGB(hue, saturation, 1f);
                }
            }

            squareTexture.SetPixels32(pixels);
            squareTexture.Apply();
            squareTexture.filterMode = FilterMode.Bilinear;

            // Destroy old sprite to prevent memory leak
            if (colorWheel.sprite != null && !Application.isPlaying) { DestroyImmediate(colorWheel.sprite); }

            // Create color wheel sprite
            colorWheel.sprite = Sprite.Create(squareTexture, new Rect(0, 0, size, size), HalfVector);
        }

        void SetupSliders()
        {
            // Setup hue slider
            if (hueSlider != null)
            {
                hueSlider.minValue = 0f;
                hueSlider.maxValue = 1f;
                hueSlider.onValueChanged.AddListener(OnHueChanged);
            }

            // Setup saturation slider
            if (saturationSlider != null)
            {
                saturationSlider.minValue = 0f;
                saturationSlider.maxValue = 1f;
                saturationSlider.onValueChanged.AddListener(OnSaturationChanged);
            }

            // Setup brightness slider
            if (brightnessSlider != null)
            {
                brightnessSlider.minValue = 0f;
                brightnessSlider.maxValue = 1f;
                brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            }

            // Setup opacity slider
            if (opacitySlider != null)
            {
                opacitySlider.minValue = 0f;
                opacitySlider.maxValue = 1f;
                opacitySlider.onValueChanged.AddListener(OnOpacityChanged);
            }

            // Setup static hue gradient once
            if (hueGradient != null)
            {
                hueGradient.SetGradient(new Gradient
                {
                    colorKeys = new GradientColorKey[]
                    {
                        new(Color.red, 0f),
                        new(Color.yellow, 0.167f),
                        new(Color.green, 0.333f),
                        new(Color.cyan, 0.5f),
                        new(Color.blue, 0.667f),
                        new(Color.magenta, 0.833f),
                        new(Color.red, 1f)
                    },
                    alphaKeys = new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
                });
            }

            // Initialize dynamic gradients
            UpdateAllGradients();
        }

        void SetupHexInput()
        {
            if (hexInput == null)
                return;

            hexInput.characterLimit = 9; // # + 8 hex chars (RRGGBBAA)
            hexInput.onEndEdit.AddListener(OnHexInputChanged);
        }

        void HandleColorWheelInput(Vector2 screenPosition)
        {
            if (wheelRect == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(wheelRect, screenPosition, eventCamera, out Vector2 localPoint))
                ProcessWheelSelection(localPoint, wheelRect);
        }

        void ProcessWheelSelection(Vector2 localPoint, RectTransform wheelRect)
        {
            // Convert to normalized coordinates
            Vector2 normalizedPoint = localPoint / wheelRect.rect.size + HalfVector;

            if (pickerType == ColorPickerType.Radial) { ProcessRadialSelection(normalizedPoint); }
            else { ProcessSquareSelection(normalizedPoint); }
        }

        void ProcessRadialSelection(Vector2 normalizedPoint)
        {
            // Convert to polar coordinates
            Vector2 dir = normalizedPoint - HalfVector;
            float distance = dir.magnitude;

            // Clamp distance to wheel radius
            distance = Mathf.Clamp(distance, 0f, 0.5f);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0) { angle += 360; }

            currentHue = angle / 360f;
            currentSaturation = distance * 2f; // Scale to 0-1

            UpdateColorFromHSV();

            // Update selector position (clamp to wheel boundary)
            Vector2 clampedDir = dir.normalized * Mathf.Min(distance, 0.5f);
            UpdateSelectorPosition(HalfVector + clampedDir);
        }

        void ProcessSquareSelection(Vector2 normalizedPoint)
        {
            // Clamp to square bounds
            normalizedPoint.x = Mathf.Clamp01(normalizedPoint.x);
            normalizedPoint.y = Mathf.Clamp01(normalizedPoint.y);

            currentHue = normalizedPoint.x;
            currentSaturation = normalizedPoint.y;

            UpdateColorFromHSV();
            UpdateSelectorPosition(normalizedPoint);
        }

        void UpdateSelectorPosition(Vector2 normalizedPos)
        {
            if (colorSelector != null && wheelRect != null)
            {
                Vector2 localPos = (normalizedPos - HalfVector) * wheelRect.rect.size;
                if (colorSelector.anchoredPosition != localPos) { colorSelector.anchoredPosition = localPos; }
            }
        }

        void UpdateColorFromHSV()
        {
            Color newColor = Color.HSVToRGB(currentHue, currentSaturation, currentBrightness);
            newColor.a = currentAlpha;
            SetColorInternal(newColor, updateHSV: false);
        }

        void OnHueChanged(float value)
        {
            if (isUpdatingUI)
                return;

            currentHue = value;
            UpdateColorFromHSV();
        }

        void OnSaturationChanged(float value)
        {
            if (isUpdatingUI)
                return;

            currentSaturation = value;
            UpdateColorFromHSV();

            // Update selector position when using sliders with None picker type
            if (pickerType == ColorPickerType.None)
                UpdateSelectorPositionFromHSV();
        }

        void OnBrightnessChanged(float value)
        {
            if (isUpdatingUI)
                return;

            currentBrightness = value;
            UpdateColorFromHSV();
        }

        void OnOpacityChanged(float value)
        {
            if (isUpdatingUI)
                return;

            currentAlpha = value;
            UpdateColorFromHSV();
        }

        void OnHexInputChanged(string hexValue)
        {
            if (isUpdatingUI)
                return;

            // Remove # if present
            if (!string.IsNullOrEmpty(hexValue) && hexValue[0] == '#')
                hexValue = hexValue[1..];

            // Try to parse the color, revert if invalid
            if (ColorUtility.TryParseHtmlString("#" + hexValue, out Color newColor)) { SetColor(newColor); }
            else if (hexInput != null)
            {
                string targetHex = "#" + ColorUtility.ToHtmlStringRGBA(currentColor);
                if (hexInput.text != targetHex) { hexInput.text = targetHex; }
            }
        }

        void UpdateUI()
        {
            isUpdatingUI = true;

            // Bypass boundary crossings if values are identical
            if (colorPreview != null && colorPreview.color != currentColor) { colorPreview.color = currentColor; }
            if (hueSlider != null && !Mathf.Approximately(hueSlider.value, currentHue)) { hueSlider.value = currentHue; }
            if (saturationSlider != null && !Mathf.Approximately(saturationSlider.value, currentSaturation)) { saturationSlider.value = currentSaturation; }
            if (brightnessSlider != null && !Mathf.Approximately(brightnessSlider.value, currentBrightness)) { brightnessSlider.value = currentBrightness; }
            if (opacitySlider != null && !Mathf.Approximately(opacitySlider.value, currentAlpha)) { opacitySlider.value = currentAlpha; }
            if (hexInput != null)
            {
                string targetHex = "#" + ColorUtility.ToHtmlStringRGBA(currentColor);
                if (hexInput.text != targetHex) { hexInput.text = targetHex; }
            }

            UpdateAllGradients();
            UpdateSelectorPositionFromHSV();

            isUpdatingUI = false;
        }

        void UpdateAllGradients()
        {
            // Update saturation gradient (grayscale to current hue color at full saturation)
            if (saturationGradient != null)
            {
                // For white/grayscale colors, show gradient from gray to white
                if (currentSaturation < 0.01f && currentBrightness > 0.99f) { saturationGradient.SetGradientColors(Color.gray, Color.white); }
                else
                {
                    Color grayscale = Color.HSVToRGB(0f, 0f, currentBrightness);
                    Color fullySaturated = Color.HSVToRGB(currentHue, 1f, currentBrightness);
                    saturationGradient.SetGradientColors(grayscale, fullySaturated);
                }
            }

            // Update brightness gradient (black to current color without brightness)
            if (brightnessGradient != null)
            {
                Color colorAtFullBrightness = Color.HSVToRGB(currentHue, currentSaturation, 1f);
                brightnessGradient.SetGradientColors(Color.black, colorAtFullBrightness);
            }

            // Update opacity gradient (transparent to opaque current color)
            if (opacityGradient != null)
            {
                Color transparentColor = currentColor;
                transparentColor.a = 0f;
                Color opaqueColor = currentColor;
                opaqueColor.a = 1f;
                opacityGradient.SetGradientColors(transparentColor, opaqueColor);
            }
        }

        void UpdateSelectorPositionFromHSV()
        {
            if (colorSelector == null || pickerType == ColorPickerType.None)
                return;

            if (wheelRect == null && colorWheel != null)
                wheelRect = colorWheel.rectTransform;

            if (wheelRect == null)
                return;

            Vector2 position;

            if (pickerType == ColorPickerType.Radial)
            {
                float angle = currentHue * 360f * Mathf.Deg2Rad;
                float distance = currentSaturation * 0.5f;
                position = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance) * wheelRect.rect.size;
            }
            else
            {
                Vector2 normalizedPos = new(currentHue, currentSaturation);
                position = (normalizedPos - HalfVector) * wheelRect.rect.size;
            }

            if (colorSelector.anchoredPosition != position)
                colorSelector.anchoredPosition = position;
        }

        public void SetColor(Color color) => SetColorInternal(color, updateHSV: true);

        void SetColorInternal(Color color, bool updateHSV)
        {
            currentColor = color;
            currentAlpha = color.a;

            if (updateHSV)
            {
                // Convert to HSV
                Color.RGBToHSV(color, out float h, out float s, out float v);

                // Only update hue and saturation if brightness > threshold
                if (v > BrightnessThreshold)
                {
                    currentHue = h;
                    currentSaturation = s;
                }
                currentBrightness = v;
            }

            UpdateUI();
            onColorChanged?.Invoke(currentColor);
        }

        public Color GetCurrentColor(bool withoutBrightness = false)
        {
            if (!withoutBrightness) { return currentColor; }
            else
            {
                // Return color at full brightness
                Color colorWithoutBrightness = Color.HSVToRGB(currentHue, currentSaturation, 1f);
                colorWithoutBrightness.a = currentAlpha;
                return colorWithoutBrightness;
            }
        }

        public void SetRGB(float r, float g, float b)
        {
            Color newColor = new(r, g, b, currentAlpha);
            SetColor(newColor);
        }

        public void SetRed(float value)
        {
            Color newColor = currentColor;
            newColor.r = value;
            SetColor(newColor);
        }

        public void SetGreen(float value)
        {
            Color newColor = currentColor;
            newColor.g = value;
            SetColor(newColor);
        }

        public void SetBlue(float value)
        {
            Color newColor = currentColor;
            newColor.b = value;
            SetColor(newColor);
        }

        public void SetAlpha(float value)
        {
            currentAlpha = value;
            UpdateColorFromHSV();
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        bool needsUpdate = false;
        bool needsWheelRegeneration = false;
        Color lastStartColor;
        ColorPickerType lastPickerType;
        TextureSize lastWheelSize;

        UnityEditor.EditorApplication.CallbackFunction updateEditorUICallback;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Only regenerate wheel in editor when picker type actually changes
            bool typeChanged = lastPickerType != pickerType;
            bool colorChanged = lastStartColor != startColor;
            bool sizeChanged = lastWheelSize != wheelSize;

            if (typeChanged)
            {
                lastPickerType = pickerType;
                needsWheelRegeneration = true;
            }

            if (sizeChanged)
            {
                lastWheelSize = wheelSize;
                needsWheelRegeneration = true;
            }

            if (colorChanged)
                lastStartColor = startColor;

            // Only update UI if something actually changed
            if (typeChanged || colorChanged || sizeChanged)
            {
                needsUpdate = true;
                UnityEditor.EditorApplication.delayCall -= DelayedUpdate;
                UnityEditor.EditorApplication.delayCall += DelayedUpdate;
            }
        }

        void DelayedUpdate()
        {
            if (this == null)
                return;

            if (needsWheelRegeneration && pickerType != ColorPickerType.None)
            {
                GenerateColorWheel();
                needsWheelRegeneration = false;
            }

            if (needsUpdate)
            {
                UpdatePositionFromColor();
                needsUpdate = false;
            }
        }

        void UpdatePositionFromColor()
        {
            if (Application.isPlaying)
                return;

            currentColor = startColor;
            currentAlpha = startColor.a;
            Color.RGBToHSV(startColor, out float h, out float s, out float v);

            if (v > BrightnessThreshold)
            {
                currentHue = h;
                currentSaturation = s;
            }
            currentBrightness = v;

            UpdateUIInEditor();
        }

        void UpdateUIInEditor()
        {
            if (Application.isPlaying)
                return;

            // Set color preview
            if (colorPreview != null && colorPreview.color != currentColor) { colorPreview.color = currentColor; }

            // Update gradients
            UpdateAllGradients();

            // Update selector position immediately
            UpdateSelectorPositionFromHSV();

            // Use delayCall for slider value updates to avoid warnings and prevent accumulation
            updateEditorUICallback ??= UpdateEditorUISliders;
            UnityEditor.EditorApplication.delayCall -= updateEditorUICallback;
            UnityEditor.EditorApplication.delayCall += updateEditorUICallback;
        }

        void UpdateEditorUISliders()
        {
            if (this != null)
            {
                if (hueSlider != null) { hueSlider.SetValueWithoutNotify(currentHue); }
                if (saturationSlider != null) { saturationSlider.SetValueWithoutNotify(currentSaturation); }
                if (brightnessSlider != null) { brightnessSlider.SetValueWithoutNotify(currentBrightness); }
                if (opacitySlider != null) { opacitySlider.SetValueWithoutNotify(currentAlpha); }
                if (hexInput != null)
                {
                    string targetHex = "#" + ColorUtility.ToHtmlStringRGBA(currentColor);
                    hexInput.SetTextWithoutNotify(targetHex);
                }
            }
        }
#endif
    }
}