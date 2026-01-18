using System;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Abstract base class for view input handlers.
    /// </summary>
    public abstract class ViewInputToggleBase : MonoBehaviour, IViewInputToggle
    {
        public abstract event Action<bool> OnInputChanged;
        public abstract bool IsInputEnabled();
        public abstract void EnableInput();
        public abstract void DisableInput();
    }
}
