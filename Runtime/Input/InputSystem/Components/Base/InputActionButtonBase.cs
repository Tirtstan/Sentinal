#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Abstract base class for button components that trigger Button's onClick via input actions.
    /// Provides common functionality for button interaction and pointer event handling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public abstract class InputActionButtonBase : ViewInputActionHandler
    {
        [Header("Action")]
        [SerializeField]
        [Tooltip("Input action to trigger the button (e.g., 'Submit').")]
        protected InputActionSelector actionSelector = new() { useActionName = true, actionName = "Submit" };

        [Header("Configs")]
        [SerializeField]
        [Tooltip("This enables button visual feedback and re-selection.")]
        protected bool sendPointerEvents = true;

        protected Button button;
        protected InputAction inputAction;
        protected EventSystem eventSystem;

        protected override void Awake()
        {
            base.Awake();
            button = GetComponent<Button>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            eventSystem = EventSystem.current;
        }

        protected override void Subscribe()
        {
            if (playerInput == null || playerInput.actions == null)
                return;

            if (actionSelector == null || !actionSelector.IsValid())
            {
                Debug.LogWarning($"{GetType().Name}: Action selector is not configured.", this);
                return;
            }

            inputAction = actionSelector.FindAction(playerInput);
            if (inputAction == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: Input action '{actionSelector.GetDisplayName()}' not found on PlayerInput actions.",
                    this
                );
                return;
            }

            SubscribeToInputAction();
            isSubscribed = true;
        }

        protected override void Unsubscribe()
        {
            if (inputAction != null)
            {
                UnsubscribeFromInputAction();
                inputAction = null;
            }

            isSubscribed = false;
        }

        /// <summary>
        /// Called when subscribing to the input action. Override to subscribe to specific action events.
        /// </summary>
        protected abstract void SubscribeToInputAction();

        /// <summary>
        /// Called when unsubscribing from the input action. Override to unsubscribe from specific action events.
        /// </summary>
        protected abstract void UnsubscribeFromInputAction();

        /// <summary>
        /// Triggers the button's onClick event.
        /// </summary>
        protected void Click()
        {
            if (button == null)
                return;

            if (sendPointerEvents && eventSystem != null)
            {
                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.selectHandler
                );

                // ExecuteEvents.pointerClickHandler will trigger Button's onClick automatically
                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.pointerClickHandler
                );
            }
            else
            {
                // When not sending pointer events, directly invoke onClick
                button.onClick?.Invoke();
            }
        }

        /// <summary>
        /// Gets the current action selector. Useful for programmatic configuration.
        /// </summary>
        public InputActionSelector GetActionSelector() => actionSelector;

        /// <summary>
        /// Sets the action selector. Useful for programmatic configuration.
        /// </summary>
        public void SetActionSelector(InputActionSelector selector)
        {
            if (actionSelector == selector)
                return;

            bool wasSubscribed = isSubscribed;
            if (wasSubscribed)
                Unsubscribe();

            actionSelector = selector;

            if (wasSubscribed)
                UpdateSubscription();
        }
    }
}
#endif
