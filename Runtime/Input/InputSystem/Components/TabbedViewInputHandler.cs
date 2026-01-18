#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    [DisallowMultipleComponent]
    public class TabbedViewInputHandler : ViewInputActionHandler
    {
        [Header("Tabbed View")]
        [SerializeField]
        [Tooltip("The TabbedView component to control.")]
        private TabbedView tabbedView;

        [SerializeField]
        [Tooltip(
            "Input action for switching tabs (e.g., 'Navigate' or 'TabSwitch'). Ideally a Value type with Axis Control."
        )]
        private InputActionSelector switchTabActionSelector = new() { useActionName = true, actionName = "TabSwitch" };

        private InputAction switchTabAction;

        protected override void Reset()
        {
            base.Reset();

            if (tabbedView == null)
                tabbedView = GetComponent<TabbedView>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (tabbedView == null)
            {
                if (!TryGetComponent(out tabbedView))
                {
                    Debug.LogWarning(
                        $"{GetType().Name}: TabbedView is not assigned and not found on this GameObject. No tabs will be switched.",
                        this
                    );
                }
            }
        }

        protected override void Subscribe()
        {
            if (playerInput == null || playerInput.actions == null)
                return;

            if (switchTabActionSelector == null || !switchTabActionSelector.IsValid())
            {
                Debug.LogWarning($"{GetType().Name}: Switch tab action selector is not configured.", this);
                return;
            }

            switchTabAction = switchTabActionSelector.FindAction(playerInput);
            if (switchTabAction == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: Input action '{switchTabActionSelector.GetDisplayName()}' not found on PlayerInput actions.",
                    this
                );
                return;
            }

            switchTabAction.performed += OnSwitchTab;
            isSubscribed = true;
        }

        protected override void Unsubscribe()
        {
            if (switchTabAction != null)
            {
                switchTabAction.performed -= OnSwitchTab;
                switchTabAction = null;
            }

            isSubscribed = false;
        }

        private void OnSwitchTab(InputAction.CallbackContext context)
        {
            if (tabbedView == null)
                return;

            float input = context.ReadValue<float>();

            if (input > 0)
            {
                tabbedView.Next();
            }
            else if (input < 0)
            {
                tabbedView.Previous();
            }
        }
    }
}
#endif
