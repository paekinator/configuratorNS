using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "ui-elements/list-view")]
    [AddComponentMenu("Evo/UI/UI Elements/List View (Preview)")]
    public partial class ListView : MonoBehaviour, IStylerHandler
    {
        // Content
        public List<ListViewColumn> columns = new();
        public List<ListViewRow> rows = new();

        // Style Settings
        public ListViewStyle style = new();
        public ListViewStyle.StylingSource stylingSource = ListViewStyle.StylingSource.Custom;
        public List<ListViewStyle.Mapping> styleMapping = new();
        public StylerPreset stylerPreset;

        // Scrolling
        public bool enableScrolling = false;
        public Scrollbar verticalScrollbar;
        [Min(0)] public float scrollSensitivity = 10;

        // References
        public RectTransform parentRect;

        // Events
        public UnityEvent<int, int> onCellClicked = new();

        // Cache
        RectTransform contentContainer;
        RectTransform headerContainer;
        RectTransform rowContainer;
        ScrollRect scrollRect;

        // Object pools
        readonly List<GameObject> headerObjects = new();
        readonly List<List<GameObject>> rowObjects = new();

        // Cached values for performance
        float[] cachedColumnWidths;
        bool layoutCacheDirty = true;
        Vector2 lastContainerSize;
        float cachedTotalHeight = 0f;

        // Change tracking
        [HideInInspector] public bool isDirty = false;
        int columnDataHash = 0;
        int rowDataHash = 0;
        int layoutPropertiesHash = 0;
        int stylePropertiesHash = 0;

        // Styler Interface
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            // Invalidate style hash to ensure visual update even if the preset reference is the same
            stylePropertiesHash = 0;
            RequestRefresh();
        }

        void Awake()
        {
            Initialize();
            InitializeStyleMappings();
        }

        void Start() => Refresh();

        public void Initialize()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                return;
