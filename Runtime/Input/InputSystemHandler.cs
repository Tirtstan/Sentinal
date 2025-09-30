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

        [SerializeField]
        [Tooltip("Player index to get PlayerInput from if above is null.")]
        private int playerIndex = 0;

        [Header("Actions")]
        [SerializeField]
        [Tooltip("Action to close the current menu.")]
        private InputActionReference cancelAction;

        [SerializeField]
        [Tooltip("Action to refocus the last selected element within the current menu.")]
        private InputActionReference focusAction;

        [Header("Configs")]
        [SerializeField]
        private bool cancelActionOnRelease;

        private void Awake()
        {
            if (playerInput == null)
                playerInput = PlayerInput.GetPlayerByIndex(playerIndex);

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
            playerInput.actions.FindAction(cancelAction.action.id).canceled += OnCancelCanceled;
            playerInput.actions.FindAction(focusAction.action.id).performed += OnFocusPerformed;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!cancelActionOnRelease)
                SentinalManager.Instance.CloseCurrentView();
        }

        private void OnCancelCanceled(InputAction.CallbackContext context)
        {
            if (cancelActionOnRelease)
                SentinalManager.Instance.CloseCurrentView();
        }

        private void OnFocusPerformed(InputAction.CallbackContext context) =>
            SentinalManager.Instance.TrySelectCurrentView();

        private void OnDestroy()
        {
            if (playerInput != null)
            {
                playerInput.actions.FindAction(cancelAction.action.id).performed -= OnCancelPerformed;
                playerInput.actions.FindAction(cancelAction.action.id).canceled -= OnCancelCanceled;
                playerInput.actions.FindAction(focusAction.action.id).performed -= OnFocusPerformed;
            }
        }
    }
}
#endif
