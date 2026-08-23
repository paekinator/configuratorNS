using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "layout/scroll-rect")]
    [AddComponentMenu("Evo/UI/Layout/Scroll Rect")]
    public class ScrollRect : UnityEngine.UI.ScrollRect
    {
        [EvoHeader("Snapping", Constants.CustomEditorID)]
        public bool enableSnapping = false;
        [Range(0.05f, 2f)] public float snapDuration = 0.3f;
        public AnimationCurve snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool disableUnfocused = false;

        [EvoHeader("Scaling", Constants.CustomEditorID)]
        public bool enableScaling = false;
        [Range(0f, 1f)] public float minScale = 0.7f;
        public float scaleDistance = 200f;

        [EvoHeader("Fading", Constants.CustomEditorID)]
        public bool enableFading = false;
        [Range(0f, 1f)] public float minAlpha = 0.3f;
        public float fadeDistance = 150f;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int> onItemFocused = new();

        // Constants
        const float MovementThreshold = 0.01f;

        // Helpers
        readonly List<ItemData> items = new();
        readonly int startingIndex = 0;
        int focusedIndex = -1;
        int lastFocusedIndex = -1;
        bool isDragging;
        bool isSnapping;
        Vector2 snapTarget;
        Vector2 snapStartPosition;
        float snapStartTime;
        Vector2 lastContentPosition;

        // Cache
        bool hasVisualEffects;
        float invScaleDistance;
        float invFadeDistance;
        Coroutine snapCoroutine;
        Coroutine initialEffectsCoroutine;
        Coroutine startingSnapCoroutine;
        WaitForEndOfFrame cachedEndOfFrame;
        readonly Stack<ItemData> itemDataPool = new();

        class ItemData
        {
            public RectTransform rectTransform;
            public CanvasGroup canvasGroup;
            public float distance;
            public float distReposition;
        }

        protected override void Awake()
        {
            base.Awake();

            cachedEndOfFrame = new WaitForEndOfFrame();
            if (Application.isPlaying) { CacheInverseDistances(); }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (Application.isPlaying)
            {
                // Prevent duplicate listeners when domain reload is disabled
                onValueChanged.RemoveListener(OnScrollValueChanged);
                onValueChanged.AddListener(OnScrollValueChanged);
            }
        }

        protected override void Start()
        {
            base.Start();

            if (Application.isPlaying)
            {
                RefreshItems();
                if (enableSnapping && items.Count > 0 && startingIndex >= 0 && startingIndex < items.Count)
                {
                    inertia = false;
                    movementType = MovementType.Unrestricted;
                    startingSnapCoroutine = StartCoroutine(SnapToStartingElement());
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Application.isPlaying)
            {
                onValueChanged.RemoveListener(OnScrollValueChanged);

                StopSnapCoroutine();

                // Clean up any pending visual/startup routines
                if (initialEffectsCoroutine != null) { StopCoroutine(initialEffectsCoroutine); initialEffectsCoroutine = null; }
                if (startingSnapCoroutine != null) { StopCoroutine(startingSnapCoroutine); startingSnapCoroutine = null; }

                isDragging = false;
                isSnapping = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (Application.isPlaying)
                onItemFocused?.RemoveAllListeners();
        }

        public override void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnBeginDrag(eventData);

            if (Application.isPlaying)
            {
                isDragging = true;
                StopSnapCoroutine();
            }
        }

        public override void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnEndDrag(eventData);

            if (Application.isPlaying)
            {
                isDragging = false;
                if (enableSnapping) { StartSnapCoroutine(); }
            }
        }

        ItemData GetItemData()
        {
            var data = itemDataPool.Count > 0 ? itemDataPool.Pop() : new ItemData();
            data.distance = 0;
            data.distReposition = 0;
            data.canvasGroup = null;
            data.rectTransform = null;
            return data;
        }

        void ReleaseItemData(ItemData data) => itemDataPool.Push(data);

        void CacheInverseDistances()
        {
            invScaleDistance = scaleDistance > 0 ? 1f / scaleDistance : 0f;
            invFadeDistance = fadeDistance > 0 ? 1f / fadeDistance : 0f;
            hasVisualEffects = enableFading || enableScaling;
        }

        void OnScrollValueChanged(Vector2 position)
        {
            // Only update if content has moved significantly
            if (Vector2.SqrMagnitude(content.anchoredPosition - lastContentPosition) > MovementThreshold)
            {
                UpdateItemStates();
                lastContentPosition = content.anchoredPosition;
            }
        }

        void StartSnapCoroutine() => snapCoroutine ??= StartCoroutine(HandleSnappingCoroutine());

        void StopSnapCoroutine()
        {
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
                snapCoroutine = null;
            }
        }

        IEnumerator SnapToStartingElement()
        {
            yield return cachedEndOfFrame;
            SnapToElementInstant(startingIndex);
        }

        IEnumerator HandleSnappingCoroutine()
        {
            if (!enableSnapping || items.Count == 0)
            {
                snapCoroutine = null;
                yield break;
            }

            // Handle external snapping (smooth snap to specific element)
            if (isSnapping)
            {
                snapStartTime = Time.unscaledTime;
                snapStartPosition = content.anchoredPosition;

                while (isSnapping)
                {
                    float elapsed = Time.unscaledTime - snapStartTime;
                    float t = Mathf.Clamp01(elapsed / snapDuration);
                    float curveValue = snapCurve.Evaluate(t);

                    content.anchoredPosition = Vector2.Lerp(
                        snapStartPosition,
                        snapTarget,
                        curveValue
                    );

                    if (t >= 1f)
                    {
                        content.anchoredPosition = snapTarget;
                        isSnapping = false;
                    }

                    UpdateItemStates();
                    yield return null;
                }
            }
            // Regular snapping to nearest element after drag
            else
            {
                yield return null;

                if (focusedIndex < 0 || focusedIndex >= items.Count)
                {
                    snapCoroutine = null;
                    yield break;
                }

                var item = items[focusedIndex];
                float targetPosition = vertical
                    ? content.anchoredPosition.y + item.distReposition
                    : content.anchoredPosition.x + item.distReposition;

                Vector2 startPosition = content.anchoredPosition;
                Vector2 endPosition = vertical ?
                    new Vector2(content.anchoredPosition.x, targetPosition) :
                    new Vector2(targetPosition, content.anchoredPosition.y);

                snapStartTime = Time.unscaledTime;

                while (true)
                {
                    float elapsed = Time.unscaledTime - snapStartTime;
                    float t = Mathf.Clamp01(elapsed / snapDuration);
                    float curveValue = snapCurve.Evaluate(t);

                    content.anchoredPosition = Vector2.Lerp(
                        startPosition,
                        endPosition,
                        curveValue
                    );

                    UpdateItemStates();

                    if (t >= 1f)
                    {
                        content.anchoredPosition = endPosition;
                        break;
                    }

                    yield return null;
                }
            }

            snapCoroutine = null;
        }

        void UpdateItemStates()
        {
            if (items.Count == 0 || viewport == null || content == null)
                return;

            // Remove destroyed items
            int removedCount = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].rectTransform == null)
                {
                    ReleaseItemData(items[i]);
                    items.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                if (items.Count == 0)
                    return;

                focusedIndex = Mathf.Clamp(focusedIndex, 0, items.Count - 1);
                lastFocusedIndex = -1; // force re-evaluation
            }

            // Get viewport center in world space first
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);
            float minDistance = float.MaxValue;
            int newFocusedIndex = -1;

            // Single loop to calculate distances and find minimum
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // Transform.position uses the pivot, but we need the center
                Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

                // Convert both to content's local space for accurate distance calculation
                Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
                Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

                // Calculate distance based on orientation
                if (vertical) { item.distReposition = viewportLocalPos.y - itemLocalCenter.y; }
                else { item.distReposition = viewportLocalPos.x - itemLocalCenter.x; }

                item.distance = Mathf.Abs(item.distReposition);

                // Find minimum distance in same loop
                if (item.distance < minDistance)
                {
                    minDistance = item.distance;
                    newFocusedIndex = i;
                }
            }

            // Apply visual effects only if enabled
            if (hasVisualEffects)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    bool isFocused = (i == newFocusedIndex);

                    // Apply fading
                    if (enableFading) { ApplyFadeEffect(item, item.distance); }

                    // Apply scaling
                    if (enableScaling)
                    {
                        float normalizedDistance = Mathf.Clamp01(item.distance * invScaleDistance);
                        float scale = Mathf.Lerp(1f, minScale, normalizedDistance);
                        item.rectTransform.localScale = new Vector3(scale, scale, scale);
                    }

                    // Set interactability
                    if (enableSnapping && disableUnfocused && item.canvasGroup != null) { item.canvasGroup.interactable = isFocused; }
                }
            }

            // Update focused index
            if (!isSnapping) { focusedIndex = newFocusedIndex; }

            // Trigger event only when focus changes and not dragging
            if (!isDragging && focusedIndex != lastFocusedIndex && focusedIndex >= 0)
            {
                lastFocusedIndex = focusedIndex;
                onItemFocused?.Invoke(focusedIndex);
            }
        }

        void ApplyFadeEffect(ItemData item, float distance)
        {
            if (item.canvasGroup != null)
            {
                float normalizedDistance = Mathf.Clamp01(distance * invFadeDistance);
                item.canvasGroup.alpha = Mathf.Lerp(1f, minAlpha, normalizedDistance);
            }
        }

        public void RefreshItems()
        {
            // Recycle items
            foreach (var item in items) { ReleaseItemData(item); }
            items.Clear();

            if (content == null)
                return;

            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                {
                    var rectTransform = child.GetComponent<RectTransform>();
                    if (rectTransform)
                    {
                        var itemData = GetItemData();
                        itemData.rectTransform = rectTransform;

                        // Pre-cache or create CanvasGroup if visual effects are enabled
                        if (enableFading || (enableScaling && disableUnfocused))
                        {
                            itemData.canvasGroup = rectTransform.GetComponent<CanvasGroup>();
                            if (itemData.canvasGroup == null)
                            {
                                itemData.canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();
                            }
                        }

                        items.Add(itemData);
                    }
                }
            }

            CacheInverseDistances();
            lastContentPosition = content.anchoredPosition;

            // Apply initial visual effects after refreshing
            if (Application.isPlaying && items.Count > 0)
            {
                if (initialEffectsCoroutine != null) { StopCoroutine(initialEffectsCoroutine); }
                initialEffectsCoroutine = StartCoroutine(ApplyInitialEffects());
            }
        }

        IEnumerator ApplyInitialEffects()
        {
            yield return cachedEndOfFrame;
            UpdateItemStates();
        }

        public void AddTransform(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            var itemData = GetItemData();
            itemData.rectTransform = rectTransform;

            if (enableFading || disableUnfocused)
            {
                itemData.canvasGroup = rectTransform.GetComponent<CanvasGroup>();
                if (itemData.canvasGroup == null) { itemData.canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>(); }
            }

            items.Add(itemData);
        }

        public void RemoveTransform(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].rectTransform == rectTransform)
                {
                    ReleaseItemData(items[i]);
                    items.RemoveAt(i);
                }
            }

            focusedIndex = Mathf.Clamp(focusedIndex, 0, Mathf.Max(0, items.Count - 1));
            lastFocusedIndex = -1;
        }

        public void InstantiateObject(GameObject rectObject, Transform parent)
        {
            if (rectObject == null)
                return;

            GameObject createdObj = Instantiate(rectObject, parent);

            var rectTransform = createdObj.GetComponent<RectTransform>();
            if (rectTransform) { AddTransform(rectTransform); }
        }

        public void SnapToElement(int index, float offset = 0)
        {
            if (!enableSnapping || index < 0 || index >= items.Count || viewport == null)
                return;

            var item = items[index];

            // Store current content position
            Vector2 currentContentPos = content.anchoredPosition;

            // Temporarily set content to zero to get absolute positions
            content.anchoredPosition = Vector2.zero;

            // Get viewport center in world space
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);

            // Get item center in world space
            Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

            // Convert to content local space
            Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
            Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

            // Calculate the absolute offset needed
            Vector2 calculatedOffset = viewportLocalPos - itemLocalCenter;

            if (vertical) { snapTarget = new Vector2(currentContentPos.x, calculatedOffset.y + offset); }
            else { snapTarget = new Vector2(calculatedOffset.x + offset, currentContentPos.y); }

            // Restore original position before starting animation
            content.anchoredPosition = currentContentPos;

            isSnapping = true;
            focusedIndex = index;

            StopSnapCoroutine();
            StartSnapCoroutine();
        }

        public void SnapToElementInstant(int index, float offset = 0)
        {
            if (index < 0 || index >= items.Count || viewport == null)
                return;

            var item = items[index];

            // Store current content position
            Vector2 currentContentPos = content.anchoredPosition;

            // Temporarily set content to zero to get absolute positions
            content.anchoredPosition = Vector2.zero;

            // Get viewport center in world space
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);

            // Get item center in world space
            Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

            // Convert to content local space
            Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
            Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

            // Calculate the absolute offset needed
            Vector2 calculatedOffset = viewportLocalPos - itemLocalCenter;

            if (vertical) { content.anchoredPosition = new Vector2(currentContentPos.x, calculatedOffset.y + offset); }
            else { content.anchoredPosition = new Vector2(calculatedOffset.x + offset, currentContentPos.y); }

            focusedIndex = index;
            isSnapping = false;
            lastContentPosition = content.anchoredPosition;
            UpdateItemStates();
        }

        public void SnapToElement(RectTransform targetRect)
        {
            if (!targetRect)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].rectTransform == targetRect)
                {
                    SnapToElement(i);
                    return;
                }
            }
        }

        public void ScrollToNext()
        {
            if (enableSnapping && focusedIndex < items.Count - 1)
                SnapToElement(focusedIndex + 1);
        }

        public void ScrollToPrevious()
        {
            if (enableSnapping && focusedIndex > 0)
                SnapToElement(focusedIndex - 1);
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool styleFoldout = true;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}