using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/dropdown")]
    [AddComponentMenu("Evo/UI/UI Elements/Dropdown (Multi Select)")]
    public class DropdownMultiSelect : MonoBehaviour
    {
        [EvoHeader("Item List", Constants.CustomEditorID)]
        public List<Item> items = new();

        [EvoHeader("Item Layout", Constants.CustomEditorID)]
        [Range(0, 100)] public int itemSpacing = 2;
        [Range(20f, 200f)] public float itemHeight = 40f;
        public RectOffset padding;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public ScrollbarPosition scrollbarPosition = ScrollbarPosition.Top;
        [Range(0f, 2000f)] public float maxHeight = 240f;
        public bool blockUIWhileOpen = true;
        public bool closeOnClickOutside = true;

        [EvoHeader("Header Settings", Constants.CustomEditorID)]
        public string headerPlaceholder = "Select items...";
        public HeaderFormat headerFormat = HeaderFormat.Adaptive;
        [Range(1, 10)] public int maxDisplayCount = 3;
        public string countSuffix = "selected";

        [EvoHeader("Arrow Settings", Constants.CustomEditorID)]
        public bool rotateArrow = true;
        [Range(-180f, 180f)] public float arrowRotation = 180f;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Fade;
        [Range(0.1f, 2f)] public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("References", Constants.CustomEditorID)]
        public GameObject itemPrefab;
        public Transform itemParent;
        public Button headerButton;
        [SerializeField] private Image headerArrow;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private CanvasGroup canvasGroup;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int, bool> onItemToggled = new(); // (index, isSelected)
        public UnityEvent<List<int>> onSelectionChanged = new(); // all selected indices
        public UnityEvent onOpen = new();
        public UnityEvent onClose = new();

        // Constants
        const int SortingOrder = 30000;

        // Enums
        public enum DropdownState { Closed, Opening, Open, Closing }
        public enum AnimationType { Fade = 0, Scale = 1, Slide = 2 }
        public enum ScrollbarPosition { LastPosition = 0, SelectedItem = 1, Top = 2, Bottom = 3 }
        public enum HeaderFormat { CommaSeparated, CountOnly, Adaptive }

        // Public properties
        public bool IsOpen => dropdownState == DropdownState.Open;
        public List<int> SelectedIndices => new(selectedIndices);
        public List<Item> SelectedItems
        {
            get
            {
                var result = new List<Item>();
                foreach (int i in selectedIndices)
                {
                    if (i >= 0 && i < items.Count)
                        result.Add(items[i]);
                }
                return result;
            }
        }

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
        readonly List<int> selectedIndices = new();
        readonly List<Button> itemButtons = new();
        readonly Dictionary<Button, Navigation> previousItemNavigations = new();

        [System.Serializable]
        public class Item
        {
            public string label = "Item";
            public Sprite icon;
            public bool defaultSelected = false;

            // Runtime
            [HideInInspector] public int index;
            [HideInInspector] public Button generatedButton;
            [HideInInspector] public DropdownMultiSelectItem multiSelectItem;

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
            // Apply default selections without triggering events
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].defaultSelected)
                    SelectItem(i, false);
            }

            UpdateHeader();
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

        void OnDestroy() => DestroyBlocker();

        void Initialize()
        {
            if (scrollRect != null)
            {
                RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
                originalScrollScale = scrollRT.localScale;
                scrollRT.localScale = animationType == AnimationType.Scale ? Vector3.zero : originalScrollScale;

                if (!scrollRect.gameObject.TryGetComponent<DropdownScrollDragHandler>(out var scrollDragHandler))
                    scrollDragHandler = scrollRect.gameObject.AddComponent<DropdownScrollDragHandler>();

                scrollDragHandler.onBeginDrag.RemoveAllListeners();
                scrollDragHandler.onEndDrag.RemoveAllListeners();
                scrollDragHandler.onBeginDrag.AddListener(() => isDragging = true);
                scrollDragHandler.onEndDrag.AddListener(() => isDragging = false);
            }

            if (headerButton != null)
            {
                headerButton.onClick.AddListener(Toggle);
                headerButton.onSubmit.AddListener(() =>
                {
                    if (itemParent.childCount > 0)
                        Utilities.SetSelectedObject(itemParent.GetChild(0).gameObject);
                });
            }
        }

        void GenerateItems()
        {
            if (itemPrefab == null || itemParent == null)
                return;

            itemButtons.Clear();

            for (int i = itemParent.childCount - 1; i >= 0; i--)
                Destroy(itemParent.GetChild(i).gameObject);

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
            item.generatedButton = button;

            // Resolve the helper component — prefer one already on the prefab (with checkmark wired up),
            // fall back to adding one programmatically if none is found.
            if (!button.TryGetComponent<DropdownMultiSelectItem>(out var msItem))
                msItem = button.gameObject.AddComponent<DropdownMultiSelectItem>();

            item.multiSelectItem = msItem;
            msItem.SetSelected(selectedIndices.Contains(item.index));

            button.onClick.AddListener(() => ToggleItem(item.index));
            button.onSubmit.AddListener(() =>
            {
                if (headerButton != null) { Utilities.SetSelectedObject(headerButton.gameObject); }
            });

            RectTransform rt = button.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, itemHeight);

            button.SetText(item.label);
            button.SetIcon(item.icon);
        }

        void RestrictNavigation()
        {
            if (headerButton != null)
            {
                previousHeaderNavigation = headerButton.navigation.mode;
                Navigation nav = headerButton.navigation;
                nav.mode = Navigation.Mode.None;
                headerButton.navigation = nav;
            }

            previousItemNavigations.Clear();

            for (int i = 0; i < itemButtons.Count; i++)
            {
                Button btn = itemButtons[i];
                previousItemNavigations[btn] = btn.navigation;
                Navigation nav = new() { mode = Navigation.Mode.Explicit };

                if (i > 0) { nav.selectOnUp = itemButtons[i - 1]; }
                if (i < itemButtons.Count - 1) { nav.selectOnDown = itemButtons[i + 1]; }

                if (i == 0) { nav.selectOnUp = itemButtons[^1]; }
                if (i == itemButtons.Count - 1) { nav.selectOnDown = itemButtons[0]; }

                btn.navigation = nav;
            }
        }

        void RestoreNavigation()
        {
            if (headerButton != null)
            {
                Navigation nav = headerButton.navigation;
                nav.mode = previousHeaderNavigation;
                headerButton.navigation = nav;
            }

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
                float clampedHeight = Mathf.Min(maxHeight, CalculateContentHeight());
                RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
                if (dropdownState != DropdownState.Closed) { scrollRT.sizeDelta = new Vector2(scrollRT.sizeDelta.x, clampedHeight); }
            }
        }

        void UpdateHeader()
        {
            if (headerButton == null)
                return;

            headerButton.SetText(BuildHeaderText());
            headerButton.SetIcon(null);
        }

        string BuildHeaderText()
        {
            if (selectedIndices.Count == 0)
                return headerPlaceholder;

            bool useCount = headerFormat == HeaderFormat.CountOnly ||
                            (headerFormat == HeaderFormat.Adaptive && selectedIndices.Count > maxDisplayCount);

            if (useCount)
                return $"{selectedIndices.Count} {countSuffix}";

            var labels = new List<string>(selectedIndices.Count);
            foreach (int i in selectedIndices)
            {
                if (i >= 0 && i < items.Count)
                    labels.Add(items[i].label);
            }
            return string.Join(", ", labels);
        }

        void SetScrollbarPosition()
        {
            if (scrollRect == null || scrollRect.verticalScrollbar == null)
                return;

            if (scrollbarPosition == ScrollbarPosition.Bottom)
                scrollRect.verticalScrollbar.value = 0;
            else if (scrollbarPosition == ScrollbarPosition.Top)
                scrollRect.verticalScrollbar.value = 1;
            else if (scrollbarPosition == ScrollbarPosition.SelectedItem && selectedIndices.Count > 0)
                scrollRect.SnapToElementInstant(selectedIndices[0], -(itemHeight * 2));
        }

        void SetState(DropdownState state, bool instant = false)
        {
            dropdownState = state;

            if (scrollRect != null)
            {
                bool shouldBeActive = state != DropdownState.Closed;
                scrollRect.gameObject.SetActive(shouldBeActive);

                if (state == DropdownState.Closed && animationType == AnimationType.Slide)
                {
                    RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
                    scrollRT.sizeDelta = new Vector2(scrollRT.sizeDelta.x, 0f);
                }
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
                rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

            if (rootCanvas == null)
                return;

            GameObject blockerGO = new("Dropdown Multi Select Blocker");
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
            button.onClick.AddListener(() => 
            {
                if (!isDragging && closeOnClickOutside)
                    Close();
            });

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
        }

        void CacheCurrentPosition()
        {
            if (scrollRect == null)
                return;

            RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
            originalScrollAnchorMin = scrollRT.anchorMin;
            originalScrollAnchorMax = scrollRT.anchorMax;
            originalScrollPivot = scrollRT.pivot;
            originalScrollPosition = scrollRT.anchoredPosition;
        }

        void CheckAndAdjustPosition()
        {
            if (scrollRect == null)
                return;

            RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
            float dropdownHeight = Mathf.Min(maxHeight, CalculateContentHeight());

            if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>().rootCanvas; }
            if (rootCanvas == null) { return; }

            TryGetComponent(out RectTransform headerRT);
            Vector3[] headerCorners = new Vector3[4];
            headerRT.GetWorldCorners(headerCorners);

            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            Vector3[] canvasCorners = new Vector3[4];
            canvasRT.GetWorldCorners(canvasCorners);

            float spaceBelow = headerCorners[0].y - canvasCorners[0].y;
            float spaceAbove = canvasCorners[2].y - headerCorners[1].y;
            bool shouldOpenUpward = spaceBelow < dropdownHeight && spaceAbove > spaceBelow;

            if (shouldOpenUpward)
            {
                scrollRT.anchorMin = new Vector2(originalScrollAnchorMin.x, 1f);
                scrollRT.anchorMax = new Vector2(originalScrollAnchorMax.x, 1f);
                scrollRT.pivot = new Vector2(originalScrollPivot.x, 0f);
                scrollRT.anchoredPosition = new Vector2(originalScrollPosition.x, -originalScrollPosition.y);
            }
            else
            {
                scrollRT.anchorMin = originalScrollAnchorMin;
                scrollRT.anchorMax = originalScrollAnchorMax;
                scrollRT.pivot = originalScrollPivot;
                scrollRT.anchoredPosition = originalScrollPosition;
            }
        }

        void RestoreOriginalPosition()
        {
            if (scrollRect == null)
                return;

            RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();
            scrollRT.anchorMin = originalScrollAnchorMin;
            scrollRT.anchorMax = originalScrollAnchorMax;
            scrollRT.pivot = originalScrollPivot;
            scrollRT.anchoredPosition = originalScrollPosition;
        }

        float CalculateContentHeight()
        {
            float totalHeight = items.Count * itemHeight + (items.Count - 1) * itemSpacing;
            totalHeight += padding.top + padding.bottom;
            return totalHeight;
        }

        IEnumerator AnimateDropdown(bool opening)
        {
            if (scrollRect == null || canvasGroup == null)
                yield break;

            RectTransform scrollRT = scrollRect.GetComponent<RectTransform>();

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = scrollRT.localScale;
            Vector3 targetScale = opening ? originalScrollScale : Vector3.zero;

            Vector2 startSize = scrollRT.sizeDelta;
            Vector2 targetSize = opening
                ? new Vector2(scrollRT.sizeDelta.x, Mathf.Min(maxHeight, CalculateContentHeight()))
                : new Vector2(scrollRT.sizeDelta.x, 0f);

            float startArrowRotation = headerArrow != null ? headerArrow.transform.localEulerAngles.z : 0f;
            float targetArrowRotation = opening && rotateArrow ? arrowRotation : 0f;

            if (opening)
            {
                if (animationType == AnimationType.Fade)
                {
                    canvasGroup.alpha = 0f;
                    scrollRT.localScale = originalScrollScale;
                    scrollRT.sizeDelta = targetSize;
                }
                else if (animationType == AnimationType.Scale)
                {
                    canvasGroup.alpha = 0f;
                    scrollRT.localScale = Vector3.zero;
                    scrollRT.sizeDelta = targetSize;
                }
                else if (animationType == AnimationType.Slide)
                {
                    canvasGroup.alpha = 0f;
                    scrollRT.localScale = originalScrollScale;
                    scrollRT.sizeDelta = new Vector2(scrollRT.sizeDelta.x, 0f);
                }
            }

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = animationCurve.Evaluate(elapsed / animationDuration);

                if (animationType == AnimationType.Fade) { canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress); }
                else if (animationType == AnimationType.Scale)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                    scrollRT.localScale = Vector3.Lerp(startScale, targetScale, progress);
                }
                else if (animationType == AnimationType.Slide)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                    scrollRT.sizeDelta = Vector2.Lerp(startSize, targetSize, progress);
                }

                if (rotateArrow && headerArrow != null)
                {
                    float currentRotation = Mathf.LerpAngle(startArrowRotation, targetArrowRotation, progress);
                    headerArrow.transform.localRotation = Quaternion.Euler(0, 0, currentRotation);
                }

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            scrollRT.localScale = targetScale;
            scrollRT.sizeDelta = targetSize;

            if (rotateArrow && headerArrow != null)
                headerArrow.transform.localRotation = Quaternion.Euler(0, 0, targetArrowRotation);

            SetState(opening ? DropdownState.Open : DropdownState.Closed);
           
            if (!opening)
                RestoreOriginalPosition();

            currentAnimation = null;
        }

        IEnumerator SetScrollbarPositionDelayed()
        {
            yield return new WaitForEndOfFrame();
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

            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            CacheCurrentPosition();
            CheckAndAdjustPosition();
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

        /// <summary>
        /// Toggles selection on the item at the given index.
        /// </summary>
        public void ToggleItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            if (selectedIndices.Contains(index)) { DeselectItem(index, triggerEvents); }
            else { SelectItem(index, triggerEvents); }
        }

        /// <summary>
        /// Selects the item at the given index. Does nothing if already selected.
        /// </summary>
        public void SelectItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count || selectedIndices.Contains(index))
                return;

            selectedIndices.Add(index);
            selectedIndices.Sort();

            var item = items[index];
            if (item.multiSelectItem != null) { item.multiSelectItem.SetSelected(true); }

            UpdateHeader();

            if (triggerEvents)
            {
                onItemToggled?.Invoke(index, true);
                onSelectionChanged?.Invoke(new List<int>(selectedIndices));
            }
        }

        /// <summary>
        /// Deselects the item at the given index. Does nothing if not selected.
        /// </summary>
        public void DeselectItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count || !selectedIndices.Contains(index))
                return;

            selectedIndices.Remove(index);

            var item = items[index];
            if (item.multiSelectItem != null) { item.multiSelectItem.SetSelected(false); }

            UpdateHeader();

            if (triggerEvents)
            {
                onItemToggled?.Invoke(index, false);
                onSelectionChanged?.Invoke(new List<int>(selectedIndices));
            }
        }

        /// <summary>
        /// Selects the first item matching the given label.
        /// </summary>
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

        /// <summary>
        /// Deselects the first item matching the given label.
        /// </summary>
        public bool DeselectItem(string label, bool triggerEvents = true)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].label == label)
                {
                    DeselectItem(i, triggerEvents);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Selects all items.
        /// </summary>
        public void SelectAll(bool triggerEvents = true)
        {
            for (int i = 0; i < items.Count; i++)
                SelectItem(i, triggerEvents);
        }

        /// <summary>
        /// Deselects all currently selected items.
        /// </summary>
        public void DeselectAll(bool triggerEvents = true)
        {
            var toDeselect = new List<int>(selectedIndices);
            foreach (int i in toDeselect) { DeselectItem(i, triggerEvents); }
        }

        public bool IsItemSelected(int index) => selectedIndices.Contains(index);

        public bool IsItemSelected(string label)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].label == label)
                    return IsItemSelected(i);
            }
            return false;
        }

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

                if (scrollRect != null)
                    scrollRect.AddTransform(itemGO.GetComponent<RectTransform>());
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
            if (index < 0 || index >= items.Count)
                return;

            if (items[index].generatedButton != null) { Destroy(items[index].generatedButton.gameObject); }
            items.RemoveAt(index);

            // Shift indices down past the removed one
            selectedIndices.Remove(index);
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                if (selectedIndices[i] > index)
                    selectedIndices[i]--;
            }

            for (int i = 0; i < items.Count; i++)
                items[i].index = i;

            if (scrollRect != null)
                scrollRect.RefreshItems();

            UpdateHeader();
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
            selectedIndices.Clear();

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
            if (ascending)
                items.Sort((a, b) => string.Compare(a.label, b.label, System.StringComparison.OrdinalIgnoreCase));
            else
                items.Sort((a, b) => string.Compare(b.label, a.label, System.StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < items.Count; i++)
                items[i].index = i;
        }

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

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    UpdateUI();
            };
        }
#endif
    }
}