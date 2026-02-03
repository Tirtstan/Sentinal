#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Serializable configuration for an action map's enabled state.
    /// </summary>
    [Serializable]
    public class ActionMapConfig
    {
        [Tooltip("The name of the action map.")]
        public string actionMapName;

        [Tooltip("Whether this action map should be enabled (true) or disabled (false).")]
        public bool enable;

        public ActionMapConfig(string actionMapName, bool enable)
        {
            this.actionMapName = actionMapName;
            this.enable = enable;
        }
    }
}
#endif
