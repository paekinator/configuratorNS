using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "layout/pages")]
    [AddComponentMenu("Evo/UI/Layout/Nested Scroll Manager")]
    [RequireComponent(typeof(UnityEngine.UI.ScrollRect))]
    public class NestedScrollManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Settings")]
        [Tooltip("When true, automatically finds the Pages component in parent parents.")]
        public bool findParentAutomatically = true;
        public Pages parentPages;

        // Helpers
        UnityEngine.UI.ScrollRect innerScrollRect;
        bool routeToParent = false;

        void Awake()
        {
            TryGetComponent(out innerScrollRect);

            if (findParentAutomatically && parentPages == null)
                parentPages = GetComponentInParent<Pages>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (parentPages == null || innerScrollRect == null)
                return;

            // Calculate the drag angle to determine intent
            bool isHorizontalDrag = Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y);
            bool isVerticalDrag = !isHorizontalDrag;

            // Check the configuration of the parent and the child
            bool parentIsHorizontal = parentPages.swipeDirection == Pages.SwipeDirection.Horizontal;
            bool childIsVertical = innerScrollRect.vertical;
            bool childIsHorizontal = innerScrollRect.horizontal;

            // Decide who gets the event.
            // If the drag direction matches the Parent's direction, and the child isn't set up to handle that direction.
            // Route the event to the parent.
            if (parentIsHorizontal && isHorizontalDrag && childIsVertical) { StartParentDrag(eventData); }
            else if (!parentIsHorizontal && isVerticalDrag && childIsHorizontal) { StartParentDrag(eventData); }
            else { routeToParent = false; } // Let the ScrollRect handle this normally
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (routeToParent && parentPages != null)
                parentPages.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (routeToParent && parentPages != null)
            {
                parentPages.OnEndDrag(eventData);

                if (innerScrollRect != null && !innerScrollRect.enabled)
                    innerScrollRect.enabled = true;

                routeToParent = false;
            }
        }

        void StartParentDrag(PointerEventData eventData)
        {
            routeToParent = true;

            // Disable the inner scroll rect temporarily so it doesn't fight for control or move diagonally while we swipe pages
            if (innerScrollRect != null && innerScrollRect.enabled)
                innerScrollRect.enabled = false;

            // Manually trigger the parent's drag start
            parentPages.OnBeginDrag(eventData);
        }
    }
}