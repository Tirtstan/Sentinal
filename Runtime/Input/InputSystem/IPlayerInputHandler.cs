#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Interface for components that provide PlayerInput configuration.
    /// </summary>
    public interface IPlayerInputHandler
    {
        /// <summary>
        /// Gets the PlayerInput reference.
        /// </summary>
        public PlayerInput GetPlayerInput();

        /// <summary>
        /// Sets the PlayerInput reference.
        /// </summary>
        public void SetPlayerInput(PlayerInput input);
    }
}
#endif
