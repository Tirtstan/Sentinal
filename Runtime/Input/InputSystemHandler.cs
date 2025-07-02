#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace MenuNavigation
{
    [RequireComponent(typeof(MenuNavigatorManager))]
    [AddComponentMenu("Menu Navigation/Input System Handler"), DisallowMultipleComponent]
    public class InputSystemHandler : MonoBehaviour
    {
        public static InputSystemHandler Instance { get; private set; }

        [Header("Input")]
        [SerializeField]
        private PlayerInput playerInput;

        [Header("Actions")]
        [SerializeField]
        [Tooltip("Action to close the current menu.")]
        private InputActionReference cancelAction;

        [SerializeField]
        [Tooltip("Action to refocus the last selected element within the current menu.")]
        private InputActionReference focusAction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (playerInput != null)
                SubscribeToInputActions();
        }

        public void SetPlayerInput(PlayerInput input)
        {
            playerInput = input;
            SubscribeToInputActions();
        }

        public PlayerInput GetPlayerInput() => playerInput;

        private void SubscribeToInputActions()
        {
            playerInput.actions.FindAction(cancelAction.action.id).performed += OnCancelPerformed;
            playerInput.actions.FindAction(focusAction.action.id).performed += OnFocusPerformed;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) =>
            MenuNavigatorManager.Instance.CloseCurrentMenu();

        private void OnFocusPerformed(InputAction.CallbackContext context)
        {
            if (MenuNavigatorManager.Instance.CurrentMenu != null)
                MenuNavigatorManager.Instance.CurrentMenu.Select();
        }

        private void OnDestroy()
        {
            playerInput.actions.FindAction(cancelAction.action.id).performed -= OnCancelPerformed;
            playerInput.actions.FindAction(focusAction.action.id).performed -= OnFocusPerformed;
        }
    }
}
#endif
