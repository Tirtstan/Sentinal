#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Base class for components that need PlayerInput from a ViewInputSystemHandler or IPlayerInputProvider.
    /// Provides common functionality for managing PlayerInput references and handling changes.
    /// </summary>
    public abstract class ViewInputSystemComponent : MonoBehaviour, IPlayerInputHandler
    {
        [Header("Input Handler")]
        [SerializeField]
        [Tooltip("The ViewInputSystemHandler to get PlayerInput and input state from.")]
        protected ViewInputSystemHandler viewInputHandler;
        protected PlayerInput playerInput;

        protected virtual void Awake()
        {
            if (viewInputHandler == null)
                viewInputHandler = GetComponent<ViewInputSystemHandler>();
        }

        protected virtual void OnEnable()
        {
            RefreshPlayerInput();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromPlayerInput();

            if (viewInputHandler != null)
                viewInputHandler.OnPlayerInputChanged -= OnPlayerInputChangedHandler;
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeFromPlayerInput();

            if (viewInputHandler != null)
                viewInputHandler.OnPlayerInputChanged -= OnPlayerInputChangedHandler;
        }

        /// <summary>
        /// Refreshes the PlayerInput reference from the ViewInputSystemHandler.
        /// Called automatically when the component is enabled.
        /// </summary>
        protected virtual void RefreshPlayerInput()
        {
            if (viewInputHandler != null)
            {
                viewInputHandler.OnPlayerInputChanged -= OnPlayerInputChangedHandler;
                viewInputHandler.OnPlayerInputChanged += OnPlayerInputChangedHandler;

                PlayerInput newPlayerInput = viewInputHandler.GetPlayerInput();
                if (playerInput != newPlayerInput)
                {
                    UnsubscribeFromPlayerInput();
                    playerInput = newPlayerInput;
                    SubscribeToPlayerInput();
                    OnPlayerInputChanged(newPlayerInput);
                }
            }

            OnPlayerInputRefreshed();
        }

        private void OnPlayerInputChangedHandler(PlayerInput newPlayerInput)
        {
            if (playerInput != newPlayerInput)
            {
                UnsubscribeFromPlayerInput();
                playerInput = newPlayerInput;
                SubscribeToPlayerInput();
                OnPlayerInputChanged(newPlayerInput);
                OnPlayerInputRefreshed();
            }
        }

        /// <summary>
        /// Sets the ViewInputSystemHandler reference programmatically.
        /// </summary>
        public void SetViewInputHandler(ViewInputSystemHandler handler)
        {
            if (viewInputHandler == handler)
                return;

            if (viewInputHandler != null)
                viewInputHandler.OnPlayerInputChanged -= OnPlayerInputChangedHandler;

            viewInputHandler = handler;
            RefreshPlayerInput();
        }

        /// <summary>
        /// Gets the ViewInputSystemHandler reference.
        /// </summary>
        /// <returns>The ViewInputSystemHandler reference.</returns>
        public ViewInputSystemHandler GetViewInputHandler() => viewInputHandler;

        /// <summary>
        /// Gets the PlayerInput reference.
        /// </summary>
        public PlayerInput GetPlayerInput() => playerInput;

        /// <summary>
        /// Sets the PlayerInput reference programmatically.
        /// Useful when not using a ViewInputSystemHandler.
        /// </summary>
        public void SetPlayerInput(PlayerInput input)
        {
            if (playerInput == input)
                return;

            UnsubscribeFromPlayerInput();
            playerInput = input;
            SubscribeToPlayerInput();
            OnPlayerInputChanged(input);
            OnPlayerInputRefreshed();
        }

        /// <summary>
        /// Called when PlayerInput reference changes.
        /// Override to handle PlayerInput changes.
        /// </summary>
        protected virtual void OnPlayerInputChanged(PlayerInput newPlayerInput) { }

        /// <summary>
        /// Called after PlayerInput is refreshed.
        /// Override to perform actions when PlayerInput is updated.
        /// </summary>
        protected virtual void OnPlayerInputRefreshed() { }

        /// <summary>
        /// Subscribes to PlayerInput events.
        /// Override to subscribe to specific PlayerInput events (e.g., onControlsChanged).
        /// </summary>
        protected virtual void SubscribeToPlayerInput()
        {
            if (playerInput != null)
                playerInput.onControlsChanged += OnControlsChanged;
        }

        /// <summary>
        /// Unsubscribes from PlayerInput events.
        /// Override to unsubscribe from specific PlayerInput events.
        /// </summary>
        protected virtual void UnsubscribeFromPlayerInput()
        {
            if (playerInput != null)
                playerInput.onControlsChanged -= OnControlsChanged;
        }

        /// <summary>
        /// Called when the control scheme changes.
        /// Override to handle control scheme changes.
        /// </summary>
        protected virtual void OnControlsChanged(PlayerInput input) { }

        protected virtual void Reset()
        {
            if (viewInputHandler == null)
                viewInputHandler = GetComponent<ViewInputSystemHandler>();
        }
    }
}
#endif