#endif

            CleanupExistingObjects();
            CreateContainerStructure();
            ResetChangeTracking();
        }

        void InitializeStyleMappings()
        {
            if (styleMapping == null || styleMapping.Count == 0)
            {
                styleMapping = new List<ListViewStyle.Mapping>
                {
                    new() { type = ListViewStyle.Type.HeaderFont, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.RowFont, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.HeaderTextColor, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.RowTextColor, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.HeaderBackgroundColor, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.RowBackgroundColor, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.AlternatingRowColor, colorID = "", fontID = "" },
                    new() { type = ListViewStyle.Type.BorderColor, colorID = "", fontID = "" }
                };
            }
        }

        void CleanupExistingObjects()
        {
            ClearObjectPool(headerObjects);
            foreach (var rowList in rowObjects) { ClearObjectPool(rowList); }
            rowObjects.Clear();

            // Destroy generated containers
            if (this != null && gameObject != null)
            {
                var children = new List<GameObject>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    // Only destroy objects we likely created (marked with DontSave or specific names)
                    if (child.gameObject.hideFlags == HideFlags.DontSave ||
                        child.name.Contains("[Generated]") ||
                        child.name == "Header" ||
                        child.name == "Rows" ||
                        child.name == "Scroll View")
                    {
                        children.Add(child.gameObject);
                    }
                }

                foreach (var child in children)
                    DestroyGameObject(child);
            }
        }

        void ClearObjectPool(List<GameObject> pool)
        {
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (pool[i] != null)
                    DestroyGameObject(pool[i]);
            }
            pool.Clear();
        }

        void DestroyGameObject(GameObject obj)
        {
            if (Application.isPlaying) { Destroy(obj); }
            else { DestroyImmediate(obj, true); } // Allow asset destruction as fallback
        }

        void CreateContainerStructure()
        {
            if (enableScrolling) { CreateScrollingStructure(); }
            else { CreateStaticStructure(); }
        }

        void CreateStaticStructure()
        {
            // Root Container
            var contentObj = new GameObject("Content [Generated]") { hideFlags = HideFlags.DontSave };
            contentObj.transform.SetParent(transform, false);
            contentContainer = contentObj.AddComponent<RectTransform>();
            SetupFullRectTransform(contentContainer);

            // Header
            var headerObj = new GameObject("Header") { hideFlags = HideFlags.DontSave };
            headerObj.transform.SetParent(contentContainer, false);
            headerContainer = headerObj.AddComponent<RectTransform>();
            SetupHeaderContainer(false);

            // Rows
            var rowContainerObj = new GameObject("Rows") { hideFlags = HideFlags.DontSave };
            rowContainerObj.transform.SetParent(contentContainer, false);
            rowContainer = rowContainerObj.AddComponent<RectTransform>();
            SetupRowContainer(false);
        }

        void CreateScrollingStructure()
        {
            // Header (Pinned to top of ListView)
            var headerObj = new GameObject("Header") { hideFlags = HideFlags.DontSave };
            headerObj.transform.SetParent(transform, false);
            headerContainer = headerObj.AddComponent<RectTransform>();
            SetupHeaderContainer(true);

            // Scroll View (Fills remaining space below header)
            var scrollObj = new GameObject("Scroll View") { hideFlags = HideFlags.DontSave };
            scrollObj.transform.SetParent(transform, false);

            var scrollRT = scrollObj.AddComponent<RectTransform>();

            // Pivot -> Anchors -> Offsets
            scrollRT.pivot = new Vector2(0.5f, 0f); // Bottom Pivot
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one; // Stretch
            scrollRT.offsetMin = Vector2.zero; // Bottom 0 ("Zero the bottom")
            scrollRT.offsetMax = new Vector2(0, -style.headerHeight - style.rowSpacing); // Top offset by header + row spacing

            // Add ScrollRect
            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = scrollSensitivity;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = false;
            scrollRect.verticalScrollbar = verticalScrollbar;
            if (verticalScrollbar != null)
            {
                scrollRect.viewport = scrollRT;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            // Raycast Target for dragging
            var img = scrollObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;

            // Viewport (Mask)
            var viewportObj = new GameObject("Viewport") { hideFlags = HideFlags.DontSave };
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewportRT = viewportObj.AddComponent<RectTransform>();
            SetupFullRectTransform(viewportRT); // Fill ScrollView

            // Add Mask
            var maskImg = viewportObj.AddComponent<Image>();
            maskImg.color = Color.clear;
            var mask = viewportObj.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0, 0, 0, 0);

            // Assign Viewport
            scrollRect.viewport = viewportRT;

            // Row Container (Content)
            var rowContainerObj = new GameObject("Rows") { hideFlags = HideFlags.DontSave };
            rowContainerObj.transform.SetParent(viewportObj.transform, false);
            rowContainer = rowContainerObj.AddComponent<RectTransform>();

            // Setup Row Container (Top Anchor, grows down)
            rowContainer.anchorMin = new Vector2(0, 1);
            rowContainer.anchorMax = new Vector2(1, 1);
            rowContainer.pivot = new Vector2(0, 1);
            rowContainer.sizeDelta = Vector2.zero; // Height controlled by script
            rowContainer.anchoredPosition = Vector2.zero;

            // Assign Content
            scrollRect.content = rowContainer;

            // Set contentContainer ref for internal logic compatibility
            contentContainer = scrollRT;
        }

        void SetupFullRectTransform(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void SetupHeaderContainer(bool isScrolling)
        {
            headerContainer.anchorMin = new Vector2(0, 1);
            headerContainer.anchorMax = new Vector2(1, 1);
            headerContainer.pivot = new Vector2(0, 1);
            headerContainer.sizeDelta = new Vector2(0, style.headerHeight);
            headerContainer.anchoredPosition = Vector2.zero;
        }

        void SetupRowContainer(bool isScrolling)
        {
            rowContainer.anchorMin = new Vector2(0, 0); // Stretch X
            rowContainer.anchorMax = new Vector2(1, 1);
            rowContainer.pivot = new Vector2(0, 1);
            rowContainer.offsetMin = new Vector2(0, 0);
            // If scrolling, the ScrollView offset handles the spacing, so rows start at 0 relative to viewport
            rowContainer.offsetMax = new Vector2(0, isScrolling ? 0 : -style.headerHeight - style.rowSpacing);
        }

        void UpdateHashes((int column, int row, int layout, int style) hashes)
        {
            columnDataHash = hashes.column;
            rowDataHash = hashes.row;
            layoutPropertiesHash = hashes.layout;
            stylePropertiesHash = hashes.style;
        }

        void ResetChangeTracking()
        {
            columnDataHash = 0;
            rowDataHash = 0;
            layoutPropertiesHash = 0;
            stylePropertiesHash = 0;
            layoutCacheDirty = true;
        }

        void FullRebuild()
        {
            CleanupListViewContent();
            InvalidateLayoutCache();
            ResetChangeTracking();

            CreateHeader();
            CreateRows();

            UpdateHeaderContent();
            UpdateRowContent();
            UpdateParentHeight();

            isDirty = false;
        }

        void SmartUpdateContent()
        {
            bool needsPositionUpdate = false;
            if (rowObjects.Count > 0 && rowObjects[0].Count > 0)
            {
                var firstCell = rowObjects[0][0].GetComponent<RectTransform>();
                if (firstCell != null && firstCell.sizeDelta.y != style.rowHeight) { needsPositionUpdate = true; }
            }

            if (headerObjects.Count > 0)
            {
                var firstHeader = headerObjects[0].GetComponent<RectTransform>();
                if (firstHeader != null && firstHeader.sizeDelta.y != style.headerHeight) { needsPositionUpdate = true; }
            }

            if (needsPositionUpdate) { UpdateCellHeightsAndPositions(); }
            UpdateHeaderContent();
            UpdateRowContent();
            UpdateParentHeight();

            isDirty = false;
        }

        void UpdateCellHeightsAndPositions()
        {
            if (headerContainer != null)
            {
                headerContainer.sizeDelta = new Vector2(0, style.headerHeight);

                foreach (var headerObj in headerObjects)
                {
                    if (headerObj != null)
                    {
                        if (headerObj.TryGetComponent<RectTransform>(out var headerRect))
                            headerRect.sizeDelta = new Vector2(headerRect.sizeDelta.x, style.headerHeight);
                    }
                }

                if (enableScrolling && scrollRect != null)
                {
                    // Ensure ScrollView offset updates if Header Height or Row Spacing changes
                    if (scrollRect.TryGetComponent<RectTransform>(out var scrollRT))
                        scrollRT.offsetMax = new Vector2(0, -style.headerHeight - style.rowSpacing);
                }
                else if (rowContainer != null)
                {
                    rowContainer.offsetMax = new Vector2(0, -style.headerHeight - style.rowSpacing);
                }
            }

            for (int rowIndex = 0; rowIndex < rowObjects.Count; rowIndex++)
            {
                var rowCells = rowObjects[rowIndex];
                float currentY = rowIndex * (style.rowHeight + style.rowSpacing);

                foreach (var cellObj in rowCells)
                {
                    if (cellObj != null)
                    {
                        if (cellObj.TryGetComponent<RectTransform>(out var cellRect))
                        {
                            cellRect.sizeDelta = new Vector2(cellRect.sizeDelta.x, style.rowHeight);
                            Vector2 currentPos = cellRect.anchoredPosition;
                            cellRect.anchoredPosition = new Vector2(currentPos.x, -currentY);
                        }
                    }
                }
            }
        }

        void UpdateContentOnly()
        {
            UpdateRowContent();
            isDirty = false;
        }

        float CalculateRowsHeight()
        {
            float height = 0f;
            if (rows.Count > 0)
            {
                height += rows.Count * style.rowHeight;
                height += (rows.Count - 1) * style.rowSpacing;
                // Add bottom padding just in case
                height += style.rowSpacing;
            }
            return height;
        }

        float CalculateTotalHeight()
        {
            float totalHeight = style.headerHeight;
            if (rows.Count > 0) totalHeight += style.rowSpacing;
            totalHeight += CalculateRowsHeight();
            return totalHeight;
        }

        void UpdateParentHeight()
        {
            float rowsHeight = CalculateRowsHeight();

            if (enableScrolling)
            {
                // When scrolling, we only resize the Row Container (ScrollRect Content)
                // The parentRect (ListView itself) remains fixed size as defined by user in Editor
                if (rowContainer != null)
                    rowContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowsHeight);

                // Update scroll sensitivity if changed at runtime
                if (scrollRect != null)
                {
                    scrollRect.scrollSensitivity = scrollSensitivity;
                    scrollRect.verticalScrollbar = verticalScrollbar;
                }
            }
            else
            {
                // Classic mode: Resize the parent to fit everything
                if (parentRect == null)
                    return;

                float totalHeight = CalculateTotalHeight();
                if (Mathf.Abs(cachedTotalHeight - totalHeight) > 0.01f)
                {
                    cachedTotalHeight = totalHeight;
                    parentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
                }
            }
        }

        void InvalidateLayoutCache()
        {
            layoutCacheDirty = true;
            cachedColumnWidths = null;
        }

        void RecalculateColumnWidths()
        {
            if (columns.Count == 0)
                return;

            cachedColumnWidths = new float[columns.Count];
            RectTransform target = headerContainer ? headerContainer : rowContainer;

            float containerWidth = target ? target.rect.width : 0f;
            float totalWidth = containerWidth - (style.columnSpacing * (columns.Count - 1));
            float totalFixedWidth = 0f;
            int flexibleCount = 0;

            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].useFlexibleWidth) flexibleCount++;
                else
                {
                    cachedColumnWidths[i] = columns[i].width;
                    totalFixedWidth += columns[i].width;
                }
            }

            float flexibleWidth = flexibleCount > 0 ? (totalWidth - totalFixedWidth) / flexibleCount : 0f;

            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].useFlexibleWidth)
                    cachedColumnWidths[i] = flexibleWidth;
            }
        }

        void CleanupListViewContent()
        {
            ClearObjectPool(headerObjects);

            foreach (var rowList in rowObjects)
                ClearObjectPool(rowList);

            rowObjects.Clear();

            CleanupContainer(headerContainer);
            CleanupContainer(rowContainer);
        }

        void CleanupContainer(RectTransform container)
        {
            if (container == null)
                return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child != null) DestroyGameObject(child.gameObject);
            }
        }

        void CreateHeader()
        {
            if (headerContainer == null || columns.Count == 0)
                return;

            var columnWidths = GetColumnWidths();
            float currentX = 0f;

            for (int i = 0; i < columns.Count; i++)
            {
                var headerCell = CreateHeaderCell(columns[i]);
                headerCell.transform.SetParent(headerContainer, false);
                PositionCell(headerCell, currentX, columnWidths[i], 0f, true);

                int colIndex = i;
                var button = headerCell.AddComponent<ListViewCellButton>();
                button.onClick.AddListener(() => onCellClicked?.Invoke(-1, colIndex));

                headerObjects.Add(headerCell);
                currentX += columnWidths[i] + style.columnSpacing;
            }
        }

        void CreateRows()
        {
            if (rowContainer == null || columns.Count == 0)
                return;

            var columnWidths = GetColumnWidths();
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var rowCells = CreateRowCells(rowIndex, columnWidths);
                rowObjects.Add(rowCells);
            }
        }

        void PositionCell(GameObject cell, float x, float width, float y, bool isHeader = false)
        {
            var cellRect = cell.GetComponent<RectTransform>();
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);
            cellRect.sizeDelta = new Vector2(width, isHeader ? style.headerHeight : style.rowHeight);
            cellRect.anchoredPosition = new Vector2(x, -y);
        }

        void SetContentPivot(RectTransform contentRect, TextAnchor alignment)
        {
            Vector2 pivot = new(0.5f, 0.5f);

            if (IsLeftAligned(alignment)) { pivot.x = 0f; }
            else if (IsRightAligned(alignment)) { pivot.x = 1f; }

            contentRect.pivot = pivot;
        }

        void AddIconToContent(GameObject content, Sprite icon, Color color)
        {
            var iconObj = new GameObject("Icon") { hideFlags = HideFlags.DontSave };
            iconObj.transform.SetParent(content.transform, false);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = color;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;

            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(style.iconSize, style.iconSize);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            var iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = style.iconSize;
            iconLayout.preferredHeight = style.iconSize;
        }

        void AddTextToContent(GameObject content, string text, Color color, int fontSize, FontStyles fontStyle,
            TMP_FontAsset font = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var textObj = new GameObject("Text") { hideFlags = HideFlags.DontSave };
            textObj.transform.SetParent(content.transform, false);

            var textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.color = color;
            textComponent.font = font != null ? font : style.rowFont;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = ToTMPAlignment(alignment);
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.overflowMode = TextOverflowModes.Overflow;
#if UNITY_6000_0_OR_NEWER
            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
#else
            textComponent.enableWordWrapping = false;
#endif

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);

            var textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;
            textLayout.flexibleHeight = 0;
        }

        void UpdateHeaderContent()
        {
            if (headerObjects.Count != columns.Count)
                return;

            for (int i = 0; i < headerObjects.Count; i++)
            {
                var headerBgColor = GetStyleColor(ListViewStyle.Type.HeaderBackgroundColor, style.headerBackgroundColor);
                var headerTextColor = GetStyleColor(ListViewStyle.Type.HeaderTextColor, style.headerTextColor);
                var headerFont = GetStyleFont(ListViewStyle.Type.HeaderFont, style.headerFont);
                UpdateCellContent(headerObjects[i], columns[i], columns[i].columnName, columns[i].columnIcon, null, style.iconSize,
                    headerBgColor, headerTextColor, style.headerFontSize, style.headerFontStyle, headerFont);
            }
        }

        void UpdateRowContent()
        {
            for (int rowIndex = 0; rowIndex < rowObjects.Count && rowIndex < rows.Count; rowIndex++)
            {
                var rowCells = rowObjects[rowIndex];
                var row = rows[rowIndex];
                for (int colIndex = 0; colIndex < rowCells.Count && colIndex < columns.Count; colIndex++)
                {
                    string cellValue = colIndex < row.values.Count ? row.values[colIndex] : "";
                    Sprite cellIcon = row.GetIcon(colIndex);
                    GameObject customObj = row.GetCustomObject(colIndex);
                    Color bgColor = GetRowBackgroundColor(rowIndex);
                    var rowTextColor = GetStyleColor(ListViewStyle.Type.RowTextColor, style.rowTextColor);
                    var rowFont = GetStyleFont(ListViewStyle.Type.RowFont, style.rowFont);
                    UpdateCellContent(rowCells[colIndex], columns[colIndex], cellValue, cellIcon, customObj, style.iconSize,
                        bgColor, rowTextColor, style.rowFontSize, style.rowFontStyle, rowFont);
                }
            }
        }

        void UpdateCellContent(GameObject cell, ListViewColumn column, string text, Sprite icon, GameObject customObject,
            float iconSize, Color backgroundColor, Color textColor, int fontSize, FontStyles fontStyle, TMP_FontAsset font = null)
        {
            if (cell.TryGetComponent<Image>(out var bgImage))
            {
                bgImage.color = backgroundColor;

                if (style.backgroundSprite != null)
                {
                    bgImage.sprite = style.backgroundSprite;
                    bgImage.type = Image.Type.Sliced;
                    bgImage.pixelsPerUnitMultiplier = style.ppuMultiplier;
                }
                else
                {
                    bgImage.sprite = null;
                    bgImage.type = Image.Type.Simple;
                }
            }

            var outline = cell.GetComponent<Outline>();
            if (style.showBorder)
            {
                var borderColor = GetStyleColor(ListViewStyle.Type.BorderColor, style.borderColor);
                if (outline == null) { outline = cell.AddComponent<Outline>(); }
                outline.effectColor = borderColor;
                outline.effectDistance = new Vector2(style.borderWidth, style.borderWidth);
                outline.enabled = true;
            }
            else if (outline != null) outline.enabled = false;

            var content = cell.transform.Find("Content");
            if (content == null) { return; }

            var contentRect = content.GetComponent<RectTransform>();
            var layoutGroup = content.GetComponent<HorizontalLayoutGroup>();
            var fitter = content.GetComponent<ContentSizeFitter>();

            if (layoutGroup)
            {
                layoutGroup.childAlignment = column.alignment;
                layoutGroup.padding = style.contentPadding;
            }
            if (contentRect) { SetContentPivot(contentRect, column.alignment); }

            // Child Management
            var customWrapper = content.Find("CustomObjectWrapper");
            var iconObj = content.Find("Icon");
            var textObj = content.Find("Text");

            if (customObject != null)
            {
                if (fitter) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                if (customWrapper == null)
                {
                    var wrapper = new GameObject("CustomObjectWrapper") { hideFlags = HideFlags.DontSave };
                    wrapper.transform.SetParent(content.transform, false);
                    var wrapperRT = wrapper.AddComponent<RectTransform>();
                    wrapperRT.anchorMin = Vector2.zero;
                    wrapperRT.anchorMax = Vector2.one;
                    wrapperRT.offsetMin = Vector2.zero;
                    wrapperRT.offsetMax = Vector2.zero;
                    var le = wrapper.AddComponent<LayoutElement>();
                    le.flexibleWidth = 1;
                    le.flexibleHeight = 1;
                    customWrapper = wrapper.transform;
                }
                if (iconObj) { iconObj.gameObject.SetActive(false); }
                if (textObj) { textObj.gameObject.SetActive(false); }

                customWrapper.gameObject.SetActive(true);

                // Instance logic
                bool needsInstance = customWrapper.childCount == 0 || customWrapper.GetChild(0).name != customObject.name + "_Instance";
                if (needsInstance)
                {
                    for (int i = customWrapper.childCount - 1; i >= 0; i--) DestroyGameObject(customWrapper.GetChild(i).gameObject);
                    var instance = Instantiate(customObject, customWrapper, false);
                    instance.name = customObject.name + "_Instance";
                    if (instance.TryGetComponent<RectTransform>(out var instanceRT))
                    {
                        instanceRT.anchorMin = Vector2.zero;
                        instanceRT.anchorMax = Vector2.one;
                        instanceRT.offsetMin = Vector2.zero;
                        instanceRT.offsetMax = Vector2.zero;
                    }
                }
            }
            else
            {
                if (fitter) fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                if (customWrapper) customWrapper.gameObject.SetActive(false);

                // Handle Icon creation/update
                if (icon != null)
                {
                    if (iconObj == null) { AddIconToContent(content.gameObject, icon, textColor); iconObj = content.Find("Icon"); }
                    iconObj.gameObject.SetActive(true);
                    if (iconObj.TryGetComponent<Image>(out var img)) { img.sprite = icon; img.color = textColor; }
                }
                else if (iconObj) iconObj.gameObject.SetActive(false);

                // Handle Text creation/update
                if (textObj == null)
                {
                    AddTextToContent(content.gameObject, text, textColor, fontSize, fontStyle, font, column.alignment);
                    textObj = content.Find("Text");
                }
                textObj.gameObject.SetActive(true);
                if (textObj.TryGetComponent<TextMeshProUGUI>(out var tmp))
                {
                    tmp.text = text;
                    tmp.color = textColor;
                    tmp.font = font != null ? font : style.rowFont;
                    tmp.fontSize = fontSize;
                    tmp.fontStyle = fontStyle;
                    tmp.alignment = ToTMPAlignment(column.alignment);
                }
            }
        }

        bool NeedsFullRebuild((int column, int row, int layout, int style) hashes)
        {
            return hashes.column != columnDataHash || hashes.layout != layoutPropertiesHash || rows.Count != rowObjects.Count || columns.Count != headerObjects.Count || layoutCacheDirty;
        }

        bool HasStyleChanges((int column, int row, int layout, int style) hashes)
        {
            return hashes.style != stylePropertiesHash || hashes.row != rowDataHash || hashes.column != columnDataHash;
        }

        bool IsValidCellIndex(int rowIndex, int columnIndex) => rowIndex >= 0 && rowIndex < rows.Count && columnIndex >= 0 && columnIndex < columns.Count;
        bool IsLeftAligned(TextAnchor alignment) => alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.UpperLeft || alignment == TextAnchor.LowerLeft;
        bool IsRightAligned(TextAnchor alignment) => alignment == TextAnchor.MiddleRight || alignment == TextAnchor.UpperRight || alignment == TextAnchor.LowerRight;

        int HashColumns()
        {
            int hash = columns.Count;
            foreach (var col in columns)
            {
                hash = hash * 31 + (col.columnName?.GetHashCode() ?? 0);
                hash = hash * 31 + col.width.GetHashCode();
                hash = hash * 31 + col.useFlexibleWidth.GetHashCode();
                hash = hash * 31 + col.alignment.GetHashCode();
                hash = hash * 31 + (col.columnIcon != null ? col.columnIcon.GetHashCode() : 0);
            }
            return hash;
        }

        int HashRows()
        {
            int hash = rows.Count;
            foreach (var row in rows)
            {
                hash = hash * 31 + row.values.Count;
                foreach (var value in row.values) { hash = hash * 31 + (value?.GetHashCode() ?? 0); }

                hash = hash * 31 + row.icons.Count;
                foreach (var icon in row.icons) { hash = hash * 31 + (icon != null ? icon.GetHashCode() : 0); }

                hash = hash * 31 + row.customObjects.Count;
                foreach (var obj in row.customObjects) { hash = hash * 31 + (obj != null ? obj.GetHashCode() : 0); }
            }
            return hash;
        }

        int HashLayoutProperties() => style.columnSpacing.GetHashCode() * 31 + style.rowSpacing.GetHashCode() * 31 + style.contentSpacing.GetHashCode() * 31;

        (int column, int row, int layout, int style) CalculateCurrentHashes()
        {
            int colH = HashColumns();
            int rowH = HashRows();
            int layH = HashLayoutProperties();
            int styH = style.GetHashCode() * 31 + (int)stylingSource * 31 + (stylerPreset != null ? stylerPreset.GetHashCode() : 0);
            return (colH, rowH, layH, styH);
        }

        float[] GetColumnWidths()
        {
            if (layoutCacheDirty || cachedColumnWidths == null) { RecalculateColumnWidths(); layoutCacheDirty = false; }
            return cachedColumnWidths;
        }

        Color GetRowBackgroundColor(int rowIndex)
        {
            if (stylingSource == ListViewStyle.StylingSource.StylerPreset && stylerPreset != null)
            {
                var mapping = GetStyleMapping(ListViewStyle.Type.AlternatingRowColor);

                if (style.useAlternatingRowColor && rowIndex % 2 == 1 && !string.IsNullOrEmpty(mapping?.colorID))
                    return stylerPreset.GetColor(mapping.colorID);

                var rowBgMapping = GetStyleMapping(ListViewStyle.Type.RowBackgroundColor);

                if (!string.IsNullOrEmpty(rowBgMapping?.colorID))
                    return stylerPreset.GetColor(rowBgMapping.colorID);

                return Color.clear;
            }

            return style.useAlternatingRowColor && rowIndex % 2 == 1 ? style.alternatingRowColor : style.rowBackgroundColor;
        }

        Color GetStyleColor(ListViewStyle.Type styleType, Color fallback)
        {
            if (stylingSource == ListViewStyle.StylingSource.StylerPreset && stylerPreset != null)
            {
                var mapping = GetStyleMapping(styleType);

                if (!string.IsNullOrEmpty(mapping?.colorID))
                    return stylerPreset.GetColor(mapping.colorID);

                return Color.clear;
            }
            return fallback;
        }

        TMP_FontAsset GetStyleFont(ListViewStyle.Type styleType, TMP_FontAsset fallback)
        {
            if (stylingSource == ListViewStyle.StylingSource.StylerPreset && stylerPreset != null)
            {
                var mapping = GetStyleMapping(styleType);

                if (!string.IsNullOrEmpty(mapping?.fontID))
                    return stylerPreset.GetFont(mapping.fontID);
            }

            return fallback;
        }

        ListViewStyle.Mapping GetStyleMapping(ListViewStyle.Type styleType)
        {
            foreach (var mapping in styleMapping)
            {
                if (mapping.type == styleType)
                    return mapping;
            }

            return null;
        }

        GameObject CreateHeaderCell(ListViewColumn column)
        {
            var cell = CreateBaseCell($"Header {column.columnName}", style.headerBackgroundColor);
            CreateCellContent(cell, column.alignment);
            return cell;
        }

        GameObject CreateRowCell(ListViewColumn column, string value, Sprite icon, GameObject customObject, Color backgroundColor)
        {
            var cell = CreateBaseCell($"Cell {column.columnName}", backgroundColor);
            CreateCellContent(cell, column.alignment);
            return cell;
        }

        GameObject CreateBaseCell(string name, Color backgroundColor)
        {
            var cell = new GameObject(name) { hideFlags = HideFlags.DontSave };
            var bgImage = cell.AddComponent<Image>();
            bgImage.color = backgroundColor;

            if (style.backgroundSprite != null)
            {
                bgImage.sprite = style.backgroundSprite;
                bgImage.type = Image.Type.Sliced; bgImage.pixelsPerUnitMultiplier = style.ppuMultiplier;
            }

            if (style.showBorder)
            {
                var outline = cell.AddComponent<Outline>();
                outline.effectColor = style.borderColor;
                outline.effectDistance = new Vector2(style.borderWidth, style.borderWidth);
            }

            return cell;
        }

        GameObject CreateCellContent(GameObject parent, TextAnchor alignment)
        {
            var contentObj = new GameObject("Content") { hideFlags = HideFlags.DontSave };
            contentObj.transform.SetParent(parent.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            SetupFullRectTransform(contentRect);
            SetContentPivot(contentRect, alignment);
            var layout = contentObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = style.contentSpacing;
            layout.padding = style.contentPadding;
            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return contentObj;
        }

        List<GameObject> CreateRowCells(int rowIndex, float[] columnWidths)
        {
            var rowCells = new List<GameObject>(columns.Count);
            var row = rows[rowIndex];
            float currentX = 0f;
            float currentY = rowIndex * (style.rowHeight + style.rowSpacing);
            for (int colIndex = 0; colIndex < columns.Count; colIndex++)
            {
                var rowCell = CreateRowCell(columns[colIndex], "", null, null, Color.white);
                rowCell.transform.SetParent(rowContainer, false);
                PositionCell(rowCell, currentX, columnWidths[colIndex], currentY, false);

                int rIndex = rowIndex;
                int cIndex = colIndex;

                var button = rowCell.AddComponent<ListViewCellButton>();
                button.onClick.AddListener(() => onCellClicked?.Invoke(rIndex, cIndex));

                rowCells.Add(rowCell);
                currentX += columnWidths[colIndex] + style.columnSpacing;
            }
            return rowCells;
        }

        TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center,
            };
        }

        /// <summary>
        /// Wrapper for safe editor updates.
        /// </summary>
        void RequestRefresh()
        {
            if (this == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (pendingRedraw)
                    return;

                // Prevent rebuilding layout elements directly on Prefab Assets
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                    return;

                pendingRedraw = true;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    pendingRedraw = false;
                    if (this != null) { Refresh(); }
                };

                return;
            }
