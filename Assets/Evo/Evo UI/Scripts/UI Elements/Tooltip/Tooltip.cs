using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/tooltip")]
    [AddComponentMenu("Evo/UI/UI Elements/Tooltip")]
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        public GameObject tooltipPreset;
        public Sprite icon;
        public string title;
        [TextArea(2, 4)] public string description;

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool followCursor = true;
        public bool is3DObject = false;
        [SerializeField] private float maxWidth = 400f;
        [SerializeField, Range(0f, 10f)] private float showDelay = 0f;
        [SerializeField, Range(0f, 40f)] private float movementSmoothing = 0f;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Slide;
        [Range(0.01f, 1f)] public float animationDuration = 0.1f;
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField, Range(0f, 1f)] private float scaleFrom = 0.7f;
        [SerializeField] private Vector2 slideOffset = new(0f, -20f);

        [EvoHeader("Position & Offset", Constants.CustomEditorID)]
        [SerializeField] private OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        [SerializeField] private Vector2 customOffset = new(10f, 10f);
        [SerializeField] private float offsetDistance = 30f;
        [SerializeField] private float screenEdgePadding = 10f;

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private Canvas tooltipCanvas;
        public GameObject customContent;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        // Enums
        public enum AnimationType
        {
            None = 0,
            Fade = 1,
            Scale = 2,
            Slide = 3
        }

        // Cache
        TooltipPreset tooltipInstance;
        Coroutine showCoroutine;
        Coroutine positionCoroutine;
        Coroutine animationCoroutine;

        // Helpers
        bool isCustomLogGiven;
        float lastShowDelay = -1f;

        // Cache
        Canvas cachedCanvas;
        Camera cachedCamera;
        RectTransform cachedCanvasRect;
        RectTransform myRectTransform;
        WaitForSecondsRealtime cachedWaitDelay;
        Vector2 cachedOffset;
        bool offsetNeedsUpdate;
        bool isUIElement;

        // Properties
        Canvas ActiveCanvas
        {
            get
            {
                if (cachedCanvas != null)
                    return cachedCanvas;

                if (tooltipCanvas != null) { cachedCanvas = tooltipCanvas; }
                else
                {
                    cachedCanvas = GetComponentInParent<Canvas>();
                    if (cachedCanvas == null) { cachedCanvas = Globals.GetCanvas(); }
                }

                if (cachedCanvas != null)
                    cachedCanvasRect = cachedCanvas.GetComponent<RectTransform>();

                return cachedCanvas;
            }
        }

        // Public Properties
        public TooltipPreset Instance => tooltipInstance;
        public bool IsVisible() => tooltipInstance != null;

        void Awake() => isUIElement = TryGetComponent(out myRectTransform);

#if EVO_LOCALIZATION
        void Start()
        {
            if (enableLocalization && customContent == null)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
        }
