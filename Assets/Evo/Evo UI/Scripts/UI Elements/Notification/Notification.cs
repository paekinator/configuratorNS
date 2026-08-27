using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "ui-elements/notification")]
    [AddComponentMenu("Evo/UI/UI Elements/Notification")]
    public class Notification : MonoBehaviour
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        [SerializeField] private Sprite icon;
        [SerializeField] private string title = "Notification Title";
        [SerializeField, TextArea(2, 5)] private string description = "Notification description text goes here.";

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool enableStacking = true;
        public bool useUnscaledTime = false;
        public bool playOnEnable = true;
        public bool destroyAfter = false;
        public bool autoClose = true;
        [Range(0f, 60f)] public float autoCloseDelay = 3f;
        [Tooltip("If true, notifications will queue up and show one by one. If false, they will all show simultaneously (overlapping).")]

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Fade;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0.1f, 5f)] public float duration = 0.3f;
        [Range(0f, 1f)] public float scaleFrom = 0.8f;
        public Vector2 slideOffset = new(0f, -100f);

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource sfxSource = StylingSource.StylerPreset;
        public StylerPreset stylerPreset;

        [EvoHeader("SFX", Constants.CustomEditorID)]
        public AudioMapping openSFX = new() { stylerID = "Open SFX" };
        public AudioMapping closeSFX = new() { stylerID = "Close SFX" };
        public static string[] GetSFXFields() => new[] { "openSFX", "closeSFX" };

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private CanvasGroup canvasGroup;

        // Enums
        public enum AnimationType { None, Fade, Scale, Slide }

        // Properties
        public bool IsOpen { get; private set; }

        // Cache
        Vector2 originalPosition;
        Vector3 originalScale;
        RectTransform rectTransform;
        Coroutine currentAnimation;
        Coroutine autoCloseCoroutine;
        WaitForSeconds cachedWaitDelay;
        WaitForSecondsRealtime cachedWaitDelayRealtime;

        // State
        bool isQueued;
        bool isInitialized;
        float lastAutoCloseDelay = -1f;

        void Awake() => Initialize();

        void OnEnable()
        {
            if (playOnEnable)
                Open();
        }

        void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                StopCurrentAnimations();
            }

            isQueued = false;
            gameObject.SetActive(false);
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

        void Initialize()
        {
            if (isInitialized)
                return;

            rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null && !TryGetComponent(out canvasGroup))
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Store original transform values
            originalPosition = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;

            // Initialize the notification as closed
            SetInitialState();
            isInitialized = true;
        }

        void UpdateUI()
        {
            string newTitle = title ?? string.Empty;
            string newDescription = description ?? string.Empty;

            bool hasIcon = icon != null;
            bool hasTitle = !string.IsNullOrEmpty(newTitle);
            bool hasDescription = !string.IsNullOrEmpty(newDescription);

            // Update Title
            if (titleText != null)
            {
                titleText.gameObject.SetActive(hasTitle);

                if (hasTitle && titleText.text != newTitle)
                    titleText.text = newTitle;
            }

            // Update Description
            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(hasDescription);

                if (hasDescription && descriptionText.text != newDescription)
                    descriptionText.text = newDescription;
            }

            // Update Icon
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);

                if (hasIcon && iconImage.sprite != icon)
                    iconImage.sprite = icon;
            }
        }

        void SetInitialState()
        {
            if (animationType == AnimationType.None)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            switch (animationType)
            {
                case AnimationType.Fade:
                    canvasGroup.alpha = 0f;
                    break;
                case AnimationType.Scale:
                    canvasGroup.alpha = 0f;
                    rectTransform.localScale = originalScale * scaleFrom;
                    break;
                case AnimationType.Slide:
                    canvasGroup.alpha = 0f;
                    rectTransform.anchoredPosition = originalPosition + slideOffset;
                    break;
            }
        }

        void StopCurrentAnimations()
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }
        }

        void OnOpenComplete()
        {
            currentAnimation = null;

            if (autoClose && autoCloseDelay > 0f)
                autoCloseCoroutine = StartCoroutine(AutoCloseCoroutine());
        }

        void OnCloseComplete()
        {
            currentAnimation = null;
            gameObject.SetActive(false);

            if (enableStacking && transform.parent != null)
            {
                foreach (Transform child in transform.parent)
                {
                    if (child == transform)
                        continue;

                    if (child.gameObject.activeInHierarchy)
                    {
                        TryGetComponent(out Notification sibling);
                        // Find the first sibling that is waiting in the queue
                        if (sibling != null && sibling.enableStacking && sibling.isQueued)
                        {
                            sibling.Open(); // Trigger the next one
                            break; // Open only one at a time
                        }
                    }
                }
            }

            if (destroyAfter)
                Destroy(gameObject);
        }

        IEnumerator AutoCloseCoroutine()
        {
            if (lastAutoCloseDelay != autoCloseDelay)
            {
                cachedWaitDelay = new WaitForSeconds(autoCloseDelay);
                cachedWaitDelayRealtime = new WaitForSecondsRealtime(autoCloseDelay);
                lastAutoCloseDelay = autoCloseDelay;
            }

            yield return useUnscaledTime ? cachedWaitDelayRealtime : cachedWaitDelay;

            autoCloseCoroutine = null;
            Close();
        }

        IEnumerator AnimateOpen()
        {
            float elapsed = 0f;
            float animationDuration = duration;

            // Store starting values
            float startAlpha = 0f;
            Vector3 startScale = animationType == AnimationType.Scale ? originalScale * scaleFrom : rectTransform.localScale;
            Vector2 startPosition = animationType == AnimationType.Slide ? originalPosition + slideOffset : rectTransform.anchoredPosition;

            while (elapsed < animationDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsed / animationDuration;
                float curveValue = animationCurve.Evaluate(t);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, curveValue);

                switch (animationType)
                {
                    case AnimationType.Scale:
                        rectTransform.localScale = Vector3.Lerp(startScale, originalScale, curveValue);
                        break;
                    case AnimationType.Slide:
                        rectTransform.anchoredPosition = Vector2.Lerp(startPosition, originalPosition, curveValue);
                        break;
                }

                yield return null;
            }

            if (animationType != AnimationType.None && animationType != AnimationType.Fade)
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalPosition;
            }

            canvasGroup.alpha = 1f;
            OnOpenComplete();
        }

        IEnumerator AnimateClose()
        {
            float elapsed = 0f;
            float animationDuration = duration;

            // Store starting values
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = rectTransform.localScale;
            Vector2 startPosition = rectTransform.anchoredPosition;

            // Calculate target values (inverted from open animation)
            Vector3 targetScale = animationType == AnimationType.Scale ? originalScale * scaleFrom : startScale;
            Vector2 targetPosition = animationType == AnimationType.Slide ? originalPosition + slideOffset : startPosition;

            while (elapsed < animationDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsed / animationDuration;
                float curveValue = animationCurve.Evaluate(t);

                // Always fade out
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, curveValue);

                switch (animationType)
                {
                    case AnimationType.Scale:
                        rectTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
                        break;
                    case AnimationType.Slide:
                        rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);
                        break;
                }

                yield return null;
            }

            if (animationType != AnimationType.None && animationType != AnimationType.Fade)
            {
                rectTransform.localScale = targetScale;
                rectTransform.anchoredPosition = targetPosition;
            }

            canvasGroup.alpha = 0f;
            OnCloseComplete();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            if (!isInitialized)
                Initialize();

            if (enableStacking && transform.parent != null)
            {
                foreach (Transform child in transform.parent)
                {
                    if (child == transform)
                        continue;

                    if (child.gameObject.activeInHierarchy)
                    {
                        TryGetComponent(out Notification sibling);
                        // If any sibling with stacking enabled is currently Open
                        if (sibling != null && sibling.enableStacking && sibling.IsOpen)
                        {
                            // Queue this notification
                            isQueued = true;

                            // Keep GameObject active so logic runs, but hide visuals and input
                            gameObject.SetActive(true);
                            if (canvasGroup != null)
                            {
                                canvasGroup.alpha = 0f;
                                canvasGroup.blocksRaycasts = false;
                            }

                            return; // Wait for our turn
                        }
                    }
                }
            }

            gameObject.SetActive(true);

            if (!gameObject.activeInHierarchy)
                return;

            isQueued = false;
            IsOpen = true;

            StopCurrentAnimations();
            UpdateUI();
            AudioManager.PlayClip(Styler.GetAudio(sfxSource, openSFX, stylerPreset));

            if (animationType == AnimationType.None)
            {
                canvasGroup.alpha = 1f;
                OnOpenComplete();
                return;
            }

            currentAnimation = StartCoroutine(AnimateOpen());
        }

        public void Close()
        {
            if (!IsOpen || !gameObject.activeInHierarchy)
                return;

            if (!isInitialized)
                Initialize();

            IsOpen = false;

            StopCurrentAnimations();
            AudioManager.PlayClip(Styler.GetAudio(sfxSource, closeSFX, stylerPreset));

            if (animationType == AnimationType.None)
            {
                canvasGroup.alpha = 0f;
                OnCloseComplete();
                return;
            }

            currentAnimation = StartCoroutine(AnimateClose());
        }

        public void ForceClose()
        {
            if (!isInitialized)
                return;

            StopCurrentAnimations();
            canvasGroup.alpha = 0f;

            if (animationType != AnimationType.None && animationType != AnimationType.Fade)
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalPosition;
            }

            isQueued = false;
            IsOpen = false;
            gameObject.SetActive(false);
        }

        public void SetContent(Sprite newIcon, string newTitle, string newDescription)
        {
            icon = newIcon;
            title = newTitle;
            description = newDescription;

            UpdateUI();
        }

        public static Notification Create(GameObject preset, Sprite icon, string title, string description,
            Transform parent, bool openAfter = true)
        {
            if (preset == null)
            {
                Debug.LogError("[Notification] A notification preset object is required.");
                return null;
            }

            if (parent == null)
            {
                Debug.LogError("[Notification] A parent transform must be specified.");
                return null;
            }

            GameObject ntfGo = Instantiate(preset, parent);
            if (!ntfGo.TryGetComponent(out Notification ntf))
            {
                Debug.LogError("[Notification] Assigned preset does not contain the 'Notification' component.");
                Destroy(ntfGo);
                return null;
            }

            ntf.icon = icon;
            ntf.title = title;
            ntf.description = description;
            ntf.destroyAfter = true;

            if (openAfter)
                ntf.Open();

            return ntf;
        }

        #region Get/Set
        public Sprite Icon
        {
            get => icon;
            set
            {
                icon = value;
                UpdateUI();
            }
        }

        public string Title
        {
            get => title;
            set
            {
                title = value;
                UpdateUI();
            }
        }

        public string Description
        {
            get => description;
            set
            {
                description = value;
                UpdateUI();
            }
        }
        #endregion

        #region Obsolete

        [System.Obsolete("The 'Notification.SetIcon' is obsolete. Use 'Notification.Icon' instead.", false)]
        public Sprite SetIcon
        {
            get => icon;
            set
            {
                icon = value;
                UpdateUI();
            }
        }

        [System.Obsolete("The 'Notification.SetTitle' is obsolete. Use 'Notification.Title' instead.", false)]
        public string SetTitle
        {
            get => title;
            set
            {
                title = value;
                UpdateUI();
            }
        }

        [System.Obsolete("The 'Notification.SetDescription' is obsolete. Use 'Notification.Description' instead.", false)]
        public string SetDescription
        {
            get => description;
            set
            {
                description = value;
                UpdateUI();
            }
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            bool changed = false;

            if (!string.IsNullOrEmpty(titleKey)) 
            { 
                string newTitle = localizedObject.GetString(titleKey); 
                if (title != newTitle)
                {
                    title = newTitle;
                    changed = true;
                }
            }
            if (!string.IsNullOrEmpty(descriptionKey)) 
            { 
                string newDescription = localizedObject.GetString(descriptionKey); 
                if (description != newDescription)
                {
                    description = newDescription;
                    changed = true;
                }
            }

            if (changed)
                UpdateUI();
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;

        void OnValidate()
        {
            if (!Application.isPlaying)
                UpdateUI();
        }
#endif
    }
}