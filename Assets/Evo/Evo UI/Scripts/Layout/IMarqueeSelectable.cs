using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Interface to implement Marquee Selection to classes.
    /// </summary>
    public interface IMarqueeSelectable
    {
        bool IsSelected { get; }
        bool Interactable { get; }
        Transform Transform { get; }

        bool IsInsideScreenRect(Rect screenRect, Camera renderCamera);
        void OnMarqueeSelect();
        void OnMarqueeDeselect();
    }
}