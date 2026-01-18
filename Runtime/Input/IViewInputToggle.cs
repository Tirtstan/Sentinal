using System;

namespace Sentinal
{
    /// <summary>
    /// Interface for components that can enable/disable input for a view.
    /// </summary>
    public interface IViewInputToggle
    {
        public event Action<bool> OnInputChanged;

        /// <summary>
        /// Gets whether input is currently enabled.
        /// </summary>
        /// <returns>True if input is enabled, false otherwise.</returns>
        public bool IsInputEnabled();

        /// <summary>
        /// Enables input for this view.
        /// </summary>
        public void EnableInput();

        /// <summary>
        /// Disables input for this view.
        /// </summary>
        public void DisableInput();
    }
}
