using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "navigation/selection-box")]
    [AddComponentMenu("Evo/UI/Navigation/Selection Box")]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SelectionBox : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Duration of the fade in/out animation")]
        [SerializeField, Range(0f, 1f)] private float fadeDuration = 0.1f;

        [Tooltip("Duration of the movement and resize animation")]
        [SerializeField, Range(0f, 1f)] private float animationDuration = 0.15f;

        [Tooltip("Animation curve for position and size transitions")]
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Settings")]
        [Tooltip("If true, only objects with SelectionBoxOverride component will be selectable.")]
        public bool onlyOverrideObjects = false;
        [Tooltip("Default offset from the selected object's edges.")]
        [SerializeField] private Vector2 defaultOffset = new(10f, 10f);

        [Header("Visuals")]
        [Tooltip("Default sprite/texture for the selection box.")]
        [SerializeField] private Sprite defaultSprite;

        [Tooltip("Default color for the selection box.")]
        [SerializeField] private Color defaultColor = Color.white;

        [Tooltip("Pixels per unit multiplier for sliced images.")]
        [SerializeField] private float defaultPPU = 2.25f;

        // Properties
        public RectTransform CurrentTarget { get; private set; }

        // Cache
        Image boxImage;
        RectTransform rectTransform;
        CanvasGroup canvasGroup;
        Canvas overrideCanvas; 
        Coroutine animationCoroutine;
        Coroutine fadeCoroutine;
        EventSystem eventSystem;
        Canvas parentCanvas;
        SelectionBoxOverride currentOverride;

        // Helpers
        bool isVisible;
        bool isFadingIn;
        bool isFadingOut;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();

            // Setup image
            if (TryGetComponent<Image>(out var img)) { boxImage = img; }
            else { boxImage = gameObject.AddComponent<Image>(); }
            boxImage.raycastTarget = false;
            boxImage.sprite = defaultSprite;
            boxImage.color = defaultColor;
            boxImage.type = Image.Type.Sliced;
            boxImage.pixelsPerUnitMultiplier = defaultPPU;

            // Setup canvas
            if (TryGetComponent<Canvas>(out var cnv)) { overrideCanvas = cnv; }
            else { overrideCanvas = gameObject.AddComponent<Canvas>(); }
            overrideCanvas.overrideSorting = true;
            overrideCanvas.vertexColorAlwaysGammaSpace = true;
            overrideCanvas.sortingOrder = 32767; // Max sorting order to always be on top

            // Get parent canvas for proper positioning
            parentCanvas = GetComponentInParent<Canvas>();

            // Start fully transparent
            canvasGroup.alpha = 0f;
            isVisible = false;
            isFadingIn = false;
            isFadingOut = false;
        }

        void Start() => eventSystem = EventSystem.current;

        void Update() => CheckForSelectionChange();

        void LateUpdate()
        {
            // Continuously update if target moves (for dynamic UIs)
            if (CurrentTarget != null && isVisible && animationCoroutine == null)
            {
                Vector2 targetOffset = currentOverride != null ? currentOverride.offset : defaultOffset;
                Vector2 targetPos = GetTargetPosition(CurrentTarget);
                Vector2 targetSize = CurrentTarget.rect.size + targetOffset * 2f;

                // Check if target has moved/resized significantly
                if (Vector2.Distance(rectTransform.anchoredPosition, targetPos) > 0.5f || Vector2.Distance(rectTransform.sizeDelta, targetSize) > 0.5f)
                    animationCoroutine = StartCoroutine(AnimateToTarget());
            }
        }

        void CheckForSelectionChange()
        {
            if (eventSystem == null)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;

            // Check if selected object is null OR disabled in hierarchy
            if (selected == null || !selected.activeInHierarchy)
            {
                if (isVisible || isFadingIn)
                    Deselect();

                return;
            }

            // If selected object doesn't have RectTransform, hide
            if (!selected.TryGetComponent<RectTransform>(out var selectedRect))
            {
                if (isVisible || isFadingIn)
                    Deselect();

                return;
            }

            // Check if we should only select objects with override component
            if (onlyOverrideObjects)
            {
                if (!selectedRect.TryGetComponent<SelectionBoxOverride>(out var _))
                {
                    if (isVisible || isFadingIn)
                        Deselect();

                    return;
                }
            }

            // If same as current and target still exists, skip
            if (selectedRect == CurrentTarget && CurrentTarget != null)
                return;

            // New selection detected
            SelectObject(selectedRect);
        }

        public void SelectObject(RectTransform target)
        {
            if (target == null)
            {
                Deselect();
                return;
            }

            CurrentTarget = target;

            // Apply override if present
            if (target.TryGetComponent<SelectionBoxOverride>(out var newOverride)) { ApplyOverride(newOverride); }
            else { RevertToDefaults(); }

            // Only fade in if not already visible or fading in
            if (!isVisible && !isFadingIn)
            {
                // Stop any ongoing fade out
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                    fadeCoroutine = null;
                }

                // Start fade in
                fadeCoroutine = StartCoroutine(FadeIn());
            }

            // Animate to target
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            animationCoroutine = StartCoroutine(AnimateToTarget());
        }

        public void Deselect()
        {
            // Don't deselect if already hidden or currently fading out
            if (!isVisible && !isFadingIn && !isFadingOut)
                return;

            CurrentTarget = null;
            currentOverride = null;

            // Only start fade out if not already fading out
            if (!isFadingOut)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                    fadeCoroutine = null;
                }
                fadeCoroutine = StartCoroutine(FadeOut());
            }

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        void ApplyOverride(SelectionBoxOverride overrideSettings)
        {
            currentOverride = overrideSettings;

            boxImage.sprite = overrideSettings.overrideSprite != null ? overrideSettings.overrideSprite : defaultSprite;
            boxImage.color = overrideSettings.overrideColor ? overrideSettings.color : defaultColor;
            boxImage.pixelsPerUnitMultiplier = overrideSettings.overridePPU ? overrideSettings.PPUMultiplier : defaultPPU;
        }

        void RevertToDefaults()
        {
            currentOverride = null;

            boxImage.sprite = defaultSprite;
            boxImage.color = defaultColor;
            boxImage.pixelsPerUnitMultiplier = defaultPPU;
        }

        Vector2 GetTargetPosition(RectTransform target)
        {
            if (target == null)
                return Vector2.zero;

            // If same parent, calculate position based on pivot
            if (target.parent == rectTransform.parent)
            {
                // Get the center position of the target rect regardless of pivot
                Vector2 targetCenter = target.anchoredPosition +
                    new Vector2(
                        target.rect.width * (0.5f - target.pivot.x),
                        target.rect.height * (0.5f - target.pivot.y)
                    );

                return targetCenter;
            }

            // Convert world position to local position of this selection box's parent
            // Use the center of the target rect
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[2]) / 2f;

            if (rectTransform.parent is RectTransform parentRect)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect,
                    RectTransformUtility.WorldToScreenPoint(parentCanvas != null ? parentCanvas.worldCamera : null, worldCenter),
                    parentCanvas != null ? parentCanvas.worldCamera : null, out Vector2 localPos
                );
                return localPos;
            }

            return target.anchoredPosition;
        }

        IEnumerator FadeIn()
        {
            isFadingIn = true;
            yield return Utilities.CrossFadeCanvasGroup(canvasGroup, 1f, fadeDuration, true);
            isVisible = true;
            isFadingIn = false;
            fadeCoroutine = null;
        }

        IEnumerator FadeOut()
        {
            isFadingIn = false;
            isFadingOut = true;
            yield return Utilities.CrossFadeCanvasGroup(canvasGroup, 0f, fadeDuration, true);
            isVisible = false;
            isFadingOut = false;
            fadeCoroutine = null;
        }

        IEnumerator AnimateToTarget()
        {
            if (CurrentTarget == null)
                yield break;

            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 startSize = rectTransform.sizeDelta;
            Vector2 targetOffset = currentOverride != null ? currentOverride.offset : defaultOffset;

            // Calculate target position and size correctly
            Vector2 targetPos = GetTargetPosition(CurrentTarget);
            Vector2 targetSize = CurrentTarget.rect.size + targetOffset * 2f;

            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                // Check if target was destroyed during animation
                if (CurrentTarget == null)
                {
                    animationCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float curveValue = animationCurve.Evaluate(t);

                rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveValue);
                rectTransform.sizeDelta = Vector2.Lerp(startSize, targetSize, curveValue);

                yield return null;
            }

            // Ensure we end exactly at target
            if (CurrentTarget != null)
            {
                rectTransform.anchoredPosition = targetPos;
                rectTransform.sizeDelta = targetSize;
            }

            animationCoroutine = null;
        }
    }
}