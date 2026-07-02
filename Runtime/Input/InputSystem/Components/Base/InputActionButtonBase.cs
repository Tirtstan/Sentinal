#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

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

        [SerializeField]
        [Tooltip(
            "Defers click execution to the next frame to prevent input events carrying over to newly activated views."
        )]
        protected bool deferExecution = true;

        [SerializeField]
        [Tooltip(
            "Requires the input action to be released before accepting new presses when this component becomes active, preventing input bleeding from previous views."
        )]
        protected bool requireFreshPress = true;

        protected Button button;
        protected InputAction inputAction;
        protected EventSystem eventSystem;
        protected bool wasPressedOnSubscribe;

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

            wasPressedOnSubscribe = requireFreshPress && inputAction.IsPressed();

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

            wasPressedOnSubscribe = false;
            isSubscribed = false;
        }

        /// <summary>
        /// Validates whether the current input action event is valid based on fresh press rules.
        /// Returns true if the event should be processed, or false if it should be ignored.
        /// </summary>
        protected bool ValidateFreshPress(bool isCancelEvent = false)
        {
            if (!requireFreshPress || !wasPressedOnSubscribe)
                return true;

            if (inputAction == null)
                return false;

            if (!inputAction.IsPressed() || isCancelEvent)
                wasPressedOnSubscribe = false;

            return false;
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

            if (deferExecution)
            {
                StartCoroutine(ClickNextFrame());
            }
            else
            {
                ExecuteClick();
            }
        }

        private IEnumerator ClickNextFrame()
        {
            yield return null;
            ExecuteClick();
        }

        /// <summary>
        /// Executes the actual click event logic.
        /// </summary>
        protected void ExecuteClick()
        {
            if (button == null || !button.gameObject.activeInHierarchy)
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
