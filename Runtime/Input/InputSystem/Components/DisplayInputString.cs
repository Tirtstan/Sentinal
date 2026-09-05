#if ENABLE_INPUT_SYSTEM
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DisplayInputString : ViewInputSystemComponent
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

        protected override void Awake()
        {
            base.Awake();
            text = GetComponent<TextMeshProUGUI>();
        }

        protected override void OnPlayerInputRefreshed()
        {
            base.OnPlayerInputRefreshed();
            UpdateDisplay();
        }

        protected override void OnPlayerInputChanged(PlayerInput newPlayerInput)
        {
            base.OnPlayerInputChanged(newPlayerInput);
            UpdateDisplay();
        }

        protected override void OnControlsChanged(PlayerInput input)
        {
            base.OnControlsChanged(input);
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
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
