#if ENABLE_INPUT_SYSTEM
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    [RequireComponent(typeof(ViewSelector))]
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

        [SerializeField]
        [Tooltip(
            "Whether to switch the action map exclusively (i.e., disable the previous action map). If false, it will enable the new action map without disabling the previous one(s)."
        )]
        private bool exclusiveActionMapSwitching = true;
        private PlayerInput playerInput;
        private string previousActionMapName;

        private void OnEnable()
        {
            playerInput = PlayerInput.all.FirstOrDefault();
            if (playerInput == null)
                return;

            if (rememberPreviousActionMap)
                previousActionMapName = playerInput.currentActionMap.name;

            SwitchActionMap(onEnableActionMapName, exclusiveActionMapSwitching);
        }

        private void OnDisable()
        {
            if (SentinalManager.Instance.AnyViewsOpen)
                return;

            string actionMapName =
                rememberPreviousActionMap && !string.IsNullOrEmpty(previousActionMapName)
                    ? previousActionMapName
                    : onAllDisableActionMapName;

            SwitchActionMap(actionMapName, exclusiveActionMapSwitching);
        }

        private void SwitchActionMap(string actionMapName, bool exclusive)
        {
            if (playerInput == null || string.IsNullOrEmpty(actionMapName))
                return;

            if (exclusive)
            {
                playerInput.SwitchCurrentActionMap(actionMapName);
            }
            else
            {
                playerInput.actions.FindActionMap(actionMapName)?.Enable();
            }

            OnActionMapSwitch?.Invoke(playerInput);
        }
    }
}
#endif
