#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    [RequireComponent(typeof(SentinalViewSelector))]
    [AddComponentMenu("Sentinal/Input Action Switcher"), DisallowMultipleComponent]
    public class InputActionSwitcher : MonoBehaviour
    {
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

        private void OnEnable()
        {
            if (playerInput == null)
                playerInput = InputSystemHandler.Instance.GetPlayerInput();

            InputSystemHandler.Instance.SwitchActionMap(onEnableActionMapName);
        }

        private void OnDisable()
        {
            if (!Sentinal.Instance.AnyViewsOpen)
            {
                string actionMapName = rememberPreviousActionMap
                    ? InputSystemHandler.Instance.GetPreviousActionMapName()
                    : onAllDisableActionMapName;

                if (!string.IsNullOrEmpty(actionMapName))
                    InputSystemHandler.Instance.SwitchActionMap(actionMapName);
            }
        }
    }
}
#endif
