#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MenuNavigation
{
    [RequireComponent(typeof(MenuNavigator))]
    [AddComponentMenu("Menu Navigation/Input Action Switcher"), DisallowMultipleComponent]
    public class InputActionSwitcher : MonoBehaviour
    {
        public event Action<string> OnActionMapSwitched;

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
            if (playerInput == null)
                playerInput = InputSystemHandler.Instance.GetPlayerInput();

            SwitchActionMap(onEnableActionMapName);
        }

        private void OnDisable()
        {
            if (!MenuNavigatorManager.Instance.AnyMenusOpen)
            {
                string actionMapName = rememberPreviousActionMap ? previousActionMapName : onAllDisableActionMapName;
                SwitchActionMap(actionMapName);
            }
        }

        public void SwitchActionMap(string actionMapName)
        {
            if (playerInput == null || string.IsNullOrEmpty(actionMapName))
                return;

            string currentActionMapName = playerInput.currentActionMap.name;

            if (currentActionMapName != actionMapName)
            {
                if (currentActionMapName != onEnableActionMapName)
                    previousActionMapName = currentActionMapName;

                playerInput.SwitchCurrentActionMap(actionMapName);
                OnActionMapSwitched?.Invoke(actionMapName);

                if (logSwitching)
                    Debug.Log($"Switched action map: <b>{currentActionMapName}</b> --> <b>{actionMapName}</b>");
            }
        }
    }
}
#endif
