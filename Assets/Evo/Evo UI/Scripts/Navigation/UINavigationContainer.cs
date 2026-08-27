using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "navigation/ui-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation Container")]
    public class UINavigationContainer : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, automatically populates the list with child selectables when enabled.")]
        public bool autoFetchOnEnable = true;

        [Tooltip("If true, navigation will be clamped to objects within this container. You cannot navigate out via keyboard/controller.")]
        public bool restrictNavigation = true;

        [Tooltip("If true, the container will restore focus to the last selected element (or the default selection) " +
                 "if selection becomes null while this container is active. " +
                 "Note: disable this if a UINavigationFallback is already handling global restore, to avoid conflicts.")]
        public bool ensureSelection = false;

        [Header("References")]
        [Tooltip("If set, this object will be selected automatically when the container becomes enabled.")]
        public Selectable defaultSelection;
        [Tooltip("The object to select when this container is disabled, if one of the container's elements was currently selected.")]
        public Selectable fallbackSelection;

        [Header("Data")]
        [Tooltip("The list of objects allowed to be interacted with.")]
        public List<Selectable> interactiveElements = new();

        // Store original navigation data to restore it when this component is disabled
        readonly Dictionary<int, Navigation> originalNavigations = new();

        // Last element within this container that held valid focus
        Selectable lastContainerSelection;

        void OnEnable()
        {
            if (autoFetchOnEnable)
                FetchInteractables();

            if (restrictNavigation)
                StartCoroutine(ApplyNavigationRestrictionsRoutine());

            if (defaultSelection != null && defaultSelection.gameObject.activeInHierarchy)
                StartCoroutine(SelectDefaultRoutine());
        }

        void OnDisable()
        {
            TrySelectFallback();

            if (restrictNavigation)
                RestoreNavigation();

            lastContainerSelection = null;
        }

        void LateUpdate()
        {
            if (!ensureSelection || EventSystem.current == null)
                return;

            GameObject current = EventSystem.current.currentSelectedGameObject;

            if (current != null && current.activeInHierarchy)
            {
                // Keep track of the last element inside our container that was selected
                Selectable sel = current.GetComponent<Selectable>();
                if (sel != null && interactiveElements.Contains(sel)) { lastContainerSelection = sel; }
            }
            else
            {
                // Selection was lost — try to restore within this container
                TryRestoreSelection();
            }
        }

        void TrySelectFallback()
        {
            if (fallbackSelection == null || EventSystem.current == null)
                return;

            if (!fallbackSelection.gameObject.activeInHierarchy || !fallbackSelection.interactable)
                return;

            // Only redirect if the currently selected object belongs to this container
            GameObject currentObj = EventSystem.current.currentSelectedGameObject;
            if (currentObj == null) { return; }

            Selectable currentSel = currentObj.GetComponent<Selectable>();
            if (currentSel != null && interactiveElements.Contains(currentSel))
                EventSystem.current.SetSelectedGameObject(fallbackSelection.gameObject);
        }

        /// <summary>
        /// Restores focus to the last selected container element, or to the default selection.
        /// Called by LateUpdate when ensureSelection is true and focus has been lost.
        /// </summary>
        void TryRestoreSelection()
        {
            // Prefer the exact element that was last focused
            if (IsUsable(lastContainerSelection))
            {
                Utilities.SetSelectedObject(lastContainerSelection.gameObject);
                return;
            }

            // Fall back to the configured default selection
            if (IsUsable(defaultSelection)) { Utilities.SetSelectedObject(defaultSelection.gameObject); }
        }

        public void RestoreNavigation()
        {
            foreach (var sel in interactiveElements)
            {
                if (sel == null)
                    continue;

#if UNITY_6000_4_OR_NEWER
                if (originalNavigations.TryGetValue(sel.GetEntityId().GetHashCode(), out Navigation originalNav)) { sel.navigation = originalNav; }
#else
                if (originalNavigations.TryGetValue(sel.GetInstanceID(), out Navigation originalNav)) { sel.navigation = originalNav; }
#endif
            }
            originalNavigations.Clear();
        }

        public void FetchInteractables()
        {
            interactiveElements.Clear();

            Selectable[] allSelectables = GetComponentsInChildren<Selectable>(true);
            foreach (var sel in allSelectables)
            {
                if (sel.gameObject == this.gameObject || sel.navigation.mode == Navigation.Mode.None || !sel.interactable)
                    continue;

                interactiveElements.Add(sel);
            }
        }

        public void ApplyNavigationRestrictions()
        {
            originalNavigations.Clear();

            foreach (var sel in interactiveElements)
            {
                if (sel == null)
                    continue;

                // Save original state
#if UNITY_6000_4_OR_NEWER
                originalNavigations[sel.GetEntityId().GetHashCode()] = sel.navigation;
#else
                originalNavigations[sel.GetInstanceID()] = sel.navigation;
#endif

                // Build explicit navigation, clamped to elements inside this container
                Navigation newNav = new()
                {
                    mode = Navigation.Mode.Explicit,

                    // Calculate connections
                    // Use the element's existing finding logic to see where it wants to go,
                    // then check if that destination is allowed (inside our list).
                    selectOnUp = ValidateNeighbor(sel.FindSelectableOnUp()),
                    selectOnDown = ValidateNeighbor(sel.FindSelectableOnDown()),
                    selectOnLeft = ValidateNeighbor(sel.FindSelectableOnLeft()),
                    selectOnRight = ValidateNeighbor(sel.FindSelectableOnRight()),
                };

                // Apply
                sel.navigation = newNav;
            }
        }

        IEnumerator SelectDefaultRoutine()
        {
            yield return null; // Wait one frame for EventSystem to be ready
            if (defaultSelection != null) { Utilities.SetSelectedObject(defaultSelection.gameObject); }
        }

        IEnumerator ApplyNavigationRestrictionsRoutine()
        {
            yield return new WaitForEndOfFrame(); // Wait for LayoutGroups to rebuild so FindSelectable works correctly
            ApplyNavigationRestrictions();
        }

        /// <summary>
        /// Returns the potential neighbor only if it is within this container.
        /// </summary>
        Selectable ValidateNeighbor(Selectable potentialNeighbor)
        {
            if (potentialNeighbor == null)
                return null;

            if (interactiveElements.Contains(potentialNeighbor))
                return potentialNeighbor;

            return null;
        }

        /// <summary>
        /// Returns true if the selectable exists, is active, and is interactable.
        /// </summary>
        static bool IsUsable(Selectable selectable) => selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();

#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Fetch Selectables")]
        void ContextFetch()
        {
            FetchInteractables();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Fetched {interactiveElements.Count} interactables.", this);
        }
#endif
    }
}