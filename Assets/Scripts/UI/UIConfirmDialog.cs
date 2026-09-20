using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The one modal in the app: a title, a sentence saying what is about to
/// happen, and two ways out.
///
/// Used where a two-click "Sure?" is not enough — deleting something that
/// holds other things. UIConfirmingButton is right for a single item, where
/// the cost of a mistake is one undo; it is not right for "this collection
/// holds 6 blocks", because the sentence explaining what you are about to
/// lose does not fit on a button.
///
/// Baked by ConfiguratorUIBuilder and shared. Nothing here builds UI, and
/// there is exactly one of these so two questions can never stack.
/// </summary>
public class UIConfirmDialog : MonoBehaviour
{
    [Header("Baked by ConfiguratorUIBuilder")]
    public GameObject backdrop;
    public GameObject card;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button confirmButton;
    public Button cancelButton;
    public TextMeshProUGUI confirmLabel;

    Action _onConfirm;

    void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(Cancel);
        Hide();
    }

    void Update()
    {
        if (card == null || !card.activeSelf)
            return;

        // Escape cancels. There is no keyboard shortcut for confirming: the
        // destructive answer should cost a deliberate click.
        if (Input.GetKeyDown(KeyCode.Escape))
            Cancel();
    }

    /// <summary>
    /// Ask, then run <paramref name="onConfirm"/> if the answer is yes.
    /// <paramref name="message"/> should say exactly what will be lost — a
    /// dialog that only says "are you sure?" has told the reader nothing they
    /// did not already know.
    /// </summary>
    public void Ask(string title, string message, string confirmText, Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (titleText != null)
            titleText.text = title;
        if (messageText != null)
            messageText.text = message;
        if (confirmLabel != null)
            confirmLabel.text = confirmText;

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

    void Confirm()
    {
        Action action = _onConfirm;
        Hide();
        // Cleared and the dialog closed BEFORE the action runs: the action
        // usually rebuilds the panel the dialog was asked from, and may ask
        // again.
        action?.Invoke();
    }

    public void Cancel() => Hide();

    void Hide()
    {
        _onConfirm = null;
        if (card != null)
            card.SetActive(false);
        if (backdrop != null)
            backdrop.SetActive(false);
    }
}
