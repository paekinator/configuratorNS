using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Applies a subtle parallax depth effect to a UI element based on the pointer's location.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/rect-depth")]
    [AddComponentMenu("Evo/UI/Effects/Rect Depth")]
    [RequireComponent(typeof(RectTransform))]
    public class RectDepth : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Range(0.01f, 0.5f)]
        [Tooltip("Lower values = smoother/slower following. Higher values = faster response.")]
        private float followSpeed = 0.1f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Distance multiplier for the parallax effect.")]
        private float depthMultiplier = 0.1f;

        [Header("References")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera targetCamera;

        // Cache
        Vector2 mousePos;
        Vector2 currentVelocity;
        Vector2 targetPosition;
        Camera effectiveCamera;
        RectTransform canvasRect;

        void Awake()
        {
            if (targetRect == null) { targetRect = GetComponent<RectTransform>(); }
            if (targetCanvas == null) { targetCanvas = GetComponentInParent<Canvas>(); }

            UpdateEffectiveCamera();
            targetPosition = targetRect.anchoredPosition;
        }

        void OnEnable() => currentVelocity = Vector2.zero;

        void Update()
        {
            if (targetCanvas == null || targetRect == null)
                return;

            // Convert screen point to local point in canvas space
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Utilities.GetPointerPosition(),
                effectiveCamera,
                out mousePos))
            {
                // Calculate target position with depth multiplier
                targetPosition = mousePos * depthMultiplier;

                // Use SmoothDamp for smooth, velocity-based interpolation with proper deltaTime
                targetRect.anchoredPosition = Vector2.SmoothDamp(
                    targetRect.anchoredPosition,
                    targetPosition,
                    ref currentVelocity,
                    followSpeed
                );
            }
        }

        void UpdateEffectiveCamera()
        {
            if (targetCanvas == null)
                return;

            canvasRect = targetCanvas.transform as RectTransform;

            if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay) { effectiveCamera = null; }
            else if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera || targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                effectiveCamera = targetCanvas.worldCamera;
                if (effectiveCamera == null) { effectiveCamera = targetCamera != null ? targetCamera : Camera.main; }
            }
        }
    }
}