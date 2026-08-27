using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class ContextMenuPreset : MonoBehaviour
    {
        [Header("References")]
        public Transform itemContainer;
        public GameObject buttonPrefab;
        public GameObject separatorPrefab;
        public GameObject sectionPrefab;
        public CanvasGroup canvasGroup;

        // Instance variables
        ContextMenu sourceMenu;
        RectTransform rectTransform;
        readonly List<GameObject> instantiatedItems = new();

        // Separate list for iteration
        readonly List<ContextMenuSection> sectionList = new();
        readonly Dictionary<ContextMenu.Item, ContextMenuSection> sectionInstances = new();

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (itemContainer == null) { itemContainer = transform; }

            for (int i = itemContainer.childCount - 1; i >= 0; i--)
                Destroy(itemContainer.GetChild(i).gameObject);
        }

        void OnDestroy()
        {
            instantiatedItems.Clear();
            sectionInstances.Clear();
            sectionList.Clear();
        }

        public void Setup(ContextMenu source, List<ContextMenu.Item> items)
        {
            sourceMenu = source;

            // Create items
            CreateMenuItems(items);

            // Force layout update
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void Setup(ContextMenu source, List<ContextMenu.SectionItem> sectionItems)
        {
            sourceMenu = source;

            var convertedItems = ConvertSectionItemsToItems(sectionItems);
            CreateMenuItems(convertedItems);

            int i = 0;
            foreach (var sectionItem in sectionItems)
            {
                if (!sectionItem.IsActive)
                    continue;

                sectionItem.generatedObject = convertedItems[i++].generatedObject;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void CollapseAllSections(bool immediate = false)
        {
            // Iterate sectionList to avoid Dictionary.Values allocation
            foreach (var section in sectionList)
            {
                if (section != null && section.IsExpanded)
                    section.CollapseSubmenu(immediate);
            }
        }

        void CreateMenuItems(List<ContextMenu.Item> items)
        {
            foreach (var item in items)
            {
                if (!item.IsActive)
                    continue;

                CreateMenuItem(item);
            }
        }

        GameObject CreateMenuItem(ContextMenu.Item item)
        {
            GameObject itemGO = null;

            switch (item.type)
            {
                case ContextMenu.Item.ItemType.Button:
                    itemGO = CreateButtonItem(item);
                    break;
                case ContextMenu.Item.ItemType.Separator:
                    itemGO = CreateSeparatorItem();
                    break;
                case ContextMenu.Item.ItemType.Section:
                    itemGO = CreateSectionItem(item);
                    break;
                case ContextMenu.Item.ItemType.CustomObject:
                    itemGO = CreateCustomItem(item);
                    break;
            }

            if (itemGO != null)
            {
                item.generatedObject = itemGO;
                instantiatedItems.Add(itemGO);
            }

            return itemGO;
        }

        GameObject CreateButtonItem(ContextMenu.Item item)
        {
            if (buttonPrefab == null)
                return null;

            GameObject buttonGO = Instantiate(buttonPrefab, itemContainer);
            if (buttonGO.TryGetComponent<Button>(out var btn))
            {
                // Note: these lambdas allocate a closure object per button due to item/sourceMenu capture.
                // Eliminating this would require restructuring Item to hold pre-bound delegates.
                btn.onClick.AddListener(() =>
                {
                    item.onClick?.Invoke();
                    if (sourceMenu != null) { sourceMenu.OnItemClicked(item); }
                });
                btn.SetText(item.name);
                btn.SetIcon(item.icon);
            }

            return buttonGO;
        }

        GameObject CreateSeparatorItem()
        {
            if (separatorPrefab == null)
                return null;

            GameObject separatorGO = Instantiate(separatorPrefab, itemContainer);
            return separatorGO;
        }

        GameObject CreateSectionItem(ContextMenu.Item item)
        {
            if (sectionPrefab == null)
                return null;

            GameObject sectionGO = Instantiate(sectionPrefab, itemContainer);
            if (sectionGO.TryGetComponent<ContextMenuSection>(out var sectionComponent))
            {
                sectionComponent.Setup(sourceMenu, item);
                sectionInstances[item] = sectionComponent;
                sectionList.Add(sectionComponent);
            }

            return sectionGO;
        }

        GameObject CreateCustomItem(ContextMenu.Item item)
        {
            if (item.customPrefab == null)
                return null;

            GameObject customGO = Instantiate(item.customPrefab, itemContainer);
            if (customGO.TryGetComponent<Button>(out var customButton))
            {
                // Same closure note as CreateButtonItem
                customButton.onClick.AddListener(() =>
                {
                    item.onClick?.Invoke();
                    if (sourceMenu != null) { sourceMenu.OnItemClicked(item); }
                });
            }

            return customGO;
        }

        List<ContextMenu.Item> ConvertSectionItemsToItems(List<ContextMenu.SectionItem> sectionItems)
        {
            List<ContextMenu.Item> convertedItems = new();

            foreach (var sectionItem in sectionItems)
            {
                if (!sectionItem.IsActive)
                    continue;

                var item = new ContextMenu.Item
                {
                    name = sectionItem.name,
                    icon = sectionItem.icon,
                    onClick = sectionItem.onClick,
                    customPrefab = sectionItem.customPrefab,
                    type = sectionItem.type switch
                    {
                        ContextMenu.SectionItem.ItemType.Button => ContextMenu.Item.ItemType.Button,
                        ContextMenu.SectionItem.ItemType.Separator => ContextMenu.Item.ItemType.Separator,
                        ContextMenu.SectionItem.ItemType.CustomObject => ContextMenu.Item.ItemType.CustomObject,
                        _ => ContextMenu.Item.ItemType.Button
                    }
                };

                convertedItems.Add(item);
            }

            return convertedItems;
        }

        public void AnimateOutAndDestroy(ContextMenu.AnimationType animType, float duration, Vector3 worldStartPos, Vector3 worldEndPos)
        {
            // Close any open submenus immediately
            CollapseAllSections(true);

            // Disable interaction while fading out
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            StartCoroutine(AnimateOutRoutine(animType, duration, worldStartPos, worldEndPos));
        }

        IEnumerator AnimateOutRoutine(ContextMenu.AnimationType animType, float duration, Vector3 worldStartPos, Vector3 worldEndPos)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;

                // Reverse progress for out-animation (1 to 0)
                float curveValue = 1f - progress;

                switch (animType)
                {
                    case ContextMenu.AnimationType.Fade:
                        canvasGroup.alpha = curveValue;
                        break;

                    case ContextMenu.AnimationType.Scale:
                        canvasGroup.alpha = curveValue;
                        transform.localScale = Vector3.one * curveValue;
                        break;

                    case ContextMenu.AnimationType.Slide:
                        canvasGroup.alpha = curveValue;
                        // Lerp between the slide-out position and the open position based on the curve
                        rectTransform.position = Vector3.Lerp(worldEndPos, worldStartPos, curveValue);
                        break;
                }

                yield return null;
            }

            // Animation complete, destroy the canvas instance
            Destroy(gameObject);
        }
    }
}