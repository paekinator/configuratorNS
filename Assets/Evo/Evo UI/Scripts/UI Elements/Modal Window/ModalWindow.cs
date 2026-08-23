using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/modal-window")]
    [AddComponentMenu("Evo/UI/UI Elements/Modal Window")]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class ModalWindow : MonoBehaviour
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        public Sprite icon;
        public string title = "Modal Title";
        [TextArea(2, 5)] public string description = "Modal description text.";

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        [EvoHeader("Animation Settings", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Fade;
        [Range(0.1f, 2f)] public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, 0.95f)] public float scaleFrom = 0.8f;
        public Vector2 slideOffset = new(0f, -50f);

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool customContent = false;
        [SerializeField] private bool closeOnConfirm = true;
        [SerializeField] private bool closeOnCancel = true;
        [SerializeField] private StartBehavior startBehavior = StartBehavior.Disabled;
        [SerializeField] private CloseBehavior closeBehavior = CloseBehavior.Disable;
        public NavigationMode navigationMode = NavigationMode.Free;

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        public Button confirmButton;
        public Button cancelButton;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent onOpen = new();
        public UnityEvent onClose = new();
        public UnityEvent onConfirm = new();
        public UnityEvent onCancel = new();

        // Enums
        public enum AnimationType { None = 0, Fade = 1, Scale = 2, Slide = 3 }
        public enum StartBehavior { Open = 0, Disabled = 1 }
        public enum CloseBehavior { Disable = 0, Destroy = 1 }
        public enum NavigationMode { Focused = 0, Free = 1 }

        // Properties
        public bool IsOpen { get; private set; }

        // Cache
        CanvasGroup canvasGroup;

        // Helpers
        Vector2 originalPosition;
        Vector3 originalScale;
        Coroutine currentAnimation;
        GameObject previousNavObject;

        void Awake()
        {
            // Initialize references
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (contentParent == null) { contentParent = GetComponent<RectTransform>(); }

            // Store original values
            originalPosition = contentParent.anchoredPosition;
            originalScale = contentParent.localScale;

            // Setup button listeners
            if (confirmButton != null) { confirmButton.onClick.AddListener(OnConfirmClicked); }
            if (cancelButton != null) { cancelButton.onClick.AddListener(OnCancelClicked); }
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

            ApplyContent();
            ApplyStartBehavior();
        }

        void OnDisable()
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
                ForceVisualState();
            }
        }

        void OnDestroy()
        {
            if (confirmButton != null) { confirmButton.onClick.RemoveListener(OnConfirmClicked); }
            if (cancelButton != null) { cancelButton.onClick.RemoveListener(OnCancelClicked); }

#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
#endif
        }

        void Update()
        {
            if (!IsOpen || navigationMode == NavigationMode.Free)
                return;

            HandleFocusedNavigation();
        }

        void ApplyContent()
        {
            if (customContent)
                return;

            // Prevent TMP mesh rebuilds if text hasn't changed
            if (titleText != null && titleText.text != title)
                titleText.text = title;

            // Prevent TMP mesh rebuilds if text hasn't changed
            if (descriptionText != null && descriptionText.text != description)
                descriptionText.text = description;

            if (iconImage != null)
            {
                bool hasIcon = icon != null;

                if (iconImage.gameObject.activeSelf != hasIcon) { iconImage.gameObject.SetActive(hasIcon); }
                if (hasIcon && iconImage.sprite != icon) { iconImage.sprite = icon; }
            }
        }

        void ApplyStartBehavior()
        {
            IsOpen = startBehavior == StartBehavior.Open;
            switch (startBehavior)
            {
                case StartBehavior.Open:
                    gameObject.SetActive(true);
                    currentAnimation = StartCoroutine(DelayedOpenModal());
                    break;
                case StartBehavior.Disabled:
                    gameObject.SetActive(false);
                    break;
            }
        }

        void HandleFocusedNavigation()
        {
            if (EventSystem.current == null || (cancelButton == null && confirmButton == null))
                return;

            // Check if current selection is already one of our buttons
            var currentSelected = EventSystem.current.currentSelectedGameObject;
            bool isButtonSelected = (cancelButton != null && currentSelected == cancelButton.gameObject) ||
                                    (confirmButton != null && currentSelected == confirmButton.gameObject);

            // If neither button is selected, select the first available one
            if (!isButtonSelected)
            {
                if (cancelButton != null) { EventSystem.current.SetSelectedGameObject(cancelButton.gameObject); }
                else if (confirmButton != null) { EventSystem.current.SetSelectedGameObject(confirmButton.gameObject); }
            }
        }

        void ApplyCloseBehavior()
        {
            switch (closeBehavior)
            {
                case CloseBehavior.Disable:
                    gameObject.SetActive(false);
                    break;
                case CloseBehavior.Destroy:
                    Destroy(gameObject);
                    break;
            }
        }

        void OnConfirmClicked()
        {
            onConfirm?.Invoke();

            if (closeOnConfirm)
                Close();
        }

        void OnCancelClicked()
        {
            onCancel?.Invoke();

            if (closeOnCancel)
                Close();
        }

        void SetInitialStateForAnimation(bool IsOpening)
        {
            switch (animationType)
            {
                case AnimationType.None:
                    canvasGroup.alpha = IsOpening ? 0f : 1f;
                    break;
                case AnimationType.Fade:
                    canvasGroup.alpha = IsOpening ? 0f : 1f;
                    break;
                case AnimationType.Scale:
                    canvasGroup.alpha = IsOpening ? 0f : 1f;
                    contentParent.localScale = IsOpening ? Vector3.one * scaleFrom : originalScale;
                    break;
                case AnimationType.Slide:
                    canvasGroup.alpha = IsOpening ? 0f : 1f;
                    contentParent.anchoredPosition = IsOpening ? originalPosition + slideOffset : originalPosition;
                    break;
            }
        }

        /// <summary>
        /// Resolves stuck animations by syncing visuals with logic after interruption.
        /// </summary>
        void ForceVisualState()
        {
            if (canvasGroup == null || contentParent == null)
                return;

            if (IsOpen)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                contentParent.localScale = originalScale;
                contentParent.anchoredPosition = originalPosition;
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                if (animationType == AnimationType.Scale) { contentParent.localScale = Vector3.one * scaleFrom; }
                else if (animationType == AnimationType.Slide) { contentParent.anchoredPosition = originalPosition + slideOffset; }
            }
        }

        IEnumerator DelayedOpenModal()
        {
            yield return null;
            Open();
        }

        IEnumerator AnimateOpen()
        {
            IsOpen = true;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            onOpen?.Invoke();

            if (animationType == AnimationType.None)
            {
                canvasGroup.alpha = 1f;
                yield break;
            }

            float elapsedTime = 0f;

            // Store starting values
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = contentParent.localScale;
            Vector2 startPosition = contentParent.anchoredPosition;

            // Target values
            float targetAlpha = 1f;
            Vector3 targetScale = originalScale;
            Vector2 targetPosition = originalPosition;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsedTime / animationDuration;
                float easedProgress = animationCurve.Evaluate(progress);

                // Always animate alpha (fade)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedProgress);

                // Apply specific animation based on type
                switch (animationType)
                {
                    case AnimationType.Scale:
                        contentParent.localScale = Vector3.Lerp(startScale, targetScale, easedProgress);
                        break;
                    case AnimationType.Slide:
                        contentParent.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, easedProgress);
                        break;
                }

                yield return null;
            }

            // Ensure final values are set
            canvasGroup.alpha = targetAlpha;
            contentParent.localScale = targetScale;
            contentParent.anchoredPosition = targetPosition;
        }

        IEnumerator AnimateClose()
        {
            IsOpen = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            onClose?.Invoke();

            if (animationType == AnimationType.None)
            {
                ApplyCloseBehavior();
                yield break;
            }

            float elapsedTime = 0f;

            // Store starting values
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = contentParent.localScale;
            Vector2 startPosition = contentParent.anchoredPosition;

            // Target values
            float targetAlpha = 0f;
            Vector3 targetScale = Vector3.one * scaleFrom;
            Vector2 targetPosition = originalPosition + slideOffset;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = elapsedTime / animationDuration;
                float easedProgress = animationCurve.Evaluate(progress);

                // Always animate alpha (fade)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedProgress);

                // Apply specific animation based on type
                switch (animationType)
                {
                    case AnimationType.Scale:
                        contentParent.localScale = Vector3.Lerp(startScale, targetScale, easedProgress);
                        break;
                    case AnimationType.Slide:
                        contentParent.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, easedProgress);
                        break;
                }

                yield return null;
            }

            // Ensure final values are set
            canvasGroup.alpha = targetAlpha;

            if (animationType == AnimationType.Scale) { contentParent.localScale = targetScale; }
            else if (animationType == AnimationType.Slide) { contentParent.anchoredPosition = targetPosition; }

            ApplyCloseBehavior();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            if (startBehavior == StartBehavior.Disabled)
                startBehavior = StartBehavior.Open;

            gameObject.SetActive(true);

            if (!gameObject.activeInHierarchy)
                return;

            if (navigationMode == NavigationMode.Focused)
                previousNavObject = Utilities.GetSelectedObject();

            SetInitialStateForAnimation(true);

            if (currentAnimation != null) { StopCoroutine(currentAnimation); }
            currentAnimation = StartCoroutine(AnimateOpen());
        }

        public void Close()
        {
            if (!IsOpen || !gameObject.activeInHierarchy)
                return;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            if (navigationMode == NavigationMode.Focused && previousNavObject != null)
                Utilities.SetSelectedObject(previousNavObject);

            currentAnimation = StartCoroutine(AnimateClose());
        }

        public void SetTitle(string newTitle)
        {
            title = newTitle;

            if (titleText != null && titleText.text != title)
                titleText.text = title;
        }

        public void SetDescription(string newDescription)
        {
            description = newDescription;

            if (descriptionText != null && descriptionText.text != description)
                descriptionText.text = description;
        }

        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;

            if (iconImage != null)
            {
                bool hasIcon = icon != null;

                if (iconImage.gameObject.activeSelf != hasIcon) { iconImage.gameObject.SetActive(hasIcon); }
                if (hasIcon && iconImage.sprite != icon) { iconImage.sprite = icon; }
            }
        }

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
                ApplyContent();
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (Application.isPlaying || customContent)
                return;

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                ApplyContent();
        }
#endif
    }
}