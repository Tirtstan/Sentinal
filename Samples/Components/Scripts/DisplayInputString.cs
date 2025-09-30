using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DisplayInputString : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference inputAction;

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
    private PlayerInput playerInput;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        playerInput = PlayerInput.GetPlayerByIndex(0);
    }

    private void OnEnable()
    {
        UpdateDisplay();
        playerInput.onControlsChanged += OnControlsChanged;
    }

    private void OnControlsChanged(PlayerInput input) => UpdateDisplay();

    private void UpdateDisplay()
    {
        string inputString;

        if (useCurrentControlScheme && playerInput.currentControlScheme != null)
        {
            var bindings = inputAction.action.bindings;
            var matchingBindings = new List<int>();

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].groups.Contains(playerInput.currentControlScheme))
                    matchingBindings.Add(i);
            }

            if (matchingBindings.Count > 0)
            {
                int targetIndex = Mathf.Clamp(bindingIndex, 0, matchingBindings.Count - 1);
                inputString = inputAction.action.GetBindingDisplayString(matchingBindings[targetIndex]);
            }
            else
            {
                inputString = "N/A";
            }
        }
        else
        {
            inputString = inputAction.action.GetBindingDisplayString(bindingIndex);
        }

        text.SetText($"{prefix}{inputString}{suffix}");
    }

    private void OnDisable()
    {
        if (playerInput != null)
            playerInput.onControlsChanged -= OnControlsChanged;
    }
}
