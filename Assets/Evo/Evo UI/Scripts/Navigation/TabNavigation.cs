using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "navigation/tab-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/Tab Navigation")]
    public class TabNavigation : MonoBehaviour
    {
        [Header("Tab Settings")]
        [Tooltip("Jumps to the first available object after cycling.")]
        public bool wrapAround = true;
        [SerializeField] private bool autoFindTabbables = true;
        [SerializeField] private bool selectFirstAtStart = false;

        [Header("Manual Tab Order")]
        public List<Selectable> customTabOrder = new();

        // Helpers
        int currentIndex = -1;
        readonly List<Selectable> tabbableElements = new();

        void Start()
        {
            RefreshTabbableElements();

            // Select first element if none is selected
            if (selectFirstAtStart && EventSystem.current.currentSelectedGameObject == null && tabbableElements.Count > 0)
                SelectElement(0);
        }

        void Update()
        {
            HandleTabInput();
            UpdateCurrentIndex();
        }

        void HandleTabInput()
        {
            if (Utilities.HandleTabNavigation(out bool reverseTab))
                TabToNext(reverseTab);
        }

        void SelectElement(int index)
        {
            if (index < 0 || index >= tabbableElements.Count)
                return;

            Selectable element = tabbableElements[index];

            if (element != null && element.IsInteractable())
            {
                element.Select();
                currentIndex = index;
            }

            else
            {
                // If element is not interactable, try the next one
                int nextIndex = CalculateNextIndex(index >= currentIndex);
                if (nextIndex != index) { SelectElement(nextIndex); } // Prevent infinite loop
            }
        }

        void UpdateCurrentIndex()
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.TryGetComponent<Selectable>(out var selectable))
            {
                int index = tabbableElements.IndexOf(selectable);
                if (index >= 0) { currentIndex = index; }
            }
        }

        void FindTabbableElements()
        {
            // Find all TabbableElement components in children first (highest priority)
            TabbableElement[] customTabbables = GetComponentsInChildren<TabbableElement>();
            List<TabbableElement> sortedCustom = new(customTabbables);
            sortedCustom.Sort((a, b) => a.tabIndex.CompareTo(b.tabIndex));

            foreach (var tabbable in sortedCustom)
            {
                if (tabbable.Selectable != null && ShouldIncludeInTabOrder(tabbable.Selectable))
                    tabbableElements.Add(tabbable.Selectable);
            }

            // Then find other selectables in children that don't have TabbableElement component
            Selectable[] allSelectables = GetComponentsInChildren<Selectable>();
            List<Selectable> autoSelectables = new();

            foreach (var selectable in allSelectables)
            {
                if (selectable.GetComponent<TabbableElement>() == null && ShouldIncludeInTabOrder(selectable))
                    autoSelectables.Add(selectable);
            }

            // Sort by position (top to bottom, left to right)
            autoSelectables.Sort((a, b) => CompareSelectablePositions(a, b));
            tabbableElements.AddRange(autoSelectables);
        }

        int CalculateNextIndex(bool reverse)
        {
            if (currentIndex < 0)
                return reverse ? tabbableElements.Count - 1 : 0;

            int nextIndex = currentIndex + (reverse ? -1 : 1);

            if (!wrapAround) { nextIndex = Mathf.Clamp(nextIndex, 0, tabbableElements.Count - 1); }
            else
            {
                if (nextIndex >= tabbableElements.Count) { nextIndex = 0; }
                else if (nextIndex < 0) { nextIndex = tabbableElements.Count - 1; }
            }

            return nextIndex;
        }

        bool ShouldIncludeInTabOrder(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsInteractable() &&
                   !HasExcludeFromTabOrder(selectable);
        }

        bool HasExcludeFromTabOrder(Selectable selectable) => selectable.GetComponent<ExcludeFromTabOrder>() != null;

        int CompareSelectablePositions(Selectable a, Selectable b)
        {
            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;

            // Compare Y position first (higher Y = earlier in tab order)
            int yCompare = posB.y.CompareTo(posA.y);
            if (Mathf.Abs(posA.y - posB.y) > 0.1f) { return yCompare; }

            // If Y positions are similar, compare X position (lower X = earlier)
            return posA.x.CompareTo(posB.x);
        }

        public void TabToNext(bool reverse = false)
        {
            if (tabbableElements.Count == 0)
            {
                RefreshTabbableElements();

                if (tabbableElements.Count == 0)
                    return;
            }

            int nextIndex = CalculateNextIndex(reverse);
            SelectElement(nextIndex);
        }

        public void RefreshTabbableElements()
        {
            tabbableElements.Clear();

            if (customTabOrder.Count > 0)
            {
                // Use custom tab order
                foreach (var element in customTabOrder)
                {
                    if (element != null && ShouldIncludeInTabOrder(element))
                        tabbableElements.Add(element);
                }
            }
            else if (autoFindTabbables)
            {
                FindTabbableElements();
            }
        }

        public void AddToCustomTabOrder(Selectable selectable)
        {
            if (!customTabOrder.Contains(selectable))
            {
                customTabOrder.Add(selectable);
                RefreshTabbableElements();
            }
        }

        public void RemoveFromCustomTabOrder(Selectable selectable)
        {
            if (customTabOrder.Remove(selectable))
                RefreshTabbableElements();
        }
    }

    /// <summary>
    /// Component to manually set tab order for specific elements.
    /// </summary>
    public class TabbableElement : MonoBehaviour
    {
        public int tabIndex = 0;
        [SerializeField] private Selectable targetSelectable;

        public Selectable Selectable => targetSelectable != null ? targetSelectable : GetComponent<Selectable>();

#if UNITY_EDITOR
        void Reset() => TryGetComponent(out targetSelectable);
#endif
    }

    /// <summary>
    /// Component to exclude an element from tab order.
    /// </summary>
    public class ExcludeFromTabOrder : MonoBehaviour
    {
        // This component just serves as a marker
        // Elements with this component will be skipped in tab navigation
    }
}