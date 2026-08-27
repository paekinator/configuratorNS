using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/button")]
    [AddComponentMenu("Evo/UI/UI Elements/Button")]
    public class Button : Interactive
    {
        [EvoHeader("Icon", Constants.CustomEditorID)]
        public bool enableIcon = true;
        public Sprite icon;
        [Range(1f, 200f)] public float iconSize = 30f;

        [EvoHeader("Text", Constants.CustomEditorID)]
        public bool enableText = true;
        public string text = "Button";
        [Range(1f, 200f)] public float textSize = 24f;

        [EvoHeader("Layout", Constants.CustomEditorID)]
        [Tooltip("Sets the button size based on its content." +
            "Should be disabled if parent has a layout group component.")]
        public bool dynamicScale = true;
        public bool reverseArrangement = false;
        [Range(0, 100)] public int spacing = 12;
        public RectOffset padding = new();

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [Tooltip("Allows manual editing of button content, including icon and text.")]
        public bool customContent = false;
        public bool allowDoubleClick = false;
        [SerializeField, Range(0.1f, 1)] private float doubleClickDuration = 0.25f;

        [EvoHeader("References", Constants.CustomEditorID)]
        [Tooltip("Image object which the button icon will be applied to.")]
        public Image imageObject;
        [Tooltip("Handles layout size for the icon.")]
        [SerializeField] private LayoutElement iconElement;
        [Tooltip("Text object which the button text will be applied to.")]
        public TMP_Text textObject;
        [Tooltip("Parent container for text. Disabled when the button text is empty.")]
        [SerializeField] private RectTransform textContainer;
        [Tooltip("Handles automatic/dynamic layout scaling.")]
        [SerializeField] private ContentSizeFitter contentFitter;
        [Tooltip("Manages icon/text layout adjustments.")]
        [SerializeField] private HorizontalLayoutGroup contentLayout;

        // Cache
        Coroutine doubleClickCoroutine;

        protected override void Awake()
        {
            base.Awake();
            UpdateUI();
        }

#if EVO_LOCALIZATION
        protected override void Start()
        {
            base.Start();

            if (Application.isPlaying && enableLocalization && localizedObject == null && !customContent)
                localizedObject = Localization.LocalizedObject.Check(gameObject, textObject);
        }
#endif

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);

            if (IsInteractable() && allowDoubleClick)
            {
                if (waitingForDoubleClickInput)
                {
                    onDoubleClick?.Invoke();
                    waitingForDoubleClickInput = false;

                    if (doubleClickCoroutine != null)
                    {
                        StopCoroutine(doubleClickCoroutine);
                        doubleClickCoroutine = null;
                    }
                }
                else
                {
                    waitingForDoubleClickInput = true;

                    if (doubleClickCoroutine != null) { StopCoroutine(doubleClickCoroutine); }
                    doubleClickCoroutine = StartCoroutine(DoubleClickTimer());
                }
            }
        }

        IEnumerator DoubleClickTimer()
        {
            float elapsed = 0f;
            while (elapsed < doubleClickDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            waitingForDoubleClickInput = false;
            doubleClickCoroutine = null;
        }

        /// <summary>
        /// Force-updates all button content, such as icon and text.
        /// </summary>
        public void UpdateUI()
        {
            SetIcon(icon);
            SetText(text);
            UpdateLayout();
        }

        /// <summary>
        /// Force-updates the button layout.
        /// </summary>
        public void UpdateLayout()
        {
            if (contentFitter != null)
                contentFitter.enabled = dynamicScale;

            if (contentLayout != null)
            {
                contentLayout.childForceExpandHeight = dynamicScale;
                contentLayout.childForceExpandWidth = dynamicScale;
                contentLayout.padding = padding;
                contentLayout.spacing = spacing;
                contentLayout.reverseArrangement = reverseArrangement;
            }
        }

        /// <summary>
        /// Sets the given sprite as the button icon.
        /// </summary>
        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;

            if (customContent || imageObject == null)
                return;

            imageObject.gameObject.SetActive(enableIcon && newIcon);

            if (enableIcon)
            {
                imageObject.sprite = icon;

                if (iconElement != null)
                {
                    iconElement.preferredWidth = iconSize;
                    iconElement.preferredHeight = iconSize;
                }
            }
        }

        /// <summary>
        /// Sets the given string as the button text.
        /// </summary>
        public void SetText(string newText)
        {
            text = newText;

            if (customContent || textObject == null)
                return;

            bool bypassText = false;
#if EVO_LOCALIZATION
            bypassText = enableLocalization && localizedObject != null && !string.IsNullOrEmpty(localizedObject.tableKey);
#endif

            textObject.gameObject.SetActive(enableText);

            if (textContainer != null)
                textContainer.gameObject.SetActive(enableText);

            if (enableText)
            {
                if (!bypassText) { textObject.text = text; }
                textObject.fontSize = textSize;
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;

        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateUI();
        }
#endif
    }
}