#endif

        void OnDisable() => Hide();

        void OnDestroy()
        {
#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
#endif
            Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!is3DObject)
                Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!is3DObject)
                Hide();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!is3DObject)
                Show();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!is3DObject)
                Hide();
        }

        void OnMouseEnter()
        {
            if (is3DObject && enabled)
                Show();
        }

        void OnMouseExit()
        {
            if (is3DObject && enabled)
                Hide();
        }

        void StopAllTooltipCoroutines()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }
            if (positionCoroutine != null)
            {
                StopCoroutine(positionCoroutine);
                positionCoroutine = null;
            }
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        void SetInitialAnimationState()
        {
            if (tooltipInstance == null)
                return;

            switch (animationType)
            {
                case AnimationType.Fade:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    break;

                case AnimationType.Scale:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    tooltipInstance.transform.localScale = Vector3.one * scaleFrom;
                    break;

                case AnimationType.Slide:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    // Store the current position as base, then apply offset
                    Vector3 currentPos = tooltipInstance.tooltipRect.anchoredPosition;
                    tooltipInstance.tooltipRect.anchoredPosition = currentPos + (Vector3)slideOffset;
                    break;
            }
        }

        void ApplyAnimationState(float progress, bool isIn, Vector3 startScale = default)
        {
            if (tooltipInstance == null)
                return;

            switch (animationType)
            {
                case AnimationType.Fade:
                    tooltipInstance.canvasGroup.alpha = progress;
                    break;
                case AnimationType.Scale:
                    tooltipInstance.canvasGroup.alpha = progress;
                    if (isIn) { tooltipInstance.transform.localScale = Vector3.Lerp(Vector3.one * scaleFrom, Vector3.one, progress); }
                    else { tooltipInstance.transform.localScale = Vector3.Lerp(startScale, Vector3.one * scaleFrom, 1f - progress); }
                    break;
                case AnimationType.Slide:
                    tooltipInstance.canvasGroup.alpha = progress;
                    if (isIn)
                    {
                        Vector2 currentTarget = CalculateCurrentTargetPosition();
                        Vector2 slideStartPos = currentTarget + slideOffset;
                        tooltipInstance.tooltipRect.anchoredPosition = Vector2.Lerp(slideStartPos, currentTarget, progress);
                    }
                    break;
            }
        }

        Vector2 CalculateTargetPosition(Camera targetCamera)
        {
            Canvas canvas = ActiveCanvas;

            if (canvas == null || cachedCanvasRect == null)
                return Vector2.zero;

            Vector2 screenPoint;

            // Get screen point based on object type
            if (followCursor) { screenPoint = Utilities.GetPointerPosition(); }
            else if (isUIElement && myRectTransform != null)
            {
                // For UI elements
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { screenPoint = myRectTransform.position; }
                else
                {
                    Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                    if (cam != null) { screenPoint = RectTransformUtility.WorldToScreenPoint(cam, myRectTransform.position); }
                    else { screenPoint = myRectTransform.position; }
                }

                // Adjust for non-centered pivot
                Vector2 pivot = myRectTransform.pivot;
                Vector2 pivotOffset = new(
                    (pivot.x - 0.5f) * myRectTransform.rect.width * myRectTransform.lossyScale.x,
                    (pivot.y - 0.5f) * myRectTransform.rect.height * myRectTransform.lossyScale.y
                );

                screenPoint.x -= pivotOffset.x;
                screenPoint.y -= pivotOffset.y;
            }
            else
            {
                // For 3D objects
                if (targetCamera == null)
                    targetCamera = Camera.main;

                screenPoint = targetCamera != null ? targetCamera.WorldToScreenPoint(transform.position) : Vector2.zero;
            }

            // Convert screen point to canvas anchored position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cachedCanvasRect,
                screenPoint,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 anchoredPosition
            );

            return anchoredPosition;
        }

        Camera GetTargetCamera()
        {
            if (cachedCamera != null)
                return cachedCamera;

            Canvas canvas = ActiveCanvas;
            if (canvas != null && canvas.worldCamera != null)
            {
                cachedCamera = canvas.worldCamera;
                return cachedCamera;
            }

            if (isUIElement)
            {
                // ActiveCanvas lookup already checked parent structure, use it safely
                if (canvas != null && canvas.worldCamera != null)
                    return canvas.worldCamera;
            }

            cachedCamera = Camera.main;
            return cachedCamera;
        }

        void UpdateCachedOffset()
        {
            if (offsetPosition == OffsetPosition.Custom)
            {
                cachedOffset = customOffset;
                return;
            }

            if (tooltipInstance == null || tooltipInstance.tooltipRect == null)
            {
                cachedOffset = Vector2.zero;
                return;
            }

            float width = tooltipInstance.tooltipRect.rect.width;
            float height = tooltipInstance.tooltipRect.rect.height;

            switch (offsetPosition)
            {
                case OffsetPosition.TopLeft:
                    cachedOffset = new Vector2(-width * 0.5f - offsetDistance, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.TopRight:
                    cachedOffset = new Vector2(width * 0.5f + offsetDistance, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.BottomLeft:
                    cachedOffset = new Vector2(-width * 0.5f - offsetDistance, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.BottomRight:
                    cachedOffset = new Vector2(width * 0.5f + offsetDistance, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.Top:
                    cachedOffset = new Vector2(0, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.Bottom:
                    cachedOffset = new Vector2(0, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.Left:
                    cachedOffset = new Vector2(-width * 0.5f - offsetDistance, 0);
                    break;
                case OffsetPosition.Right:
                    cachedOffset = new Vector2(width * 0.5f + offsetDistance, 0);
                    break;
            }
        }

        Vector2 CalculateCurrentTargetPosition()
        {
            if (tooltipInstance == null)
                return Vector2.zero;

            Vector2 currentTarget = CalculateTargetPosition(GetTargetCamera());
            currentTarget += cachedOffset;

            return ClampToCanvasBounds(currentTarget, tooltipInstance.tooltipRect);
        }

        Vector2 ClampToCanvasBounds(Vector2 anchoredPosition, RectTransform tooltipRect)
        {
            Canvas canvas = ActiveCanvas;

            if (canvas == null || cachedCanvasRect == null)
                return anchoredPosition;

            float width = tooltipRect.rect.width;
            float height = tooltipRect.rect.height;

            // Calculate canvas bounds in local space
            float canvasWidth = cachedCanvasRect.rect.width;
            float canvasHeight = cachedCanvasRect.rect.height;

            // Account for padding
            float minX = -canvasWidth * 0.5f + width * 0.5f + screenEdgePadding;
            float maxX = canvasWidth * 0.5f - width * 0.5f - screenEdgePadding;
            float minY = -canvasHeight * 0.5f + height * 0.5f + screenEdgePadding;
            float maxY = canvasHeight * 0.5f - height * 0.5f - screenEdgePadding;

            // Clamp position
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);

            return anchoredPosition;
        }

        IEnumerator ShowTooltip()
        {
            if (cachedWaitDelay == null || lastShowDelay != showDelay)
            {
                cachedWaitDelay = new WaitForSecondsRealtime(showDelay);
                lastShowDelay = showDelay;
            }

            if (showDelay > 0)
                yield return cachedWaitDelay;

            // Verify object is still active and valid
            if (this == null || !gameObject.activeInHierarchy)
                yield break;

            // Determine which prefab to use
            GameObject prefabToUse = customContent != null ? customContent : tooltipPreset;

            if (prefabToUse == null)
            {
                Debug.LogWarning($"No Tooltip Preset or Custom Content assigned to {gameObject.name}.", this);
                yield break;
            }

            // Instantiate tooltip
            GameObject toGo = Instantiate(prefabToUse, ActiveCanvas.transform);
            bool isCustom = customContent != null;

            // Try to get existing TooltipPreset, or add one if missing
            if (!toGo.TryGetComponent(out tooltipInstance))
            {
                tooltipInstance = toGo.AddComponent<TooltipPreset>();
                if (!isCustomLogGiven)
                {
                    Debug.LogWarning($"<b>Tooltip Preset</b> component is not attached to <b>{gameObject.name}</b>, " +
                        $"but it's assigned as Tooltip Preset." +
                        $"\nThis tooltip will be treated as Custom Content, and the default content will be skipped.\n", this);
                    isCustomLogGiven = true;
                }
                isCustom = true;
            }

            // Setup tooltip content
            tooltipInstance.Setup(title, description, icon, maxWidth, isCustom);

            // Force layout update once to get accurate size, then cache the offset
            Canvas.ForceUpdateCanvases();
            UpdateCachedOffset();
            offsetNeedsUpdate = false;

            // Start position updates immediately for all animations
            positionCoroutine = StartCoroutine(UpdateTooltipPosition());

            // Start animations
            if (tooltipInstance != null && animationType != AnimationType.None)
                animationCoroutine = StartCoroutine(AnimateTooltipIn());

            // Set coroutine
            showCoroutine = null;
        }

        IEnumerator UpdateTooltipPosition()
        {
            if (tooltipInstance.tooltipRect == null)
                yield break;

            // Cache camera reference
            Camera targetCamera = GetTargetCamera();

            while (tooltipInstance != null && this != null && gameObject.activeInHierarchy)
            {
                // Recalculate layout offset only if text/content changed mid-display
                if (offsetNeedsUpdate)
                {
                    Canvas.ForceUpdateCanvases();
                    UpdateCachedOffset();
                    offsetNeedsUpdate = false;
                }

                Vector2 newTargetPosition = CalculateTargetPosition(targetCamera);
                newTargetPosition += cachedOffset;
                newTargetPosition = ClampToCanvasBounds(newTargetPosition, tooltipInstance.tooltipRect);

                if (movementSmoothing == 0) { tooltipInstance.tooltipRect.anchoredPosition = newTargetPosition; }
                else
                {
                    Vector2 currentPosition = tooltipInstance.tooltipRect.anchoredPosition;
                    Vector2 smoothedPosition = Vector2.Lerp(currentPosition, newTargetPosition, movementSmoothing * Time.unscaledDeltaTime);
                    tooltipInstance.tooltipRect.anchoredPosition = smoothedPosition;
                }

                yield return null;
            }
        }

        IEnumerator AnimateTooltipIn()
        {
            if (tooltipInstance == null)
                yield break;

            SetInitialAnimationState();

            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = animationCurve.Evaluate(progress);

                ApplyAnimationState(curveValue, true);

                yield return null;
            }

            // Ensure final state
            ApplyAnimationState(1f, true);
            animationCoroutine = null;
        }

        IEnumerator AnimateTooltipOut()
        {
            if (tooltipInstance == null)
            {
                HideImmediate();
                yield break;
            }

            // Store initial values
            float startingAlpha = tooltipInstance.canvasGroup.alpha;
            Vector3 startingScale = tooltipInstance.transform.localScale;
            Vector2 startPosition = tooltipInstance.tooltipRect.anchoredPosition;

            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;

                switch (animationType)
                {
                    case AnimationType.Fade:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        break;
                    case AnimationType.Scale:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        tooltipInstance.transform.localScale = Vector3.Lerp(startingScale, Vector3.one * scaleFrom, progress);
                        break;
                    case AnimationType.Slide:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        Vector2 endPos = startPosition + slideOffset;
                        tooltipInstance.tooltipRect.anchoredPosition = Vector2.Lerp(startPosition, endPos, progress);
                        break;
                }

                yield return null;
            }

            HideImmediate();
        }

        public void Show()
        {
            // Check if we have either a tooltip preset or custom content
            if (tooltipPreset == null && customContent == null)
            {
                Debug.LogWarning($"No tooltip preset or custom content assigned to {gameObject.name}. " +
                    $"Please assign a prefab with 'Tooltip Preset' component or a custom content GameObject.", this);
                return;
            }

            StopAllTooltipCoroutines();
            HideImmediate();

            showCoroutine = StartCoroutine(ShowTooltip());
            onShow?.Invoke();
        }

        public void Hide()
        {
            StopAllTooltipCoroutines();

            // Hide the tooltip
            if (!gameObject.activeInHierarchy || animationType == AnimationType.None) { HideImmediate(); }
            else if (tooltipInstance != null) { animationCoroutine = StartCoroutine(AnimateTooltipOut()); }

            onHide?.Invoke();
        }

        public void HideImmediate()
        {
            if (tooltipInstance != null)
            {
                Destroy(tooltipInstance.gameObject);
                tooltipInstance = null;
            }
        }

        public void SetContent(string newTitle, string newDescription, Sprite newIcon = null, GameObject newCustomContent = null)
        {
            if (newCustomContent != null)
            {
                customContent = newCustomContent;
                if (tooltipInstance != null)
                {
                    HideImmediate();
                    Show();
                }
                return;
            }

            title = newTitle;
            description = newDescription;
            icon = newIcon;
            customContent = newCustomContent;

            if (tooltipInstance != null)
            {
                tooltipInstance.Setup(title, description, icon, maxWidth);
                offsetNeedsUpdate = true; // Flag to recalculate offset on next frame due to text change
            }
        }

        public void SetContent(GameObject newCustomContent) => SetContent(null, null, null, newCustomContent);

        public void SetTitle(string newTitle)
        {
            title = newTitle;

            if (tooltipInstance != null)
            {
                tooltipInstance.Setup(title, description, icon, maxWidth);
                offsetNeedsUpdate = true;
            }
        }

        public void SetDescription(string newDescription)
        {
            description = newDescription;

            if (tooltipInstance != null)
            {
                tooltipInstance.Setup(title, description, icon, maxWidth);
                offsetNeedsUpdate = true;
            }
        }

        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;

            if (tooltipInstance != null)
            {
                tooltipInstance.Setup(title, description, icon, maxWidth);
                offsetNeedsUpdate = true;
            }
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(titleKey)) 
            { 
                title = localizedObject.GetString(titleKey); 
                changed = true;
            }
            if (!string.IsNullOrEmpty(descriptionKey)) 
            { 
                description = localizedObject.GetString(descriptionKey); 
                changed = true;
            }

            if (changed && tooltipInstance != null)
            {
                tooltipInstance.Setup(title, description, icon, maxWidth);
                offsetNeedsUpdate = true;
            }
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}