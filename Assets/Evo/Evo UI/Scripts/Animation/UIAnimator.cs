using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "animation/ui-animator")]
    [AddComponentMenu("Evo/UI/Animation/UI Animator")]
    public class UIAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        // Content
        public List<AnimationGroup> animationGroups = new();

        // References
        public RectTransform rectTransform;
        public CanvasGroup canvasGroup;
        public Image image;
        public TMP_Text text;

        // Settings
        [SerializeField] private bool useUnscaledTime = false;

        // Global Events
        public UnityEvent onAnimationStart = new();
        public UnityEvent onAnimationEnd = new();

        // Cache
        readonly Dictionary<AnimationType, Coroutine> activeAnimations = new();
        readonly Dictionary<AnimationType, int> animationIds = new();
        readonly Dictionary<int, int> groupActiveAnimations = new(); // Track active animations per group
        Vector3 cachedShakeOffset = Vector3.zero;
        int nextAnimationID = 0;

        // Original values
        Vector3 originalScale;
        Vector3 originalPosition;
        Color originalColor;
        float originalAlpha;
        float originalRotation;
        bool originalValuesStored;

        [System.Serializable]
        public enum AnimationType
        {
            Fade = 0,
            Scale = 1,
            Slide = 2,
            Rotate = 3,
            PunchScale = 4,
            Shake = 5,
            Bounce = 6,
            ColorTint = 7
        }

        [System.Serializable]
        public enum EaseType
        {
            Linear, EaseInQuad, EaseOutQuad, EaseInOutQuad, EaseInCubic, EaseOutCubic, EaseInOutCubic,
            EaseInQuart, EaseOutQuart, EaseInOutQuart, EaseInQuint, EaseOutQuint, EaseInOutQuint,
            EaseInSine, EaseOutSine, EaseInOutSine, EaseInExpo, EaseOutExpo, EaseInOutExpo,
            EaseInCirc, EaseOutCirc, EaseInOutCirc, EaseInBack, EaseOutBack, EaseInOutBack,
            EaseInElastic, EaseOutElastic, EaseInOutElastic, EaseInBounce, EaseOutBounce, EaseInOutBounce
        }

        [System.Serializable]
        public enum TriggerType
        {
            OnEnable = 0,
            OnClick = 1,
            OnPointerEnter = 2,
            OnPointerLeave = 3,
            OnPointerDown = 4,
            OnPointerUp = 5,
            Manual = 6
        }

        [System.Serializable]
        public class AnimationData
        {
            public AnimationType type = AnimationType.Fade;
            public bool enabled = true;
            public float duration = 0.3f;
            public float delay = 0f;
            public EaseType easeType = EaseType.EaseOutCubic;
            public bool loop = false;
            public bool yoyo = false;
            public bool animateFromCurrentValue = true;
#if UNITY_EDITOR
            [HideInInspector] public bool isExpanded = true;
#endif
            // Animation-specific values
            [Range(0f, 1f)] public float fadeFrom = 0f, fadeTo = 1f;
            public Vector3 scaleFrom = Vector3.zero, scaleTo = Vector3.one;
            public Vector3 slideFrom = Vector3.zero, slideTo = Vector3.zero;
            public float rotateFrom = 0f, rotateTo = 360f;
            public float punchIntensity = 0.2f;
            public float shakeIntensity = 10f;
            public float bounceHeight = 30f;
            public Color colorFrom = Color.white, colorTo = Color.white;
        }

        [System.Serializable]
        public class AnimationGroup
        {
            public string label;
            public TriggerType trigger = TriggerType.OnEnable;
            public List<AnimationData> animations = new();

            // Local Events
            public UnityEvent onGroupStart = new();
            public UnityEvent onGroupEnd = new();

#if UNITY_EDITOR
            [HideInInspector] public bool isExpanded = true;
            [HideInInspector] public bool eventsExpanded = false;
#endif
        }

        void Awake()
        {
            if (rectTransform == null) { rectTransform = GetComponent<RectTransform>(); }
            if (canvasGroup == null && !TryGetComponent(out canvasGroup)) { canvasGroup = gameObject.AddComponent<CanvasGroup>(); }
            if (image == null) { TryGetComponent(out image); }
            if (text == null) { TryGetComponent(out text); }

            StoreOriginalValues();
        }

        void OnEnable()
        {
            if (!originalValuesStored) { StoreOriginalValues(); }
            ExecuteAnimations(TriggerType.OnEnable);
        }

        void OnDisable()
        {
            if (activeAnimations.Count > 0)
            {
                ResetToOriginalValues();
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => ExecuteAnimations(TriggerType.OnPointerEnter);
        public void OnPointerExit(PointerEventData eventData) => ExecuteAnimations(TriggerType.OnPointerLeave);
        public void OnPointerDown(PointerEventData eventData) => ExecuteAnimations(TriggerType.OnPointerDown);
        public void OnPointerUp(PointerEventData eventData) => ExecuteAnimations(TriggerType.OnPointerUp);
        public void OnPointerClick(PointerEventData eventData) => ExecuteAnimations(TriggerType.OnClick);

        void StoreOriginalValues()
        {
            if (rectTransform != null)
            {
                originalScale = rectTransform.localScale;
                originalPosition = rectTransform.anchoredPosition;
                originalRotation = rectTransform.eulerAngles.z;
            }

            if (canvasGroup != null) { originalAlpha = canvasGroup.alpha; }
            else { originalAlpha = 1f; }

            if (image != null) { originalColor = image.color; }
            else if (text != null) { originalColor = text.color; }
            else { originalColor = Color.white; }

            originalValuesStored = true;
        }

        void PlayAnimation(AnimationData animData, int groupIndex)
        {
            // Generate unique ID for this animation
            int animationID = ++nextAnimationID;

            // Track group animation count
            if (!groupActiveAnimations.ContainsKey(groupIndex)) { groupActiveAnimations[groupIndex] = 0; }
            groupActiveAnimations[groupIndex]++;

            // Start the new animation with its unique ID
            Coroutine coroutine = StartCoroutine(AnimateCoroutineWithID(animData, animationID, groupIndex));
            activeAnimations[animData.type] = coroutine;
        }

        void SetInitialValues(AnimationData animData, ref Vector3 startPos, ref Vector3 startScale,
            ref float startAlpha, ref float startRotation, ref Color startColor)
        {
            if (!animData.animateFromCurrentValue)
            {
                switch (animData.type)
                {
                    case AnimationType.Fade:
                        startAlpha = animData.fadeFrom;
                        canvasGroup.alpha = startAlpha;
                        break;

                    case AnimationType.Scale:
                        startScale = animData.scaleFrom;
                        rectTransform.localScale = startScale;
                        break;

                    case AnimationType.Slide:
                        startPos = originalPosition + animData.slideFrom;
                        rectTransform.anchoredPosition = startPos;
                        break;

                    case AnimationType.Rotate:
                        startRotation = animData.rotateFrom;
                        rectTransform.rotation = Quaternion.Euler(0, 0, startRotation);
                        break;

                    case AnimationType.ColorTint:
                        startColor = animData.colorFrom;
                        SetColor(startColor);
                        break;
                }
            }
            else
            {
                // When using current values, make sure startColor is properly set for ColorTint
                if (animData.type == AnimationType.ColorTint)
                {
                    startColor = GetCurrentColor();
                }
            }
        }

        void ApplyAnimationFrame(AnimationData animData, float t, Vector3 startPos, Vector3 startScale,
            float startAlpha, float startRotation, Color startColor)
        {
            switch (animData.type)
            {
                case AnimationType.Fade:
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, animData.fadeTo, t);
                    break;

                case AnimationType.Scale:
                    rectTransform.localScale = Vector3.Lerp(startScale, animData.scaleTo, t);
                    break;

                case AnimationType.Slide:
                    Vector3 targetPosition = originalPosition + animData.slideTo;
                    rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPosition, t);
                    break;

                case AnimationType.Rotate:
                    float rotation = Mathf.Lerp(startRotation, animData.rotateTo, t);
                    rectTransform.rotation = Quaternion.Euler(0, 0, rotation);
                    break;

                case AnimationType.PunchScale:
                    float punchScale = 1f + animData.punchIntensity * Mathf.Sin(t * Mathf.PI);
                    rectTransform.localScale = startScale * punchScale;
                    break;

                case AnimationType.Shake:
                    float intensity = animData.shakeIntensity * (1f - t);
                    cachedShakeOffset.x = UnityEngine.Random.Range(-1f, 1f) * intensity;
                    cachedShakeOffset.y = UnityEngine.Random.Range(-1f, 1f) * intensity;
                    cachedShakeOffset.z = 0f;
                    rectTransform.anchoredPosition = startPos + cachedShakeOffset;
                    break;

                case AnimationType.Bounce:
                    float bounceY = animData.bounceHeight * Mathf.Sin(t * Mathf.PI);
                    Vector3 bouncePos = rectTransform.anchoredPosition;
                    bouncePos.y = startPos.y + bounceY;
                    rectTransform.anchoredPosition = bouncePos;
                    break;

                case AnimationType.ColorTint:
                    Color lerpedColor = Color.Lerp(startColor, animData.colorTo, t);
                    SetColor(lerpedColor);
                    break;
            }
        }

        void SetColor(Color color)
        {
            if (image != null) { image.color = color; }
            if (text != null) { text.color = color; }
        }

        bool IsAnimationActive(AnimationType type, int animationId)
        {
            return animationIds.TryGetValue(type, out int currentId) && currentId == animationId;
        }

        bool ShouldRestoreOriginalValues(AnimationData animData)
        {
            // Check if we need to restore based on animation type and current state
            return animData.type switch
            {
                AnimationType.PunchScale or AnimationType.Shake or AnimationType.Bounce => true, // These should always return to original
                _ => false // Others stay at their final values
            };
        }

        float ApplyEasing(float t, EaseType easeType)
        {
            switch (easeType)
            {
                case EaseType.Linear: return t;
                case EaseType.EaseInQuad: return t * t;
                case EaseType.EaseOutQuad: return t * (2f - t);
                case EaseType.EaseInOutQuad: return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
                case EaseType.EaseInCubic: return t * t * t;
                case EaseType.EaseOutCubic: return (--t) * t * t + 1f;
                case EaseType.EaseInOutCubic: return t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;
                case EaseType.EaseInSine: return 1f - Mathf.Cos(t * Mathf.PI / 2f);
                case EaseType.EaseOutSine: return Mathf.Sin(t * Mathf.PI / 2f);
                case EaseType.EaseInOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case EaseType.EaseOutBounce:
                    if (t < 1f / 2.75f) return 7.5625f * t * t;
                    else if (t < 2f / 2.75f) return 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f;
                    else if (t < 2.5f / 2.75f) return 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f;
                    else return 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;
                default: return t;
            }
        }

        Color GetCurrentColor()
        {
            if (image != null) { return image.color; }
            if (text != null) { return text.color; }
            return Color.white;
        }

        AnimationData CreateReversedAnimation(AnimationData original)
        {
            var reversed = new AnimationData
            {
                type = original.type,
                duration = original.duration,
                easeType = original.easeType,
                fadeFrom = original.fadeTo,
                fadeTo = original.fadeFrom,
                scaleFrom = original.scaleTo,
                scaleTo = original.scaleFrom,
                slideFrom = original.slideTo,
                slideTo = original.slideFrom,
                rotateFrom = original.rotateTo,
                rotateTo = original.rotateFrom,
                colorFrom = original.colorTo,
                colorTo = original.colorFrom,
                punchIntensity = original.punchIntensity,
                shakeIntensity = original.shakeIntensity,
                bounceHeight = original.bounceHeight,
                animateFromCurrentValue = true // Always use current for reversed animations
            };
            return reversed;
        }

        IEnumerator AnimateCoroutineWithID(AnimationData animData, int animationId, int groupIndex)
        {
            if (animData.delay > 0)
            {
                if (useUnscaledTime) { yield return new WaitForSecondsRealtime(animData.delay); }
                else { yield return new WaitForSeconds(animData.delay); }
            }

            // The previous animation will continue running until this exact moment.
            animationIds[animData.type] = animationId;

            do
            {
                yield return StartCoroutine(ExecuteAnimation(animData, animationId));

                if (animData.yoyo && IsAnimationActive(animData.type, animationId))
                {
                    var reversedData = CreateReversedAnimation(animData);
                    yield return StartCoroutine(ExecuteAnimation(reversedData, animationId));
                }

            } 
            while (animData.loop && IsAnimationActive(animData.type, animationId));

            // Clean up when animation completes (only if this is still the active animation)
            if (IsAnimationActive(animData.type, animationId))
            {
                if (activeAnimations.ContainsKey(animData.type)) { activeAnimations.Remove(animData.type); }
                if (animationIds.ContainsKey(animData.type)) { animationIds.Remove(animData.type); }

                // Restore original values if needed (for PunchScale, Shake, Bounce)
                if (!animData.loop && ShouldRestoreOriginalValues(animData))
                {
                    yield return StartCoroutine(RestoreToOriginalValues(animData, 0.1f));
                }

                // Decrement group animation count and trigger end event if this was the last one
                if (groupActiveAnimations.ContainsKey(groupIndex))
                {
                    groupActiveAnimations[groupIndex]--;
                    if (groupActiveAnimations[groupIndex] <= 0)
                    {
                        groupActiveAnimations.Remove(groupIndex);

                        // Trigger group end event
                        if (groupIndex >= 0 && groupIndex < animationGroups.Count)
                        {
                            var group = animationGroups[groupIndex];
                            group.onGroupEnd?.Invoke();
                        }

                        // Trigger global end event
                        onAnimationEnd?.Invoke();
                    }
                }
            }
        }

        IEnumerator RestoreToOriginalValues(AnimationData animData, float duration)
        {
            float elapsedTime = 0f;
            Vector3 startPos = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;

            while (elapsedTime < duration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / duration;

                switch (animData.type)
                {
                    case AnimationType.PunchScale:
                        rectTransform.localScale = Vector3.Lerp(startScale, originalScale, t);
                        break;
                    case AnimationType.Shake:
                    case AnimationType.Bounce:
                        rectTransform.anchoredPosition = Vector3.Lerp(startPos, originalPosition, t);
                        break;
                }

                yield return null;
            }

            // Ensure final restoration
            switch (animData.type)
            {
                case AnimationType.PunchScale:
                    rectTransform.localScale = originalScale;
                    break;
                case AnimationType.Shake:
                case AnimationType.Bounce:
                    rectTransform.anchoredPosition = originalPosition;
                    break;
            }
        }

        IEnumerator ExecuteAnimation(AnimationData animData, int animationID)
        {
            float elapsedTime = 0f;
            Vector3 startPos = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;
            float startAlpha = canvasGroup.alpha;
            float startRotation = rectTransform.eulerAngles.z;
            Color startColor = GetCurrentColor();

            // Set initial values
            SetInitialValues(animData, ref startPos, ref startScale, ref startAlpha, ref startRotation, ref startColor);

            while (elapsedTime < animData.duration)
            {
                // Only continue if this is still the active animation of this type
                if (!IsAnimationActive(animData.type, animationID))
                {
                    yield break; // Another animation of same type started, exit gracefully
                }

                float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsedTime += deltaTime;
                float t = Mathf.Clamp01(elapsedTime / animData.duration);
                float easedT = ApplyEasing(t, animData.easeType);

                ApplyAnimationFrame(animData, easedT, startPos, startScale, startAlpha, startRotation, startColor);
                yield return null;
            }

            // Only set final values if this is still the active animation
            if (IsAnimationActive(animData.type, animationID))
            {
                ApplyAnimationFrame(animData, 1f, startPos, startScale, startAlpha, startRotation, startColor);
            }
        }

        public void ExecuteAnimations(TriggerType triggerType)
        {
            if (animationGroups == null || !gameObject.activeInHierarchy)
                return;

            for (int groupIndex = 0; groupIndex < animationGroups.Count; groupIndex++)
            {
                AnimationGroup group = animationGroups[groupIndex];
                if (group?.trigger == triggerType && group.animations != null)
                {
                    bool hasEnabledAnimations = false;
                    foreach (var animData in group.animations)
                    {
                        if (animData?.enabled == true)
                        {
                            hasEnabledAnimations = true;
                            break;
                        }
                    }

                    if (hasEnabledAnimations)
                    {
                        group.onGroupStart?.Invoke();
                        onAnimationStart?.Invoke();

                        foreach (var animData in group.animations)
                        {
                            if (animData?.enabled == true)
                            {
                                PlayAnimation(animData, groupIndex);
                            }
                        }
                    }
                }
            }
        }

        public void StopAllAnimations()
        {
            foreach (var kvp in activeAnimations)
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }
            }
            activeAnimations.Clear();
            animationIds.Clear();
            groupActiveAnimations.Clear();
        }

        /// <summary>
        /// Plays all manual animation groups.
        /// </summary>
        public void PlayManualAnimations()
        {
            ExecuteAnimations(TriggerType.Manual);
        }

        /// <summary>
        /// Plays specified animation group by label.
        /// </summary>
        public void PlayAnimationGroup(string targetLabel)
        {
            if (animationGroups == null || !gameObject.activeInHierarchy || string.IsNullOrEmpty(targetLabel))
                return;

            for (int groupIndex = 0; groupIndex < animationGroups.Count; groupIndex++)
            {
                AnimationGroup group = animationGroups[groupIndex];

                // Check if this group matches the target label
                if (group.label == targetLabel && group.animations != null)
                {
                    bool hasEnabledAnimations = false;
                    foreach (var animData in group.animations)
                    {
                        if (animData?.enabled == true)
                        {
                            hasEnabledAnimations = true;
                            break;
                        }
                    }

                    if (hasEnabledAnimations)
                    {
                        group.onGroupStart?.Invoke();
                        onAnimationStart?.Invoke();

                        foreach (var animData in group.animations)
                        {
                            if (animData?.enabled == true)
                            {
                                PlayAnimation(animData, groupIndex);
                            }
                        }
                    }

                    // Found and played the matching group
                    return;
                }
            }

            // Optional: Log warning if no matching group was found
            Debug.LogWarning($"No manual animation group found with label: {targetLabel}");
        }

        public void ResetToOriginalValues()
        {
            StopAllAnimations();
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalPosition;
                rectTransform.rotation = Quaternion.Euler(0, 0, originalRotation);
            }
            if (canvasGroup != null) { canvasGroup.alpha = originalAlpha; }
            SetColor(originalColor);
        }

        public void RemoveAnimationGroup(int index)
        {
            if (index < 0 || index >= animationGroups.Count) { return; }
            animationGroups.RemoveAt(index);
        }

        public void MoveGroupUp(int index)
        {
            if (index > 0)
            {
                (animationGroups[index - 1], animationGroups[index]) = (animationGroups[index], animationGroups[index - 1]);
            }
        }

        public void MoveGroupDown(int index)
        {
            if (index < animationGroups.Count - 1)
            {
                (animationGroups[index + 1], animationGroups[index]) = (animationGroups[index], animationGroups[index + 1]);
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool groupFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}