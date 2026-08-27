using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "layout/pages")]
    [AddComponentMenu("Evo/UI/Layout/Pages")]
    public class Pages : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [EvoHeader("Pages", Constants.CustomEditorID)]
        [SerializeField] private int defaultPageIndex = 0;
        public List<Page> pages = new();

        [EvoHeader("References", Constants.CustomEditorID)]
        public RectTransform container;
        public RectTransform indicator;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool disableInvisiblePages = true;
        public bool useUnscaledTime = false;
        public bool interruptTransitions = true;
        [SerializeField] private bool autoHandleNestedScrolling = false;

        [EvoHeader("Swipe Settings", Constants.CustomEditorID)]
        [Range(0.1f, 0.9f)] public float swipeThreshold = 0.3f;
        [Range(0.1f, 1f)] public float elasticResistance = 0.3f;
        public float velocityThreshold = 500f;
        public SwipeDirection swipeDirection = SwipeDirection.Horizontal;

        [EvoHeader("Animation Settings", Constants.CustomEditorID)]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, 2f)] public float transitionDuration = 0.3f;
        public float pageSpacing = 20f;

        [EvoHeader("Events")]
        public UnityEvent<int> onPageChanged = new();

        public enum SwipeDirection
        {
            Horizontal,
            Vertical
        }

        [System.Serializable]
        public class Page
        {
            public string pageID = "";
            public RectTransform pageObject;
            public Button pageButton;
        }

        // Public properties
        public int CurrentPageIndex { get; private set; } = -1;
        public bool IsTransitioning { get; private set; } = false;

        // Cache
        Canvas canvas;
        Coroutine transitionCoroutine;

        // Helpers
        bool isDragging;
        float currentOffset;
        float targetOffset;
        float dragVelocity;
        float dragStartTime;
        Vector2 dragStartPos;
        Vector2 indicatorTargetPos;
        Vector2 indicatorTargetSize;

        // Static Cache
        static readonly List<ScrollRect> scrollRectCache = new();
        static readonly WaitForEndOfFrame endOfFrameWait = new();

        IEnumerator Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (container == null) { container = transform as RectTransform; }

            // Setup indicator anchor to middle center
            if (indicator != null)
            {
                indicator.anchorMin = new Vector2(0.5f, 0.5f);
                indicator.anchorMax = new Vector2(0.5f, 0.5f);
                indicator.pivot = new Vector2(0.5f, 0.5f);

                // Set initial visibility
                bool shouldShow = pages.Count > 0 && defaultPageIndex >= 0
                    && defaultPageIndex < pages.Count
                    && pages[defaultPageIndex].pageButton != null;
                indicator.gameObject.SetActive(shouldShow);
            }

            // Defer one frame so Unity's layout system can finish calculating RectTransform dimensions
            yield return null;

            PositionPages();
            CheckAndSetupNestedScrolling();

            // Set initial page
            if (pages.Count > 0) { OpenPage(defaultPageIndex, false); }

            // Wait for pages to be initialized
            yield return endOfFrameWait;

            if (ShouldShowIndicator(defaultPageIndex))
            {
                UpdateIndicator(defaultPageIndex, false);
                indicator.anchoredPosition = indicatorTargetPos;
                indicator.sizeDelta = indicatorTargetSize;
                indicator.gameObject.SetActive(true);
            }
            else if (indicator != null)
            {
                indicator.gameObject.SetActive(false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Allow interrupting transitions
            if (IsTransitioning && !interruptTransitions)
                return;

            isDragging = true;
            dragStartPos = eventData.position;
            dragStartTime = Time.unscaledTime;
            dragVelocity = 0f;

            // Stop any ongoing transition
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                IsTransitioning = false;
            }

            // Activate all pages for smooth dragging
            if (disableInvisiblePages)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    if (pages[i].pageObject != null)
                        pages[i].pageObject.gameObject.SetActive(true);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;

            float currentPos = swipeDirection == SwipeDirection.Horizontal ? eventData.position.x : eventData.position.y;
            float startPos = swipeDirection == SwipeDirection.Horizontal ? dragStartPos.x : dragStartPos.y;
            float delta = (currentPos - startPos) / scaleFactor;

            // Invert delta for vertical scrolling to match expected behavior
            if (swipeDirection == SwipeDirection.Vertical) { delta = -delta; }

            // Calculate velocity (smoothed over time)
            float timeDelta = Time.unscaledTime - dragStartTime;
            if (timeDelta > 0) { dragVelocity = delta / timeDelta; }

            // Limit drag to one page in each direction
            float pageSize = containerSize + pageSpacing;
            float maxDrag = pageSize;
            delta = Mathf.Clamp(delta, -maxDrag, maxDrag);

            // Apply elastic resistance at bounds
            float targetDragOffset = targetOffset + delta;
            float minOffset = -(pages.Count - 1) * pageSize;
            float maxOffset = 0;

            if (targetDragOffset > maxOffset)
            {
                float overflow = targetDragOffset - maxOffset;
                delta -= overflow * (1f - elasticResistance);
            }
            else if (targetDragOffset < minOffset)
            {
                float overflow = minOffset - targetDragOffset;
                delta += overflow * (1f - elasticResistance);
            }

            currentOffset = targetOffset + delta;
            ApplyOffset(currentOffset);

            // Update indicator during drag
            if (ShouldShowIndicator(CurrentPageIndex)) { UpdateIndicatorDuringDrag(currentOffset); }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            isDragging = false;
            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;

            float currentPos = swipeDirection == SwipeDirection.Horizontal ? eventData.position.x : eventData.position.y;
            float startPos = swipeDirection == SwipeDirection.Horizontal ? dragStartPos.x : dragStartPos.y;
            float dragDelta = (currentPos - startPos) / scaleFactor;

            // Invert delta for vertical scrolling to match expected behavior
            if (swipeDirection == SwipeDirection.Vertical) { dragDelta = -dragDelta; }

            float dragDistance = Mathf.Abs(dragDelta);
            float dragPercent = dragDistance / containerSize;
            int targetPage = CurrentPageIndex;
            bool isQuickSwipe = Mathf.Abs(dragVelocity) > velocityThreshold;

            if (isQuickSwipe)
            {
                // Quick swipe - move one page in swipe direction
                if (dragVelocity > 0 && CurrentPageIndex > 0) { targetPage = CurrentPageIndex - 1; }
                else if (dragVelocity < 0 && CurrentPageIndex < pages.Count - 1) { targetPage = CurrentPageIndex + 1; }
            }
            else if (dragPercent >= swipeThreshold)
            {
                // Slow drag past threshold - move one page in drag direction
                if (dragDelta > 0 && CurrentPageIndex > 0) { targetPage = CurrentPageIndex - 1; }
                else if (dragDelta < 0 && CurrentPageIndex < pages.Count - 1) { targetPage = CurrentPageIndex + 1; }
            }

            // Clamp target page
            targetPage = Mathf.Clamp(targetPage, 0, pages.Count - 1);
            OpenPage(targetPage, true);
        }

        void CheckAndSetupNestedScrolling()
        {
            if (!autoHandleNestedScrolling)
                return;

            // Iterate through all defined pages
            foreach (var page in pages)
            {
                if (page.pageObject == null)
                    continue;

                page.pageObject.GetComponentsInChildren<ScrollRect>(true, scrollRectCache);
                for (int i = 0; i < scrollRectCache.Count; i++)
                {
                    var scrollRect = scrollRectCache[i];

                    // Check if the manager is already there
                    if (!scrollRect.TryGetComponent<NestedScrollManager>(out var manager))
                    {
                        // If not, add it
                        manager = scrollRect.gameObject.AddComponent<NestedScrollManager>();

                        // Configure it immediately
                        manager.findParentAutomatically = false;
                        manager.parentPages = this;
                    }
                    else if (manager.parentPages == null)
                    {
                        // If it exists but has no parent assigned, assign this component
                        manager.parentPages = this;
                    }
                }
            }
        }

        void UpdateButtonStates(int selectedTabIndex)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var tab = pages[i];
                if (tab.pageButton != null)
                {
                    // Safe reflection-free state change (assumes custom Button logic as written)
                    tab.pageButton.SetState(i == selectedTabIndex
                        ? InteractionState.Selected
                        : InteractionState.Normal);
                }
            }
        }

        void ApplyOffset(float offset)
        {
            if (container == null)
                return;

            // Move each page instead of the container
            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].pageObject == null)
                    continue;

                float basePosition = i * (containerSize + pageSpacing);

                if (swipeDirection == SwipeDirection.Horizontal)
                    pages[i].pageObject.anchoredPosition = new Vector2(basePosition + offset, 0);
                else
                    pages[i].pageObject.anchoredPosition = new Vector2(0, -(basePosition + offset));
            }
        }

        void UpdatePageVisibility()
        {
            if (!disableInvisiblePages)
                return;

            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].pageObject == null)
                    continue;

                bool shouldBeActive = i == CurrentPageIndex;

                // Keep adjacent pages active for smooth transitions
                if (Mathf.Abs(i - CurrentPageIndex) <= 1)
                    shouldBeActive = true;

                pages[i].pageObject.gameObject.SetActive(shouldBeActive);
            }
        }

        void UpdateIndicatorDuringDrag(float offset)
        {
            if (indicator == null || pages.Count == 0)
                return;

            // Calculate which pages we're between
            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
            float pageSize = containerSize + pageSpacing;
            float normalizedOffset = -offset / pageSize;

            int fromPage = Mathf.FloorToInt(normalizedOffset);
            int toPage = Mathf.CeilToInt(normalizedOffset);

            fromPage = Mathf.Clamp(fromPage, 0, pages.Count - 1);
            toPage = Mathf.Clamp(toPage, 0, pages.Count - 1);

            // Check if both pages have buttons
            bool fromHasButton = pages[fromPage].pageButton != null;
            bool toHasButton = pages[toPage].pageButton != null;

            // Hide indicator if either page is missing a button
            if (!fromHasButton || !toHasButton)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            indicator.gameObject.SetActive(true);
            float t = normalizedOffset - fromPage;

            // Get positions and sizes for interpolation
            Vector2 fromPos = GetIndicatorPositionForPage(fromPage);
            Vector2 toPos = GetIndicatorPositionForPage(toPage);
            Vector2 fromSize = GetIndicatorSizeForPage(fromPage);
            Vector2 toSize = GetIndicatorSizeForPage(toPage);

            // Interpolate
            Vector2 currentPos = Vector2.Lerp(fromPos, toPos, t);
            Vector2 currentSize = Vector2.Lerp(fromSize, toSize, t);

            // Apply to indicator
            indicator.anchoredPosition = currentPos;
            indicator.sizeDelta = currentSize;
        }

        void UpdateIndicator(int pageIndex, bool animate = true)
        {
            if (indicator == null || pages.Count == 0)
                return;

            if (!ShouldShowIndicator(pageIndex))
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            indicator.gameObject.SetActive(true);
            indicatorTargetPos = GetIndicatorPositionForPage(pageIndex);
            indicatorTargetSize = GetIndicatorSizeForPage(pageIndex);

            if (!animate || !Application.isPlaying)
            {
                indicator.anchoredPosition = indicatorTargetPos;
                indicator.sizeDelta = indicatorTargetSize;
            }
        }

        bool ShouldShowIndicator(int pageIndex)
        {
            if (indicator == null || pages.Count == 0)
                return false;

            if (pageIndex < 0 || pageIndex >= pages.Count)
                return false;

            return pages[pageIndex].pageButton != null;
        }

        Vector2 GetIndicatorPositionForPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= pages.Count)
                return Vector2.zero;

            var page = pages[pageIndex];

            // Button must be present (checked by ShouldShowIndicator)
            if (page.pageButton == null) { return Vector2.zero; }
            if (!page.pageButton.TryGetComponent<RectTransform>(out var buttonRect)) { return Vector2.zero; }

            // Get button's center position in world space
            Vector3 buttonWorldCenter = buttonRect.TransformPoint(buttonRect.rect.center);

            // Convert to indicator parent's local space
            RectTransform indicatorParent = indicator.parent as RectTransform;
            if (indicatorParent == null) { return Vector2.zero; }

            // Convert world position to local position in indicator's parent space
            Vector2 localPos = indicatorParent.InverseTransformPoint(buttonWorldCenter);
            return localPos;
        }

        Vector2 GetIndicatorSizeForPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= pages.Count)
                return indicator.sizeDelta;

            var page = pages[pageIndex];

            // Button must be present (checked by ShouldShowIndicator)
            if (page.pageButton == null) { return indicator.sizeDelta; }
            if (!page.pageButton.TryGetComponent<RectTransform>(out var buttonRect)) { return indicator.sizeDelta; }

            // Get the button's actual rect size
            Rect buttonWorldRect = buttonRect.rect;
            return new Vector2(buttonWorldRect.width, buttonWorldRect.height);
        }

        IEnumerator AnimateToPage(float targetPos)
        {
            IsTransitioning = true;
            float startOffset = currentOffset;
            float distance = Mathf.Abs(targetPos - startOffset);
            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;

            // Adjust duration based on distance
            float dynamicDuration = transitionDuration * Mathf.Clamp01(distance / containerSize);
            dynamicDuration = Mathf.Max(dynamicDuration, 0.1f); // Minimum duration

            // Store indicator start values
            Vector2 indicatorStartPos = Vector2.zero;
            Vector2 indicatorStartSize = Vector2.zero;
            
            bool animateIndicator = ShouldShowIndicator(CurrentPageIndex);
            if (animateIndicator)
            {
                indicatorStartPos = indicator.anchoredPosition;
                indicatorStartSize = indicator.sizeDelta;
            }

            float elapsed = 0f;

            while (elapsed < dynamicDuration)
            {
                // Check if dragging started
                if (isDragging && interruptTransitions)
                {
                    IsTransitioning = false;
                    yield break;
                }

                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dynamicDuration);
                float curvedT = transitionCurve.Evaluate(t);

                currentOffset = Mathf.Lerp(startOffset, targetPos, curvedT);
                ApplyOffset(currentOffset);

                // Animate indicator
                if (animateIndicator)
                {
                    indicator.anchoredPosition = Vector2.Lerp(indicatorStartPos, indicatorTargetPos, curvedT);
                    indicator.sizeDelta = Vector2.Lerp(indicatorStartSize, indicatorTargetSize, curvedT);
                }

                yield return null;
            }

            currentOffset = targetPos;
            ApplyOffset(currentOffset);

            // Ensure indicator is at final position
            if (animateIndicator)
            {
                indicator.anchoredPosition = indicatorTargetPos;
                indicator.sizeDelta = indicatorTargetSize;
            }

            UpdatePageVisibility();
            IsTransitioning = false;
        }

        public void PositionPages()
        {
            if (container == null || pages.Count == 0)
                return;

            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;

            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].pageObject == null)
                    continue;

                RectTransform pageRect = pages[i].pageObject;

                // Set anchors to stretch
                pageRect.anchorMin = new Vector2(0, 0);
                pageRect.anchorMax = new Vector2(1, 1);
                pageRect.sizeDelta = Vector2.zero;

                // Position based on index and direction
                float offset = i * (containerSize + pageSpacing);
                if (swipeDirection == SwipeDirection.Horizontal) { pageRect.anchoredPosition = new Vector2(offset, 0); }
                else { pageRect.anchoredPosition = new Vector2(0, -offset); }

                // Set active state
                if (disableInvisiblePages && i != CurrentPageIndex) { pageRect.gameObject.SetActive(false); }
                else { pages[i].pageObject.gameObject.SetActive(true); }

                // Setup button listeners
                if (pages[i].pageButton != null)
                {
                    int index = i; // Capture for closure
                    pages[i].pageButton.onClick.RemoveAllListeners();
                    pages[i].pageButton.onClick.AddListener(() => OpenPage(index));
                }
            }

            // Update indicator visibility
            if (indicator != null)
            {
                bool shouldShow = ShouldShowIndicator(CurrentPageIndex >= 0 ? CurrentPageIndex : defaultPageIndex);
                indicator.gameObject.SetActive(shouldShow);
            }
        }

        public void OpenPage(int index, bool animate = true)
        {
            if (index < 0 || index >= pages.Count)
                return;

            // Allow opening pages during transitions now
            bool pageChanged = CurrentPageIndex != index;
            CurrentPageIndex = index;

            float containerSize = swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
            targetOffset = -index * (containerSize + pageSpacing);

            // Enable target page and adjacent pages
            if (pages[index].pageObject != null) { pages[index].pageObject.gameObject.SetActive(true); }
            if (index > 0 && pages[index - 1].pageObject != null) { pages[index - 1].pageObject.gameObject.SetActive(true); }
            if (index < pages.Count - 1 && pages[index + 1].pageObject != null) { pages[index + 1].pageObject.gameObject.SetActive(true); }

            // Update indicator target
            UpdateIndicator(index, animate);

            if (animate && Application.isPlaying)
            {
                if (transitionCoroutine != null) { StopCoroutine(transitionCoroutine); }
                transitionCoroutine = StartCoroutine(AnimateToPage(targetOffset));
            }
            else
            {
                currentOffset = targetOffset;
                ApplyOffset(currentOffset);
                UpdatePageVisibility();
            }

            // Check for event
            if (pageChanged) { onPageChanged?.Invoke(CurrentPageIndex); }

            // Update buttons
            UpdateButtonStates(CurrentPageIndex);
        }

        public void OpenPage(int index) => OpenPage(index, true);

        public void OpenNextPage()
        {
            if (CurrentPageIndex < pages.Count - 1)
                OpenPage(CurrentPageIndex + 1);
        }

        public void OpenPreviousPage()
        {
            if (CurrentPageIndex > 0)
                OpenPage(CurrentPageIndex - 1);
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
        int lastDefaultTabIndex = -1;
        bool pendingEditorUpdate = false;

        // Cache the delegate to prevent Editor memory leaks from repeated OnValidate calls
        private UnityEditor.EditorApplication.CallbackFunction onValidateDelayCall;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Clamp default tab index
            if (pages.Count > 0) { defaultPageIndex = Mathf.Clamp(defaultPageIndex, 0, pages.Count - 1); }

            // Update editor preview if index changed
            if (lastDefaultTabIndex != defaultPageIndex)
            {
                if (!pendingEditorUpdate)
                {
                    pendingEditorUpdate = true;

                    onValidateDelayCall ??= () =>
                    {
                        if (this != null)
                        {
                            UpdateEditorPreview();
                            pendingEditorUpdate = false;
                        }
                    };

                    UnityEditor.EditorApplication.delayCall -= onValidateDelayCall;
                    UnityEditor.EditorApplication.delayCall += onValidateDelayCall;
                }
                lastDefaultTabIndex = defaultPageIndex;
            }
        }

        void UpdateEditorPreview()
        {
            if (pages == null || pages.Count == 0)
                return;

            // Setup indicator anchor
            if (indicator != null)
            {
                indicator.anchorMin = new Vector2(0.5f, 0.5f);
                indicator.anchorMax = new Vector2(0.5f, 0.5f);
                indicator.pivot = new Vector2(0.5f, 0.5f);

                // Set visibility based on default page
                bool shouldShow = pages.Count > 0 && defaultPageIndex >= 0 && defaultPageIndex < pages.Count
                    && pages[defaultPageIndex].pageButton != null;
                indicator.gameObject.SetActive(shouldShow);
            }

            for (int i = 0; i < pages.Count; i++)
            {
                // Check page object
                var tab = pages[i];
                if (tab.pageObject == null) { continue; }

                // Page visibility
                if (i == defaultPageIndex) { tab.pageObject.gameObject.SetActive(true); }
                else if (tab.pageObject.gameObject.activeInHierarchy) { tab.pageObject.gameObject.SetActive(false); }

                // Button state 
                if (tab.pageButton != null)
                    tab.pageButton.SetState(i == defaultPageIndex 
                        ? InteractionState.Selected 
                        : InteractionState.Normal);
            }

            // Update indicator in editor
            if (ShouldShowIndicator(defaultPageIndex)) { UpdateIndicator(defaultPageIndex, false); }
            else if (indicator != null) { indicator.gameObject.SetActive(false); }
        }
#endif
    }
}