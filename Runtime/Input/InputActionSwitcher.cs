#if ENABLE_INPUT_SYSTEM
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    [RequireComponent(typeof(SentinalViewSelector))]
    [AddComponentMenu("Sentinal/Input Action Switcher"), DisallowMultipleComponent]
    public class InputActionSwitcher : MonoBehaviour
    {
        public event Action<PlayerInput> OnActionMapSwitch;

        [Header("Configs")]
        [SerializeField]
        [Tooltip("The name of the action map to switch to when menus are opened.")]
        private string onEnableActionMapName = "UI";

        [SerializeField]
        [Tooltip("The name of the action map to switch to when ALL menus are closed.")]
        private string onAllDisableActionMapName = "Player";

        [SerializeField]
        [Tooltip(
            "Whether to remember the previous action map when switching. If true, this will ignore onAllDisableActionMapName."
        )]
        private bool rememberPreviousActionMap = true;

        [Header("Debug")]
        [SerializeField]
        private bool logSwitching;
        private PlayerInput playerInput;
        private string previousActionMapName;

        private void OnEnable()
        {
            playerInput = PlayerInput.all.FirstOrDefault();

            if (playerInput != null)
            {
                if (rememberPreviousActionMap)
                    previousActionMapName = playerInput.currentActionMap.name;

                if (logSwitching)
                    Debug.Log($"Switching from '{previousActionMapName}' to '{onEnableActionMapName}'");

                playerInput.SwitchCurrentActionMap(onEnableActionMapName);
                OnActionMapSwitch?.Invoke(playerInput);
            }
        }

        private void OnDisable()
        {
            if (SentinalManager.Instance.AnyViewsOpen)
                return;

            string actionMapName =
                rememberPreviousActionMap && !string.IsNullOrEmpty(previousActionMapName)
                    ? previousActionMapName
                    : onAllDisableActionMapName;

            if (playerInput != null && !string.IsNullOrEmpty(actionMapName))
            {
                if (logSwitching)
                    Debug.Log($"Switching back to '{actionMapName}'");

                playerInput.SwitchCurrentActionMap(actionMapName);
                OnActionMapSwitch?.Invoke(playerInput);
            }
        }
    }
}
#endif
