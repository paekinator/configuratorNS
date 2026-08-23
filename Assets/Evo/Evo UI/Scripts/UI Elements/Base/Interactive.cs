using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Base class for all interactive UI elements with state management.
    /// Based on Unity's Selectable class with extended features.
    /// </summary>
    public class Interactive : Selectable, IStylerHandler, IStylerInteractable, IPointerDownHandler, IPointerClickHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [EvoHeader("Ripple Effect", Constants.CustomEditorID)]
        [Tooltip("Adds a drop or ripple effect on click.")]
        public bool enableRipple = false;
        [Tooltip("This transform will serve as the parent for ripple objects.")]
        [SerializeField] private RectTransform rippleParent;
        public Ripple.Preset ripplePreset;

        [EvoHeader("Trail Effect", Constants.CustomEditorID)]
        [Tooltip("Creates a sprite that follows the cursor.")]
        public bool enableTrail = false;
        [Tooltip("This transform will serve as the parent for the trail object.")]
        [SerializeField] private RectTransform trailParent;
        public PointerTrail.Preset trailPreset;

        [EvoHeader("Styling", Constants.CustomEditorID)]
        public StylingSource sfxSource = StylingSource.StylerPreset;
        public StylerPreset stylerPreset;

        [EvoHeader("SFX", Constants.CustomEditorID)]
        public AudioMapping highlightedSFX = new() { stylerID = "Hover SFX" };
        public AudioMapping pressedSFX = new() { stylerID = "Click SFX" };
        public AudioMapping selectedSFX = new() { stylerID = "" };

        [EvoHeader("Cursor", Constants.CustomEditorID)]
        [Tooltip("Controls the cursor type on hover.")]
        public CursorType cursorType = CursorType.Default;
        [Tooltip("The texture of the custom cursor.")]
        public Texture2D cursorTexture;
        [Tooltip("The offset from the top left of the texture.")]
        public Vector2 cursorHotspot = Vector2.zero;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField, Range(0f, 2f)] protected float transitionDuration = 0.1f;
        [SerializeField] protected InteractionState interactionState = InteractionState.Normal;

        [EvoHeader("References", Constants.CustomEditorID)]
        [SerializeField] private CanvasGroup disabledCG;
        [SerializeField] private CanvasGroup normalCG;
        [SerializeField] private CanvasGroup highlightedCG;
        [SerializeField] private CanvasGroup pressedCG;
        [SerializeField] private CanvasGroup selectedCG;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent onClick = new();
        public UnityEvent onPointerDown = new();
        public UnityEvent onPointerUp = new();
        public UnityEvent onDoubleClick = new();
        public UnityEvent onPointerEnter = new();
        public UnityEvent onPointerExit = new();
        public UnityEvent onSelect = new();
        public UnityEvent onDeselect = new();
        public UnityEvent onSubmit = new();

        // State change event for external listeners
        public event System.Action<InteractionState> OnStateChanged;

        // ID Cache
        static readonly string[] interactionStateIDsCache = new[] { "Disabled", "Normal", "Highlighted", "Pressed", "Selected" };
        static readonly string[] sfxFieldsCache = new[] { "highlightedSFX", "pressedSFX", "selectedSFX" };

        // ID Fetchers
        public static string[] GetInteractionStateIDs() => interactionStateIDsCache;
        public static string[] GetSFXFields() => sfxFieldsCache;

        // IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        // IStylerInteractable Implementation
        public InteractionState InteractionState
        {
            get => interactionState;
            set => SetState(value);
        }
        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = value;
        }

        // Cache
        Graphic raycastGraphic;
        protected bool isPointerOn;
        protected bool isPressedDown;
        protected bool waitingForDoubleClickInput;
        protected Coroutine currentAnimation;
        protected readonly Dictionary<InteractionState, CanvasGroup> stateCanvasGroupsCache = new();
        protected readonly Dictionary<CanvasGroup, float> animationTargetsCache = new();
        readonly List<StylerObject> stylerObjectsCache = new();

        protected override void OnEnable()
        {
            isPointerOn = false;
            isPressedDown = false;
            waitingForDoubleClickInput = false;
            currentAnimation = null;

            if (Application.isPlaying && raycastGraphic == null)
                raycastGraphic = Utilities.AddRaycastGraphic(gameObject);

            base.OnEnable();

            SetState(interactionState, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (isPointerOn && cursorType == CursorType.CustomHardware)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            if (IsInteractable() && interactionState != InteractionState.Selected)
                interactionState = InteractionState.Normal;
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();

            if (Time.frameCount == 0 || interactionState == InteractionState.Selected)
                return;

            if (!IsInteractable() && interactionState != InteractionState.Disabled)
                SetState(InteractionState.Disabled);
            else if (IsInteractable() && interactionState == InteractionState.Disabled && (isPointerOn || Utilities.GetSelectedObject() == gameObject))
                SetState(InteractionState.Highlighted);
            else if (IsInteractable() && interactionState == InteractionState.Disabled)
                SetState(InteractionState.Normal);

#if UNITY_EDITOR
            // Update visuals for editor, pre-runtime
            if (!Application.isPlaying) { UpdateStyler(); }
#endif
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            onClick?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            isPressedDown = true;

            if (enableRipple)
                Ripple.Create(ripplePreset, rippleParent, true);

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Pressed);

            if (navigation.mode != Navigation.Mode.None)
                Utilities.SetSelectedObject(gameObject);

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, pressedSFX, stylerPreset));
            onPointerDown?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected)
                SetState(isPointerOn ? InteractionState.Highlighted : InteractionState.Normal);

            isPressedDown = false;
            onPointerUp?.Invoke();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOn = true;

            if (!IsInteractable())
                return;

            if (cursorType == CursorType.CustomHardware && cursorTexture != null)
                Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);

            if (interactionState != InteractionState.Selected && !isPressedDown)
                SetState(InteractionState.Highlighted);

            if (enableTrail)
                PointerTrail.Create(trailPreset, trailParent, true);

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, highlightedSFX, stylerPreset));
            onPointerEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            isPointerOn = false;

            if (cursorType == CursorType.CustomHardware)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected && !isPressedDown)
                SetState(InteractionState.Normal);

            if (enableTrail)
                PointerTrail.Hide(trailParent);

            onPointerExit?.Invoke();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            if (!IsInteractable())
                return;

            onSelect?.Invoke();

            if (isPressedDown)
                return;

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Highlighted);

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, highlightedSFX, stylerPreset));
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Normal);

            onDeselect?.Invoke();
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            if (!IsInteractable())
                return;

            if (enableRipple)
                Ripple.Create(ripplePreset, rippleParent, true, true);

            if (interactionState != InteractionState.Selected && Utilities.GetSelectedObject() != gameObject)
                SetState(InteractionState.Normal);

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, pressedSFX, stylerPreset));
            onClick?.Invoke();
            onSubmit?.Invoke();
        }

        protected virtual IEnumerator AnimateToState(InteractionState state)
        {
            if (state == InteractionState.Selected)
                AudioManager.PlayClip(Styler.GetAudio(sfxSource, selectedSFX, stylerPreset));

            if (enableTrail && state == InteractionState.Disabled)
                PointerTrail.Hide(trailParent);

            var stateGroups = GetStateCanvasGroups();
            animationTargetsCache.Clear();

            foreach (var kvp in stateGroups)
            {
                if (kvp.Value != null)
                    animationTargetsCache[kvp.Value] = kvp.Key == state ? 1 : 0;
            }

            yield return Utilities.CrossFadeCanvasGroup(animationTargetsCache, Mathf.Max(0f, transitionDuration));
        }

        /// <summary>
        /// Sets the interactable state.
        /// </summary>
        public virtual void SetState(InteractionState state, bool instant = false)
        {
            if (interactionState == state)
                return;

            if (cursorType == CursorType.CustomHardware && isPointerOn)
            {
                if (state == InteractionState.Disabled)
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                else if (interactionState == InteractionState.Disabled && cursorTexture != null)
                    Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
            }

            interactionState = state;
            OnStateChanged?.Invoke(interactionState);

            if (!Application.isPlaying || instant || !gameObject.activeInHierarchy)
            {
                var stateGroups = GetStateCanvasGroups();
                foreach (var kvp in stateGroups)
                {
                    if (kvp.Value != null)
                        kvp.Value.alpha = kvp.Key == state ? 1 : 0;
                }
            }
            else
            {
                if (currentAnimation != null) { StopCoroutine(currentAnimation); }
                currentAnimation = StartCoroutine(AnimateToState(state));
            }
        }

        /// <summary>
        /// Helper method to change interactable state and update visual state accordingly.
        /// </summary>
        public virtual void SetInteractable(bool value)
        {
            if (interactable && value)
                return;

            isPressedDown = !value && isPressedDown;
            isPointerOn = !value && isPointerOn;
            interactable = value;

            SetState(value ? InteractionState.Normal : InteractionState.Disabled);
        }

        /// <summary>
        /// Override this to map custom states to canvas groups.
        /// </summary>
        protected virtual Dictionary<InteractionState, CanvasGroup> GetStateCanvasGroups()
        {
            stateCanvasGroupsCache.Clear();

            if (disabledCG != null) { stateCanvasGroupsCache[InteractionState.Disabled] = disabledCG; }
            if (normalCG != null) { stateCanvasGroupsCache[InteractionState.Normal] = normalCG; }
            if (highlightedCG != null) { stateCanvasGroupsCache[InteractionState.Highlighted] = highlightedCG; }
            if (pressedCG != null) { stateCanvasGroupsCache[InteractionState.Pressed] = pressedCG; }
            if (selectedCG != null) { stateCanvasGroupsCache[InteractionState.Selected] = selectedCG; }

            return stateCanvasGroupsCache;
        }

        public void UpdateStyler()
        {
            if (!gameObject.activeInHierarchy)
                return;

            GetComponentsInChildren(true, stylerObjectsCache);
            for (int i = 0; i < stylerObjectsCache.Count; i++)
            {
                var styler = stylerObjectsCache[i];

                if (styler != null && styler.enableInteraction && styler.interactableObject == this)
                    styler.UpdateStyler();
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateEditorState();
        }

        protected void UpdateEditorState()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    if (!IsInteractable()) { interactionState = InteractionState.Disabled; }
                    else if (interactionState == InteractionState.Disabled) { interactionState = InteractionState.Normal; }

                    SetState(interactionState, true);
                    UpdateStyler();
                }
            };
        }
#endif
    }
}