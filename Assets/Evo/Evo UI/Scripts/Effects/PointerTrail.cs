using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    /// <summary>
    /// Creates an animated trail that follows the pointer, either constantly or only on hover.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/pointer-trail")]
    [AddComponentMenu("Evo/UI/Effects/Pointer Trail")]
    public class PointerTrail : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Customization")]
        [SerializeField] private Preset preset;

        [Header("Settings")]
        [SerializeField] private Visibility visibility = Visibility.Always;
        [SerializeField] private Transform trailParent;

        public enum Visibility
        {
            Always,
            HoverOnly
        }

        [System.Serializable]
        public class Preset
        {
            public Sprite shape;
            public Color color = new(1f, 1f, 1f, 0.8f);
            [Range(0.1f, 50f)] public float scale = 1f;
            public Vector2 offset = Vector2.zero;

            [Header("Animation")]
            [Range(0f, 2f)] public float fadeDuration = 0.1f;
        }

        // Cache
        PointerTrailHandler cachedHandler;

        void Start()
        {
            if (trailParent == null)
                trailParent = transform;

            if (visibility == Visibility.Always)
                cachedHandler = Create(preset, trailParent, false, false);
        }

        /// <summary>
        /// Creates or reuses a pointer trail on the specified parent.
        /// </summary>
        public static PointerTrailHandler Create(Preset preset, Transform parent, bool manageParent = false, bool shouldDestroy = true)
        {
            if (parent == null)
                return null;

            if (manageParent)
                parent.gameObject.SetActive(true);

            // Check if a trail already exists to prevent duplicates
            var existingHandler = parent.GetComponentInChildren<PointerTrailHandler>(true);
            if (existingHandler != null)
            {
                existingHandler.Create(preset, manageParent, shouldDestroy);
                existingHandler.Show();
                return existingHandler;
            }

            GameObject trailObj = new("Trail Effect");
            trailObj.transform.SetParent(parent, false);

            PointerTrailHandler handler = trailObj.AddComponent<PointerTrailHandler>();
            handler.Create(preset, manageParent, shouldDestroy);
            return handler;
        }

        public static void Hide(Transform parent)
        {
            if (parent == null)
                return;

            var handler = parent.GetComponentInChildren<PointerTrailHandler>();
            if (handler != null) { handler.Hide(); }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (visibility != Visibility.HoverOnly)
                return;

            // Reuse the cached handler instead of instantiating a new GameObject every hover
            if (cachedHandler == null) { cachedHandler = Create(preset, trailParent, false, false); }
            else { cachedHandler.Show(); }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (visibility != Visibility.HoverOnly)
                return;

            if (cachedHandler != null) { cachedHandler.Hide(); }
            else { Hide(trailParent); }
        }
    }

    public class PointerTrailHandler : MonoBehaviour
    {
        Image trailImage;
        RectTransform parentRect;
        Canvas targetCanvas;
        Camera worldCamera;
        Color originalColor;
        Coroutine fadeCoroutine;
        PointerTrail.Preset preset;
        bool manageParent;
        bool destroyOnDisable;

        void OnDisable()
        {
            if (manageParent && transform.parent != null && transform.parent.childCount <= 1)
                transform.parent.gameObject.SetActive(false);

            if (destroyOnDisable)
                Destroy(gameObject);
        }

        void Update()
        {
            if (preset != null && trailImage != null && trailImage.color.a > 0.01f)
                UpdatePosition();
        }

        public void Create(PointerTrail.Preset source, bool setParent = false, bool shouldDestroy = true)
        {
            preset = source;
            manageParent = setParent;
            destroyOnDisable = shouldDestroy;

            Initialize();
            Show();
        }

        void Initialize()
        {
            // Find canvas and parent rect for coordinate conversion
            parentRect = transform.parent as RectTransform;
            targetCanvas = GetComponentInParent<Canvas>();

            if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                worldCamera = targetCanvas.worldCamera;

            // Create and setup image component safely to avoid adding duplicates
            if (trailImage == null)
            {
                trailImage = gameObject.AddComponent<Image>();
                trailImage.raycastTarget = false;
            }

            trailImage.sprite = preset.shape;

            // Apply scale
            transform.localScale = Vector3.one * preset.scale;

            // Cache original color
            originalColor = preset.color;

            // Start with invisible state but keep GameObject active for coroutines
            var color = originalColor;
            color.a = 0f;
            trailImage.color = color;
        }

        void UpdatePosition()
        {
            Vector2 screenPosition = Utilities.GetPointerPosition();

            if (parentRect != null && targetCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    worldCamera,
                    out Vector2 localPos
                );
                transform.localPosition = localPos + preset.offset;
            }
            else
            {
                // Fallback for non-RectTransform parents
                transform.position = screenPosition + preset.offset;
            }
        }

        public void Show()
        {
            if (trailImage == null)
                return;

            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);

            StartTransition(preset.color.a);
        }

        public void Hide()
        {
            if (!gameObject.activeInHierarchy || trailImage == null)
                return;

            StartTransition(0);
        }

        public void SetAlpha(float alpha)
        {
            if (trailImage == null)
                return;

            StopTransition();

            var color = originalColor;
            color.a = Mathf.Clamp01(alpha);
            trailImage.color = color;

            if (alpha <= 0.01f)
                gameObject.SetActive(false);
        }

        void StartTransition(float targetAlpha)
        {
            StopTransition();
            fadeCoroutine = StartCoroutine(Utilities.CrossFadeAlpha(trailImage, targetAlpha, preset.fadeDuration, destroyOnCull: destroyOnDisable));

            // If we aren't destroying, safely hide it when faded out
            if (!destroyOnDisable && targetAlpha <= 0.01f)
                SetAlpha(0);
        }

        void StopTransition()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }
    }
}