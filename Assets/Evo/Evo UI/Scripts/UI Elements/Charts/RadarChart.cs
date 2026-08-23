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
    [HelpURL(Constants.HelpUrl + "ui-elements/radar-chart")]
    [AddComponentMenu("Evo/UI/UI Elements/Radar Chart")]
    public class RadarChart : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Chart Data", Constants.CustomEditorID)]
        public List<DataPoint> dataPoints = new()
        {
            new("Speed", 85),
            new("Shooting", 70),
            new("Passing", 75),
            new("Dribbling", 90),
            new("Defense", 45),
            new("Physical", 80)
        };

        [EvoHeader("Chart Settings", Constants.CustomEditorID)]
        [SerializeField, Range(0.5f, 1)] private float scaleMultiplier = 0.8f;
        [SerializeField, Range(0, 360)] private float angleOffset = 90;

        [EvoHeader("Axis Settings", Constants.CustomEditorID)]
        [SerializeField, Range(0, 40)] private float axisThickness = 2;

        [EvoHeader("Fill Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showFill = true;

        [EvoHeader("Line Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showLine = true;
        [SerializeField, Range(0.1f, 20)] private float lineThickness = 3;

        [EvoHeader("Point Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showPoints = true;
        [SerializeField, Range(0.1f, 40)] private float pointSize = 10;

        [EvoHeader("Grid Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showGrid = true;
        [SerializeField, Range(1, 30)] private int gridLevels = 5;
        [SerializeField, Range(0.1f, 20)] private float gridLineThickness = 1;

        [EvoHeader("Label Settings", Constants.CustomEditorID)]
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelOffset = 20;
        [SerializeField] private float labelFontSize = 14;
        [SerializeField] private bool drawLabelBackground = false;
        [SerializeField] private Sprite labelBackgroundSprite;
        [SerializeField] private float labelBackgroundPPU = 6;
        [SerializeField] private Vector2 labelPadding = new(8, 4);

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource stylingSource = StylingSource.Custom;
        public StylerPreset stylerPreset;
        public ColorMapping axisColor = new() { stylerID = "Primary" };
        public ColorMapping fillColor = new() { stylerID = "Primary" };
        public ColorMapping lineColor = new() { stylerID = "Primary" };
        public ColorMapping pointColor = new() { stylerID = "Primary" };
        public ColorMapping gridColor = new() { stylerID = "Primary" };
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        public ColorMapping labelBackgroundColor = new() { stylerID = "Primary" };
        public FontMapping labelFont = new() { stylerID = "Regular" };
        public static string[] GetColorFields() => new[] { "axisColor", "fillColor", "lineColor", "pointColor",
            "gridColor", "labelColor", "labelBackgroundColor" };
        public static string[] GetFontFields() => new[] { "labelFont" };

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Constants
        const string GEN_SUFFIX = "[Generated]";

        // Helpers
        float radius;
        bool pendingRedraw;
        RectTransform rectTransform;

        [System.Serializable]
        public class DataPoint
        {
            public string label;
            [Range(0, 100)] public float value = 50;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            public DataPoint(string label = "", float value = 50)
            {
                this.label = label;
                this.value = Mathf.Clamp(value, 0, 100);
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

        void OnRectTransformDimensionsChange()
        {
            DrawChartSafe();
        }

        void DrawChartSafe()
        {
            if (gameObject.activeInHierarchy && rectTransform != null && !pendingRedraw)
            {
                pendingRedraw = true;
                // Defer the redraw until after the current UI rebuild completes
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

            if (dataPoints == null || dataPoints.Count < 3)
                return;

            // Calculate actual radius
            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;
            float minDimension = Mathf.Min(width, height);
            radius = (minDimension / 2f) * scaleMultiplier;

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
            Vector2 center = Vector2.zero;

            if (showGrid) { DrawGrid(gridContainer, center, radius); }
            DrawAxes(gridContainer, center, radius);
            DrawData(dataContainer, center, radius);
            if (showLabels) { DrawLabels(labelContainer, center, radius); }
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

        void DrawGrid(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;

            // Get styled colors
            Color styledGridColor = Styler.GetColor(stylingSource, gridColor, stylerPreset);

            // Draw concentric polygons
            for (int level = 1; level <= gridLevels; level++)
            {
                float levelRadius = (radius / gridLevels) * level;
                DrawPolygon(container, center, levelRadius, sides, styledGridColor, gridLineThickness, "Grid Level " + level);
            }
        }

        void DrawAxes(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;

            // Get styled color
            Color styledAxisColor = Styler.GetColor(stylingSource, axisColor, stylerPreset);

            // Draw main axes (stronger lines from center to each point)
            for (int i = 0; i < sides; i++)
            {
                float angle = (360f / sides) * i + angleOffset;
                float angleRad = angle * Mathf.Deg2Rad;
                Vector2 endPoint = center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
                DrawLine(container, center, endPoint, styledAxisColor, axisThickness, "Axis " + i);
            }
        }

        void DrawData(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;
            List<Vector2> points = new();

            // Calculate data points
            for (int i = 0; i < sides; i++)
            {
                float angle = (360f / sides) * i + angleOffset;
                float angleRad = angle * Mathf.Deg2Rad;
                float value = dataPoints[i].value / 100f; // Normalize to 0-1
                float pointRadius = radius * value;

                Vector2 point = center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * pointRadius;
                points.Add(point);
            }

            // Get styled colors
            Color styledFillColor = Styler.GetColor(stylingSource, fillColor, stylerPreset);
            Color styledLineColor = Styler.GetColor(stylingSource, lineColor, stylerPreset);
            Color styledPointColor = Styler.GetColor(stylingSource, pointColor, stylerPreset);

            // Draw filled area
            if (showFill && points.Count >= 3)
            {
                GameObject fillObj = new("Fill") { hideFlags = HideFlags.DontSave };
                fillObj.transform.SetParent(container.transform, false);

                RectTransform rt = fillObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = center;
                rt.sizeDelta = new Vector2(radius * 2, radius * 2);

                fillObj.AddComponent<CanvasRenderer>();

                RadarChartGraphic fill = fillObj.AddComponent<RadarChartGraphic>();
                fill.color = styledFillColor;
                fill.points = points;
                fill.center = center;
            }

            // Draw outline
            if (showLine)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    int nextIndex = (i + 1) % points.Count;
                    DrawLine(container, points[i], points[nextIndex], styledLineColor, lineThickness, "Data Line " + i);
                }
            }

            // Draw points
            if (showPoints)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    DrawPoint(container, points[i], styledPointColor, pointSize, "Data Point " + i);
                }
            }
        }

        void DrawLabels(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;

            // Get styled font and colors
            TMP_FontAsset styledLabelFont = Styler.GetFont(stylingSource, labelFont, stylerPreset);
            Color styledLabelColor = Styler.GetColor(stylingSource, labelColor, stylerPreset);
            Color styledLabelBackgroundColor = Styler.GetColor(stylingSource, labelBackgroundColor, stylerPreset);

            for (int i = 0; i < sides; i++)
            {
                float angle = (360f / sides) * i + angleOffset;
                float angleRad = angle * Mathf.Deg2Rad;
                Vector2 labelPos = center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * (radius + labelOffset);
                CreateLabel(dataPoints[i].label, labelPos, container, "Label " + i, styledLabelFont, styledLabelColor, styledLabelBackgroundColor);
            }
        }

        void DrawPolygon(GameObject container, Vector2 center, float radius, int sides, Color color, float thickness, string name)
        {
            for (int i = 0; i < sides; i++)
            {
                float angle1 = (360f / sides) * i + angleOffset;
                float angle2 = (360f / sides) * ((i + 1) % sides) + angleOffset;

                float rad1 = angle1 * Mathf.Deg2Rad;
                float rad2 = angle2 * Mathf.Deg2Rad;

                Vector2 point1 = center + new Vector2(Mathf.Cos(rad1), Mathf.Sin(rad1)) * radius;
                Vector2 point2 = center + new Vector2(Mathf.Cos(rad2), Mathf.Sin(rad2)) * radius;

                DrawLine(container, point1, point2, color, thickness, name + " Side " + i);
            }
        }

        void DrawLine(GameObject container, Vector2 start, Vector2 end, Color color, float thickness, string name)
        {
            GameObject lineObj = new(name) { hideFlags = HideFlags.DontSave };
            lineObj.transform.SetParent(container.transform, false);

            RectTransform rt = lineObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);

            Vector2 dir = end - start;
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = start;
            rt.sizeDelta = new Vector2(distance, thickness);
            rt.rotation = Quaternion.Euler(0, 0, angle);

            Image img = lineObj.AddComponent<Image>();
            img.color = color;
        }

        void DrawPoint(GameObject container, Vector2 position, Color color, float size, string name)
        {
            GameObject pointObj = new(name) { hideFlags = HideFlags.DontSave };
            pointObj.transform.SetParent(container.transform, false);

            RectTransform rt = pointObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);

            Image img = pointObj.AddComponent<Image>();
            img.color = color;

            // Make it circular
            img.sprite = CreateCircleSprite(32);
        }

        void CreateLabel(string text, Vector2 position, GameObject parent, string name, TMP_FontAsset font, Color textColor, Color backgroundColor)
        {
            GameObject labelObj = new(name) { hideFlags = HideFlags.DontSave };
            labelObj.transform.SetParent(parent.transform, false);

            RectTransform rt = labelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;

            // Create background first if enabled
            if (drawLabelBackground)
            {
                GameObject bgObj = new("Background") { hideFlags = HideFlags.DontSave };
                bgObj.transform.SetParent(labelObj.transform, false);

                RectTransform bgRt = bgObj.AddComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                bgRt.anchoredPosition = Vector2.zero;

                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.sprite = labelBackgroundSprite;
                bgImage.color = backgroundColor;
                bgImage.pixelsPerUnitMultiplier = labelBackgroundPPU;
                bgImage.type = labelBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }

            // Create text after background
            GameObject textObj = new("Text") { hideFlags = HideFlags.DontSave };
            textObj.transform.SetParent(labelObj.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = font;
            textComp.fontSize = labelFontSize;
            textComp.color = textColor;
            textComp.alignment = TextAlignmentOptions.Center;

            // Size the label based on text
            textComp.ForceMeshUpdate();
            Vector2 textSize = textComp.GetRenderedValues(false);
            rt.sizeDelta = textSize + labelPadding * 2;
        }

        Sprite CreateCircleSprite(int size)
        {
            Texture2D tex = new(size, size);
            Color32[] pixels = new Color32[size * size];

            float radius = size / 2f;
            Vector2 center = new(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        IEnumerator DeferredDrawChart()
        {
            // Wait until end of frame to ensure UI rebuild is complete
            yield return new WaitForEndOfFrame();
            pendingRedraw = false;
            DrawChart();
        }

        /// <summary>
        /// Adds a new data point to the chart with a label and value.
        /// </summary>
        public void AddDataPoint(string label, float value)
        {
            dataPoints.Add(new DataPoint(label, value));
            DrawChart();
        }

        /// <summary>
        /// Adds a pre-configured DataPoint object to the chart.
        /// </summary>
        public void AddDataPoint(DataPoint point)
        {
            if (point != null)
            {
                dataPoints.Add(point);
                DrawChart();
            }
        }

        /// <summary>
        /// Removes the first data point matching the specified label.
        /// </summary>
        /// <param name="label">The label to search for.</param>
        public void RemoveDataPoint(string label)
        {
            var point = dataPoints.Find(p => p.label == label);
            if (point != null)
            {
                dataPoints.Remove(point);
                DrawChart();
            }
        }

        /// <summary>
        /// Removes a data point at a specific index.
        /// </summary>
        public void RemoveDataPoint(int index)
        {
            if (index >= 0 && index < dataPoints.Count)
            {
                dataPoints.RemoveAt(index);
                DrawChart();
            }
        }

        /// <summary>
        /// Removes a specific DataPoint object reference.
        /// </summary>
        public void RemoveDataPoint(DataPoint point)
        {
            if (dataPoints.Contains(point))
            {
                dataPoints.Remove(point);
                DrawChart();
            }
        }

        /// <summary>
        /// Sets data point using the data index.
        /// </summary>
        public void SetDataPoint(int index, float value)
        {
            if (index >= 0 && index < dataPoints.Count)
            {
                dataPoints[index].value = Mathf.Clamp(value, 0f, 100f);
                DrawChart();
            }
        }

        /// <summary>
        /// Sets data point using the data label.
        /// </summary>
        public void SetDataPoint(string label, float value)
        {
            var point = dataPoints.Find(p => p.label == label);
            if (point != null)
            {
                point.value = Mathf.Clamp(value, 0f, 100f);
                DrawChart();
            }
        }

        /// <summary>
        /// Sets data point using a specific data reference.
        /// </summary>
        public void SetDataPoint(DataPoint data, string label, float value)
        {
            data.label = label;
            data.value = Mathf.Clamp(value, 0f, 100f);
            DrawChart();
        }

        /// <summary>
        /// Updates all chart data.
        /// </summary>
        public void UpdateAllData(float[] values)
        {
            for (int i = 0; i < values.Length && i < dataPoints.Count; i++) { dataPoints[i].value = Mathf.Clamp(values[i], 0f, 100f); }
            DrawChart();
        }

        /// <summary>
        /// Removes all data points from the chart.
        /// </summary>
        public void ClearDataPoints()
        {
            dataPoints.Clear();
            DrawChart();
        }

        public void SetAxisThickness(float value)
        {
            axisThickness = value;
            DrawChart();
        }

        public bool ShowFill
        {
            get { return showFill; }
            set
            {
                showFill = value;
                DrawChart();
            }
        }

        public bool ShowPoints
        {
            get { return showPoints; }
            set
            {
                showPoints = value;
                DrawChart();
            }
        }

        public bool ShowLine
        {
            get { return showLine; }
            set
            {
                showLine = value;
                DrawChart();
            }
        }

        public bool ShowGrid
        {
            get { return showGrid; }
            set
            {
                showGrid = value;
                DrawChart();
            }
        }

        public bool ShowLabels
        {
            get { return showLabels; }
            set
            {
                showLabels = value;
                DrawChart();
            }
        }

#if EVO_LOCALIZATION
        void OnDestroy()
        {
            if (Application.isPlaying && enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
        }

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
        [HideInInspector] public bool labelFoldout = false;
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

    public class RadarChartGraphic : MaskableGraphic
    {
        public List<Vector2> points = new();
        public Vector2 center = Vector2.zero;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (points == null || points.Count < 3)
                return;

            // Add center vertex
            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.position = center;
            centerVertex.color = color;
            vh.AddVert(centerVertex);

            // Add perimeter vertices
            foreach (var point in points)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = point;
                vertex.color = color;
                vh.AddVert(vertex);
            }

            // Create triangles
            for (int i = 0; i < points.Count; i++)
            {
                int nextIndex = (i + 1) % points.Count;
                vh.AddTriangle(0, i + 1, nextIndex + 1);
            }
        }
    }
}