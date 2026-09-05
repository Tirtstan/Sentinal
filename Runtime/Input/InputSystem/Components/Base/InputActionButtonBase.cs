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
        protected InputActionSelector actionSelector = new("Submit");

        [Header("Configs")]
        [SerializeField]
        [Tooltip("This enables button visual feedback and re-selection.")]
        protected bool sendPointerEvents = true;

        [SerializeField]
        [Tooltip("Selects this button in the EventSystem when its input action triggers it.")]
        protected bool selectButtonOnInputAction = true;

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
        protected InputFreshPressGate freshPressGate;

        public InputActionSelector ActionSelector
        {
            get => actionSelector;
            set
            {
                if (ReferenceEquals(actionSelector, value))
                    return;

                bool wasSubscribed = isSubscribed;
                if (wasSubscribed)
                    Unsubscribe();

                actionSelector = value;

                if (wasSubscribed)
                    UpdateSubscription();
            }
        }

        public bool SendPointerEvents
        {
            get => sendPointerEvents;
            set => sendPointerEvents = value;
        }

        public bool SelectButtonOnInputAction
        {
            get => selectButtonOnInputAction;
            set => selectButtonOnInputAction = value;
        }

        public bool DeferExecution
        {
            get => deferExecution;
            set => deferExecution = value;
        }

        public bool RequireFreshPress
        {
            get => requireFreshPress;
            set
            {
                if (requireFreshPress == value)
                    return;

                requireFreshPress = value;
                if (isSubscribed && inputAction != null)
                    freshPressGate.Arm(inputAction, requireFreshPress);
            }
        }

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

            freshPressGate.Arm(inputAction, requireFreshPress);

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

            freshPressGate.Disarm();
            isSubscribed = false;
        }

        /// <summary>
        /// Validates whether the current input action event is valid based on fresh press rules.
        /// Returns true if the event should be processed, or false if it should be ignored.
        /// </summary>
        protected bool ValidateFreshPress(bool isCancelEvent = false) =>
            freshPressGate.ShouldProcess(inputAction, isCancelEvent, requireFreshPress);

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
                if (selectButtonOnInputAction)
                    eventSystem.SetSelectedGameObject(button.gameObject);

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
    }
}
#endif
