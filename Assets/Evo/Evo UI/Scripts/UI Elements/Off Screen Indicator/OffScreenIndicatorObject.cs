using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    public class OffScreenIndicatorObject : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform indicatorUI;
        [SerializeField] private RectTransform arrowObject;
        [SerializeField] private Image iconObject;
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Runtime")]
        public Transform targetTransform;

        // Cache
        Coroutine fadeCoroutine;
        float visibilityAlpha = 1f;
        float lastArrowRotation;
        string lastDistanceText;
        bool lastArrowVisible = true;
        bool lastDistanceVisible = true;

        void OnEnable()
        {
            // Only reset alpha if it was previously faded out
            if (canvasGroup != null && canvasGroup.alpha <= 0.001f)
            {
                canvasGroup.alpha = 1f;
            }
        }

        void OnDisable()
        {
            // Clean up coroutine reference
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }

        void OnDestroy()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }

        public void Initialize()
        {
            // Get indicator rect if not assigned
            if (indicatorUI == null) { indicatorUI = GetComponent<RectTransform>(); }

            // Setup canvas group for fading
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) { canvasGroup = gameObject.AddComponent<CanvasGroup>(); }

            // Cache initial states
            if (arrowObject != null)
            {
                lastArrowVisible = arrowObject.gameObject.activeSelf;
                lastArrowRotation = arrowObject.rotation.eulerAngles.z;
            }

            if (distanceText != null)
            {
                lastDistanceVisible = distanceText.gameObject.activeSelf;
                lastDistanceText = distanceText.text;
            }
        }

        public void SetPosition(Vector2 position)
        {
            indicatorUI.anchoredPosition = position;
        }

        public void SetSize(float size)
        {
            Vector3 newScale = Vector3.one * size;
            if (indicatorUI.localScale != newScale) { indicatorUI.localScale = newScale; }
        }

        public void SetIcon(Sprite icon)
        {
            iconObject.sprite = icon;
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                alpha = Mathf.Clamp01(alpha);
                if (Mathf.Abs(canvasGroup.alpha - alpha) > 0.001f) { canvasGroup.alpha = alpha; }
            }
        }

        public void SetVisibilityAlpha(float alpha)
        {
            visibilityAlpha = Mathf.Clamp01(alpha);
        }

        public float GetVisibilityAlpha()
        {
            return visibilityAlpha;
        }

        public void SetArrowVisible(bool visible)
        {
            if (arrowObject != null && lastArrowVisible != visible)
            {
                arrowObject.gameObject.SetActive(visible);
                lastArrowVisible = visible;
            }
        }

        public void SetArrowRotation(float angle)
        {
            if (arrowObject != null)
            {
                // Normalize angle to 0-360 range
                angle = angle % 360f;
                if (angle < 0) { angle += 360f; }

                // Only update if rotation changed significantly
                if (Mathf.Abs(lastArrowRotation - angle) > 0.1f)
                {
                    arrowObject.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    lastArrowRotation = angle;
                }
            }
        }

        public void SetDistanceVisible(bool visible)
        {
            if (distanceText != null && lastDistanceVisible != visible)
            {
                distanceText.gameObject.SetActive(visible);
                lastDistanceVisible = visible;
            }
        }

        public void SetDistanceText(string text)
        {
            if (distanceText != null && !string.IsNullOrEmpty(text) && lastDistanceText != text)
            {
                distanceText.text = text;
                lastDistanceText = text;
            }
        }

        public void FadeIn(float duration = 0.5f)
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
            }

            StartFade(1f, duration);
        }

        public void FadeOut(float duration = 0.5f)
        {
            StartFade(0f, duration);
        }

        void StartFade(float targetAlpha, float duration)
        {
            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); }
            if (gameObject.activeInHierarchy && canvasGroup != null) { fadeCoroutine = StartCoroutine(FadeCoroutine(targetAlpha, duration)); }
        }

        IEnumerator FadeCoroutine(float targetAlpha, float duration)
        {
            if (canvasGroup == null || duration <= 0f)
            {
                if (canvasGroup != null) { canvasGroup.alpha = targetAlpha; }
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            // Early exit if already at target
            if (Mathf.Abs(startAlpha - targetAlpha) < 0.001f) { yield break; }
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            if (targetAlpha <= 0f) { gameObject.SetActive(false); }
            fadeCoroutine = null;
        }
    }
}