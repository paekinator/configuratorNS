using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    /// <summary>
    /// Applies a smooth, continuous swaying effect to a UI element based on cursor positioning.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/rect-sway")]
    [AddComponentMenu("Evo/UI/Effects/Rect Sway")]
    [RequireComponent(typeof(RectTransform))]
    public class RectSway : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform swayObject;

        [Header("Settings")]
        [SerializeField] private bool useUnscaledTime = true;
        public bool enableXAxis = true;
        public bool enableYAxis = true;
        [Range(1f, 30f)] public float smoothness = 10f;
        [Range(0.1f, 5f)] public float swayIntensity = 1f;

        // Helpers
        bool isInitialized;
        bool allowSway;
        Vector3 targetPos;
        Vector3 defaultPos;
        Vector3 currentVelocity;

        // Cache
        RectTransform rectTransform;
        Canvas cachedCanvas;

        void Awake() => InitializeComponents();

        void Start()
        {
            if (!isInitialized)
                Initialize();
        }

        void OnEnable()
        {
            if (isInitialized)
                ResetSway();
        }

        void Update()
        {
            if (!isInitialized)
                return;

            UpdateInput();
            UpdateSwayPosition();
        }

        public void OnPointerEnter(PointerEventData eventData) => SetSwayState(true);

        public void OnPointerExit(PointerEventData eventData) => SetSwayState(false);

        void InitializeComponents()
        {
            // Cache rect transform
            rectTransform = GetComponent<RectTransform>();

            // Auto-assign components if not set
            if (swayObject == null)
                swayObject = rectTransform;

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (canvas != null) { cachedCanvas = canvas; }
            else
            {
                canvas = GetComponentInParent<Canvas>();
                cachedCanvas = canvas;
            }
        }

        void Initialize()
        {
            if (swayObject == null)
            {
                enabled = false;
                return;
            }

            defaultPos = swayObject.anchoredPosition;
            targetPos = swayObject.position;
            isInitialized = true;
        }

        void UpdateInput()
        {
            if (!allowSway)
            {
                targetPos = transform.TransformPoint(defaultPos);
                return;
            }

            Vector2 inputPos = Utilities.GetPointerPosition();
            if (inputPos != Vector2.zero) { ProcessSwayTarget(inputPos); }
        }

        void ProcessSwayTarget(Vector3 inputPos)
        {
            Vector3 worldPos = GetWorldPosition(inputPos);
            Vector3 offset = worldPos - swayObject.position;

            // Apply intensity
            offset *= swayIntensity;

            // Apply constraints
            if (!enableXAxis) { offset.x = 0; }
            if (!enableYAxis) { offset.y = 0; }

            targetPos = swayObject.position + offset;
        }

        void UpdateSwayPosition()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (allowSway)
            {
                swayObject.position = Vector3.SmoothDamp(swayObject.position, targetPos, ref currentVelocity, 
                    1f / smoothness, Mathf.Infinity, deltaTime);
            }
            else
            {
                // Return to default position
                Vector3 defaultWorldPos = transform.TransformPoint(defaultPos);
                swayObject.position = Vector3.SmoothDamp(swayObject.position, defaultWorldPos, ref currentVelocity, 
                    1f / smoothness, Mathf.Infinity, deltaTime);
            }
        }

        Vector3 GetWorldPosition(Vector3 screenPos)
        {
            switch (cachedCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    return screenPos;

                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    if (targetCamera != null)
                    {
                        screenPos.z = cachedCanvas.planeDistance;
                        return targetCamera.ScreenToWorldPoint(screenPos);
                    }
                    return screenPos;

                default:
                    return screenPos;
            }
        }

        public void SetSwayState(bool enabled) => allowSway = enabled;

        public void ResetSway()
        {
            swayObject.anchoredPosition = defaultPos;
            targetPos = swayObject.position;
            currentVelocity = Vector3.zero;
        }
    }
}