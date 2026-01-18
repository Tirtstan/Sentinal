using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Defines how input components should respond to ViewInputSystemHandler's input enabled state.
    /// This allows overriding the default behavior inherited from the handler.
    /// </summary>
    public enum InputWhenCurrentMode
    {
        [Tooltip("Follow ViewInputSystemHandler's input state (default behavior).")]
        Inherit = 0,

        [Tooltip("Always disabled, regardless of the handler's state.")]
        AlwaysDisabled = 1,

        [Tooltip("Always enabled, regardless of the handler's state.")]
        AlwaysEnabled = 2,
    }
}
