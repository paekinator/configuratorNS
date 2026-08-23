using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    [AddComponentMenu("Evo/UI/UI Elements/Context Menu")]
    public class ContextMenu : MonoBehaviour, IPointerClickHandler
    {
        [EvoHeader("Content", Constants.CustomEditorID)]
        public List<Item> menuItems = new();

        [EvoHeader("Settings", Constants.CustomEditorID)]
        public bool is3DObject = false;
        public bool usePointerPosition = true;
        public bool blockUIWhileOpen = false;
        public bool closeOnItemClick = true;
        public bool closeOnOutsideClick = true;
        public MouseButton triggerButton = MouseButton.Right;
        [Tooltip("Maximum distance in pixels the pointer can move between down and up to still register as a click.")]
        [Min(0f)] public float dragThreshold = 0f;

        [EvoHeader("Animation", Constants.CustomEditorID)]
        public AnimationType animationType = AnimationType.Slide;
        [Range(0.01f, 1f)] public float animationDuration = 0.1f;
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [Range(0f, 1f)] public float scaleFrom = 0.8f;
        public Vector2 slideOffset = new(0f, -20f);

        [EvoHeader("Position & Offset", Constants.CustomEditorID)]
        public OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        public Vector2 customOffset = new(10f, 10f);
        public float offsetDistance = 10f;
        public float screenEdgePadding = 10f;

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CustomEditorID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CustomEditorID)]
        public GameObject menuPreset;
        public Canvas targetCanvas;
        [Tooltip("Used for 3D object casting. If 'Is 3D Object' is enabled and camera set null, MainCamera will be used.")]
        public Camera targetCamera;

        [EvoHeader("Events", Constants.CustomEditorID)]
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        // Enums
        public enum AnimationType { None = 0, Fade = 1, Scale = 2, Slide = 3 }
        public enum MouseButton { Left = 0, Right = 1, Middle = 2, None = 9 }

        // Constants
        const int SortingOrder = 30000;

        // Instance variables
        ContextMenuPreset menuInstance;
        Coroutine animationCoroutine;
        Coroutine outsideCoroutine;
        Vector3 lastClickPosition;
        Vector3 targetPosition;
        Canvas rootCanvas;
        Canvas blocker;
        Canvas menuCanvas;
        readonly static List<ContextMenu> activeMenus = new();

        // Cache
        PointerEventData pointerDataCache;
        readonly Vector3[] cornersCache = new Vector3[4];
        readonly List<RaycastResult> raycastResultsCache = new();
        readonly List<ContextMenuSection> sectionsCache = new();

        [System.Serializable]
        public class Item
        {
            [FormerlySerializedAs("itemName")] public string name = "Menu Item";
            public Sprite icon;
            [FormerlySerializedAs("itemType")] public ItemType type = ItemType.Button;

            [System.Obsolete("The 'ContextMenu.Item.itemName' field has been renamed to 'ContextMenu.Item.name'. Please update your script.", false)]
            public string itemName
            {
                get => name;
                set => name = value;
            }

            [System.Obsolete("'ContextMenu.Item.itemType' field has been renamed to 'ContextMenu.Item.type'. Please update your script.", false)]
            public ItemType itemType
            {
                get => type;
                set => type = value;
            }

            [EvoShowIf(nameof(type), ItemType.Button)]
            public UnityEvent onClick = new();

            [EvoShowIf(nameof(type), ItemType.CustomObject)]
            public GameObject customPrefab;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            [Header("Section Settings")]
            [EvoShowIf(nameof(type), ItemType.Section)]
            public bool expandOnHover = true;
            public List<SectionItem> sectionItems = new();

            public enum ItemType
            {
                Button = 0,
                Separator = 1,
                Section = 2,
                CustomObject = 3
            }

            /// <summary>
            /// Returns the generated object for this context menu item.
            /// </summary>
            [HideInInspector] public GameObject generatedObject;

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive { get; set; } = true;

            /// <summary>
            /// Returns a sub-item by its entry name.
            /// </summary>
            public SectionItem GetSectionItem(string sectionName)
            {
                for (int i = 0; i < sectionItems.Count; i++)
                {
                    if (sectionItems[i].name == sectionName)
                        return sectionItems[i];
                }

                return null;
            }

            /// <summary>
            /// Returns a button from the generated item object.
            /// </summary>
            public Button GetButton() => generatedObject != null ? generatedObject.GetComponent<Button>() : null;
        }

        [System.Serializable]
        public class SectionItem
        {
            [Header("Basic Properties")]
            [FormerlySerializedAs("itemName")] public string name = "Menu Item";
            public Sprite icon;
            [FormerlySerializedAs("itemType")] public ItemType type = ItemType.Button;

            [System.Obsolete("The 'ContextMenu.SectionItem.itemName' field has been renamed to 'ContextMenu.SectionItem.name'. Please update your script.", false)]
            public string itemName
            {
                get => name;
                set => name = value;
            }

            [System.Obsolete("'ContextMenu.SectionItem.itemType' field has been renamed to 'ContextMenu.SectionItem.type'. Please update your script.", false)]
            public ItemType itemType
            {
                get => type;
                set => type = value;
            }

            [EvoShowIf(nameof(type), ItemType.Button)]
            public UnityEvent onClick = new();

            [EvoShowIf(nameof(type), ItemType.CustomObject)]
            public GameObject customPrefab;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            public enum ItemType
            {
                Button = 0,
                Separator = 1,
                CustomObject = 2
            }

            /// <summary>
            /// Returns the generated object for this context menu item.
            /// </summary>
            [HideInInspector] public GameObject generatedObject;

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive { get; set; } = true;

            /// <summary>
            /// Returns a button from the generated item object.
            /// </summary>
            public Button GetButton() => generatedObject != null ? generatedObject.GetComponent<Button>() : null;
        }

        void Start()
        {
            if (menuPreset == null)
            {
                Debug.LogError("[Context Menu] 'Menu Preset' is missing. " +
                    "Please select the object and assign a valid context menu preset via the References tab.", this);
                return;
            }

            if (is3DObject)
                EnsurePhysicsRaycaster();

#if EVO_LOCALIZATION
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
#endif
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            activeMenus.Remove(this);
            DestroyBlocker();

            // Handoff the animation to the Preset if it exists and animations are enabled
            if (animationType == AnimationType.None) { HideImmediate(); }
            else if (menuInstance != null)
            {
                RectTransform menuRect = menuInstance.GetComponent<RectTransform>();

                // Pre-calculate world positions so the Preset doesn't need to rely on the dying Canvas
                Vector3 worldOpenPos = GetWorldPositionFromScreen(menuRect, targetPosition);
                Vector3 worldSlideOutPos = GetWorldPositionFromScreen(menuRect, targetPosition + (Vector3)slideOffset);

                menuInstance.AnimateOutAndDestroy(animationType, animationDuration, worldOpenPos, worldSlideOutPos);

                // Clear the reference so it doesn't get caught in any stray cleanup
                menuInstance = null;
            }

#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
#endif
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsCorrectMouseButton(eventData))
            {
                // If threshold is 0 or less, we disable the check and always treat it as valid.
                // Otherwise, check if the drag distance is within our defined threshold.
                bool isValidClick = dragThreshold <= 0f || Vector2.Distance(eventData.pressPosition, eventData.position) <= dragThreshold;
                if (isValidClick)
                {
                    lastClickPosition = eventData.position;
                    Show();
                }
            }
        }

        void EnsurePhysicsRaycaster()
        {
            Camera cam = targetCamera ? targetCamera : Camera.main;
            
            if (cam == null)
            {
                Debug.LogWarning("[Context Menu] 'Is 3D Object' is enabled, but no camera found. " +
                    "Assign 'Target Camera' in the References tab.", this);
                return;
            }

            if (cam.GetComponent<PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<PhysicsRaycaster>();
        }

        void SetupMenu()
        {
            if (menuInstance == null)
                return;

            menuInstance.Setup(this, menuItems);
        }

        void PositionMenu()
        {
            if (menuInstance == null)
                return;

            if (!menuInstance.TryGetComponent<RectTransform>(out var menuRect))
                return;

            // Setup initial pivot and layout using the configured settings
            SetMenuPivot(menuRect, offsetPosition);

            // Force layout update to get accurate size for calculations
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);

            // Calculate base position (Screen Space) without offsets
            Vector3 basePosition = GetBasePosition();

            // Determine final offset and handle flipping
            // Check if the default configuration would push the menu off-screen
            Vector3 finalOffset = GetOffsetVector(offsetPosition);
            Vector3 projectedPos = basePosition + finalOffset;

            if (ShouldFlipVertical(projectedPos, menuRect))
            {
                if (offsetPosition == OffsetPosition.Custom)
                {
                    // Handle Custom Flip: Invert pivot Y and Y offset
                    menuRect.pivot = new Vector2(menuRect.pivot.x, 1f - menuRect.pivot.y);
                    finalOffset.y = -finalOffset.y;
                }
                else
                {
                    // Handle Standard Flip: Switch to opposite type (e.g., Bottom -> Top)
                    OffsetPosition flippedType = GetOppositeOffset(offsetPosition);
                    SetMenuPivot(menuRect, flippedType);
                    finalOffset = GetOffsetVector(flippedType);
                }
            }

            // Calculate final target position
            targetPosition = basePosition + finalOffset;

            // Clamp to screen
            // This ensures it never goes off-screen even after flipping
            targetPosition = ClampToScreen(targetPosition, menuRect);

            // Apply Position
            if (animationType != AnimationType.Slide) { menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition); }
        }

        Vector3 GetBasePosition()
        {
            if (menuInstance == null)
                return Vector3.zero;

            Vector3 basePosition;

            if (usePointerPosition) { basePosition = (Vector3)Utilities.GetPointerPosition(); }
            else
            {
                // Calculate position based on the UI element's RectTransform
                Canvas canvas = ActiveCanvas;

                if (TryGetComponent<RectTransform>(out var rectTransform))
                {
                    // For UI elements
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { basePosition = rectTransform.position; }
                    else
                    {
                        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                        if (cam != null) { basePosition = RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position); }
                        else { basePosition = rectTransform.position; }
                    }

                    // Adjust for non-centered pivot to ensure targeting the center/corner accurately
                    Vector2 pivot = rectTransform.pivot;
                    Vector2 pivotOffset = new(
                        (pivot.x - 0.5f) * rectTransform.rect.width * rectTransform.lossyScale.x,
                        (pivot.y - 0.5f) * rectTransform.rect.height * rectTransform.lossyScale.y
                    );

                    basePosition.x -= pivotOffset.x;
                    basePosition.y -= pivotOffset.y;
                }
                else if (is3DObject)
                {
                    Camera cam = targetCamera ? targetCamera : Camera.main;
                    basePosition = cam != null ? cam.WorldToScreenPoint(transform.position) : Vector3.zero;
                }
                else
                {
                    basePosition = lastClickPosition;
                }
            }

            return basePosition;
        }

        Vector3 GetWorldPositionFromScreen(RectTransform target, Vector3 screenPos)
        {
            Canvas canvas = ActiveCanvas;

            // If overlay, World Space == Screen Space
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return screenPos;

            // For Camera/World modes, we must convert
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam == null) { return screenPos; } // Fallback

            // Convert screen point to world point on the plane of the RectTransform
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                target.parent as RectTransform,
                screenPos,
                cam,
                out Vector3 worldPos
            );

            return worldPos;
        }

        void SetMenuPivot(RectTransform menuRect, OffsetPosition type)
        {
            Vector2 pivot = Vector2.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    pivot = new Vector2(1, 0); // Menu grows from bottom-right corner
                    break;
                case OffsetPosition.TopRight:
                    pivot = new Vector2(0, 0); // Menu grows from bottom-left corner
                    break;
                case OffsetPosition.BottomLeft:
                    pivot = new Vector2(1, 1); // Menu grows from top-right corner
                    break;
                case OffsetPosition.BottomRight:
                    pivot = new Vector2(0, 1); // Menu grows from top-left corner
                    break;
                case OffsetPosition.Top:
                    pivot = new Vector2(0.5f, 0); // Menu grows from bottom-center
                    break;
                case OffsetPosition.Bottom:
                    pivot = new Vector2(0.5f, 1); // Menu grows from top-center
                    break;
                case OffsetPosition.Left:
                    pivot = new Vector2(1, 0.5f); // Menu grows from right-center
                    break;
                case OffsetPosition.Right:
                    pivot = new Vector2(0, 0.5f); // Menu grows from left-center
                    break;
                case OffsetPosition.Custom:
                    pivot = new Vector2(0, 1); // Default to top-left for custom
                    break;
            }

            menuRect.pivot = pivot;
        }

        bool ShouldFlipVertical(Vector3 targetScreenPos, RectTransform menuRect)
        {
            Vector2 size = GetScreenSize(menuRect);

            // Pivot Y: 1 = Top (Grows Down), 0 = Bottom (Grows Up)
            if (menuRect.pivot.y > 0.5f) // Currently growing down (e.g. BottomRight)
            {
                // Check if the bottom edge is off-screen
                float bottomY = targetScreenPos.y - (size.y * menuRect.pivot.y);
                if (bottomY < screenEdgePadding) { return true; }
            }
            else if (menuRect.pivot.y < 0.5f) // Currently Growing Up (e.g. TopRight)
            {
                // Check if the top edge is off-screen
                float topY = targetScreenPos.y + (size.y * (1f - menuRect.pivot.y));
                if (topY > Screen.height - screenEdgePadding) { return true; }
            }

            return false;
        }

        OffsetPosition GetOppositeOffset(OffsetPosition current)
        {
            return current switch
            {
                OffsetPosition.TopLeft => OffsetPosition.BottomLeft,
                OffsetPosition.TopRight => OffsetPosition.BottomRight,
                OffsetPosition.BottomLeft => OffsetPosition.TopLeft,
                OffsetPosition.BottomRight => OffsetPosition.TopRight,
                OffsetPosition.Top => OffsetPosition.Bottom,
                OffsetPosition.Bottom => OffsetPosition.Top,
                _ => current,
            };
        }

        void SetInitialAnimationState()
        {
            if (menuInstance == null)
                return;

            RectTransform menuRect = menuInstance.GetComponent<RectTransform>();

            switch (animationType)
            {
                case AnimationType.Fade:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
                    break;

                case AnimationType.Scale:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    menuInstance.transform.localScale = Vector3.one * scaleFrom;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
                    break;

                case AnimationType.Slide:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    Vector3 startPosScreen = targetPosition + (Vector3)slideOffset;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, startPosScreen);
                    break;
            }
        }

        void ApplyAnimationState(float progress, bool isIn)
        {
            if (menuInstance == null)
                return;

            RectTransform menuRect = menuInstance.GetComponent<RectTransform>();

            switch (animationType)
            {
                case AnimationType.Fade:
                    menuInstance.canvasGroup.alpha = progress;
                    break;

                case AnimationType.Scale:
                    menuInstance.canvasGroup.alpha = progress;
                    menuInstance.transform.localScale = Vector3.one * progress;
                    break;

                case AnimationType.Slide:
                    menuInstance.canvasGroup.alpha = progress;
                    if (isIn)
                    {
                        Vector3 startPos = targetPosition + (Vector3)slideOffset;
                        Vector3 currentScreenPos = Vector3.Lerp(startPos, targetPosition, progress);
                        menuRect.position = GetWorldPositionFromScreen(menuRect, currentScreenPos);
                    }
                    else
                    {
                        Vector3 endPos = targetPosition + (Vector3)slideOffset;
                        Vector3 currentScreenPos = Vector3.Lerp(targetPosition, endPos, 1f - progress);
                        menuRect.position = GetWorldPositionFromScreen(menuRect, currentScreenPos);
                    }
                    break;
            }
        }

        void CreateMenuCanvas()
        {
            if (menuInstance == null || menuCanvas != null)
                return;

            menuCanvas = menuInstance.gameObject.AddComponent<Canvas>();
            menuInstance.gameObject.AddComponent<GraphicRaycaster>();
            menuCanvas.vertexColorAlwaysGammaSpace = true;
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = SortingOrder;
        }

        void CreateBlocker()
        {
            if (blocker != null)
                return;

            if (rootCanvas == null)
            {
                var parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null) { rootCanvas = parentCanvas.rootCanvas; }
                else { rootCanvas = ActiveCanvas.rootCanvas; } // fallback for 3D objects
            }

            if (rootCanvas == null)
                return;

            GameObject blockerGO = new("Context Menu Blocker");
            blocker = blockerGO.AddComponent<Canvas>();
            blocker.overrideSorting = true;
            blocker.sortingOrder = SortingOrder - 1;
            blockerGO.AddComponent<GraphicRaycaster>();

            RectTransform blockerRT = blockerGO.GetComponent<RectTransform>();
            blockerRT.SetParent(rootCanvas.transform, false);
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.sizeDelta = Vector2.zero;
            blockerRT.anchoredPosition = Vector2.zero;

            Image blockerImage = blockerGO.AddComponent<Image>();
            blockerImage.color = Color.clear;

            UnityEngine.UI.Button button = blockerGO.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(OnBlockerClicked);

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void OnBlockerClicked()
        {
            if (closeOnOutsideClick)
                Hide();
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
            blocker = null;
        }

        public void Show()
        {
            if (menuPreset == null || IsVisible())
                return;

            // Hide other active menus first
            HideAllActiveMenus();

            // Hide any existing menu
            HideImmediate();

            // Instantiate menu
            GameObject menuGO = Instantiate(menuPreset, ActiveCanvas.transform);
            menuInstance = menuGO.GetComponent<ContextMenuPreset>();

            if (menuInstance == null)
            {
                Destroy(menuGO);
                return;
            }

            onShow?.Invoke();
            SetupMenu();
            PositionMenu();

            if (blockUIWhileOpen)
            {
                CreateMenuCanvas();
                CreateBlocker();
            }

            activeMenus.Add(this);

            if (closeOnOutsideClick)
            {
                if (outsideCoroutine != null) { StopCoroutine(outsideCoroutine); outsideCoroutine = null; }
                outsideCoroutine = StartCoroutine(DetectOutsideClick());
            }

            if (animationType != AnimationType.None)
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateMenuIn());
            }
        }

        public void Hide()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            if (closeOnOutsideClick && outsideCoroutine != null)
            {
                StopCoroutine(outsideCoroutine);
                outsideCoroutine = null;
            }

            DestroyBlocker();

            if (menuInstance != null && animationType != AnimationType.None && gameObject.activeInHierarchy)
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateMenuOut());

                menuInstance.CollapseAllSections();
            }
            else { HideImmediate(); }

            // Remove from active menus
            activeMenus.Remove(this);

            // Invoke events
            onHide?.Invoke();
        }

        public void HideImmediate()
        {
            if (menuInstance != null)
            {
                // Destroy all submenus first
                menuInstance.GetComponentsInChildren(sectionsCache);
                for (int i = 0; i < sectionsCache.Count; i++) { sectionsCache[i].DestroySubmenuImmediate(); }

                Destroy(menuInstance.gameObject);
                menuInstance = null;
            }

            menuCanvas = null;
        }

        public void OnItemClicked(Item item)
        {
            if (closeOnItemClick && item.type != Item.ItemType.Section)
                Hide();
        }

        public bool IsVisible() => menuInstance != null;

        public void SetTriggerButton(int index) => triggerButton = (MouseButton)index;

        bool IsCorrectMouseButton(PointerEventData eventData)
        {
            return triggerButton switch
            {
                MouseButton.Left => eventData.button == PointerEventData.InputButton.Left,
                MouseButton.Right => eventData.button == PointerEventData.InputButton.Right,
                MouseButton.Middle => eventData.button == PointerEventData.InputButton.Middle,
                _ => false,
            };
        }

        bool IsPointerOverMenuOrSubmenus()
        {
            if (menuInstance == null)
                return false;

            // Get all UI elements under the mouse
            pointerDataCache ??= new PointerEventData(EventSystem.current);
            pointerDataCache.position = Utilities.GetPointerPosition();

            raycastResultsCache.Clear();
            EventSystem.current.RaycastAll(pointerDataCache, raycastResultsCache);

            // Check if any of the hit elements belong to our menu or any submenu
            for (int i = 0; i < raycastResultsCache.Count; i++)
            {
                Transform current = raycastResultsCache[i].gameObject.transform;

                // Check if it's part of the main menu
                if (current.IsChildOf(menuInstance.transform))
                    return true;

                // Check all sections for submenus
                menuInstance.GetComponentsInChildren(sectionsCache);
                for (int j = 0; j < sectionsCache.Count; j++)
                {
                    var section = sectionsCache[j];
                    if (section.submenuInstance != null && current.IsChildOf(section.submenuInstance.transform))
                        return true;
                }
            }

            return false;
        }

        Vector3 GetOffsetVector(OffsetPosition type)
        {
            if (type == OffsetPosition.Custom)
                return (Vector3)customOffset;

            Vector3 offset = Vector3.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    offset = new Vector3(-offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.TopRight:
                    offset = new Vector3(offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.BottomLeft:
                    offset = new Vector3(-offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.BottomRight:
                    offset = new Vector3(offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.Top:
                    offset = new Vector3(0, offsetDistance, 0);
                    break;
                case OffsetPosition.Bottom:
                    offset = new Vector3(0, -offsetDistance, 0);
                    break;
                case OffsetPosition.Left:
                    offset = new Vector3(-offsetDistance, 0, 0);
                    break;
                case OffsetPosition.Right:
                    offset = new Vector3(offsetDistance, 0, 0);
                    break;
            }

            return offset;
        }

        Vector3 ClampToScreen(Vector3 position, RectTransform menuRect)
        {
            // Get menu size in pixels
            Vector2 size = GetScreenSize(menuRect);
            float width = size.x;
            float height = size.y;

            // Get pivot offsets
            float pivotOffsetX = width * menuRect.pivot.x;
            float pivotOffsetY = height * menuRect.pivot.y;

            // Calculate actual bounds of the menu in Screen Space
            float minX = position.x - pivotOffsetX;
            float maxX = position.x + (width - pivotOffsetX);
            float minY = position.y - pivotOffsetY;
            float maxY = position.y + (height - pivotOffsetY);

            // Adjust position if menu goes out of screen bounds
            if (minX < screenEdgePadding) { position.x += (screenEdgePadding - minX); }
            if (maxX > Screen.width - screenEdgePadding) { position.x -= (maxX - (Screen.width - screenEdgePadding)); }
            if (minY < screenEdgePadding) { position.y += (screenEdgePadding - minY); }
            if (maxY > Screen.height - screenEdgePadding) { position.y -= (maxY - (Screen.height - screenEdgePadding)); }

            return position;
        }

        Vector2 GetScreenSize(RectTransform rect)
        {
            Canvas canvas = ActiveCanvas;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return Vector2.Scale(rect.rect.size, rect.lossyScale);

            // For Camera/World modes, calculate screen projection
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            if (cam == null)
                return Vector2.Scale(rect.rect.size, rect.lossyScale); // Fallback

            rect.GetWorldCorners(cornersCache);

            // Calculate screen bounds from world corners
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, cornersCache[i]);
                if (screenPos.x < minX) { minX = screenPos.x; }
                if (screenPos.x > maxX) { maxX = screenPos.x; }
                if (screenPos.y < minY) { minY = screenPos.y; }
                if (screenPos.y > maxY) { maxY = screenPos.y; }
            }

            return new Vector2(maxX - minX, maxY - minY);
        }

        IEnumerator DetectOutsideClick()
        {
            // Skip the frame the menu was opened on to avoid the opening click being treated as an outside click
            yield return null;

            while (true)
            {
                // Check for ESC key to close menu
                if (IsVisible() && Utilities.WasEscapeKeyPressed())
                {
                    Hide();
                    yield return null;
                    continue;
                }

                // Check for any mouse button clicks
                if (IsVisible() && Utilities.WasPointerPressed())
                {
                    // Wait one frame to let UI events process
                    yield return null;

                    // Check if the click was outside the menu
                    if (!IsPointerOverMenuOrSubmenus()) { Hide(); }
                }

                yield return null;
            }
        }

        IEnumerator AnimateMenuIn()
        {
            if (menuInstance == null || menuInstance.canvasGroup == null)
                yield break;

            SetInitialAnimationState();

            float elapsedTime = 0f;
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = animationCurve.Evaluate(progress);

                ApplyAnimationState(curveValue, true);

                yield return null;
            }

            // Ensure final state
            ApplyAnimationState(1f, true);
            animationCoroutine = null;
        }

        IEnumerator AnimateMenuOut()
        {
            if (menuInstance == null || menuInstance.canvasGroup == null)
            {
                HideImmediate();
                yield break;
            }

            menuInstance.canvasGroup.blocksRaycasts = false;
            menuInstance.canvasGroup.interactable = false;

            float elapsedTime = 0f;
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = 1f - progress; // Reverse for out animation

                ApplyAnimationState(curveValue, false);

                yield return null;
            }

            HideImmediate();
        }

        static void HideAllActiveMenus()
        {
            for (int i = activeMenus.Count - 1; i >= 0; i--)
            {
                if (activeMenus[i] != null) { activeMenus[i].Hide(); }
                else { activeMenus.RemoveAt(i); }
            }
        }

        /// <summary>
        /// Finds a top-level menu item by its entry name.
        /// </summary>
        public Item GetItem(string itemName)
        {
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuItems[i].name == itemName)
                    return menuItems[i];
            }

            return null;
        }

        public Canvas ActiveCanvas
        {
            get
            {
                if (targetCanvas != null) { return targetCanvas; }
                else
                {
                    var tempCanvas = GetComponentInParent<Canvas>();
                    if (tempCanvas != null)
                    {
                        targetCanvas = tempCanvas;
                        return targetCanvas;
                    }
                    return Globals.GetCanvas();
                }
            }
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (!string.IsNullOrEmpty(menuItems[i].tableKey))
                    menuItems[i].name = localizedObject.GetString(menuItems[i].tableKey);
            }
        }
#endif


#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}