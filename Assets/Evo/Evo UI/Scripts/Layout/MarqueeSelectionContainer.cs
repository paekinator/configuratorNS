using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl)]
    [AddComponentMenu("Evo/UI/Layout/Marquee Selection Container")]
    public class MarqueeSelectionContainer : MonoBehaviour
    {
        [EvoHeader("Selection Rectangle", Constants.CustomEditorID)]
        [SerializeField] private Color rectFillColor = new(0.22f, 0.56f, 1f, 0.08f);
        [SerializeField] private Color rectBorderColor = new(0.22f, 0.56f, 1f, 0.75f);
        [SerializeField, Range(0, 10)] private float rectBorderWidth = 1;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool allowAdditiveSelect = true;
        public bool allowDragMove = true;
        public bool clickEmptyToDeselect = true;
        [SerializeField, Min(1)] private float marqueeDeadZone = 4;
        [SerializeField, Min(1)] private float dragMoveDeadZone = 4;
        [Tooltip("On drag end, items can be reparented into detected containers of these types.")]
        [SerializeField] private ReparentTarget reparentTargets = ReparentTarget.None;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        [Tooltip("Fade-out duration for the marquee rectangle. 0 = instant.")]
        [SerializeField, Range(0, 1)] private float rectFadeOutDuration = 0.1f;
        [Tooltip("Smoothing speed for the marquee rectangle. 0 = instant (no smoothing).")]
        [SerializeField, Range(0, 50)] private float rectSmoothing = 0;
        [Tooltip("Duration for the reparent animation. 0 = instant.")]
        [SerializeField, Range(0f, 2f)] private float reparentDuration = 0.3f;
        [Tooltip("Easing curve for the reparent animation.")]
        [SerializeField] private AnimationCurve reparentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("3D", Constants.CustomEditorID)]
        [SerializeField] private bool include3DObjects;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0)] private float maxRaycastDistance = 1000;
        [SerializeField] private LayerMask selectableLayer3D = ~0;

        [Flags]
        public enum ReparentTarget
        {
            None = 0,
            ReorderableList = 1 << 0,
            ScrollRect = 1 << 1,
            LayoutGroup = 1 << 2,
            RectTransform = 1 << 3,
        }

        #region Public Properties
        public IReadOnlyList<MarqueeSelectable> Selectables => selectables;
        public IReadOnlyList<IMarqueeSelectable> Selection => selection;
        public int SelectionCount => selection.Count;
        public bool IsSelecting => mode == Mode.Marquee;
        public bool IsDragging => mode == Mode.Drag;
        public Camera WorldCamera
        {
            get => worldCamera != null ? worldCamera : Camera.main;
            set => worldCamera = value;
        }
        #endregion

        #region Events
        public readonly struct SelectionChangedEvent
        {
            public readonly IReadOnlyList<IMarqueeSelectable> CurrentSelection;
            public readonly IReadOnlyList<IMarqueeSelectable> Added;
            public readonly IReadOnlyList<IMarqueeSelectable> Removed;

            public SelectionChangedEvent(IReadOnlyList<IMarqueeSelectable> current,
                IReadOnlyList<IMarqueeSelectable> added, IReadOnlyList<IMarqueeSelectable> removed)
            {
                CurrentSelection = current;
                Added = added;
                Removed = removed;
            }
        }

        public readonly struct DragCompletedEvent
        {
            public readonly IReadOnlyList<IMarqueeSelectable> DraggedItems;
            public readonly Vector2 TotalDelta;
            public readonly MarqueeSelectionContainer TargetParent;

            public DragCompletedEvent(IReadOnlyList<IMarqueeSelectable> items, Vector2 delta, MarqueeSelectionContainer target)
            {
                DraggedItems = items;
                TotalDelta = delta;
                TargetParent = target;
            }
        }

        public readonly struct ReparentEvent
        {
            public readonly MarqueeSelectable Item;
            public readonly MarqueeSelectionContainer OldParent;
            public readonly MarqueeSelectionContainer NewParent;

            public ReparentEvent(MarqueeSelectable item, MarqueeSelectionContainer oldP, MarqueeSelectionContainer newP)
            {
                Item = item;
                OldParent = oldP;
                NewParent = newP;
            }
        }

        public event Action<SelectionChangedEvent> OnSelectionChanged;
        public event Action<DragCompletedEvent> OnDragCompleted;
        #endregion

        #region Helpers & Cache
        enum Mode { Idle, MarqueePending, Marquee, DragPending, Drag }
        Mode mode = Mode.Idle;

        Vector2 pointerDownPos;
        Vector2 pointerPos;

        readonly List<IMarqueeSelectable> selection = new(32);
        readonly HashSet<IMarqueeSelectable> selectionSet = new(32);
        readonly List<IMarqueeSelectable> hoveredThisFrame = new(32);
        readonly HashSet<IMarqueeSelectable> previousHovered = new(32);

        Vector2 dragLastPos;
        Vector2 dragTotalDelta;

        readonly List<IMarqueeSelectable> tmpAdded = new(16);
        readonly List<IMarqueeSelectable> tmpRemoved = new(16);

        // Visual
        RectTransform overlayParent;
        MarqueeRectGraphic rectGraphic;
        CanvasGroup rectCanvasGroup;
        Coroutine rectFadeCoroutine;

        // Canvas
        Canvas canvas;
        Camera canvasCamera;
        RectTransform canvasRect;

        // Drag raycast management
        readonly List<Graphic> disabledRaycastTargets = new(16);

        // Marquee rect smoothing
        Vector2 smoothedPointerPos;

        // Static state
        static readonly List<MarqueeSelectionContainer> allParents = new(16);
        public static MarqueeSelectionContainer ActiveParent { get; private set; }

        // Per-instance Registry
        readonly List<MarqueeSelectable> selectables = new(64);
        readonly HashSet<MarqueeSelectable> selectablesSet = new(64);
        #endregion

        void Awake()
        {
            SetupVisual();
            Utilities.AddRaycastGraphic(gameObject);
        }

        void OnEnable()
        {
            allParents.Add(this);
            HideRect();
        }

        void OnDisable()
        {
            allParents.Remove(this);
            if (ActiveParent == this) { ReleaseActiveLock(); }
        }

        void OnDestroy()
        {
            if (rectGraphic != null) Destroy(rectGraphic.gameObject);
            if (ActiveParent == this) ActiveParent = null;
        }

        void Update()
        {
            pointerPos = Utilities.GetPointerPosition();

            switch (mode)
            {
                case Mode.Idle: HandleIdle(); break;
                case Mode.MarqueePending: HandleMarqueePending(); break;
                case Mode.Marquee: HandleMarquee(); break;
                case Mode.DragPending: HandleDragPending(); break;
                case Mode.Drag: HandleDrag(); break;
            }
        }

        #region Active Lock
        bool TryAcquireActiveLock()
        {
            if (ActiveParent != null && ActiveParent != this)
                return false;

            ActiveParent = this;

            if (!IsModifierHeld())
            {
                for (int i = 0; i < allParents.Count; i++)
                {
                    var other = allParents[i];
                    if (other != this && other.selection.Count > 0) { other.ClearSelection(); }
                }
            }
            return true;
        }

        void ReleaseActiveLock()
        {
            if (ActiveParent == this)
            {
                ActiveParent = null;
            }
        }
        #endregion

        #region Input Helpers
        bool IsPointerDown() => Utilities.WasPointerPressed();
        bool IsPointerUp() => Utilities.WasPointerReleased();
        bool IsModifierHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
                return false;

            Keyboard kb = Keyboard.current;
            return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ||
                   kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed ||
                   kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
