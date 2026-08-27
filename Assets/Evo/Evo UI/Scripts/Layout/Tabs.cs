using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "layout/tabs")]
    [AddComponentMenu("Evo/UI/Layout/Tabs")]
    public class Tabs : MonoBehaviour
    {
        [EvoHeader("Tabs", Constants.CustomEditorID)]
        [SerializeField] private int defaultTabIndex = 0;
        public List<Item> tabs = new();

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool useUnscaledTime = true;
        public bool disableInvisibleTabs = true;

        [EvoHeader("Animation Settings", Constants.CustomEditorID)]
        [SerializeField] private AnimationType animationType = AnimationType.Fade;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0.1f, 1f)] private float animationDuration = 0.3f;
        [SerializeField] private float scaleOutMultiplier = 1.15f;
        [SerializeField] private float scaleInMultiplier = 0.85f;
        [SerializeField] private float slideDistance = 50f;

        [EvoHeader("Title Display", Constants.CustomEditorID)]
        [SerializeField] private TextMeshProUGUI titleObject;
        [SerializeField] private bool animateTitleDirectionally = false;
        [SerializeField] private AnimationCurve titleSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float titleSlideDuration = 0.2f;
        [SerializeField, Range(0f, 0.5f)] private float titleChangeDelay = 0;
        [SerializeField] private Vector2 titleSlideOffset = new(0, -25);

        [EvoHeader("Indicator", Constants.CustomEditorID)]
        [SerializeField] private RectTransform indicatorObject;
        [SerializeField] private IndicatorDirection indicatorDirection = IndicatorDirection.Horizontal;
        [SerializeField] private bool indicatorAutoSize = true;
        [SerializeField] private float indicatorStretch = 50f;
        [SerializeField] private AnimationCurve indicatorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0.01f, 1f)] private float indicatorDuration = 0.3f;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int> onTabChanged = new();

        // Enums
        public enum IndicatorDirection { Horizontal, Vertical }
        public enum AnimationType
        {
            None,
            Fade,
            Scale,
            SlideHorizontal,
            SlideVertical
        }

        // Properties
        public int CurrentTabIndex { get; private set; } = -1;
        public string CurrentTabID => CurrentTabIndex >= 0 && CurrentTabIndex < tabs.Count ? tabs[CurrentTabIndex].tabID : "";
        public Item CurrentTab => CurrentTabIndex >= 0 && CurrentTabIndex < tabs.Count ? tabs[CurrentTabIndex] : null;

        // Cache
        Coroutine indicatorCoroutine;
        Coroutine switchTabCoroutine;
        Coroutine titleCoroutine;
        CanvasGroup titleCanvasGroup;
        WaitForEndOfFrame cachedEndOfFrame;
        const float TabNameAnimSplit = 0.5f;

        // Helpers
        int upcomingTabIndex = -1;
        bool isInitialized = false;
        bool isAnimating = false;
        readonly Dictionary<string, int> tabIDToIndex = new();

        void Awake()
        {
            cachedEndOfFrame = new WaitForEndOfFrame();

            // Get canvas group from title object if available
            if (titleObject != null && !titleObject.TryGetComponent<CanvasGroup>(out _))
                titleCanvasGroup = titleObject.gameObject.AddComponent<CanvasGroup>();
        }

        void Start()
        {
            InitializeTabs();
            OpenTab(defaultTabIndex, false);
        }

        void OnEnable()
        {
            if (!isInitialized)
                return;

            int cachedIndex = upcomingTabIndex;
            CurrentTabIndex = -1;
            upcomingTabIndex = -1;
           
            OpenTab(cachedIndex, false);
        }

        void OnDisable()
        {
            isAnimating = false;

            if (switchTabCoroutine != null)
            {
                StopCoroutine(switchTabCoroutine);
                switchTabCoroutine = null;
            }

            if (titleCoroutine != null)
            {
                StopCoroutine(titleCoroutine);
                titleCoroutine = null;
            }

            if (indicatorCoroutine != null)
            {
                StopCoroutine(indicatorCoroutine);
                indicatorCoroutine = null;
            }
        }

        void InitializeTabs()
        {
            tabIDToIndex.Clear();

            for (int i = 0; i < tabs.Count; i++)
            {
                Item tab = tabs[i];

                // Skip if tab object is null
                if (tab.tabObject == null)
                    continue;

                tab.Initialize();

                // Map tab ID to index (validate unique IDs)
                if (!string.IsNullOrEmpty(tab.tabID))
                {
                    if (tabIDToIndex.ContainsKey(tab.tabID))
                    {
                        Debug.LogWarning($"Duplicate tab ID '{tab.tabID}' found at index {i}. " +
                        $"Each tab should have a unique ID.", this);
                    }
                    else { tabIDToIndex[tab.tabID] = i; }
                }

                // Setup button listeners
                if (tab.tabButton != null)
                {
                    int index = i; // Capture for closure
                    tab.tabButton.onClick.AddListener(() => OpenTab(index));
                }

                // Initially hide all tabs
                tab.SetVisibility(false, disableInvisibleTabs);
            }

            isInitialized = true;
        }

        void UpdateButtonStates(int selectedTabIndex)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                if (tab.tabButton != null)
                    tab.tabButton.SetState(i == selectedTabIndex ? InteractionState.Selected : InteractionState.Normal);
            }
        }

        void SelectUIElement(Item tab)
        {
            if (EventSystem.current == null)
                return;

            GameObject toSelect = null;

            // Try to select the latest selected object first
            if (tab.latestSelected != null && tab.latestSelected.activeInHierarchy) { toSelect = tab.latestSelected; }

            // Fall back to first selected object
            if (toSelect == null && tab.firstSelected != null && tab.firstSelected.activeInHierarchy) { toSelect = tab.firstSelected; }

            // Select the object, if exists
            if (toSelect != null) { EventSystem.current.SetSelectedGameObject(toSelect); }
        }

        bool IsChildOf(Transform child, Transform parent)
        {
            if (child == null || parent == null)
                return false;

            Transform current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;

                current = current.parent;
            }

            return false;
        }

        IEnumerator SwitchTab(int newTabIndex, bool animate = true)
        {
            isAnimating = true;
            upcomingTabIndex = newTabIndex;

            var newTab = tabs[newTabIndex];
            Item currentTab = null;

            if (CurrentTabIndex >= 0 && CurrentTabIndex < tabs.Count)
            {
                currentTab = tabs[CurrentTabIndex];

                // Only proceed if current tab object still exists
                if (currentTab.tabObject != null)
                {
                    // Store latest selected object
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                        && IsChildOf(EventSystem.current.currentSelectedGameObject.transform, currentTab.tabObject.transform))
                    {
                        currentTab.latestSelected = EventSystem.current.currentSelectedGameObject;
                    }
                }
            }

            // Trigger event
            onTabChanged?.Invoke(newTabIndex);

            // Enable new tab if needed
            if (!newTab.tabObject.gameObject.activeInHierarchy) { newTab.tabObject.gameObject.SetActive(true); }

            // Update button states before animation starts for immediate visual feedback
            UpdateButtonStates(newTabIndex);

            // Animate header text
            if (titleObject != null)
            {
                if (titleCoroutine != null) { StopCoroutine(titleCoroutine); }
                titleCoroutine = StartCoroutine(AnimateTabTitle(currentTab, newTab, CurrentTabIndex, newTabIndex));
            }

            // Trigger Indicator Animation
            UpdateIndicator(newTab, animate);

            // Perform animation or snap instantly
            if (animate && animationType != AnimationType.None) { yield return AnimateTabTransition(currentTab, newTab, newTabIndex); }
            else { ApplyInstantTransition(currentTab, newTab); }

            // Disable old tab if needed
            if (currentTab != null && disableInvisibleTabs && currentTab.tabObject != null) { currentTab.tabObject.gameObject.SetActive(false); }

            // Update states and current tab index
            isAnimating = false;
            CurrentTabIndex = newTabIndex;

            // Handle UI navigation
            SelectUIElement(newTab);
        }

        void UpdateIndicator(Item targetTab, bool animate)
        {
            if (indicatorObject == null || !gameObject.activeInHierarchy)
                return;

            if (indicatorCoroutine != null) { StopCoroutine(indicatorCoroutine); }
            indicatorCoroutine = StartCoroutine(AnimateIndicatorRoutine(targetTab, animate));
        }

        IEnumerator AnimateIndicatorRoutine(Item targetTab, bool animate)
        {
            // Wait for end of frame to ensure layout rebuilds if this was called during initialization
            if (!animate)
                yield return cachedEndOfFrame;

            // Get target rect
            RectTransform targetRect = (targetTab != null && targetTab.tabButton != null) ? (RectTransform)targetTab.tabButton.transform : null;

            // Capture current visual state (world space)
            Vector3 worldPos = indicatorObject.position;
            Vector2 startSize = indicatorObject.sizeDelta;
            Vector2 startAnchoredPos;

            // Determine Target Values
            Vector2 targetAnchoredPos;
            Vector2 targetSize = startSize;

            if (targetRect != null)
            {
                // Cache pre-change values for the non-animated axis to prevent floating-point drift
                Vector2 preAnchorPos = indicatorObject.anchoredPosition;
                Vector2 preAnchorSize = indicatorObject.sizeDelta;

                // Apply target anchors and pivot
                if (indicatorDirection == IndicatorDirection.Horizontal)
                {
                    indicatorObject.anchorMin = new Vector2(targetRect.anchorMin.x, indicatorObject.anchorMin.y);
                    indicatorObject.anchorMax = new Vector2(targetRect.anchorMax.x, indicatorObject.anchorMax.y);
                    indicatorObject.pivot = new Vector2(targetRect.pivot.x, indicatorObject.pivot.y);
                }
                else
                {
                    indicatorObject.anchorMin = new Vector2(indicatorObject.anchorMin.x, targetRect.anchorMin.y);
                    indicatorObject.anchorMax = new Vector2(indicatorObject.anchorMax.x, targetRect.anchorMax.y);
                    indicatorObject.pivot = new Vector2(indicatorObject.pivot.x, targetRect.pivot.y);
                }

                // Restore visual position after anchor change
                indicatorObject.position = worldPos;

                // Revert the non-animated axis exactly to prevent cross-axis drift/jitter
                if (indicatorDirection == IndicatorDirection.Horizontal)
                {
                    Vector2 correctedPos = indicatorObject.anchoredPosition;
                    correctedPos.y = preAnchorPos.y;
                    indicatorObject.anchoredPosition = correctedPos;

                    Vector2 correctedSize = indicatorObject.sizeDelta;
                    correctedSize.y = preAnchorSize.y;
                    indicatorObject.sizeDelta = correctedSize;
                }
                else
                {
                    Vector2 correctedPos = indicatorObject.anchoredPosition;
                    correctedPos.x = preAnchorPos.x;
                    indicatorObject.anchoredPosition = correctedPos;

                    Vector2 correctedSize = indicatorObject.sizeDelta;
                    correctedSize.x = preAnchorSize.x;
                    indicatorObject.sizeDelta = correctedSize;
                }

                startAnchoredPos = indicatorObject.anchoredPosition;
                targetAnchoredPos = indicatorObject.anchoredPosition; // Default to current

                // Calculate target position and size based on target rect
                if (indicatorDirection == IndicatorDirection.Horizontal)
                {
                    targetAnchoredPos.x = targetRect.anchoredPosition.x;
                    if (indicatorAutoSize) { targetSize.x = targetRect.sizeDelta.x; }
                }
                else
                {
                    targetAnchoredPos.y = targetRect.anchoredPosition.y;
                    if (indicatorAutoSize) { targetSize.y = targetRect.sizeDelta.y; }
                }
            }
            else
            {
                // Keep existing anchors/pivots to shrink in place.
                startAnchoredPos = indicatorObject.anchoredPosition;
                targetAnchoredPos = startAnchoredPos;

                // Shrink size to 0 on relevant axis
                if (indicatorDirection == IndicatorDirection.Horizontal) { targetSize.x = 0; }
                else { targetSize.y = 0; }
            }

            // Animate or Snap
            if (animate && Application.isPlaying)
            {
                float elapsed = 0f;
                while (elapsed < indicatorDuration)
                {
                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / indicatorDuration);
                    float curveValue = indicatorCurve.Evaluate(t);

                    // Calculate stretch based on progression
                    // Only stretch if there's a target, otherwise just shrink (disappear)
                    float stretchValue = (targetRect != null) ? Mathf.Sin(Mathf.Clamp01(curveValue) * Mathf.PI) * indicatorStretch : 0f;

                    // Lerp specific axis only
                    Vector2 currentPos = indicatorObject.anchoredPosition;
                    Vector2 currentSize = indicatorObject.sizeDelta;

                    if (indicatorDirection == IndicatorDirection.Horizontal)
                    {
                        currentPos.x = Mathf.Lerp(startAnchoredPos.x, targetAnchoredPos.x, curveValue);
                        currentSize.x = Mathf.Lerp(startSize.x, targetSize.x, curveValue) + stretchValue;
                    }
                    else
                    {
                        currentPos.y = Mathf.Lerp(startAnchoredPos.y, targetAnchoredPos.y, curveValue);
                        currentSize.y = Mathf.Lerp(startSize.y, targetSize.y, curveValue) + stretchValue;
                    }

                    indicatorObject.anchoredPosition = currentPos;
                    indicatorObject.sizeDelta = currentSize;
                    yield return null;
                }
            }

            // Ensure exact snap at end
            Vector2 finalPos = indicatorObject.anchoredPosition;
            Vector2 finalSize = indicatorObject.sizeDelta;

            if (indicatorDirection == IndicatorDirection.Horizontal)
            {
                finalPos.x = targetAnchoredPos.x;
                finalSize.x = targetSize.x;
            }
            else
            {
                finalPos.y = targetAnchoredPos.y;
                finalSize.y = targetSize.y;
            }

            indicatorObject.anchoredPosition = finalPos;
            indicatorObject.sizeDelta = finalSize;
        }

        IEnumerator AnimateTabTitle(Item previousTab, Item newTab, int previousIndex, int newIndex)
        {
            if (titleObject == null || titleCanvasGroup == null)
                yield break;

            float halfDuration = titleSlideDuration * TabNameAnimSplit;

            // Calculate effective offset based on direction if enabled
            Vector2 currentSlideOffset = titleSlideOffset;
            if (animateTitleDirectionally && newIndex < previousIndex) { currentSlideOffset = -titleSlideOffset; }

            // Animate out (previous tab name)
            if (previousTab != null)
            {
                Vector2 startPos = titleObject.rectTransform.anchoredPosition;
                float startAlpha = titleCanvasGroup.alpha;

                float elapsed = 0f;
                while (elapsed < halfDuration)
                {
                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float progress = elapsed / halfDuration;
                    float curveValue = titleSlideCurve.Evaluate(progress);

                    titleObject.rectTransform.anchoredPosition = Vector2.Lerp(startPos, startPos - currentSlideOffset, curveValue);
                    titleCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);

                    yield return null;
                }

                titleObject.rectTransform.anchoredPosition = startPos - currentSlideOffset;
                titleCanvasGroup.alpha = 0f;
            }

            if (titleChangeDelay > 0)
            {
                float delayElapsed = 0f;
                while (delayElapsed < titleChangeDelay)
                {
                    delayElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            // Update text to new tab name
            if (newTab.tabButton != null && newTab.tabButton.textObject != null) { titleObject.text = newTab.tabButton.textObject.text; }
            else { titleObject.text = newTab.tabID; }

            // Animate in (new tab name)
            Vector2 targetPos = Vector2.zero;
            titleObject.rectTransform.anchoredPosition = currentSlideOffset;
            titleCanvasGroup.alpha = 0f;

            float elapsedIn = 0f;
            while (elapsedIn < halfDuration)
            {
                elapsedIn += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsedIn / halfDuration;

                titleObject.rectTransform.anchoredPosition = Vector2.Lerp(currentSlideOffset, targetPos, progress);
                titleCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);

                yield return null;
            }

            titleObject.rectTransform.anchoredPosition = targetPos;
            titleCanvasGroup.alpha = 1f;
        }

        void ApplyInstantTransition(Item currentTab, Item newTab)
        {
            if (currentTab != null)
            {
                CanvasGroup currentCanvasGroup = currentTab.GetCanvasGroup();
                currentCanvasGroup.alpha = 0f;
                currentCanvasGroup.interactable = false;
                currentCanvasGroup.blocksRaycasts = false;
            }

            if (newTab != null)
            {
                CanvasGroup newCanvasGroup = newTab.GetCanvasGroup();
                newCanvasGroup.alpha = 1f;
                newCanvasGroup.interactable = true;
                newCanvasGroup.blocksRaycasts = true;
            }
        }

        IEnumerator AnimateTabTransition(Item currentTab, Item newTab, int newTabIndex)
        {
            switch (animationType)
            {
                case AnimationType.None:
                    ApplyInstantTransition(currentTab, newTab);
                    yield break;
                case AnimationType.Fade:
                    yield return AnimateFade(currentTab, newTab);
                    break;
                case AnimationType.Scale:
                    yield return AnimateScale(currentTab, newTab);
                    break;
                case AnimationType.SlideHorizontal:
                    yield return AnimateSlideHorizontal(currentTab, newTab, newTabIndex);
                    break;
                case AnimationType.SlideVertical:
                    yield return AnimateSlideVertical(currentTab, newTab, newTabIndex);
                    break;
            }
        }

        IEnumerator AnimateFade(Item currentTab, Item newTab)
        {
            float halfDuration = animationDuration / 2f;

            // Fade out current tab
            if (currentTab != null)
            {
                CanvasGroup currentCanvasGroup = currentTab.GetCanvasGroup();

                yield return AnimateAlpha(currentCanvasGroup, currentCanvasGroup.alpha, 0, halfDuration);

                // Done post-yield to avoid lambda allocation
                currentCanvasGroup.interactable = false;
                currentCanvasGroup.blocksRaycasts = false;
            }

            // Fade in new tab
            CanvasGroup newCanvasGroup = newTab.GetCanvasGroup();
            newCanvasGroup.alpha = 0;
            newCanvasGroup.interactable = true;
            newCanvasGroup.blocksRaycasts = true;

            yield return AnimateAlpha(newCanvasGroup, 0, 1, halfDuration);
        }

        IEnumerator AnimateScale(Item currentTab, Item newTab)
        {
            float halfDuration = animationDuration / 2f;

            // Scale out and fade out current tab
            if (currentTab != null)
            {
                CanvasGroup currentCanvasGroup = currentTab.GetCanvasGroup();
                Vector3 startScale = currentTab.tabObject.localScale;
                Vector3 targetScale = startScale * scaleOutMultiplier;
                float startAlpha = currentCanvasGroup.alpha;

                yield return AnimateScaleAndAlpha(currentTab.tabObject, currentCanvasGroup, startScale, targetScale, startAlpha, 0f, halfDuration);

                // Done post-yield to avoid lambda allocation
                currentCanvasGroup.interactable = false;
                currentCanvasGroup.blocksRaycasts = false;
                currentTab.tabObject.localScale = Vector3.one;
            }

            // Scale in and fade in new tab
            CanvasGroup newCanvasGroup = newTab.GetCanvasGroup();
            Vector3 startScaleNew = Vector3.one * scaleInMultiplier;
            Vector3 targetScaleNew = Vector3.one;

            newTab.tabObject.localScale = startScaleNew;
            newCanvasGroup.alpha = 0f;
            newCanvasGroup.interactable = true;
            newCanvasGroup.blocksRaycasts = true;

            yield return AnimateScaleAndAlpha(newTab.tabObject, newCanvasGroup, startScaleNew, targetScaleNew, 0f, 1f, halfDuration);
        }

        IEnumerator AnimateSlideHorizontal(Item currentTab, Item newTab, int newTabIndex)
        {
            float halfDuration = animationDuration / 2f;
            bool slideLeft = newTabIndex > CurrentTabIndex;

            // Slide out current tab
            if (currentTab != null)
            {
                CanvasGroup currentCanvasGroup = currentTab.GetCanvasGroup();
                Vector2 startPos = currentTab.tabObject.anchoredPosition;
                Vector2 targetPos = startPos;
                targetPos.x += slideLeft ? -slideDistance : slideDistance;
                float startAlpha = currentCanvasGroup.alpha;

                yield return AnimatePositionAndAlpha(currentTab.tabObject, currentCanvasGroup, startPos, targetPos, startAlpha, 0f, halfDuration);

                // Done post-yield to avoid lambda allocation
                currentCanvasGroup.interactable = false;
                currentCanvasGroup.blocksRaycasts = false;
                currentTab.RestoreOriginalPosition();
            }

            // Slide in new tab
            CanvasGroup newCanvasGroup = newTab.GetCanvasGroup();
            Vector2 startPosNew = newTab.originalAnchoredPosition;
            startPosNew.x += slideLeft ? slideDistance : -slideDistance;
            Vector2 targetPosNew = newTab.originalAnchoredPosition;

            newTab.tabObject.anchoredPosition = startPosNew;
            newCanvasGroup.alpha = 0f;
            newCanvasGroup.interactable = true;
            newCanvasGroup.blocksRaycasts = true;

            yield return AnimatePositionAndAlpha(newTab.tabObject, newCanvasGroup, startPosNew, targetPosNew, 0f, 1f, halfDuration);
        }

        IEnumerator AnimateSlideVertical(Item currentTab, Item newTab, int newTabIndex)
        {
            float halfDuration = animationDuration / 2f;
            bool slideUp = newTabIndex > CurrentTabIndex;

            // Slide out current tab
            if (currentTab != null)
            {
                CanvasGroup currentCanvasGroup = currentTab.GetCanvasGroup();
                Vector2 startPos = currentTab.tabObject.anchoredPosition;
                Vector2 targetPos = startPos;
                targetPos.y += slideUp ? slideDistance : -slideDistance;
                float startAlpha = currentCanvasGroup.alpha;

                yield return AnimatePositionAndAlpha(currentTab.tabObject, currentCanvasGroup, startPos, targetPos, startAlpha, 0f, halfDuration);

                // Done post-yield to avoid lambda allocation
                currentCanvasGroup.interactable = false;
                currentCanvasGroup.blocksRaycasts = false;
                currentTab.RestoreOriginalPosition();
            }

            // Slide in new tab
            CanvasGroup newCanvasGroup = newTab.GetCanvasGroup();
            Vector2 startPosNew = newTab.originalAnchoredPosition;
            startPosNew.y += slideUp ? -slideDistance : slideDistance;
            Vector2 targetPosNew = newTab.originalAnchoredPosition;

            newTab.tabObject.anchoredPosition = startPosNew;
            newCanvasGroup.alpha = 0f;
            newCanvasGroup.interactable = true;
            newCanvasGroup.blocksRaycasts = true;

            yield return AnimatePositionAndAlpha(newTab.tabObject, newCanvasGroup, startPosNew, targetPosNew, 0f, 1f, halfDuration);
        }

        IEnumerator AnimateAlpha(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
        {
            if (canvasGroup == null)
                yield break;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / duration;
                float curveValue = animationCurve.Evaluate(t);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        IEnumerator AnimateScaleAndAlpha(RectTransform rectTransform, CanvasGroup canvasGroup,
            Vector3 startScale, Vector3 targetScale, float startAlpha, float targetAlpha, float duration)
        {
            if (rectTransform == null || canvasGroup == null)
                yield break;

            float elapsedTime = 0f;
            while (elapsedTime < duration && rectTransform != null && canvasGroup != null)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / duration;
                float curveValue = animationCurve.Evaluate(t);

                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);

                yield return null;
            }

            rectTransform.localScale = targetScale;
            canvasGroup.alpha = targetAlpha;
        }

        IEnumerator AnimatePositionAndAlpha(RectTransform rectTransform, CanvasGroup canvasGroup,
            Vector2 startPos, Vector2 targetPos, float startAlpha, float targetAlpha, float duration)
        {
            if (rectTransform == null || canvasGroup == null)
                yield break;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / duration;
                float curveValue = animationCurve.Evaluate(t);

                rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveValue);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);

                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
            canvasGroup.alpha = targetAlpha;
        }

        public void OpenTab(string tabID, bool animate = true)
        {
            if (string.IsNullOrEmpty(tabID))
                return;

            if (tabIDToIndex.TryGetValue(tabID, out int index)) { OpenTab(index, animate); }
            else { Debug.LogWarning($"Tab with ID '{tabID}' not found! Available tabs: {string.Join(", ", tabIDToIndex.Keys)}", this); }
        }

        public void OpenTab(int tabIndex, bool animate = true)
        {
            if (tabs.Count == 0 || tabIndex < 0 || tabIndex >= tabs.Count || tabs[tabIndex].tabObject == null)
                return;

            if (!isInitialized)
            {
                defaultTabIndex = tabIndex;
                return;
            }

            if (upcomingTabIndex == tabIndex)
                return;

            // If tabs are already in transition progress, modify visibility and state
            if (isAnimating)
            {
                // Stop explicitly tracked coroutines rather than blindly hitting StopAllCoroutines
                if (switchTabCoroutine != null) { StopCoroutine(switchTabCoroutine); switchTabCoroutine = null; }
                if (titleCoroutine != null) { StopCoroutine(titleCoroutine); titleCoroutine = null; }

                // This index will be swapped soon, but beforehand, restore to its originals to ensure smooth animation
                tabs[upcomingTabIndex].RestoreOriginalPosition();
                tabs[upcomingTabIndex].SetVisibility(true);

                // In case the user is spamming, make sure to disable unnecessary tabs
                for (int i = 0; i < tabs.Count; i++)
                {
                    if (i == upcomingTabIndex || tabs[i].tabObject == null)
                        continue;

                    tabs[i].RestoreOriginalPosition();
                    tabs[i].SetVisibility(false, disableInvisibleTabs);
                }

                // Swap the indexes
                CurrentTabIndex = upcomingTabIndex;
            }

            // Start the switch process
            if (gameObject.activeInHierarchy){   switchTabCoroutine = StartCoroutine(SwitchTab(tabIndex, animate));  }
            else
            {
                CurrentTab?.tabObject.gameObject.SetActive(false);

                // Queue for OnEnable
                upcomingTabIndex = tabIndex;

                // Snap indicator logically
                if (upcomingTabIndex >= 0 && upcomingTabIndex < tabs.Count)
                    UpdateIndicator(tabs[upcomingTabIndex], false);
            }
        }

        public void OpenNextTab()
        {
            int newIndex = CurrentTabIndex + 1;
            if (newIndex < tabs.Count) { OpenTab(newIndex); }
        }

        public void OpenPreviousTab()
        {
            int newIndex = CurrentTabIndex - 1;
            if (newIndex >= 0) { OpenTab(newIndex); }
        }

        public void OpenFirstTab() => OpenTab(0);
        public void OpenLastTab() => OpenTab(tabs.Count - 1);
        public void OpenTab(string tabID) => OpenTab(tabID, true);
        public void OpenTab(int tabIndex) => OpenTab(tabIndex, true);

        public void AddTab(Item newTab)
        {
            tabs.Add(newTab);
            newTab.Initialize();

            if (!string.IsNullOrEmpty(newTab.tabID))
                tabIDToIndex[newTab.tabID] = tabs.Count - 1;

            if (newTab.tabButton != null)
            {
                int index = tabs.Count - 1;
                newTab.tabButton.onClick.AddListener(() => OpenTab(index));
            }
        }

        public void RemoveTab(int index)
        {
            if (index < 0 || index >= tabs.Count)
                return;

            var tabToRemove = tabs[index];

            // If removing current tab, switch to another
            if (CurrentTabIndex > index) { CurrentTabIndex--; }
            else if (CurrentTabIndex == index)
            {
                int newIndex = index > 0 ? index - 1 : (tabs.Count > 1 ? 1 : -1);
                if (newIndex >= 0) { OpenTab(newIndex); }
            }

            // Remove from dictionary
            if (!string.IsNullOrEmpty(tabToRemove.tabID))
                tabIDToIndex.Remove(tabToRemove.tabID);

            // Remove from list
            tabs.RemoveAt(index);

            // Update dictionary indices
            tabIDToIndex.Clear();
            for (int i = 0; i < tabs.Count; i++)
            {
                if (!string.IsNullOrEmpty(tabs[i].tabID))
                    tabIDToIndex[tabs[i].tabID] = i;
            }
        }

        public void RemoveTab(string tabID)
        {
            if (tabIDToIndex.TryGetValue(tabID, out int index))
                RemoveTab(index);
        }

        /// <summary>
        /// Instantly refreshes the indicator's position and size to match the current tab.
        /// Useful when tab button sizes change dynamically (e.g., localization or layout rebuilds).
        /// </summary>
        public void RefreshIndicator()
        {
            if (CurrentTab != null)
                UpdateIndicator(CurrentTab, false);
        }

        /// <summary>
        /// Refreshes title object based on the current tab index.
        /// Useful when the title text needs a force refresh (e.g., language changes).
        /// </summary>
        public void RefreshTitleObject()
        {
            if (titleObject == null)
                return;

            if (CurrentTab != null && CurrentTab.tabButton != null && CurrentTab.tabButton.textObject != null)
                titleObject.text = CurrentTab.tabButton.textObject.text;
            else
                titleObject.text = CurrentTab.tabID;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
        int lastDefaultTabIndex = -1;
        bool pendingEditorUpdate = false;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Clamp default tab index
            if (tabs.Count > 0) { defaultTabIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1); }
            else { defaultTabIndex = 0; }

            // Update editor preview if index changed - defer to avoid SendMessage warnings
            if (lastDefaultTabIndex != defaultTabIndex)
            {
                if (!pendingEditorUpdate)
                {
                    pendingEditorUpdate = true;
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            UpdateEditorPreview();
                            pendingEditorUpdate = false;
                        }
                    };
                }
                lastDefaultTabIndex = defaultTabIndex;
            }
        }

        void UpdateEditorPreview()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            for (int i = 0; i < tabs.Count; i++)
            {
                // Check tab object
                var tab = tabs[i];
                if (tab.tabObject == null) { continue; }

                // Get canvas group
                if (!tab.tabObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
                    canvasGroup = tab.tabObject.gameObject.AddComponent<CanvasGroup>();

                // Tab visibility
                if (i == defaultTabIndex)
                {
                    // Show selected tab
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    if (!tab.tabObject.gameObject.activeInHierarchy) { tab.tabObject.gameObject.SetActive(true); }

                    // Update Indicator Preview
                    UpdateIndicator(tab, false);
                }
                else
                {
                    if (disableInvisibleTabs && tab.tabObject.gameObject.activeInHierarchy) { tab.tabObject.gameObject.SetActive(false); }
                    else if (!disableInvisibleTabs)
                    {
                        canvasGroup.alpha = 0f;
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                    }
                }

                // Button state 
                if (tab.tabButton != null)
                    tab.tabButton.SetState(i == defaultTabIndex ? InteractionState.Selected : InteractionState.Normal);
            }

            // Update tab name display in editor
            if (titleObject != null && defaultTabIndex >= 0 && defaultTabIndex < tabs.Count)
                titleObject.text = tabs[defaultTabIndex].tabID;
        }
