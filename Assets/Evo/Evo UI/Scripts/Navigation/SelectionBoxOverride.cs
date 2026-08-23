using UnityEngine;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "effects/selection-box-override")]
    [AddComponentMenu("Evo/UI/Navigation/Selection Box Override")]
    public class SelectionBoxOverride : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Custom offset from this object's edges")]
        public Vector2 offset = new(10f, 10f);

        [Header("Sprite")]
        [Tooltip("Custom sprite/texture for the selection box when this object is selected")]
        public Sprite overrideSprite;

        [Header("Color")]
        [Tooltip("Enable to override the selection box color")]
        public bool overrideColor = false;

        [Tooltip("Custom color for the selection box when this object is selected")]
        public Color color = Color.white;

        [Header("Pixels Per Unit")]
        [Tooltip("Enable to override the pixels per unit multiplier")]
        public bool overridePPU = false;

        [Tooltip("Custom pixels per unit multiplier for sliced images")]
        public float PPUMultiplier = 2.25f;
    }
}