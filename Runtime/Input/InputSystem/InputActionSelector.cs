#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using System;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Flexible input action selector that supports both string names and InputActionReference.
    /// Use string names to subscribe to actions across multiple action maps (e.g., "Submit" in UI and Gameplay).
    /// Use InputActionReference to target a specific action in a specific action map.
    /// </summary>
    [Serializable]
    public class InputActionSelector
    {
        [Header("Selection")]
        [SerializeField]
        [Tooltip("If true, uses the action name. If false, uses the action reference.")]
        private bool useActionName = true;

        [SerializeField]
        [Tooltip(
            "Action name to find across all action maps (e.g., 'Submit', 'Cancel'). Used when useActionName is true."
        )]
        private string actionName = "Submit";

        [SerializeField]
        [Tooltip("Direct reference to a specific action. Used when useActionName is false.")]
        private InputActionReference actionReference;

        public bool UseActionName => useActionName;

        public string ActionName => actionName;

        public InputActionReference ActionReference => actionReference;

        public InputActionSelector() { }

        public InputActionSelector(string actionName)
        {
            useActionName = true;
            this.actionName = actionName;
        }

        public InputActionSelector(InputActionReference actionReference)
        {
            useActionName = false;
            this.actionReference = actionReference;
        }

        /// <summary>
        /// Resolves an action in the supplied asset without enabling it. References are resolved
        /// by ID so selectors also work with player-specific copies of the asset.
        /// </summary>
        public InputAction FindAction(InputActionAsset asset)
        {
            if (asset == null)
                return null;

            if (useActionName)
                return string.IsNullOrEmpty(actionName) ? null : asset.FindAction(actionName);

            if (actionReference == null || actionReference.action == null)
                return null;

            return asset.FindAction(actionReference.action.id);
        }

        /// <summary>
        /// Finds the InputAction from the given PlayerInput based on the selector settings.
        /// </summary>
        /// <param name="playerInput">The PlayerInput to search in.</param>
        /// <returns>The found InputAction, or null if not found.</returns>
        public InputAction FindAction(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.actions == null)
                return null;

            return FindAction(playerInput.actions);
        }

        /// <summary>
        /// Gets a display name for this action selector.
        /// </summary>
        public string GetDisplayName()
        {
            if (useActionName)
                return string.IsNullOrEmpty(actionName) ? "None" : actionName;
            else
                return actionReference != null && actionReference.action != null ? actionReference.action.name : "None";
        }

        /// <summary>
        /// Checks if this selector is valid (has either a name or reference).
        /// </summary>
        public bool IsValid()
        {
            if (useActionName)
                return !string.IsNullOrEmpty(actionName);
            else
                return actionReference != null && actionReference.action != null;
        }
    }
}
#endif
