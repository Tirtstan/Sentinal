using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinal.Samples
{
    [DisallowMultipleComponent]
    public class InputActionButton : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private Button button;

        [SerializeField]
        private InputActionReference inputActionReference;

        [Header("Configs")]
        [SerializeField]
        [Tooltip("This enables button visual feedback and re-selection.")]
        private bool sendPointerEvents = true;

        [SerializeField]
        [Tooltip("The button will be triggered on action release. If false, it will be triggered on action press.")]
        private bool triggerOnRelease;
        private EventSystem eventSystem;

        private void OnEnable()
        {
            inputActionReference.action.performed += OnActionPerformed;
            inputActionReference.action.canceled += OnActionCanceled;

            eventSystem = EventSystem.current;
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

        private void Click()
        {
            if (sendPointerEvents)
            {
                if (eventSystem == null)
                    return;

                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.selectHandler
                );

                ExecuteEvents.Execute(
                    button.gameObject,
                    new PointerEventData(eventSystem),
                    ExecuteEvents.pointerClickHandler
                );
            }

            button.onClick?.Invoke();
        }

        private void OnDisable()
        {
            inputActionReference.action.performed -= OnActionPerformed;
            inputActionReference.action.canceled -= OnActionCanceled;
        }

        private void Reset()
        {
            if (button == null)
                button = GetComponent<Button>();
        }
    }
}
