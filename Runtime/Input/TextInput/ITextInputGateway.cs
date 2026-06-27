using System;

namespace Sentinal.Input
{
    /// <summary>
    /// Scene-level text input presenter registered with <see cref="TextInputGateway"/>.
    /// </summary>
    public interface ITextInputGateway
    {
        public bool IsShowing { get; }
        public void Show(TextInputPrompt prompt, Action<string> onConfirmed, Action onCancelled = null);
        public void ForceHide();
    }
}
