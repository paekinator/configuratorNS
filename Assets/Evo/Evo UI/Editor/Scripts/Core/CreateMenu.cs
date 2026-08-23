using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    public class CreateMenu : Editor
    {
        static string objectPath;
        static bool isPathCached;

        const int TopMenuOrder = 7;
        const int MenuOrder = 20;
        const string MenuPrefix = "GameObject/Evo UI/";

        static void GetObjectPath()
        {
            // Return cached path if available
            if (isPathCached && !string.IsNullOrEmpty(objectPath))
                return;

            // Try primary method first
            var stylerAsset = Resources.Load(Constants.StylerFallbackPath);
            if (stylerAsset != null)
            {
                objectPath = AssetDatabase.GetAssetPath(stylerAsset);
                objectPath = objectPath.Replace($"Resources/{Constants.StylerFallbackPath}.asset", "").TrimEnd('/') + "/Prefabs/";
                isPathCached = true;
                return;
            }

            // Fallback: Search for any Evo UI folder structure
            string[] folders = AssetDatabase.FindAssets("Evo UI t:folder");
            foreach (string guid in folders)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Evo UI") || path.EndsWith("Evo UI/"))
                {
                    objectPath = path + "/Prefabs/";
                    isPathCached = true;
                    return;
                }
            }

            // Return to default if no fallback
            objectPath = "Assets/Evo/Evo UI/Prefabs/";
            isPathCached = true;
        }

        static Canvas GetCanvas()
        {
#if UNITY_6000_4_OR_NEWER
            var tCanvas = FindAnyObjectByType<Canvas>();
            if (tCanvas != null) { return FindAnyObjectByType<Canvas>(); }
#elif UNITY_2023_2_OR_NEWER
            var tCanvas = FindFirstObjectByType<Canvas>();
            if (tCanvas != null) { return FindFirstObjectByType<Canvas>(); }
#else
            var canvases = FindObjectsOfType<Canvas>();
            if (canvases.Length > 0) { return canvases[0]; }
#endif

            // Create if not found
            var canvasGO = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasGO.GetComponent<Canvas>();
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Set options
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            scaler.matchWidthOrHeight = 0.5f;

            // Create EventSystem if not present
#if UNITY_6000_4_OR_NEWER
            if (FindAnyObjectByType<EventSystem>() == null)
#elif UNITY_2023_2_OR_NEWER
            if (FindFirstObjectByType<EventSystem>() == null)
#else
            if (FindObjectOfType<EventSystem>() == null)
#endif
            {
#if ENABLE_INPUT_SYSTEM
                var eventGO = new GameObject("Event System", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
                var eventGO = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
                Undo.RegisterCreatedObjectUndo(eventGO, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            Selection.activeObject = canvasGO;

            return canvas;
        }

        static GameObject CreateObject(string path)
        {
            GetObjectPath();
            string fullPath = objectPath + path + ".prefab";

            // Check if prefab exists before trying to load
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Evo UI", $"Prefab not found at: {fullPath}\nPlease check your Evo UI installation.", "OK");
                return null;
            }

            GameObject clone = Instantiate(AssetDatabase.LoadAssetAtPath(fullPath, typeof(GameObject)), Vector3.zero, Quaternion.identity) as GameObject;
            clone.name = clone.name.Replace("(Clone)", "").Trim();

            if (Selection.activeGameObject != null) { clone.transform.SetParent(Selection.activeGameObject.transform, false); }
            else { clone.transform.SetParent(GetCanvas().transform, false); }
 
            Undo.RegisterCreatedObjectUndo(clone, $"Create {clone.name}");
            Selection.activeObject = clone;

            return clone;
        }

        [MenuItem(MenuPrefix + "Animated/Counter", false, MenuOrder)]
        static void CreateCounter() => CreateObject("Animated/Counter");

        [MenuItem(MenuPrefix + "Button/Default", false, MenuOrder)]
        static void CreateButton() => CreateObject("Button/Button");

        [MenuItem(MenuPrefix + "Button/Rounded", false, MenuOrder)]
        static void CreateButtonRounded() => CreateObject("Button/Button (Rounded)");

        [MenuItem(MenuPrefix + "Button/Rectangle", false, MenuOrder)]
        static void CreateButtonRectangle() => CreateObject("Button/Button (Rectangle)");

        [MenuItem(MenuPrefix + "Button/Default (Gradient)", false, MenuOrder)]
        static void CreateButtonGradientD() => CreateObject("Button/Button (Gradient)");

        [MenuItem(MenuPrefix + "Button/Rectangle (Gradient)", false, MenuOrder)]
        static void CreateButtonGradientR() => CreateObject("Button/Button (Rectangle Gradient)");

        [MenuItem(MenuPrefix + "Button/Rounded (Gradient)", false, MenuOrder)]
        static void CreateButtonGradientRo() => CreateObject("Button/Button (Rounded Gradient)");

        [MenuItem(MenuPrefix + "Button/Default (Outline)", false, MenuOrder)]
        static void CreateButtonOutlineD() => CreateObject("Button/Button (Outline)");

        [MenuItem(MenuPrefix + "Button/Rectangle (Outline)", false, MenuOrder)]
        static void CreateButtonOutlineR() => CreateObject("Button/Button (Rectangle Outline)");

        [MenuItem(MenuPrefix + "Button/Rounded (Outline)", false, MenuOrder)]
        static void CreateButtonOutlineRo() => CreateObject("Button/Button (Rounded Outline)");

        [MenuItem(MenuPrefix + "Button/Icon Only", false, MenuOrder)]
        static void CreateButtonIO() => CreateObject("Button/Button (Icon Only)");

        [MenuItem(MenuPrefix + "Button/Icon Sway", false, MenuOrder)]
        static void CreateButtonIS() => CreateObject("Button/Button (Icon Sway)");  

        [MenuItem(MenuPrefix + "Button/Radio Button", false, MenuOrder)]
        static void CreateRadioButton() => CreateObject("Button/Radio Button");

        [MenuItem(MenuPrefix + "Button/Radio Button (Alt)", false, MenuOrder)]
        static void CreateRadioButtonAlt() => CreateObject("Button/Radio Button (Alt)");

        [MenuItem(MenuPrefix + "Button/Radio Button Group", false, MenuOrder)]
        static void CreateRadioButtonGroup() => CreateObject("Button/Radio Button Group");

        [MenuItem(MenuPrefix + "Button/Radio Button Group (Alt)", false, MenuOrder)]
        static void CreateRadioButtonGroupAlt() => CreateObject("Button/Radio Button Group (Alt)");

        [MenuItem(MenuPrefix + "Button/Progress Button", false, MenuOrder)]
        static void CreateProgressButton() => CreateObject("Button/Progress Button");

        [MenuItem(MenuPrefix + "Button/Progress Button (Alt)", false, MenuOrder)]
        static void CreateProgressButtonAlt() => CreateObject("Button/Progress Button (Alt)");

        [MenuItem(MenuPrefix + "Carousel/Default", false, MenuOrder)]
        static void CreateCarousel() => CreateObject("Carousel/Carousel");

        [MenuItem(MenuPrefix + "Carousel/Alternative", false, MenuOrder)]
        static void CreateCarouselAlt() => CreateObject("Carousel/Carousel (Alternative)");

        [MenuItem(MenuPrefix + "Charts/Horizontal Chart", false, MenuOrder)]
        static void CreateHorizontalChart() => CreateObject("Charts/Horizontal Chart");

        [MenuItem(MenuPrefix + "Charts/Line Chart", false, MenuOrder)]
        static void CreateLineChart() => CreateObject("Charts/Line Chart");

        [MenuItem(MenuPrefix + "Charts/Pie Chart", false, MenuOrder)]
        static void CreatePieChart() => CreateObject("Charts/Pie Chart");

        [MenuItem(MenuPrefix + "Charts/Radar Chart", false, MenuOrder)]
        static void CreateRadarChart() => CreateObject("Charts/Radar Chart");

        [MenuItem(MenuPrefix + "Charts/Vertical Chart", false, MenuOrder)]
        static void CreateVerticalChart() => CreateObject("Charts/Vertical Chart");

        [MenuItem(MenuPrefix + "Color Picker/Compact", false, MenuOrder)]
        static void CreateColorPickerC() => CreateObject("Color Picker/Color Picker (Compact)");

        [MenuItem(MenuPrefix + "Color Picker/Square", false, MenuOrder)]
        static void CreateColorPickerS() => CreateObject("Color Picker/Color Picker (Square)");

        [MenuItem(MenuPrefix + "Color Picker/Wheel", false, MenuOrder)]
        static void CreateColorPickerW() => CreateObject("Color Picker/Color Picker (Wheel)");

        [MenuItem(MenuPrefix + "Date and Time/Calendar", false, MenuOrder)]
        static void CreateDatePicker() => CreateObject("Date & Time/Calendar");

        [MenuItem(MenuPrefix + "Date and Time/Countdown", false, MenuOrder)]
        static void CreateCountdown() => CreateObject("Date & Time/Countdown");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Horizontal)", false, MenuOrder)]
        static void CreateTimerH() => CreateObject("Timer/Timer (Horizontal)");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Vertical)", false, MenuOrder)]
        static void CreateTimerV() => CreateObject("Timer/Timer (Vertical)");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Radial)", false, MenuOrder)]
        static void CreateTimerR() => CreateObject("Timer/Timer (Radial)");

        [MenuItem(MenuPrefix + "Dropdown/Dropdown", false, MenuOrder)]
        static void CreateDropdown() => CreateObject("Dropdown/Dropdown");

        [MenuItem(MenuPrefix + "Dropdown/Input (Combo Box)", false, MenuOrder)]
        static void CreateDropdownInput() => CreateObject("Dropdown/Dropdown (Input)");

        [MenuItem(MenuPrefix + "Dropdown/Multi Select", false, MenuOrder)]
        static void CreateDropdownMulti() => CreateObject("Dropdown/Dropdown (Multi Select)");

        [MenuItem(MenuPrefix + "Input Field/Default", false, MenuOrder)]
        static void CreateInputField() => CreateObject("Input Field/Input Field");

        [MenuItem(MenuPrefix + "Input Field/With Icon", false, MenuOrder)]
        static void CreateInputFieldWithIcon() => CreateObject("Input Field/Input Field (With Icon)");

        [MenuItem(MenuPrefix + "Input Field/Line", false, MenuOrder)]
        static void CreateInputFieldLine() => CreateObject("Input Field/Input Field (Line)");

        [MenuItem(MenuPrefix + "Input Field/Multi Line", false, MenuOrder)]
        static void CreateInputFieldMultiLine() => CreateObject("Input Field/Input Field (Multi Line)");

        [MenuItem(MenuPrefix + "Input Field/Multi Line (Scrollbar)", false, MenuOrder)]
        static void CreateInputFieldMultiLineScrollbar() => CreateObject("Input Field/Input Field (Multi Line Scrollbar)");

        [MenuItem(MenuPrefix + "List View/Default", false, MenuOrder)]
        static void CreateListView() => CreateObject("List View/List View");

        [MenuItem(MenuPrefix + "List View/Masked", false, MenuOrder)]
        static void CreateListViewMasked() => CreateObject("List View/List View (Masked)");

        [MenuItem(MenuPrefix + "Menu Bar/Menu Bar Preset", false, MenuOrder)]
        static void CreateMenuBar() => CreateObject("Menu Bar/Menu Bar Preset");

        [MenuItem(MenuPrefix + "Modal Window/Default", false, MenuOrder)]
        static void CreateModalWindow() => CreateObject("Modal Window/Modal Window");

        [MenuItem(MenuPrefix + "Modal Window/Alternative", false, MenuOrder)]
        static void CreateModalWindowAlt() => CreateObject("Modal Window/Modal Window (Alternative)");

        [MenuItem(MenuPrefix + "Notification/Default", false, MenuOrder)]
        static void CreateNotification() => CreateObject("Notification/Notification");

        [MenuItem(MenuPrefix + "Pages/Horizontal", false, MenuOrder)]
        static void CreatePages() => CreateObject("Pages/Pages Preset");

        [MenuItem(MenuPrefix + "Pages/Horizontal (Alt)", false, MenuOrder)]
        static void CreatePagesAlt() => CreateObject("Pages/Pages Preset (Alt)");

        [MenuItem(MenuPrefix + "Pages/Vertical", false, MenuOrder)]
        static void CreatePagesVertical() => CreateObject("Pages/Pages Preset (Vertical)");

        [MenuItem(MenuPrefix + "Pages/Vertical (Alt)", false, MenuOrder)]
        static void CreatePagesVerticalAlt() => CreateObject("Pages/Pages Preset (Vertical Alt)");

        [MenuItem(MenuPrefix + "Progress Bar/Horizontal", false, MenuOrder)]
        static void CreateProgressBarH() => CreateObject("Progress Bar/Progress Bar (Horizontal)");

        [MenuItem(MenuPrefix + "Progress Bar/Vertical", false, MenuOrder)]
        static void CreateProgressBarV() => CreateObject("Progress Bar/Progress Bar (Vertical)");

        [MenuItem(MenuPrefix + "Progress Bar/Radial", false, MenuOrder)]
        static void CreateProgressBarR() => CreateObject("Progress Bar/Progress Bar (Radial)");

        [MenuItem(MenuPrefix + "Scrollbar/Horizontal", false, MenuOrder)]
        static void CreateScrollbarH() => CreateObject("Scrollbar/Scrollbar (Horizontal)");

        [MenuItem(MenuPrefix + "Scrollbar/Vertical", false, MenuOrder)]
        static void CreateScrollbarV() => CreateObject("Scrollbar/Scrollbar (Vertical)");

        [MenuItem(MenuPrefix + "Selector/Horizontal", false, MenuOrder)]
        static void CreateSelectorH() => CreateObject("Selector/Selector (Horizontal)");

        [MenuItem(MenuPrefix + "Selector/Vertical", false, MenuOrder)]
        static void CreateSelectorV() => CreateObject("Selector/Selector (Vertical)");

        [MenuItem(MenuPrefix + "Selector/Quantity", false, MenuOrder)]
        static void CreateSelectorQuantity() => CreateObject("Selector/Selector (Quantity)");

        [MenuItem(MenuPrefix + "Showcase Panel/Default", false, MenuOrder)]
        static void CreateShowcasePanel() => CreateObject("Showcase Panel/Showcase Panel");

        [MenuItem(MenuPrefix + "Slider/Horizontal", false, MenuOrder)]
        static void CreateSliderH() => CreateObject("Slider/Slider (Horizontal)");

        [MenuItem(MenuPrefix + "Slider/Horizontal (Input)", false, MenuOrder)]
        static void CreateSliderHI() => CreateObject("Slider/Slider (Horizontal Input)");

        [MenuItem(MenuPrefix + "Slider/Horizontal (Popup Value)", false, MenuOrder)]
        static void CreateSliderHP() => CreateObject("Slider/Slider (Horizontal Popup Value)");

        [MenuItem(MenuPrefix + "Slider/Vertical", false, MenuOrder)]
        static void CreateSliderV() => CreateObject("Slider/Slider (Vertical)");

        [MenuItem(MenuPrefix + "Slider/Vertical (Input)", false, MenuOrder)]
        static void CreateSliderVI() => CreateObject("Slider/Slider (Vertical Input)");

        [MenuItem(MenuPrefix + "Slider/Vertical (Popup Value)", false, MenuOrder)]
        static void CreateSliderVP() => CreateObject("Slider/Slider (Vertical Popup Value)");

        [MenuItem(MenuPrefix + "Slider/Radial", false, MenuOrder)]
        static void CreateSliderR() => CreateObject("Slider/Slider (Radial)");

        [MenuItem(MenuPrefix + "Slider/Radial (Knob)", false, MenuOrder)]
        static void CreateSliderRK() => CreateObject("Slider/Slider (Radial Knob)");

        [MenuItem(MenuPrefix + "Spinner/Horizontal", false, MenuOrder)]
        static void CreateSpinnerH() => CreateObject("Spinner/Spinner (Horizontal)");

        [MenuItem(MenuPrefix + "Spinner/Vertical", false, MenuOrder)]
        static void CreateSpinnerV() => CreateObject("Spinner/Spinner (Vertical)");

        [MenuItem(MenuPrefix + "Spinner/Radial", false, MenuOrder)]
        static void CreateSpinnerR() => CreateObject("Spinner/Spinner (Radial)");

        [MenuItem(MenuPrefix + "Styler Objects/Image", false, TopMenuOrder)]
        static void CreateStylerObjImage()
        {
            GameObject obj = CreateObject("Styler Objects/Image");
           
            if (obj != null && obj.TryGetComponent(out StylerObject stObj))
                stObj.Preset = Styler.GetDefaultPreset(false, true);
        }

        [MenuItem(MenuPrefix + "Styler Objects/Procedural Rect", false, TopMenuOrder)]
        static void CreateStylerObjPR()
        {
            GameObject obj = CreateObject("Styler Objects/Procedural Rect");
            
            if (obj != null && obj.TryGetComponent(out StylerObject stObj))
                stObj.Preset = Styler.GetDefaultPreset(false, true);
        }

        [MenuItem(MenuPrefix + "Styler Objects/TMP Text", false, TopMenuOrder)]
        static void CreateStylerObjTMP()
        {
            GameObject obj = CreateObject("Styler Objects/TMP Text");
            
            if (obj != null && obj.TryGetComponent(out StylerObject stObj))
                stObj.Preset = Styler.GetDefaultPreset(false, true);
        }

        [MenuItem(MenuPrefix + "Switch/Default", false, MenuOrder)]
        static void CreateSwitch() => CreateObject("Switch/Switch");

        [MenuItem(MenuPrefix + "Switch/With Indicator", false, MenuOrder)]
        static void CreateSwitchWI() => CreateObject("Switch/Switch (Indicator)");

        [MenuItem(MenuPrefix + "Switch/With Label", false, MenuOrder)]
        static void CreateSwitchWL() => CreateObject("Switch/Switch (Label)");

        [MenuItem(MenuPrefix + "Switch/With Indicator + Label", false, MenuOrder)]
        static void CreateSwitchWIL() => CreateObject("Switch/Switch (Indicator + Label)");

        [MenuItem(MenuPrefix + "Tabs/Tabs Preset", false, MenuOrder)]
        static void CreateTabs() => CreateObject("Tabs/Tabs Preset");

        [MenuItem(MenuPrefix + "Tabs/Tabs Preset (Down)", false, MenuOrder)]
        static void CreateTabsDown() => CreateObject("Tabs/Tabs Preset (Down)");

        [MenuItem(MenuPrefix + "Tabs/Tabs Preset (Left)", false, MenuOrder)]
        static void CreateTabsLeft() => CreateObject("Tabs/Tabs Preset (Left)");

        [MenuItem(MenuPrefix + "Tabs/Tabs Preset (Right)", false, MenuOrder)]
        static void CreateTabsRight() => CreateObject("Tabs/Tabs Preset (Right)");

        [MenuItem(MenuPrefix + "Tabs/Tab Button", false, MenuOrder)]
        static void CreateTabButton() => CreateObject("Tabs/Tab Button");
    }
}