#endif
    }

    [System.Serializable]
    public class Item
    {
        public string tabID;
        public RectTransform tabObject;
        public Button tabButton;

        [Header("UI Navigation")]
        public GameObject firstSelected;

        // Cache
        [HideInInspector] public GameObject latestSelected;
        [HideInInspector] public CanvasGroup canvasGroup;
        [HideInInspector] public Vector2 originalAnchoredPosition;
        [HideInInspector] public Vector2 originalAnchorMin;
        [HideInInspector] public Vector2 originalAnchorMax;
        [HideInInspector] public Vector2 originalOffsetMin;
        [HideInInspector] public Vector2 originalOffsetMax;

        public CanvasGroup GetCanvasGroup()
        {
            // Check if attached first
            if (canvasGroup != null)
                return canvasGroup;

            // If not, get or create
            if (!tabObject.TryGetComponent(out canvasGroup))
                canvasGroup = tabObject.gameObject.AddComponent<CanvasGroup>();

            return canvasGroup;
        }

        public void Initialize()
        {
            GetCanvasGroup();
            StoreOriginalPositionData();
        }

        public void StoreOriginalPositionData()
        {
            originalAnchoredPosition = tabObject.anchoredPosition;
            originalAnchorMin = tabObject.anchorMin;
            originalAnchorMax = tabObject.anchorMax;
            originalOffsetMin = tabObject.offsetMin;
            originalOffsetMax = tabObject.offsetMax;
        }

        public void RestoreOriginalPosition()
        {
            tabObject.anchoredPosition = originalAnchoredPosition;
            tabObject.anchorMin = originalAnchorMin;
            tabObject.anchorMax = originalAnchorMax;
            tabObject.offsetMin = originalOffsetMin;
            tabObject.offsetMax = originalOffsetMax;
        }

        public void SetVisibility(bool visible, bool respectDisableInvisibleTabs = true)
        {
            CanvasGroup canvasGroup = GetCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            // Handle GameObject activation
            if (visible && !tabObject.gameObject.activeInHierarchy) { tabObject.gameObject.SetActive(true); }
            else if (respectDisableInvisibleTabs && tabObject.gameObject.activeInHierarchy) { tabObject.gameObject.SetActive(false); }
        }
    }
}