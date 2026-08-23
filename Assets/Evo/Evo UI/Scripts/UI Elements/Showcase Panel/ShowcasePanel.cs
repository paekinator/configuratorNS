using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/showcase-panel")]
    [AddComponentMenu("Evo/UI/UI Elements/Showcase Panel")]
    public class ShowcasePanel : MonoBehaviour
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        public int currentIndex;
        public List<Item> items = new();

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool useUnscaledTime = false;
        public bool setWithTimer = true;
        [Range(1f, 30f)] public float timer = 3f;
        [Range(0.05f, 2f)] public float animationDuration = 0.25f;
        public Vector2 slideOffset = new(0f, 15f);

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private Transform buttonParent;
        [SerializeField] private GameObject buttonPreset;
        [SerializeField] private TextMeshProUGUI textDisplay;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image backgroundShadow;

        // Helpers
        int displayedIndex = -1;
        float timerCount = 0;
        bool isInitialized;
        bool updateTimer;
        bool isTransitionInProgress;

        // Cache
        CanvasGroup textCanvasGroup;
        CanvasGroup backgroundCanvasGroup;
        Coroutine hoverCoroutine;
        Coroutine shadowCoroutine;
        WaitForSeconds cachedHoverWait;
        WaitForSecondsRealtime cachedHoverWaitRealtime;

        // Constants
        const float HoverTransitionDelay = 0.15f;
        const float AnimationSplit = 0.5f;

        [System.Serializable]
        public class Item
        {
            public string title = "Item Title";
            public string url;
            public Sprite icon;
            public Sprite background;
            public Color shadowColor = Color.clear;
            [TextArea(2, 4)] public string description = "Item description";
            public UnityEvent onClick = new();
            [HideInInspector] public Button button;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string titleKey;
            public string descriptionKey;
#endif
        }

        void OnEnable()
        {
            if (!isInitialized) { Initialize(); }
            else { ShowCurrentItem(); }
        }

        void Update()
        {
            if (!isInitialized || !updateTimer || !setWithTimer)
                return;

            timerCount += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (timerCount > timer)
            {
                if (items.Count > 1)
                    StartCoroutine(DoTimerTransition());

                timerCount = 0;
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
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
        }
#endif

        public void Initialize()
        {
            cachedHoverWait = new WaitForSeconds(HoverTransitionDelay);
            cachedHoverWaitRealtime = new WaitForSecondsRealtime(HoverTransitionDelay);

            // Clean up existing buttons
            foreach (Transform child in buttonParent)
                Destroy(child.gameObject);

            SetupCanvasGroups();
            CreateButtons();
            ShowCurrentItem();

            isInitialized = true;
        }

        void SetupCanvasGroups()
        {
            if (textDisplay != null)
            {
                if (!textDisplay.TryGetComponent(out textCanvasGroup))
                    textCanvasGroup = textDisplay.gameObject.AddComponent<CanvasGroup>();
            }

            if (backgroundImage != null)
            {
                if (!backgroundImage.TryGetComponent(out backgroundCanvasGroup))
                    backgroundCanvasGroup = backgroundImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        void CreateButtons()
        {
            for (int i = 0; i < items.Count; ++i)
            {
                int tempIndex = i; // Capture for closure

                GameObject btnGO = Instantiate(buttonPreset, buttonParent);
                btnGO.name = items[tempIndex].title;

                Button btn = btnGO.GetComponent<Button>();
                items[tempIndex].button = btn;

                btn.SetIcon(items[tempIndex].icon);
                btn.SetText(items[tempIndex].title);

                // Setup click event
                btn.onClick.AddListener(() => HandleButtonClick(tempIndex));

                // Setup hover events
                btn.onPointerEnter.AddListener(() => HandleButtonHover(tempIndex, btn));
                btn.onPointerExit.AddListener(() => HandleButtonLeave());
            }
        }

        void HandleButtonClick(int index)
        {
            items[index].onClick.Invoke();

            if (!string.IsNullOrEmpty(items[index].url))
                Application.OpenURL(items[index].url);
        }

        void HandleButtonHover(int index, Button hoveredButton)
        {
            // Disable other buttons during hover (optimized)
            SetButtonsInteractable(false, hoveredButton);

            // Stop existing hover coroutine and start new one
            if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
            hoverCoroutine = StartCoroutine(SetItemByHover(index));
        }

        void HandleButtonLeave()
        {
            updateTimer = true;
            SetButtonsInteractable(true);
            HighlightCurrentButton();
        }

        void SetButtonsInteractable(bool interactable, Button except = null)
        {
            for (int x = 0; x < items.Count; ++x)
            {
                if (items[x].button != except && items[x].button.interactable != interactable)
                    items[x].button.SetInteractable(interactable);
            }
        }

        void HighlightCurrentButton()
        {
            if (currentIndex >= 0 && currentIndex < items.Count)
                items[currentIndex].button.SetState(InteractionState.Highlighted);
        }

        void ShowCurrentItem()
        {
            SetItemContent(currentIndex);
            HighlightCurrentButton();
            StartCoroutine(PlayInAnimation());

            if (backgroundShadow != null && currentIndex >= 0 && currentIndex < items.Count)
            {
                if (backgroundShadow.color != items[currentIndex].shadowColor)
                    backgroundShadow.color = items[currentIndex].shadowColor;
            }

            displayedIndex = currentIndex;
            timerCount = 0;
            updateTimer = true;
        }

        void SetItemContent(int index)
        {
            Item item = items[index];

            if (textDisplay != null && textDisplay.text != item.description)
                textDisplay.text = item.description;

            if (backgroundImage != null)
            {
                bool hasBackground = item.background != null;

                if (backgroundImage.gameObject.activeSelf != hasBackground) { backgroundImage.gameObject.SetActive(hasBackground); }
                if (hasBackground && backgroundImage.sprite != item.background) { backgroundImage.sprite = item.background; }
            }
        }

        void AnimateText(Vector2 startPos, Vector2 endPos, float startAlpha, float endAlpha, float progress)
        {
            if (textDisplay != null)
            {
                textDisplay.rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
                textCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
            }
        }

        void AnimateBackground(float startAlpha, float endAlpha, float progress)
        {
            if (backgroundCanvasGroup != null)
                backgroundCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
        }

        void FinalizeTextAnimation(Vector2 position, float alpha)
        {
            if (textDisplay != null)
            {
                textDisplay.rectTransform.anchoredPosition = position;
                textCanvasGroup.alpha = alpha;
            }
        }

        void FinalizeBackgroundAnimation(float alpha)
        {
            if (backgroundCanvasGroup != null)
                backgroundCanvasGroup.alpha = alpha;
        }

        bool ShouldAnimateBackground(int index)
        {
            return backgroundImage != null &&
                   index >= 0 &&
                   index < items.Count &&
                   items[index].background != null;
        }

        IEnumerator DoTimerTransition()
        {
            items[currentIndex].button.SetState(InteractionState.Normal);
            yield return StartCoroutine(PlayOutAnimation());

            // Move to next index
            currentIndex = (currentIndex + 1) % items.Count;

            SetItemContent(currentIndex);
            displayedIndex = currentIndex;
            HighlightCurrentButton();

            if (backgroundShadow != null)
            {
                Color tsColor = items[currentIndex].shadowColor;

                // Only animate if there's actually a color difference
                if (backgroundShadow.color != tsColor)
                {
                    if (shadowCoroutine != null) { StopCoroutine(shadowCoroutine); }
                    shadowCoroutine = StartCoroutine(Utilities.CrossFadeGraphic(backgroundShadow, tsColor, animationDuration));
                }
            }

            yield return StartCoroutine(PlayInAnimation());
        }

        IEnumerator SetItemByHover(int index)
        {
            updateTimer = false;
            timerCount = 0;
            currentIndex = index;

            if (displayedIndex == index && !isTransitionInProgress)
                yield break;

            if (isTransitionInProgress)
                yield return useUnscaledTime ? cachedHoverWaitRealtime : cachedHoverWait;

            isTransitionInProgress = true;

            yield return StartCoroutine(PlayOutAnimation());

            SetItemContent(currentIndex);
            displayedIndex = index;

            if (backgroundShadow != null)
            {
                Color tsColor = items[currentIndex].shadowColor;

                // Only animate if there's actually a color difference
                if (backgroundShadow.color != tsColor)
                {
                    if (shadowCoroutine != null) { StopCoroutine(shadowCoroutine); }
                    shadowCoroutine = StartCoroutine(Utilities.CrossFadeGraphic(backgroundShadow, tsColor, animationDuration));
                }
            }

            yield return StartCoroutine(PlayInAnimation());
            isTransitionInProgress = false;
        }

        IEnumerator PlayOutAnimation()
        {
            float elapsed = 0f;
            float duration = animationDuration * AnimationSplit;

            // Get starting values
            Vector2 textStartPos = Vector2.zero;
            float textStartAlpha = 1f;
            float bgStartAlpha = 1f;
            bool shouldAnimateBackground = ShouldAnimateBackground(displayedIndex);

            if (textDisplay != null)
            {
                textStartPos = textDisplay.rectTransform.anchoredPosition;
                textStartAlpha = textCanvasGroup.alpha;
            }

            if (shouldAnimateBackground)
                bgStartAlpha = backgroundCanvasGroup.alpha;

            // Animate out
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsed / duration;

                AnimateText(textStartPos, -slideOffset, textStartAlpha, 0f, progress);

                if (shouldAnimateBackground)
                    AnimateBackground(bgStartAlpha, 0f, progress);

                yield return null;
            }

            // Ensure final out state
            FinalizeTextAnimation(-slideOffset, 0f);
            if (shouldAnimateBackground) { FinalizeBackgroundAnimation(0f); }
        }

        IEnumerator PlayInAnimation()
        {
            float elapsed = 0f;
            float duration = animationDuration * AnimationSplit;
            bool shouldAnimateBackground = ShouldAnimateBackground(currentIndex);

            if (shouldAnimateBackground)
                backgroundCanvasGroup.alpha = 0f;

            if (textDisplay != null)
            {
                textDisplay.rectTransform.anchoredPosition = slideOffset;
                textCanvasGroup.alpha = 0f;
            }

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsed / duration;

                AnimateText(slideOffset, Vector2.zero, 0f, 1f, progress);

                if (shouldAnimateBackground)
                    AnimateBackground(0f, 1f, progress);

                yield return null;
            }

            // Ensure final in state
            FinalizeTextAnimation(Vector2.zero, 1f);

            if (shouldAnimateBackground)
                FinalizeBackgroundAnimation(1f);
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (Item item in items)
            {
                if (!string.IsNullOrEmpty(item.titleKey)) { item.title = localizedObject.GetString(item.titleKey); }
                if (!string.IsNullOrEmpty(item.descriptionKey)) { item.description = localizedObject.GetString(item.descriptionKey); }
            }

            SetItemContent(currentIndex);
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
#endif
    }
}