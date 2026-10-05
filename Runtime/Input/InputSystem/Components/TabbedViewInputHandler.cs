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
        private InputActionSelector switchTabActionSelector = new("TabSwitch");

        [SerializeField]
        [Tooltip("Whether tab switching wraps around from the last tab to the first and vice versa.")]
        private bool wrapTabs = true;

        [SerializeField]
        [Tooltip(
            "Keep tab-switch input enabled while this handler's view or one of the Tabbed View's panels is focused. Other views suspend it."
        )]
        private bool includeTabPanelsInFocus;

        private InputAction switchTabAction;

        public TabbedView TabbedView
        {
            get => tabbedView;
            set
            {
                if (tabbedView == value)
                    return;

                if (isActiveAndEnabled && tabbedView != null)
                    tabbedView.TabsChanged -= OnTabsChanged;

                tabbedView = value;

                if (isActiveAndEnabled && tabbedView != null)
                    tabbedView.TabsChanged += OnTabsChanged;

                if (isActiveAndEnabled)
                    UpdateSubscription();
            }
        }

        public bool IncludeTabPanelsInFocus
        {
            get => includeTabPanelsInFocus;
            set
            {
                if (includeTabPanelsInFocus == value)
                    return;

                includeTabPanelsInFocus = value;
                if (isActiveAndEnabled)
                    UpdateSubscription();
            }
        }

        public InputActionSelector SwitchTabAction
        {
            get => switchTabActionSelector;
            set
            {
                if (ReferenceEquals(switchTabActionSelector, value))
                    return;

                bool resubscribe = isSubscribed;
                if (resubscribe)
                    Unsubscribe();

                switchTabActionSelector = value;

                if (resubscribe)
                    UpdateSubscription();
            }
        }

        public bool WrapTabs
        {
            get => wrapTabs;
            set => wrapTabs = value;
        }

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

        protected override void OnEnable()
        {
            base.OnEnable();
            SentinalViewRouter.OnSwitch += OnViewSwitch;
            if (tabbedView != null)
                tabbedView.TabsChanged += OnTabsChanged;
            UpdateSubscription();
        }

        protected override void OnDisable()
        {
            SentinalViewRouter.OnSwitch -= OnViewSwitch;
            if (tabbedView != null)
                tabbedView.TabsChanged -= OnTabsChanged;
            base.OnDisable();
        }

        public override bool ShouldSubscribe()
        {
            if (!includeTabPanelsInFocus || InputWhenCurrentMode != InputWhenCurrentMode.Inherit)
                return base.ShouldSubscribe();

            if (viewInputHandler == null || !viewInputHandler.isActiveAndEnabled || tabbedView == null)
                return false;

            ViewSelector currentView = SentinalViewRouter.CurrentView;
            if (currentView == null)
                return false;

            if (currentView == viewInputHandler.ViewSelector)
                return true;

            var panels = tabbedView.GroupPanels;
            for (int i = 0; i < panels.Count; i++)
            {
                if (currentView == panels[i])
                    return true;
            }

            return false;
        }

        private void OnViewSwitch(ViewSelector _, ViewSelector __) => UpdateSubscription();

        private void OnTabsChanged() => UpdateSubscription();

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
                tabbedView.Next(wrapTabs);
            }
            else if (input < 0)
            {
                tabbedView.Previous(wrapTabs);
            }
        }
    }
}
#endif
