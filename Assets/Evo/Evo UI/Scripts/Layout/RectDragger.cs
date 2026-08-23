using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "layout/rect-dragger")]
    [AddComponentMenu("Evo/UI/Layout/Rect Dragger")]
    public class RectDragger : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool isDraggable = true;
        public AxisConstraint axisConstraint = AxisConstraint.Free;

        [EvoHeader("Boundary", Constants.CustomEditorID)]
        public BoundaryType boundaryType = BoundaryType.ScreenBounds;
        public RectTransform boundaryRect;

        [EvoHeader("Smooth Return", Constants.CustomEditorID)]
        public bool allowOutOfBounds = false;
        [SerializeField, Min(0f)] private float returnDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("Multiple Sources", Constants.CustomEditorID)]
        public List<RectTransform> dragSources = new();

        // Events
        public Action<Vector2> OnDragUpdate;
        public Action OnDragEnd;

        public enum BoundaryType
        {
            NoBounds,
            ScreenBounds,
            RectBounds
        }

        public enum AxisConstraint
        {
            Free,
            HorizontalOnly,
            VerticalOnly
        }

        // Cache
        Canvas canvas;
        RectTransform rectTransform;
        Coroutine returnCoroutine;
        readonly Vector3[] cachedCorners = new Vector3[4];

        // State
        bool isDragging;
        Vector2 lastMousePosition;
        Vector2 originalPosition;

        void Start()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (boundaryType == BoundaryType.RectBounds && boundaryRect == null && transform.parent != null)
                boundaryRect = transform.parent.GetComponent<RectTransform>();

            if (dragSources != null && dragSources.Count > 0)
            {
                foreach (var target in dragSources)
                {
                    if (target != null && target != rectTransform)
                    {
                        var listener = target.gameObject.AddComponent<DragListener>();
                        listener.Initialize(this);
                    }
                }

                // Disable main component as dragging should be controlled by drag sources
                this.enabled = false;
            }

            originalPosition = rectTransform.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // If targets are assigned, disable direct dragging of this object
            // (Unless this object is explicitly in the list)
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandlePointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandleDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandlePointerUp(eventData);
        }

        public void HandlePointerDown(PointerEventData eventData)
        {
            if (!isDraggable)
                return;

            isDragging = true;

            // Stop any ongoing return animation when we grab the object
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }

            RectTransform parentRect = rectTransform.parent as RectTransform;
            RectTransform referenceRect = parentRect != null ? parentRect : rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                referenceRect,
                eventData.position,
                eventData.pressEventCamera,
                out lastMousePosition
            );
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (!isDraggable || !isDragging)
                return;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            RectTransform referenceRect = parentRect != null ? parentRect : rectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                referenceRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localMousePosition))
            {
                Vector2 deltaPosition = localMousePosition - lastMousePosition;

                // Apply axis constraint to the delta before computing the new position
                deltaPosition = ApplyAxisConstraint(deltaPosition);

                Vector2 newPosition = rectTransform.anchoredPosition + deltaPosition;

                // Only constrain during drag if not allowing to go out of bounds
                if (boundaryType != BoundaryType.NoBounds && !allowOutOfBounds)
                {
                    Vector2 previousPosition = rectTransform.anchoredPosition;
                    newPosition = ConstrainToBounds(newPosition);

                    // If position was clamped, adjust lastMousePosition to match
                    if (newPosition != previousPosition + deltaPosition)
                    {
                        Vector2 actualDelta = newPosition - previousPosition;
                        lastMousePosition = localMousePosition - (deltaPosition - actualDelta);
                    }
                    else
                    {
                        lastMousePosition = localMousePosition;
                    }
                }
                else
                {
                    lastMousePosition = localMousePosition;
                }

                rectTransform.anchoredPosition = newPosition;
                OnDragUpdate?.Invoke(newPosition);
            }
        }

        public void HandlePointerUp(PointerEventData eventData)
        {
            isDragging = false;
            bool isAnimatingReturn = false;

            if (boundaryType != BoundaryType.NoBounds && !allowOutOfBounds)
            {
                rectTransform.anchoredPosition = ConstrainToBounds(rectTransform.anchoredPosition);
            }
            else if (boundaryType != BoundaryType.NoBounds && allowOutOfBounds)
            {
                Vector2 constrainedPosition = ConstrainToBounds(rectTransform.anchoredPosition);
                if (Vector2.Distance(rectTransform.anchoredPosition, constrainedPosition) > 0.1f)
                {
                    if (returnCoroutine != null)
                        StopCoroutine(returnCoroutine);

                    returnCoroutine = StartCoroutine(SmoothReturnToBounds(constrainedPosition));
                    isAnimatingReturn = true;
                }
            }

            // Only fire OnDragEnd immediately if we AREN'T doing a bounce animation.
            // If bouncing, the coroutine below will fire it when it's completely finished.
            if (!isAnimatingReturn)
                OnDragEnd?.Invoke();
        }

        Vector2 ApplyAxisConstraint(Vector2 delta)
        {
            return axisConstraint switch
            {
                AxisConstraint.HorizontalOnly => new Vector2(delta.x, 0f),
                AxisConstraint.VerticalOnly => new Vector2(0f, delta.y),
                _ => delta,
            };
        }

        Vector2 ConstrainToBounds(Vector2 position)
        {
            return boundaryType switch
            {
                BoundaryType.ScreenBounds => ConstrainToScreenBounds(position),
                BoundaryType.RectBounds => ConstrainToRectBounds(position),
                _ => position,
            };
        }

        Vector2 ConstrainToScreenBounds(Vector2 anchoredPos)
        {
            if (canvas == null)
                return anchoredPos;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform parentRect = rectTransform.parent as RectTransform;

            if (canvasRect == null || parentRect == null)
                return anchoredPos;

            // Store current anchored position and set new one to test
            Vector2 originalPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = anchoredPos;

            // Work in local space
            Vector3 localPos = rectTransform.localPosition;

            // Get canvas rect in parent's local space
            Rect canvasRectLocal;

            // Parent is the canvas
            if (canvasRect == parentRect) { canvasRectLocal = canvasRect.rect; }
            else
            {
                canvasRect.GetWorldCorners(cachedCorners);

                Vector2 min = parentRect.InverseTransformPoint(cachedCorners[0]);
                Vector2 max = parentRect.InverseTransformPoint(cachedCorners[2]);

                canvasRectLocal = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Get source rect
            Rect sourceRect = rectTransform.rect;

            // Clamp using the proven logic
            Vector3 minPosition = canvasRectLocal.min - sourceRect.min;
            Vector3 maxPosition = canvasRectLocal.max - sourceRect.max;

            localPos.x = Mathf.Clamp(localPos.x, minPosition.x, maxPosition.x);
            localPos.y = Mathf.Clamp(localPos.y, minPosition.y, maxPosition.y);

            // Apply the clamped local position
            rectTransform.localPosition = localPos;

            // Read back the resulting anchored position
            Vector2 clampedAnchoredPos = rectTransform.anchoredPosition;

            // Restore original position before returning
            rectTransform.anchoredPosition = originalPos;

            return clampedAnchoredPos;
        }

        Vector2 ConstrainToRectBounds(Vector2 anchoredPos)
        {
            if (boundaryRect == null)
                return anchoredPos;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null) { return anchoredPos; }

            // Store current anchored position and set new one to test
            Vector2 originalPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = anchoredPos;

            // Now work in local space (which works consistently across all canvas modes)
            Vector3 localPos = rectTransform.localPosition;

            // Determine the boundary rect in parent's local space
            Rect boundaryRectLocal;

            // Parent is the boundary
            if (boundaryRect == parentRect) { boundaryRectLocal = boundaryRect.rect; }
            else
            {
                // Boundary is somewhere else in hierarchy
                // Transform boundary corners to parent's local space using cached array
                boundaryRect.GetWorldCorners(cachedCorners);

                Vector2 min = parentRect.InverseTransformPoint(cachedCorners[0]);
                Vector2 max = parentRect.InverseTransformPoint(cachedCorners[2]);

                boundaryRectLocal = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Get source rect
            Rect sourceRect = rectTransform.rect;

            // Clamp using the proven WindowDragger logic
            Vector3 minPosition = boundaryRectLocal.min - sourceRect.min;
            Vector3 maxPosition = boundaryRectLocal.max - sourceRect.max;

            localPos.x = Mathf.Clamp(localPos.x, minPosition.x, maxPosition.x);
            localPos.y = Mathf.Clamp(localPos.y, minPosition.y, maxPosition.y);

            // Apply the clamped local position
            rectTransform.localPosition = localPos;

            // Read back the resulting anchored position
            Vector2 clampedAnchoredPos = rectTransform.anchoredPosition;

            // Restore original position before returning
            rectTransform.anchoredPosition = originalPos;

            return clampedAnchoredPos;
        }

        IEnumerator SmoothReturnToBounds(Vector2 targetPosition)
        {
            Vector2 startPosition = rectTransform.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < returnDuration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / returnDuration);
                float curveValue = returnCurve.Evaluate(normalizedTime);

                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);
                OnDragUpdate?.Invoke(rectTransform.anchoredPosition);

                yield return null;
            }

            rectTransform.anchoredPosition = targetPosition;
            OnDragUpdate?.Invoke(rectTransform.anchoredPosition);
            OnDragEnd?.Invoke();
            returnCoroutine = null;
        }

        /// <summary>
        /// Instantly or smoothly resets the transform back to the position it occupied when Start() was called.
        /// </summary>
        public void ResetToOriginalPosition()
        {
            if (allowOutOfBounds && boundaryType != BoundaryType.NoBounds)
            {
                if (returnCoroutine != null) { StopCoroutine(returnCoroutine); }
                returnCoroutine = StartCoroutine(SmoothReturnToBounds(originalPosition));
            }
            else
            {
                rectTransform.anchoredPosition = originalPosition;
                OnDragUpdate?.Invoke(originalPosition);
                OnDragEnd?.Invoke();
            }
        }

        /// <summary>
        /// Stop dragging process manually.
        /// </summary>
        public void StopDrag() => HandlePointerUp(null);

        /// <summary>
        /// Helper class to forward events.
        /// </summary>
        class DragListener : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            RectDragger dragger;

            public void Initialize(RectDragger srcDragger) => dragger = srcDragger;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (dragger != null)
                    dragger.HandlePointerDown(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (dragger != null)
                    dragger.HandleDrag(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (dragger != null)
                    dragger.HandlePointerUp(eventData);
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
#endif
    }
}