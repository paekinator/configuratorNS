using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "navigation/ui-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation Fallback")]
    public class UINavigationFallback : MonoBehaviour
    {
        [Header("Settings")]
        public FallbackMode fallbackMode = FallbackMode.Nearest;

        [Header("References")]
        public Selectable specifiedSelectable;

        public enum FallbackMode
        {
            FirstActive = 0,
            Nearest = 1,
            Specified = 2,
            /// <summary>
            /// Restores the last valid selection if it is still active and interactable.
            /// Falls back to Nearest if the last selection is no longer valid.
            /// </summary>
            RestoreLast = 3,
        }

        // Cache
        EventSystem eventSystem;
        GameObject lastSelected;
        Vector2 lastSelectedScreenPosition;

        void Start() => eventSystem = EventSystem.current;

        void LateUpdate()
        {
            // Skip update if using Specified mode
            if (fallbackMode == FallbackMode.Specified)
                return;

            GameObject current = eventSystem.currentSelectedGameObject;

            if (current != null && current.activeInHierarchy)
            {
                lastSelected = current;
                lastSelectedScreenPosition = lastSelected.transform.position;
            }

            if (eventSystem.currentSelectedGameObject == null || !eventSystem.currentSelectedGameObject.activeInHierarchy)
                SelectFallback();
        }

        void OnDisable()
        {
            if (fallbackMode != FallbackMode.Specified)
                return;

            if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
                SelectSpecifiedFallback();
        }

        void SelectFallback()
        {
            Selectable toSelect;

            if (fallbackMode == FallbackMode.RestoreLast) { toSelect = FindRestoreLast(); }
            else if (fallbackMode == FallbackMode.Nearest) { toSelect = FindNearestSelectable(); }
            else { toSelect = FindFirstActiveSelectable(); }

            // Select the object
            if (toSelect != null) { toSelect.Select(); }
        }

        void SelectSpecifiedFallback()
        {
            if (IsUsable(specifiedSelectable)) { specifiedSelectable.Select(); }
            else
            {
                // Fallback to finding any active selectable if specified is unavailable
                Selectable toSelect = FindFirstActiveSelectable();
                if (toSelect != null) { toSelect.Select(); }
            }
        }

        /// <summary>
        /// Tries to re-select the exact object that was selected before focus was lost.
        /// Falls back to nearest if the last object is no longer usable.
        /// </summary>
        Selectable FindRestoreLast()
        {
            if (lastSelected != null)
            {
                Selectable sel = lastSelected.GetComponent<Selectable>();
                if (IsUsable(sel)) { return sel; }
            }

            return FindNearestSelectable();
        }

        Selectable FindNearestSelectable()
        {
            Selectable nearest = null;
            float minDistance = float.MaxValue;

            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (selectable != null &&
                    selectable.gameObject != lastSelected &&
                    selectable.gameObject.activeInHierarchy &&
                    selectable.IsInteractable() &&
                    IsNavigable(selectable))
                {
                    Vector2 selectablePos = selectable.transform.position;
                    float distance = Vector2.Distance(lastSelectedScreenPosition, selectablePos);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = selectable;
                    }
                }
            }

            return nearest;
        }

        Selectable FindFirstActiveSelectable()
        {
            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (selectable != null &&
                    selectable.gameObject.activeInHierarchy &&
                    selectable.IsInteractable() &&
                    IsNavigable(selectable))
                {
                    return selectable;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true if the selectable exists, is active, interactable, and navigable.
        /// </summary>
        static bool IsUsable(Selectable selectable)
        {
            return selectable != null
                && selectable.gameObject.activeInHierarchy
                && selectable.IsInteractable()
                && selectable.navigation.mode != Navigation.Mode.None;
        }

        bool IsNavigable(Selectable selectable) => selectable.navigation.mode != Navigation.Mode.None;
    }
}