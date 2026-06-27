using System;
using UnityEngine;

namespace Sentinal.Input
{
    /// <summary>
    /// Global access point for modal text entry. A persistent presenter (for example on PersistentCanvas)
    /// registers itself at startup.
    /// </summary>
    public static class TextInputGateway
    {
        public static ITextInputGateway Instance { get; private set; }

        public static bool IsAvailable => Instance != null;
        public static bool IsShowing => IsAvailable && Instance.IsShowing;

        public static void Register(ITextInputGateway gateway) => Instance = gateway;

        public static void Unregister(ITextInputGateway gateway)
        {
            if (Instance == gateway)
                Instance = null;
        }

        public static void Show(TextInputPrompt prompt, Action<string> onConfirmed, Action onCancelled = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning($"{nameof(TextInputGateway)}: no presenter is registered.");
                onCancelled?.Invoke();
                return;
            }

            Instance.Show(prompt, onConfirmed, onCancelled);
        }
    }
}
