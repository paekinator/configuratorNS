using System;

/// <summary>
/// Parses the connector naming conventions shared by placement algorithms.
/// </summary>
internal static class ConnectorNameUtility
{
    public static string GetGroupKey(string connectorName)
    {
        if (string.IsNullOrEmpty(connectorName))
            return string.Empty;

        int open = connectorName.IndexOf('(');
        return open < 0
            ? connectorName.Trim()
            : connectorName.Substring(0, open).Trim();
    }

    public static int GetOrderIndex(string connectorName)
    {
        if (string.IsNullOrEmpty(connectorName))
            return int.MinValue;

        int open = connectorName.LastIndexOf('(');
        int close = connectorName.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            string inside = connectorName.Substring(open + 1, close - open - 1).Trim();
            if (int.TryParse(inside, out int parsedInParens))
                return parsedInParens;
        }

        int end = connectorName.Length - 1;
        while (end >= 0 && !char.IsDigit(connectorName[end]))
            end--;

        if (end < 0)
            return 0;

        int start = end;
        while (start >= 0 && char.IsDigit(connectorName[start]))
            start--;

        string digits = connectorName.Substring(start + 1, end - start);
        return int.TryParse(digits, out int parsedTail) ? parsedTail : 0;
    }

    public static int GetFaceIndex(string connectorName)
    {
        if (string.IsNullOrEmpty(connectorName))
            return 0;

        const string marker = "AP_Side";
        int markerIndex = connectorName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return 0;

        int digitIndex = markerIndex + marker.Length;
        if (digitIndex >= connectorName.Length)
            return 0;

        char digit = connectorName[digitIndex];
        return digit >= '1' && digit <= '4' ? digit - '0' : 0;
    }
}
