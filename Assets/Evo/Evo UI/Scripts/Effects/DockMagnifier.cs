using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Applies a macOS dock-style magnification effect to child elements when hovered.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/dock-magnifier")]
    [AddComponentMenu("Evo/UI/Effects/Dock Magnifier")]
    public class DockMagnifier : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Header("Scale Settings")]
        [Min(1f)] public float maxScale = 1.5f;
        [Min(0.01f)] public float baseScale = 1f;
        [Tooltip("Determines which axes are scaled during magnification.")]
        public Orientation scaleDirection = Orientation.Free;

        [Header("Magnification Settings")]
        [Tooltip("Pixels in parent local space.")]
        [Min(1f)] public float influenceRadius = 120f;
        [Tooltip("Widens influence along the main axis.")]
        [Min(0.01f)] public float axisBias = 2f;
        [Range(0.001f, 1f)] public float smoothTime = 0.05f;

        [Header("Behavior Settings")]
        [Tooltip("While hovering, refresh pointer from current mouse position every frame. Fixes ScrollRect wheel/touchpad scrolling issues.")]
        [SerializeField] private bool alwaysUpdateOnHover = true;
        [Tooltip("Only refresh pointer-from-mouse when the cursor is actually over this RectTransform.")]
        [SerializeField] private bool requirePointerInsideForRefresh = true;
        public Orientation orientation = Orientation.Horizontal;

        [Header("Restriction")]
        [Tooltip("Ignores attached rects from magnifiation.")]
        public List<RectTransform> ignoredRects = new();

        // Cache
        Canvas canvas;
        Camera eventCamera;
        RectTransform parentRect;
        ScrollRect parentScrollRect;
        ReorderableList reorderableList;

        readonly List<ChildState> states = new();
        readonly List<RectTransform> targets = new();
        readonly List<RectTransform> currentChildrenPool = new();

        // Helpers
        float sigma;
        bool isHovering;
        bool requestedRebuild;
        Vector2 lastPointerLocal;

        struct ChildState
        {
            public RectTransform rt;
            public Vector2 baseSize; // captured from rt.rect.size at Awake
            public Vector2 vel;      // for SmoothDamp per axis (w,h)
        }

        public enum Orientation
        {
            Horizontal,
            Vertical,
            Free
        }

        protected override void Awake()
        {
            base.Awake();

            parentRect = transform as RectTransform;
            canvas = GetComponentInParent<Canvas>();
            eventCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            // ScrollRect integration
            parentScrollRect = GetComponentInParent<ScrollRect>();
            if (parentScrollRect != null) { parentScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged); }

            // ReorderableList integration
            if (TryGetComponent(out reorderableList))
                reorderableList.DockMagnifier = this;

            RefreshTargets();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (parentScrollRect != null)
                parentScrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            isHovering = false;

            // Determine active axes
            bool scaleW = scaleDirection == Orientation.Horizontal || scaleDirection == Orientation.Free;
            bool scaleH = scaleDirection == Orientation.Vertical || scaleDirection == Orientation.Free;

            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];
                if (st.rt != null)
                {
                    // Forcibly reset to the intended "resting" size (baseSize * baseScale)
                    if (scaleW) { SetSize(st.rt, st.baseSize.x * baseScale, RectTransform.Axis.Horizontal); }
                    if (scaleH) { SetSize(st.rt, st.baseSize.y * baseScale, RectTransform.Axis.Vertical); }
                }
            }
        }

        void Update()
        {
            if (states.Count == 0)
                return;

            // If ReorderableList is actively dragging or animating (sliding back to position),
            // disable magnification and force items to relax to their base size.
            bool isReordering = reorderableList != null && (reorderableList.IsDraggingActive || reorderableList.IsAnimating);
            bool performMagnification = isHovering && !isReordering;

            if (performMagnification && alwaysUpdateOnHover)
                TryRefreshPointerFromMouse();

            // Determine active axes for this frame
            bool scaleW = scaleDirection == Orientation.Horizontal || scaleDirection == Orientation.Free;
            bool scaleH = scaleDirection == Orientation.Vertical || scaleDirection == Orientation.Free;

            if (!performMagnification)
            {
                // Relax back to base size
                for (int i = 0; i < states.Count; i++)
                {
                    var st = states[i];
                    if (!st.rt) { continue; }

                    var cur = st.rt.rect.size;
                    bool changed = false;

                    // Only process Width if allowed
                    if (scaleW)
                    {
                        float targetW = st.baseSize.x * baseScale;
                        float nextW = Mathf.SmoothDamp(cur.x, targetW, ref st.vel.x, smoothTime);
                        if (!Mathf.Approximately(nextW, cur.x))
                        {
                            SetSize(st.rt, nextW, RectTransform.Axis.Horizontal);
                            changed = true;
                        }
                    }

                    // Only process Height if allowed
                    if (scaleH)
                    {
                        float targetH = st.baseSize.y * baseScale;
                        float nextH = Mathf.SmoothDamp(cur.y, targetH, ref st.vel.y, smoothTime);
                        if (!Mathf.Approximately(nextH, cur.y))
                        {
                            SetSize(st.rt, nextH, RectTransform.Axis.Vertical);
                            changed = true;
                        }
                    }

                    if (changed)
                        requestedRebuild = true;

                    states[i] = st;
                }

                if (requestedRebuild)
                {
                    requestedRebuild = false;
                    LayoutRebuilder.MarkLayoutForRebuild(parentRect);
                }

                return;
            }

            // Hovering: compute target size for each child based on pointer distance
            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];
                if (!st.rt) { continue; }

                // child center in parent local space
                Vector2 childCenterLocal = WorldToParentLocal(st.rt, st.rt.rect.center);

                Vector2 delta = childCenterLocal - lastPointerLocal;
                float d;
                switch (orientation)
                {
                    case Orientation.Horizontal:
                        delta.x /= Mathf.Max(0.01f, axisBias);
                        d = delta.magnitude;
                        break;

                    case Orientation.Vertical:
                        delta.y /= Mathf.Max(0.01f, axisBias);
                        d = delta.magnitude;
                        break;

                    default:
                        d = delta.magnitude;
                        break;
                }

                // Gaussian falloff -> scale
                float w = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                float targetScale = Mathf.Lerp(baseScale, maxScale, w);

                // Smooth width/height toward target
                var cur = st.rt.rect.size;
                bool changed = false;

                if (scaleW)
                {
                    float targetW = st.baseSize.x * targetScale;
                    float nextW = Mathf.SmoothDamp(cur.x, targetW, ref st.vel.x, smoothTime);
                    if (!Mathf.Approximately(nextW, cur.x))
                    {
                        SetSize(st.rt, nextW, RectTransform.Axis.Horizontal);
                        changed = true;
                    }
                }

                if (scaleH)
                {
                    float targetH = st.baseSize.y * targetScale;
                    float nextH = Mathf.SmoothDamp(cur.y, targetH, ref st.vel.y, smoothTime);
                    if (!Mathf.Approximately(nextH, cur.y))
                    {
                        SetSize(st.rt, nextH, RectTransform.Axis.Vertical);
                        changed = true;
                    }
                }

                if (changed)
                    requestedRebuild = true;

                states[i] = st;
            }

            if (requestedRebuild)
            {
                requestedRebuild = false;
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            UpdatePointerLocal(eventData);
        }

        public void OnPointerExit(PointerEventData eventData) => isHovering = false;

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isHovering)
                return;

            UpdatePointerLocal(eventData);
        }

        /// <summary>
        /// Rescans children to add new items or remove missing ones.
        /// </summary>
        public void RefreshTargets()
        {
            if (parentRect == null)
                return;

            currentChildrenPool.Clear();

            // Identify current valid children
            for (int i = 0; i < parentRect.childCount; i++)
            {
                if (parentRect.GetChild(i) is RectTransform rt && rt != parentRect && !ignoredRects.Contains(rt))
                    currentChildrenPool.Add(rt);
            }

            // Remove states for items that are no longer valid children
            for (int i = states.Count - 1; i >= 0; i--)
            {
                if (states[i].rt == null || !currentChildrenPool.Contains(states[i].rt))
                    states.RemoveAt(i);
            }

            // Update cached targets list
            targets.Clear();

            for (int i = 0; i < states.Count; i++)
                targets.Add(states[i].rt);

            // Add new states for new children
            sigma = Mathf.Max(1f, influenceRadius * 0.5f);
            baseScale = Mathf.Max(0.01f, baseScale);
            maxScale = Mathf.Max(baseScale, maxScale);

            bool scaleW = scaleDirection == Orientation.Horizontal || scaleDirection == Orientation.Free;
            bool scaleH = scaleDirection == Orientation.Vertical || scaleDirection == Orientation.Free;

            for (int i = 0; i < currentChildrenPool.Count; i++)
            {
                var child = currentChildrenPool[i];
                if (!targets.Contains(child))
                {
                    // It's a new item
                    targets.Add(child);
                    var size = child.rect.size;

                    states.Add(new ChildState { rt = child, baseSize = size, vel = Vector2.zero });

                    // Apply initial base scale
                    if (scaleW) { SetSize(child, size.x * baseScale, RectTransform.Axis.Horizontal); }
                    if (scaleH) { SetSize(child, size.y * baseScale, RectTransform.Axis.Vertical); }
                }
            }
        }

        void UpdatePointerLocal(PointerEventData eventData)
        {
            if (!parentRect)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out var local))
                lastPointerLocal = local;
        }

        void TryRefreshPointerFromMouse()
        {
            Vector2 screenPos = Utilities.GetPointerPosition();

            if (requirePointerInsideForRefresh && !RectTransformUtility.RectangleContainsScreenPoint(parentRect, screenPos, eventCamera))
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, eventCamera, out var local))
                lastPointerLocal = local;
        }

        void OnScrollRectValueChanged(Vector2 _)
        {
            if (isHovering && alwaysUpdateOnHover)
            {
                TryRefreshPointerFromMouse();
                requestedRebuild = true;
            }
        }

        void SetSize(RectTransform rt, float size, RectTransform.Axis axis) => rt.SetSizeWithCurrentAnchors(axis, Mathf.Max(0.0001f, size));

        Vector2 WorldToParentLocal(RectTransform child, Vector2 childLocalPoint)
        {
            Vector3 world = child.TransformPoint(childLocalPoint);
            Vector3 localInParent3D = parentRect.InverseTransformPoint(world);
            return (Vector2)localInParent3D;
        }

        /// <summary>
        /// Returns the size the item would be if magnification was at rest (baseScale).
        /// Used by ReorderableList to calculate correct slots even if items are currently magnified.
        /// </summary>
        public Vector2 GetRestingSize(RectTransform rt)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].rt == rt)
                    return states[i].baseSize * baseScale;
            }

            return rt.rect.size;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            sigma = Mathf.Max(1f, influenceRadius * 0.5f);
            baseScale = Mathf.Max(0.01f, baseScale);
            maxScale = Mathf.Max(baseScale, maxScale);
        }
#endif
    }
}