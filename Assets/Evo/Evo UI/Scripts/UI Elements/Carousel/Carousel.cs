using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/carousel")]
    [AddComponentMenu("Evo/UI/UI Elements/Carousel")]
    public class Carousel : MonoBehaviour
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        public int currentIndex;
        public List<Item> items = new();

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool useUnscaledTime = false;
        public bool autoSlide = true;
        [Range(0.5f, 30f)] public float autoSlideTimer = 3f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0.1f, 2f)] public float animationDuration = 0.4f;
        [Tooltip("Scale which applied to non-selected indicators.")]
        [Range(0.1f, 1f)] public float indicatorShrink = 0.5f;
        public Vector2 slideOffset = new(100f, 0f);

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private Transform itemParent;
        [SerializeField] private GameObject itemPreset;
        [SerializeField] private Transform indicatorParent;
        [SerializeField] private GameObject indicatorPreset;

        // Properties
        public bool IsTransitioning { get; private set; }

        // Cache
        readonly List<CarouselPreset> itemObjects = new();
        readonly List<CarouselIndicator> indicatorObjects = new();

        // Helpers
        bool isInitialized;
        int targetIndex;
        float timer;

        // Animation constants
        const float FADE_THRESHOLD = 0.3f;
        const float INDICATOR_DURATION = 0.3f;

        [System.Serializable]
        public class Item
        {
            public string title = "Item title";
            [TextArea(2, 4)] public string description = "Item description";
            public Sprite background;
            public UnityEvent onClick = new();

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string titleKey;
            public string descriptionKey;
#endif
        }

        void OnEnable()
        {
            if (!isInitialized) { Initialize(); }
            else if (IsTransitioning) { CompleteCurrentTransition(); }
            else
            {
                ResetTimer();
                ShowCurrentItem();
            }
        }

        void Update()
        {
            if (!isInitialized || IsTransitioning || items.Count <= 1)
                return;

            UpdateTimer();
            UpdateIndicators();
        }

        public void Initialize()
        {
            if (items.Count == 0)
                return;

            ClearExistingObjects();
            CreateItemObjects();
            CreateIndicators();

            targetIndex = currentIndex;

            ShowCurrentItem();
            ResetTimer();

            isInitialized = true;
        }

        void UpdateTimer()
        {
            if (!autoSlide)
                return;

            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (timer >= autoSlideTimer) { NextItem(); }
        }

        void UpdateIndicators()
        {
            if (indicatorObjects.Count == 0)
                return;

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                if (i == currentIndex)
                {
                    float progress = autoSlide ? (timer / autoSlideTimer) : 0f;
                    indicatorObjects[i].SetProgress(progress);
                }
                else
                {
                    indicatorObjects[i].SetTimerAlpha(0f);
                    indicatorObjects[i].SetIdleAlpha(1f);
                }
            }
        }

        void ClearExistingObjects()
        {
            foreach (Transform child in itemParent) { Destroy(child.gameObject); }
            foreach (Transform child in indicatorParent) { Destroy(child.gameObject); }

            itemObjects.Clear();
            indicatorObjects.Clear();
        }

        void CreateItemObjects()
        {
            for (int i = 0; i < items.Count; i++)
            {
                GameObject itemGO = Instantiate(itemPreset, itemParent);
                itemGO.name = items[i].title;

                CarouselPreset itemUI = itemGO.GetComponent<CarouselPreset>();
                itemUI.SetContent(items[i]);
                itemUI.SetAlpha(0f);
                itemUI.SetInteractable(false);

                itemObjects.Add(itemUI);
            }
        }

        void CreateIndicators()
        {
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;

                GameObject indicatorGO = Instantiate(indicatorPreset, indicatorParent);
                indicatorGO.name = items[i].title;

                CarouselIndicator indicatorUI = indicatorGO.GetComponent<CarouselIndicator>();
                if (indicatorUI.button != null) { indicatorUI.button.onClick.AddListener(() => GoToItem(index)); }

                indicatorUI.SetProgress(0f);
                indicatorUI.SetTimerAlpha(1f);
                indicatorUI.SetIdleAlpha(0f);
                indicatorUI.SetTimerScale(1f);
                indicatorUI.SetIdleScale(indicatorShrink);

                bool isCurrentIndicator = currentIndex == i;

                indicatorUI.SetIdleActive(!isCurrentIndicator);
                indicatorUI.SetTimerActive(isCurrentIndicator);

                indicatorObjects.Add(indicatorUI);
            }
        }

        void ShowCurrentItem()
        {
            if (itemObjects.Count == 0)
                return;

            for (int i = 0; i < itemObjects.Count; i++)
            {
                if (i == currentIndex)
                {
                    itemObjects[i].SetPosition(Vector2.zero);
                    itemObjects[i].SetAlpha(1f);
                    itemObjects[i].SetInteractable(true);
                }
                else
                {
                    itemObjects[i].SetAlpha(0f);
                    itemObjects[i].SetInteractable(false);
                }
            }
        }

        void ResetTimer() => timer = 0f;

        void CompleteCurrentTransition()
        {
            StopAllCoroutines();
            ResetTimer();

            // Set final states
            for (int i = 0; i < itemObjects.Count; i++)
            {
                if (i == targetIndex)
                {
                    itemObjects[i].SetPosition(Vector2.zero);
                    itemObjects[i].SetAlpha(1f);
                    itemObjects[i].SetInteractable(true);
                }
                else
                {
                    itemObjects[i].SetAlpha(0f);
                    itemObjects[i].SetInteractable(false);
                }
            }

            // Update indicators
            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                if (i == targetIndex)
                {
                    indicatorObjects[i].SetTimerActive(true);
                    indicatorObjects[i].SetTimerScale(1f);
                    indicatorObjects[i].SetTimerAlpha(1f);
                    indicatorObjects[i].SetIdleActive(false);
                }
                else
                {
                    indicatorObjects[i].SetTimerActive(false);
                    indicatorObjects[i].SetIdleActive(true);
                    indicatorObjects[i].SetIdleScale(indicatorShrink);
                    indicatorObjects[i].SetIdleAlpha(1f);
                }
            }

            currentIndex = targetIndex;
            IsTransitioning = false;
        }

        float IndicatorEaseInOutCubic(float t) => t < indicatorShrink ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        IEnumerator ManualTransitionSequence()
        {
            IsTransitioning = true;

            // First transition current indicator to idle (same as timer does)
            yield return StartCoroutine(TransitionIndicatorToIdle(indicatorObjects[currentIndex]));

            // Then transition to new item
            yield return StartCoroutine(TransitionToItem(targetIndex));

            // Finally reset the transition flag
            IsTransitioning = false;
        }

        IEnumerator TransitionToItem(int newIndex)
        {
            CarouselPreset currentItemUI = itemObjects[currentIndex];
            CarouselPreset newItemUI = itemObjects[newIndex];

            // Determine slide direction based on index comparison (reversed)
            bool slideRight = newIndex < currentIndex || (currentIndex == 0 && newIndex == items.Count - 1);
            Vector2 slideDirection = slideRight ? slideOffset : -slideOffset;

            // Set initial positions
            newItemUI.SetPosition(-slideDirection);
            newItemUI.SetAlpha(0f);
            newItemUI.SetInteractable(false);

            float elapsed = 0f;
            Vector2 currentStartPos = Vector2.zero;
            Vector2 newStartPos = -slideDirection;

            while (elapsed < animationDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = elapsed / animationDuration;

                // Use customizable animation curve for motion
                float positionProgress = animationCurve.Evaluate(normalizedTime);

                // Enhanced position animation with customizable curve
                Vector2 currentTargetPos = Vector2.Lerp(currentStartPos, slideDirection, positionProgress);
                Vector2 newTargetPos = Vector2.Lerp(newStartPos, Vector2.zero, positionProgress);

                // Apply curved positions
                currentItemUI.SetPosition(currentTargetPos);
                newItemUI.SetPosition(newTargetPos);

                // Enhanced alpha transitions with linear fading
                if (normalizedTime < FADE_THRESHOLD)
                {
                    // Linear fade out current item
                    float fadeOutProgress = normalizedTime / FADE_THRESHOLD;
                    currentItemUI.SetAlpha(1f - fadeOutProgress);
                    newItemUI.SetAlpha(0f);
                }
                else
                {
                    // Linear fade in new item
                    float fadeInProgress = (normalizedTime - FADE_THRESHOLD) / (1f - FADE_THRESHOLD);
                    newItemUI.SetAlpha(fadeInProgress);
                    currentItemUI.SetAlpha(0f);
                }

                yield return null;
            }

            // Ensure perfect final states
            currentItemUI.SetPosition(slideDirection);
            currentItemUI.SetAlpha(0f);
            currentItemUI.SetInteractable(false);

            newItemUI.SetPosition(Vector2.zero);
            newItemUI.SetAlpha(1f);
            newItemUI.SetInteractable(true);

            currentIndex = newIndex;

            // Transition current indicator from idle to timer
            if (currentIndex >= 0 && currentIndex < indicatorObjects.Count)
                StartCoroutine(TransitionIndicatorToTimer(indicatorObjects[currentIndex]));

            // Reset timer
            ResetTimer();
        }

        IEnumerator TransitionIndicatorToIdle(CarouselIndicator indicator)
        {
            indicator.SetIdleActive(true);
            float elapsed = 0f;

            while (elapsed < INDICATOR_DURATION)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsed / INDICATOR_DURATION;
                float smoothProgress = IndicatorEaseInOutCubic(progress);

                indicator.SetTimerScale(Mathf.Lerp(1f, indicatorShrink, smoothProgress));
                indicator.SetTimerAlpha(Mathf.Lerp(1f, 0f, smoothProgress));

                indicator.SetIdleScale(Mathf.Lerp(1f, indicatorShrink, smoothProgress));
                indicator.SetIdleAlpha(Mathf.Lerp(0f, 1f, smoothProgress));

                yield return null;
            }

            indicator.SetTimerScale(indicatorShrink);
            indicator.SetTimerAlpha(0f);
            indicator.SetTimerActive(false);

            indicator.SetIdleScale(indicatorShrink);
            indicator.SetIdleAlpha(1f);
        }

        IEnumerator TransitionIndicatorToTimer(CarouselIndicator indicator)
        {
            indicator.SetTimerActive(true);
            float elapsed = 0f;

            while (elapsed < INDICATOR_DURATION)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsed / INDICATOR_DURATION;
                float smoothProgress = IndicatorEaseInOutCubic(progress);

                indicator.SetTimerScale(Mathf.Lerp(indicatorShrink, 1f, smoothProgress));
                indicator.SetTimerAlpha(Mathf.Lerp(0f, 1f, smoothProgress));

                indicator.SetIdleScale(Mathf.Lerp(indicatorShrink, 1f, smoothProgress));
                indicator.SetIdleAlpha(Mathf.Lerp(1f, 0f, smoothProgress));

                yield return null;
            }

            indicator.SetTimerScale(1f);
            indicator.SetTimerAlpha(1f);

            indicator.SetIdleScale(1f);
            indicator.SetIdleAlpha(0f);
            indicator.SetIdleActive(false);
        }

        public void NextItem()
        {
            if (IsTransitioning || items.Count <= 1)
                return;

            targetIndex = (currentIndex + 1) % items.Count;
            if (currentIndex >= 0 && currentIndex < indicatorObjects.Count) { StartCoroutine(ManualTransitionSequence()); }
        }

        public void PreviousItem()
        {
            if (IsTransitioning || items.Count <= 1)
                return;

            targetIndex = currentIndex - 1;
            if (targetIndex < 0) { targetIndex = items.Count - 1; }
            if (currentIndex >= 0 && currentIndex < indicatorObjects.Count) { StartCoroutine(ManualTransitionSequence()); }
        }

        public void GoToItem(int index)
        {
            if (IsTransitioning || index == currentIndex || index < 0 || index >= items.Count)
                return;

            targetIndex = index;
            if (currentIndex >= 0 && currentIndex < indicatorObjects.Count)
            {
                StopAllCoroutines();
                StartCoroutine(ManualTransitionSequence());
            }
        }

#if EVO_LOCALIZATION
        void Start()
        {
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
        }

        void OnDestroy()
        {
            if (enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
        }

        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!string.IsNullOrEmpty(items[i].titleKey)) { items[i].title = localizedObject.GetString(items[i].titleKey); }
                if (!string.IsNullOrEmpty(items[i].descriptionKey)) { items[i].description = localizedObject.GetString(items[i].descriptionKey); }
                if (itemObjects[i] != null) { itemObjects[i].SetContent(items[i]); }
            }
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
#endif
    }
}