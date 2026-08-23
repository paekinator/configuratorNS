namespace Evo.UI
{
    /// <summary>
    /// Interface to implement Styler to classes.
    /// </summary>
    public interface IStylerHandler
    {
        /// <summary>
        /// Points to the local variable.
        /// </summary>
        StylerPreset Preset { get; set; }

        /// <summary>
        /// The method to run when the preset changes.
        /// </summary>
        void UpdateStyler();
    }

    /// <summary>
    /// Interface for Selectables to broadcast their state to StylerObjects.
    /// </summary>
    public interface IStylerInteractable
    {
        /// <summary>
        /// Gets the transition duration used for animating state changes.
        /// </summary>
        float TransitionDuration { get; }

        /// <summary>
        /// Gets the current interaction state of this UI element.
        /// </summary>
        InteractionState InteractionState { get; }

        /// <summary>
        /// Event triggered when the interaction state of the element changes.
        /// </summary>
        event System.Action<InteractionState> OnStateChanged;
    }
}