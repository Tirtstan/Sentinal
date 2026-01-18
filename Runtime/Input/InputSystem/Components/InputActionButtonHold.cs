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
    /// Subscribes to a UI action with hold interaction and triggers a button's onClick event.
    /// Works with Sentinel ViewInputActionHandler and follows view focus rules.
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
        private const string HoldInteractionName = "Hold";

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
        }

        private void OnActionStarted(InputAction.CallbackContext context)
        {
            if (context.interaction is HoldInteraction hold)
            {
                SendPointerDown();
                OnHoldStarted?.Invoke(hold.duration);
            }
        }

        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            if (context.interaction is HoldInteraction)
                Click();
        }

        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            if (context.interaction is HoldInteraction)
            {
                SendPointerUp();
                OnHoldCanceled?.Invoke();
            }
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
