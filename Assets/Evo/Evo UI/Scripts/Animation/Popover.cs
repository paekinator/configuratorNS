using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    /// <summary>
    /// Handles the logic and animations for showing/hiding a UI popover.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl)]
    [AddComponentMenu("Evo/UI/Animation/Popover")]
    public class Popover : MonoBehaviour
    {
        [Header("Settings")]
        public bool isOpen = false;
        public bool manageGameObjectState = true;
        public bool closeOnOutsideClick = true;
        public bool closeOnEscapeKey = true;

        [Header("Animation")]
        public AnimationType animationType = AnimationType.Scale;
        [EvoHideIf(nameof(animationType), AnimationType.None)]
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [EvoHideIf(nameof(animationType), AnimationType.None)]
        [Range(0.01f, 1f)] public float animationDuration = 0.2f;
        [EvoShowIf(nameof(animationType), AnimationType.Scale)]
        [Range(0f, 1f)] public float scaleFrom = 0.8f;
        [EvoShowIf(nameof(animationType), AnimationType.Slide)]
        public Vector2 slideOffset = new(0f, -20f);

        [Header("Events")]
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        // Enums
        public enum AnimationType
        {
            None = 0,
            Fade = 1,
            Scale = 2,
            Slide = 3
        }

        // Cache
        CanvasGroup canvasGroup;
        RectTransform rectTransform;
        PointerEventData pointerData;
        readonly List<RaycastResult> raycastResults = new();

        // Helpers
        bool isBlockingOpen = false;
        Vector2 initialAnchoredPosition;
        Coroutine animationCoroutine;
        Coroutine outsideClickCoroutine;

        void Awake()
        {
            EnsureComponents();
            SetStateImmediate(isOpen);
        }

        void OnDisable()
        {
            if (isOpen)
                SetStateImmediate(false);
        }

        void EnsureComponents()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                initialAnchoredPosition = rectTransform.anchoredPosition;
            }
        }

        bool IsPointerOverPopover()
        {
            pointerData ??= new PointerEventData(EventSystem.current);
            pointerData.position = Utilities.GetPointerPosition();

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (RaycastResult result in raycastResults)
            {
                // Check if the raycast hit this popover or any of its children
                if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform))
                    return true;
            }

            return false;
        }

        IEnumerator DetectOutsideClick()
        {
            // Yield one frame to ensure the click that opened the popover isn't registered as an outside click
            yield return null;

            while (isOpen)
            {
                if (closeOnEscapeKey && Utilities.WasEscapeKeyPressed())
                {
                    Close();
                    yield break;
                }

                if (closeOnOutsideClick && Utilities.WasPointerPressed())
                {
                    yield return null; // Wait one frame to let UI events process correctly

                    if (!IsPointerOverPopover())
                    {
                        StartCoroutine(BlockReopen());
                        Close();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        IEnumerator BlockReopen()
        {
            isBlockingOpen = true;

            while (!Utilities.WasPointerReleased())
                yield return null;

            yield return null; // Wait one more frame to let the UI event pass into the void

            isBlockingOpen = false;
        }

        IEnumerator AnimateOut()
        {
            IEnumerator anim = null;

            switch (animationType)
            {
                case AnimationType.Fade:
                    anim = Utilities.CrossFadeCanvasGroup(canvasGroup, 0f, animationDuration);
                    break;
                case AnimationType.Scale:
                    anim = Utilities.ScaleAndFade(rectTransform, canvasGroup, rectTransform.localScale,
                        Vector3.one * scaleFrom, canvasGroup.alpha, 0f, animationDuration, animationCurve);
                    break;
                case AnimationType.Slide:
                    Vector2 targetPos = initialAnchoredPosition + slideOffset;
                    anim = Utilities.SlideAndFade(rectTransform, canvasGroup, rectTransform.anchoredPosition,
                        targetPos, canvasGroup.alpha, 0f, animationDuration, animationCurve);
                    break;
                case AnimationType.None:
                    SetStateImmediate(false);
                    break;
            }

            if (anim != null)
                yield return StartCoroutine(anim);

            if (manageGameObjectState)
                gameObject.SetActive(false);
        }

        /// <summary>
        /// Toggles the popover visibility based on its current state.
        /// </summary>
        public void Toggle()
        {
            if (isOpen) { Close(); }
            else { Open(); }
        }

        /// <summary>
        /// Opens the popover with the configured animation.
        /// </summary>
        public void Open()
        {
            if (isOpen || isBlockingOpen)
                return;

            EnsureComponents();

            isOpen = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (manageGameObjectState)
                gameObject.SetActive(true);

            if (!gameObject.activeInHierarchy)
            {
                SetStateImmediate(true);
                onShow?.Invoke();
                return;
            }

            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            switch (animationType)
            {
                case AnimationType.Fade:
                    animationCoroutine = StartCoroutine(Utilities.CrossFadeCanvasGroup(canvasGroup, 1f, animationDuration));
                    break;
                case AnimationType.Scale:
                    animationCoroutine = StartCoroutine(Utilities.ScaleAndFade(rectTransform, canvasGroup,
                        Vector3.one * scaleFrom, Vector3.one, canvasGroup.alpha, 1f, animationDuration, animationCurve));
                    break;
                case AnimationType.Slide:
                    Vector2 startPos = initialAnchoredPosition + slideOffset;
                    animationCoroutine = StartCoroutine(Utilities.SlideAndFade(rectTransform, canvasGroup,
                        startPos, initialAnchoredPosition, canvasGroup.alpha, 1f, animationDuration, animationCurve));
                    break;
                case AnimationType.None:
                    SetStateImmediate(true);
                    break;
            }

            if (closeOnOutsideClick || closeOnEscapeKey)
            {
                if (outsideClickCoroutine != null) { StopCoroutine(outsideClickCoroutine); }
                outsideClickCoroutine = StartCoroutine(DetectOutsideClick());
            }

            onShow?.Invoke();
        }

        /// <summary>
        /// Closes the popover with the configured animation.
        /// </summary>
        public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            if (outsideClickCoroutine != null)
            {
                StopCoroutine(outsideClickCoroutine);
                outsideClickCoroutine = null;
            }

            if (!gameObject.activeInHierarchy)
            {
                SetStateImmediate(false);
                onHide?.Invoke();
                return;
            }

            animationCoroutine = StartCoroutine(AnimateOut());
            onHide?.Invoke();
        }

        /// <summary>
        /// Forces the popover to immediately snap open or closed without animations.
        /// </summary>
        public void SetStateImmediate(bool open)
        {
            EnsureComponents();

            isOpen = open;

            canvasGroup.alpha = open ? 1f : 0f;
            canvasGroup.blocksRaycasts = open;
            canvasGroup.interactable = open;

            if (manageGameObjectState)
                gameObject.SetActive(open);

            if (open)
            {
                rectTransform.localScale = Vector3.one;
                rectTransform.anchoredPosition = initialAnchoredPosition;
            }
        }
    }
}