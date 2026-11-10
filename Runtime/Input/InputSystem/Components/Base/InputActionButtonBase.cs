#if ENABLE_INPUT_SYSTEM
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Base for components that click a <see cref="Button"/> from an input action of their view's player.
    /// Provides common functionality for button interaction and pointer event handling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public abstract class InputActionButtonBase : ViewInputComponent
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

        protected Button button;
        protected InputAction inputAction;
        protected EventSystem eventSystem;

        protected bool CanInteract => button != null && button.IsActive() && button.IsInteractable();

        public InputActionSelector ActionSelector
        {
            get => actionSelector;
            set
            {
                if (ReferenceEquals(actionSelector, value))
                    return;

                bool wasListening = IsInputActive;
                if (wasListening)
                    Unsubscribe();

                actionSelector = value;

                if (wasListening)
                    Subscribe(Player);
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

        protected virtual void Awake()
        {
            button = GetComponent<Button>();
        }

        protected override void OnEnable()
        {
            eventSystem = EventSystem.current;
            base.OnEnable();
        }

        protected override void Subscribe(PlayerInput player)
        {
            if (actionSelector == null || !actionSelector.IsValid())
            {
                Debug.LogWarning($"{GetType().Name}: Action selector is not configured.", this);
                return;
            }

            inputAction = actionSelector.FindAction(player);
            if (inputAction == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: Input action '{actionSelector.GetDisplayName()}' not found on PlayerInput actions.",
                    this
                );
                return;
            }

            SubscribeToInputAction();
        }

        protected override void Unsubscribe()
        {
            if (inputAction == null)
                return;

            UnsubscribeFromInputAction();
            inputAction = null;
        }

        /// <summary>True when a callback of the button's action may act (fresh press, view accepts input).</summary>
        protected bool ValidateFreshPress(InputAction.CallbackContext context) => CanProcessInput(context);

        /// <summary>
        /// Called when subscribing to the input action. Override to subscribe to specific action events.
        /// </summary>
        protected abstract void SubscribeToInputAction();

        /// <summary>
        /// Called when unsubscribing from the input action. Override to unsubscribe from specific action events.
        /// </summary>
        protected abstract void UnsubscribeFromInputAction();

        /// <summary>
        /// Triggers the button's onClick event. A click queued by input is dropped if input stops before it runs.
        /// </summary>
        protected void Click()
        {
            if (!CanInteract)
                return;

            bool fromInput = IsInputActive;
            if (deferExecution)
                StartCoroutine(ClickNextFrame(fromInput));
            else
                ExecuteClick(fromInput);
        }

        private IEnumerator ClickNextFrame(bool fromInput)
        {
            yield return null;
            ExecuteClick(fromInput);
        }

        private void ExecuteClick(bool fromInput)
        {
            if (!CanInteract || (fromInput && !IsInputActive))
                return;

            if (sendPointerEvents && eventSystem != null)
            {
                if (selectButtonOnInputAction)
                    eventSystem.SetSelectedGameObject(button.gameObject);

                // End the synthetic press before onClick can change focus or disable this button.
                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.pointerUpHandler
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
    }
}
#endif
