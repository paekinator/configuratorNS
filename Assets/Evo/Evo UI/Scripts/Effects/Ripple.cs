using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Creates Material Design style ripples on interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/ripple")]
    [AddComponentMenu("Evo/UI/Effects/Ripple")]
    public class Ripple : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [Header("Customization")]
        [SerializeField] private Preset preset;

        [Header("Settings")]
        [SerializeField] private bool createOnClick;
        [SerializeField] private bool createOnSubmit;
        [SerializeField] private Transform rippleParent;

        [System.Serializable]
        public class Preset
        {
            public Sprite shape;
            public Color color = new(1f, 1f, 1f, 0.25f);
            public bool centered = false;

            [Header("Animation")]
            [Range(0.05f, 5f)] public float duration = 0.5f;
            [Range(0.1f, 50f)] public float size = 6f;
        }

        // Global Object Pool
        static readonly Stack<RippleHandler> ripplePool = new();

        void Awake()
        {
            if (rippleParent == null)
                rippleParent = transform;
        }

        /// <summary>
        /// Retrieves a pooled ripple handler or creates a new one if the pool is empty.
        /// </summary>
        private static RippleHandler GetFromPool(Transform parent)
        {
            while (ripplePool.Count > 0)
            {
                RippleHandler handler = ripplePool.Pop();
                if (handler != null && handler.gameObject != null)
                {
                    handler.transform.SetParent(parent, false);
                    return handler;
                }
            }

            GameObject rippleObj = new("Ripple Effect");
            rippleObj.transform.SetParent(parent, false);
            return rippleObj.AddComponent<RippleHandler>();
        }

        /// <summary>
        /// Returns a ripple back to the global pool.
        /// </summary>
        public static void ReturnToPool(RippleHandler handler)
        {
            if (handler != null && handler.gameObject != null)
                ripplePool.Push(handler);
        }

        public static void Create(Preset preset, Transform parent, bool manageParent = false, bool forceCentered = false)
        {
            if (parent == null)
                return;

            RippleHandler handler = GetFromPool(parent);
            handler.Create(preset, manageParent, forceCentered);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!createOnClick)
                return;

            Create(preset, rippleParent);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!createOnSubmit)
                return;

            Create(preset, rippleParent);
        }
    }

    public class RippleHandler : MonoBehaviour
    {
        Image rippleImg;
        bool isPooled = false;

        void Awake()
        {
            rippleImg = gameObject.AddComponent<Image>();
            rippleImg.raycastTarget = false;
        }

        void OnEnable() => isPooled = false;

        void OnDisable()
        {
            StopAllCoroutines();

            // Safety reset before returning to pool
            if (rippleImg != null)
            {
                Color c = rippleImg.color;
                c.a = 0f;
                rippleImg.color = c;
            }

            if (!isPooled)
            {
                isPooled = true;
                Ripple.ReturnToPool(this);
            }
        }

        public void Create(Ripple.Preset preset, bool manageParent, bool forceCentered)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (manageParent)
                transform.parent.gameObject.SetActive(true);

            StartCoroutine(CreateHelper(preset, manageParent, forceCentered));
        }

        IEnumerator CreateHelper(Ripple.Preset preset, bool manageParent, bool forceCentered)
        {
            rippleImg.sprite = preset.shape;

            // Start with zero alpha
            Color currentColor = preset.color;
            currentColor.a = 0f;
            rippleImg.color = currentColor;

            // Position the ripple
            if (forceCentered || preset.centered) { gameObject.transform.localPosition = Vector2.zero; }
            else
            {
                // Get pointer position and convert to local coordinates
                Vector2 screenPosition = Utilities.GetPointerPosition();
                RectTransform parentRect = transform.parent as RectTransform;

                if (parentRect != null)
                {
                    Canvas canvas = parentRect.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            parentRect,
                            screenPosition,
                            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                            out Vector2 localPos
                        );
                        gameObject.transform.localPosition = localPos;
                    }
                }
            }

            // Store values
            Vector3 startScale = Vector3.zero;
            Vector3 targetScale = new(preset.size, preset.size, preset.size);
            float targetAlpha = preset.color.a;
            float duration = preset.duration;
            float fadeInTime = Mathf.Min(0.1f, duration * 0.2f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f);

                // Scale up
                gameObject.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                // Alpha: fade in quickly, then fade out
                float alpha;
                if (elapsed < fadeInTime)
                {
                    // Quick fade in
                    float fadeT = elapsed / fadeInTime;
                    alpha = Mathf.Lerp(0f, targetAlpha, fadeT);
                }
                else
                {
                    // Fade out over remaining time
                    float fadeOutT = (elapsed - fadeInTime) / (duration - fadeInTime);
                    alpha = Mathf.Lerp(targetAlpha, 0f, fadeOutT);
                }

                currentColor.a = alpha;
                rippleImg.color = currentColor;

                yield return null;
            }

            // Final values
            gameObject.transform.localScale = targetScale;
            currentColor.a = 0f;
            rippleImg.color = currentColor;

            // Disabling the object automatically fires OnDisable, which pools it allocation-free
            gameObject.SetActive(false);

            if (manageParent && transform.parent != null && transform.parent.childCount <= 1)
                transform.parent.gameObject.SetActive(false);
        }
    }
}