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
    [HelpURL(Constants.HelpUrl + "ui-elements/pie-chart")]
    [AddComponentMenu("Evo/UI/UI Elements/Pie Chart")]
    public class PieChart : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Chart Data", Constants.CustomEditorID)]
        public List<DataPoint> dataPoints = new()
        {
            new("Category A", 30, new Color(0.2f, 0.5f, 1f, 1f)),
            new("Category B", 25, new Color(1f, 0.4f, 0.3f, 1f)),
            new("Category C", 20, new Color(0.3f, 0.8f, 0.4f, 1f)),
            new("Category D", 15, new Color(1f, 0.8f, 0.2f, 1f))
        };

        [EvoHeader("Chart Settings", Constants.CustomEditorID)]
        [Min(0)] public float innerRadius = 0;
        [Range(0, 360)] public float startAngle = 90;
        [Range(1, 100)] public int segments = 100;
        public bool enableAntiAliasing = true;
        [Range(0.1f, 4)] public float antiAliasingWidth = 0.5f;

        [EvoHeader("Border Settings", Constants.CustomEditorID)]
        public bool drawBorder = false;
        public float borderWidth = 2;

        [EvoHeader("Label Settings", Constants.CustomEditorID)]
        public bool showLabels = true;
        public bool showPercentages = true;
        public float labelFontSize = 24;
        public bool drawLabelBackground = true;
        public Sprite labelBackgroundSprite;
        public float labelBackgroundPPU = 6;
        public Vector2 labelPadding = new(8, 4);

        [EvoHeader("Legend Settings", Constants.CustomEditorID)]
        public bool showLegend = true;
        public RectTransform legendContainer;
        public float legendItemHeight = 40;
        public float legendColorBoxSize = 24;
        public Sprite legendColorBoxSprite;
        public float legendFontSize = 24;

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource stylingSource = StylingSource.Custom;
        public StylerPreset stylerPreset;
        public ColorMapping borderColor = new() { stylerID = "Primary" };
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        public ColorMapping labelBackgroundColor = new() { stylerID = "Primary" };
        public ColorMapping legendTextColor = new() { stylerID = "Primary" };
        public FontMapping labelFont = new() { stylerID = "Regular" };
        public FontMapping legendFont = new() { stylerID = "Regular" };
        public static string[] GetColorFields() => new[] { "borderColor", "labelColor", "labelBackgroundColor", "legendTextColor" };
        public static string[] GetFontFields() => new[] { "labelFont", "legendFont" };

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Constants
        const string GEN_SUFFIX = "[Generated]";

        // Helpers
        RectTransform rectTransform;
        float totalValue;
        float radius;
        bool pendingRedraw;

        // Legend cache
        const string LEGEND_SUFFIX = "(Legend)";
        readonly Dictionary<RectTransform, List<GameObject>> legendTracking = new();

        [System.Serializable]
        public class DataPoint
        {
            public string label;
            [Min(0)] public float value;
            public Color color;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            public DataPoint(string label = "", float value = 0, Color? color = null)
            {
                this.label = label;
                this.value = value;
                this.color = color ?? Color.blue;
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
            if (Application.isPlaying && showLegend && enableLocalization)
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
            // Clean up legend tracking for this chart
            if (legendContainer != null && legendTracking.ContainsKey(legendContainer))
            {
                legendTracking.Remove(legendContainer);
            }
#if EVO_LOCALIZATION
            if (Application.isPlaying && showLegend && enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
#endif
        }

        void OnRectTransformDimensionsChange()
        {
            DrawChartSafe();
        }

        /// <summary>
        /// Used to create chart so that Unity catches up. Triggeredw via rect change and editor calls.
        /// </summary>
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

            if (dataPoints == null || dataPoints.Count == 0)
                return;

            // Calculate total value
            totalValue = 0f;
            foreach (var point in dataPoints) { totalValue += point.value; }

            // Return if no value
            if (totalValue <= 0)
                return;

            // Calculate actual radius
            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;
            float minDimension = Mathf.Min(width, height);
            radius = minDimension / 2f;

            // Draw components
            DrawPieSlices(radius);
            if (drawBorder) { DrawBorder(radius); }
            if (showLabels) { DrawLabels(radius); }
            if (showLegend) { DrawLegend(); }
        }

        public void ClearChart()
        {
            // Clear all chart elements
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != legendContainer && child.name.Contains(GEN_SUFFIX))
                {
                    if (Application.isPlaying) { Destroy(child.gameObject); }
                    else { DestroyImmediate(child.gameObject); }
                }
            }

            // Clear legend if disabled
            if (!showLegend && legendContainer != null) { CleanupLegendContainer(legendContainer); }
        }

        void DrawPieSlices(float actualRadius)
        {
            float currentAngle = startAngle;

            GameObject container = new($"Slices {GEN_SUFFIX}") { hideFlags = HideFlags.DontSave };
            container.transform.SetParent(transform, false);
         
            RectTransform gridRT = container.AddComponent<RectTransform>();
            gridRT.anchorMin = Vector2.zero;
            gridRT.anchorMax = Vector2.one;
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                float percentage = dataPoints[i].value / totalValue;
                float angleSpan = percentage * 360f;

                if (angleSpan > 0)
                {
                    GameObject slice = CreatePieSlice(currentAngle, angleSpan, dataPoints[i].color, actualRadius, innerRadius);
                    slice.transform.SetParent(container.transform, false);
                    slice.name = "Slice " + i;
                }

                currentAngle += angleSpan;
            }
        }

        GameObject CreatePieSlice(float startAngle, float angleSpan, Color color, float outerRadius, float innerRadius)
        {
            GameObject sliceObj = new("Slice") { hideFlags = HideFlags.DontSave };
            RectTransform rt = sliceObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(outerRadius * 2, outerRadius * 2);

            // Add CanvasRenderer
            sliceObj.AddComponent<CanvasRenderer>();

            // Create custom graphic component
            PieChartSlice slice = sliceObj.AddComponent<PieChartSlice>();
            slice.enableAntiAliasing = enableAntiAliasing;
            slice.antiAliasingWidth = antiAliasingWidth;
            slice.color = color;
            slice.startAngle = startAngle;
            slice.angleSpan = angleSpan;
            slice.outerRadius = outerRadius;
            slice.innerRadius = innerRadius;
            slice.segments = Mathf.Max(3, Mathf.CeilToInt(segments * (angleSpan / 360f)));

            return sliceObj;
        }

        void DrawBorder(float actualRadius)
        {
            // Get styled border color
            Color styledBorderColor = Styler.GetColor(stylingSource, borderColor, stylerPreset);

            // Outer border
            GameObject outerBorder = CreateBorderRing(actualRadius, actualRadius + borderWidth, "Outer Border", styledBorderColor);
            outerBorder.transform.SetParent(transform, false);

            // Inner border for donut
            if (innerRadius > 0)
            {
                GameObject innerBorder = CreateBorderRing(innerRadius - borderWidth, innerRadius, "Inner Border", styledBorderColor);
                innerBorder.transform.SetParent(transform, false);
            }
        }

        GameObject CreateBorderRing(float inner, float outer, string name, Color borderColor)
        {
            GameObject borderObj = new(name) { hideFlags = HideFlags.DontSave };
            RectTransform rt = borderObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(outer * 2, outer * 2);

            borderObj.AddComponent<CanvasRenderer>();

            PieChartSlice border = borderObj.AddComponent<PieChartSlice>();
            border.color = borderColor;
            border.startAngle = 0;
            border.angleSpan = 360;
            border.outerRadius = outer;
            border.innerRadius = inner;
            border.segments = segments;

            return borderObj;
        }

        void DrawLabels(float actualRadius)
        {
            float currentAngle = startAngle;

            // Count how many items are actually visible
            int visibleCount = 0;
            foreach (var point in dataPoints)
            {
                if (point.value > 0)
                {
                    visibleCount++;
                }
            }

            GameObject labelContainer = new($"Labels {GEN_SUFFIX}") { hideFlags = HideFlags.DontSave };
            labelContainer.transform.SetParent(transform, false);

            RectTransform gridRT = labelContainer.AddComponent<RectTransform>();
            gridRT.anchorMin = Vector2.zero;
            gridRT.anchorMax = Vector2.one;
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (dataPoints[i].value <= 0)
                    continue;

                float percentage = dataPoints[i].value / totalValue;
                float angleSpan = percentage * 360f;
                float midAngle = currentAngle + angleSpan / 2;

                Vector2 labelPos;

                // Check if this is the ONLY visible label
                if (visibleCount == 1) { labelPos = Vector2.zero; }
                else
                {
                    // Normal calculation for multiple slices
                    float labelRadius = innerRadius > 0 ? (innerRadius + actualRadius) / 2 : actualRadius * 0.7f;
                    float angleRad = midAngle * Mathf.Deg2Rad;
                    labelPos = new(Mathf.Cos(angleRad) * labelRadius, Mathf.Sin(angleRad) * labelRadius);
                }

                string labelText = showPercentages ? (percentage * 100f).ToString("F0") + "%" : dataPoints[i].value.ToString("F0");
                if (!string.IsNullOrEmpty(labelText)) { CreateLabel(labelText, labelPos, labelContainer, "Label " + i); }

                currentAngle += angleSpan;
            }
        }

        void CreateLabel(string text, Vector2 position, GameObject parent, string name)
        {
            GameObject labelObj = new(name) { hideFlags = HideFlags.DontSave };
            labelObj.transform.SetParent(parent.transform, false);

            RectTransform rt = labelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;

            // Create text
            GameObject textObj = new("Text") { hideFlags = HideFlags.DontSave };
            textObj.transform.SetParent(labelObj.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = Styler.GetFont(stylingSource, labelFont, stylerPreset);
            textComp.fontSize = labelFontSize;
            textComp.color = Styler.GetColor(stylingSource, labelColor, stylerPreset);
            textComp.alignment = TextAlignmentOptions.Center;

            // Size the label
            textComp.ForceMeshUpdate();
            Vector2 textSize = textComp.GetRenderedValues(false);
            rt.sizeDelta = textSize + labelPadding * 2;

            // Create background
            if (drawLabelBackground)
            {
                GameObject bgObj = new("Background") { hideFlags = HideFlags.DontSave };
                bgObj.transform.SetParent(labelObj.transform, false);
                bgObj.transform.SetAsFirstSibling();

                RectTransform bgRt = bgObj.AddComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                bgRt.anchoredPosition = Vector2.zero;

                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.sprite = labelBackgroundSprite;
                bgImage.color = Styler.GetColor(stylingSource, labelBackgroundColor, stylerPreset);
                bgImage.type = labelBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                bgImage.pixelsPerUnitMultiplier = labelBackgroundPPU;
            }
        }

        void DrawLegend()
        {
            if (legendContainer == null)
                return;

            // Clean up any existing legend items in this container
            CleanupLegendContainer(legendContainer);

            // Track new legend items
            List<GameObject> newLegendItems = new();

            float totalHeight = dataPoints.Count * legendItemHeight;
            float startY = totalHeight / 2f;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                GameObject legendItem = new($"{dataPoints[i].label} {LEGEND_SUFFIX}") { hideFlags = HideFlags.DontSave };
                legendItem.transform.SetParent(legendContainer, false);
                newLegendItems.Add(legendItem);

                RectTransform itemRt = legendItem.AddComponent<RectTransform>();
                itemRt.anchorMin = new Vector2(0, 0.5f);
                itemRt.anchorMax = new Vector2(1, 0.5f);
                itemRt.pivot = new Vector2(0.5f, 0.5f);
                itemRt.anchoredPosition = new Vector2(0, startY - (i * legendItemHeight) - legendItemHeight / 2f);
                itemRt.sizeDelta = new Vector2(0, legendItemHeight);

                // Color box
                GameObject colorBox = new("Color Box") { hideFlags = HideFlags.DontSave };
                colorBox.transform.SetParent(legendItem.transform, false);

                RectTransform colorRt = colorBox.AddComponent<RectTransform>();
                colorRt.anchorMin = new Vector2(0, 0.5f);
                colorRt.anchorMax = new Vector2(0, 0.5f);
                colorRt.pivot = new Vector2(0, 0.5f);
                colorRt.anchoredPosition = new Vector2(5, 0);
                colorRt.sizeDelta = new Vector2(legendColorBoxSize, legendColorBoxSize);

                Image colorBoxImage = colorBox.AddComponent<Image>();
                colorBoxImage.sprite = legendColorBoxSprite;
                colorBoxImage.color = dataPoints[i].color;
                colorBoxImage.type = legendColorBoxSprite != null ? Image.Type.Sliced : Image.Type.Simple;

                // Label
                GameObject label = new("Label") { hideFlags = HideFlags.DontSave };
                label.transform.SetParent(legendItem.transform, false);

                RectTransform labelRt = label.AddComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0, 0);
                labelRt.anchorMax = new Vector2(1, 1);
                labelRt.anchoredPosition = new Vector2(legendColorBoxSize + 4, 0);
                labelRt.sizeDelta = new Vector2(-(legendColorBoxSize), 0);

                TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
                float percentage = (dataPoints[i].value / totalValue) * 100f;
                labelText.text = dataPoints[i].label + " (" + percentage.ToString("F0") + "%)";
                labelText.font = Styler.GetFont(stylingSource, legendFont, stylerPreset);
                labelText.fontSize = legendFontSize;
                labelText.color = Styler.GetColor(stylingSource, legendTextColor, stylerPreset);
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
                labelText.verticalAlignment = VerticalAlignmentOptions.Middle;
#if UNITY_6000_0_OR_NEWER
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
#else
                labelText.enableWordWrapping = false;
#endif
            }

            // Update tracking
            legendTracking[legendContainer] = newLegendItems;
        }

        void CleanupLegendContainer(RectTransform container)
        {
            // Clean up tracked items for this container
            if (legendTracking.ContainsKey(container))
            {
                foreach (var item in legendTracking[container])
                {
                    if (item != null)
                    {
                        if (Application.isPlaying) { Destroy(item); }
                        else { DestroyImmediate(item); }
                    }
                }

                legendTracking[container].Clear();
            }

            // Clean up all legend items
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                // Check for legend items by looking for the legend suffix
                if (child.name.EndsWith(LEGEND_SUFFIX))
                {
                    if (Application.isPlaying) { Destroy(child.gameObject); }
                    else { DestroyImmediate(child.gameObject); }
                }
            }
        }

        IEnumerator DeferredDrawChart()
        {
            // Wait until end of frame to ensure UI rebuild is complete
            yield return new WaitForEndOfFrame();
            pendingRedraw = false;
            DrawChart();
        }

        public void AddDataPoint(string label, float value, Color color)
        {
            dataPoints.Add(new DataPoint(label, value, color));
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

        public void SetDataPoint(int index, string label, float value, Color? color = null)
        {
            if (index >= 0 && index < dataPoints.Count)
            {
                dataPoints[index].label = label;
                dataPoints[index].value = value;
                if (color.HasValue) { dataPoints[index].color = color.Value; }
                DrawChart();
            }
        }

        public void SetDataPoint(DataPoint data, string label, float value, Color? color = null)
        {
            data.label = label;
            data.value = value;
            if (color.HasValue) { data.color = color.Value; }
            DrawChart();
        }

        public void SetInnerRadius(float newInnerRadius)
        {
            innerRadius = Mathf.Clamp(newInnerRadius, 0, radius * 0.9f);
            DrawChart();
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

        public bool ShowLegend
        {
            get { return showLegend; }
            set
            {
                showLegend = value;
                if (showLegend) { DrawLegend(); }
                else if (legendContainer != null) { CleanupLegendContainer(legendContainer); }
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
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool labelFoldout = false;
        [HideInInspector] public bool legendFoldout = false;

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

    public class PieChartSlice : MaskableGraphic
    {
        public float startAngle = 0;
        public float angleSpan = 90;
        public float outerRadius = 100;
        public float innerRadius = 0;
        public int segments = 20;

        [Header("Anti-Aliasing")]
        public bool enableAntiAliasing = true;
        public float antiAliasingWidth = 0.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (angleSpan <= 0) 
                return;

            if (enableAntiAliasing) { PopulateMeshWithAA(vh); }
            else { PopulateMeshNoAA(vh); }
        }

        void PopulateMeshWithAA(VertexHelper vh)
        {
            List<UIVertex> vertices = new();
            List<int> indices = new();

            float angleStep = angleSpan / segments;
            float aaWidth = antiAliasingWidth;

            if (innerRadius <= 0)
            {
                // Pie slice with AA
                UIVertex centerVertex = UIVertex.simpleVert;
                centerVertex.position = Vector3.zero;
                centerVertex.color = color;
                vertices.Add(centerVertex);

                // Inner solid ring
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    UIVertex innerVertex = UIVertex.simpleVert;
                    innerVertex.position = new Vector3(
                        cos * (outerRadius - aaWidth),
                        sin * (outerRadius - aaWidth),
                        0
                    );
                    innerVertex.color = color;
                    vertices.Add(innerVertex);
                }

                // Outer AA ring (transparent edge)
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    UIVertex outerVertex = UIVertex.simpleVert;
                    outerVertex.position = new Vector3(
                        cos * outerRadius,
                        sin * outerRadius,
                        0
                    );
                    Color32 transparentColor = color;
                    transparentColor.a = 0;
                    outerVertex.color = transparentColor;
                    vertices.Add(outerVertex);
                }

                // Center to inner ring triangles
                for (int i = 0; i < segments; i++)
                {
                    indices.Add(0);
                    indices.Add(i + 1);
                    indices.Add(i + 2);
                }

                // Inner to outer ring triangles (AA edge)
                int innerStart = 1;
                int outerStart = segments + 2;
                for (int i = 0; i < segments; i++)
                {
                    int innerCurrent = innerStart + i;
                    int innerNext = innerStart + i + 1;
                    int outerCurrent = outerStart + i;
                    int outerNext = outerStart + i + 1;

                    indices.Add(innerCurrent);
                    indices.Add(outerCurrent);
                    indices.Add(outerNext);

                    indices.Add(innerCurrent);
                    indices.Add(outerNext);
                    indices.Add(innerNext);
                }
            }
            else
            {
                // Donut slice with AA on both edges
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    // Inner edge AA (transparent)
                    UIVertex innerAA = UIVertex.simpleVert;
                    innerAA.position = new Vector3(cos * innerRadius, sin * innerRadius, 0);
                    Color32 transparentColor = color;
                    transparentColor.a = 0;
                    innerAA.color = transparentColor;
                    vertices.Add(innerAA);

                    // Inner edge solid
                    UIVertex innerSolid = UIVertex.simpleVert;
                    innerSolid.position = new Vector3(cos * (innerRadius + aaWidth), sin * (innerRadius + aaWidth), 0);
                    innerSolid.color = color;
                    vertices.Add(innerSolid);

                    // Outer edge solid
                    UIVertex outerSolid = UIVertex.simpleVert;
                    outerSolid.position = new Vector3(cos * (outerRadius - aaWidth), sin * (outerRadius - aaWidth), 0);
                    outerSolid.color = color;
                    vertices.Add(outerSolid);

                    // Outer edge AA (transparent)
                    UIVertex outerAA = UIVertex.simpleVert;
                    outerAA.position = new Vector3(cos * outerRadius, sin * outerRadius, 0);
                    outerAA.color = transparentColor;
                    vertices.Add(outerAA);
                }

                for (int i = 0; i < segments; i++)
                {
                    int innerAACurr = i * 4;
                    int innerSolidCurr = i * 4 + 1;
                    int outerSolidCurr = i * 4 + 2;
                    int outerAACurr = i * 4 + 3;

                    int innerAANext = (i + 1) * 4;
                    int innerSolidNext = (i + 1) * 4 + 1;
                    int outerSolidNext = (i + 1) * 4 + 2;
                    int outerAANext = (i + 1) * 4 + 3;

                    // Inner AA strip
                    indices.Add(innerAACurr);
                    indices.Add(innerSolidCurr);
                    indices.Add(innerSolidNext);

                    indices.Add(innerAACurr);
                    indices.Add(innerSolidNext);
                    indices.Add(innerAANext);

                    // Solid middle strip
                    indices.Add(innerSolidCurr);
                    indices.Add(outerSolidCurr);
                    indices.Add(outerSolidNext);

                    indices.Add(innerSolidCurr);
                    indices.Add(outerSolidNext);
                    indices.Add(innerSolidNext);

                    // Outer AA strip
                    indices.Add(outerSolidCurr);
                    indices.Add(outerAACurr);
                    indices.Add(outerAANext);

                    indices.Add(outerSolidCurr);
                    indices.Add(outerAANext);
                    indices.Add(outerSolidNext);
                }
            }

            // Add to VertexHelper
            foreach (var vertex in vertices) { vh.AddVert(vertex); }
            for (int i = 0; i < indices.Count; i += 3) { vh.AddTriangle(indices[i], indices[i + 1], indices[i + 2]); }
        }

        void PopulateMeshNoAA(VertexHelper vh)
        {
            List<UIVertex> vertices = new();
            List<int> indices = new();

            float angleStep = angleSpan / segments;

            if (innerRadius <= 0)
            {
                // Pie slice
                UIVertex centerVertex = UIVertex.simpleVert;
                centerVertex.position = Vector3.zero;
                centerVertex.color = color;
                vertices.Add(centerVertex);

                for (int i = 0; i <= segments; i++)
                {
                    float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;

                    UIVertex vertex = UIVertex.simpleVert;
                    vertex.position = new Vector3(
                        Mathf.Cos(angle) * outerRadius,
                        Mathf.Sin(angle) * outerRadius,
                        0
                    );
                    vertex.color = color;
                    vertices.Add(vertex);
                }

                for (int i = 0; i < segments; i++)
                {
                    indices.Add(0);
                    indices.Add(i + 1);
                    indices.Add(i + 2);
                }
            }
            else
            {
                // Donut slice
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    UIVertex innerVertex = UIVertex.simpleVert;
                    innerVertex.position = new Vector3(cos * innerRadius, sin * innerRadius, 0);
                    innerVertex.color = color;
                    vertices.Add(innerVertex);

                    UIVertex outerVertex = UIVertex.simpleVert;
                    outerVertex.position = new Vector3(cos * outerRadius, sin * outerRadius, 0);
                    outerVertex.color = color;
                    vertices.Add(outerVertex);
                }

                for (int i = 0; i < segments; i++)
                {
                    int innerCurrent = i * 2;
                    int outerCurrent = i * 2 + 1;
                    int innerNext = (i + 1) * 2;
                    int outerNext = (i + 1) * 2 + 1;

                    indices.Add(innerCurrent);
                    indices.Add(outerCurrent);
                    indices.Add(outerNext);

                    indices.Add(innerCurrent);
                    indices.Add(outerNext);
                    indices.Add(innerNext);
                }
            }

            foreach (var vertex in vertices) { vh.AddVert(vertex); }
            for (int i = 0; i < indices.Count; i += 3) { vh.AddTriangle(indices[i], indices[i + 1], indices[i + 2]); }
        }
    }
}