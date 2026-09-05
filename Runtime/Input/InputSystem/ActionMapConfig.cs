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
        [Header("Action Map")]
        [SerializeField]
        [Tooltip("The name of the action map.")]
        private string actionMapName;

        [SerializeField]
        [Tooltip("How this action map should be applied when the gate is active.")]
        private InputWhenCurrentMode applyMode = InputWhenCurrentMode.Inherit;

        public string ActionMapName => actionMapName;

        public InputWhenCurrentMode ApplyMode => applyMode;

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
