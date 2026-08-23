using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Two-step button for destructive actions: the first click arms it (label
/// turns "Sure?"), the second click within a few seconds fires
/// <see cref="onConfirmed"/>. Wire actions to onConfirmed, NOT to the
/// Button's own onClick.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIConfirmingButton : MonoBehaviour
{
    public UnityEvent onConfirmed = new UnityEvent();

    [Tooltip("Label whose text swaps to 'Sure?' while armed.")]
    public TextMeshProUGUI label;

    string _idleText;
    bool _armed;
    Coroutine _disarm;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(HandleClick);
        if (label != null)
            _idleText = label.text;
    }

    void OnDisable() => Disarm();

    void HandleClick()
    {
        if (!_armed)
        {
            _armed = true;
            if (label != null)
            {
                if (string.IsNullOrEmpty(_idleText))
                    _idleText = label.text;
                label.text = "Sure?";
            }
            _disarm = StartCoroutine(DisarmLater());
            return;
        }

        Disarm();
        onConfirmed.Invoke();
    }

    void Disarm()
    {
        if (_disarm != null)
        {
            StopCoroutine(_disarm);
            _disarm = null;
        }
        _armed = false;
        if (label != null && !string.IsNullOrEmpty(_idleText))
            label.text = _idleText;
    }

    IEnumerator DisarmLater()
    {
        yield return new WaitForSeconds(2.5f);
        _armed = false;
        if (label != null && !string.IsNullOrEmpty(_idleText))
            label.text = _idleText;
        _disarm = null;
    }
}
