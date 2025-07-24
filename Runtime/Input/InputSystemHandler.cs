#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    [RequireComponent(typeof(Sentinal))]
    [AddComponentMenu("Sentinal/Input System Handler"), DisallowMultipleComponent]
    public class InputSystemHandler : MonoBehaviour
    {
        public static InputSystemHandler Instance { get; private set; }
        public event Action<PlayerInput> OnActionMapSwitched;

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

        private string previousActionMapName;

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

        public void SwitchActionMap(string actionMapName)
        {
            if (playerInput == null || string.IsNullOrEmpty(actionMapName))
                return;

            string currentActionMapName = playerInput.currentActionMap.name;
            if (currentActionMapName != actionMapName)
            {
                previousActionMapName = currentActionMapName;

                playerInput.SwitchCurrentActionMap(actionMapName);
                OnActionMapSwitched?.Invoke(playerInput);
            }
        }

        public string GetPreviousActionMapName() => previousActionMapName;

        private void SubscribeToInputActions()
        {
            playerInput.actions.FindAction(cancelAction.action.id).performed += OnCancelPerformed;
            playerInput.actions.FindAction(focusAction.action.id).performed += OnFocusPerformed;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) => Sentinal.Instance.CloseCurrentView();

        private void OnFocusPerformed(InputAction.CallbackContext context)
        {
            if (Sentinal.Instance.CurrentView != null)
            {
                if (Sentinal.Instance.CurrentView.TryGetComponent(out ISentinalSelector sentinalView))
                    sentinalView.Select();
            }
        }

        private void OnDestroy()
        {
            playerInput.actions.FindAction(cancelAction.action.id).performed -= OnCancelPerformed;
            playerInput.actions.FindAction(focusAction.action.id).performed -= OnFocusPerformed;
        }
    }
}
#endif
