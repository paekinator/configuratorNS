using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "ui-elements/line-chart")]
    [AddComponentMenu("Evo/UI/UI Elements/Line Chart")]
    public class LineChart : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Chart Data", Constants.CustomEditorID)]
        public List<DataPoint> dataPoints = new()
        {
            new("Q1", 10),
            new("Q2", 25),
            new("Q3", 60),
            new("Q4", 40)
        };

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private RectOffset padding = new();
        [SerializeField] private float labelPadding = 20;
        [SerializeField] private float valuePadding = 10;
        [SerializeField, Range(0.1f, 20)] private float lineThickness = 3;
        [Range(1, 40)] public int horizontalGridLines = 5;
        [Range(1, 40)] public int verticalGridLines = 10;
        [SerializeField, Range(0.1f, 10)] private float gridLineThickness = 1;
        [SerializeField, Range(0, 40)] private float pointSize = 12;
        public Sprite pointSprite;

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource stylingSource = StylingSource.Custom;
        public StylerPreset stylerPreset;
        public ColorMapping lineColor = new() { stylerID = "Primary" };
        public ColorMapping pointColor = new() { stylerID = "Primary" };
        public ColorMapping gridColor = new() { stylerID = "Primary" };
        public ColorMapping axisColor = new() { stylerID = "Primary" };
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        public FontMapping labelFont = new() { stylerID = "Regular" };
        [SerializeField] private float labelFontSize = 12;
        public static string[] GetColorFields() => new[] { "lineColor", "pointColor", "gridColor", "axisColor", "labelColor" };
        public static string[] GetFontFields() => new[] { "labelFont" };

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
        Sprite defaultPointSprite;

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
            CreateDefaultPointSprite();
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
            if (defaultPointSprite != null && defaultPointSprite.texture != null)
            {
                DestroyImmediate(defaultPointSprite.texture);
                DestroyImmediate(defaultPointSprite);
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

        void CreateDefaultPointSprite()
        {
            if (pointSprite == null)
            {
                Texture2D circleTexture = new(32, 32);
                Color32[] pixels = new Color32[32 * 32];

                float radius = 16f;
                Vector2 center = new(16f, 16f);

                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);

                        if (distance <= radius)
                        {
                            float alpha = 1f - (distance / radius);
                            alpha = Mathf.SmoothStep(0f, 1f, alpha);
                            pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                        }
                        else
                        {
                            pixels[y * 32 + x] = Color.clear;
                        }
                    }
                }

                circleTexture.SetPixels32(pixels);
                circleTexture.Apply();

                defaultPointSprite = Sprite.Create(circleTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            }
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

            if (dataPoints == null || dataPoints.Count < 2)
                return;

            float chartWidth = rectTransform.rect.width;
            float chartHeight = rectTransform.rect.height;

            // Calculate graph area using 4-way padding
            float graphLeft = padding.left;
            float graphBottom = padding.bottom;
            float graphWidth = chartWidth - padding.left - padding.right;
            float graphHeight = chartHeight - padding.top - padding.bottom;

            if (graphWidth <= 0 || graphHeight <= 0)
                return;

            // Create containers
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

            // Draw components in order
            DrawGrid(gridContainer, graphLeft, graphBottom, graphWidth, graphHeight);
            DrawAxes(gridContainer, graphLeft, graphBottom, graphWidth, graphHeight);
            DrawData(dataContainer, graphLeft, graphBottom, graphWidth, graphHeight);
            DrawLabels(labelContainer, graphLeft, graphBottom, graphWidth, graphHeight);
        }

        public void ClearChart()
        {
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
            for (int i = 0; i <= horizontalGridLines; i++)
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

            // Vertical grid lines
            for (int i = 0; i <= verticalGridLines; i++)
            {
                float x = left + (i * width / verticalGridLines);
                CreateLine(
                    container,
                    new Vector2(x, bottom),
                    new Vector2(x, bottom + height),
                    styledGridColor,
                    gridLineThickness,
                    "V-Grid " + i
                );
            }
        }

        void DrawAxes(GameObject container, float left, float bottom, float width, float height)
        {
            Color styledAxisColor = Styler.GetColor(stylingSource, axisColor, stylerPreset);

            CreateLine(container, new Vector2(left, bottom), new Vector2(left + width, bottom), styledAxisColor, 2f, "X-Axis");
            CreateLine(container, new Vector2(left, bottom), new Vector2(left, bottom + height), styledAxisColor, 2f, "Y-Axis");
        }

        void DrawData(GameObject container, float left, float bottom, float width, float height)
        {
            if (dataPoints.Count == 0)
                return;

            // Calculate min/max values
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            foreach (var point in dataPoints)
            {
                if (point.value < minValue) minValue = point.value;
                if (point.value > maxValue) maxValue = point.value;
            }

            float valueRange = maxValue - minValue;
            if (valueRange == 0) valueRange = 1;

            // Calculate positions
            List<Vector2> positions = new();
            float xSpacing = width / (dataPoints.Count - 1);

            for (int i = 0; i < dataPoints.Count; i++)
            {
                float x = left + (i * xSpacing);
                float normalizedValue = (dataPoints[i].value - minValue) / valueRange;
                float y = bottom + (normalizedValue * height);

                positions.Add(new Vector2(x, y));
            }

            // Get styled colors
            Color styledLineColor = Styler.GetColor(stylingSource, lineColor, stylerPreset);
            Color styledPointColor = Styler.GetColor(stylingSource, pointColor, stylerPreset);

            // Draw lines between points
            for (int i = 0; i < positions.Count - 1; i++)
            {
                CreateLine(container, positions[i], positions[i + 1], styledLineColor, lineThickness, "Data Line " + i);
            }

            // Draw points
            for (int i = 0; i < positions.Count; i++)
            {
                CreatePoint(container, positions[i], styledPointColor, pointSize, "Data Point " + i);
            }
        }

        void DrawLabels(GameObject container, float left, float bottom, float width, float height)
        {
            if (dataPoints.Count == 0)
                return;

            // Get styled font and color
            TMP_FontAsset styledFont = Styler.GetFont(stylingSource, labelFont, stylerPreset);
            Color styledLabelColor = Styler.GetColor(stylingSource, labelColor, stylerPreset);

            // X-axis labels
            float xSpacing = width / (dataPoints.Count - 1);
            for (int i = 0; i < dataPoints.Count; i++)
            {
                float x = left + (i * xSpacing);
                CreateTMPLabel(
                    container,
                    dataPoints[i].label,
                    new Vector2(x, bottom - labelPadding),
                    TextAlignmentOptions.Top,
                    "X-Label " + i,
                    new Vector2(100, 30),
                    styledFont,
                    styledLabelColor
                );
            }

            // Y-axis labels
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            foreach (var point in dataPoints)
            {
                if (point.value < minValue) minValue = point.value;
                if (point.value > maxValue) maxValue = point.value;
            }

            for (int i = 0; i <= horizontalGridLines; i++)
            {
                float normalizedValue = (float)i / horizontalGridLines;
                float value = minValue + normalizedValue * (maxValue - minValue);
                float y = bottom + (normalizedValue * height);

                CreateTMPLabel(
                    container,
                    value.ToString("F1"),
                    new Vector2(left - valuePadding, y),
                    TextAlignmentOptions.MidlineRight,
                    "Y-Label " + i,
                    new Vector2(padding.left - valuePadding - 5, 30),
                    styledFont,
                    styledLabelColor
                );
            }
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

        void CreatePoint(GameObject container, Vector2 position, Color color, float size, string name)
        {
            GameObject pointObj = new(name, typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
            pointObj.transform.SetParent(container.transform, false);

            Image img = pointObj.GetComponent<Image>();
            img.sprite = pointSprite != null ? pointSprite : defaultPointSprite;
            img.color = color;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;

            RectTransform rt = pointObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
        }

        void CreateTMPLabel(GameObject container, string text, Vector2 position, TextAlignmentOptions alignment, string name, Vector2 size, TMP_FontAsset font, Color color)
        {
            GameObject labelObj = new(name, typeof(RectTransform), typeof(TextMeshProUGUI)) { hideFlags = HideFlags.DontSave };
            labelObj.transform.SetParent(container.transform, false);

            TextMeshProUGUI textComp = labelObj.GetComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = font;
            textComp.fontSize = labelFontSize;
            textComp.color = color;
            textComp.alignment = alignment;

            RectTransform rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            // For right-aligned text (Y-axis values), adjust pivot
            if (alignment == TextAlignmentOptions.MidlineRight) { rt.pivot = new Vector2(1f, 0.5f); }
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

        public void SetDataPoint(DataPoint targetData, string label, float value)
        {
            targetData.label = label;
            targetData.value = value;
            DrawChart();
        }

        public void SetPointSprite(Sprite sprite)
        {
            pointSprite = sprite;
            DrawChart();
        }

        public void SetPointSize(float newSize)
        {
            pointSize = newSize;
            DrawChart();
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
        [HideInInspector] public bool styleFoldout = false;

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