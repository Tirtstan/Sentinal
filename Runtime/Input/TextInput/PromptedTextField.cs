using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sentinal.Input
{
    /// <summary>
    /// Button-backed label that opens <see cref="TextInputGateway"/> on click and stores the entered text.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Sentinal/Input/Prompted Text Field")]
    public class PromptedTextField : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private TextMeshProUGUI captionText;

        [SerializeField]
        private TextMeshProUGUI valueText;

        [Header("Prompt")]
        [SerializeField]
        private string header = "Enter text";

        [SerializeField]
        private string placeholder = string.Empty;

        [SerializeField]
        private bool multiline;

        [SerializeField]
        private int maxLength = 128;

        [Header("Events")]
        public UnityEvent<string> OnValueChanged;

        private Button button;
        private bool isPromptOpen;
        private string emptyDisplayText = string.Empty;
        private string value = string.Empty;
        private int promptClosedFrame = -1;

        public bool IsPromptOpen => isPromptOpen;

        public string Value
        {
            get => value;
            set => SetValue(value, notify: true);
        }

        private void Reset()
        {
            if (valueText == null)
                valueText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OpenPrompt);

            if (valueText == null)
            {
                Transform label = transform.Find("Text (TMP)");
                if (label != null)
                    valueText = label.GetComponent<TextMeshProUGUI>();
            }

            emptyDisplayText = captionText != null ? captionText.text : string.Empty;
            RefreshLabel();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenPrompt);
        }

        private void OnValidate() => RefreshLabel();

        /// <summary>
        /// Sets the stored value and updates the label without invoking <see cref="OnValueChanged"/>.
        /// </summary>
        public void SetValueWithoutNotify(string newValue)
        {
            value = newValue ?? string.Empty;
            RefreshLabel();
        }

        /// <summary>
        /// Returns the stored value, treating <see cref="emptyDisplayText"/> as empty.
        /// </summary>
        public string GetEffectiveValue()
        {
            if (string.IsNullOrEmpty(emptyDisplayText))
                return value?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(value) || value == emptyDisplayText)
                return string.Empty;

            return value.Trim();
        }

        public void OpenPrompt()
        {
            if (isPromptOpen || !isActiveAndEnabled || Time.frameCount == promptClosedFrame)
                return;

            if (!TextInputGateway.IsAvailable)
            {
                Debug.LogWarning(
                    $"{nameof(PromptedTextField)}: {nameof(TextInputGateway)} has no registered presenter.",
                    this
                );
                return;
            }

            isPromptOpen = true;
            TextInputPrompt prompt = multiline
                ? TextInputPrompt.MultiLine(header, placeholder, GetPromptInitialValue(value), maxLength)
                : TextInputPrompt.SingleLine(header, placeholder, GetPromptInitialValue(value), maxLength);

            TextInputGateway.Show(
                prompt,
                confirmed =>
                {
                    promptClosedFrame = Time.frameCount;
                    isPromptOpen = false;
                    ApplyConfirmedValue(confirmed);
                },
                () =>
                {
                    promptClosedFrame = Time.frameCount;
                    isPromptOpen = false;
                }
            );
        }

        private void SetValue(string newValue, bool notify)
        {
            string normalized = NormalizeStoredValue(newValue);
            if (normalized == value)
                return;

            value = normalized;
            RefreshLabel();

            if (notify)
                OnValueChanged?.Invoke(value);
        }

        private void ApplyConfirmedValue(string confirmed)
        {
            string normalized = NormalizeStoredValue(confirmed);
            if (normalized == value)
                return;

            value = normalized;
            RefreshLabel();
            OnValueChanged?.Invoke(value);
        }

        private void RefreshLabel()
        {
            if (valueText == null)
                return;

            valueText.SetText(GetDisplayText(value));
            bool hasValue = !string.IsNullOrEmpty(GetEffectiveValue());

            if (valueText.gameObject.activeSelf != hasValue)
                valueText.gameObject.SetActive(hasValue);

            if (captionText != null && captionText.gameObject.activeSelf == hasValue)
                captionText.gameObject.SetActive(!hasValue);
        }

        private string GetPromptInitialValue(string currentValue)
        {
            if (string.IsNullOrEmpty(emptyDisplayText))
                return currentValue?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentValue) || currentValue == emptyDisplayText)
                return string.Empty;

            return currentValue.Trim();
        }

        private string NormalizeStoredValue(string newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue))
                return emptyDisplayText;

            return newValue.Trim();
        }

        private string GetDisplayText(string currentValue)
        {
            if (!string.IsNullOrEmpty(emptyDisplayText) && string.IsNullOrWhiteSpace(currentValue))
                return emptyDisplayText;

            if (!string.IsNullOrEmpty(emptyDisplayText) && currentValue == emptyDisplayText)
                return emptyDisplayText;

            return string.IsNullOrWhiteSpace(currentValue) ? string.Empty : currentValue;
        }
    }
}
