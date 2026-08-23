using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/radio-button")]
    [AddComponentMenu("Evo/UI/UI Elements/Radio Button Group")]
    public class RadioButtonGroup : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        public int selectedIndex = -1;
        [SerializeField] private bool allowDeselection = false;
        [SerializeField] private bool useUnscaledTime = true;

        [EvoHeader("Indicator", Constants.CustomEditorID)]
        [SerializeField] private RectTransform indicatorObject;
        [SerializeField] private IndicatorDirection indicatorDirection = IndicatorDirection.Horizontal;
        [SerializeField] private bool indicatorAutoSize = true;
        [SerializeField, Min(0f)] private float indicatorStretch = 0f;
        [SerializeField] private AnimationCurve indicatorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0.01f, 1f)] private float indicatorDuration = 0.3f;
        [SerializeField, Range(0f, 1f)] private float stateChangeDelay = 0f;

        [EvoHeader("References", Constants.CustomEditorID)]
        public Transform targetParent;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent<int> onSelectionChanged = new();

        // Cache
        Button selectedButton;
        Coroutine indicatorCoroutine;
        Coroutine stateDelayCoroutine;
        WaitForEndOfFrame cachedEndOfFrame;
        readonly List<Button> availableButtons = new();

#if UNITY_EDITOR
        readonly List<Button> editorPreviewButtonsCache = new();
#endif

        // Enums
        public enum IndicatorDirection { Horizontal, Vertical }

        void Awake()
        {
            cachedEndOfFrame = new WaitForEndOfFrame();
            Initialize();
        }

        void OnEnable()
        {
            // If there's a selection, force the indicator to snap to it again.
            // Just in case the layout changed while this object was disabled.
            if (selectedIndex >= 0 && availableButtons.Count > selectedIndex)
                UpdateIndicator(availableButtons[selectedIndex], false);
        }

        public void Initialize()
        {
            selectedButton = null;
            availableButtons.Clear();

            // Get button parent
            if (targetParent == null)
                targetParent = transform;

            // Get all Button components from direct children only
            for (int i = 0; i < targetParent.childCount; i++)
            {
                Transform child = targetParent.GetChild(i);
                if (child.TryGetComponent(out Button btn))
                {
                    availableButtons.Add(btn);
                    int btnIndex = availableButtons.Count - 1;
                    btn.onClick.AddListener(() => SetButton(btnIndex));
                }
                else
                {
                    // Add null to maintain index consistency
                    availableButtons.Add(null);
                }
            }

            // Set default selection if specified
            if (selectedIndex >= 0 && selectedIndex < availableButtons.Count)
                SetButton(selectedIndex, false);
            else
                UpdateIndicator(null, false); // Initialize indicator state (hidden) if nothing is selected
        }

        void UpdateIndicator(Button targetButton, bool animate)
        {
            if (indicatorObject == null || !gameObject.activeInHierarchy)
                return;

            if (indicatorCoroutine != null)
                StopCoroutine(indicatorCoroutine);

            indicatorCoroutine = StartCoroutine(AnimateIndicatorRoutine(targetButton, animate));
        }

        IEnumerator AnimateIndicatorRoutine(Button targetButton, bool animate)
        {
            // Wait for end of frame to ensure layout rebuilds if this was called during initialization
            if (!animate)
                yield return cachedEndOfFrame;

            RectTransform targetRect = (targetButton != null) ? targetButton.GetComponent<RectTransform>() : null;

            // Capture current visual state (world space)
            Vector3 worldPos = indicatorObject.position;
            Vector2 startSize = indicatorObject.sizeDelta;
            Vector2 startAnchoredPos;

            // Determine Target Values
            Vector2 targetAnchoredPos;
            Vector2 targetSize = startSize;

            if (targetRect != null)
            {
                // Match anchors and pivot to the target completely
                indicatorObject.anchorMin = targetRect.anchorMin;
                indicatorObject.anchorMax = targetRect.anchorMax;
                indicatorObject.pivot = targetRect.pivot;

                // Restore visual position after anchor change
                indicatorObject.position = worldPos;
                startAnchoredPos = indicatorObject.anchoredPosition;

                // Target is the target's anchored position (relative to its own anchors)
                targetAnchoredPos = targetRect.anchoredPosition;

                // Calculate target size
                if (indicatorAutoSize)
                    targetSize = targetRect.sizeDelta;
            }
            else
            {
                // Keep existing anchors/pivots to shrink in place.
                startAnchoredPos = indicatorObject.anchoredPosition;
                targetAnchoredPos = startAnchoredPos;

                // Shrink size to 0 on relevant axis
                if (indicatorDirection == IndicatorDirection.Horizontal) { targetSize.x = 0; }
                else { targetSize.y = 0; }
            }

            // Animate or Snap
            if (animate && Application.isPlaying)
            {
                float elapsed = 0f;
                while (elapsed < indicatorDuration)
                {
                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / indicatorDuration);
                    float curveValue = indicatorCurve.Evaluate(t);

                    // Calculate stretch based on progression
                    // Only stretch if there's a target, otherwise just shrink (disappear)
                    float stretchValue = (targetRect != null) ? Mathf.Sin(Mathf.Clamp01(curveValue) * Mathf.PI) * indicatorStretch : 0f;

                    // Lerp position and size (Both axes to handle grid/multiline movement)
                    Vector2 currentPos = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, curveValue);
                    Vector2 currentSize = Vector2.Lerp(startSize, targetSize, curveValue);

                    // Apply stretch to main axis
                    if (indicatorDirection == IndicatorDirection.Horizontal) { currentSize.x += stretchValue; }
                    else { currentSize.y += stretchValue; }

                    // Apply pos
                    indicatorObject.anchoredPosition = currentPos;
                    indicatorObject.sizeDelta = currentSize;

                    yield return null;
                }
            }

            // Ensure exact snap at end
            indicatorObject.anchoredPosition = targetAnchoredPos;
            indicatorObject.sizeDelta = targetSize;
        }

        public void SetButton(int index) => SetButton(index, true);

        public void SetButton(int index, bool animate)
        {
            if (index < 0 || index >= availableButtons.Count)
            {
                Debug.LogWarning($"[Radio Button Group] Invalid button index {index}. Valid range: 0-{availableButtons.Count - 1}", this);
                return;
            }

            Button targetButton = availableButtons[index];
            if (targetButton == null)
            {
                Debug.LogWarning($"[Radio Button Group] No button found at index {index}", this);
                return;
            }

            // Allow deselection if enabled and clicking the same button
            if (allowDeselection && selectedButton == targetButton)
            {
                DeselectAll();
                return;
            }

            // Don't do anything if this button is already selected
            if (selectedButton == targetButton)
                return;

            // Stop any pending state change
            if (stateDelayCoroutine != null)
                StopCoroutine(stateDelayCoroutine);

            // Update Indicator immediately so it starts moving
            UpdateIndicator(targetButton, animate);

            // Determine if we should use delay (only if indicator is active and valid)
            bool useDelay = animate && stateChangeDelay > 0 && indicatorObject != null && indicatorObject.gameObject.activeInHierarchy;
            if (!useDelay) { ApplyStateChange(targetButton, index, false); }
            else
            {
                onSelectionChanged?.Invoke(index); // Fire events to ensure no delay for callbacks
                stateDelayCoroutine = StartCoroutine(ApplyStateChangeDelayed(targetButton, index));
            }
        }

        IEnumerator ApplyStateChangeDelayed(Button targetButton, int index)
        {
            // Set button non-interactable to avoid spamming
            targetButton.interactable = false;

            // Wait for delay using a zero-allocation loop
            float elapsed = 0f;
            while (elapsed < stateChangeDelay)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            // Set button interactable and apply the state
            targetButton.interactable = true;
            ApplyStateChange(targetButton, index, true);
        }

        void ApplyStateChange(Button targetButton, int index, bool isDelayed)
        {
            // Deselect current button
            if (selectedButton != null)
                selectedButton.SetState(InteractionState.Normal);

            // Select new button
            targetButton.SetState(InteractionState.Selected);
            selectedButton = targetButton;
            selectedIndex = index;

            // Check for delay and notify listeners
            // We already notify listeners in coroutine, so bypassing for duplicate
            if (!isDelayed)
                onSelectionChanged?.Invoke(index);
        }

        public void DeselectAll()
        {
            // Stop any pending state change
            if (stateDelayCoroutine != null)
                StopCoroutine(stateDelayCoroutine);

            // Check for selected button
            if (selectedButton != null)
            {
                selectedButton.SetState(InteractionState.Normal);
                selectedButton = null;
                selectedIndex = -1;

                // Update Indicator
                UpdateIndicator(null, true);

                // Fire events
                onSelectionChanged?.Invoke(-1);
            }
        }

        /// <summary>
        /// Sets interactable state for all buttons.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < availableButtons.Count; i++)
            {
                if (availableButtons[i] != null)
                    availableButtons[i].interactable = interactable;
            }
        }

        /// <summary>
        /// Returns the currently selected button.
        /// </summary>
        public Button GetButton()
        {
            if (availableButtons[selectedIndex] == null)
                return null;

            return availableButtons[selectedIndex];
        }

        /// <summary>
        /// Returns the specified button at index.
        /// </summary>
        public Button GetButton(int index)
        {
            if (availableButtons[index] == null)
                return null;

            return availableButtons[index];
        }

        /// <summary>
        /// Returns true if specified button index is selected.
        /// </summary>
        public bool IsButtonSelected(int index) => selectedIndex == index;

