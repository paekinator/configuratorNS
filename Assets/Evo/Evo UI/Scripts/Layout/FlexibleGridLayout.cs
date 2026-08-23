using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// A flexible grid layout system that supports standard grid structures, 
    /// auto-fitting (responsive design), and masonry (waterfall) layouts.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "layout/flexible-grid-layout")]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Flexible Grid Layout (Preview)")]
    public class FlexibleGridLayout : LayoutGroup
    {
        [EvoHeader("Grid Settings", Constants.CustomEditorID)]
        [Tooltip("Determines how rows and columns are calculated.")]
        public FitType fitType = FitType.AutoFit;
        [SerializeField, Min(1)] private int rows = 2;
        [SerializeField, Min(1)] private int columns = 2;

        [EvoHeader("Cell Sizing & Spacing", Constants.CustomEditorID)]
        [Tooltip("The gap space between cells.")]
        [SerializeField] private Vector2 spacing = new(10, 10);
        [Tooltip("The minimum dimensions for each cell. Used strongly in AutoFit.")]
        [SerializeField] private Vector2 minCellSize = new(100, 100);

        [EvoHeader("Masonry / Flow", Constants.CustomEditorID)]
        [Tooltip("If true, items push down only the items directly below them in the same column (Waterfall style).")]
        [SerializeField] private bool masonry = false;

        [EvoHeader("Child Control Size", Constants.CustomEditorID)]
        [Tooltip("Forces children to stretch to the cell width.")]
        [SerializeField] private bool controlChildWidth = true;
        [Tooltip("Forces children to stretch to the cell height.")]
        [SerializeField] private bool controlChildHeight = true;

        [EvoHeader("Stretch Settings", Constants.CustomEditorID)]
        [Tooltip("Allows items to grow slightly to fill remaining side space perfectly.")]
        [SerializeField] private bool fitX = false;
        [Tooltip("Allows items to grow vertically to fill remaining space.")]
        [SerializeField] private bool fitY = false;

        [EvoHeader("Aspect Ratio", Constants.CustomEditorID)]
        [Tooltip("Forces cell heights to scale proportionately with widths.")]
        [SerializeField] private bool preserveAspectRatio = false;
        [Tooltip("Width divided by Height (1.0 is square. > 1 is wide. < 1 is tall).")]
        [SerializeField] private float aspectRatio = 1f;

        public enum FitType
        {
            Uniform,        // Stays perfectly square (e.g., 2x2, 3x3)
            Width,          // Fits a specific number of columns, rows expand downwards
            Height,         // Fits a specific number of rows, columns expand sidewards
            FixedRows,      // Locks the exact row count
            FixedColumns,   // Locks the exact column count
            AutoFit         // Modern behavior: Uses MinCellSize to calculate columns, stretches to fill
        }

        // Runtime cached layout data
        Vector2 cellSize;
        float[] rowHeights = new float[0];

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            int childCount = rectChildren.Count;
            if (childCount == 0) { return; }

            // Calculate the foundational cell size based on settings
            Vector2 baseCellSize = minCellSize;

            // Determine Rows and Columns structurally
            switch (fitType)
            {
                case FitType.Uniform:
                    fitX = true;
                    fitY = true;
                    float sqrRt = Mathf.Sqrt(childCount);
                    rows = Mathf.CeilToInt(sqrRt);
                    columns = Mathf.CeilToInt(sqrRt);
                    break;

                case FitType.Width:
                case FitType.FixedColumns:
                    rows = Mathf.CeilToInt(childCount / (float)columns);
                    break;

                case FitType.Height:
                case FitType.FixedRows:
                    columns = Mathf.CeilToInt(childCount / (float)rows);
                    break;

                case FitType.AutoFit:
                    float width = rectTransform.rect.width;
                    float availWidth = width - padding.left - padding.right;
                    columns = Mathf.FloorToInt((availWidth + spacing.x) / (baseCellSize.x + spacing.x));
                    columns = Mathf.Max(1, columns);
                    rows = Mathf.CeilToInt(childCount / (float)columns);
                    break;
            }

            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);

            // Finalize Horizontal Cell Dimensions
            float parentWidth = rectTransform.rect.width;
            if (fitX) { cellSize.x = (parentWidth - padding.left - padding.right - spacing.x * (columns - 1)) / (float)columns; }
            else { cellSize.x = baseCellSize.x; }
        }

        public override void CalculateLayoutInputVertical()
        {
            float totalHeight = padding.top + padding.bottom;

            if (masonry)
            {
                // Calculate height based on the tallest independent column
                float[] colHeights = new float[columns];
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    int col = i % columns;
                    int row = i / columns;

                    if (row > 0) { colHeights[col] += spacing.y; }
                    colHeights[col] += GetChildHeight(rectChildren[i]);
                }

                float maxColHeight = 0;
                for (int i = 0; i < columns; i++) { maxColHeight = Mathf.Max(maxColHeight, colHeights[i]); }
                totalHeight += maxColHeight;
            }
            else
            {
                // Calculate height based on standard row heights
                UpdateRowHeights();
                float contentHeight = spacing.y * Mathf.Max(0, rows - 1);
                for (int i = 0; i < rows; i++) { contentHeight += rowHeights[i]; }
                totalHeight += contentHeight;
            }

            // ContentSizeFitter support
            SetLayoutInputForAxis(totalHeight, totalHeight, -1, 1);
        }

        float GetChildHeight(RectTransform child)
        {
            float childHeight = minCellSize.y;

            if (preserveAspectRatio) { childHeight = cellSize.x / aspectRatio; }
            else if (fitY)
            {
                float parentHeight = rectTransform.rect.height;
                childHeight = (parentHeight - padding.top - padding.bottom - spacing.y * (rows - 1)) / (float)rows;
            }
            else if (!controlChildHeight) { childHeight = child.rect.height; }

            return Mathf.Max(0, childHeight);
        }

        void UpdateRowHeights()
        {
            // Resizing only when row counts change
            if (rowHeights == null || rows != rowHeights.Length) { rowHeights = new float[rows]; }
            else { System.Array.Clear(rowHeights, 0, rowHeights.Length); }

            for (int i = 0; i < rectChildren.Count; i++)
            {
                int rowCount = i / columns;
                if (rowCount >= rows) { continue; }

                float childHeight = GetChildHeight(rectChildren[i]);
                rowHeights[rowCount] = Mathf.Max(rowHeights[rowCount], childHeight);
            }
        }

        void SetCellsAlongAxis(int axis)
        {
            if (rectChildren.Count == 0)
                return;

            float requiredWidth = (cellSize.x * columns) + (spacing.x * (columns - 1));
            float requiredHeight = 0;

            float[] colYOffsets = null;
            float[] rowYPositions = null;

            if (masonry)
            {
                // Precalculate column heights to align the whole masonry block properly
                float[] colHeights = new float[columns];
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    if (row > 0) { colHeights[col] += spacing.y; }
                    colHeights[col] += GetChildHeight(rectChildren[i]);
                }

                for (int i = 0; i < columns; i++) { requiredHeight = Mathf.Max(requiredHeight, colHeights[i]); }

                float startOffsetY = GetStartOffset(1, requiredHeight);
                colYOffsets = new float[columns];
                for (int i = 0; i < columns; i++) { colYOffsets[i] = startOffsetY; }
            }
            else
            {
                // Unity async layouts can skip updates if only horizontal is dirtied
                if (rowHeights == null || rowHeights.Length != rows) { UpdateRowHeights(); }

                requiredHeight = spacing.y * Mathf.Max(0, rows - 1);
                for (int i = 0; i < rows; i++) { requiredHeight += rowHeights[i]; }

                float startOffsetY = GetStartOffset(1, requiredHeight);
                rowYPositions = new float[rows];
                float currentY = startOffsetY;
                for (int i = 0; i < rows; i++)
                {
                    rowYPositions[i] = currentY;
                    currentY += rowHeights[i] + spacing.y;
                }
            }

            float startOffsetX = GetStartOffset(0, requiredWidth);

            for (int i = 0; i < rectChildren.Count; i++)
            {
                int rowCount = i / columns;
                if (!masonry && rowCount >= rows) { continue; }

                int columnCount = i % columns;
                var item = rectChildren[i];

                float xPos = startOffsetX + (cellSize.x * columnCount) + (spacing.x * columnCount);

                float yPos;
                float currentCellHeight;

                if (masonry)
                {
                    yPos = colYOffsets[columnCount];
                    currentCellHeight = GetChildHeight(item);

                    // Push offset down specifically for this column
                    colYOffsets[columnCount] += currentCellHeight + spacing.y;
                }
                else
                {
                    yPos = rowYPositions[rowCount];
                    currentCellHeight = rowHeights[rowCount];
                }

                if (axis == 0)
                {
                    if (controlChildWidth) { SetChildAlongAxis(item, 0, xPos, cellSize.x); }
                    else
                    {
                        float alignOffset = GetAlignmentOffset(0, cellSize.x, item.rect.width);
                        SetChildAlongAxis(item, 0, xPos + alignOffset);
                    }
                }
                else
                {
                    if (controlChildHeight) { SetChildAlongAxis(item, 1, yPos, currentCellHeight); }
                    else
                    {
                        float alignOffset = GetAlignmentOffset(1, currentCellHeight, item.rect.height);
                        SetChildAlongAxis(item, 1, yPos + alignOffset);
                    }
                }
            }
        }

        float GetAlignmentOffset(int axis, float cellSize, float itemSize)
        {
            int alignment = (int)childAlignment;
            if (axis == 0)
            {
                int horizontalAlign = alignment % 3;
                if (horizontalAlign == 0) { return 0; } // Left
                if (horizontalAlign == 1) { return (cellSize - itemSize) * 0.5f; } // Center
                return cellSize - itemSize; // Right
            }
            else
            {
                int verticalAlign = alignment / 3;
                if (verticalAlign == 0) { return 0; } // Upper
                if (verticalAlign == 1) { return (cellSize - itemSize) * 0.5f; } // Middle
                return cellSize - itemSize; // Lower
            }
        }

        public override void SetLayoutHorizontal()
        {
            SetCellsAlongAxis(0);
        }

        public override void SetLayoutVertical()
        {
            SetCellsAlongAxis(1);
        }

        #region Properties & Public Methods
        public void SetFitType(FitType type)
        {
            if (fitType != type)
            {
                fitType = type;
                SetDirty();
            }
        }

        public int Rows
        {
            get { return rows; }
            set
            {
                int clampedValue = Mathf.Max(1, value);
                if (rows != clampedValue)
                {
                    rows = clampedValue;
                    SetDirty();
                }
            }
        }

        public int Columns
        {
            get { return columns; }
            set
            {
                int clampedValue = Mathf.Max(1, value);
                if (columns != clampedValue)
                {
                    columns = clampedValue;
                    SetDirty();
                }
            }
        }

        public Vector2 MinCellSize
        {
            get { return minCellSize; }
            set
            {
                if (minCellSize != value)
                {
                    minCellSize = value;
                    SetDirty();
                }
            }
        }

        public Vector2 Spacing
        {
            get { return spacing; }
            set
            {
                if (spacing != value)
                {
                    spacing = value;
                    SetDirty();
                }
            }
        }

        public bool ControlChildWidth
        {
            get { return controlChildWidth; }
            set
            {
                if (controlChildWidth != value)
                {
                    controlChildWidth = value;
                    SetDirty();
                }
            }
        }

        public bool ControlChildHeight
        {
            get { return controlChildHeight; }
            set
            {
                if (controlChildHeight != value)
                {
                    controlChildHeight = value;
                    SetDirty();
                }
            }
        }

        public bool FitX
        {
            get { return fitX; }
            set
            {
                if (fitX != value)
                {
                    fitX = value;
                    SetDirty();
                }
            }
        }

        public bool FitY
        {
            get { return fitY; }
            set
            {
                if (fitY != value)
                {
                    fitY = value;
                    SetDirty();
                }
            }
        }

        public bool PreserveAspectRatio
        {
            get { return preserveAspectRatio; }
            set
            {
                if (preserveAspectRatio != value)
                {
                    preserveAspectRatio = value;
                    SetDirty();
                }
            }
        }

        public float AspectRatio
        {
            get { return aspectRatio; }
            set
            {
                if (!Mathf.Approximately(aspectRatio, value))
                {
                    aspectRatio = value;
                    SetDirty();
                }
            }
        }

        public bool Masonry
        {
            get { return masonry; }
            set
            {
                if (masonry != value)
                {
                    masonry = value;
                    SetDirty();
                }
            }
        }
        #endregion
    }
}