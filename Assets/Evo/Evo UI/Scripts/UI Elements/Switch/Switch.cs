using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/switch")]
    [AddComponentMenu("Evo/UI/UI Elements/Switch")]
    public class Switch : Interactive
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool isOn;
        public bool invokeAtStart;
        [Range(0f, 3f)] public float handleDuration = 0.2f;
        public AnimationCurve handleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private RectTransform switchHandle;
        [SerializeField] private CanvasGroup[] offCG;
        [SerializeField] private CanvasGroup[] onCG;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<bool> onValueChanged = new();
        public UnityEvent onSwitchOn = new();
        public UnityEvent onSwitchOff = new();

        // Cache
        readonly Dictionary<CanvasGroup, float> cachedStateTargets = new();

        // Helpers
        float targetHandlePosition;
        Vector2 initialHandlePosition;
        Coroutine currentStateAnimation;
        Coroutine currentSwitchAnimation;

        public bool IsOn
        {
            get => isOn;
            set => SetValue(value);
        }

        protected override void Awake()
        {
            base.Awake();

            if (switchHandle != null)
            {
                initialHandlePosition = switchHandle.anchoredPosition;
                targetHandlePosition = GetTargetPosition();
                switchHandle.anchoredPosition = new Vector2(targetHandlePosition, initialHandlePosition.y);
            }
        }

        protected override void Start()
        {
            base.Start();

            if (invokeAtStart)
            {
                onValueChanged?.Invoke(isOn);

                if (isOn) { onSwitchOn?.Invoke(); }
                else { onSwitchOff.Invoke(); }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            UpdateStates();
            UpdateState(false);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);

            if (IsInteractable())
                Toggle();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);

            if (IsInteractable())
                Toggle();
        }

        void UpdateState(bool animate)
        {
            targetHandlePosition = GetTargetPosition();

            if (animate && Application.isPlaying)
            {
                if (currentSwitchAnimation != null) { StopCoroutine(currentSwitchAnimation); }
                currentSwitchAnimation = StartCoroutine(AnimateHandle());
            }
            else if (switchHandle != null)
            {
                switchHandle.anchoredPosition = new Vector2(targetHandlePosition, switchHandle.anchoredPosition.y);
            }
        }

        void UpdateStates()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (currentStateAnimation != null) { StopCoroutine(currentStateAnimation); }
            currentStateAnimation = StartCoroutine(AnimateStates());
        }

        float GetTargetPosition()
        {
            if (switchHandle == null)
                return 0f;

            var parentRect = switchHandle.parent as RectTransform;

            if (parentRect == null)
                return 0f;

            float parentWidth = parentRect.rect.width;
            float handleWidth = switchHandle.rect.width;
            float handlePivotX = switchHandle.pivot.x;

            // Special case: full-width handles normally won't move (xMin and xMax math cancels out).
            // By capping the calculation width to half the parent's width, it perfectly slides
            // exactly 50% of the parent width while keeping the exact same edge-snapping workflow.
            float effectiveWidth = handleWidth >= parentWidth - 0.1f ? parentWidth * 0.5f : handleWidth;

            if (isOn)
            {
                // Align handle's right edge to the parent's right edge (xMax)
                return parentRect.rect.xMax - (effectiveWidth * (1f - handlePivotX));
            }
            else
            {
                // Align handle's left edge to the parent's left edge (xMin)
                return parentRect.rect.xMin + (effectiveWidth * handlePivotX);
            }
        }

        IEnumerator AnimateHandle()
        {
            if (switchHandle == null)
                yield break;

            Vector2 startPos = switchHandle.anchoredPosition;
            Vector2 targetPos = new(targetHandlePosition, startPos.y);

            float elapsed = 0f;

            while (elapsed < handleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / handleDuration;
                if (handleCurve != null && handleCurve.keys.Length > 0) { progress = handleCurve.Evaluate(progress); }

                // Smoothly interpolate position
                switchHandle.anchoredPosition = Vector2.Lerp(startPos, targetPos, progress);
                yield return null;
            }

            // Ensure final value is set exactly
            switchHandle.anchoredPosition = targetPos;
        }

        IEnumerator AnimateStates()
        {
            cachedStateTargets.Clear();

            if (offCG != null)
            {
                for (int i = 0; i < offCG.Length; i++)
                {
                    var cg = offCG[i];

                    if (cg != null)
                        cachedStateTargets[cg] = isOn ? 0 : 1;
                }
            }

            if (onCG != null)
            {
                for (int i = 0; i < onCG.Length; i++)
                {
                    var cg = onCG[i];

                    if (cg != null)
                        cachedStateTargets[cg] = isOn ? 1 : 0;
                }
            }

            yield return Utilities.CrossFadeCanvasGroup(cachedStateTargets, Mathf.Max(0f, transitionDuration));
        }

        public void SetValue(bool value) => SetValue(value, true);

        public void SetValue(bool value, bool sendCallback)
        {
            if (isOn == value)
                return;

            isOn = value;

            if (!gameObject.activeInHierarchy)
                return;

            UpdateState(true);
            UpdateStates();

            if (isOn)
                AudioManager.PlayClip(Styler.GetAudio(sfxSource, selectedSFX, stylerPreset));

            if (sendCallback)
            {
                onValueChanged?.Invoke(isOn);

                if (isOn) { onSwitchOn?.Invoke(); }
                else { onSwitchOff?.Invoke(); }
            }
        }

        public void Toggle() => SetValue(!isOn);

#if UNITY_EDITOR
        protected override void OnValidate() => UpdateStatesValidate();

        void UpdateStatesValidate()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
                UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
            }
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            // Set handle
            if (switchHandle != null)
            {
                targetHandlePosition = GetTargetPosition();
                switchHandle.anchoredPosition = new Vector2(targetHandlePosition, switchHandle.anchoredPosition.y);
            }

            // Set on/off states for all off canvas groups
            if (offCG != null)
            {
                for (int i = 0; i < offCG.Length; i++)
                {
                    if (offCG[i] != null)
                        offCG[i].alpha = !isOn ? 1 : 0;
                }
            }

            // Set on/off states for all on canvas groups
            if (onCG != null)
            {
                for (int i = 0; i < onCG.Length; i++)
                {
                    if (onCG[i] != null)
                        onCG[i].alpha = isOn ? 1 : 0;
                }
            }
        }
#endif
    }
}