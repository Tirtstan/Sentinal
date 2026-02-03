#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using System;

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
        [Tooltip("If true, uses the action name. If false, uses the action reference.")]
        public bool useActionName = true;

        [Tooltip(
            "Action name to find across all action maps (e.g., 'Submit', 'Cancel'). Used when useActionName is true."
        )]
        public string actionName = "Submit";

        [Tooltip("Direct reference to a specific action. Used when useActionName is false.")]
        public InputActionReference actionReference;

        /// <summary>
        /// Finds the InputAction from the given PlayerInput based on the selector settings.
        /// </summary>
        /// <param name="playerInput">The PlayerInput to search in.</param>
        /// <returns>The found InputAction, or null if not found.</returns>
        public InputAction FindAction(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.actions == null)
                return null;

            EnsureSupportedNotificationBehavior(playerInput);

            InputAction inputAction;
            if (useActionName)
            {
                if (string.IsNullOrEmpty(actionName))
                    return null;

                inputAction = playerInput.actions.FindAction(actionName);
            }
            else
            {
                if (actionReference == null || actionReference.action == null)
                    return null;

                inputAction = actionReference.action;
            }

            inputAction?.Enable();
            return inputAction;
        }

        /// <summary>
        /// Ensures the PlayerInput.notificationBehavior is set to a mode supported by Sentinal
        /// (InvokeCSharpEvents or InvokeUnityEvents). If it is set to an unsupported mode
        /// (SendMessages/BroadcastMessages), it will be changed to InvokeCSharpEvents at runtime
        /// and a warning will be logged so the user can fix the setting in the inspector.
        /// </summary>
        private static void EnsureSupportedNotificationBehavior(PlayerInput playerInput)
        {
            if (playerInput == null)
                return;

            var behavior = playerInput.notificationBehavior;

            if (behavior == PlayerNotifications.InvokeCSharpEvents || behavior == PlayerNotifications.InvokeUnityEvents)
                return;

            var previous = behavior;
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            Debug.LogWarning(
                $"InputActionSelector: PlayerInput.notificationBehavior was '{previous}' and was automatically changed "
                    + $"to '{PlayerNotifications.InvokeCSharpEvents}' for Sentinal input integration. "
                    + "Please update this setting on the PlayerInput component in the inspector to avoid runtime changes.",
                playerInput
            );
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