#if UNITY_EDITOR
        bool pendingEditorUpdate = false;
        int lastSelectedIndex = -1;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Get target parent
            if (targetParent == null)
                targetParent = transform;

            // Clamp selected index
            selectedIndex = targetParent.childCount > 0 ? Mathf.Clamp(selectedIndex, -1, targetParent.childCount - 1) : -1;

            // Update editor preview if index changed - defer to avoid SendMessage warnings
            if (lastSelectedIndex != selectedIndex)
            {
                if (!pendingEditorUpdate)
                {
                    pendingEditorUpdate = true;
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            UpdateEditorPreview(targetParent);
                            pendingEditorUpdate = false;
                        }
                    };
                }
                lastSelectedIndex = selectedIndex;
            }
        }

        void UpdateEditorPreview(Transform targetParent)
        {
            editorPreviewButtonsCache.Clear();

            for (int i = 0; i < targetParent.childCount; i++)
            {
                if (targetParent.GetChild(i).TryGetComponent(out Button btn))
                    editorPreviewButtonsCache.Add(btn);
                else
                    editorPreviewButtonsCache.Add(null);
            }

            // Update States
            for (int i = 0; i < editorPreviewButtonsCache.Count; i++)
            {
                var btn = editorPreviewButtonsCache[i];
                
                if (btn == null)
                    continue;

                if (i != selectedIndex) { btn.SetState(InteractionState.Normal); }
                else
                {
                    btn.SetState(InteractionState.Selected);
                    UpdateIndicator(btn, false);
                }
            }

            // Handle deselect case
            if (selectedIndex == -1)
                UpdateIndicator(null, false);
        }
#endif
    }
}