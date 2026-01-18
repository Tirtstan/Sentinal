namespace Sentinal
{
    /// <summary>
    /// Interface for components that want to override ViewInputSystemHandler's input enabled behavior.
    /// </summary>
    public interface IInputToggleOverride
    {
        /// <summary>
        /// Gets or sets the input when current mode (Inherit, Off, or On).
        /// </summary>
        public InputWhenCurrentMode InputWhenCurrentMode { get; set; }

        /// <summary>
        /// Determines if the component should subscribe to input based on when current mode.
        /// </summary>
        /// <returns>True if should subscribe, false otherwise.</returns>
        public bool ShouldSubscribe();
    }
}
