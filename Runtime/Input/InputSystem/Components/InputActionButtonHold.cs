#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Clicks a button when its action's hold interaction completes. A hold in progress is cancelled whenever the
    /// component stops listening (focus lost, player changed), so a partial hold never carries into a later press.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class InputActionButtonHold : InputActionButtonBase
    {
        /// <summary>
        /// Invoked when a hold interaction starts. Parameter is the hold duration in seconds.
        /// </summary>
        public event Action<float> OnHoldStarted;

        /// <summary>
        /// Invoked when a hold interaction is canceled (before completion).
        /// </summary>
        public event Action OnHoldCanceled;
        public event Action<PlayerInput> OnHoldCompleted;

        /// <summary>Invoked only when the player releases a hold before completion.</summary>
        public event Action<PlayerInput> OnHoldReleasedBeforeCompletion;

        private const string HoldInteractionName = "Hold";

        private bool holding;

        protected override void SubscribeToInputAction()
        {
            if (
                string.IsNullOrEmpty(inputAction.interactions)
                || !inputAction.interactions.Contains(HoldInteractionName)
            )
            {
                Debug.LogWarning(
                    $"{GetType().Name}: Input action '{actionSelector.GetDisplayName()}' does not have a hold interaction.",
                    this
                );
                return;
            }

            inputAction.started += OnActionStarted;
            inputAction.performed += OnActionPerformed;
            inputAction.canceled += OnActionCanceled;
        }

        protected override void UnsubscribeFromInputAction()
        {
            inputAction.started -= OnActionStarted;
            inputAction.performed -= OnActionPerformed;
            inputAction.canceled -= OnActionCanceled;
            CancelHold();
        }

        private void OnActionStarted(InputAction.CallbackContext context)
        {
            if (!ValidateFreshPress(context) || !CanInteract)
                return;

            if (context.interaction is HoldInteraction hold)
            {
                holding = true;
                SendPointerDown();
                OnHoldStarted?.Invoke(hold.duration);
            }
        }

        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            if (!ValidateFreshPress(context) || !holding)
                return;

            if (context.interaction is HoldInteraction && CanInteract)
            {
                PlayerInput player = Player;
                holding = false;
                SendPointerUp();
                OnHoldCompleted?.Invoke(player);
                Click();
            }
        }

        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            if (!ValidateFreshPress(context))
                return;

            if (context.interaction is HoldInteraction && holding)
            {
                OnHoldReleasedBeforeCompletion?.Invoke(Player);
                CancelHold();
            }
        }

        private void CancelHold()
        {
            if (!holding)
                return;

            holding = false;
            SendPointerUp();
            OnHoldCanceled?.Invoke();
        }

        private void SendPointerDown()
        {
            if (!sendPointerEvents || eventSystem == null || button == null)
                return;

            ExecuteEvents.Execute(
                button.gameObject,
                new PointerEventData(eventSystem),
                ExecuteEvents.pointerDownHandler
            );
        }

        private void SendPointerUp()
        {
            if (!sendPointerEvents || eventSystem == null || button == null)
                return;

            ExecuteEvents.Execute(button.gameObject, new PointerEventData(eventSystem), ExecuteEvents.pointerUpHandler);
        }
    }
}
#endif
