#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Runtime wiring and rectangle smoke checks in the real scene. The caller
/// must survive scene loading. Does not save, copy, download or apply a size.
/// These checks supplement, rather than replace, rendered visual QA.
/// </summary>
public static class WorkflowUIRegression
{
    const string SizePreferenceKey = "neospace.space-size.v1";

    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0;
        void Check(bool passed, string message)
        {
            checks++;
            if (!passed) failure("Workflow UI: " + message);
        }

        bool hadSizePreference = PlayerPrefs.HasKey(SizePreferenceKey);
        string originalSizePreference = PlayerPrefs.GetString(SizePreferenceKey, "");
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ConfiguratorScene.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return Frames(2); // The scene switch completes on the following frame.
        // AfterSceneLoad attributes run for application startup, not a later
        // scene load. Replay only this app's startup hooks before Start runs.
        ReplayApplicationBootstraps(failure);
        yield return Frames(5);

        // The redesign's own bake/wiring audit must hold in the merged scene
        // (dock, rail, mode switch, baked click targets).
        checks += UIWiringCheck.RunAll(out List<string> wiringFailures);
        foreach (string problem in wiringFailures)
            failure("Redesign UI wiring: " + problem);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        var build = Object.FindFirstObjectByType<BuildController>();
        var history = Object.FindFirstObjectByType<BuildHistory>();
        var workflow = Object.FindFirstObjectByType<WorkflowReviewUI>();
        Transform rail = canvas != null ? UIChrome.Rail(canvas.transform) : null;
        Check(canvas != null && build != null && history != null && workflow != null && rail != null,
            $"The real scene must have canvas ({canvas != null}), build ({build != null}), history ({history != null}), workflow ({workflow != null}) and utility rail ({rail != null}) wiring.");
        if (canvas == null || build == null || history == null || workflow == null || rail == null)
            yield break;
        RectTransform canvasRect = (RectTransform)canvas.transform;

        // Operate only the loaded runtime scene. No scene asset is saved.
        if (SpaceModeController.Active)
            Object.FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
        history.ClearAll();
        yield return Frames(5);

        Transform spaceItem = UIChrome.FindButton(canvas.transform, "Btn_SpaceSize");
        Check(spaceItem != null && spaceItem.IsChildOf(rail) && spaceItem.GetComponent<Button>() != null,
            "Set space size must be connected to the real utility rail.");
        if (spaceItem == null) yield break;
        Check(spaceItem.GetComponent<RailButtonVisual>()?.hint == "Set space size" &&
            spaceItem.GetComponentInChildren<TMP_Text>(true)?.text == "Size",
            "The space size rail button must carry its label and hint.");
        Canvas.ForceUpdateCanvases();
        Check(WithinCanvas((RectTransform)rail, canvasRect), "The utility rail with the workflow rows must fit within the canvas.");
        var planningControls = WorkspaceControls();
        spaceItem.GetComponent<Button>().onClick.Invoke();
        yield return Frames(2);
        Check(SpacePlanningUI.IsOpen, "Selecting Set space size must open the planning dialog.");
        Check(AllPaused(planningControls), "The planning dialog must pause enabled workspace input.");

        Transform planning = canvas.transform.Find("SpacePlanningUI");
        RectTransform planningDialog = planning != null ? planning.Find("SpaceSizeDialog") as RectTransform : null;
        RectTransform fitCard = planning != null ? planning.Find("SpaceFitReadout") as RectTransform : null;
        Canvas.ForceUpdateCanvases();
        Check(planningDialog != null && WithinCanvas(planningDialog, canvasRect), "The planning dialog must fit within the current canvas.");
        Check(fitCard != null && WithinCanvas(fitCard, canvasRect), "The fit card rectangle must fit within the current canvas.");
        Check(planningDialog != null && planningDialog.Find("WidthInput") != null &&
            planningDialog.Find("DepthInput") != null && planningDialog.Find("HeightInput") != null,
            "The planning dialog must provide width, depth and height fields.");
        if (planningDialog != null)
        {
            // Invalid input must remain in the dialog and avoid persistence.
            planningDialog.Find("WidthInput").GetComponent<TMP_InputField>().text = "0";
            planningDialog.Find("Btn_Apply").GetComponent<Button>().onClick.Invoke();
            Check(SpacePlanningUI.IsOpen, "An invalid size must not close the dialog.");
            Check(planningDialog.Find("Feedback").GetComponent<TMP_Text>().text.Contains("1 to 50,000"),
                "Invalid size input must show an actionable validation error.");
        }
        SpacePlanningUI.Hide();
        yield return null;
        Check(!SpacePlanningUI.IsOpen, "Hide must close the planning dialog.");
        Check(Restored(planningControls), "Closing the planning dialog must restore previous input states.");

