using UnityEngine;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/off-screen-indicator")]
    [AddComponentMenu("Evo/UI/UI Elements/Off Screen Indicator")]
    public class OffScreenIndicator : MonoBehaviour
    {
        [EvoHeader("Customization", Constants.CustomEditorID)]
        [SerializeField] private GameObject indicatorPreset;
        public Sprite indicatorIcon;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool trackUIElement;
        public bool hideWhenOnScreen;
        public float checkDistance = 100f; // In metric
        public float borderOffset = 40f;
        [Range(0.01f, 2f)] public float fadeDuration = 0.1f;
        [Range(0f, 1f)] public float transitionEase = 0f;

        [EvoHeader("Distance Settings", Constants.CustomEditorID)]
        public DistanceUnit distanceUnit = DistanceUnit.None;
        public string distanceFormat = "{0}{1}"; // {0} = distance, {1} = unit
        public Transform distanceSource;
        public bool enableDistanceFade = true;
        public float fadeStartDistance = 5;
        public float fadeEndDistance = 2;

        [EvoHeader("References", Constants.CustomEditorID)]
        public Transform targetTransform;
        public Camera targetCamera;
        [SerializeField] private Canvas targetCanvas;
        public RectTransform rectBoundary;

        // Enums
        public enum DistanceUnit { None, Metric, Imperial }

        // Cache
        OffScreenIndicatorObject indicatorObject;
        RectTransform indicatorRect;
        RectTransform targetCanvasRect;
        float targetObjectVisibilityAlpha = 1;
        Vector2 screenCenter;
        Vector2 currentPosition;
        Vector2 positionVelocity;
        Vector3 cachedTargetPosition;
        float cachedDistanceToTarget;
        float screenWidth;
        float screenHeight;
        float dynamicBorderOffset;
        bool isDisabling;

        // Rect boundary cache
        Vector2 rectBoundaryMin;
        Vector2 rectBoundaryMax;
        Vector2 rectBoundaryCenter;
        readonly Vector3[] rectBoundaryCorners = new Vector3[4];

        // Constants
        const float MetersToFeet = 3.28084f;
        const float MetersToKilometers = 0.001f;
        const float FeetToMiles = 1f / 5280f;
       

        void Awake()
        {
            // Use self if no targetObject specified
            if (targetTransform == null) { targetTransform = transform; }

            // Cache screen values that don't change often
            UpdateScreenValues();
        }

        void Start()
        {
            SetupCamera();
            SetupCanvas();
            SetupDistanceSource();
            CreateIndicatorObject();
        }

        void OnEnable()
        {
            if (indicatorObject != null)
                indicatorObject.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (indicatorObject != null)
                indicatorObject.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (indicatorObject != null && indicatorObject.gameObject != null)
                Destroy(indicatorObject.gameObject);
        }

        void Update()
        {
            if (!isDisabling && (targetTransform == null || indicatorObject == null || targetCamera == null))
                return;

            if (isDisabling)
            {
                UpdateAlphaValues();
                return;
            }

            UpdateIndicator();
        }

        void SetupCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    Debug.LogError("[Off Screen Indicator] No camera assigned and no main camera found!", this);
                    Destroy(this);
                }
            }
        }

        void SetupDistanceSource()
        {
            if (distanceSource == null)
                distanceSource = targetCamera.transform;
        }

        void SetupCanvas()
        {
            // Check if targetCanvas is assigned first
            if (targetCanvas != null)
            {
                targetCanvasRect = targetCanvas.GetComponent<RectTransform>();
                return;
            }

            // Get global targetCanvas if missing
            targetCanvas = Globals.GetCanvas();
            targetCanvasRect = targetCanvas.GetComponent<RectTransform>();
        }

        void CreateIndicatorObject()
        {
            if (indicatorPreset == null)
            {
                Debug.LogError("[Off Screen Indicator] No indicator preset assigned!", this);
                return;
            }

            GameObject indicatorInstance = Instantiate(indicatorPreset, targetCanvas.transform);
            indicatorInstance.name = $"Off Screen Indicator ({gameObject.name})";
            indicatorObject = indicatorInstance.GetComponent<OffScreenIndicatorObject>();
            indicatorObject.targetTransform = targetTransform;
            indicatorRect = indicatorInstance.GetComponent<RectTransform>();

            // Set anchor and pivot to center for proper positioning
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);

            indicatorObject.Initialize();
            indicatorObject.SetIcon(indicatorIcon);
            indicatorObject.gameObject.SetActive(false);
        }

        void UpdateIndicator()
        {
            // Skip updates if disabling
            if (isDisabling)
                return;

            // Cache targetObject position for this frame
            cachedTargetPosition = targetTransform.position;

            // Update screen/boundary values if resolution changed or using rect boundary
            if (rectBoundary != null) { UpdateRectBoundaryValues(); }
            else if (Mathf.Abs(Screen.width - screenWidth) > 0.1f || Mathf.Abs(Screen.height - screenHeight) > 0.1f) { UpdateScreenValues(); }

            Vector3 targetObjectScreenPos;
            bool isBehindCamera = false;

            if (trackUIElement)
            {
                // For UI elements, get screen position directly from RectTransform
                RectTransform targetRect = targetTransform as RectTransform;
                if (targetRect != null)
                {
                    Vector3[] corners = new Vector3[4];
                    targetRect.GetWorldCorners(corners);
                    Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
                    targetObjectScreenPos = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, worldCenter);
                    targetObjectScreenPos.z = 1; // Always in front for UI elements
                }
                else
                {
                    // Fallback if not a RectTransform
                    targetObjectScreenPos = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, cachedTargetPosition);
                    targetObjectScreenPos.z = 1;
                }
            }
            else
            {
                // For 3D objects, use camera world to screen
                targetObjectScreenPos = targetCamera.WorldToScreenPoint(cachedTargetPosition);
                isBehindCamera = targetObjectScreenPos.z < 0;
            }

            bool isOnScreen = IsPositionOnScreen(targetObjectScreenPos, isBehindCamera);
            cachedDistanceToTarget = Vector3.Distance(distanceSource.position, cachedTargetPosition);

            // Determine visibility
            DetermineVisibility(isOnScreen, out bool shouldShowArrow, out bool shouldFollowOnScreen);

            // Activate indicator if needed
            if (!indicatorObject.gameObject.activeInHierarchy) { indicatorObject.gameObject.SetActive(true); }

            // Update alpha values
            UpdateAlphaValues();

            // Update UI elements
            indicatorObject.SetArrowVisible(shouldShowArrow);
            UpdateDistanceDisplay(shouldFollowOnScreen);

            // Calculate and apply position
            Vector2 calculatedPosition = CalculateIndicatorPosition(targetObjectScreenPos, isBehindCamera, shouldFollowOnScreen, shouldShowArrow);

            // Smooth position update
            currentPosition = Vector2.SmoothDamp(currentPosition, calculatedPosition, ref positionVelocity, transitionEase);
            indicatorObject.SetPosition(currentPosition);
        }

        void UpdateScreenValues()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
        }

        void UpdateRectBoundaryValues()
        {
            if (rectBoundary == null)
                return;

            // Get the world corners of the rect boundary
            rectBoundary.GetWorldCorners(rectBoundaryCorners);

            // Convert to screen space
            Vector2 corner0 = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rectBoundaryCorners[0]);
            Vector2 corner2 = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rectBoundaryCorners[2]);

            rectBoundaryMin = Vector2.Min(corner0, corner2);
            rectBoundaryMax = Vector2.Max(corner0, corner2);
            rectBoundaryCenter = (rectBoundaryMin + rectBoundaryMax) * 0.5f;

            // Update screen dimensions to match rect boundary
            screenWidth = rectBoundaryMax.x - rectBoundaryMin.x;
            screenHeight = rectBoundaryMax.y - rectBoundaryMin.y;
            screenCenter = rectBoundaryCenter;
        }

        void UpdateDynamicBorderOffset()
        {
            if (indicatorRect == null)
                return;

            // Get the actual world size of the indicator
            Vector3[] corners = new Vector3[4];
            indicatorRect.GetWorldCorners(corners);

            // Calculate the screen space size
            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            float indicatorScreenSize = Mathf.Max(Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));

            // Dynamic border offset = base offset + half of indicator size
            dynamicBorderOffset = borderOffset + (indicatorScreenSize * 0.5f);
        }

        bool IsPositionOnScreen(Vector3 screenPos, bool isBehind)
        {
            if (rectBoundary != null)
            {
                // Check against rect boundary
                return !isBehind &&
                       screenPos.x > rectBoundaryMin.x && screenPos.x < rectBoundaryMax.x &&
                       screenPos.y > rectBoundaryMin.y && screenPos.y < rectBoundaryMax.y;
            }
            else
            {
                // Check against screen
                return !isBehind &&
                       screenPos.x > 0 && screenPos.x < Screen.width &&
                       screenPos.y > 0 && screenPos.y < Screen.height;
            }
        }

        void DetermineVisibility(bool isOnScreen, out bool shouldShowArrow, out bool shouldFollowOnScreen)
        {
            shouldShowArrow = true;
            shouldFollowOnScreen = false;

            // Check distance first - if beyond checkDistance, always fade out
            if (cachedDistanceToTarget > checkDistance)
            {
                targetObjectVisibilityAlpha = 0f;

                // Keep current state but invisible
                if (isOnScreen && !hideWhenOnScreen)
                {
                    shouldShowArrow = false;
                    shouldFollowOnScreen = true;
                }

                return;
            }

            // Within checkDistance - normal behavior
            if (isOnScreen)
            {
                if (hideWhenOnScreen)
                {
                    shouldShowArrow = false;
                    shouldFollowOnScreen = true;
                    targetObjectVisibilityAlpha = 0f;
                }

                else
                {
                    shouldShowArrow = false;
                    shouldFollowOnScreen = true;
                    targetObjectVisibilityAlpha = 1f;
                }
            }
            else
            {
                // Off-screen and within distance
                targetObjectVisibilityAlpha = 1f;
            }
        }

        void UpdateAlphaValues()
        {
            // Calculate fade speed from duration
            float fadeSpeed = fadeDuration > 0 ? 1f / fadeDuration : 10f;

            // Handle disable fade
            if (isDisabling)
            {
                float currentAlpha = indicatorObject.GetVisibilityAlpha();
                float newAlpha = Mathf.MoveTowards(currentAlpha, 0f, fadeSpeed * Time.deltaTime);
                indicatorObject.SetVisibilityAlpha(newAlpha);
                indicatorObject.SetAlpha(newAlpha);

                // Check if fade complete
                if (newAlpha <= 0f)
                {
                    indicatorObject.gameObject.SetActive(false);
                    enabled = false;
                    isDisabling = false;
                }
                return;
            }

            // Normal visibility fade
            float currentVisibilityAlpha = indicatorObject.GetVisibilityAlpha();
            float newVisibilityAlpha = Mathf.MoveTowards(currentVisibilityAlpha, targetObjectVisibilityAlpha, fadeSpeed * Time.deltaTime);
            indicatorObject.SetVisibilityAlpha(newVisibilityAlpha);

            // Distance fade
            float distanceFadeAlpha = 1f;
            if (enableDistanceFade)
            {
                if (cachedDistanceToTarget <= fadeEndDistance)
                    distanceFadeAlpha = 0f;
                else if (cachedDistanceToTarget <= fadeStartDistance)
                    distanceFadeAlpha = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, cachedDistanceToTarget);
            }

            // Apply combined alpha
            indicatorObject.SetAlpha(newVisibilityAlpha * distanceFadeAlpha);
        }

        void UpdateDistanceDisplay(bool shouldFollowOnScreen)
        {
            bool shouldShowDistance = !hideWhenOnScreen && shouldFollowOnScreen && distanceUnit != DistanceUnit.None;
            indicatorObject.SetDistanceVisible(shouldShowDistance);

            if (shouldShowDistance)
            {
                string distanceString = FormatDistance(cachedDistanceToTarget);
                indicatorObject.SetDistanceText(distanceString);
            }
        }

        Vector2 CalculateIndicatorPosition(Vector3 targetObjectScreenPos, bool isBehindCamera, bool shouldFollowOnScreen, bool shouldShowArrow)
        {
            if (shouldFollowOnScreen)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(targetCanvasRect, targetObjectScreenPos, targetCanvas.worldCamera, 
                    out Vector2 targetCanvasPosition);
                return targetCanvasPosition;
            }

            // Update dynamic border offset based on current indicator size
            UpdateDynamicBorderOffset();

            // Handle off-screen positioning
            if (isBehindCamera)
            {
                if (rectBoundary != null)
                {
                    // Mirror across rect boundary center
                    targetObjectScreenPos.x = 2 * rectBoundaryCenter.x - targetObjectScreenPos.x;
                    targetObjectScreenPos.y = 2 * rectBoundaryCenter.y - targetObjectScreenPos.y;
                }
                else
                {
                    targetObjectScreenPos.x = screenWidth - targetObjectScreenPos.x;
                    targetObjectScreenPos.y = screenHeight - targetObjectScreenPos.y;
                }
            }

            Vector2 targetObjectDirection = ((Vector2)targetObjectScreenPos - screenCenter).normalized;
            Vector2 borderPosition = GetBorderPosition(screenCenter, targetObjectDirection);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetCanvasRect, borderPosition, targetCanvas.worldCamera,
                out Vector2 borderCanvasPos);

            // Update arrow rotation
            if (shouldShowArrow)
            {
                float angle = Mathf.Atan2(targetObjectDirection.y, targetObjectDirection.x) * Mathf.Rad2Deg;
                indicatorObject.SetArrowRotation(angle - 90f);
            }

            return borderCanvasPos;
        }

        Vector2 GetBorderPosition(Vector2 center, Vector2 direction)
        {
            float minX, maxX, minY, maxY;

            if (rectBoundary != null)
            {
                // Use rect boundary limits
                minX = rectBoundaryMin.x + dynamicBorderOffset;
                maxX = rectBoundaryMax.x - dynamicBorderOffset;
                minY = rectBoundaryMin.y + dynamicBorderOffset;
                maxY = rectBoundaryMax.y - dynamicBorderOffset;
            }
            else
            {
                // Use screen limits
                minX = dynamicBorderOffset;
                maxX = Screen.width - dynamicBorderOffset;
                minY = dynamicBorderOffset;
                maxY = Screen.height - dynamicBorderOffset;
            }

            // Early exit if direction is zero
            if (direction.sqrMagnitude < 0.0001f)
                return center;

            // Calculate intersections
            float t = float.MaxValue;

            if (direction.y != 0)
            {
                float tTop = (maxY - center.y) / direction.y;
                float tBottom = (minY - center.y) / direction.y;
                if (direction.y > 0 && tTop > 0) t = Mathf.Min(t, tTop);
                if (direction.y < 0 && tBottom > 0) t = Mathf.Min(t, tBottom);
            }

            if (direction.x != 0)
            {
                float tRight = (maxX - center.x) / direction.x;
                float tLeft = (minX - center.x) / direction.x;
                if (direction.x > 0 && tRight > 0) t = Mathf.Min(t, tRight);
                if (direction.x < 0 && tLeft > 0) t = Mathf.Min(t, tLeft);
            }

            Vector2 borderPos = t != float.MaxValue ? center + direction * t : center;

            // Clamp to bounds
            borderPos.x = Mathf.Clamp(borderPos.x, minX, maxX);
            borderPos.y = Mathf.Clamp(borderPos.y, minY, maxY);

            return borderPos;
        }

        string FormatDistance(float distance)
        {
            string distanceValue;
            string unit;

            switch (distanceUnit)
            {
                case DistanceUnit.Metric:
                    if (distance < 1000f) { distanceValue = distance.ToString("F0"); unit = "m"; }
                    else { distanceValue = (distance * MetersToKilometers).ToString("F1"); unit = "km"; }
                    break;

                case DistanceUnit.Imperial:
                    float feet = distance * MetersToFeet;
                    if (feet < 5280f) { distanceValue = feet.ToString("F0"); unit = "ft"; }
                    else { distanceValue = (feet * FeetToMiles).ToString("F1"); unit = "mi"; }
                    break;

                default:
                    return string.Empty;
            }

            return string.Format(distanceFormat, distanceValue, unit);
        }

        public void SetTarget(Transform newTarget)
        {
            targetTransform = newTarget;

            if (targetTransform == null)
                targetTransform = transform;

            if (newTarget != null && !enabled)
                enabled = true;
        }

        public void SetDistanceUnit(DistanceUnit unit) => distanceUnit = unit;

        public void SetRectBoundary(RectTransform boundary)
        {
            rectBoundary = boundary;

            if (rectBoundary != null)
                UpdateRectBoundaryValues();
        }

        public void Disable()
        {
            if (indicatorObject != null && indicatorObject.gameObject.activeInHierarchy)
                isDisabling = true;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;

        void OnValidate()
        {
            // Ensure fade distances make sense
            if (fadeEndDistance > fadeStartDistance) { fadeEndDistance = fadeStartDistance; }

            // Clamp border offset
            borderOffset = Mathf.Max(0, borderOffset);

            // Ensure fade duration is positive
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
        }
#endif
    }
}