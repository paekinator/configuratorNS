using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "layout/reorderable-list")]
    [AddComponentMenu("Evo/UI/Layout/Reorderable List")]
    [RequireComponent(typeof(RectTransform))]
    public class ReorderableList : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool instantSnap = false;
        [Tooltip("Used if there is no layout group attached.")]
        [SerializeField] private float itemSpacing = 10f;
        [SerializeField, Range(0.05f, 2f)] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("Drag Settings", Constants.CustomEditorID)]
        [Range(0.1f, 1f)] public float dragAlpha = 1f;
        [Range(1f, 2f)] public float dragScale = 1.2f;

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private Canvas canvas;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<ReorderableListItem, int, int> onOrderChanged = new();

        // Cache
        Camera uiCamera;
        LayoutGroup layoutGroup;
        GridLayoutGroup gridLayout;
        ContentSizeFitter contentSizeFitter;
        ReorderableListItem draggedItem;
        WaitForEndOfFrame cachedEndOfFrame;

        readonly List<ReorderableListItem> activeSiblingsCache = new(32);
        readonly List<ReorderableListItem> previousItemsCache = new(32);
        readonly List<ReorderableListItem> newItemsCache = new(32);
        readonly List<ReorderableListItem> previewItemsCache = new(32);
        readonly List<Vector3> startPositionsCache = new(32);
        readonly List<ReorderableListItem> items = new();
        readonly List<Coroutine> activeAnimations = new();

        // Trackers
        Coroutine layoutCoroutine;
        Coroutine dragScaleCoroutine;
        Coroutine dragFadeCoroutine;

        // State Restoration for OnEnable
        bool isStartup = true;
        int pendingResortIndex = -1;
        ReorderableListItem pendingResortItem;

        // Layout group alignment cache
        TextAnchor childAlignment = TextAnchor.MiddleCenter;
        RectOffset layoutPadding;

        // Helpers
        int draggedFromIndex = -1;
        int previewInsertIndex = -1;
        LayoutType layoutType = LayoutType.Horizontal;

        // Enums
        public enum LayoutType { Horizontal, Vertical, Grid }

        // Properties
        public DockMagnifier DockMagnifier { get; set; }
        public bool IsDraggingActive => draggedItem != null;
        public bool IsAnimating { get; private set; }
        public bool IsDragging(ReorderableListItem item) => draggedItem == item;
        public List<ReorderableListItem> Items() => items;

        void Awake()
        {
            cachedEndOfFrame = new WaitForEndOfFrame();

            // Set default refs if not assigned
            if (canvas == null) { canvas = GetComponentInParent<Canvas>(); }
            if (listContainer == null) { listContainer = GetComponent<RectTransform>(); }

            // Cache UI camera
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // Detect layout group and content size fitter
            layoutGroup = listContainer.GetComponent<LayoutGroup>();
            contentSizeFitter = listContainer.GetComponent<ContentSizeFitter>();
            gridLayout = listContainer.GetComponent<GridLayoutGroup>();

            if (layoutGroup != null)
            {
                if (layoutGroup is GridLayoutGroup)
                {
                    layoutType = LayoutType.Grid;
                    itemSpacing = 0; // Handled by grid cellSize/spacing
                }
                else if (layoutGroup is HorizontalLayoutGroup group)
                {
                    layoutType = LayoutType.Horizontal;
                    itemSpacing = group.spacing;
                }
                else
                {
                    layoutType = LayoutType.Vertical;
                    itemSpacing = ((VerticalLayoutGroup)layoutGroup).spacing;
                }

                childAlignment = layoutGroup.childAlignment;
                layoutPadding = layoutGroup.padding;
            }
            else
            {
                // Default to Horizontal if no group, or stick to configured defaults
                layoutType = LayoutType.Horizontal;
            }

            // Initialize padding
            layoutPadding ??= new RectOffset(0, 0, 0, 0);
        }

        void OnEnable()
        {
            if (isStartup) { StartCoroutine(StartupInitialization()); }
            else
            {
                RefreshItemsList();
                RestorePendingResort();
            }
        }

        void OnDisable()
        {
            // Reset Drag State visuals and index
            if (draggedItem != null)
            {
                // Revert visual changes instantly
                draggedItem.transform.localScale = Vector3.one;
                draggedItem.canvasGroup.alpha = 1f;

                // Restore the item to its original active index position safely
                if (draggedFromIndex >= 0)
                {
                    if (gameObject.activeInHierarchy) { SetActiveSiblingIndex(draggedItem, draggedFromIndex); }
                    else
                    {
                        pendingResortItem = draggedItem;
                        pendingResortIndex = draggedFromIndex;
                    }
                }
            }

            // Stop all active routines so they don't try to run on disabled object
            StopAllActiveAnimations();

            // Clean up cached routines
            if (dragScaleCoroutine != null) { StopCoroutine(dragScaleCoroutine); dragScaleCoroutine = null; }
            if (dragFadeCoroutine != null) { StopCoroutine(dragFadeCoroutine); dragFadeCoroutine = null; }
            if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); layoutCoroutine = null; }

            // Reset internal state variables
            draggedItem = null;
            draggedFromIndex = -1;
            previewInsertIndex = -1;
            IsAnimating = false;

            // Force Layout Group back on so items aren't stuck in animation limbo
            if (layoutGroup != null) { layoutGroup.enabled = true; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
        }

        IEnumerator StartupInitialization()
        {
            // Wait one frame to allow Unity's native UI Layout Group to calculate initial sizes and positions.
            // This prevents items from stacking at 0,0 due to 0 width/height.
            yield return null;

            isStartup = false;
            RefreshItemsList();
            RestorePendingResort();
        }

        // Pivot-aware position helpers
        // Local coordinate origin sits at the container's pivot.
        // Item localPosition refers to where the item's own pivot sits.

        // Container edges in local space
        float ContainerLeft() => -listContainer.rect.width * listContainer.pivot.x;
        float ContainerRight() => listContainer.rect.width * (1f - listContainer.pivot.x);
        float ContainerBottom() => -listContainer.rect.height * listContainer.pivot.y;
        float ContainerTop() => listContainer.rect.height * (1f - listContainer.pivot.y);

        // What localPosition should an item have so that its left/right/top/bottom edge aligns with a given coordinate?
        float ItemXFromLeft(RectTransform r, float leftEdge) => leftEdge + r.rect.width * r.pivot.x;
        float ItemXFromRight(RectTransform r, float rightEdge) => rightEdge - r.rect.width * (1f - r.pivot.x);
        float ItemYFromTop(RectTransform r, float topEdge) => topEdge - r.rect.height * (1f - r.pivot.y);
        float ItemYFromBottom(RectTransform r, float bottomEdge) => bottomEdge + r.rect.height * r.pivot.y;
        float ItemXFromCenter(RectTransform r, float centerX) => centerX - r.rect.width * (0.5f - r.pivot.x);
        float ItemYFromCenter(RectTransform r, float centerY) => centerY - r.rect.height * (0.5f - r.pivot.y);

        // Where is the center of an item given its localPosition?
        float ItemCenterX(RectTransform r) => r.localPosition.x + r.rect.width * (0.5f - r.pivot.x);
        float ItemCenterY(RectTransform r) => r.localPosition.y + r.rect.height * (0.5f - r.pivot.y);

        /// <summary>
        /// Sets the absolute sibling index of an item so its visual order matches the desired active index.
        /// Prevents critical desyncs when inactive elements exist in the same hierarchy.
        /// </summary>
        void SetActiveSiblingIndex(ReorderableListItem item, int targetActiveIndex)
        {
            if (item == null)
                return;

            // Gather all active siblings EXCEPT the item we are moving using a cached list to avoid GC
            activeSiblingsCache.Clear();
            for (int i = 0; i < listContainer.childCount; i++)
            {
                Transform child = listContainer.GetChild(i);

                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child.TryGetComponent<ReorderableListItem>(out var listItem) && listItem != item)
                    activeSiblingsCache.Add(listItem);
            }

            if (targetActiveIndex >= activeSiblingsCache.Count) { item.transform.SetAsLastSibling(); }
            else
            {
                targetActiveIndex = Mathf.Max(0, targetActiveIndex);
                int targetSiblingIndex = activeSiblingsCache[targetActiveIndex].transform.GetSiblingIndex();

                // If the item currently sits before the target in the absolute hierarchy, 
                // removing it will shift the target's index down by 1.
                if (item.transform.GetSiblingIndex() < targetSiblingIndex)
                    targetSiblingIndex--;

                item.transform.SetSiblingIndex(targetSiblingIndex);
            }
        }

        void RestorePendingResort()
        {
            // If there's a pending restoration that was skipped in OnDisable (due to deactivation), apply it now
            if (pendingResortItem != null && pendingResortIndex >= 0)
            {
                SetActiveSiblingIndex(pendingResortItem, pendingResortIndex);
                pendingResortItem = null;
                pendingResortIndex = -1;
            }
        }

        /// <summary>
        /// Adds an existing RectTransform items to the list.
        /// </summary>
        public void AddExistingItems(List<RectTransform> itemsList)
        {
            foreach (var item in itemsList)
            {
                item.SetParent(listContainer, false);
                item.gameObject.SetActive(true);
            }

            // Refresh internal list
            RefreshItemsList();

            // Sync with DockMagnifier if present
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        /// <summary>
        /// Adds an existing RectTransform to the list.
        /// </summary>
        public void AddExistingItem(RectTransform item)
        {
            item.SetParent(listContainer, false);
            item.gameObject.SetActive(true);

            // Refresh internal list
            RefreshItemsList();

            // Sync with DockMagnifier if present
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        /// <summary>
        /// Adds an existing object to the list.
        /// </summary>
        public void AddExistingItem(GameObject item) => AddExistingItem(item.GetComponent<RectTransform>());

        /// <summary>
        /// Removes an item from the list and destroys its GameObject.
        /// </summary>
        public void RemoveItem(ReorderableListItem item)
        {
            if (items.Contains(item))
            {
                items.Remove(item);

                if (item != null && item.gameObject != null)
                {
                    // Unparent immediately
                    // This ensures DockMagnifier or other components don't 'see' this object 
                    // in their child loops while waiting for the Destroy() to actually happen.
                    item.transform.SetParent(null);
                    Destroy(item.gameObject);
                }

                if (gameObject.activeInHierarchy)
                {
                    if (IsDraggingActive) { StartCoroutine(DelayedLayoutRefresh()); }
                    if (DockMagnifier != null) { StartCoroutine(DelayedDockRefresh()); }
                }
                else
                {
                    // If inactive, don't start coroutines. 
                    if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
                }
            }
        }

        /// <summary>
        /// Removes a specific GameObject from the list and destroys it.
        /// </summary>
        public void RemoveItem(GameObject itemGo)
        {
            if (itemGo != null && itemGo.TryGetComponent<ReorderableListItem>(out var item))
                RemoveItem(item);
        }

        /// <summary>
        /// Detaches an item from the list without destroying it.
        /// The item is unparented and the list re-flows smoothly.
        /// </summary>
        public void DetachItem(RectTransform itemRect)
        {
            if (itemRect == null)
                return;

            // If this item is currently being dragged by the list, cancel that first.
            if (draggedItem != null && draggedItem.CachedRectTransform == itemRect)
                CancelDrag();

            // Remove the ReorderableListItem component so it doesn't interfere
            // with whatever system receives this object next.
            if (itemRect.TryGetComponent<ReorderableListItem>(out var listItem))
            {
                items.Remove(listItem);
                Destroy(listItem);
            }

            // Unparent — keep world position so marquee can calculate screen coords
            itemRect.SetParent(null, true);

            // Re-flow remaining items.
            if (gameObject.activeInHierarchy)
            {
                RefreshItemsList();
                if (DockMagnifier != null) { StartCoroutine(DelayedDockRefresh()); }
            }
        }

        /// <summary>
        /// Detaches multiple items from the list without destroying them.
        /// </summary>
        public void DetachItems(List<RectTransform> itemRects)
        {
            if (itemRects == null || itemRects.Count == 0)
                return;

            for (int i = 0; i < itemRects.Count; i++)
            {
                var itemRect = itemRects[i];

                if (itemRect == null)
                    continue;

                if (draggedItem != null && draggedItem.CachedRectTransform == itemRect)
                    CancelDrag();

                if (itemRect.TryGetComponent<ReorderableListItem>(out var listItem))
                {
                    items.Remove(listItem);
                    Destroy(listItem);
                }

                itemRect.SetParent(null, true);
            }

            if (gameObject.activeInHierarchy)
            {
                RefreshItemsList();
                if (DockMagnifier != null) { StartCoroutine(DelayedDockRefresh()); }
            }
        }

        /// <summary>
        /// Clears all items from the list and destroys their GameObjects.
        /// </summary>
        public void ClearItems()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item != null && item.gameObject != null)
                {
                    item.transform.SetParent(null);
                    Destroy(item.gameObject);
                }
            }
            items.Clear();

            if (gameObject.activeInHierarchy)
            {
                if (IsDraggingActive) { StartCoroutine(DelayedLayoutRefresh()); }
                if (DockMagnifier != null) { StartCoroutine(DelayedDockRefresh()); }
            }
            else if (DockMagnifier != null)
            {
                DockMagnifier.RefreshTargets();
            }
        }

        IEnumerator DelayedDockRefresh()
        {
            yield return cachedEndOfFrame;
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        void StopAllActiveAnimations()
        {
            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                if (activeAnimations[i] != null)
                    StopCoroutine(activeAnimations[i]);
            }
            activeAnimations.Clear();
        }

        // Helper to stop and restart visual scale coroutines
        void RestartScaleCoroutine(Transform target, Vector3 targetScale, float duration)
        {
            if (dragScaleCoroutine != null) { StopCoroutine(dragScaleCoroutine); }
            dragScaleCoroutine = StartCoroutine(AnimateScale(target, targetScale, duration));
        }

        // Helper to stop and restart visual fade coroutines
        void RestartFadeCoroutine(CanvasGroup target, float targetAlpha, float duration)
        {
            if (dragFadeCoroutine != null) { StopCoroutine(dragFadeCoroutine); }
            dragFadeCoroutine = StartCoroutine(AnimateFade(target, targetAlpha, duration));
        }

        public void ResetState()
        {
            draggedItem = null;
            draggedFromIndex = -1;
            previewInsertIndex = -1;
        }

        public void RefreshItemsList()
        {
            // Using cached lists here to avoid Hashset and List GC allocation every drag frame or detach
            previousItemsCache.Clear();
            foreach (var existingItem in items) { previousItemsCache.Add(existingItem); }

            items.Clear();
            newItemsCache.Clear();

            // Fetch childs
            for (int i = 0; i < listContainer.childCount; i++)
            {
                Transform child = listContainer.GetChild(i);

                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (!child.TryGetComponent<ReorderableListItem>(out var item))
                    item = child.gameObject.AddComponent<ReorderableListItem>();

                item.Initialize(this);
                items.Add(item);

                // Add to our list of freshly detected items
                if (!previousItemsCache.Contains(item)) { newItemsCache.Add(item); }
            }

            bool listChanged = (newItemsCache.Count > 0) || (items.Count != previousItemsCache.Count);

            if (gameObject.activeInHierarchy)
            {
                // Unconditionally refresh targets if composition changed to prevent lingering caches
                if (listChanged && DockMagnifier != null) { DockMagnifier.RefreshTargets(); }

                // Only strictly enforce positions if we are actively rearranging items to maintain drag preview layout
                // (Instantly snap newly created items to their correct target position preventing them from visually sliding from 0,0)
                if (IsDraggingActive && newItemsCache.Count > 0) { SnapItemsToTarget(newItemsCache); }
            }

            // Update drag indices if we're currently dragging
            if (draggedItem != null && items.Contains(draggedItem))
            {
                int newDraggedIndex = items.IndexOf(draggedItem);
                draggedFromIndex = newDraggedIndex;
                previewInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);
            }

            // Only hijack layout updates if we are actively rearranging, let native UI handle simple addition/removal
            if (gameObject.activeInHierarchy && IsDraggingActive) { StartCoroutine(DelayedLayoutRefresh()); }
        }

        /// <summary>
        /// Explicitly snaps specific items to their designated target position instantly.
        /// </summary>
        void SnapItemsToTarget(List<ReorderableListItem> targetItems)
        {
            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, items.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);

                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                // Check for target items
                if (targetItems.Contains(items[i])) { items[i].transform.localPosition = targetPos; }
            }
        }

        public void RefreshLayout()
        {
            // Stop conflicting layout routines
            if (layoutCoroutine != null)
            {
                StopCoroutine(layoutCoroutine);
                IsAnimating = false;
            }

            if (!gameObject.activeInHierarchy)
                return;

            if (instantSnap)
            {
                if (draggedItem == null) { layoutCoroutine = StartCoroutine(SnapToNormalPositions()); }
                else { layoutCoroutine = StartCoroutine(SnapToPreviewPositions()); }
            }
            else
            {
                if (draggedItem == null) { layoutCoroutine = StartCoroutine(AnimateToNormalPositions()); }
                else { layoutCoroutine = StartCoroutine(AnimateToPreviewPositions()); }
            }
        }

        public void CancelDrag()
        {
            if (draggedItem == null)
                return;

            if (draggedFromIndex >= 0)
            {
                // Restore index here as well for consistency securely ignoring inactive items
                SetActiveSiblingIndex(draggedItem, draggedFromIndex);
            }

            // Reset visual properties (respect instant snap setting)
            if (instantSnap)
            {
                draggedItem.transform.localScale = Vector3.one;
                draggedItem.canvasGroup.alpha = 1f;
            }
            else
            {
                if (gameObject.activeInHierarchy)
                {
                    RestartScaleCoroutine(draggedItem.transform, Vector3.one, 0.2f);
                    RestartFadeCoroutine(draggedItem.canvasGroup, 1f, 0.2f);
                }
                else
                {
                    draggedItem.transform.localScale = Vector3.one;
                    draggedItem.canvasGroup.alpha = 1f;
                }
            }

            // Reset state and refresh
            ResetState();
            RefreshItemsList();
            RefreshLayout();
        }

        public void OnItemBeginDrag(ReorderableListItem item)
        {
            if (item == null || !items.Contains(item))
                return;

            draggedItem = item;
            draggedFromIndex = items.IndexOf(item);
            previewInsertIndex = draggedFromIndex;

            // Store current position before any changes
            Vector3 currentPosition = item.transform.localPosition;

            // Immediately disable layout group to prepare for manual drag preview animations
            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            // Bring dragged item to front
            item.transform.SetAsLastSibling();

            // Immediately restore position to prevent flicker
            item.transform.localPosition = currentPosition;

            // Start drag animations
            if (instantSnap)
            {
                // Set scale and alpha instantly
                item.transform.localScale = Vector3.one * dragScale;
                item.canvasGroup.alpha = dragAlpha;
            }
            else
            {
                RestartScaleCoroutine(item.transform, Vector3.one * dragScale, 0.1f);
                RestartFadeCoroutine(item.canvasGroup, dragAlpha, 0.1f);
            }

            RefreshLayout();
        }

        public void OnItemDrag(ReorderableListItem item, Vector2 screenPosition, Vector2 offset)
        {
            if (item != draggedItem)
                return;

            // Convert screen position to local position
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(listContainer, screenPosition, uiCamera, out Vector2 localPoint))
            {
                localPoint += offset;
                RectTransform itemRect = item.CachedRectTransform;

                if (layoutType == LayoutType.Grid)
                {
                    // Grid Layout: Allow 2D movement clamped to container
                    float minX = ItemXFromLeft(itemRect, ContainerLeft() + layoutPadding.left);
                    float maxX = ItemXFromRight(itemRect, ContainerRight() - layoutPadding.right);
                    float minY = ItemYFromBottom(itemRect, ContainerBottom() + layoutPadding.bottom);
                    float maxY = ItemYFromTop(itemRect, ContainerTop() - layoutPadding.top);

                    // Clamp position to stay within bounds
                    float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
                    float clampedY = Mathf.Clamp(localPoint.y, minY, maxY);

                    item.transform.localPosition = new Vector3(clampedX, clampedY, 0);
                }
                else if (layoutType == LayoutType.Horizontal)
                {
                    // Calculate Y position based on vertical alignment
                    float yPos = 0f;

                    float top = ContainerTop() - layoutPadding.top;
                    float bottom = ContainerBottom() + layoutPadding.bottom;
                    float innerCenterY = (top + bottom) * 0.5f;

                    switch (childAlignment)
                    {
                        case TextAnchor.UpperLeft:
                        case TextAnchor.UpperCenter:
                        case TextAnchor.UpperRight:
                            yPos = ItemYFromTop(itemRect, top);
                            break;
                        case TextAnchor.MiddleLeft:
                        case TextAnchor.MiddleCenter:
                        case TextAnchor.MiddleRight:
                            yPos = ItemYFromCenter(itemRect, innerCenterY);
                            break;
                        case TextAnchor.LowerLeft:
                        case TextAnchor.LowerCenter:
                        case TextAnchor.LowerRight:
                            yPos = ItemYFromBottom(itemRect, bottom);
                            break;
                    }

                    // Calculate bounds for horizontal dragging
                    float minPos = ItemXFromLeft(itemRect, ContainerLeft() + layoutPadding.left);
                    float maxPos = ItemXFromRight(itemRect, ContainerRight() - layoutPadding.right);

                    // Clamp the X position to stay within bounds
                    float clampedX = Mathf.Clamp(localPoint.x, minPos, maxPos);

                    item.transform.localPosition = new Vector3(clampedX, yPos, 0);
                }
                else // Vertical
                {
                    // Calculate X position based on horizontal alignment
                    float xPos = 0f;

                    float left = ContainerLeft() + layoutPadding.left;
                    float right = ContainerRight() - layoutPadding.right;
                    float innerCenterX = (left + right) * 0.5f;

                    switch (childAlignment)
                    {
                        case TextAnchor.UpperLeft:
                        case TextAnchor.MiddleLeft:
                        case TextAnchor.LowerLeft:
                            xPos = ItemXFromLeft(itemRect, left);
                            break;
                        case TextAnchor.UpperCenter:
                        case TextAnchor.MiddleCenter:
                        case TextAnchor.LowerCenter:
                            xPos = ItemXFromCenter(itemRect, innerCenterX);
                            break;
                        case TextAnchor.UpperRight:
                        case TextAnchor.MiddleRight:
                        case TextAnchor.LowerRight:
                            xPos = ItemXFromRight(itemRect, right);
                            break;
                    }

                    // Calculate bounds for vertical dragging
                    float maxPos = ItemYFromTop(itemRect, ContainerTop() - layoutPadding.top);
                    float minPos = ItemYFromBottom(itemRect, ContainerBottom() + layoutPadding.bottom);

                    // Clamp the Y position to stay within bounds
                    float clampedY = Mathf.Clamp(localPoint.y, minPos, maxPos);
                    item.transform.localPosition = new Vector3(xPos, clampedY, 0);
                }
            }

            // Check for insertion point
            int newInsertIndex = GetInsertionIndex();
            if (newInsertIndex != previewInsertIndex)
            {
                previewInsertIndex = newInsertIndex;
                RefreshLayout();
            }
        }

        public void OnItemEndDrag(ReorderableListItem item)
        {
            if (item != draggedItem)
                return;

            // Store original index before changes
            int originalIndex = draggedFromIndex;

            // Clamp insert index to valid range
            int finalInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);

            // Remove from current position
            items.Remove(item);

            // Insert at final position
            if (finalInsertIndex >= 0 && finalInsertIndex <= items.Count)
            {
                items.Insert(finalInsertIndex, item);
                SetActiveSiblingIndex(item, finalInsertIndex);
            }

            // Reset visual properties (respect instant snap setting)
            if (instantSnap)
            {
                // Reset scale and alpha instantly
                item.transform.localScale = Vector3.one;
                item.canvasGroup.alpha = 1f;
            }
            else
            {
                RestartScaleCoroutine(item.transform, Vector3.one, 0.2f);
                RestartFadeCoroutine(item.canvasGroup, 1f, 0.2f);
            }

            // Fire event only if order actually changed
            if (originalIndex != finalInsertIndex) { onOrderChanged?.Invoke(item, originalIndex, finalInsertIndex); }

            // Reset drag state
            ResetState();
            RefreshItemsList();
            RefreshLayout();
        }

        float CalculateStartPosition()
        {
            // Only used for Horizontal/Vertical layouts
            float totalSize = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                totalSize += GetItemSize(itemRect);
                if (i < items.Count - 1) { totalSize += itemSpacing; }
            }

            // Calculate start position (left edge for horizontal, top edge for vertical)
            // based on alignment. This is an edge cursor, not an item center.
            float startPos = 0f;

            float left = ContainerLeft() + layoutPadding.left;
            float right = ContainerRight() - layoutPadding.right;
            float top = ContainerTop() - layoutPadding.top;
            float bottom = ContainerBottom() + layoutPadding.bottom;

            if (layoutType == LayoutType.Horizontal)
            {
                float innerWidth = right - left;
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.LowerLeft:
                        startPos = left;
                        break;
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        startPos = left + (innerWidth - totalSize) * 0.5f;
                        break;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        startPos = right - totalSize;
                        break;
                }
            }
            else
            {
                // Vertical: startPos is the top edge cursor (items go downward)
                float innerHeight = top - bottom;
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.UpperCenter:
                    case TextAnchor.UpperRight:
                        startPos = top;
                        break;
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.MiddleRight:
                        startPos = top - (innerHeight - totalSize) * 0.5f;
                        break;
                    case TextAnchor.LowerLeft:
                    case TextAnchor.LowerCenter:
                    case TextAnchor.LowerRight:
                        startPos = bottom + totalSize;
                        break;
                }
            }

            return startPos;
        }

        /// <summary>
        /// Checks DockMagnifier for the correct resting size.
        /// </summary>
        float GetItemSize(RectTransform itemRect)
        {
            if (layoutType == LayoutType.Grid && gridLayout != null)
            {
                // Grids enforce cell size, typically
                return (gridLayout.startAxis == GridLayoutGroup.Axis.Horizontal) ? gridLayout.cellSize.x : gridLayout.cellSize.y;
            }

            if (DockMagnifier != null && DockMagnifier.isActiveAndEnabled && IsDraggingActive)
            {
                // Dragging: Prioritize stable baseline size so target slots don't violently shift.
                Vector2 restingSize = DockMagnifier.GetRestingSize(itemRect);

                if (layoutType == LayoutType.Horizontal && restingSize.x > 0.1f)
                    return restingSize.x;

                if (layoutType == LayoutType.Vertical && restingSize.y > 0.1f)
                    return restingSize.y;
            }

            return (layoutType == LayoutType.Horizontal) ? itemRect.rect.width : itemRect.rect.height;
        }

        int GetInsertionIndex()
        {
            if (items.Count == 0 || draggedItem == null)
                return 0;

            if (layoutType == LayoutType.Grid)
            {
                // Grid Logic: Find closest index based on distance
                float minDistance = float.MaxValue;
                Vector3 draggedPos = draggedItem.transform.localPosition;
                int bestIndex = 0;

                // Simulate "If I put it at index K, where would it be?"
                // and compare draggedPos to that target position
                int totalItems = items.Count; // Total items including dragged
                for (int k = 0; k < totalItems; k++)
                {
                    Vector3 slotPos = GetGridPosition(k, totalItems);
                    float d = Vector3.SqrMagnitude(draggedPos - slotPos);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestIndex = k;
                    }
                }

                return bestIndex;
            }

            // Get the dragged item's geometric center and size
            RectTransform draggedRect = draggedItem.CachedRectTransform;
            float draggedItemSize = GetItemSize(draggedRect);
            float draggedItemCenter;

            if (layoutType == LayoutType.Horizontal) { draggedItemCenter = ItemCenterX(draggedRect); }
            else { draggedItemCenter = ItemCenterY(draggedRect); }

            float startPos = CalculateStartPosition();
            float currentPos = startPos;

            // Calculate center points of each item
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform itemRect = items[i].CachedRectTransform;
                float itemSize = GetItemSize(itemRect);

                // Skip the dragged item in position calculations
                if (items[i] == draggedItem)
                {
                    if (layoutType == LayoutType.Horizontal) { currentPos += itemSize + itemSpacing; }
                    else { currentPos -= itemSize + itemSpacing; }
                    
                    continue;
                }

                // Calculate this item's center position (geometric center of slot)
                float itemCenter = currentPos + ((layoutType == LayoutType.Horizontal) ? itemSize * 0.5f : -itemSize * 0.5f);

                // Determine which edge to check based on drag direction
                bool shouldInsertHere;
                if (layoutType == LayoutType.Horizontal)
                {
                    if (draggedFromIndex < i)
                    {
                        // Dragging left to right: use right edge of dragged item
                        float draggedRightEdge = draggedItemCenter + (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedRightEdge < itemCenter;
                    }
                    else
                    {
                        // Dragging right to left: use left edge of dragged item
                        float draggedLeftEdge = draggedItemCenter - (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedLeftEdge < itemCenter;
                    }
                }
                else
                {
                    if (draggedFromIndex < i)
                    {
                        // Dragging top to bottom: use bottom edge of dragged item
                        float draggedBottomEdge = draggedItemCenter - (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedBottomEdge > itemCenter;
                    }
                    else
                    {
                        // Dragging bottom to top: use top edge of dragged item
                        float draggedTopEdge = draggedItemCenter + (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedTopEdge > itemCenter;
                    }
                }

                if (shouldInsertHere)
                {
                    int insertIndex = i;

                    if (draggedFromIndex < i && draggedFromIndex >= 0)
                        insertIndex = i - 1;

                    return Mathf.Clamp(insertIndex, 0, items.Count);
                }

                if (layoutType == LayoutType.Horizontal) { currentPos += itemSize + itemSpacing; }
                else { currentPos -= itemSize + itemSpacing; }
            }

            // If we're past all items, insert at the end
            int finalIndex = items.Count;
            if (draggedFromIndex >= 0) { finalIndex = items.Count - 1; }

            return Mathf.Clamp(finalIndex, 0, items.Count);
        }

        Vector3 GetLinearPosition(float currentPos, RectTransform itemRect)
        {
            float yPos = 0f;
            float xPos = 0f;

            float left = ContainerLeft() + layoutPadding.left;
            float right = ContainerRight() - layoutPadding.right;
            float innerCenterX = (left + right) * 0.5f;

            float top = ContainerTop() - layoutPadding.top;
            float bottom = ContainerBottom() + layoutPadding.bottom;
            float innerCenterY = (top + bottom) * 0.5f;

            if (layoutType == LayoutType.Horizontal)
            {
                // currentPos is the left-edge cursor; offset by item's pivot
                xPos = ItemXFromLeft(itemRect, currentPos);

                // Calculate Y position based on vertical alignment
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.UpperCenter:
                    case TextAnchor.UpperRight:
                        yPos = ItemYFromTop(itemRect, top);
                        break;
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.MiddleRight:
                        yPos = ItemYFromCenter(itemRect, innerCenterY);
                        break;
                    case TextAnchor.LowerLeft:
                    case TextAnchor.LowerCenter:
                    case TextAnchor.LowerRight:
                        yPos = ItemYFromBottom(itemRect, bottom);
                        break;
                }
                return new Vector3(xPos, yPos, 0);
            }
            else // Vertical
            {
                // currentPos is the top-edge cursor; offset by item's pivot
                yPos = ItemYFromTop(itemRect, currentPos);

                // Calculate X position based on horizontal alignment
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.LowerLeft:
                        xPos = ItemXFromLeft(itemRect, left);
                        break;
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        xPos = ItemXFromCenter(itemRect, innerCenterX);
                        break;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        xPos = ItemXFromRight(itemRect, right);
                        break;
                }
                return new Vector3(xPos, yPos, 0);
            }
        }

        Vector3 GetGridPosition(int index, int totalCount)
        {
            if (gridLayout == null)
                return Vector3.zero;

            // Extract Grid Settings
            int constraintCount = gridLayout.constraintCount;
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            GridLayoutGroup.Constraint constraint = gridLayout.constraint;
            GridLayoutGroup.Corner startCorner = gridLayout.startCorner;
            GridLayoutGroup.Axis startAxis = gridLayout.startAxis;

            // Calculate Cells Per Line
            int cellCountX;
            int cellCountY;

            float width = listContainer.rect.width;
            float height = listContainer.rect.height;

            if (constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                cellCountX = constraintCount;
                cellCountY = Mathf.CeilToInt(totalCount / (float)cellCountX);
            }
            else if (constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                cellCountY = constraintCount;
                cellCountX = Mathf.CeilToInt(totalCount / (float)cellCountY);
            }
            else // Flexible
            {
                if (startAxis == GridLayoutGroup.Axis.Horizontal)
                {
                    cellCountX = Mathf.FloorToInt((width - layoutPadding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x));
                    cellCountX = Mathf.Max(1, cellCountX);
                    cellCountY = Mathf.CeilToInt(totalCount / (float)cellCountX);
                }
                else
                {
                    cellCountY = Mathf.FloorToInt((height - layoutPadding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y));
                    cellCountY = Mathf.Max(1, cellCountY);
                    cellCountX = Mathf.CeilToInt(totalCount / (float)cellCountY);
                }
            }

            // Calculate Row and Column for the specific index
            int row, col;
            if (startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                col = index % cellCountX;
                row = index / cellCountX;
            }
            else
            {
                row = index % cellCountY;
                col = index / cellCountY;
            }

            // Total size of the grid content
            // Used for Alignment
            float totalGridWidth = cellCountX * cellSize.x + (cellCountX - 1) * spacing.x;
            float totalGridHeight = cellCountY * cellSize.y + (cellCountY - 1) * spacing.y;

            // Handle Start Corner (re-map col/row)
            if (startCorner == GridLayoutGroup.Corner.UpperRight || startCorner == GridLayoutGroup.Corner.LowerRight) { col = cellCountX - 1 - col; }
            if (startCorner == GridLayoutGroup.Corner.LowerLeft || startCorner == GridLayoutGroup.Corner.LowerRight) { row = cellCountY - 1 - row; }

            // Cell offsets from top-left of content area to the cell's top-left corner
            float cellOffsetX = col * (cellSize.x + spacing.x);
            float cellOffsetY = row * (cellSize.y + spacing.y);

            // Item pivot within its cell (use grid cell center as default, but respect item pivot if items exist)
            // For grid layout, items use the cell size, so pivot offset within cell:
            float pivotInCellX = cellSize.x * 0.5f; // Grid items are centered in cells
            float pivotInCellY = cellSize.y * 0.5f;

            // Calculate Alignment Offset — where the top-left of the content area is in local coords
            float left = ContainerLeft() + layoutPadding.left;
            float right = ContainerRight() - layoutPadding.right;
            float top = ContainerTop() - layoutPadding.top;
            float bottom = ContainerBottom() + layoutPadding.bottom;

            float innerWidth = right - left;
            float innerHeight = top - bottom;

            float startX;
            float startY;

            // Horizontal Alignment
            if (childAlignment == TextAnchor.UpperLeft || childAlignment == TextAnchor.MiddleLeft || childAlignment == TextAnchor.LowerLeft)
                startX = left;
            else if (childAlignment == TextAnchor.UpperCenter || childAlignment == TextAnchor.MiddleCenter || childAlignment == TextAnchor.LowerCenter)
                startX = left + (innerWidth - totalGridWidth) * 0.5f;
            else // Right
                startX = right - totalGridWidth;

            // Vertical Alignment
            if (childAlignment == TextAnchor.UpperLeft || childAlignment == TextAnchor.UpperCenter || childAlignment == TextAnchor.UpperRight)
                startY = top;
            else if (childAlignment == TextAnchor.MiddleLeft || childAlignment == TextAnchor.MiddleCenter || childAlignment == TextAnchor.MiddleRight)
                startY = top - (innerHeight - totalGridHeight) * 0.5f;
            else // Bottom
                startY = bottom + totalGridHeight;

            // Final position: startX/Y is top-left of content area
            // Add cell offset to get to cell's top-left, then add pivot offset within cell
            float xPos = startX + cellOffsetX + pivotInCellX;
            float yPos = startY - cellOffsetY - pivotInCellY;

            return new Vector3(xPos, yPos, 0);
        }

        IEnumerator DelayedLayoutRefresh()
        {
            yield return cachedEndOfFrame;
            RefreshLayout();
        }

        IEnumerator AnimateToNormalPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Capture initial start positions perfectly
            startPositionsCache.Clear();
            for (int i = 0; i < items.Count; i++)
                startPositionsCache.Add(items[i] != null ? items[i].transform.localPosition : Vector3.zero);

            float elapsed = 0f;
            float invDuration = 1f / animationDuration;

            // Iterate seamlessly, recalculating target positions frame-by-frame 
            // so magnifier additions are handled elegantly without overlap snaps.
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed * invDuration;
                t = animationCurve.Evaluate(Mathf.Clamp01(t));

                float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null)
                        continue;

                    RectTransform itemRect = items[i].CachedRectTransform;
                    Vector3 targetPos;

                    if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, items.Count); }
                    else
                    {
                        targetPos = GetLinearPosition(currentPos, itemRect);

                        if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                        else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                    }

                    items[i].transform.localPosition = Vector3.Lerp(startPositionsCache[i], targetPos, t);
                }

                yield return null;
            }

            SnapItemsToTarget(items);

            if (layoutGroup != null) { layoutGroup.enabled = true; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
            IsAnimating = false;
        }

        IEnumerator AnimateToPreviewPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null)
                    layoutGroup.enabled = true;

                if (contentSizeFitter != null)
                    contentSizeFitter.enabled = true;

                IsAnimating = false;

                yield break;
            }

            // Create preview list using GC-free cached lists
            previewItemsCache.Clear();
            previewItemsCache.AddRange(items);

            // Remove dragged item from its current position
            if (draggedItem != null && previewItemsCache.Contains(draggedItem))
                previewItemsCache.Remove(draggedItem);

            // Insert at preview position
            if (draggedItem != null && previewInsertIndex >= 0 && previewInsertIndex <= previewItemsCache.Count)
                previewItemsCache.Insert(previewInsertIndex, draggedItem);

            startPositionsCache.Clear();
            for (int i = 0; i < previewItemsCache.Count; i++)
                startPositionsCache.Add(previewItemsCache[i] != null ? previewItemsCache[i].transform.localPosition : Vector3.zero);

            float duration = animationDuration * 0.5f;
            float elapsed = 0f;
            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed * invDuration;
                t = animationCurve.Evaluate(Mathf.Clamp01(t));

                float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

                for (int i = 0; i < previewItemsCache.Count; i++)
                {
                    if (previewItemsCache[i] == null)
                        continue;

                    if (previewItemsCache[i] == draggedItem)
                    {
                        // Skip dragged item but reserve space
                        if (layoutType != LayoutType.Grid)
                        {
                            RectTransform draggedRect = draggedItem.CachedRectTransform;

                            if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(draggedRect) + itemSpacing; }
                            else { currentPos -= GetItemSize(draggedRect) + itemSpacing; }
                        }

                        continue;
                    }

                    RectTransform itemRect = previewItemsCache[i].CachedRectTransform;
                    Vector3 targetPos;

                    if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, previewItemsCache.Count); }
                    else
                    {
                        targetPos = GetLinearPosition(currentPos, itemRect);

                        if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                        else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                    }

                    previewItemsCache[i].transform.localPosition = Vector3.Lerp(startPositionsCache[i], targetPos, t);
                }

                yield return null;
            }

            IsAnimating = false;
        }

        IEnumerator SnapToNormalPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null)
                    layoutGroup.enabled = true;

                if (contentSizeFitter != null)
                    contentSizeFitter.enabled = true;

                IsAnimating = false;

                yield break;
            }

            // Calculate and set positions instantly
            SnapItemsToTarget(items);

            // Wait one frame then re-enable layout components
            yield return null;

            if (layoutGroup != null)
                layoutGroup.enabled = true;

            if (contentSizeFitter != null)
                contentSizeFitter.enabled = true;

            IsAnimating = false;
        }

        IEnumerator SnapToPreviewPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Create preview list using GC-free cache
            previewItemsCache.Clear();
            previewItemsCache.AddRange(items);

            // Remove dragged item from its current position
            if (draggedItem != null && previewItemsCache.Contains(draggedItem)) { previewItemsCache.Remove(draggedItem); }

            // Insert at preview position
            if (draggedItem != null && previewInsertIndex >= 0 && previewInsertIndex <= previewItemsCache.Count) { previewItemsCache.Insert(previewInsertIndex, draggedItem); }

            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            // Set all positions instantly except the dragged item
            for (int i = 0; i < previewItemsCache.Count; i++)
            {
                if (previewItemsCache[i] == null)
                    continue;

                if (previewItemsCache[i] == draggedItem)
                {
                    // Skip dragged item but reserve space
                    if (layoutType != LayoutType.Grid)
                    {
                        RectTransform draggedRect = draggedItem.CachedRectTransform;

                        if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(draggedRect) + itemSpacing; }
                        else { currentPos -= GetItemSize(draggedRect) + itemSpacing; }
                    }
                    continue;
                }

                RectTransform itemRect = previewItemsCache[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, previewItemsCache.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);
                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                previewItemsCache[i].transform.localPosition = targetPos;
            }

            // Wait one frame
            yield return null;
            IsAnimating = false;
        }

        IEnumerator AnimateScale(Transform target, Vector3 targetScale, float duration)
        {
            if (target == null)
                yield break;

            Vector3 startScale = target.localScale;
            float elapsed = 0f;
            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed * invDuration);
                target.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            target.localScale = targetScale;
        }

        IEnumerator AnimateFade(CanvasGroup target, float targetAlpha, float duration)
        {
            if (target == null)
                yield break;

            float startAlpha = target.alpha;
            float elapsed = 0f;
            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed * invDuration);
                target.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            target.alpha = targetAlpha;
        }
    }

    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class ReorderableListItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // References
        public CanvasGroup canvasGroup;
        public RectTransform CachedRectTransform { get; private set; }

        // Helpers
        bool isDragging;
        ReorderableList targetList;
        Vector2 dragOffset;

        void OnEnable()
        {
            if (targetList != null && !targetList.Items().Contains(this))
                targetList.RefreshItemsList();
        }

        void OnDisable()
        {
            if (targetList != null && targetList.gameObject.activeInHierarchy)
            {
                if (targetList.IsDragging(this)) { targetList.CancelDrag(); }
                else if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
                {
                    transform.SetAsLastSibling();
                    targetList.RefreshItemsList();
                }
            }
        }

        public void Initialize(ReorderableList parentList)
        {
            targetList = parentList;
            CachedRectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetList == null)
                return;

            isDragging = true;

            // Calculate offset between pointer and item position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPos);

            dragOffset = (Vector2)transform.localPosition - localPointerPos;
            targetList.OnItemBeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            targetList.OnItemDrag(this, eventData.position, dragOffset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            isDragging = false;
            targetList.OnItemEndDrag(this);
        }
    }
}