#else
           return false;
#endif
        }
        #endregion

        #region State Machine
        void HandleIdle()
        {
            if (!IsPointerDown()) { return; }
            if (ActiveParent != null && ActiveParent != this) { return; }

            PointerHitResult hit = ClassifyPointerHit(pointerPos);

            switch (hit.Ownership)
            {
                case HitOwnership.ThisParent_Selectable:
                    if (!TryAcquireActiveLock()) { return; }
                    pointerDownPos = pointerPos;

                    // Check requireSelectionToDrag.
                    bool canDrag = allowDragMove && hit.Selectable.IsSelected;
                    if (!canDrag && allowDragMove && hit.Selectable is MarqueeSelectable ms && !ms.requireSelectionToDrag) { canDrag = true; }

                    if (hit.Selectable.IsSelected && canDrag) { mode = Mode.DragPending; }
                    else
                    {
                        if (!(allowAdditiveSelect && IsModifierHeld())) { ClearSelection(); }
                        AddToSelection(hit.Selectable);
                        mode = canDrag ? Mode.DragPending : Mode.Idle;
                    }
                    break;

                case HitOwnership.ThisParent_EmptyArea:
                    if (!TryAcquireActiveLock()) { return; }
                    pointerDownPos = pointerPos;
                    mode = Mode.MarqueePending;
                    break;

                case HitOwnership.OtherParent:
                case HitOwnership.Nothing:
                    if (clickEmptyToDeselect && !IsModifierHeld()) { ClearSelection(); }
                    break;

                case HitOwnership.BlockingUI:
                    break;
            }
        }

        void HandleMarqueePending()
        {
            if (IsPointerUp())
            {
                if (clickEmptyToDeselect && !IsModifierHeld()) { ClearSelection(); }
                ReturnToIdle();
                return;
            }

            if (Vector2.Distance(pointerPos, pointerDownPos) >= marqueeDeadZone)
            {
                if (!IsModifierHeld()) { ClearSelection(); }
                smoothedPointerPos = pointerPos;
                ShowRect();
                mode = Mode.Marquee;
            }
        }

        void HandleMarquee()
        {
            // Smooth the end point of the rect for visual; use real pointer for selection logic
            if (rectSmoothing > 0f) { smoothedPointerPos = Vector2.Lerp(smoothedPointerPos, pointerPos, Time.deltaTime * rectSmoothing); }
            else { smoothedPointerPos = pointerPos; }

            // Visual rect uses smoothed position
            rectGraphic.SetScreenRect(pointerDownPos, smoothedPointerPos, canvasRect, canvasCamera);

            // Selection logic uses the actual pointer position for responsiveness
            Rect selRect = MakeScreenRect(pointerDownPos, pointerPos);
            Camera cam = include3DObjects ? WorldCamera : null;

            hoveredThisFrame.Clear();

            for (int i = 0; i < selectables.Count; i++)
            {
                var s = selectables[i];
                if (s.Interactable && s.IsInsideScreenRect(selRect, cam)) { hoveredThisFrame.Add(s); }
            }

            if (include3DObjects && cam != null)
            {
                var world = MarqueeSelectable.GlobalWorldSelectables;
                for (int i = 0; i < world.Count; i++)
                {
                    var s = world[i];
                    if (s.Interactable && s.IsInsideScreenRect(selRect, cam)) { hoveredThisFrame.Add(s); }
                }
            }

            // Live preview
            foreach (var s in previousHovered)
            {
                if (!hoveredThisFrame.Contains(s) && !selectionSet.Contains(s))
                {
                    s.OnMarqueeDeselect();
                }
            }

            for (int i = 0; i < hoveredThisFrame.Count; i++)
            {
                if (!hoveredThisFrame[i].IsSelected)
                {
                    hoveredThisFrame[i].OnMarqueeSelect();
                }
            }

            previousHovered.Clear();
            for (int i = 0; i < hoveredThisFrame.Count; i++) { previousHovered.Add(hoveredThisFrame[i]); }

            if (!IsPointerUp())
                return;

            // Commit
            bool additive = allowAdditiveSelect && IsModifierHeld();
            tmpAdded.Clear();
            tmpRemoved.Clear();

            if (!additive)
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    var s = selection[i];
                    if (!hoveredThisFrame.Contains(s))
                    {
                        s.OnMarqueeDeselect();
                        tmpRemoved.Add(s);
                    }
                }
                selection.Clear();
                selectionSet.Clear();

                for (int i = 0; i < hoveredThisFrame.Count; i++)
                {
                    var s = hoveredThisFrame[i];
                    if (selectionSet.Add(s))
                    {
                        selection.Add(s);
                        if (!s.IsSelected)
                        {
                            s.OnMarqueeSelect();
                            tmpAdded.Add(s);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < hoveredThisFrame.Count; i++)
                {
                    var s = hoveredThisFrame[i];
                    if (selectionSet.Add(s))
                    {
                        selection.Add(s);
                        s.OnMarqueeSelect();
                        tmpAdded.Add(s);
                    }
                }
            }

            foreach (var s in previousHovered)
            {
                if (!selectionSet.Contains(s))
                {
                    s.OnMarqueeDeselect();
                }
            }
            previousHovered.Clear();

            HideRect();
            ReturnToIdle();

            if (tmpAdded.Count > 0 || tmpRemoved.Count > 0) { OnSelectionChanged?.Invoke(new SelectionChangedEvent(selection, tmpAdded, tmpRemoved)); }
        }

        void HandleDragPending()
        {
            if (IsPointerUp())
            {
                var hit = ClassifyPointerHit(pointerDownPos);
                bool additive = allowAdditiveSelect && IsModifierHeld();

                if (additive && hit.Selectable != null && hit.Selectable.IsSelected) { RemoveFromSelection(hit.Selectable); }
                else if (!additive && hit.Selectable != null)
                {
                    ClearSelection();
                    AddToSelection(hit.Selectable);
                }
                ReturnToIdle();
                return;
            }

            if (Vector2.Distance(pointerPos, pointerDownPos) >= dragMoveDeadZone)
            {
                dragLastPos = pointerPos;
                dragTotalDelta = Vector2.zero;
                DisableDraggedRaycasts();
                mode = Mode.Drag;
            }
        }

        void HandleDrag()
        {
            Vector2 delta = pointerPos - dragLastPos;
            dragLastPos = pointerPos;
            dragTotalDelta += delta;

            for (int i = 0; i < selection.Count; i++) { MoveSelectable(selection[i], delta); }
            if (!IsPointerUp()) { return; }

            // Detect reparent target BEFORE restoring raycasts (so dragged items don't block).
            Transform dropTarget = (reparentTargets != ReparentTarget.None)
                ? FindReparentTarget(pointerPos)
                : null;

            RestoreDraggedRaycasts();
            if (dropTarget != null) { ReparentSelectionTo(dropTarget); }

            OnDragCompleted?.Invoke(new DragCompletedEvent(selection, dragTotalDelta, null));
            ReturnToIdle();
        }

        void ReturnToIdle()
        {
            mode = Mode.Idle;
            ReleaseActiveLock();
        }

        void MoveSelectable(IMarqueeSelectable s, Vector2 delta)
        {
            if (s.Transform is RectTransform rt)
            {
                RectTransform parent = rt.parent as RectTransform ?? overlayParent;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent, pointerPos, canvasCamera, out Vector2 localNow) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent, pointerPos - delta, canvasCamera, out Vector2 localPrev))
                {
                    rt.anchoredPosition += localNow - localPrev;
                }
            }
            else
            {
                Camera cam = WorldCamera;
                if (cam != null)
                {
                    float depth = cam.WorldToScreenPoint(s.Transform.position).z;
                    Vector3 wNow = cam.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, depth));
                    Vector3 wPrev = cam.ScreenToWorldPoint(new Vector3((pointerPos - delta).x, (pointerPos - delta).y, depth));
                    s.Transform.position += wNow - wPrev;
                }
            }
        }
        #endregion

        #region Raycast Management
        void DisableDraggedRaycasts()
        {
            disabledRaycastTargets.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i].Transform == null)
                    continue;

                var graphics = selection[i].Transform.GetComponentsInChildren<Graphic>(true);
                for (int j = 0; j < graphics.Length; j++)
                {
                    if (graphics[j].raycastTarget)
                    {
                        graphics[j].raycastTarget = false;
                        disabledRaycastTargets.Add(graphics[j]);
                    }
                }
            }
        }

        void RestoreDraggedRaycasts()
        {
            for (int i = 0; i < disabledRaycastTargets.Count; i++)
            {
                if (disabledRaycastTargets[i] != null)
                {
                    disabledRaycastTargets[i].raycastTarget = true;
                }
            }
            disabledRaycastTargets.Clear();
        }
        #endregion

        #region Reparenting
        /// <summary>
        /// Raycasts from the pointer and finds the first matching reparent target based on the enabled ReparentTarget flags.
        /// Returns the Transform to parent items under, or null.
        /// </summary>
        Transform FindReparentTarget(Vector2 screenPos)
        {
            if (EventSystem.current == null)
                return null;

            var results = new List<RaycastResult>(8);
            var pData = new PointerEventData(EventSystem.current) { position = screenPos };
            EventSystem.current.RaycastAll(pData, results);

            for (int i = 0; i < results.Count; i++)
            {
                GameObject go = results[i].gameObject;

                // Skip hits on ourselves.
                var hitParent = go.GetComponentInParent<MarqueeSelectionContainer>();
                if (hitParent == this) { return null; }

                // Check each enabled target type in priority order.
                if ((reparentTargets & ReparentTarget.ReorderableList) != 0)
                {
                    var list = go.GetComponentInParent<ReorderableList>();
                    if (list != null) { return list.transform; }
                }

                if ((reparentTargets & ReparentTarget.ScrollRect) != 0)
                {
                    // Check for ScrollRect (Evo.UI or UnityEngine.UI)
                    var unityScroll = go.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                    if (unityScroll != null) { return unityScroll.content != null ? unityScroll.content : (Transform)unityScroll.viewport; }
                }

                if ((reparentTargets & ReparentTarget.LayoutGroup) != 0)
                {
                    var layout = go.GetComponentInParent<LayoutGroup>();
                    if (layout != null) { return layout.transform; }
                }

                if ((reparentTargets & ReparentTarget.RectTransform) != 0)
                {
                    var rt = go.GetComponentInParent<RectTransform>();
                    if (rt != null) { return rt; }
                }
            }

            return null;
        }

        void ReparentSelectionTo(Transform dropTarget)
        {
            var targetList = dropTarget.GetComponent<ReorderableList>();

            // Collect items and capture their world positions before anything moves.
            var worldPositions = new List<Vector3>();
            var rects = new List<RectTransform>();
            var movedSelectables = new List<MarqueeSelectable>();

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                if (selection[i] is not MarqueeSelectable ms || ms.selectableType == MarqueeSelectable.SelectableType.World) { continue; }
                if (!ms.TryGetComponent<RectTransform>(out var rt)) { continue; }

                worldPositions.Add(rt.position);
                rects.Add(rt);
                movedSelectables.Add(ms);

                ms.OnMarqueeDeselect();
                selectionSet.Remove(ms);
                selection.RemoveAt(i);
                if (ms.CurrentParent != null) { ms.CurrentParent.UnregisterSelectable(ms); }
            }

            if (rects.Count == 0)
                return;

            if (targetList != null) { ReparentToReorderableList(targetList, rects, worldPositions); }
            else { ReparentToTransform(dropTarget, rects, worldPositions); }

            // Re-register each selectable with whichever container it now lives under.
            for (int i = 0; i < movedSelectables.Count; i++)
            {
                if (movedSelectables[i] != null) { movedSelectables[i].FindAndRegisterParent(); }
            }
        }

        void ReparentToReorderableList(ReorderableList targetList, List<RectTransform> rects, List<Vector3> worldPositions)
        {
            // AddExistingItems: SetParent(listContainer, false) -> localPos reset to 0,
            // RefreshItemsList -> DelayedLayoutRefresh (end of frame) -> AnimateToNormalPositions.
            // AnimateToNormalPositions lerps from current localPosition to computed slot.
            // Set localPosition to the entry point so it animates from there.

            targetList.AddExistingItems(rects);

            // Items are now children of listContainer. Set each item's localPosition
            // to its world-space entry point so the list's SmoothMove animates from there.
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i] == null || rects[i].parent == null)
                    continue;

                RectTransform container = rects[i].parent as RectTransform;
                if (container != null)
                {
                    Vector3 localEntry = container.InverseTransformPoint(worldPositions[i]);
                    rects[i].localPosition = localEntry;
                }
            }

            // The list's own AnimateToNormalPositions uses its animationDuration and animationCurve
            // which are configured on the ReorderableList inspector.
            // If the user also wants our reparentDuration to apply, override the list's animation
            // by running our own coroutine that waits for the list's layout computation,
            // captures final positions, and animates with our settings.
            if (reparentDuration > 0f) { StartCoroutine(OverrideListAnimation(targetList, rects, worldPositions)); }
        }

        /// <summary>
        /// Takes over the ReorderableList animation. Waits for the list to compute
        /// layout target positions, then animates items from entry to final ourselves.
        /// </summary>
        IEnumerator OverrideListAnimation(ReorderableList targetList, List<RectTransform> rects, List<Vector3> worldPositions)
        {
            // Capture entry positions (already set by ReparentToReorderableList)
            var entryPositions = new Vector3[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i] != null)
                {
                    entryPositions[i] = rects[i].localPosition;
                }
            }

            // Wait for the list's DelayedLayoutRefresh to fire and then one more frame for AnimateToNormalPositions to compute targets
            yield return new WaitForEndOfFrame();
            yield return null;

            // The list's AnimateToNormalPositions has now started SmoothMove coroutines.
            // Those coroutines are lerping from our entry positions to the layout targets.
            // We need the final target positions. The list computes them internally.
            // Since items may have been moved by SmoothMove already (one frame in),
            // we stop the list's layout animation and capture the targets by forcing a rebuild.

            // Tell the list to snap to final layout (stops its animations).
            targetList.RefreshItemsList();

            // Wait for that refresh to settle.
            yield return new WaitForEndOfFrame();
            yield return null;

            // Now items are at their final layout positions. Capture them.
            var finalPositions = new Vector3[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i] != null)
                {
                    finalPositions[i] = rects[i].localPosition;
                }
            }

            // Snap back to entry and animate ourselves
            float elapsed = 0f;
            while (elapsed < reparentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = reparentCurve.Evaluate(Mathf.Clamp01(elapsed / reparentDuration));
                for (int i = 0; i < rects.Count; i++)
                {
                    if (rects[i] == null) { continue; }
                    rects[i].localPosition = Vector3.LerpUnclamped(entryPositions[i], finalPositions[i], t);
                }
                yield return null;
            }

            // Snap to final.
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i] != null)
                {
                    rects[i].localPosition = finalPositions[i];
                }
            }

            // Let the list re-enable its layout.
            targetList.RefreshLayout();
        }

        void ReparentToTransform(Transform dropTarget, List<RectTransform> rects, List<Vector3> worldPositions)
        {
            // Disable LayoutGroup before reparenting so it never gets a chance to snap items to computed positions.
            var targetRect = dropTarget as RectTransform;
            LayoutGroup layout = targetRect != null ? targetRect.GetComponent<LayoutGroup>() : null;

            bool layoutWasEnabled = layout != null && layout.enabled;
            if (layoutWasEnabled) { layout.enabled = false; }

            // Capture pre-layout positions of existing children so they can animate
            // instead of snapping when the layout shifts (e.g. center/right alignment).
            var existingChildren = new List<RectTransform>();
            var existingPrePositions = new Dictionary<RectTransform, Vector3>();
            if (targetRect != null)
            {
                for (int i = 0; i < targetRect.childCount; i++)
                {
                    if (targetRect.GetChild(i) is RectTransform childRT && childRT.gameObject.activeInHierarchy)
                    {
                        existingChildren.Add(childRT);
                        existingPrePositions[childRT] = childRT.localPosition;
                    }
                }
            }

            for (int i = 0; i < rects.Count; i++)
            {
                var rt = rects[i];
                if (rt == null) { continue; }

                // Capture size before reparenting.
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                Vector2 worldSize = new(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));
                rt.SetParent(dropTarget, true);

                if (targetRect == null)
                    continue;

                Vector3 entryLocalPos = targetRect.InverseTransformPoint(worldPositions[i]);

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = worldSize / GetCanvasScale();
                rt.localPosition = entryLocalPos;
            }

            // Compute final layout positions synchronously without ever rendering at those positions.
            // This repositions ALL children (existing + dropped).
            Vector3[] finalPositions = null;
            Dictionary<RectTransform, Vector3> existingFinalPositions = null;
            if (layout != null && targetRect != null)
            {
                layout.enabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetRect);
                layout.enabled = false;

                finalPositions = new Vector3[rects.Count];
                for (int i = 0; i < rects.Count; i++)
                {
                    if (rects[i] != null)
                    {
                        finalPositions[i] = rects[i].localPosition;
                    }
                }

                // Capture final positions for existing children and restore them to pre-layout positions.
                existingFinalPositions = new Dictionary<RectTransform, Vector3>(existingChildren.Count);
                for (int i = 0; i < existingChildren.Count; i++)
                {
                    var child = existingChildren[i];
                    if (child == null) { continue; }
                    existingFinalPositions[child] = child.localPosition;

                    // Restore so animation starts from where the item visually was.
                    if (existingPrePositions.TryGetValue(child, out Vector3 prePos))
                    {
                        child.localPosition = prePos;
                    }
                }
            }

            // Animate or snap
            if (reparentDuration > 0f && finalPositions != null)
            {
                // Snap dropped items back to entry, start animation.
                var entryPositions = new Vector3[rects.Count];
                for (int i = 0; i < rects.Count; i++)
                {
                    if (rects[i] != null && targetRect != null)
                    {
                        entryPositions[i] = targetRect.InverseTransformPoint(worldPositions[i]);
                        rects[i].localPosition = entryPositions[i];
                    }
                }
                StartCoroutine(AnimateReparentBatch(rects, entryPositions, finalPositions,
                    existingChildren, existingPrePositions, existingFinalPositions, layout));
            }
            else if (layoutWasEnabled && layout != null)
            {
                // No animation, just re-enable layout
                layout.enabled = true;
            }
        }

        IEnumerator AnimateReparentBatch(
            List<RectTransform> rects, Vector3[] entryPositions, Vector3[] finalPositions,
            List<RectTransform> existingChildren, Dictionary<RectTransform, Vector3> existingPrePositions,
            Dictionary<RectTransform, Vector3> existingFinalPositions, LayoutGroup layout)
        {
            float elapsed = 0f;
            while (elapsed < reparentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = reparentCurve.Evaluate(Mathf.Clamp01(elapsed / reparentDuration));

                // Animate dropped items
                for (int i = 0; i < rects.Count; i++)
                {
                    if (rects[i] == null) { continue; }
                    rects[i].localPosition = Vector3.LerpUnclamped(entryPositions[i], finalPositions[i], t);
                }

                // Animate existing children that shifted
                if (existingFinalPositions != null)
                {
                    for (int i = 0; i < existingChildren.Count; i++)
                    {
                        var child = existingChildren[i];
                        if (child == null) { continue; }
                        if (!existingPrePositions.TryGetValue(child, out Vector3 from)) { continue; }
                        if (!existingFinalPositions.TryGetValue(child, out Vector3 to)) { continue; }
                        child.localPosition = Vector3.LerpUnclamped(from, to, t);
                    }
                }

                yield return null;
            }

            // Snap everything to final
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i] != null)
                {
                    rects[i].localPosition = finalPositions[i];
                }
            }

            if (existingFinalPositions != null)
            {
                for (int i = 0; i < existingChildren.Count; i++)
                {
                    var child = existingChildren[i];
                    if (child != null && existingFinalPositions.TryGetValue(child, out Vector3 to))
                    {
                        child.localPosition = to;
                    }
                }
            }

            if (layout != null) { layout.enabled = true; }
        }

        float GetCanvasScale()
        {
            if (canvas != null) { return Mathf.Max(canvas.transform.lossyScale.x, 0.001f); }
            return 1f;
        }
        #endregion

        #region Pointer Classification
        enum HitOwnership { Nothing, ThisParent_Selectable, ThisParent_EmptyArea, OtherParent, BlockingUI }

        struct PointerHitResult
        {
            public HitOwnership Ownership;
            public IMarqueeSelectable Selectable;
        }

        PointerHitResult ClassifyPointerHit(Vector2 screenPos)
        {
            var result = new PointerHitResult { Ownership = HitOwnership.Nothing };
            if (EventSystem.current == null) { return result; }

            var rayResults = new List<RaycastResult>(8);
            var pData = new PointerEventData(EventSystem.current) { position = screenPos };
            EventSystem.current.RaycastAll(pData, rayResults);

            for (int i = 0; i < rayResults.Count; i++)
            {
                GameObject go = rayResults[i].gameObject;

                var selectable = go.GetComponentInParent<MarqueeSelectable>();
                if (selectable != null)
                {
                    if (selectable.CurrentParent == this && selectable.Interactable)
                    {
                        result.Ownership = HitOwnership.ThisParent_Selectable;
                        result.Selectable = selectable;
                        return result;
                    }
                    result.Ownership = HitOwnership.OtherParent;
                    return result;
                }

                var hitParent = go.GetComponentInParent<MarqueeSelectionContainer>();
                if (hitParent != null)
                {
                    result.Ownership = hitParent == this ? HitOwnership.ThisParent_EmptyArea : HitOwnership.OtherParent;
                    return result;
                }

                if (go.GetComponentInParent<Selectable>() != null)
                {
                    result.Ownership = HitOwnership.BlockingUI;
                    return result;
                }
            }

            // 3D fallback
            if (include3DObjects)
            {
                Camera cam = WorldCamera;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(screenPos);
                    if (Physics.Raycast(ray, out RaycastHit hit3d, maxRaycastDistance, selectableLayer3D))
                    {
                        var sel = hit3d.collider.GetComponentInParent<MarqueeSelectable>();
                        if (sel != null && sel.selectableType == MarqueeSelectable.SelectableType.World && sel.Interactable)
                        {
                            result.Ownership = HitOwnership.ThisParent_Selectable;
                            result.Selectable = sel;
                            return result;
                        }
                    }
                }
            }

            return result;
        }
        #endregion

        #region Selection Helpers
        internal void AddToSelection(IMarqueeSelectable s)
        {
            if (!selectionSet.Add(s))
                return;

            selection.Add(s);
            s.OnMarqueeSelect();
            FireSelectionChanged(new[] { s }, true);
        }

        void RemoveFromSelection(IMarqueeSelectable s)
        {
            if (!selectionSet.Remove(s))
                return;

            selection.Remove(s);
            s.OnMarqueeDeselect();
            FireSelectionChanged(new[] { s }, false);
        }

        void FireSelectionChanged(IReadOnlyList<IMarqueeSelectable> changed, bool added)
        {
            OnSelectionChanged?.Invoke(new SelectionChangedEvent(
                selection,
                added ? changed : Array.Empty<IMarqueeSelectable>(),
                !added ? changed : Array.Empty<IMarqueeSelectable>()));
        }
        #endregion

        #region Utilities
        static Rect MakeScreenRect(Vector2 a, Vector2 b)
        {
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        #endregion

        #region Visual
        void SetupVisual()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) { canvas = canvas.rootCanvas; }

            if (canvas == null)
            {
                Debug.LogError("[Marquee Selection Container] Must be placed under a Canvas.", this);
                enabled = false;
                return;
            }

            canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            canvasRect = canvas.GetComponent<RectTransform>();
            overlayParent = canvasRect;

            var rectGo = new GameObject($"MarqueeRect_{gameObject.name}", typeof(RectTransform), typeof(CanvasRenderer));
            rectGo.transform.SetParent(canvasRect, false);

            var rt = rectGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            rectGraphic = rectGo.AddComponent<MarqueeRectGraphic>();
            rectGraphic.raycastTarget = false;
            rectGraphic.FillColor = rectFillColor;
            rectGraphic.BorderColor = rectBorderColor;
            rectGraphic.BorderWidth = rectBorderWidth;

            rectCanvasGroup = rectGo.AddComponent<CanvasGroup>();
            rectCanvasGroup.alpha = 0f;
            rectCanvasGroup.blocksRaycasts = false;
            rectCanvasGroup.interactable = false;

            rectGraphic.Hide();
        }

        void ShowRect()
        {
            if (rectGraphic == null)
                return;

            StopRectFade();

            rectGraphic.gameObject.SetActive(true);
            rectGraphic.transform.SetAsLastSibling();
            if (rectCanvasGroup != null) { rectCanvasGroup.alpha = 1f; }
        }

        void HideRect()
        {
            if (rectGraphic == null)
                return;

            if (rectFadeOutDuration > 0f && Application.isPlaying && gameObject.activeInHierarchy) { rectFadeCoroutine = StartCoroutine(FadeRectOut()); }
            else
            {
                StopRectFade();

                if (rectCanvasGroup != null) { rectCanvasGroup.alpha = 0f; }
                rectGraphic.rectTransform.sizeDelta = Vector2.zero;
                rectGraphic.gameObject.SetActive(false);
            }
        }

        void StopRectFade()
        {
            if (rectFadeCoroutine != null)
            {
                StopCoroutine(rectFadeCoroutine);
                rectFadeCoroutine = null;
            }
        }

        IEnumerator FadeRectOut()
        {
            float startAlpha = rectCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < rectFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                rectCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / rectFadeOutDuration));
                yield return null;
            }

            rectCanvasGroup.alpha = 0f;
            rectGraphic.rectTransform.sizeDelta = Vector2.zero;
            rectGraphic.gameObject.SetActive(false);
            rectFadeCoroutine = null;
        }
        #endregion

        #region Public Methods
        public void RegisterSelectable(MarqueeSelectable s)
        {
            if (s != null && selectablesSet.Add(s))
            {
                selectables.Add(s);
            }
        }

        public void UnregisterSelectable(MarqueeSelectable s)
        {
            if (s != null && selectablesSet.Remove(s))
            {
                selectables.Remove(s);
            }
        }

        public void SelectAll()
        {
            tmpAdded.Clear();

            for (int i = 0; i < selectables.Count; i++)
            {
                var s = selectables[i];
                if (s.Interactable && selectionSet.Add(s))
                {
                    selection.Add(s);
                    s.OnMarqueeSelect();
                    tmpAdded.Add(s);
                }
            }

            if (include3DObjects)
            {
                var world = MarqueeSelectable.GlobalWorldSelectables;
                for (int i = 0; i < world.Count; i++)
                {
                    var s = world[i];
                    if (s.Interactable && selectionSet.Add(s))
                    {
                        selection.Add(s);
                        s.OnMarqueeSelect();
                        tmpAdded.Add(s);
                    }
                }
            }

            if (tmpAdded.Count > 0) FireSelectionChanged(tmpAdded, true);
        }

        public void ClearSelection()
        {
            if (selection.Count == 0)
                return;

            tmpRemoved.Clear();
            tmpRemoved.AddRange(selection);

            for (int i = 0; i < selection.Count; i++)
            {
                selection[i].OnMarqueeDeselect();
            }

            selection.Clear();
            selectionSet.Clear();

            FireSelectionChanged(tmpRemoved, false);
        }

        public void Select(IMarqueeSelectable item) => AddToSelection(item);

        public void Deselect(IMarqueeSelectable item) => RemoveFromSelection(item);
        #endregion

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool styleFoldout = true;
#endif
    }

    [AddComponentMenu("")]
    public class MarqueeRectGraphic : Graphic
    {
        [NonSerialized] public Color FillColor = new(0.22f, 0.56f, 0.94f, 0.18f);
        [NonSerialized] public Color BorderColor = new(0.22f, 0.56f, 0.94f, 0.75f);
        [NonSerialized] public float BorderWidth = 1.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var r = GetPixelAdjustedRect();
            if (r.width < 0.1f || r.height < 0.1f) { return; }

            float x0 = r.xMin, y0 = r.yMin, x1 = r.xMax, y1 = r.yMax;
            AddQuad(vh, x0, y0, x1, y1, FillColor);

            float bw = Mathf.Min(BorderWidth, r.width * 0.5f, r.height * 0.5f);
            AddQuad(vh, x0, y0, x1, y0 + bw, BorderColor);
            AddQuad(vh, x0, y1 - bw, x1, y1, BorderColor);
            AddQuad(vh, x0, y0 + bw, x0 + bw, y1 - bw, BorderColor);
            AddQuad(vh, x1 - bw, y0 + bw, x1, y1 - bw, BorderColor);
        }

        static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            int idx = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = new Vector3(x0, y0); vh.AddVert(v);
            v.position = new Vector3(x0, y1); vh.AddVert(v);
            v.position = new Vector3(x1, y1); vh.AddVert(v);
            v.position = new Vector3(x1, y0); vh.AddVert(v);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }

        public void SetScreenRect(Vector2 start, Vector2 end, RectTransform parent, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, start, cam, out Vector2 ls);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, end, cam, out Vector2 le);

            Vector2 min = Vector2.Min(ls, le);
            Vector2 size = new(Mathf.Abs(le.x - ls.x), Mathf.Abs(le.y - ls.y));

            rectTransform.localPosition = new Vector3(min.x, min.y, 0f);
            rectTransform.sizeDelta = size;
            SetVerticesDirty();
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide()
        {
            rectTransform.sizeDelta = Vector2.zero;
            gameObject.SetActive(false);
        }
    }
}