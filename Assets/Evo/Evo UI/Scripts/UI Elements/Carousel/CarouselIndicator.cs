using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    public class CarouselIndicator : MonoBehaviour
    {
        [Header("References")]
        public Button button;
        [SerializeField] private Image fillImage;

        [Header("Idle References")]
        [SerializeField] private RectTransform idleTransform;
        [SerializeField] private CanvasGroup idleCanvasGroup;

        [Header("Timer References")]
        [SerializeField] private RectTransform timerTransform;
        [SerializeField] private CanvasGroup timerCanvasGroup;

        public void SetProgress(float progress)
        {
            fillImage.fillAmount = progress;
        }

        public void SetTimerAlpha(float alpha)
        {
            timerCanvasGroup.alpha = alpha;
        }

        public void SetIdleAlpha(float alpha)
        {
            idleCanvasGroup.alpha = alpha;
        }

        public void SetTimerScale(float scale)
        {
            timerTransform.localScale = Vector3.one * scale;
        }

        public void SetIdleScale(float scale)
        {
            idleTransform.localScale = Vector3.one * scale;
        }

        public void SetTimerActive(bool active)
        {
            timerTransform.gameObject.SetActive(active);
        }

        public void SetIdleActive(bool active)
        {
            idleTransform.gameObject.SetActive(active);
        }
    }
}