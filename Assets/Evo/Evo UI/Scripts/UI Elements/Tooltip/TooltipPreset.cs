using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "ui-elements/tooltip")]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class TooltipPreset : MonoBehaviour
    {
        [Header("Required References")]
        public RectTransform tooltipRect;
        public CanvasGroup canvasGroup;

        [Header("Content References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private ContentSizeFitter contentSizeFitter;
        [SerializeField] private GameObject contentContainer;
        [SerializeField] private LayoutGroup layoutGroup;
        public LayoutElement layoutElement;

        // Helpers
        bool isInitialized;

        void Awake() => CheckForReferences();

        public void Setup(string title, string description, Sprite icon, float maxWidth, bool isCustom = false)
        {
            CheckForReferences();

            if (isCustom)
                return;

            // Check if header should be active, and re-enable it if it was previously disabled
            bool hasHeader = !string.IsNullOrEmpty(title) || icon != null;
            if (titleText != null && titleText.transform.parent != null)
            {
                GameObject headerObj = titleText.transform.parent.gameObject;
                if (headerObj.activeSelf != hasHeader) { headerObj.SetActive(hasHeader); }
            }

            if (hasHeader)
            {
                SetIcon(icon);
                SetTitle(title);
            }

            SetDescription(description);
            ForceLayoutUpdate(maxWidth);
        }

        void CheckForReferences()
        {
            if (isInitialized)
                return;

            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (tooltipRect == null) { tooltipRect = GetComponent<RectTransform>(); }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            isInitialized = true;
        }

        void SetTitle(string title)
        {
            if (titleText == null)
                return;

            // Prevent TMP mesh rebuilds if text hasn't changed
            if (titleText.text != title) { titleText.text = title; }

            // Check if state is changed
            bool hasTitle = !string.IsNullOrEmpty(title);
            if (titleText.gameObject.activeSelf != hasTitle) { titleText.gameObject.SetActive(hasTitle); }
        }

        void SetDescription(string description)
        {
            if (descriptionText == null)
                return;

            // Prevent TMP mesh rebuilds if text hasn't changed
            if (descriptionText.text != description) { descriptionText.text = description; }

            // Check if state is changed
            bool hasDesc = !string.IsNullOrEmpty(description);
            if (descriptionText.gameObject.activeSelf != hasDesc) { descriptionText.gameObject.SetActive(hasDesc); }
        }

        void SetIcon(Sprite icon)
        {
            if (iconImage == null)
                return;

            // Prevent material dirtying if sprite hasn't changed
            if (iconImage.sprite != icon) { iconImage.sprite = icon; }

            // Check if state is changed
            bool hasIcon = icon != null;
            if (iconImage.gameObject.activeSelf != hasIcon) { iconImage.gameObject.SetActive(hasIcon); }
        }

        void ForceLayoutUpdate(float maxWidth)
        {
            if (contentSizeFitter != null)
            {
                contentSizeFitter.SetLayoutHorizontal();
                contentSizeFitter.SetLayoutVertical();
            }

            if (layoutGroup != null)
            {
                layoutGroup.SetLayoutHorizontal();
                layoutGroup.SetLayoutVertical();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

            if (layoutElement != null)
            {
                bool exceedsWidth = tooltipRect.sizeDelta.x > maxWidth;
                bool layoutChanged = false;

                if (layoutElement.enabled != exceedsWidth)
                {
                    layoutElement.enabled = exceedsWidth;
                    layoutChanged = true;
                }

                if (layoutElement.preferredWidth != maxWidth)
                {
                    layoutElement.preferredWidth = maxWidth;
                    layoutChanged = true;
                }

                // If dynamically constrained the width, must rebuild the layout one more time.
                // Otherwise, the tooltip background won't stretch vertically to fit the newly wrapped text.
                if (layoutChanged && exceedsWidth) { LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect); }
            }
        }
    }
}