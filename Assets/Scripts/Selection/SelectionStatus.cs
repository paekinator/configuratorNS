using UnityEngine;

/// <summary>
/// Status-bar override used by the selection / copy / panel-move tools so
/// their guidance always wins over the regular build status while they are
/// active. Timed messages expire on their own; sticky messages (seconds &lt;= 0)
/// stay until cleared or replaced.
/// </summary>
public static class SelectionStatus
{
    static string _message;
    static float _expiry;

    public static void Set(string message, float seconds = 0f)
    {
        _message = message;
        _expiry = seconds > 0f ? Time.unscaledTime + seconds : float.PositiveInfinity;
    }

    public static void Clear()
    {
        _message = null;
    }

    public static bool TryGet(out string message)
    {
        if (!string.IsNullOrEmpty(_message) && Time.unscaledTime <= _expiry)
        {
            message = _message;
            return true;
        }
        message = null;
        return false;
    }
}
