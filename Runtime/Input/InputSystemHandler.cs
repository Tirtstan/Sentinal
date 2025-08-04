#if ENABLE_INPUT_SYSTEM
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    [AddComponentMenu("Sentinal/Input System Handler"), DisallowMultipleComponent]
    public class InputSystemHandler : MonoBehaviour
    {
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
            if (playerInput == null)
                playerInput = PlayerInput.all.FirstOrDefault();

            SubscribeToInputActions();
        }

        public void SetPlayerInput(PlayerInput input)
        {
            playerInput = input;
            SubscribeToInputActions();
        }

        private void SubscribeToInputActions()
        {
            playerInput.actions.FindAction(cancelAction.action.id).performed += OnCancelPerformed;
            playerInput.actions.FindAction(focusAction.action.id).performed += OnFocusPerformed;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) =>
            SentinalManager.Instance.CloseCurrentView();

        private void OnFocusPerformed(InputAction.CallbackContext context) =>
            SentinalManager.Instance.TrySelectCurrentView();

        private void OnDestroy()
        {
            if (playerInput != null)
            {
                playerInput.actions.FindAction(cancelAction.action.id).performed -= OnCancelPerformed;
                playerInput.actions.FindAction(focusAction.action.id).performed -= OnFocusPerformed;
            }
        }
    }
}
#endif
