using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CarouselPreset : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image backgroundImage;

        [Header("Optional")]
        public Button actionButton;

        public void SetContent(Carousel.Item item)
        {
            if (titleText != null) titleText.text = item.title;
            if (descriptionText != null) descriptionText.text = item.description;
            if (backgroundImage != null) backgroundImage.sprite = item.background;
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => item.onClick?.Invoke());
                actionButton.gameObject.SetActive(item.onClick.GetPersistentEventCount() > 0);
            }
        }

        public void SetPosition(Vector2 position)
        {
            rectTransform.anchoredPosition = position;
        }

        public void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
        }

        public void SetInteractable(bool interactable)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }
}