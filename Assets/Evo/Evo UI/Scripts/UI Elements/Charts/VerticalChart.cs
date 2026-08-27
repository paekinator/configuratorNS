using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "ui-elements/vertical-chart")]
    [AddComponentMenu("Evo/UI/UI Elements/Vertical Chart")]
    public class VerticalChart : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Chart Data", Constants.CustomEditorID)]
        public List<DataPoint> dataPoints = new()
        {
            new DataPoint("Q1", 45f),
            new DataPoint("Q2", 75f),
            new DataPoint("Q3", 30f),
            new DataPoint("Q4", 90f)
        };

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private RectOffset padding = new();
        [SerializeField] private float gridToBarsOffset = 5;
        [SerializeField] private float barSpacing = 10;
        [SerializeField] private float barCornerRadius = 4;
        [SerializeField] private Sprite barSprite;

        [EvoHeader("Grid Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showGrid = true;
        [SerializeField, Range(1, 50)] private int horizontalGridLines = 5;
        [SerializeField, Range(0.1f, 20)] private float gridLineThickness = 1;

        [EvoHeader("Axis Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showAxis = true;
        [SerializeField, Range(0.1f, 20)] private float axisThickness = 2;

        [EvoHeader("Value Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showValues = true;
        [SerializeField] private float valueHeight = 20;
        [SerializeField] private float valueFontSize = 12;

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource stylingSource = StylingSource.Custom;
        public StylerPreset stylerPreset;
        public ColorMapping gridColor = new() { stylerID = "Primary" };
        public ColorMapping axisColor = new() { stylerID = "Primary" };
        public ColorMapping barColor = new() { stylerID = "Accent" };
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        public FontMapping valueFont = new() { stylerID = "Regular" };
        public FontMapping labelFont = new() { stylerID = "Regular" };
        [SerializeField] private float labelHeight = 30;
        [SerializeField] private float labelFontSize = 12;
        public static string[] GetColorFields() => new[] { "gridColor", "axisColor", "barColor", "labelColor" };
        public static string[] GetFontFields() => new[] { "valueFont", "labelFont" };

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Constants
        const string GEN_SUFFIX = "[Generated]";

        // Helpers
        bool pendingRedraw;
        RectTransform rectTransform;
        Texture2D whiteTexture;
        Sprite defaultBarSprite;

        [System.Serializable]
        public class DataPoint
        {
            public string label;
            public float value;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            public DataPoint(string label = "", float value = 0)
            {
                this.label = label;
                this.value = value;
            }
        }

        // Styler Interface
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value) { return; }
                stylerPreset = value;
                UpdateStyler();
            }
        }
        public void UpdateStyler() => DrawChartSafe();

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            padding ??= new RectOffset();

            CreateWhiteTexture();
            CreateDefaultBarSprite();
        }

#if EVO_LOCALIZATION
        void Start()
        {
            if (Application.isPlaying && enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
        }
#endif

        void OnEnable()
        {
            DrawChart();
        }

        void OnDestroy()
        {
            if (whiteTexture != null) { DestroyImmediate(whiteTexture); }
            if (defaultBarSprite != null && defaultBarSprite.texture != null)
            {
                DestroyImmediate(defaultBarSprite.texture);
                DestroyImmediate(defaultBarSprite);
            }
#if EVO_LOCALIZATION
            if (Application.isPlaying && enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
#endif
        }

        void OnRectTransformDimensionsChange()
        {
            DrawChartSafe();
        }

        void DrawChartSafe()
        {
            if (gameObject.activeInHierarchy && rectTransform != null && !pendingRedraw)
            {
                pendingRedraw = true;

                if (Application.isPlaying) { StartCoroutine(DeferredDrawChart()); }
#if UNITY_EDITOR
                else
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            pendingRedraw = false;
                            DrawChart();
                        }
                    };
                }
#endif
            }
        }

        void CreateWhiteTexture()
        {
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }

        void CreateDefaultBarSprite()
        {
            if (barSprite == null)
            {
                // Create a rounded rectangle sprite
                int width = 32;
                int height = 32;
                Texture2D barTexture = new(width, height);
                Color32[] pixels = new Color32[width * height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = Color.white;
                    }
                }

                barTexture.SetPixels32(pixels);
                barTexture.Apply();

                defaultBarSprite = Sprite.Create(
                    barTexture,
                    new Rect(0, 0, width, height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.Tight,
                    new Vector4(barCornerRadius, barCornerRadius, barCornerRadius, barCornerRadius)
                );
            }
        }

        public void UpdateData(List<DataPoint> newData)
        {
            dataPoints = newData;
            DrawChart();
        }

        public void DrawChart()
        {
            if (rectTransform == null) { return; }
            if (!gameObject.activeInHierarchy)
            {
                pendingRedraw = true;
                return;
            }

            ClearChart();

            if (dataPoints == null || dataPoints.Count == 0)
                return;

            float chartWidth = rectTransform.rect.width;
            float chartHeight = rectTransform.rect.height;

            // Calculate chart area
            float chartLeft = padding.left;
            float chartBottom = padding.bottom + labelHeight;
            float chartAreaWidth = chartWidth - padding.left - padding.right;
            float chartAreaHeight = chartHeight - padding.top - padding.bottom - labelHeight - valueHeight;

            if (chartAreaWidth <= 0 || chartAreaHeight <= 0)
                return;

            // Create containers with full anchors
            GameObject gridContainer = new($"Grid {GEN_SUFFIX}") { hideFlags = HideFlags.DontSave };
            gridContainer.transform.SetParent(transform, false);
            RectTransform gridRT = gridContainer.AddComponent<RectTransform>();
            gridRT.anchorMin = Vector2.zero;
            gridRT.anchorMax = Vector2.one;
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;

            GameObject dataContainer = new($"Data {GEN_SUFFIX}") { hideFlags = HideFlags.DontSave };
            dataContainer.transform.SetParent(transform, false);
            RectTransform dataRT = dataContainer.AddComponent<RectTransform>();
            dataRT.anchorMin = Vector2.zero;
            dataRT.anchorMax = Vector2.one;
            dataRT.offsetMin = Vector2.zero;
            dataRT.offsetMax = Vector2.zero;

            GameObject labelContainer = new($"Labels {GEN_SUFFIX}") { hideFlags = HideFlags.DontSave };
            labelContainer.transform.SetParent(transform, false);
            RectTransform labelRT = labelContainer.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            // Draw components
            if (showGrid) DrawGrid(gridContainer, chartLeft, chartBottom, chartAreaWidth, chartAreaHeight);
            if (showAxis) DrawAxes(gridContainer, chartLeft, chartBottom, chartAreaWidth, chartAreaHeight);
            DrawBars(dataContainer, chartLeft, chartBottom, chartAreaWidth, chartAreaHeight);
            DrawLabels(labelContainer, chartLeft, chartBottom, chartAreaWidth, chartAreaHeight);
        }

        public void ClearChart()
        {
            // Clear all generated containers and their children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.Contains(GEN_SUFFIX))
                {
                    if (Application.isPlaying) { Destroy(child.gameObject); }
                    else { DestroyImmediate(child.gameObject); }
                }
            }
        }

        void DrawGrid(GameObject container, float left, float bottom, float width, float height)
        {
            Color styledGridColor = Styler.GetColor(stylingSource, gridColor, stylerPreset);

            // Horizontal grid lines
            for (int i = 1; i <= horizontalGridLines; i++)
            {
                float y = bottom + (i * height / horizontalGridLines);
                CreateLine(
                    container,
                    new Vector2(left, y),
                    new Vector2(left + width, y),
                    styledGridColor,
                    gridLineThickness,
                    "H-Grid " + i
                );
            }

            // Vertical line at the right edge
            CreateLine(
                container,
                new Vector2(left + width, bottom),
                new Vector2(left + width, bottom + height),
                styledGridColor,
                gridLineThickness,
                "V-Grid Right"
            );
        }

        void DrawAxes(GameObject container, float left, float bottom, float width, float height)
        {
            Color styledAxisColor = Styler.GetColor(stylingSource, axisColor, stylerPreset);

            // X-axis (bottom) - extend slightly to meet Y-axis perfectly
            CreateLine(
                container,
                new Vector2(left - axisThickness / 2, bottom),
                new Vector2(left + width, bottom),
                styledAxisColor,
                axisThickness,
                "X-Axis"
            );

            // Y-axis (left) - extend slightly to meet X-axis perfectly
            CreateLine(
                container,
                new Vector2(left, bottom - axisThickness / 2),
                new Vector2(left, bottom + height),
                styledAxisColor,
                axisThickness,
                "Y-Axis"
            );
        }

        void DrawBars(GameObject container, float left, float bottom, float width, float height)
        {
            if (dataPoints.Count == 0)
                return;

            // Find max value for scaling
            float maxValue = float.MinValue;
            foreach (var point in dataPoints) { if (point.value > maxValue) maxValue = point.value; }
            if (maxValue <= 0) maxValue = 1;

            // Apply offset
            float barStartX = left + gridToBarsOffset;
            float barStartY = bottom + gridToBarsOffset;
            float adjustedWidth = width - gridToBarsOffset * 2;
            float adjustedHeight = height - gridToBarsOffset;

            // Calculate bar width
            float totalBarWidth = adjustedWidth - (barSpacing * (dataPoints.Count - 1));
            float barWidth = totalBarWidth / dataPoints.Count;

            // Get styled colors and fonts
            Color styledBarColor = Styler.GetColor(stylingSource, barColor, stylerPreset);
            Color styledValueColor = Styler.GetColor(stylingSource, labelColor, stylerPreset);
            TMP_FontAsset styledValueFont = Styler.GetFont(stylingSource, valueFont, stylerPreset);

            // Draw bars
            for (int i = 0; i < dataPoints.Count; i++)
            {
                float barHeight = (dataPoints[i].value / maxValue) * adjustedHeight;
                float barX = barStartX + (i * (barWidth + barSpacing));

                CreateBar(
                    container,
                    new Vector2(barX, barStartY),
                    new Vector2(barWidth, barHeight),
                    styledBarColor,
                    "Bar " + i
                );

                // Draw value text
                if (showValues)
                {
                    CreateTMPLabel(
                        container,
                        dataPoints[i].value.ToString("F1"),
                        new Vector2(barX + barWidth / 2, barStartY + barHeight + 10),
                        TextAlignmentOptions.Bottom,
                        "Value " + i,
                        new Vector2(barWidth, valueHeight),
                        styledValueFont,
                        valueFontSize,
                        styledValueColor
                    );
                }
            }
        }

        void DrawLabels(GameObject container, float left, float bottom, float width, float height)
        {
            if (dataPoints.Count == 0)
                return;

            // Get styled font and color
            TMP_FontAsset styledLabelFont = Styler.GetFont(stylingSource, labelFont, stylerPreset);
            Color styledLabelColor = Styler.GetColor(stylingSource, labelColor, stylerPreset);

            // Apply offset for bar positions
            float barStartX = left + gridToBarsOffset;
            float adjustedWidth = width - gridToBarsOffset * 2;

            float totalBarWidth = adjustedWidth - (barSpacing * (dataPoints.Count - 1));
            float barWidth = totalBarWidth / dataPoints.Count;

            // X-axis labels (bar names)
            for (int i = 0; i < dataPoints.Count; i++)
            {
                float barX = barStartX + (i * (barWidth + barSpacing)) + barWidth / 2;

                CreateTMPLabel(
                    container,
                    dataPoints[i].label,
                    new Vector2(barX, bottom - labelHeight / 2),
                    TextAlignmentOptions.Top,
                    "Label " + i,
                    new Vector2(barWidth, labelHeight),
                    styledLabelFont,
                    labelFontSize,
                    styledLabelColor
                );
            }

            // Y-axis labels (scale values)
            float maxValue = float.MinValue;
            foreach (var point in dataPoints) { if (point.value > maxValue) maxValue = point.value; }
            for (int i = 0; i <= horizontalGridLines; i++)
            {
                float value = (i * maxValue) / horizontalGridLines;
                float y = bottom + (i * height / horizontalGridLines);

                CreateTMPLabel(
                    container,
                    value.ToString("F0"),
                    new Vector2(left - 10, y),
                    TextAlignmentOptions.MidlineRight,
                    "Scale Label " + i,
                    new Vector2(40, 20),
                    styledLabelFont,
                    labelFontSize * 0.8f,
                    styledLabelColor
                );
            }
        }

        void CreateBar(GameObject container, Vector2 position, Vector2 size, Color color, string name)
        {
            GameObject barObj = new(name, typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
            barObj.transform.SetParent(container.transform, false);

            Image img = barObj.GetComponent<Image>();
            img.sprite = barSprite != null ? barSprite : defaultBarSprite;
            img.color = color;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = barCornerRadius;

            RectTransform rt = barObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        void CreateLine(GameObject container, Vector2 start, Vector2 end, Color color, float thickness, string name)
        {
            GameObject lineObj = new(name, typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.DontSave };
            lineObj.transform.SetParent(container.transform, false);

            RawImage img = lineObj.GetComponent<RawImage>();
            img.texture = whiteTexture;
            img.color = color;

            RectTransform rt = lineObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0, 0.5f);

            Vector2 diff = end - start;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            float distance = diff.magnitude;

            rt.anchoredPosition = start;
            rt.sizeDelta = new Vector2(distance, thickness);
            rt.rotation = Quaternion.Euler(0, 0, angle);
        }

        void CreateTMPLabel(GameObject container, string text, Vector2 position, TextAlignmentOptions alignment, string name, Vector2 size, TMP_FontAsset font = null, float fontSize = 0, Color? color = null)
        {
            GameObject labelObj = new(name, typeof(RectTransform), typeof(TextMeshProUGUI)) { hideFlags = HideFlags.DontSave };
            labelObj.transform.SetParent(container.transform, false);

            TextMeshProUGUI textComp = labelObj.GetComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = font;
            textComp.fontSize = fontSize > 0 ? fontSize : labelFontSize;
            textComp.color = color ?? Color.white;
            textComp.alignment = alignment;

            RectTransform rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            // Adjust pivot based on alignment
            if (alignment == TextAlignmentOptions.MidlineRight) { rt.pivot = new Vector2(1f, 0.5f); }
            else if (alignment == TextAlignmentOptions.Bottom) { rt.pivot = new Vector2(0.5f, 0f); }
            else if (alignment == TextAlignmentOptions.Top) { rt.pivot = new Vector2(0.5f, 1f); }
            else { rt.pivot = new Vector2(0.5f, 0.5f); }

            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        IEnumerator DeferredDrawChart()
        {
            // Wait until end of frame to ensure UI rebuild is complete
            yield return new WaitForEndOfFrame();
            pendingRedraw = false;
            DrawChart();
        }

        public void AddDataPoint(string label, float value)
        {
            dataPoints.Add(new DataPoint(label, value));
            DrawChart();
        }

        public void RemoveDataPoint(int index)
        {
            if (index >= 0 && index < dataPoints.Count)
            {
                dataPoints.RemoveAt(index);
                DrawChart();
            }
        }

        public void ClearData()
        {
            dataPoints.Clear();
            DrawChart();
        }

        public void SetDataPoint(int index, string label, float value)
        {
            if (index >= 0 && index < dataPoints.Count)
            {
                dataPoints[index].label = label;
                dataPoints[index].value = value;
                DrawChart();
            }
        }

        public void SetDataPoint(DataPoint item, string label, float value)
        {
            item.label = label;
            item.value = value;
            DrawChart();
        }

        public bool ShowValues
        {
            get { return showValues; }
            set
            {
                showValues = value;
                DrawChart();
            }
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (DataPoint item in dataPoints)
            {
                if (!string.IsNullOrEmpty(item.tableKey))
                {
                    SetDataPoint(item, localizedObject.GetString(item.tableKey), item.value);
                }
            }

            DrawChart();
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool dataFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool stylingFoldout = false;

        void OnValidate()
        {
            if (gameObject.activeInHierarchy)
            {
                pendingRedraw = true;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        pendingRedraw = false;
                        DrawChart();
                    }
                };
            }
        }
#endif
    }
}