        var guideControls = WorkspaceControls();
        workflow.ShowGuide();
        yield return Frames(2);
        RectTransform review = canvas.transform.Find("WorkflowReview") as RectTransform;
        Check(review != null && review.gameObject.activeSelf, "The build/save guide must open.");
        Check(AllPaused(guideControls), "The guide must pause workspace input.");
        if (review == null) yield break;
        Canvas.ForceUpdateCanvases();
        Check(WithinCanvas(review, canvasRect), "The guide rectangle must fit within the current canvas.");
        var body = review.Find("ReviewText")?.GetComponent<TMP_InputField>();
        Check(body != null && body.readOnly && body.text.Contains("1. SET YOUR SPACE") && body.text.Contains("4. SAVE"),
            "The guide must contain the size and save journey in a read-only field.");
        Check(review.Find("Download .txt") != null && !review.Find("Download .txt").gameObject.activeSelf,
            "The guide must not expose the quote download action.");
        review.Find("Close").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Check(!review.gameObject.activeSelf && Restored(guideControls), "Closing the guide must hide it and restore input states.");

        var placement = build.PlacePartsBatch(new List<TemplatePartPose>
        {
            new TemplatePartPose("V9", Vector3.zero, Quaternion.identity, "workflow smoke")
        }, seatVerticalsOnFloor: false, validateOverlap: false);
        Check(placement.Placed == 1, "The real scene catalogue must place a V9 for quote review.");
        yield return Frames(3);
        var partsControls = WorkspaceControls();
        Transform partsItem = UIChrome.FindButton(canvas.transform, "Btn_PartsPrices");
        Check(partsItem != null && partsItem.IsChildOf(rail) && partsItem.GetComponent<Button>() != null,
            "Parts & prices must be connected to the real utility rail.");
        if (partsItem != null)
            partsItem.GetComponent<Button>().onClick.Invoke();
        else
            workflow.ShowParts();
        yield return Frames(2);
        var stats = Object.FindFirstObjectByType<UIBuildStats>();
        Check(review.gameObject.activeSelf && review.Find("Title").GetComponent<TMP_Text>().text == "Parts & prices",
            "Parts & prices must open its named dialog.");
        Check(AllPaused(partsControls), "The parts dialog must pause workspace input.");
        Check(stats != null && body != null && body.readOnly &&
            body.text == QuoteSummary.CreateParts(stats.Summary),
            "The parts dialog must use the same current BOM snapshot as the stats pill.");
        Check(body != null && body.text.Contains("V9 x 1 | AUD 35.00 each | AUD 35.00") &&
            body.text.Contains("Placeholder estimates"), "The parts dialog must show actual quantities and identify placeholder prices.");
        Check(review.Find("Copy text") != null && !review.Find("Download .txt").gameObject.activeSelf,
            "The parts dialog must support copying without exposing the quote download action.");
        review.Find("Close").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Check(!review.gameObject.activeSelf && Restored(partsControls), "Closing the parts dialog must restore previous input states.");

        // Raycast the visible pill, then dispatch the same left-click interface
        // used by the event system. This also catches a blocked raycast target.
        // The redesign keeps UIBuildStats as a service without its own pill
        // (the total moves to the Checkout tab); then the rail's Parts row is
        // the click path and gets the same raycast treatment.
        var pillControls = WorkspaceControls();
        Canvas.ForceUpdateCanvases();
        bool pillHit = false;
        RectTransform pill = stats != null && stats.GetComponent<Graphic>() != null && stats.gameObject.activeInHierarchy
            ? stats.transform as RectTransform
            : partsItem as RectTransform;
        if (pill != null && EventSystem.current != null)
        {
            var click = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, pill.TransformPoint(pill.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(click, hits);
            pillHit = hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(pill);
            if (pillHit) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, click, ExecuteEvents.pointerClickHandler);
        }
        Check(pillHit, "The visible parts control must receive the pointer raycast at its centre.");
        yield return Frames(2);
        Check(review.gameObject.activeSelf && body != null && stats != null &&
            body.text == QuoteSummary.CreateParts(stats.Summary),
            "Left-clicking the stats pill must open Parts & prices with the shared current BOM.");
        Check(AllPaused(pillControls), "Opening parts through the pill must pause workspace input.");
        Canvas.ForceUpdateCanvases();
        Check(WithinCanvas(review, canvasRect), "The Parts & prices dialog must fit within the canvas.");
        // WaitForEndOfFrame requires a rendered editor; batch validation still
        // runs all wiring checks and leaves capture to the rendered smoke run.
        if (review.gameObject.activeSelf && !Application.isBatchMode &&
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory("Logs");
            ScreenCapture.CaptureScreenshot("Logs/parts-prices-ui.png");
            yield return Frames(2);
        }
        review.Find("Close").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Check(!review.gameObject.activeSelf && Restored(pillControls), "Closing parts opened through the pill must restore input.");

