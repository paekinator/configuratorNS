using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/input-field")]
    [AddComponentMenu("Evo/UI/UI Elements/Input Field Enhancer")]
    public class InputFieldEnhancer : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool clearAfterSubmit = false;
        [SerializeField] private bool deselectOnEndEdit = false;
        [SerializeField] private bool handleShiftEnter = false;
        [SerializeField] private PlaceholderAnimation animationType = PlaceholderAnimation.Slide;
        [SerializeField] private Vector2 slideOffset = new(0, 20);
        [SerializeField, Range(0f, 1f)] private float fadeAlpha = 0;
        [SerializeField, Range(0.1f, 3f)] private float scaleMultiplier = 0.8f;
        [SerializeField, Range(0.1f, 2f)] private float animationDuration = 0.25f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("References", Constants.CustomEditorID)]
        public TMP_InputField source;
        public Interactive interactableObject;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public SubmitEvent onSubmit = new();

        // Enums
        public enum PlaceholderAnimation { Fade = 0, FadeScale = 1, Slide = 2 }

        // Component Cache
        TextMeshProUGUI placeholderText;
        RectTransform placeholderRect;

        // Yield Cache
        static readonly WaitForEndOfFrame waitForEndOfFrame = new();

        // Animation state
        Coroutine currentAnimation;
        Vector2 originalPlaceholderPosition;
        Vector3 originalPlaceholderScale;
        float originalPlaceholderAlpha = 0.5f;

        [System.Serializable] public class SubmitEvent : UnityEvent<string> { }

        // Styler Interface
        public StylerPreset Preset
        {
            get => null;
            set
            {
                if (placeholderText != null && !gameObject.activeInHierarchy) { UpdateStyler(); }
                else if (placeholderText != null) { StartCoroutine(UpdateStylerNextFrame()); }
            }
        }

        void Awake() => Initialize();

        void OnEnable()
        {
            if (source == null)
                return;

            AnimatePlaceholder(!string.IsNullOrEmpty(source.text));
        }

        void OnDestroy()
        {
            // Remove all listeners to prevent memory leaks and dangling references
            if (source != null)
            {
                source.onEndEdit.RemoveListener(OnEndEdit);
                source.onValueChanged.RemoveListener(OnValueChanged);
                source.onSelect.RemoveListener(OnSourceSelect);
                source.onDeselect.RemoveListener(OnSourceDeselect);

                if (interactableObject != null)
                {
                    source.onSelect.RemoveListener(OnSourceSelectSetInteractableState);
                    source.onDeselect.RemoveListener(OnSourceDeselectSetInteractableState);
                }
            }

            if (interactableObject != null)
            {
                interactableObject.OnStateChanged -= OnInteractableStateChanged;
                interactableObject.onSelect.RemoveListener(OnInteractableSelect);
            }
        }

        void Initialize()
        {
            if (source == null)
                TryGetComponent(out source);

            if (source == null)
            {
                Debug.LogError("[Input Field Enhancer] TMP Input Field is missing!", this);
                return;
            }

            // Set source settings if Shift+Enter enabled
            if (handleShiftEnter)
            {
                source.lineType = TMP_InputField.LineType.MultiLineSubmit;
                source.onFocusSelectAll = false;
            }

            source.onEndEdit.AddListener(OnEndEdit);
            source.onValueChanged.AddListener(OnValueChanged);
            // Named methods used here (instead of anonymous delegates) so listeners
            // can be cleanly removed in OnDestroy without leaking references
            source.onSelect.AddListener(OnSourceSelect);
            source.onDeselect.AddListener(OnSourceDeselect);

            if (source.placeholder != null)
            {
                placeholderText = source.placeholder.GetComponent<TextMeshProUGUI>();
                placeholderRect = placeholderText.rectTransform;
                source.placeholder = null;

                // Prevent native crossing if already enabled
                if (!placeholderText.enabled) { placeholderText.enabled = true; }

                originalPlaceholderPosition = placeholderRect.anchoredPosition;
                originalPlaceholderScale = placeholderRect.localScale;
                originalPlaceholderAlpha = placeholderText.color.a;
            }

            if (interactableObject != null)
            {
                interactableObject.OnStateChanged += OnInteractableStateChanged;
                interactableObject.onSelect.AddListener(OnInteractableSelect);

                source.onSelect.AddListener(OnSourceSelectSetInteractableState);
                source.onDeselect.AddListener(OnSourceDeselectSetInteractableState);
            }
        }

        void OnEndEdit(string value)
        {
            if (Utilities.WasEnterKeyPressed())
            {
                if (handleShiftEnter && Utilities.WasShiftKeyPressed())
                {
                    source.text += "\n";
                    Focus();
                    source.MoveTextEnd(false);
                    return;
                }

                onSubmit?.Invoke(value);

                if (clearAfterSubmit && !string.IsNullOrEmpty(source.text))
                    source.text = null;
            }

            if (deselectOnEndEdit && Utilities.GetSelectedObject() == source.gameObject)
                Utilities.SetSelectedObject(null);
        }

        void OnValueChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) { AnimatePlaceholder(false); }
            else { AnimatePlaceholder(true); }
        }
        void OnSourceSelect(string _) => AnimatePlaceholder(true);

        void OnSourceDeselect(string _)
        {
            if (string.IsNullOrEmpty(source.text))
                AnimatePlaceholder(false);
        }

        void OnInteractableSelect() => StartCoroutine(SelectHelper());

        void OnSourceSelectSetInteractableState(string _) => interactableObject.SetState(InteractionState.Selected);

        void OnSourceDeselectSetInteractableState(string _)
        {
            interactableObject.SetState(source.interactable
                ? InteractionState.Normal
                : InteractionState.Disabled);
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (newState == InteractionState.Disabled && source.interactable) { source.interactable = false; }
            else if (newState != InteractionState.Disabled && !source.interactable) { source.interactable = true; }
        }

        void AnimatePlaceholder(bool animate)
        {
            if (!gameObject.activeInHierarchy || placeholderRect == null || placeholderText == null)
                return;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            switch (animationType)
            {
                case PlaceholderAnimation.Fade:
                    currentAnimation = StartCoroutine(AnimateFade(animate));
                    break;
                case PlaceholderAnimation.FadeScale:
                    currentAnimation = StartCoroutine(AnimateFadeScale(animate));
                    break;
                case PlaceholderAnimation.Slide:
                    currentAnimation = StartCoroutine(AnimateSlide(animate));
                    break;
            }
        }

        IEnumerator SelectHelper()
        {
            yield return waitForEndOfFrame;
            source.Select();
        }

        IEnumerator AnimateFade(bool animate)
        {
            Color startColor = placeholderText.color;
            Color sourceColor = new(startColor.r, startColor.g, startColor.b, animate ? fadeAlpha : originalPlaceholderAlpha);

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                t = animationCurve.Evaluate(t);

                placeholderText.color = Color.Lerp(startColor, sourceColor, t);

                yield return null;
            }

            placeholderText.color = sourceColor;
            currentAnimation = null;
        }

        IEnumerator AnimateFadeScale(bool animate)
        {
            Color startColor = placeholderText.color;
            Color sourceColor = new(startColor.r, startColor.g, startColor.b, animate ? fadeAlpha : originalPlaceholderAlpha);

            Vector3 startScale = placeholderRect.localScale;
            Vector3 sourceScale = animate ? originalPlaceholderScale * scaleMultiplier : originalPlaceholderScale;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                t = animationCurve.Evaluate(t);

                placeholderText.color = Color.Lerp(startColor, sourceColor, t);
                placeholderRect.localScale = Vector3.Lerp(startScale, sourceScale, t);

                yield return null;
            }

            placeholderText.color = sourceColor;
            placeholderRect.localScale = sourceScale;
            currentAnimation = null;
        }

        IEnumerator AnimateSlide(bool animate)
        {
            Vector2 startPosition = placeholderRect.anchoredPosition;
            Vector2 sourcePosition = animate ? originalPlaceholderPosition + slideOffset : originalPlaceholderPosition;

            Vector3 startScale = placeholderRect.localScale;
            Vector3 sourceScale = animate ? originalPlaceholderScale * scaleMultiplier : originalPlaceholderScale;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                t = animationCurve.Evaluate(t);

                placeholderRect.anchoredPosition = Vector2.Lerp(startPosition, sourcePosition, t);
                placeholderRect.localScale = Vector3.Lerp(startScale, sourceScale, t);

                yield return null;
            }

            placeholderRect.anchoredPosition = sourcePosition;
            placeholderRect.localScale = sourceScale;
            currentAnimation = null;
        }

        IEnumerator UpdateStylerNextFrame()
        {
            bool hasText = !string.IsNullOrEmpty(source.text);

            // Optimization: Prevent redundant native component crossing if state matches
            if (hasText && placeholderText.enabled) { placeholderText.enabled = false; }

            // Check for affected transitions
            if (animationType != PlaceholderAnimation.Slide && currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            // Wait a frame for child object to be updated properly
            yield return waitForEndOfFrame;

            // Update cached and current references
            UpdateStyler();

            // We're done
            if (hasText && !placeholderText.enabled) { placeholderText.enabled = true; }
        }

        public void UpdateStyler()
        {
            if (placeholderText.TryGetComponent<StylerObject>(out var stObj) && !stObj.enableInteraction)
            {
                Color clr = stObj.Preset.GetColor(stObj.colorID);
                originalPlaceholderAlpha = clr.a;

                Color targetColor = new(clr.r, clr.g, clr.b, string.IsNullOrEmpty(source.text) ? originalPlaceholderAlpha : fadeAlpha);

                // Check if color is changed
                if (placeholderText.color != targetColor) { placeholderText.color = targetColor; }
            }
        }

        public void Focus()
        {
            source.Select();
            source.ActivateInputField();
        }

        public void SetInteractable(bool value)
        {
            if (source.interactable != value) { source.interactable = value; }
            if (interactableObject != null) { interactableObject.SetInteractable(value); }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}