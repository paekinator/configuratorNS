using UnityEngine;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Used where interaction states are fixed, such as Button.
    /// </summary>
    public enum InteractionState
    {
        Disabled = 0,
        Normal = 1,
        Highlighted = 2,
        Pressed = 3,
        Selected = 4
    }

    /// <summary>
    /// Used where multiple sources are available.
    /// </summary>
    public enum StylingSource
    {
        None = 0,
        Custom = 1,
        StylerPreset = 2
    }

    /// <summary>
    /// Used where custom offset position is offered, such as Tooltip and Context Menu.
    /// </summary>
    public enum OffsetPosition
    {
        Custom = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomLeft = 3,
        BottomRight = 4,
        Top = 5,
        Bottom = 6,
        Left = 7,
        Right = 8
    }

    /// <summary>
    /// Used where cursor type can be defined, such as Interactive.
    /// </summary>
    public enum CursorType
    {
        Default = 0,
        [InspectorName("Custom (Hardware)")] CustomHardware = 1
    }

    /// <summary>
    /// Used for dynamic objects where multiple audio sources are available.
    /// </summary>
    [System.Serializable]
    public class AudioMapping
    {
        public string stylerID;
        public AudioClip audioClip;
    }

    /// <summary>
    /// Used for dynamic objects where multiple color sources are available.
    /// </summary>
    [System.Serializable]
    public class ColorMapping
    {
        public string stylerID;
        public Color color = Color.white;
    }

    /// <summary>
    /// Used for dynamic objects where multiple font sources are available.
    /// </summary>
    [System.Serializable]
    public class FontMapping
    {
        public string stylerID;
        public TMP_FontAsset font;
    }

    /// <summary>
    /// Used for dynamic objects where multiple sprite sources are available.
    /// </summary>
    [System.Serializable]
    public class SpriteMapping
    {
        public string stylerID;
        public Sprite sprite;
    }

    /// <summary>
    /// Used for dynamic objects where multiple gradient sources are available.
    /// </summary>
    [System.Serializable]
    public class GradientMapping
    {
        public string stylerID;
        public Gradient gradient = new();
    }
}