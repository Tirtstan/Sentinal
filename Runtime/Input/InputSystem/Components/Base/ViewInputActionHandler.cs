#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Abstract base class for components that handle input actions based on ViewInputSystemHandler's input state.
    /// Provides common functionality for subscribing/unsubscribing to input actions based on the handler's input enabled state.
    /// </summary>
    public abstract class ViewInputActionHandler : ViewInputSystemComponent, IInputToggleOverride
    {
        [SerializeField]
        [Tooltip("Controls when input is activated. Inherit follows the ViewInputSystemHandler's state.")]
        private InputWhenCurrentMode inputWhenCurrentMode = InputWhenCurrentMode.Inherit;
        protected bool isSubscribed;

        public InputWhenCurrentMode InputWhenCurrentMode
        {
            get => inputWhenCurrentMode;
            set => inputWhenCurrentMode = value;
        }

        protected virtual void Start()
        {
            if (playerInput == null)
                RefreshPlayerInput();
            else
                UpdateSubscription();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateSubscription();
        }

        protected override void OnDisable()
        {
            Unsubscribe();

            if (viewInputHandler != null)
                viewInputHandler.OnInputChanged -= OnInputChangedHandler;

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Unsubscribe();

            if (viewInputHandler != null)
                viewInputHandler.OnInputChanged -= OnInputChangedHandler;

            base.OnDestroy();
        }

        protected override void OnPlayerInputRefreshed()
        {
            base.OnPlayerInputRefreshed();

            if (viewInputHandler != null)
            {
                viewInputHandler.OnInputChanged -= OnInputChangedHandler;
                viewInputHandler.OnInputChanged += OnInputChangedHandler;
            }

            UpdateSubscription();
        }

        protected override void OnPlayerInputChanged(PlayerInput newPlayerInput)
        {
            if (isSubscribed)
                Unsubscribe();

            base.OnPlayerInputChanged(newPlayerInput);
            UpdateSubscription();
        }

        private void OnInputChangedHandler(bool isEnabled) => UpdateSubscription();

        public bool ShouldSubscribe() =>
            inputWhenCurrentMode switch
            {
                InputWhenCurrentMode.Inherit => viewInputHandler != null && viewInputHandler.IsInputEnabled(),
                InputWhenCurrentMode.AlwaysEnabled => true,
                InputWhenCurrentMode.AlwaysDisabled => false,
                _ => true,
            };

        protected void UpdateSubscription()
        {
            bool shouldBeSubscribed = ShouldSubscribe();
            if (shouldBeSubscribed && !isSubscribed)
            {
                Subscribe();
            }
            else if (!shouldBeSubscribed && isSubscribed)
            {
                Unsubscribe();
            }
        }

        /// <summary>
        /// Called when the component should subscribe to input actions.
        /// Override this to subscribe to specific input actions.
        /// </summary>
        protected abstract void Subscribe();

        /// <summary>
        /// Called when the component should unsubscribe from input actions.
        /// Override this to unsubscribe from specific input actions.
        /// </summary>
        protected abstract void Unsubscribe();
    }
}
#endif
