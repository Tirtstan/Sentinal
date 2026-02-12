#if ENABLE_INPUT_SYSTEM
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    [AddComponentMenu("Sentinal/View Dismissal Input Handler"), DisallowMultipleComponent]
    public class ViewDismissalInputHandler : MonoBehaviour, IPlayerInputHandler
    {
        [Header("Input")]
        [SerializeField]
        private PlayerInput playerInput;

        [SerializeField]
        [Tooltip("Player index to get PlayerInput from if above is null.")]
        private int playerIndex = 0;

        [Header("Actions")]
        [SerializeField]
        [Tooltip("Action to close the current menu (e.g., 'Cancel').")]
        private InputActionSelector cancelAction = new() { useActionName = true, actionName = "UI/Cancel" };

        [SerializeField]
        [Tooltip("Action to refocus the last selected element (e.g., 'Focus').")]
        private InputActionSelector focusAction = new() { useActionName = true, actionName = "UI/Focus" };

        [Header("Configs")]
        [SerializeField]
        private bool cancelActionOnRelease;

        private InputAction cancelInputAction;
        private InputAction focusInputAction;
        private bool isSubscribed;
        private bool closeRequestedThisFrame;

        private void Awake()
        {
            if (playerInput == null)
                playerInput = PlayerInput.GetPlayerByIndex(playerIndex);

            if (playerInput != null)
                SubscribeToInputActions();
        }

        private void OnEnable()
        {
            if (playerInput != null && !isSubscribed)
                SubscribeToInputActions();
        }

        private void OnDisable()
        {
            UnsubscribeFromInputActions();
        }

        /// <summary>
        /// Sets the PlayerInput reference programmatically.
        /// </summary>
        public void SetPlayerInput(PlayerInput input)
        {
            if (playerInput != input)
            {
                UnsubscribeFromInputActions();
                playerInput = input;

                if (playerInput != null)
                {
                    SubscribeToInputActions();
                }
            }
        }

        /// <summary>
        /// Gets the PlayerInput reference.
        /// </summary>
        public PlayerInput GetPlayerInput() => playerInput;

        private void SubscribeToInputActions()
        {
            if (isSubscribed || playerInput == null || playerInput.actions == null)
                return;

            if (cancelAction != null && cancelAction.IsValid())
            {
                cancelInputAction = cancelAction.FindAction(playerInput);
                if (cancelInputAction != null)
                {
                    if (cancelActionOnRelease)
                    {
                        cancelInputAction.canceled += OnCancelCanceled;
                    }
                    else
                    {
                        cancelInputAction.performed += OnCancelPerformed;
                    }
                }
            }

            if (focusAction != null && focusAction.IsValid())
            {
                focusInputAction = focusAction.FindAction(playerInput);
                if (focusInputAction != null)
                {
                    focusInputAction.performed += OnFocusPerformed;
                }
            }

            isSubscribed = true;
        }

        private void UnsubscribeFromInputActions()
        {
            if (!isSubscribed)
                return;

            if (cancelInputAction != null)
            {
                if (cancelActionOnRelease)
                {
                    cancelInputAction.canceled -= OnCancelCanceled;
                }
                else
                {
                    cancelInputAction.performed -= OnCancelPerformed;
                }
                cancelInputAction = null;
            }

            if (focusInputAction != null)
            {
                focusInputAction.performed -= OnFocusPerformed;
                focusInputAction = null;
            }

            isSubscribed = false;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) => RequestCloseCurrentView();

        private void OnCancelCanceled(InputAction.CallbackContext context) => RequestCloseCurrentView();

        private void RequestCloseCurrentView()
        {
            // Avoid multiple close requests within the same frame.
            if (closeRequestedThisFrame)
                return;

            closeRequestedThisFrame = true;
            StartCoroutine(CloseCurrentViewNextFrame());
        }

        private IEnumerator CloseCurrentViewNextFrame()
        {
            // Wait until the current input update & UI callbacks have fully finished.
            yield return null;

            closeRequestedThisFrame = false;

            if (SentinalManager.Instance != null)
                SentinalManager.Instance.CloseCurrentView();
        }

        private void OnFocusPerformed(InputAction.CallbackContext context) =>
            SentinalManager.Instance.TrySelectCurrentView();

        private void OnDestroy()
        {
            UnsubscribeFromInputActions();
        }
    }
}
#endif