        var quoteControls = WorkspaceControls();
        workflow.ShowQuote();
        yield return Frames(2);
        Check(review.gameObject.activeSelf, "Prepare quote must open for the placed V9.");
        Check(AllPaused(quoteControls), "The quote must pause workspace input.");
        Check(body != null && body.text.Contains("V9 x 1 | AUD 35.00 each | AUD 35.00") &&
            stats != null && body.text.Contains("Estimated total: AUD " + stats.Summary.TotalPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
            "The quote must show the actual V9 quantity and the same total as the shared BOM.");
        Check(body != null && body.text.Contains("NS1-") && body.text.Contains("Estimate only.") &&
            body.text.Contains("Placeholder prices") && body.text.Contains("Not included: delivery"),
            "The quote must include its portable configuration code and pricing limitations.");
        Check(review.Find("Download .txt").gameObject.activeSelf,
            "Quote review must expose its download button (without invoking it).");
        Canvas.ForceUpdateCanvases();
        Check(WithinCanvas(review, canvasRect), "The quote rectangle must fit within the current canvas.");
        Check(body != null && WithinParent((RectTransform)body.transform, review),
            "The quote text viewport must fit within its dialog.");
        Check(WithinParent((RectTransform)review.Find("Close"), review) &&
            WithinParent((RectTransform)review.Find("Download .txt"), review),
            "Quote close and download controls must fit within their dialog.");
        review.Find("Close").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Check(!review.gameObject.activeSelf && Restored(quoteControls), "Closing quote review must restore all previous input states.");
        Check(PlayerPrefs.HasKey(SizePreferenceKey) == hadSizePreference &&
            PlayerPrefs.GetString(SizePreferenceKey, "") == originalSizePreference,
            "Opening/closing dialogs and rejecting invalid input must leave the size preference unchanged.");
        Debug.Log($"Workflow UI smoke: {checks} checks executed at {Screen.width} × {Screen.height}; rectangle/wiring checks only.");
    }

    static void ReplayApplicationBootstraps(Action<string> failure)
    {
        foreach (Type type in typeof(BuildController).Assembly.GetTypes())
        {
            if (type == typeof(ConfiguratorValidationHost)) continue;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                if (attribute == null || attribute.loadType != RuntimeInitializeLoadType.AfterSceneLoad) continue;
                try { method.Invoke(null, null); }
                catch (Exception e) { failure("Workflow bootstrap " + type.Name + "." + method.Name + ": " + (e.InnerException ?? e)); }
            }
        }
    }

    static Dictionary<Behaviour, bool> WorkspaceControls()
    {
        var states = new Dictionary<Behaviour, bool>();
        foreach (MonoBehaviour control in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (control is FlyCameraController || control is CadCameraController || control is OrbitCamera ||
                control is ConfiguratorCameraController || control is BuildController || control is GhostController ||
                control is FreePartSession || control is TemplateSession || control is SelectionManager ||
                control is MarqueeSelectionController || control is StructureClipboard || control is BeamResizeSession ||
                control is PanelLayerMover || control is BuildHistory || control is SpaceModeController ||
                control is SpaceInteractionController)
                states.Add(control, control.enabled);
        }
        return states;
    }

    static bool AllPaused(Dictionary<Behaviour, bool> states)
    {
        foreach (var entry in states) if (entry.Key != null && entry.Key.enabled) return false;
        return states.Count > 0;
    }

    static bool Restored(Dictionary<Behaviour, bool> states)
    {
        foreach (var entry in states) if (entry.Key == null || entry.Key.enabled != entry.Value) return false;
        return true;
    }

    static bool WithinCanvas(RectTransform child, RectTransform canvas) => WithinParent(child, canvas);

    static bool WithinParent(RectTransform child, RectTransform parent)
    {
        if (child == null || parent == null) return false;
        Vector3[] corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Rect bounds = parent.rect;
        foreach (Vector3 corner in corners)
        {
            Vector3 point = parent.InverseTransformPoint(corner);
            if (point.x < bounds.xMin - 1f || point.x > bounds.xMax + 1f ||
                point.y < bounds.yMin - 1f || point.y > bounds.yMax + 1f) return false;
        }
        return child.rect.width > 0f && child.rect.height > 0f;
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
#endif
