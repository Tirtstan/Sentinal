#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Serializable configuration for how an action map should be applied by <see cref="ActionMapGate"/>.
    /// </summary>
    [Serializable]
    public class ActionMapConfig
    {
        [Tooltip("The name of the action map.")]
        public string actionMapName;

        [Tooltip("How this action map should be applied when the gate is active.")]
        public InputWhenCurrentMode applyMode = InputWhenCurrentMode.Inherit;

        public ActionMapConfig(
            string actionMapName,
            InputWhenCurrentMode applyMode = InputWhenCurrentMode.AlwaysEnabled
        )
        {
            this.actionMapName = actionMapName;
            this.applyMode = applyMode;
        }
    }
}
#endif
