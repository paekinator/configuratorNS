using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "styler")]
    [CreateAssetMenu(fileName = "Styler Preset", menuName = "Evo/UI/Styler Preset")]
    public class StylerPreset : ScriptableObject
    {
        [EvoHeader("Audio", Constants.CustomEditorID)]
        public List<Styler.AudioItem> audioItems = new()
        {
            new("Hover SFX", null),
            new("Click SFX", null),
            new("Notification SFX", null)
        };

        [EvoHeader("Color", Constants.CustomEditorID)]
        public List<Styler.ColorItem> colorItems = new()
        {
            new Styler.ColorItem("Primary", Color.white),
            new Styler.ColorItem("Secondary", new Color(0.1f, 0.15f, 0.2f, 1f))
        };

        [EvoHeader("Font", Constants.CustomEditorID)]
        public List<Styler.FontItem> fontItems = new()
        {
            new Styler.FontItem("Thin", null),
            new Styler.FontItem("Light", null),
            new Styler.FontItem("Regular", null),
            new Styler.FontItem("Semibold", null),
            new Styler.FontItem("Bold", null)
        };

        [EvoHeader("Text Style", Constants.CustomEditorID)]
        public List<Styler.TextStyleItem> textStyleItems = new() { };

        [EvoHeader("Gradient", Constants.CustomEditorID)]
        public List<Styler.GradientItem> gradientItems = new() { };

        [EvoHeader("Sprite", Constants.CustomEditorID)]
        public List<Styler.SpriteItem> spriteItems = new() { };

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [Tooltip("Default update mode for all StylerObjects using this preset.")]
        public Styler.UpdateMode updateMode = Styler.UpdateMode.Adaptive;

        /// <summary>
        /// Try get an audio clip from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetAudio(string itemID, out AudioClip clip)
        {
            for (int i = 0; i < audioItems.Count; i++)
            {
                if (audioItems[i].itemID == itemID)
                {
                    clip = audioItems[i].audioAsset;
                    return true;
                }
            }

            clip = null;
            return false;
        }

        /// <summary>
        /// Get audio clip from the preset.
        /// </summary>
        public AudioClip GetAudio(string itemID)
        {
            TryGetAudio(itemID, out AudioClip clip);
            return clip;
        }

        /// <summary>
        /// Try get a color from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetColor(string itemID, out Color color)
        {
            for (int i = 0; i < colorItems.Count; i++)
            {
                if (colorItems[i].itemID == itemID)
                {
                    color = colorItems[i].colorValue;
                    return true;
                }
            }

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Get color from the preset.
        /// </summary>
        public Color GetColor(string itemID)
        {
            TryGetColor(itemID, out Color color);
            return color;
        }

        /// <summary>
        /// Try get a font from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetFont(string itemID, out TMP_FontAsset font)
        {
            for (int i = 0; i < fontItems.Count; i++)
            {
                if (fontItems[i].itemID == itemID)
                {
                    font = fontItems[i].fontAsset;
                    return true;
                }
            }

            font = null;
            return false;
        }

        /// <summary>
        /// Get font from the preset.
        /// </summary>
        public TMP_FontAsset GetFont(string itemID)
        {
            TryGetFont(itemID, out TMP_FontAsset font);
            return font;
        }

        /// <summary>
        /// Try get the full text style item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetTextStyleItem(string itemID, out Styler.TextStyleItem textStyleItem)
        {
            for (int i = 0; i < textStyleItems.Count; i++)
            {
                if (textStyleItems[i].itemID == itemID)
                {
                    textStyleItem = textStyleItems[i];
                    return true;
                }
            }

            textStyleItem = null;
            return false;
        }

        /// <summary>
        /// Get the full text style item definition from the preset.
        /// </summary>
        public Styler.TextStyleItem GetTextStyleItem(string itemID)
        {
            TryGetTextStyleItem(itemID, out Styler.TextStyleItem textStyleItem);
            return textStyleItem;
        }

        /// <summary>
        /// Try get a sprite from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetSprite(string itemID, out Sprite sprite)
        {
            for (int i = 0; i < spriteItems.Count; i++)
            {
                if (spriteItems[i].itemID == itemID)
                {
                    sprite = spriteItems[i].spriteAsset;
                    return true;
                }
            }

            sprite = null;
            return false;
        }

        /// <summary>
        /// Get sprite from the preset.
        /// </summary>
        public Sprite GetSprite(string itemID)
        {
            TryGetSprite(itemID, out Sprite sprite);
            return sprite;
        }

        /// <summary>
        /// Try get the full sprite item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetSpriteItem(string itemID, out Styler.SpriteItem spriteItem)
        {
            for (int i = 0; i < spriteItems.Count; i++)
            {
                if (spriteItems[i].itemID == itemID)
                {
                    spriteItem = spriteItems[i];
                    return true;
                }
            }

            spriteItem = null;
            return false;
        }

        /// <summary>
        /// Get the full sprite item definition from the preset.
        /// </summary>
        public Styler.SpriteItem GetSpriteItem(string itemID)
        {
            TryGetSpriteItem(itemID, out Styler.SpriteItem spriteItem);
            return spriteItem;
        }

        /// <summary>
        /// Try get a gradient from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetGradient(string itemID, out Gradient gradient)
        {
            for (int i = 0; i < gradientItems.Count; i++)
            {
                if (gradientItems[i].itemID == itemID)
                {
                    gradient = gradientItems[i].gradientValue;
                    return true;
                }
            }

            gradient = null;
            return false;
        }

        /// <summary>
        /// Get gradient from the preset.
        /// </summary>
        public Gradient GetGradient(string itemID)
        {
            TryGetGradient(itemID, out Gradient gradient);
            return gradient;
        }

        /// <summary>
        /// Set audio clip for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetAudio(string itemID, AudioClip audioClip)
        {
            for (int i = 0; i < audioItems.Count; i++)
            {
                if (audioItems[i].itemID == itemID)
                {
                    audioItems[i].audioAsset = audioClip;
                    return;
                }
            }

            audioItems.Add(new Styler.AudioItem(itemID, audioClip));
        }

        /// <summary>
        /// Set color for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetColor(string itemID, Color color)
        {
            for (int i = 0; i < colorItems.Count; i++)
            {
                if (colorItems[i].itemID == itemID)
                {
                    colorItems[i].colorValue = color;
                    return;
                }
            }

            colorItems.Add(new Styler.ColorItem(itemID, color));
        }

        /// <summary>
        /// Set font for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetFont(string itemID, TMP_FontAsset font)
        {
            for (int i = 0; i < fontItems.Count; i++)
            {
                if (fontItems[i].itemID == itemID)
                {
                    fontItems[i].fontAsset = font;
                    return;
                }
            }

            fontItems.Add(new Styler.FontItem(itemID, font));
        }

        /// <summary>
        /// Set sprite for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetSprite(string itemID, Sprite sprite)
        {
            for (int i = 0; i < spriteItems.Count; i++)
            {
                if (spriteItems[i].itemID == itemID)
                {
                    spriteItems[i].spriteAsset = sprite;
                    return;
                }
            }

            spriteItems.Add(new Styler.SpriteItem(itemID, sprite));
        }

        /// <summary>
        /// Set gradient for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetGradient(string itemID, Gradient gradient)
        {
            for (int i = 0; i < gradientItems.Count; i++)
            {
                if (gradientItems[i].itemID == itemID)
                {
                    gradientItems[i].gradientValue = gradient;
                    return;
                }
            }

            gradientItems.Add(new Styler.GradientItem(itemID, gradient));
        }

        /// <summary>
        /// Add an audio item to the preset.
        /// </summary>
        public void AddAudio(string itemID, AudioClip audioClip)
        {
            for (int i = 0; i < audioItems.Count; i++)
            {
                if (audioItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Audio item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            audioItems.Add(new Styler.AudioItem(itemID, audioClip));
        }

        /// <summary>
        /// Add a color item to the preset.
        /// </summary>
        public void AddColor(string itemID, Color color)
        {
            for (int i = 0; i < colorItems.Count; i++)
            {
                if (colorItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Color item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            colorItems.Add(new Styler.ColorItem(itemID, color));
        }

        /// <summary>
        /// Add a font item to the preset.
        /// </summary>
        public void AddFont(string itemID, TMP_FontAsset font)
        {
            for (int i = 0; i < fontItems.Count; i++)
            {
                if (fontItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Font item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            fontItems.Add(new Styler.FontItem(itemID, font));
        }

        /// <summary>
        /// Add a text style item to the preset.
        /// </summary>
        public void AddTextStyle(string itemID)
        {
            for (int i = 0; i < textStyleItems.Count; i++)
            {
                if (textStyleItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Text style item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            textStyleItems.Add(new Styler.TextStyleItem(itemID));
        }

        /// <summary>
        /// Add a sprite item to the preset.
        /// </summary>
        public void AddSprite(string itemID, Sprite sprite)
        {
            for (int i = 0; i < spriteItems.Count; i++)
            {
                if (spriteItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Sprite item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            spriteItems.Add(new Styler.SpriteItem(itemID, sprite));
        }

        /// <summary>
        /// Add a gradient item to the preset.
        /// </summary>
        public void AddGradient(string itemID, Gradient gradient)
        {
            for (int i = 0; i < gradientItems.Count; i++)
            {
                if (gradientItems[i].itemID == itemID)
                {
                    Debug.LogWarning($"Gradient item with ID '{itemID}' already exists.", this);
                    return;
                }
            }

            gradientItems.Add(new Styler.GradientItem(itemID, gradient));
        }

        /// <summary>
        /// Remove an audio item from the preset.
        /// </summary>
        public bool RemoveAudio(string itemID)
        {
            for (int i = 0; i < audioItems.Count; i++)
            {
                if (audioItems[i].itemID == itemID)
                {
                    audioItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a color item from the preset.
        /// </summary>
        public bool RemoveColor(string itemID)
        {
            for (int i = 0; i < colorItems.Count; i++)
            {
                if (colorItems[i].itemID == itemID)
                {
                    colorItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a font item from the preset.
        /// </summary>
        public bool RemoveFont(string itemID)
        {
            for (int i = 0; i < fontItems.Count; i++)
            {
                if (fontItems[i].itemID == itemID)
                {
                    fontItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a text style item from the preset.
        /// </summary>
        public bool RemoveTextStyle(string itemID)
        {
            for (int i = 0; i < textStyleItems.Count; i++)
            {
                if (textStyleItems[i].itemID == itemID)
                {
                    textStyleItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a sprite item from the preset.
        /// </summary>
        public bool RemoveSprite(string itemID)
        {
            for (int i = 0; i < spriteItems.Count; i++)
            {
                if (spriteItems[i].itemID == itemID)
                {
                    spriteItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a gradient item from the preset.
        /// </summary>
        public bool RemoveGradient(string itemID)
        {
            for (int i = 0; i < gradientItems.Count; i++)
            {
                if (gradientItems[i].itemID == itemID)
                {
                    gradientItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool audioFoldout = false;
        [HideInInspector] public bool colorFoldout = false;
        [HideInInspector] public bool fontFoldout = false;
        [HideInInspector] public bool textStyleFoldout = false;
        [HideInInspector] public bool spriteFoldout = false;
        [HideInInspector] public bool gradientFoldout = false;
        [HideInInspector] public bool settingsFoldout = false;

        void OnValidate()
        {
            if (updateMode == Styler.UpdateMode.Always || (!Application.isPlaying && updateMode == Styler.UpdateMode.Adaptive))
                NotifyStylerObjects();
        }

        void NotifyStylerObjects()
        {
            // Find all MonoBehaviours, but filter for the ones implementing our interface
#if UNITY_6000_4_OR_NEWER
            var targets = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
#else
            var targets = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is IStylerHandler handler && handler.Preset == this)
                    handler.UpdateStyler();
            }
        }
#endif
    }
}