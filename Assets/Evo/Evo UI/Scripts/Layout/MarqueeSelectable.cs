using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl)]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Marquee Selectable")]
    public class MarqueeSelectable : MonoBehaviour, IMarqueeSelectable
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private bool interactable = true;
        [Tooltip("When enabled, this object can only be drag-moved if it is marquee-selected first.")]
        public bool requireSelectionToDrag;
        public SelectableType selectableType = SelectableType.UI_Graphic;

        [EvoHeader("UI (Interactive) Settings", Constants.CustomEditorID)]
        [Tooltip("Interactive component to drive. Auto-detected if null.")]
        [SerializeField] private Interactive targetInteractive;
        [Tooltip("InteractionState to apply when selected by marquee.")]
        [SerializeField] private InteractionState selectedState = InteractionState.Selected;
        [Tooltip("InteractionState to apply when deselected. Only used if the Interactive is interactable.")]
        [SerializeField] private InteractionState normalState = InteractionState.Normal;

        [EvoHeader("UI (Graphic) Settings", Constants.CustomEditorID)]
        [Tooltip("Graphic to tint. Auto-detected if null.")]
        [SerializeField] private Graphic targetGraphic;

        [EvoHeader("World (3D) Settings", Constants.CustomEditorID)]
        [Tooltip("Renderer to tint. Auto-detected if null.")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Include child renderers when computing screen bounds.")]
        [SerializeField] private bool includeChildren;

        [EvoHeader("Shared Settings", Constants.CustomEditorID)]
        [Tooltip("Duration of the color fade transition. 0 = instant.")]
        [SerializeField, Range(0f, 2f)] private float fadeDuration = 0.15f;
        [SerializeField] private Color selectedColor = new(0.6f, 0.82f, 1f, 1f);

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent onSelected = new();
        public UnityEvent onDeselected = new();

        public enum SelectableType
        {
            [InspectorName("UI (Interactive)")] UI_Interactive,
            [InspectorName("UI (Graphic)")] UI_Graphic,
            [InspectorName("World (3D)")] World
        }

        // Interface
        public bool IsSelected => isSelected;
        public bool Interactable => interactable && isActiveAndEnabled;
        public Transform Transform => transform;
        public bool IsInsideScreenRect(Rect screenRect, Camera renderCamera)
        {
            return selectableType == SelectableType.World
                ? IsInsideScreenRect_World(screenRect, renderCamera)
                : IsInsideScreenRect_UI(screenRect, renderCamera);
        }

        // Public Properties
        public MarqueeSelectionContainer CurrentParent { get; private set; }

        // State
        bool isSelected;
        RectTransform rect;
        MaterialPropertyBlock mpb;

        // Cached resting state — captured before any selection visual
        bool hasCachedState;
        Color cachedColor;
        InteractionState cachedInteractionState;

        // Fade tracking
        bool isFading;
        Color fadeTarget;
        Coroutine fadeCoroutine;

        // Shader props
        static readonly int ColorProp = Shader.PropertyToID("_Color");
        static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

        // Registry for 3D objects
        static readonly List<MarqueeSelectable> globalWorld = new(64);
        static readonly HashSet<MarqueeSelectable> globalWorldSet = new(64);
        public static IReadOnlyList<MarqueeSelectable> GlobalWorldSelectables => globalWorld;

        void Awake()
        {
            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive == null) { targetInteractive = GetComponent<Interactive>(); }
                    if (targetInteractive == null && targetGraphic == null) { targetGraphic = GetComponent<Graphic>(); }
                    rect = GetComponent<RectTransform>();
                    break;

                case SelectableType.UI_Graphic:
                    rect = GetComponent<RectTransform>();
                    if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
                    break;

                case SelectableType.World:
                    mpb = new MaterialPropertyBlock();
                    if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
                    break;
            }
        }

        void OnEnable()
        {
            if (selectableType == SelectableType.World && globalWorldSet.Add(this)) { globalWorld.Add(this); }
            else { FindAndRegisterParent(); }
        }

        void OnDisable()
        {
            FinishFadeImmediate();

            if (selectableType == SelectableType.World && globalWorldSet.Remove(this)) { globalWorld.Remove(this); }
            else if (CurrentParent != null)
            {
                CurrentParent.UnregisterSelectable(this);
                CurrentParent = null;
            }
        }

        #region State Caching
        void CacheCurrentState()
        {
            if (hasCachedState)
                return;

            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive != null) { cachedInteractionState = targetInteractive.InteractionState; }
                    else if (targetGraphic != null) { cachedColor = isFading ? fadeTarget : targetGraphic.color; }
                    break;

                case SelectableType.UI_Graphic:
                    if (targetGraphic != null) { cachedColor = isFading ? fadeTarget : targetGraphic.color; }
                    break;

                case SelectableType.World:
                    if (targetRenderer != null) { cachedColor = isFading ? fadeTarget : ReadRendererColor(); }
                    break;
            }
            hasCachedState = true;
        }

        void ApplySelectedVisual()
        {
            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive != null) { targetInteractive.SetState(selectedState); }
                    else if (targetGraphic != null) { FadeColorTo(selectedColor); }
                    break;

                case SelectableType.UI_Graphic:
                    if (targetGraphic != null) { FadeColorTo(selectedColor); }
                    break;

                case SelectableType.World:
                    FadeColorTo(selectedColor);
                    break;
            }
        }

        void RestoreCachedState()
        {
            if (!hasCachedState)
                return;

            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive != null)
                    {
                        // Use normalState if the Interactive is interactable, otherwise restore cached
                        InteractionState restoreState = targetInteractive.IsInteractable() ? normalState : cachedInteractionState;
                        targetInteractive.SetState(restoreState);
                    }
                    else if (targetGraphic != null) { FadeColorTo(cachedColor); }
                    break;

                case SelectableType.UI_Graphic:
                    if (targetGraphic != null) { FadeColorTo(cachedColor); }
                    break;

                case SelectableType.World:
                    FadeColorTo(cachedColor);
                    break;
            }
            hasCachedState = false;
        }
        #endregion

        #region Fade System
        void FadeColorTo(Color target)
        {
            FinishFadeImmediate();
            fadeTarget = target;

            bool canAnimate = fadeDuration > 0f && Application.isPlaying && gameObject.activeInHierarchy;
            if (!canAnimate)
            {
                ApplyColorImmediate(target);
                return;
            }

            Color start = ReadCurrentColor();
            if (ColorsEqual(start, target))
            {
                ApplyColorImmediate(target);
                return;
            }

            isFading = true;
            fadeCoroutine = StartCoroutine(FadeCoroutine(start, target));
        }

        void FinishFadeImmediate()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
                ApplyColorImmediate(fadeTarget);
            }
            isFading = false;
        }

        IEnumerator FadeCoroutine(Color from, Color to)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                ApplyColorImmediate(Color.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            ApplyColorImmediate(to);
            fadeCoroutine = null;
            isFading = false;
        }

        void ApplyColorImmediate(Color c)
        {
            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive == null && targetGraphic != null) { targetGraphic.color = c; }
                    break;
                case SelectableType.UI_Graphic:
                    if (targetGraphic != null) { targetGraphic.color = c; }
                    break;
                case SelectableType.World:
                    ApplyRendererColor(c);
                    break;
            }
        }

        Color ReadCurrentColor()
        {
            switch (selectableType)
            {
                case SelectableType.UI_Interactive:
                    if (targetInteractive == null && targetGraphic != null) { return targetGraphic.color; }
                    break;
                case SelectableType.UI_Graphic:
                    if (targetGraphic != null) { return targetGraphic.color; }
                    break;
                case SelectableType.World:
                    return ReadRendererColor();
            }
            return Color.white;
        }

        Color ReadRendererColor()
        {
            if (targetRenderer == null)
                return Color.white;

            mpb ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(mpb);
            return mpb.GetColor(ColorProp);
        }

        void ApplyRendererColor(Color c)
        {
            if (targetRenderer == null)
                return;

            mpb ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorProp, c);
            mpb.SetColor(BaseColorProp, c);
            targetRenderer.SetPropertyBlock(mpb);
        }

        static bool ColorsEqual(Color a, Color b)
        {
            const float e = 0.001f;
            return Mathf.Abs(a.r - b.r) < e && Mathf.Abs(a.g - b.g) < e &&
                   Mathf.Abs(a.b - b.b) < e && Mathf.Abs(a.a - b.a) < e;
        }
        #endregion

        #region UI Overlap
        // Reusable array to avoid GC
        readonly Vector3[] corners = new Vector3[4];

        bool IsInsideScreenRect_UI(Rect screenRect, Camera renderCamera)
        {
            if (rect == null) { return false; }
            rect.GetWorldCorners(corners);

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (canvas.worldCamera != null ? canvas.worldCamera : renderCamera)
                : null;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = cam != null
                    ? RectTransformUtility.WorldToScreenPoint(cam, corners[i])
                    : new Vector2(corners[i].x, corners[i].y);

                if (sp.x < minX) { minX = sp.x; }
                if (sp.y < minY) { minY = sp.y; }
                if (sp.x > maxX) { maxX = sp.x; }
                if (sp.y > maxY) { maxY = sp.y; }
            }

            return screenRect.Overlaps(Rect.MinMaxRect(minX, minY, maxX, maxY));
        }
        #endregion

        #region World/3D Overlap
        bool IsInsideScreenRect_World(Rect screenRect, Camera renderCamera)
        {
            if (renderCamera == null)
                return false;

            Bounds bounds = GetWorldBounds();
            Vector3 c = bounds.center;
            Vector3 ext = bounds.extents;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? ext.x : -ext.x,
                    (i & 2) == 0 ? ext.y : -ext.y,
                    (i & 4) == 0 ? ext.z : -ext.z);

                Vector3 sp = renderCamera.WorldToScreenPoint(corner);
                if (sp.z < 0) { continue; }

                if (sp.x < minX) { minX = sp.x; }
                if (sp.y < minY) { minY = sp.y; }
                if (sp.x > maxX) { maxX = sp.x; }
                if (sp.y > maxY) { maxY = sp.y; }
            }

            if (minX > maxX) { return false; }
            return screenRect.Overlaps(Rect.MinMaxRect(minX, minY, maxX, maxY));
        }

        Bounds GetWorldBounds()
        {
            if (includeChildren)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { return new Bounds(transform.position, Vector3.one); }

                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) { b.Encapsulate(renderers[i].bounds); }
                return b;
            }
            return targetRenderer != null ? targetRenderer.bounds : new Bounds(transform.position, Vector3.one);
        }
        #endregion

        #region Public Methods
        public void Select() => OnMarqueeSelect();
        public void Deselect() => OnMarqueeDeselect();

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!interactable && isSelected) OnMarqueeDeselect();
        }

        public void FindAndRegisterParent()
        {
            if (CurrentParent != null) { CurrentParent.UnregisterSelectable(this); }
            CurrentParent = GetComponentInParent<MarqueeSelectionContainer>();
            if (CurrentParent != null) { CurrentParent.RegisterSelectable(this); }
        }

        public void OnMarqueeSelect()
        {
            if (IsSelected)
                return;

            CacheCurrentState();
            isSelected = true;
            ApplySelectedVisual();
            onSelected?.Invoke();
        }

        public void OnMarqueeDeselect()
        {
            if (!isSelected)
                return;

            isSelected = false;
            RestoreCachedState();
            onDeselected?.Invoke();
        }
        #endregion

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}