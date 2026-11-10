#if ENABLE_INPUT_SYSTEM
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    /// <summary>Shows a binding of its player's action as text. The player follows <see cref="ViewPlayerComponent"/>.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DisplayInputString : ViewPlayerComponent
    {
        [Header("Action")]
        [SerializeField]
        private InputActionSelector inputActionSelector;

        [SerializeField]
        private int bindingIndex = 0;

        [Header("Display")]
        [SerializeField]
        [Tooltip("Text to display before the input string.")]
        private string prefix = "[";

        [SerializeField]
        [Tooltip("Text to display after the input string.")]
        private string suffix = "]";

        [SerializeField]
        private bool useCurrentControlScheme = true;

        private TextMeshProUGUI text;
        private bool warnedBindingIndex;

        public InputActionSelector InputActionSelector
        {
            get => inputActionSelector;
            set
            {
                inputActionSelector = value;
                warnedBindingIndex = false;
                UpdateDisplay();
            }
        }

        public int BindingIndex
        {
            get => bindingIndex;
            set
            {
                bindingIndex = Mathf.Max(0, value);
                warnedBindingIndex = false;
                UpdateDisplay();
            }
        }

        public string Prefix
        {
            get => prefix;
            set
            {
                prefix = value ?? string.Empty;
                UpdateDisplay();
            }
        }

        public string Suffix
        {
            get => suffix;
            set
            {
                suffix = value ?? string.Empty;
                UpdateDisplay();
            }
        }

        public bool UseCurrentControlScheme
        {
            get => useCurrentControlScheme;
            set
            {
                useCurrentControlScheme = value;
                warnedBindingIndex = false;
                UpdateDisplay();
            }
        }

        private PlayerInput controlsSource;

        private void Awake()
        {
            text = GetComponent<TextMeshProUGUI>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WatchControls(Player);
            UpdateDisplay();
        }

        protected override void OnDisable()
        {
            WatchControls(null);
            base.OnDisable();
        }

        protected override void OnPlayerChanged(PlayerInput previous, PlayerInput current)
        {
            if (isActiveAndEnabled)
                WatchControls(current);

            UpdateDisplay();
        }

        private void WatchControls(PlayerInput source)
        {
            if (controlsSource == source)
                return;

            if (controlsSource != null)
            {
                controlsSource.onControlsChanged -= OnControlsChanged;
                controlsSource.controlsChangedEvent.RemoveListener(OnControlsChanged);
            }

            controlsSource = source;
            if (controlsSource == null)
                return;

            PlayerNotifications behavior = controlsSource.notificationBehavior;
            if (behavior == PlayerNotifications.InvokeCSharpEvents)
                controlsSource.onControlsChanged += OnControlsChanged;
            else if (behavior == PlayerNotifications.InvokeUnityEvents)
                controlsSource.controlsChangedEvent.AddListener(OnControlsChanged);
            else
                Debug.LogError(
                    $"{nameof(DisplayInputString)} expected PlayerInput notificationBehavior InvokeCSharpEvents or InvokeUnityEvents; actual: {behavior}.",
                    controlsSource
                );
        }

        private void OnControlsChanged(PlayerInput input) => UpdateDisplay();

        public void UpdateDisplay()
        {
            PlayerInput playerInput = Player;
            if (playerInput == null || text == null)
                return;

            if (inputActionSelector == null || !inputActionSelector.IsValid())
            {
                text.SetText($"{prefix}N/A{suffix}");
                return;
            }

            InputAction action = inputActionSelector.FindAction(playerInput);
            if (action == null)
            {
                text.SetText($"{prefix}N/A{suffix}");
                return;
            }

            string inputString;
            if (useCurrentControlScheme && playerInput.currentControlScheme != null)
            {
                var bindings = action.bindings;
                int matchingBinding = -1;
                int matchingCount = 0;

                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i].groups.Contains(playerInput.currentControlScheme))
                    {
                        if (matchingCount == bindingIndex)
                            matchingBinding = i;

                        matchingCount++;
                    }
                }

                if (matchingBinding >= 0)
                    inputString = action.GetBindingDisplayString(matchingBinding);
                else
                    inputString = "N/A";

                WarnInvalidBindingIndex(action, matchingCount);
            }
            else
            {
                if (bindingIndex < action.bindings.Count)
                    inputString = action.GetBindingDisplayString(bindingIndex);
                else
                    inputString = "N/A";

                WarnInvalidBindingIndex(action, action.bindings.Count);
            }

            text.SetText($"{prefix}{inputString}{suffix}");
        }

        private void WarnInvalidBindingIndex(InputAction action, int availableBindings)
        {
            if (bindingIndex < availableBindings || warnedBindingIndex)
                return;

            Debug.LogWarning(
                $"[{nameof(DisplayInputString)}] '{name}' expected binding index 0 to {Mathf.Max(0, availableBindings - 1)} for action '{action.name}', but found {bindingIndex}.",
                this
            );
            warnedBindingIndex = true;
        }
    }
}
#endif
