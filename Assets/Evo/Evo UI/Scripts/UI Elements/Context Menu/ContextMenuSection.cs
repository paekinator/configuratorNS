using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    [RequireComponent(typeof(RectTransform))]
    public class ContextMenuSection : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")]
        public Button sectionButton;
        public Transform submenuContainer;
        public GameObject submenuPrefab;

        [Header("Settings")]
        [Range(0f, 2f)] public float expandDelay = 0.2f;
        public Vector2 submenuOffset = new(5f, 0f);

        [Header("Animation")]
        public bool enableAnimation = true;
        public float animationDuration = 0.2f;
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        // Constants
        const int SortingOrder = 30001;

        // Instance variables
        bool isAnimating = false;
        bool submenuOnLeft = false;
        ContextMenu sourceMenu;
        ContextMenu.Item sectionItem;
        Coroutine expandCoroutine;
        Coroutine hoverCheckCoroutine;
        Coroutine animationCoroutine;
        [HideInInspector] public ContextMenuPreset submenuInstance;

        // Cache
        RectTransform rectTransform;
        RectTransform submenuRect;
        WaitForSeconds expandWait;

        // Shared yield objects
        static readonly WaitForSecondsRealtime shortCloseWait = new(0.15f);
        static readonly WaitForSecondsRealtime longCloseWait = new(0.25f);

        // Shared raycast state
        static readonly List<RaycastResult> raycastResults = new();
        static PointerEventData sharedPointerData;

        // Shared corner buffer
        static readonly Vector3[] worldCorners = new Vector3[4];

        // Static tracking for hover sections
        static ContextMenuSection currentHoveredSection;

        // Properties
        public bool IsExpanded { get; private set; }

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            expandWait = new WaitForSeconds(expandDelay);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            DestroySubmenuImmediate();

            if (currentHoveredSection == this)
                currentHoveredSection = null;
        }

        public void Setup(ContextMenu source, ContextMenu.Item item)
        {
            sourceMenu = source;
            sectionItem = item;

            if (sectionButton != null)
            {
                sectionButton.onClick.AddListener(OnSectionClicked);
                sectionButton.text = item.name;
                sectionButton.icon = item.icon;
                sectionButton.UpdateUI();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Close other section submenus (but not child sections)
            if (currentHoveredSection != null && currentHoveredSection != this && !IsChildOfSection(currentHoveredSection))
                currentHoveredSection.CollapseSubmenu();

            currentHoveredSection = this;

            // Stop any pending close check
            if (hoverCheckCoroutine != null)
            {
                StopCoroutine(hoverCheckCoroutine);
                hoverCheckCoroutine = null;
            }

            // Start expand timer if hover expand is enabled
            if (sectionItem != null && sectionItem.expandOnHover && !IsExpanded)
            {
                if (expandCoroutine != null) { StopCoroutine(expandCoroutine); }
                expandCoroutine = StartCoroutine(ExpandAfterDelay());
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Cancel any pending expand operation
            if (expandCoroutine != null)
            {
                StopCoroutine(expandCoroutine);
                expandCoroutine = null;
            }

            // Start checking if we should close the submenu
            if (IsExpanded && sectionItem != null && sectionItem.expandOnHover)
            {
                if (hoverCheckCoroutine != null) { StopCoroutine(hoverCheckCoroutine); }
                hoverCheckCoroutine = StartCoroutine(CheckAndCloseSubmenu());
            }
        }

        public void OnPointerClick(PointerEventData eventData) => OnSectionClicked();

        void OnSectionClicked()
        {
            if (isAnimating)
                return;

            // Close other section submenus (but not child sections)
            if (currentHoveredSection != null && currentHoveredSection != this && !IsChildOfSection(currentHoveredSection))
                currentHoveredSection.CollapseSubmenu();

            currentHoveredSection = this;

            if (IsExpanded) { CollapseSubmenu(); }
            else { ExpandSubmenu(); }
        }

        void ExpandSubmenu()
        {
            if (sectionItem == null || sectionItem.sectionItems.Count == 0 || IsExpanded)
                return;

            CreateSubmenu();
            IsExpanded = true;
            sectionButton.SetState(InteractionState.Selected);
        }

        public void CollapseSubmenu(bool immediate = false)
        {
            if (!IsExpanded)
                return;

            StopAllCoroutines();

            // Also collapse any child submenus
            CollapseAllChildSubmenus();

            if (immediate || !enableAnimation || submenuInstance == null) { DestroySubmenu(); }
            else
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateSubmenuOut());
            }

            IsExpanded = false;

            if (currentHoveredSection == this)
                currentHoveredSection = null;

            sectionButton.SetState(InteractionState.Normal);
        }

        void CreateSubmenu()
        {
            if (submenuPrefab == null)
                return;

            // Parent to the root canvas, not the main menu
            // This ensures position calculations work in absolute screen space
            Canvas rootCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas.rootCanvas : GetComponentInParent<Canvas>().rootCanvas;
            Transform parentTransform = rootCanvas != null ? rootCanvas.transform : sourceMenu.ActiveCanvas.transform;

            GameObject submenuGO = Instantiate(submenuPrefab, parentTransform);
            submenuInstance = submenuGO.GetComponent<ContextMenuPreset>();

            if (submenuInstance == null)
            {
                Destroy(submenuGO);
                return;
            }

            // Cache submenu RectTransform upfront — used in PositionSubmenu and both animation coroutines
            submenuRect = submenuGO.GetComponent<RectTransform>();

            // Use the SectionItem setup method instead of Item setup
            submenuInstance.Setup(sourceMenu, sectionItem.sectionItems);

            if (submenuInstance.canvasGroup == null)
                submenuInstance.canvasGroup = submenuInstance.gameObject.AddComponent<CanvasGroup>();

            if (sectionItem.expandOnHover)
                SetupSubmenuHoverHandlers();

            // If block UI is enabled, create a canvas for proper layering
            if (sourceMenu != null && sourceMenu.blockUIWhileOpen) { CreateSubmenuCanvas(); }

            PositionSubmenu();

            if (enableAnimation)
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateSubmenuIn());
            }
        }

        void SetupSubmenuHoverHandlers()
        {
            if (submenuInstance == null)
                return;

            if (!submenuInstance.gameObject.TryGetComponent<EventTrigger>(out var trigger))
                trigger = submenuInstance.gameObject.AddComponent<EventTrigger>();

            // Cancel close when entering submenu
            EventTrigger.Entry enterEntry = new() { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => 
            {
                if (hoverCheckCoroutine != null)
                {
                    StopCoroutine(hoverCheckCoroutine);
                    hoverCheckCoroutine = null;
                }
            });
            trigger.triggers.Add(enterEntry);

            // Check for close when leaving submenu
            EventTrigger.Entry exitEntry = new() { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((data) => 
            {
                if (hoverCheckCoroutine != null) { StopCoroutine(hoverCheckCoroutine); }
                hoverCheckCoroutine = StartCoroutine(CheckAndCloseSubmenu());
            });
            trigger.triggers.Add(exitEntry);
        }

        void CreateSubmenuCanvas()
        {
            if (submenuInstance == null)
                return;

            // Add canvas to submenu for proper sorting
            Canvas submenuCanvas = submenuInstance.gameObject.AddComponent<Canvas>();
            submenuInstance.gameObject.AddComponent<GraphicRaycaster>();
            submenuCanvas.overrideSorting = true;

            // Get the depth level to calculate proper sorting order
            int depth = GetSubmenuDepth();
            submenuCanvas.sortingOrder = SortingOrder + depth; // Higher than main menu and increases with nesting
        }

        bool IsChildOfSection(ContextMenuSection otherSection)
        {
            if (otherSection == null || otherSection.submenuInstance == null)
                return false;

            return transform.IsChildOf(otherSection.submenuInstance.transform);
        }

        static void PrepareSharedPointerData()
        {
            sharedPointerData ??= new PointerEventData(EventSystem.current);
            sharedPointerData.position = Utilities.GetPointerPosition();
        }

        bool IsMouseOverSection()
        {
            PrepareSharedPointerData();
            raycastResults.Clear();
            EventSystem.current.RaycastAll(sharedPointerData, raycastResults);

            bool found = false;
            foreach (var result in raycastResults)
            {
                // Check if mouse is over this section button specifically
                if (result.gameObject.transform.IsChildOf(transform))
                { 
                    found = true;
                    break; 
                }
            }

            raycastResults.Clear();
            return found;
        }

        bool IsMouseOverSectionOrSubmenu()
        {
            PrepareSharedPointerData();
            raycastResults.Clear();
            EventSystem.current.RaycastAll(sharedPointerData, raycastResults);

            bool found = false;
            foreach (var result in raycastResults)
            {
                // Check if mouse is over this section button
                if (result.gameObject.transform.IsChildOf(transform))
                {
                    found = true;
                    break;
                }

                // Check if mouse is over this submenu or any nested submenus
                if (IsMouseOverSubmenuHierarchy(result.gameObject.transform))
                {
                    found = true
                        ; break;
                }
            }

            raycastResults.Clear();
            return found;
        }

        bool IsMouseOverSubmenuHierarchy(Transform target)
        {
            if (submenuInstance == null)
                return false;

            if (target.IsChildOf(submenuInstance.transform))
                return true;

            ContextMenuSection[] childSections = submenuInstance.GetComponentsInChildren<ContextMenuSection>();
            foreach (var childSection in childSections)
            {
                if (childSection != this && childSection.IsMouseOverSubmenuHierarchy(target))
                    return true;
            }

            return false;
        }

        // Collapse all child submenus recursively
        void CollapseAllChildSubmenus()
        {
            if (submenuInstance == null)
                return;

            // Note: GetComponentsInChildren allocates an array; acceptable here since collapse is infrequent
            ContextMenuSection[] childSections = submenuInstance.GetComponentsInChildren<ContextMenuSection>();
            foreach (var childSection in childSections)
            {
                if (childSection != this)
                    childSection.CollapseSubmenu(true);
            }
        }

        void PositionSubmenu()
        {
            if (submenuInstance == null)
                return;

            Canvas rootCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas : GetComponentInParent<Canvas>().rootCanvas;

            // Determine render mode and camera once — passed down to GetScreenSize and ClampSubmenuToScreen
            bool isOverlay = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Camera cam = (!isOverlay && rootCanvas.worldCamera != null) ? rootCanvas.worldCamera : Camera.main;
            if (isOverlay) { cam = null; }

            // Set anchors to bottom-left for absolute positioning
            submenuRect.anchorMin = Vector2.zero;
            submenuRect.anchorMax = Vector2.zero;
            submenuRect.pivot = new Vector2(0, 1); // Top-left pivot for easier positioning

            // Force rebuild to get accurate dimensions
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(submenuRect);

            float screenPadding = 10f;

            // Get section's world corners and convert to Screen Space for calculations
            // Uses shared static buffer to avoid per-call allocation
            rectTransform.GetWorldCorners(worldCorners);

            // 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right
            // Need Screen Space points for clamping logic
            Vector3 topRightScreen, topLeftScreen;

            if (isOverlay)
            {
                topRightScreen = worldCorners[2];
                topLeftScreen = worldCorners[1];
            }
            else
            {
                topRightScreen = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[2]);
                topLeftScreen = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[1]);
            }

            // Get actual screen pixel size of the submenu
            // cam and isOverlay passed in to avoid re-resolving inside the helper
            Vector2 submenuSize = GetScreenSize(submenuRect, cam, isOverlay);
            float submenuWidth = submenuSize.x;

            // Calculate position starting from top-right corner of section (Screen Space)
            Vector3 targetPos = topRightScreen;
            targetPos.x += submenuOffset.x;
            targetPos.y += submenuOffset.y;

            // Check if it fits on the right
            if (targetPos.x + submenuWidth > Screen.width - screenPadding)
            {
                // Position on left side instead
                submenuRect.pivot = new Vector2(1, 1); // Top-right pivot
                targetPos = topLeftScreen;
                targetPos.x -= submenuOffset.x;
                targetPos.y += submenuOffset.y;
                submenuOnLeft = true;
            }
            else
            {
                submenuOnLeft = false;
            }

            // Clamp to screen (Calculations in Screen Space)
            targetPos = ClampSubmenuToScreen(targetPos, submenuRect, cam, isOverlay);
            // targetPos Z should be reset or kept? Let's go with 0 for screen space.
            targetPos.z = 0;

            // Convert Screen Space targetPos back to World Space if needed
            if (isOverlay) { submenuRect.position = targetPos; }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    submenuRect.parent as RectTransform,
                    targetPos,
                    cam,
                    out Vector3 worldPos
                );
                submenuRect.position = worldPos;
            }
        }

        void DestroySubmenu()
        {
            if (submenuInstance != null)
            {
                Destroy(submenuInstance.gameObject);
                submenuInstance = null;
                submenuRect = null;
            }

            isAnimating = false;
        }

        public void DestroySubmenuImmediate()
        {
            if (submenuInstance != null)
            {
                Destroy(submenuInstance.gameObject);
                submenuInstance = null;
                submenuRect = null;
                IsExpanded = false;
            }
        }

        Vector3 ClampSubmenuToScreen(Vector3 position, RectTransform rect, Camera cam, bool isOverlay)
        {
            Vector2 size = GetScreenSize(rect, cam, isOverlay);
            float width = size.x;
            float height = size.y;
            float padding = 10f;

            // Get pivot offsets
            float pivotOffsetX = width * rect.pivot.x;
            float pivotOffsetY = height * rect.pivot.y;

            // Calculate bounds
            float minX = position.x - pivotOffsetX;
            float maxX = position.x + (width - pivotOffsetX);
            float minY = position.y - pivotOffsetY;
            float maxY = position.y + (height - pivotOffsetY);

            // Adjust if out of bounds
            if (minX < padding) { position.x += (padding - minX); }
            if (maxX > Screen.width - padding) { position.x -= (maxX - (Screen.width - padding)); }
            if (minY < padding) { position.y += (padding - minY); }
            if (maxY > Screen.height - padding) { position.y -= (maxY - (Screen.height - padding)); }

            return position;
        }

        // cam and isOverlay passed in from the caller to avoid redundant canvas lookups
        // Uses shared worldCorners buffer — caller must not depend on its contents after this call
        Vector2 GetScreenSize(RectTransform rect, Camera cam, bool isOverlay)
        {
            if (isOverlay)
                return Vector2.Scale(rect.rect.size, rect.lossyScale);

            // For Camera/World modes, calculate screen projection
            if (cam == null)
                return Vector2.Scale(rect.rect.size, rect.lossyScale); // Fallback

            rect.GetWorldCorners(worldCorners);

            // Calculate screen bounds from world corners
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
                if (screenPos.x < minX) { minX = screenPos.x; }
                if (screenPos.x > maxX) { maxX = screenPos.x; }
                if (screenPos.y < minY) { minY = screenPos.y; }
                if (screenPos.y > maxY) { maxY = screenPos.y; }
            }

            return new Vector2(maxX - minX, maxY - minY);
        }

        IEnumerator ExpandAfterDelay()
        {
            yield return expandWait;

            // Only expand if we're still being hovered and not already expanded
            if (!IsExpanded && IsMouseOverSection())
                ExpandSubmenu();

            expandCoroutine = null;
        }

        // Close checking with longer delay for nested menus
        IEnumerator CheckAndCloseSubmenu()
        {
            // Use a longer delay for nested menus to give user time to navigate
            yield return GetSubmenuDepth() > 1 ? longCloseWait : shortCloseWait;

            if (!IsMouseOverSectionOrSubmenu())
                CollapseSubmenu();

            hoverCheckCoroutine = null;
        }

        // Get the depth of nested submenus
        int GetSubmenuDepth()
        {
            int depth = 0;
            Transform current = transform;

            while (current != null)
            {
                ContextMenuPreset menu = current.GetComponentInParent<ContextMenuPreset>();
                if (menu == null) { break; }

                ContextMenuSection parentSection = menu.GetComponentInParent<ContextMenuSection>();
                if (parentSection == null) { break; }

                depth++;
                current = parentSection.transform;
            }

            return depth;
        }

        IEnumerator AnimateSubmenuIn()
        {
            if (submenuInstance == null)
                yield break;

            isAnimating = true;

            CanvasGroup canvasGroup = submenuInstance.canvasGroup;

            if (canvasGroup == null || submenuRect == null)
                yield break;

            // Get current validated World Position (set in PositionSubmenu)
            Vector3 finalWorldPos = submenuRect.position;

            // Calculate Start Position (Screen Space Offset)
            Canvas rootCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas : GetComponentInParent<Canvas>().rootCanvas;
            bool isOverlay = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Camera cam = (!isOverlay && rootCanvas.worldCamera != null) ? rootCanvas.worldCamera : Camera.main;

            Vector3 finalScreenPos;
            if (isOverlay) { finalScreenPos = finalWorldPos; }
            else { finalScreenPos = RectTransformUtility.WorldToScreenPoint(cam, finalWorldPos); }

            // Apply 20px offset in screen space
            Vector3 startScreenPos = finalScreenPos;
            startScreenPos.x += submenuOnLeft ? 20f : -20f;

            // Convert back to World Space
            Vector3 startWorldPos;
            if (isOverlay) { startWorldPos = startScreenPos; }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    submenuRect.parent as RectTransform,
                    startScreenPos,
                    cam,
                    out startWorldPos
                );
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            submenuRect.position = startWorldPos;

            float elapsedTime = 0f;
            while (elapsedTime < animationDuration)
            {
                // Submenu may have been destroyed externally between frames
                if (submenuInstance == null || submenuRect == null)
                {
                    isAnimating = false;
                    animationCoroutine = null;
                    yield break;
                }

                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = animationCurve.Evaluate(progress);

                canvasGroup.alpha = curveValue;
                submenuRect.position = Vector3.Lerp(startWorldPos, finalWorldPos, curveValue);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            submenuRect.position = finalWorldPos;

            isAnimating = false;
            animationCoroutine = null;
        }

        IEnumerator AnimateSubmenuOut()
        {
            if (submenuInstance == null)
                yield break;

            isAnimating = true;

            CanvasGroup canvasGroup = submenuInstance.canvasGroup;
            if (canvasGroup == null || submenuRect == null)
            {
                DestroySubmenu();
                yield break;
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Get current world position
            Vector3 startWorldPos = submenuRect.position;

            // Calculate end position (Screen Space Offset)
            Canvas rootCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas : GetComponentInParent<Canvas>().rootCanvas;
            bool isOverlay = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Camera cam = (!isOverlay && rootCanvas.worldCamera != null) ? rootCanvas.worldCamera : Camera.main;

            Vector3 startScreenPos;
            if (isOverlay) { startScreenPos = startWorldPos; }
            else { startScreenPos = RectTransformUtility.WorldToScreenPoint(cam, startWorldPos); }

            // Apply 20px offset
            Vector3 endScreenPos = startScreenPos;
            endScreenPos.x += submenuOnLeft ? 20f : -20f;

            // Convert back to World
            Vector3 endWorldPos;
            if (isOverlay) { endWorldPos = endScreenPos; }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    submenuRect.parent as RectTransform,
                    endScreenPos,
                    cam,
                    out endWorldPos
                );
            }

            float startAlpha = canvasGroup.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                // Guard — submenu may have been destroyed externally between frames
                if (submenuInstance == null || submenuRect == null)
                {
                    isAnimating = false;
                    animationCoroutine = null;
                    yield break;
                }

                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
                submenuRect.position = Vector3.Lerp(startWorldPos, endWorldPos, progress);

                yield return null;
            }

            DestroySubmenu();
            isAnimating = false;
            animationCoroutine = null;
        }
    }
}