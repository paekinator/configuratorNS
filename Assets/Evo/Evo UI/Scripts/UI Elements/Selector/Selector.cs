using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/selector")]
    [AddComponentMenu("Evo/UI/UI Elements/Selector")]
    public class Selector : MonoBehaviour
    {
        [EvoHeader("Item List", Constants.CustomEditorID)]
        public int selectedIndex = 0;
        public List<Item> items = new();

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool loop = false;
        [SerializeField] private bool invokeAtStart = false;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        [SerializeField] private bool isHorizontal = true;
        [SerializeField] private bool invertAnimationDirection = false;
        [SerializeField] private float slideOffset = 25f;
        [SerializeField, Range(0f, 1f)] private float animationDuration = 0.25f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("Indicator", Constants.CustomEditorID)]
        [SerializeField, Range(0f, 1f)] private float selectedIndicatorAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float unselectedIndicatorAlpha = 0.5f;
        [SerializeField, Range(0f, 1f)] private float indicatorFadeDuration = 0.15f;

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private CanvasGroup contentCanvasGroup;
        [SerializeField] private TextMeshProUGUI textObject;
        [SerializeField] private Image iconObject;
        [SerializeField] private RectTransform indicatorParent;
        [SerializeField] private GameObject indicatorPrefab;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int> onSelectionChanged = new();

        // Cache
        Vector2 originalContentPosition;
        Coroutine currentAnimation;
        readonly List<GameObject> indicatorObjects = new();
        readonly List<CanvasGroup> indicatorCanvasGroups = new();
        Coroutine[] indicatorCoroutines = System.Array.Empty<Coroutine>();

        [System.Serializable]
        public class Item
        {
            public string label;
            public Sprite icon;
            public UnityEvent onItemSelection = new();

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

        void Awake() => Initialize();

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
            if (invokeAtStart && items.Count > 0)
            {
                onSelectionChanged?.Invoke(selectedIndex);
                items[selectedIndex].onItemSelection?.Invoke();
            }
        }

        void OnDestroy()
        {
            if (prevButton != null) { prevButton.onClick.RemoveListener(SelectPrevious); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(SelectNext); }

#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
#endif
        }

        void Initialize()
        {
            // Get or add canvas group for content parent
            if (contentParent != null)
            {
                if (!contentCanvasGroup.TryGetComponent<CanvasGroup>(out var cg))
                {
                    if (cg == null) { contentCanvasGroup = contentParent.gameObject.AddComponent<CanvasGroup>(); }
                    else { contentCanvasGroup = cg; }
                }
                originalContentPosition = contentParent.anchoredPosition;
            }

            // Set buttons
            if (prevButton != null) { prevButton.onClick.AddListener(SelectPrevious); }
            if (nextButton != null) { nextButton.onClick.AddListener(SelectNext); }

            // Clamp current index
            if (items.Count > 0) { selectedIndex = Mathf.Clamp(selectedIndex, 0, items.Count - 1); }
            else { selectedIndex = 0; }

            // Initialize display
            UpdateDisplay();
            GenerateIndicators();
            UpdateIndicators(false);
        }

        void UpdateDisplay()
        {
            if (items.Count == 0)
                return;

            var currentItem = items[selectedIndex];

            if (textObject != null && textObject.text != currentItem.label)
                textObject.text = currentItem.label;

            if (iconObject != null)
            {
                bool hasIcon = currentItem.icon != null;

                if (iconObject.gameObject.activeSelf != hasIcon) { iconObject.gameObject.SetActive(hasIcon); }
                if (hasIcon && iconObject.sprite != currentItem.icon) { iconObject.sprite = currentItem.icon; }
            }

            // Update buttons (Streamlined logic)
            if (prevButton != null)
            {
                bool shouldBeInteractable = loop || selectedIndex > 0;
                if (prevButton.interactable != shouldBeInteractable)
                {
                    if (shouldBeInteractable) { prevButton.SetInteractable(true); }
                    else { prevButton.SetInteractable(false); }
                }
            }

            if (nextButton != null)
            {
                bool shouldBeInteractable = loop || selectedIndex < items.Count - 1;
                if (nextButton.interactable != shouldBeInteractable)
                {
                    if (shouldBeInteractable) { nextButton.SetInteractable(true); }
                    else { nextButton.SetInteractable(false); }
                }
            }
        }

        void GenerateIndicators()
        {
            if (indicatorParent == null || indicatorPrefab == null)
                return;

            ClearIndicators();
            indicatorCoroutines = new Coroutine[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                GameObject indicator = Instantiate(indicatorPrefab, indicatorParent);
                indicatorObjects.Add(indicator);

                // Get or add canvas group
                if (!indicator.TryGetComponent<CanvasGroup>(out var canvasGroup))
                    canvasGroup = indicator.AddComponent<CanvasGroup>();

                // Set initial alpha instantly without animation
                float initialAlpha = i == selectedIndex ? selectedIndicatorAlpha : unselectedIndicatorAlpha;
                canvasGroup.alpha = initialAlpha;

                indicatorCanvasGroups.Add(canvasGroup);
            }
        }

        void ClearIndicators()
        {
            if (indicatorParent != null)
            {
                int childCount = indicatorParent.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform child = indicatorParent.GetChild(i);
                    Destroy(child.gameObject);
                }
            }

            indicatorObjects.Clear();
            indicatorCanvasGroups.Clear();
            indicatorCoroutines = System.Array.Empty<Coroutine>();
        }

        void UpdateIndicators(bool animate = true)
        {
            for (int i = 0; i < indicatorCanvasGroups.Count; i++)
            {
                if (indicatorCanvasGroups[i] != null)
                {
                    float targetAlpha = i == selectedIndex ? selectedIndicatorAlpha : unselectedIndicatorAlpha;

                    if (animate)
                    {
                        // Stop existing animation for this specific indicator to prevent overlapping coroutine fights
                        if (indicatorCoroutines[i] != null) { StopCoroutine(indicatorCoroutines[i]); }
                        indicatorCoroutines[i] = StartCoroutine(AnimateIndicatorAlpha(i, indicatorCanvasGroups[i], targetAlpha));
                    }
                    else
                    {
                        if (indicatorCoroutines[i] != null)
                        {
                            StopCoroutine(indicatorCoroutines[i]);
                            indicatorCoroutines[i] = null;
                        }
                        indicatorCanvasGroups[i].alpha = targetAlpha;
                    }
                }
            }
        }

        void StartSlideAnimation(int fromIndex, int toIndex)
        {
            // Cancel previous animation if running
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            // Reset content position and alpha immediately
            if (contentParent != null && contentCanvasGroup != null)
            {
                contentParent.anchoredPosition = originalContentPosition;
                contentCanvasGroup.alpha = 1f;
            }

            // Start new animation
            currentAnimation = StartCoroutine(SlideAnimation(fromIndex, toIndex));
        }

        IEnumerator AnimateIndicatorAlpha(int index, CanvasGroup canvasGroup, float targetAlpha)
        {
            float startAlpha = canvasGroup.alpha;

            // Skip animation if already at target
            if (Mathf.Approximately(startAlpha, targetAlpha))
            {
                indicatorCoroutines[index] = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < indicatorFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / indicatorFadeDuration);
                progress = Mathf.SmoothStep(0f, 1f, progress);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            indicatorCoroutines[index] = null;
        }

        IEnumerator SlideAnimation(int fromIndex, int toIndex)
        {
            // Determine slide direction
            bool slideLeft = toIndex > fromIndex;
            if (!isHorizontal) { slideLeft = toIndex < fromIndex; }
            if (invertAnimationDirection) { slideLeft = !slideLeft; }

            Vector2 slideDirection = isHorizontal ? (slideLeft ? Vector2.left : Vector2.right) : (slideLeft ? Vector2.down : Vector2.up);
            Vector2 startPos = originalContentPosition;
            Vector2 offscreenPos = startPos + (slideDirection * slideOffset);
            Vector2 enterPos = startPos - (slideDirection * slideOffset);

            float halfDuration = animationDuration * 0.5f;

            // Slide current content out
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / halfDuration);
                progress = animationCurve.Evaluate(progress);

                contentParent.anchoredPosition = Vector2.Lerp(startPos, offscreenPos, progress);
                contentCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);

                yield return null;
            }

            // Update content at the middle of animation
            UpdateDisplay();

            // Slide new content in
            contentParent.anchoredPosition = enterPos;

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / halfDuration);
                progress = animationCurve.Evaluate(progress);

                contentParent.anchoredPosition = Vector2.Lerp(enterPos, startPos, progress);
                contentCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);

                yield return null;
            }

            // Ensure final state
            contentParent.anchoredPosition = startPos;
            contentCanvasGroup.alpha = 1f;
            currentAnimation = null;
        }

        public void SetSelection(int index, bool animated = true)
        {
            if (items.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, items.Count - 1);

            // Allow selection even if same index, but only if no animation is running
            // This prevents blocking during rapid clicks while avoiding redundant calls
            if (index == selectedIndex && currentAnimation == null)
                return;

            int previousIndex = selectedIndex;
            selectedIndex = index;

            UpdateIndicators();

            onSelectionChanged?.Invoke(selectedIndex);
            items[selectedIndex].onItemSelection?.Invoke();

            if (animated && contentParent != null) { StartSlideAnimation(previousIndex, selectedIndex); }
            else { UpdateDisplay(); }
        }

        public void SetSelection(int index) => SetSelection(index, true);

        public void SelectNext()
        {
            if (items.Count <= 1)
                return;

            int nextIndex = selectedIndex + 1;
            if (nextIndex >= items.Count)
            {
                if (loop) { nextIndex = 0; }
                else { return; }
            }

            SetSelection(nextIndex, true);
        }

        public void SelectPrevious()
        {
            if (items.Count <= 1)
                return;

            int prevIndex = selectedIndex - 1;
            if (prevIndex < 0)
            {
                if (loop) { prevIndex = items.Count - 1; }
                else { return; }
            }

            SetSelection(prevIndex, true);
        }

        public void AddItem(Item item)
        {
            items.Add(item);
            GenerateIndicators();

            // Set index in case the selection was empty
            if (items.Count == 1) { selectedIndex = 0; }

            UpdateDisplay();
            UpdateIndicators(false);
        }

        public void AddItem(string text, Sprite icon = null) => AddItem(new Item(text, icon));

        public void AddItems(params Item[] newItems)
        {
            items.AddRange(newItems);
            GenerateIndicators();
            UpdateDisplay();
        }

        public void AddItems(params string[] labels)
        {
            foreach (var label in labels)
                items.Add(new Item(label));

            GenerateIndicators();
            UpdateDisplay();
        }

        public void RemoveItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return;

            items.RemoveAt(index);

            // Adjust current index if necessary
            if (selectedIndex >= items.Count) { selectedIndex = items.Count - 1; }
            else if (selectedIndex < 0) { selectedIndex = 0; }

            GenerateIndicators();

            if (items.Count > 0)
            {
                UpdateDisplay();
                UpdateIndicators(false);
            }
        }

        public void ClearItems()
        {
            items.Clear();
            selectedIndex = 0;
            ClearIndicators();

            // Prevent UI layout rebuild if it's already null/disabled
            if (textObject != null && !string.IsNullOrEmpty(textObject.text)) { textObject.text = null; }
            if (iconObject != null && iconObject.gameObject.activeSelf) { iconObject.gameObject.SetActive(false); }
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (Item item in items)
            {
                if (!string.IsNullOrEmpty(item.tableKey))
                {
                    item.label = localizedObject.GetString(item.tableKey);
                }
            }

            UpdateDisplay();
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool itemsFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, items.Count - 1));
                slideOffset = Mathf.Max(1f, slideOffset);
            }
        }
#endif
    }
}