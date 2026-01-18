#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// InputActionButton triggers a Button's onClick when a specific input action is performed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class InputActionButton : InputActionButtonBase
    {
        [SerializeField]
        [Tooltip("The button will be triggered on action release. If false, it will be triggered on action press.")]
        private bool triggerOnRelease;

        protected override void SubscribeToInputAction()
        {
            inputAction.performed += OnActionPerformed;
            inputAction.canceled += OnActionCanceled;
        }

        protected override void UnsubscribeFromInputAction()
        {
            inputAction.performed -= OnActionPerformed;
            inputAction.canceled -= OnActionCanceled;
        }

        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            if (sendPointerEvents)
            {
                if (eventSystem == null)
                    return;

                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.pointerDownHandler
                );
            }

            if (!triggerOnRelease)
                Click();
        }

        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            if (sendPointerEvents)
            {
                if (eventSystem == null)
                    return;

                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.pointerUpHandler
                );
            }

            if (triggerOnRelease)
                Click();
        }
    }
}
#endif
