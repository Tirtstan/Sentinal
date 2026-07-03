#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Ignores input events that are part of a press already in progress when armed
    /// (e.g. when a view opens or when subscribe runs).
    /// </summary>
    public struct InputFreshPressGate
    {
        private bool wasPressedOnArm;

        public void Arm(InputAction action, bool requireFreshPress)
        {
            wasPressedOnArm = requireFreshPress && action != null && action.IsPressed();
        }

        public void Disarm() => wasPressedOnArm = false;

        /// <summary>
        /// Returns true when the event should be processed.
        /// </summary>
        public bool ShouldProcess(InputAction action, bool isCancelEvent, bool requireFreshPress)
        {
            if (!requireFreshPress || !wasPressedOnArm)
                return true;

            if (action == null)
                return false;

            if (!action.IsPressed() || isCancelEvent)
                wasPressedOnArm = false;

            return false;
        }
    }
}
#endif