#endif
            Refresh();
        }

        public void Refresh()
        {
            if (this == null || gameObject == null)
                return;

            if (Application.isPlaying && !gameObject.activeInHierarchy)
                return;

#if UNITY_EDITOR
            // Stop Custom Editors from forcing a build directly onto the Prefab Asset in the Project Window
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                return;
#endif

            // Check if structure matches current mode (Scroll vs Static)
            bool isStructureValid = (enableScrolling && scrollRect != null) || (!enableScrolling && contentContainer != null && scrollRect == null);
            if (!isStructureValid)
            {
                Initialize();
                FullRebuild();
                return;
            }

            // Detect container size change
            RectTransform targetContainer = enableScrolling ? (RectTransform)transform : contentContainer;
            if (targetContainer != null)
            {
                Vector2 currentContainerSize = targetContainer.rect.size;
                if (currentContainerSize != lastContainerSize)
                {
                    InvalidateLayoutCache();
                    lastContainerSize = currentContainerSize;
                }
            }

            var hashes = CalculateCurrentHashes();

            if (NeedsFullRebuild(hashes)) { Initialize(); FullRebuild(); }
            else if (HasStyleChanges(hashes)) { SmartUpdateContent(); }
            else if (isDirty) { UpdateContentOnly(); }

            UpdateHashes(hashes);
        }

        public void AddColumn(ListViewColumn column)
        {
            columns.Add(column);
            InvalidateLayoutCache();
            isDirty = true;
            Refresh();
        }

        public void RemoveColumn(int index)
        {
            if (index < 0 || index >= columns.Count)
                return;

            columns.RemoveAt(index);
            foreach (var row in rows)
            {
                if (index < row.values.Count) { row.values.RemoveAt(index); }
                if (index < row.icons.Count) { row.icons.RemoveAt(index); }
                if (index < row.customObjects.Count) { row.customObjects.RemoveAt(index); }
            }

            InvalidateLayoutCache();
            isDirty = true;
            Refresh();
        }

        public void AddRow(ListViewRow row)
        {
            rows.Add(row);
            isDirty = true;
            Refresh();
        }

        public void AddRow(params string[] values)
        {
            rows.Add(new ListViewRow(values));
            isDirty = true;
            Refresh();
        }

        public void RemoveRow(int index)
        {
            if (index >= 0 && index < rows.Count)
            {
                rows.RemoveAt(index);
                isDirty = true;
                Refresh();
            }
        }

        public void ClearRows()
        {
            rows.Clear();
            isDirty = true;
            Refresh();
        }

        public void SetCellValue(int rowIndex, int columnIndex, string value)
        {
            if (IsValidCellIndex(rowIndex, columnIndex))
            {
                rows[rowIndex].SetCell(columnIndex, value);
                isDirty = true;
                Refresh();
            }
        }

        public void SetCellData(int rowIndex, int columnIndex, string value, Sprite icon = null, GameObject customObject = null)
        {
            if (IsValidCellIndex(rowIndex, columnIndex))
            {
                rows[rowIndex].SetCell(columnIndex, value, icon, customObject);
                isDirty = true;
                Refresh();
            }
        }

        public string GetCellValue(int rowIndex, int columnIndex) => IsValidCellIndex(rowIndex, columnIndex)
            ? (columnIndex < rows[rowIndex].values.Count ? rows[rowIndex].values[columnIndex] : null) : null;

        public void UseAlternatingRowColor(bool value)
        {
            style.useAlternatingRowColor = value;
            Refresh();
        }

        public GameObject GetCellCustomInstance(int rowIndex, int columnIndex)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex)) { return null; }
            if (rowIndex >= rowObjects.Count || columnIndex >= rowObjects[rowIndex].Count) { return null; }

            var cell = rowObjects[rowIndex][columnIndex];
            if (!cell) { return null; }

            var content = cell.transform.Find("Content");
            if (!content) { return null; }

            var wrapper = content.Find("CustomObjectWrapper");
            if (!wrapper || wrapper.childCount == 0) { return null; }

            return wrapper.GetChild(0).gameObject;
        }

        public T GetCellCustomInstance<T>(int rowIndex, int columnIndex) where T : Component
        {
            var obj = GetCellCustomInstance(rowIndex, columnIndex);
            return obj != null ? obj.GetComponent<T>() : null;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool autoRefresh = true;
        [HideInInspector] public bool groupFoldout = true;
        [HideInInspector] public bool rowFoldout = true;
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;

        // Draw safety
        bool pendingRedraw = false;

        void OnValidate()
        {
            InitializeStyleMappings();
            EnsureRowDataConsistency();
            isDirty = true;

            // Use safe wrapper instead of direct Refresh or passive isDirty
            if (gameObject.activeInHierarchy && !Application.isPlaying) { RequestRefresh(); }
        }

        void EnsureRowDataConsistency()
        {
            int colCount = columns.Count;
            foreach (var row in rows)
            {
                while (row.values.Count < colCount) { row.values.Add(""); }
                if (row.values.Count > colCount) { row.values.RemoveRange(colCount, row.values.Count - colCount); }

                while (row.icons.Count < colCount) { row.icons.Add(null); }
                if (row.icons.Count > colCount) { row.icons.RemoveRange(colCount, row.icons.Count - colCount); }

                while (row.customObjects.Count < colCount) { row.customObjects.Add(null); }
                if (row.customObjects.Count > colCount) { row.customObjects.RemoveRange(colCount, row.customObjects.Count - colCount); }
            }
        }
#endif
    }
}