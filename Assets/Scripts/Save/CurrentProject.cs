using UnityEngine;

/// <summary>
/// Which project is open, and what it looked like when it was opened or last
/// saved.
///
/// Nothing tracked this before. A project could be saved and reopened, but the
/// app forgot which one you were in the moment the panel closed — so there was
/// nothing to put a name to on screen, and nothing for a warning to compare
/// the scene against.
///
/// Deliberately NOT a live "is it dirty" flag. Keeping one accurate would mean
/// noticing every change a person can make — a part placed, moved, nudged,
/// deleted, a panel added, a block dropped in — and a flag that is wrong is
/// worse than no flag when the thing it guards is losing work. Instead the
/// code the project was saved with is kept, and <see cref="HasUnsavedWork"/>
/// captures the scene and compares, on demand.
///
/// That is affordable because the question is only ever asked at the moments
/// it matters: clearing the scene, opening something else, loading a code,
/// changing mode. Four times in a session, not sixty times a second.
/// </summary>
public static class CurrentProject
{
    /// <summary>The saved project's id, or empty when this scene was never saved.</summary>
    public static string Id { get; private set; } = string.Empty;

    /// <summary>What to call it on screen.</summary>
    public static string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The code that was on disk at the moment it was opened or saved. The
    /// yardstick for "has anything changed since".
    /// </summary>
    public static string SavedCode { get; private set; } = string.Empty;

    /// <summary>Shown when no project is open, matching the mockup's wording.</summary>
    public const string UntitledLabel = "Untitled Project";

    public static bool IsOpen => !string.IsNullOrEmpty(Id);

    /// <summary>What the name display should read.</summary>
    public static string DisplayName => IsOpen ? Name : UntitledLabel;

    public static event System.Action Changed;

    /// <summary>A project was opened, or the open one was saved over.</summary>
    public static void Set(string id, string name, string savedCode)
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        SavedCode = savedCode ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// The open project is gone, or the scene no longer represents it —
    /// cleared, or replaced by a pasted code. The work on screen is real
    /// either way; it simply has no project behind it any more.
    /// </summary>
    public static void Forget()
    {
        if (!IsOpen && string.IsNullOrEmpty(SavedCode))
            return;

        Id = string.Empty;
        Name = string.Empty;
        SavedCode = string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// Record what is on screen as the saved state, without changing which
    /// project is open. Used after saving over the open project.
    /// </summary>
    public static void MarkSaved(string code)
    {
        SavedCode = code ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// Is there work on screen that is not in a saved project?
    ///
    /// True when the scene holds something and it does not match what was
    /// last saved. An empty scene is never unsaved work — there is nothing to
    /// lose — so starting fresh and immediately clearing asks no question.
    ///
    /// <paramref name="reason"/> says what would be lost, for the dialog.
    /// </summary>
    public static bool HasUnsavedWork(BuildController build, out string reason)
    {
        reason = null;
        string current = CaptureCode(build, out int parts);

        if (parts == 0)
            return false;                       // nothing on screen
        if (current != null && current == SavedCode)
            return false;                       // matches what is on disk

        reason = IsOpen
            ? $"\"{Name}\" has changes that are not saved."
            : $"This scene has {parts} part{(parts == 1 ? "" : "s")} and has never been saved.";
        return true;
    }

    /// <summary>
    /// Do something that would lose the scene — but ask first if there is
    /// anything to lose.
    ///
    /// One helper rather than the same five lines at each guarded moment, so
    /// they cannot drift into asking differently, or one of them quietly not
    /// asking at all. <paramref name="consequence"/> completes the sentence
    /// after the reason: it should say what this particular action does.
    ///
    /// When nothing is at stake the action simply runs. A dialog that appears
    /// when there is nothing to lose teaches people to dismiss dialogs, and
    /// then the one that matters gets dismissed too.
    /// </summary>
    public static void GuardThen(BuildController build, string title,
                                 string consequence, string confirmLabel,
                                 System.Action proceed)
    {
        if (proceed == null)
            return;

        if (!HasUnsavedWork(build, out string reason))
        {
            proceed();
            return;
        }

        var dialog = Object.FindFirstObjectByType<UIConfirmDialog>();
        if (dialog == null)
        {
            // No modal in this scene: refuse rather than proceed. The whole
            // point is that this step is not taken silently.
            SelectionStatus.Set(reason + " Save it first.", 6f);
            return;
        }

        dialog.Ask(title, reason + " " + consequence, confirmLabel, proceed);
    }

    /// <summary>
    /// The code for whatever is on screen, in whichever mode. Null when it
    /// cannot be encoded — an empty scene, or a space holding a block with no
    /// code — in which case `parts` still says whether anything is there.
    /// </summary>
    public static string CaptureCode(BuildController build, out int parts)
    {
        parts = 0;

        if (SpaceModeController.Active)
        {
            var space = Object.FindFirstObjectByType<SpaceInteractionController>();
            if (space == null || space.InstanceCount == 0)
                return null;

            parts = space.InstanceCount;
            try
            {
                return SpaceCodec.Encode(space.CurrentStates());
            }
            catch (ConfigurationCodeException)
            {
                return null;   // unencodable, but the work is real — see above
            }
        }

        ConfigurationModel model = ConfigurationCapture.Capture(build);
        parts = model.Beams.Count + model.Panels.Count;
        return parts == 0 ? null : ConfigurationCode.Encode(model);
    }
}
