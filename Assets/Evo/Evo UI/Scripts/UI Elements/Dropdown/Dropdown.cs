using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "ui-elements/dropdown")]
    [AddComponentMenu("Evo/UI/UI Elements/Dropdown")]
    public class Dropdown : MonoBehaviour
    {
        [EvoHeader("Item List", Constants.CustomEditorID)]
        public int selectedIndex = 0;
        public List<Item> items = new();

        [EvoHeader("Item Layout", Constants.CustomEditorID)]
        [Range(0, 100)] public int itemSpacing = 2;
        [Range(20f, 200f)] public float itemHeight = 40f;
        public RectOffset padding;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public ScrollbarPosition scrollbarPosition = ScrollbarPosition.Top;
        [Range(0, 2000)] public float maxHeight = 240;
        public bool blockUIWhileOpen = true;
        public bool closeOnItemSelect = true;
        public bool closeOnClickOutside = true;

        [EvoHeader("Arrow Settings", Constants.CustomEditorID)]
        public bool rotateArrow = true;
        [Range(-180f, 180f)] public float arrowRotation = 180f;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Fade;
        [Range(0.1f, 2f)] public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CustomEditorID)]
        public GameObject itemPrefab;
        public Transform itemParent;
        public Button headerButton;
        [SerializeField] private Image headerArrow;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private CanvasGroup canvasGroup;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int> onItemSelected = new();
        public UnityEvent onOpen = new();
        public UnityEvent onClose = new();

        // Constants
        const int SortingOrder = 30000;

        // Enums
        public enum DropdownState { Closed, Opening, Open, Closing }
        public enum AnimationType { Fade = 0, Scale = 1, Slide = 2 }
        public enum ScrollbarPosition { LastPosition = 0, SelectedItem = 1, Top = 2, Bottom = 3, }

        // Properties
        public bool IsOpen => dropdownState == DropdownState.Open;
        public Item SelectedItem { get; private set; }

        // Helpers
        bool isDragging;
        Coroutine currentAnimation;
        Canvas rootCanvas;
        Canvas blocker;
        Canvas scrollRectCanvas;
        Vector3 originalScrollScale;
        Vector2 originalScrollAnchorMin;
        Vector2 originalScrollAnchorMax;
        Vector2 originalScrollPivot;
        Vector2 originalScrollPosition;
        DropdownState dropdownState = DropdownState.Closed;
        Navigation.Mode previousHeaderNavigation;
        readonly List<Button> itemButtons = new();
        readonly Dictionary<Button, Navigation> previousItemNavigations = new();

        // Cached Components
        RectTransform selfRT;
        RectTransform scrollRectRT;
        RectTransform canvasRT;

        // Cached Allocations
        readonly Vector3[] headerCorners = new Vector3[4];
        readonly Vector3[] canvasCorners = new Vector3[4];
        static readonly WaitForEndOfFrame waitForEndOfFrame = new();

        // Cached Sort Comparisons
        static readonly Comparison<Item> ascendingComparison = (a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
        static readonly Comparison<Item> descendingComparison = (a, b) => string.Compare(b.label, a.label, StringComparison.OrdinalIgnoreCase);

        [Serializable]
        public class Item
        {
            public string label = "Item";
            public Sprite icon;

            // Runtime
            [HideInInspector] public int index;
            [HideInInspector] public Button generatedButton;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            public Item(string label, Sprite icon = null)
            {
                this.label = label;
                this.icon = icon;
            }
        }

        void Awake()
        {
            Initialize();
            GenerateItems();
        }

        void Start()
        {
#if EVO_LOCALIZATION
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
#endif

            // Auto-select valid index or clear selection
            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                UpdateHeader(items[selectedIndex]);
                SelectedItem = items[selectedIndex];
                SelectedItem.generatedButton.SetState(InteractionState.Selected);
            }
            else
            {
                selectedIndex = -1;
                SelectedItem = null;
                UpdateHeader();
            }
        }

        void OnEnable()
        {
            UpdateUI();
            SetState(DropdownState.Closed, true);
        }

        void OnDisable()
        {
            if (dropdownState != DropdownState.Closed)
                SetState(DropdownState.Closed, true);

            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            DestroyBlocker();
        }

        void OnDestroy()
        {
            DestroyBlocker();

            if (headerButton != null)
            {
                headerButton.onClick.RemoveListener(Toggle);
                headerButton.onSubmit.RemoveListener(OnHeaderSubmit);
            }

            if (scrollRect != null && scrollRect.gameObject.TryGetComponent<DropdownScrollDragHandler>(out var handler))
            {
                handler.onBeginDrag.RemoveListener(OnScrollBeginDrag);
                handler.onEndDrag.RemoveListener(OnScrollEndDrag);
            }

#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
#endif
        }

        void Initialize()
        {
            selfRT = GetComponent<RectTransform>();

            if (scrollRect != null)
            {
                scrollRectRT = scrollRect.GetComponent<RectTransform>();
                originalScrollScale = scrollRectRT.localScale;
                scrollRectRT.localScale = animationType == AnimationType.Scale ? Vector3.zero : originalScrollScale;

                // Setup scroll rect drag detection
                if (!scrollRect.gameObject.TryGetComponent<DropdownScrollDragHandler>(out var scrollDragHandler))
                    scrollDragHandler = scrollRect.gameObject.AddComponent<DropdownScrollDragHandler>();

                scrollDragHandler.onBeginDrag.RemoveAllListeners();
                scrollDragHandler.onEndDrag.RemoveAllListeners();

                scrollDragHandler.onBeginDrag.AddListener(OnScrollBeginDrag);
                scrollDragHandler.onEndDrag.AddListener(OnScrollEndDrag);
            }

            // Setup header button
            if (headerButton != null)
            {
                headerButton.onClick.AddListener(Toggle);
                headerButton.onSubmit.AddListener(OnHeaderSubmit);
            }
        }

        void OnScrollBeginDrag() => isDragging = true;

        void OnScrollEndDrag() => isDragging = false;

        void OnHeaderSubmit()
        {
            if (itemParent.childCount > 0)
                Utilities.SetSelectedObject(itemParent.GetChild(0).gameObject);
        }

        void OnItemSubmit()
        {
            if (headerButton != null)
                Utilities.SetSelectedObject(headerButton.gameObject);
        }

        void SetScrollbarPosition()
        {
            if (scrollRect == null || scrollRect.verticalScrollbar == null)
                return;

            if (scrollbarPosition == ScrollbarPosition.Bottom) { scrollRect.verticalScrollbar.value = 0; }
            else if (scrollbarPosition == ScrollbarPosition.Top) { scrollRect.verticalScrollbar.value = 1; }
            else if (scrollbarPosition == ScrollbarPosition.SelectedItem) { scrollRect.SnapToElementInstant(selectedIndex, -(itemHeight * 2)); }
        }

        void UpdateHeader(Item item = null)
        {
            if (headerButton == null || item == null)
                return;

            headerButton.SetText(item.label);
            headerButton.SetIcon(item.icon);
        }

        void GenerateItems()
        {
            if (itemPrefab == null || itemParent == null)
                return;

            // Clear existing items
            itemButtons.Clear();
            for (int i = itemParent.childCount - 1; i >= 0; i--) { Destroy(itemParent.GetChild(i).gameObject); }

            // Generate new items
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.index = i;

                GameObject itemGO = Instantiate(itemPrefab, itemParent);
                itemGO.name = item.label;
                Button btn = itemGO.GetComponent<Button>();

                SetupItemButton(btn, item);
                itemButtons.Add(btn);
            }

            // Do a scroll rect refresh with new items
            if (scrollRect != null) { scrollRect.RefreshItems(); }
        }

        void SetupItemButton(Button button, Item item)
        {
            // Pass the button
            item.generatedButton = button;

            // Setup click event
            button.onClick.AddListener(() => SelectItem(item.index));
            button.onSubmit.AddListener(OnItemSubmit);

            // Set item height
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, itemHeight);

            // Update visual content
            button.SetText(item.label);
            button.SetIcon(item.icon);
        }

        void RestrictNavigation()
        {
            // Save and disable header button navigation
            if (headerButton != null)
            {
                previousHeaderNavigation = headerButton.navigation.mode;
                Navigation nav = headerButton.navigation;
                nav.mode = Navigation.Mode.None;
                headerButton.navigation = nav;
            }

            // Save item navigations and set them up in a loop
            previousItemNavigations.Clear();
            for (int i = 0; i < itemButtons.Count; i++)
            {
                Button btn = itemButtons[i];
                previousItemNavigations[btn] = btn.navigation;
                Navigation nav = new() { mode = Navigation.Mode.Explicit };

                // Set up navigation
                if (i > 0) { nav.selectOnUp = itemButtons[i - 1]; }
                if (i < itemButtons.Count - 1) { nav.selectOnDown = itemButtons[i + 1]; }

                // Loop around
                if (i == 0) { nav.selectOnUp = itemButtons[^1]; }
                if (i == itemButtons.Count - 1) { nav.selectOnDown = itemButtons[0]; }

                btn.navigation = nav;
            }
        }

        void RestoreNavigation()
        {
            // Restore header navigation
            if (headerButton != null)
            {
                Navigation nav = headerButton.navigation;
                nav.mode = previousHeaderNavigation;
                headerButton.navigation = nav;
            }

            // Restore item navigations
            foreach (var kvp in previousItemNavigations)
            {
                if (kvp.Key != null)
                    kvp.Key.navigation = kvp.Value;
            }
            previousItemNavigations.Clear();
        }

        void UpdateLayout()
        {
            if (itemParent != null && itemParent.TryGetComponent<VerticalLayoutGroup>(out var vlg))
            {
                vlg.spacing = itemSpacing;
                vlg.padding = padding;
            }

            if (scrollRect != null)
            {
                float contentHeight = CalculateContentHeight();
                float clampedHeight = Mathf.Min(maxHeight, contentHeight);

                if (dropdownState != DropdownState.Closed)
                    scrollRectRT.sizeDelta = new Vector2(scrollRectRT.sizeDelta.x, clampedHeight);
            }
        }

        void SetState(DropdownState state, bool instant = false)
        {
            dropdownState = state;

            if (scrollRect != null)
            {
                bool shouldBeActive = state != DropdownState.Closed;

                if (scrollRect.gameObject.activeSelf != shouldBeActive)
                    scrollRect.gameObject.SetActive(shouldBeActive);

                // Set height to 0 for slide animations when closed
                if (state == DropdownState.Closed && animationType == AnimationType.Slide)
                    scrollRectRT.sizeDelta = new Vector2(scrollRectRT.sizeDelta.x, 0f);
            }

            if (instant)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = state == DropdownState.Open ? 1f : 0f;

                if (rotateArrow && headerArrow != null)
                {
                    float targetRotation = state == DropdownState.Open ? arrowRotation : 0f;
                    headerArrow.transform.localRotation = Quaternion.Euler(0, 0, targetRotation);
                }
            }
        }

        void CreateScrollRectCanvas()
        {
            if (scrollRect == null || scrollRectCanvas != null)
                return;

            // Add Canvas component to the existing ScrollRect GameObject
            scrollRectCanvas = scrollRect.gameObject.AddComponent<Canvas>();
            scrollRect.gameObject.AddComponent<GraphicRaycaster>();
            scrollRectCanvas.vertexColorAlwaysGammaSpace = true;
            scrollRectCanvas.overrideSorting = true;
            scrollRectCanvas.sortingOrder = SortingOrder;
        }

        void CreateBlocker()
        {
            if (!blockUIWhileOpen || blocker != null)
                return;

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
                if (rootCanvas != null) { canvasRT = rootCanvas.GetComponent<RectTransform>(); }
            }

            if (rootCanvas == null)
                return;

            GameObject blockerGO = new("DropdownBlocker");
            blocker = blockerGO.AddComponent<Canvas>();
            blocker.overrideSorting = true;
            blocker.sortingOrder = SortingOrder - 1;
            blockerGO.AddComponent<GraphicRaycaster>();

            RectTransform blockerRT = blockerGO.GetComponent<RectTransform>();
            blockerRT.SetParent(rootCanvas.transform, false);
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.sizeDelta = Vector2.zero;
            blockerRT.anchoredPosition = Vector2.zero;

            Image blockerImage = blockerGO.AddComponent<Image>();
            blockerImage.color = Color.clear;

            UnityEngine.UI.Button button = blockerGO.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(OnBlockerClick);

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
            blocker = null;
        }

        void OnBlockerClick()
        {
            if (!isDragging && closeOnClickOutside)
                Close();
        }

        void RestoreOriginalPosition()
        {
            if (scrollRect == null)
                return;

            scrollRectRT.anchorMin = originalScrollAnchorMin;
            scrollRectRT.anchorMax = originalScrollAnchorMax;
            scrollRectRT.pivot = originalScrollPivot;
            scrollRectRT.anchoredPosition = originalScrollPosition;
        }

        void CacheCurrentPosition()
        {
            if (scrollRect == null)
                return;

            originalScrollAnchorMin = scrollRectRT.anchorMin;
            originalScrollAnchorMax = scrollRectRT.anchorMax;
            originalScrollPivot = scrollRectRT.pivot;
            originalScrollPosition = scrollRectRT.anchoredPosition;
        }

        void CheckAndAdjustPosition()
        {
            if (scrollRect == null)
                return;

            float dropdownHeight = Mathf.Min(maxHeight, CalculateContentHeight());

            // Get the root canvas to calculate screen bounds
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
                if (rootCanvas != null) { canvasRT = rootCanvas.GetComponent<RectTransform>(); }
            }

            if (rootCanvas == null)
                return;

            // Calculate world corners of the header — reuses cached arrays, no per-call allocation
            selfRT.GetWorldCorners(headerCorners);

            // Get canvas rect for screen bounds — reuses cached arrays, no per-call allocation
            canvasRT.GetWorldCorners(canvasCorners);

            // Calculate available space below and above the header
            float headerBottomY = headerCorners[0].y; // Bottom-left corner Y
            float headerTopY = headerCorners[1].y;    // Top-left corner Y
            float canvasBottomY = canvasCorners[0].y; // Canvas bottom
            float canvasTopY = canvasCorners[2].y;    // Canvas top

            float spaceBelow = headerBottomY - canvasBottomY;
            float spaceAbove = canvasTopY - headerTopY;

            // Determine best position based on available space

            // If there's not enough space below, check if there's more space above
            // Only open upward if there's more space above than below
            bool shouldOpenUpward = spaceBelow < dropdownHeight && spaceAbove > spaceBelow;

            if (shouldOpenUpward)
            {
                // Position dropdown above the header
                scrollRectRT.anchorMin = new Vector2(originalScrollAnchorMin.x, 1f);
                scrollRectRT.anchorMax = new Vector2(originalScrollAnchorMax.x, 1f);
                scrollRectRT.pivot = new Vector2(originalScrollPivot.x, 0f);
                scrollRectRT.anchoredPosition = new Vector2(originalScrollPosition.x, -originalScrollPosition.y);
            }
            else
            {
                // Position dropdown below the header (default/original positioning)
                scrollRectRT.anchorMin = originalScrollAnchorMin;
                scrollRectRT.anchorMax = originalScrollAnchorMax;
                scrollRectRT.pivot = originalScrollPivot;
                scrollRectRT.anchoredPosition = originalScrollPosition;
            }
        }

        float CalculateContentHeight()
        {
            var paddingToUse = padding;
            float totalHeight = items.Count * itemHeight + (items.Count - 1) * itemSpacing;
            totalHeight += paddingToUse.top + paddingToUse.bottom;
            return totalHeight;
        }

        IEnumerator AnimateDropdown(bool opening)
        {
            if (scrollRect == null || canvasGroup == null)
                yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = scrollRectRT.localScale;
            Vector3 targetScale = opening ? originalScrollScale : Vector3.zero;

            Vector2 startSize = scrollRectRT.sizeDelta;
            Vector2 targetSize = opening ? new Vector2(scrollRectRT.sizeDelta.x,
                Mathf.Min(maxHeight, CalculateContentHeight())) : new Vector2(scrollRectRT.sizeDelta.x, 0f);

            float startArrowRotation = headerArrow != null ? headerArrow.transform.localEulerAngles.z : 0f;
            float targetArrowRotation = opening && rotateArrow ? arrowRotation : 0f;

            // Set initial values for opening animations ONLY
            if (opening)
            {
                if (animationType == AnimationType.Fade)
                {
                    canvasGroup.alpha = 0f;
                    scrollRectRT.localScale = originalScrollScale;
                    scrollRectRT.sizeDelta = targetSize;
                }
                else if (animationType == AnimationType.Scale)
                {
                    canvasGroup.alpha = 0f;
                    scrollRectRT.localScale = Vector3.zero;
                    scrollRectRT.sizeDelta = targetSize;
                }
                else if (animationType == AnimationType.Slide)
                {
                    canvasGroup.alpha = 0f;
                    scrollRectRT.localScale = originalScrollScale;
                    scrollRectRT.sizeDelta = new Vector2(scrollRectRT.sizeDelta.x, 0f);
                }
            }

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = animationCurve.Evaluate(elapsed / animationDuration);

                // Apply animations based on type
                if (animationType == AnimationType.Fade) { canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress); }
                else if (animationType == AnimationType.Scale)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                    scrollRectRT.localScale = Vector3.Lerp(startScale, targetScale, progress);
                }
                else if (animationType == AnimationType.Slide)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                    scrollRectRT.sizeDelta = Vector2.Lerp(startSize, targetSize, progress);
                }

                // Rotate arrow
                if (rotateArrow && headerArrow != null)
                {
                    float currentRotation = Mathf.LerpAngle(startArrowRotation, targetArrowRotation, progress);
                    headerArrow.transform.localRotation = Quaternion.Euler(0, 0, currentRotation);
                }

                yield return null;
            }

            // Ensure final values
            canvasGroup.alpha = targetAlpha;
            scrollRectRT.localScale = targetScale;
            scrollRectRT.sizeDelta = targetSize;

            // Check for arrow
            if (rotateArrow && headerArrow != null) { headerArrow.transform.localRotation = Quaternion.Euler(0, 0, targetArrowRotation); }

            // Set state
            SetState(opening ? DropdownState.Open : DropdownState.Closed);

            // Restore original positioning after animation completes (only when closing)
            if (!opening) { RestoreOriginalPosition(); }

            // Clear coroutine
            currentAnimation = null;
        }

        IEnumerator SetScrollbarPositionDelayed()
        {
            yield return waitForEndOfFrame;
            Canvas.ForceUpdateCanvases();
            SetScrollbarPosition();
        }

        public void Toggle()
        {
            if (dropdownState == DropdownState.Open) { Close(); }
            else { Open(); }
        }

        public void Open()
        {
            if (dropdownState == DropdownState.Open)
                return;

            // If currently animating, stop and immediately switch to opening
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }
    
            CacheCurrentPosition(); // Cache current positioning values right before opening
            CheckAndAdjustPosition(); // Check if dropdown would be off-screen and adjust position

            SetState(DropdownState.Opening);

            CreateScrollRectCanvas();
            CreateBlocker();
            StartCoroutine(SetScrollbarPositionDelayed());
            RestrictNavigation();

            onOpen?.Invoke();
            currentAnimation = StartCoroutine(AnimateDropdown(true));
        }

        public void Close()
        {
            if (dropdownState == DropdownState.Closed)
                return;

            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            SetState(DropdownState.Closing);
            onClose?.Invoke();

            DestroyBlocker();
            RestoreNavigation();

            currentAnimation = StartCoroutine(AnimateDropdown(false));
        }

        public void UpdateUI()
        {
            UpdateHeader();
            UpdateLayout();
        }

        public void SelectItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            // Check for currently selected item
            if (SelectedItem != null && SelectedItem.generatedButton != null)
                SelectedItem.generatedButton.SetState(InteractionState.Normal);

            // Set indexes
            selectedIndex = index;
            SelectedItem = items[index];

            // Check for newly selected item
            if (SelectedItem == null || SelectedItem.generatedButton == null)
            {
                if (triggerEvents)
                    onItemSelected?.Invoke(index);

                return;
            }

            SelectedItem.generatedButton.SetState(InteractionState.Selected);
            UpdateHeader(SelectedItem);

            if (triggerEvents) { onItemSelected?.Invoke(index); }
            if (closeOnItemSelect) { Close(); }
        }

        public void SelectItem(Item item, bool triggerEvents = true)
        {
            int index = items.IndexOf(item);
            if (index >= 0) { SelectItem(index, triggerEvents); }
        }

        public bool SelectItem(string label, bool triggerEvents = true)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].label == label)
                {
                    SelectItem(i, triggerEvents);
                    return true;
                }
            }

            return false;
        }

        public void SelectItem(int index) => SelectItem(index, true);

        public bool SelectItem(string label) => SelectItem(label, true);

        public void AddItem(Item item, bool generate = true)
        {
            items.Add(item);

            if (generate && itemPrefab != null && itemParent != null)
            {
                item.index = items.Count - 1;

                GameObject itemGO = Instantiate(itemPrefab, itemParent);
                itemGO.name = item.label;
                Button btn = itemGO.GetComponent<Button>();

                SetupItemButton(btn, item);
                itemButtons.Add(btn);

                if (scrollRect != null) { scrollRect.AddTransform(itemGO.GetComponent<RectTransform>()); }
            }
        }

        public void AddItem(string label, Sprite icon = null, bool generate = true) => AddItem(new Item(label, icon), generate);

        public void AddItems(params Item[] newItems)
        {
            items.AddRange(newItems);
            GenerateItems();
        }

        public void AddItems(params string[] labels)
        {
            foreach (var label in labels)
                items.Add(new Item(label));

            GenerateItems();
        }

        public void RemoveItem(int index)
        {
            if (index >= 0 && index < items.Count)
            {
                // Destroy the full GameObject, not just the Button component
                if (items[index].generatedButton != null)
                    Destroy(items[index].generatedButton.gameObject);

                items.RemoveAt(index);

                // Keep itemButtons in sync with items list
                if (index < itemButtons.Count)
                    itemButtons.RemoveAt(index);

                if (selectedIndex > index) { selectedIndex--; }
                else if (selectedIndex == index)
                {
                    selectedIndex = -1;
                    SelectedItem = null;
                    UpdateHeader();
                }

                if (scrollRect != null)
                    scrollRect.RefreshItems();
            }
        }

        public bool RemoveItem(string label)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].label == label)
                {
                    RemoveItem(i);
                    return true;
                }
            }

            return false;
        }

        public void ClearAllItems()
        {
            items.Clear();
            itemButtons.Clear();

            selectedIndex = -1;
            SelectedItem = null;

            if (itemParent != null && itemParent.childCount > 0)
            {
                for (int i = itemParent.childCount - 1; i >= 0; i--)
                    Destroy(itemParent.GetChild(i).gameObject);
            }

            if (scrollRect != null)
                scrollRect.RefreshItems();

            UpdateHeader();
        }

        public void SortAlphabetically(bool ascending = true)
        {
            // Uses cached static delegates — no delegate allocation per call
            items.Sort(ascending ? ascendingComparison : descendingComparison);

            for (int i = 0; i < items.Count; i++)
                items[i].index = i;
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (Item item in items)
            {
                if (!string.IsNullOrEmpty(item.tableKey))
                {
                    item.label = localizedObject.GetString(item.tableKey);
                    item.generatedButton.SetText(item.label);
                }
            }

            UpdateHeader(SelectedItem);
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool itemsFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Clamp default panel index
            if (items.Count > 0) { selectedIndex = Mathf.Clamp(selectedIndex, -1, items.Count - 1); }
            else { selectedIndex = 0; }

            // Update display in editor safely and allocation-free
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                UpdateUI();
        }
#endif
    }

    /// <summary>
    /// Helper component for scroll drag detection.
    /// </summary>
    public class DropdownScrollDragHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public UnityEvent onBeginDrag = new();
        public UnityEvent onEndDrag = new();

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke();

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke();
    }
}