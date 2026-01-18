#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
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

        private void UpdateDisplay()
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
                var matchingBindings = new List<int>();

                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i].groups.Contains(playerInput.currentControlScheme))
                        matchingBindings.Add(i);
                }

                if (matchingBindings.Count > 0)
                {
                    int targetIndex = Mathf.Clamp(bindingIndex, 0, matchingBindings.Count - 1);
                    inputString = action.GetBindingDisplayString(matchingBindings[targetIndex]);
                }
                else
                {
                    inputString = "N/A";
                }
            }
            else
            {
                inputString = action.GetBindingDisplayString(bindingIndex);
            }

            text.SetText($"{prefix}{inputString}{suffix}");
        }
    }
}
#endif
