#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Real Space merges and supplied finish meshes, with only pointer gesture
/// state injected. Runs after SpacePartSummaryRegression in the isolated scene.
/// No saved files, assets, preferences or external input are changed.
/// </summary>
public static class FinishSpaceLifecycleRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            failure("Finish Space lifecycle: " + label);
        }

        var mode = Object.FindFirstObjectByType<SpaceModeController>();
        var finish = FinishController.Ensure();
        Check(mode != null && mode.interaction != null && mode.history != null,
            "Space fixture requires the configured scene");
        if (mode == null || mode.interaction == null || mode.history == null) yield break;
        var interaction = mode.interaction;
        Check(!SpaceModeController.Active && interaction.InstanceCount == 0,
            "fixture begins in Build mode with an empty space");
        if (SpaceModeController.Active || interaction.InstanceCount != 0) yield break;

        var owned = new List<GameObject>();
        bool previousFinish = finish.IsOn, previousEnabled = interaction.enabled;
        SpaceInstance post = null;
        Vector3 committedPosition = Vector3.zero;
        try
        {
            finish.SetOn(false, announce: false);
            mode.EnterSpaceMode();
            for (int i = 0; i < 5; i++) yield return null;
            Check(SpaceModeController.Active && !finish.IsOn, "Space finishing is effective with the Build toggle off");
            if (!SpaceModeController.Active) yield break;

            post = Piece("Post");
            Box(post.transform, "V9", new Vector3(0, 372.5f, 0), new Vector3(41, 745, 41), Quaternion.identity);
            Merge();
            finish.RefreshNow();
            Check(Count("Veneer H7") == 4, "bare V9 baseline installs its four full-length veneers");

            var ghost = new GameObject("PieceGhost");
            owned.Add(ghost);
            Box(ghost.transform, "V9", new Vector3(3000, 372.5f, 0), new Vector3(41, 745, 41), Quaternion.identity);
            SetInput("_ghost", ghost);
            Check(!interaction.Busy, "an armed placement preview does not hold finishing");

            var shelfPiece = Piece("Shelf");
            float edge = PanelFill.EdgeMm(7);
            GameObject shelf = Box(shelfPiece.transform, "Panel shelf", new Vector3(352, 393, 352),
                new Vector3(edge, edge, 1), Quaternion.Euler(90, 0, 0));
            var oldParts = Parts();
            Merge();
            Check(AllInactive(oldParts) && Count("Veneer H7") < 4,
                "committing a shelf while placement stays armed immediately replaces crossing long veneers");
            // Settle the old implementation too, so later checks identify their
            // own defect instead of cascading from the notification failure.
            finish.RefreshNow();
            Check(Count("Veneer H7") < 4 && NoOverlap(shelf.GetComponent<Renderer>().bounds),
                "actual installed finish respects the 1 mm shelf after a resolved merge");

            var overlapPiece = Piece("OverlappingShelf");
            Box(overlapPiece.transform, "Panel overlapping shelf", new Vector3(652, 393, 352),
                new Vector3(edge, edge, 1), Quaternion.Euler(90, 0, 0));
            oldParts = Parts();
            Merge();
            int derivedPanels = 0;
            if (SpaceMerge.DerivedRoot != null)
                foreach (Transform part in SpaceMerge.DerivedRoot)
                    if (part.gameObject.activeInHierarchy && part.name.Contains("Panel")) derivedPanels++;
            Check(derivedPanels == 1, "overlapping boards use a real merge-derived replacement strip");
            Check(AllInactive(oldParts), "a second placement refreshes finishing after the panel split with the ghost still armed");
            finish.RefreshNow();
            Check(NoOverlap(shelf.GetComponent<Renderer>().bounds), "finishing remains clear of the shelf after the panel split");
            SetInput("_ghost", null);
            ghost.SetActive(false);

            committedPosition = post.transform.position;
            BeginDrag();
            interaction.DeselectAll();
            Check(!interaction.Busy && post.transform.position == committedPosition && InputArraysEmpty(),
                "abandoning a drag through deselection restores the last accepted pose and releases finishing");
            ResetInput();

            BeginDrag();
            interaction.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            Check(!interaction.Busy && post.transform.position == committedPosition && InputArraysEmpty(),
                "losing focus cancels an unfinished drag without latching Busy");
            ResetInput();

            BeginDrag();
            interaction.enabled = false;
            Check(!interaction.Busy && post.transform.position == committedPosition && InputArraysEmpty(),
                "disabling the interaction controller cancels its unfinished drag");
            ResetInput();
            interaction.enabled = previousEnabled;

            // R uses RotateSelection even while the mouse is held. Rotation
            // must start from the accepted pose so a later cancellation cannot
            // silently move it away from the state recorded in history.
            Quaternion originalRotation = post.transform.rotation;
            mode.history.ResetBaseline(interaction.CurrentStates());
            BeginDrag();
            interaction.RotateSelection();
            Quaternion rotated = post.transform.rotation;
            Check(!interaction.Busy && post.transform.position == committedPosition &&
                Quaternion.Angle(originalRotation, rotated) > 89f,
                "R during a drag cancels the temporary translation before rotating");
            interaction.DeselectAll();
            Check(post.transform.position == committedPosition && Quaternion.Angle(post.transform.rotation, rotated) < 0.01f,
                "deselecting after the interrupted-drag rotation leaves the committed pose unchanged");
            List<SpaceHistory.InstanceState> rotationUndo = mode.history.CanUndo ? mode.history.Undo() : null;
            List<SpaceHistory.InstanceState> rotationRedo = mode.history.CanRedo ? mode.history.Redo() : null;
            Check(rotationUndo != null && rotationRedo != null &&
                rotationUndo[0].Position == committedPosition && rotationRedo[0].Position == post.transform.position &&
                Mathf.Abs(Mathf.DeltaAngle(rotationUndo[0].YawDegrees, originalRotation.eulerAngles.y)) < 0.01f &&
                Mathf.Abs(Mathf.DeltaAngle(rotationRedo[0].YawDegrees, post.transform.eulerAngles.y)) < 0.01f,
                "interrupted-drag rotation undo and redo match the original and live poses");
            post.transform.rotation = originalRotation;
            ResetInput();
            Merge();

            // Ctrl+D previously cloned at the temporary dragged pose, then
            // DeselectAll rolled its source back underneath that new copy.
            var beforeCopies = new HashSet<SpaceInstance>(interaction.Instances);
            mode.history.ResetBaseline(interaction.CurrentStates());
            BeginDrag();
            interaction.DuplicateSelection();
            var copies = new List<SpaceInstance>();
            foreach (SpaceInstance instance in interaction.Instances)
                if (!beforeCopies.Contains(instance)) copies.Add(instance);
            Check(!interaction.Busy && post.transform.position == committedPosition && copies.Count == 1,
                "Ctrl+D during a drag cancels the temporary translation before cloning");
            Check(copies.Count == 1 &&
                Vector3.Distance(copies[0].transform.position, committedPosition + Vector3.right * NeospaceUnits.Mm(88)) < NeospaceUnits.Mm(0.001f),
                "interrupted-drag duplicate occupies the next free module instead of the restored source pose");
            List<SpaceHistory.InstanceState> copyUndo = mode.history.CanUndo ? mode.history.Undo() : null;
            List<SpaceHistory.InstanceState> copyRedo = mode.history.CanRedo ? mode.history.Redo() : null;
            Check(copyUndo != null && copyRedo != null && copyUndo.Count == beforeCopies.Count &&
                copyRedo.Count == beforeCopies.Count + 1 && copyUndo[0].Position == committedPosition &&
                copyRedo[0].Position == post.transform.position && copies.Count == 1 &&
                copyRedo[copyRedo.Count - 1].Position == copies[0].transform.position,
                "interrupted-drag duplication history matches the retained source and added copy");
            ((List<SpaceInstance>)interaction.Selected).Clear();
            ((List<SpaceInstance>)interaction.Selected).AddRange(copies);
            interaction.DeleteSelection();
            ResetInput();

            mode.history.ResetBaseline(interaction.CurrentStates());
            BeginDrag();
            Vector3 acceptedMovedPose = post.transform.position;
            // Headless validation has no held button and no MouseButtonUp edge.
            // The normal Update must settle the last accepted legal drag pose.
            yield return null;
            Check(!interaction.Busy && post.transform.position == acceptedMovedPose,
                "a missed release edge completes the last accepted drag without moving to a stale cursor");
            bool canUndo = mode.history.CanUndo;
            List<SpaceHistory.InstanceState> undo = canUndo ? mode.history.Undo() : null;
            Check(canUndo && undo != null && undo.Count > 0 && undo[0].Position == committedPosition,
                "missed-release completion creates one undoable move");
            ResetInput();
            Merge();
            finish.RefreshNow();
            Check(Count("Veneer H7") < 4 && NoOverlap(shelf.GetComponent<Renderer>().bounds),
                "finish refresh resumes after a cancelled or completed drag");
        }
        finally
        {
            SetInput("_ghost", null);
            ResetInput();
            interaction.enabled = previousEnabled;
            interaction.RebuildFromStates(new List<SpaceHistory.InstanceState>());
            foreach (GameObject go in owned)
                if (go != null) { go.SetActive(false); Object.Destroy(go); }
            if (SpaceModeController.Active) mode.ExitSpaceMode();
            finish.SetOn(previousFinish, announce: false);
            finish.RefreshNow();
            Debug.Log($"Finish Space lifecycle regression: {checks} checks, {failed} failures.");
        }
        yield return null;

        SpaceInstance Piece(string name)
        {
            var root = new GameObject("FinishLifecycle" + name);
            root.transform.SetParent(interaction.transform, false);
            owned.Add(root);
            var instance = root.AddComponent<SpaceInstance>();
            instance.pieceId = name;
            instance.pieceName = name;
            ((List<SpaceInstance>)interaction.Instances).Add(instance);
            return instance;
        }

        void Merge() => SpaceMerge.Apply(interaction.Instances,
            mode.buildController != null ? mode.buildController.partDatabase : null, interaction.transform);

        FieldInfo Field(string name) => typeof(SpaceInteractionController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        void SetInput(string name, object value) => Field(name).SetValue(interaction, value);
        List<Vector3> Starts() => (List<Vector3>)Field("_dragStartPositions").GetValue(interaction);
        List<int> Groups() => (List<int>)Field("_dragStartGroups").GetValue(interaction);
        bool InputArraysEmpty() => Starts().Count == 0 && Groups().Count == 0 && Field("_pressInstance").GetValue(interaction) == null;
        void BeginDrag()
        {
            ResetInput();
            ((List<SpaceInstance>)interaction.Selected).Add(post);
            Starts().Add(committedPosition);
            Groups().Add(0);
            Vector3 delta = Vector3.left * NeospaceUnits.Mm(88);
            post.transform.position = committedPosition + delta;
            SetInput("_lastDragDelta", delta);
            SetInput("_pressInstance", post);
            SetInput("_dragging", true);
        }
        void ResetInput()
        {
            SetInput("_dragging", false);
            SetInput("_pressInstance", null);
            SetInput("_lastDragDelta", Vector3.zero);
            Starts().Clear();
            Groups().Clear();
            ((List<SpaceInstance>)interaction.Selected).Clear();
            if (post != null) post.transform.position = committedPosition;
        }
    }

    static GameObject Box(Transform parent, string name, Vector3 centerMm, Vector3 sizeMm, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centerMm * NeospaceUnits.Mm(1);
        go.transform.localRotation = rotation;
        go.transform.localScale = sizeMm * NeospaceUnits.Mm(1);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static List<GameObject> Parts()
    {
        var parts = new List<GameObject>();
        GameObject root = GameObject.Find("FinishRoot");
        if (root != null)
            foreach (Transform part in root.transform)
                if (part.gameObject.activeInHierarchy) parts.Add(part.gameObject);
        return parts;
    }
    static int Count(string name) => Parts().FindAll(part => part.name == name).Count;
    static bool AllInactive(List<GameObject> parts) => parts.Count > 0 && parts.TrueForAll(part => part == null || !part.activeInHierarchy);
    static bool NoOverlap(Bounds panel)
    {
        float epsilon = NeospaceUnits.Mm(0.005f);
        foreach (GameObject part in Parts())
            foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                Vector3 overlap = Vector3.Min(bounds.max, panel.max) - Vector3.Max(bounds.min, panel.min);
                if (overlap.x > epsilon && overlap.y > epsilon && overlap.z > epsilon) return false;
            }
        return true;
    }
}
#endif
