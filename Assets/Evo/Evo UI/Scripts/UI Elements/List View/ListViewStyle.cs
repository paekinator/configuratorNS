using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [System.Serializable]
    public class ListViewStyle
    {
        // Text Settings
        public TMP_FontAsset headerFont;
        public TMP_FontAsset rowFont;
        public int headerFontSize = 24;
        public int rowFontSize = 24;
        public FontStyles headerFontStyle = FontStyles.Bold;
        public FontStyles rowFontStyle = FontStyles.Normal;
        public Color headerTextColor = Color.white;
        public Color rowTextColor = Color.black;

        // Layout Settings
        public float headerHeight = 60;
        public float rowHeight = 60;
        public float columnSpacing = 0;
        public float rowSpacing = 0;
        public float iconSize = 16;
        public float contentSpacing = 15;
        public RectOffset contentPadding = new();

        // Background Settings
        public Color headerBackgroundColor = Color.gray;
        public Color rowBackgroundColor = Color.white;
        public bool useAlternatingRowColor = false;
        public Color alternatingRowColor = new(0.9f, 0.9f, 0.9f, 1f);
        public bool showBorder = true;
        public Color borderColor = Color.black;
        public float borderWidth = 1;
        public Sprite backgroundSprite;
        [Range(0.1f, 50f)] public float ppuMultiplier = 1;

        public enum StylingSource
        {
            Custom = 0,
            StylerPreset = 1
        }

        public enum Type
        {
            HeaderFont,
            RowFont,
            HeaderTextColor,
            RowTextColor,
            HeaderBackgroundColor,
            RowBackgroundColor,
            AlternatingRowColor,
            BorderColor
        }

        [System.Serializable]
        public class Mapping
        {
            public Type type;
            public string colorID = "";
            public string fontID = "";
        }

        public override int GetHashCode()
        {
            System.HashCode hash = new();
            hash.Add(headerBackgroundColor);
            hash.Add(rowBackgroundColor);
            hash.Add(alternatingRowColor);
            hash.Add(useAlternatingRowColor);
            hash.Add(showBorder);
            hash.Add(borderColor);
            hash.Add(borderWidth);
            hash.Add(headerTextColor);
            hash.Add(rowTextColor);
            hash.Add(headerFontSize);
            hash.Add(rowFontSize);
            hash.Add(headerFontStyle);
            hash.Add(rowFontStyle);
            hash.Add(contentSpacing);
            hash.Add(rowHeight);
            hash.Add(headerHeight);
            hash.Add(iconSize);
            hash.Add(columnSpacing);
            hash.Add(rowSpacing);
            return hash.ToHashCode();
        }
    }
}