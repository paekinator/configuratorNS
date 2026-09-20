using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Add a Block" asks which way: make one from a module in the scene, or
/// bring in one from a code someone sent you.
///
/// It asks rather than guessing because neither route is obviously the
/// default — one turns your own work into a block, the other imports
/// somebody else's — and hiding either behind a menu would make it the
/// lesser of the two for no reason.
///
/// Baked by ConfiguratorUIBuilder; this only opens it, answers it, and
/// reports what went wrong in the line under the field rather than in a
/// status pill behind the dialog, where the reader is not looking.
/// </summary>
public class UIAddBlockDialog : MonoBehaviour
{
    [Header("Baked by ConfiguratorUIBuilder")]
    public GameObject backdrop;
    public GameObject card;
    public Button pickButton;
    public TextMeshProUGUI pickRowHint;
    public TMP_InputField codeInput;
    public Button importButton;
    public TextMeshProUGUI noteText;
    public Button closeButton;
    public Button dismissButton;

    Action _onPick;
    Action<string> _onImport;

    /// <summary>What the caption says when picking is not on offer.</summary>
    const string PickHint =
        "Point at a structure and click it. The whole module is taken, never one part.";

    void Awake()
    {
        if (pickButton != null)
            pickButton.onClick.AddListener(Pick);
        if (importButton != null)
            importButton.onClick.AddListener(Import);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (dismissButton != null)
            dismissButton.onClick.AddListener(Close);
        if (codeInput != null)
            codeInput.onSubmit.AddListener(_ => Import());
        Close();
    }

    void Update()
    {
        if (card != null && card.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    /// <summary>
    /// Open it. <paramref name="canPick"/> is false in Lite, where there are
    /// no editable modules — the row is disabled and SAYS why rather than
    /// disappearing, because a choice that vanishes looks like a fault.
    /// </summary>
    public void Show(bool canPick, Action onPick, Action<string> onImport)
    {
        _onPick = onPick;
        _onImport = onImport;

        if (pickButton != null)
            pickButton.interactable = canPick;
        if (pickRowHint != null)
            pickRowHint.text = canPick
                ? PickHint
                : $"Only in {UIChrome.ProLabel} · {UIChrome.LiteLabel} has no editable modules to pick from.";

        if (codeInput != null)
            codeInput.text = string.Empty;
        Note(string.Empty, false);

        if (backdrop != null)
        {
            backdrop.SetActive(true);
            backdrop.transform.SetAsLastSibling();
        }
        if (card != null)
        {
            card.SetActive(true);
            card.transform.SetAsLastSibling();
        }
    }

    public void Close()
    {
        _onPick = null;
        _onImport = null;
        if (card != null)
            card.SetActive(false);
        if (backdrop != null)
            backdrop.SetActive(false);
    }

    /// <summary>
    /// Say why an import failed, in the dialog. Left open on a failure: a
    /// bad paste is usually a truncated one, and closing the window would
    /// throw away the attempt along with the explanation.
    /// </summary>
    public void Note(string message, bool bad)
    {
        if (noteText == null)
            return;
        noteText.text = message ?? string.Empty;
        noteText.color = bad ? UIThemeController.DangerColor : UIThemeController.MutedColor;
    }

    void Pick()
    {
        Action pick = _onPick;
        Close();
        pick?.Invoke();
    }

    void Import()
    {
        string code = codeInput != null ? codeInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            Note("Paste a code into the box first.", true);
            return;
        }

        _onImport?.Invoke(code);
    }
}
