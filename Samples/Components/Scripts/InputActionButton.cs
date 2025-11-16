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

        [Header("View")]
        [SerializeField]
        [Tooltip(
            "If enabled, input actions will only work when the specified view is in focus. Prevents multiple action presses between complex view hierarchies."
        )]
        private bool inputWhenFocus;

        [SerializeField]
        private ViewSelector viewSelector;

        [Header("Configs")]
        [SerializeField]
        [Tooltip("This enables button visual feedback and re-selection.")]
        private bool sendPointerEvents = true;

        [SerializeField]
        [Tooltip("The button will be triggered on action release. If false, it will be triggered on action press.")]
        private bool triggerOnRelease;
        private EventSystem eventSystem;
        private bool isSubscribed;

        private void Awake()
        {
            if (inputWhenFocus && viewSelector == null)
                Debug.LogWarning(
                    "ViewSelector is required when 'Input When Focus' is enabled. Please assign a ViewSelector reference."
                );
        }

        private void OnEnable()
        {
            eventSystem = EventSystem.current;

            if (inputWhenFocus && viewSelector != null)
                SentinalManager.OnSwitch += OnViewSwitch;

            UpdateSubscription();
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            UpdateSubscription();
        }

        private void UpdateSubscription()
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

        private bool ShouldSubscribe()
        {
            if (!inputWhenFocus || viewSelector == null)
                return true;

            if (SentinalManager.Instance == null)
                return false;

            return SentinalManager.Instance.CurrentView == viewSelector;
        }

        private void Subscribe()
        {
            if (inputActionReference == null || inputActionReference.action == null)
                return;

            inputActionReference.action.performed += OnActionPerformed;
            inputActionReference.action.canceled += OnActionCanceled;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (inputActionReference == null || inputActionReference.action == null)
                return;

            inputActionReference.action.performed -= OnActionPerformed;
            inputActionReference.action.canceled -= OnActionCanceled;
            isSubscribed = false;
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
            Unsubscribe();

            if (inputWhenFocus && viewSelector != null)
                SentinalManager.OnSwitch -= OnViewSwitch;
        }

        private void Reset()
        {
            if (button == null)
                button = GetComponent<Button>();
        }
    }
}
