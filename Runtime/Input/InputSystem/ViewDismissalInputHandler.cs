#if ENABLE_INPUT_SYSTEM
using System.Collections;
using Sentinal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    [AddComponentMenu("Sentinal/View Dismissal Input Handler"), DisallowMultipleComponent]
    public class ViewDismissalInputHandler : MonoBehaviour, IPlayerInputHandler
    {
        [Header("Input")]
        [SerializeField]
        [Tooltip("Where to source PlayerInput from.")]
        private PlayerInputSource inputSource = PlayerInputSource.SentinalPlayerRole;

        [SerializeField]
        [Tooltip("Player key for SentinalPlayer lookup. 0 = Primary.")]
        private int playerKey = SentinalPlayer.PrimaryKey;

        [SerializeField]
        [Tooltip("Direct PlayerInput reference used when source is DirectReference.")]
        private PlayerInput directPlayerInput;

        [SerializeField]
        [Tooltip("Index into PlayerInput.all used when source is PlayerInputIndex.")]
        private int playerInputIndex;

        [Header("Actions")]
        [SerializeField]
        [Tooltip("Action to close the current menu (e.g., 'Cancel').")]
        private InputActionSelector cancelAction = new() { useActionName = true, actionName = "UI/Cancel" };

        [SerializeField]
        [Tooltip("Action to refocus the last selected element (e.g., 'Focus').")]
        private InputActionSelector focusAction = new() { useActionName = true, actionName = "UI/Focus" };

        [SerializeField]
        [Tooltip("Fire on action release (canceled) instead of action press (performed).")]
        private bool cancelActionOnRelease;

        [Header("Grouping")]
        [SerializeField]
        [Tooltip("Optional group mask to filter which views can be dismissed by this handler. Defaults to Everything.")]
        private ViewGroupMask groupMask = ViewGroupMask.Everything;

        public ViewGroupMask GroupMask
        {
            get => groupMask;
            set => groupMask = value;
        }

        private PlayerInput playerInput;
        private InputAction cancelInputAction;
        private InputAction focusInputAction;
        private bool isSubscribed;
        private bool closeRequestedThisFrame;
        private ViewSelector pendingCloseView;

        private void Awake()
        {
            ResolvePlayerInput();

            if (playerInput != null)
                SubscribeToInputActions();
        }

        private void OnEnable()
        {
            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged += OnPlayerRoleChanged;

            if (playerInput != null && !isSubscribed)
                SubscribeToInputActions();
        }

        private void OnDisable()
        {
            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;
            UnsubscribeFromInputActions();
        }

        private void OnPlayerRoleChanged(int key, PlayerInput newPlayer)
        {
            if (key != playerKey)
                return;

            UnsubscribeFromInputActions();
            playerInput = newPlayer;

            if (playerInput != null)
                SubscribeToInputActions();
        }

        private void ResolvePlayerInput()
        {
            playerInput = inputSource switch
            {
                PlayerInputSource.SentinalPlayerRole => SentinalPlayer.GetPlayer(playerKey),
                PlayerInputSource.PlayerInputIndex => SentinalPlayer.GetPlayerByIndex(playerInputIndex),
                _ => directPlayerInput,
            };
        }

        /// <summary>
        /// Sets the PlayerInput reference programmatically.
        /// </summary>
        public void SetPlayerInput(PlayerInput input)
        {
            directPlayerInput = input;
            if (inputSource == PlayerInputSource.DirectReference)
                ResolvePlayerInput();

            if (playerInput != input)
            {
                UnsubscribeFromInputActions();
                playerInput = input;

                if (playerInput != null)
                    SubscribeToInputActions();
            }
        }

        /// <summary>
        /// Gets the PlayerInput reference.
        /// </summary>
        public PlayerInput GetPlayerInput() => playerInput;

        public string GetTrackingPlayerInputName() => playerInput != null ? playerInput.name : "None";

        public PlayerInputSource GetInputSource() => inputSource;

        public int GetPlayerKey() => playerKey;

        public int GetPlayerInputIndex() => playerInputIndex;

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
                        cancelInputAction.canceled += OnCancelCanceled;
                    else
                        cancelInputAction.performed += OnCancelPerformed;
                }
            }

            if (focusAction != null && focusAction.IsValid())
            {
                focusInputAction = focusAction.FindAction(playerInput);
                if (focusInputAction != null)
                    focusInputAction.performed += OnFocusPerformed;
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
                    cancelInputAction.canceled -= OnCancelCanceled;
                else
                    cancelInputAction.performed -= OnCancelPerformed;

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
            if (closeRequestedThisFrame)
                return;

            pendingCloseView = SentinalViewRouter.CurrentView;
            if (pendingCloseView == null || pendingCloseView.RootView)
                return;

            if (groupMask.Value >= 0 && (groupMask.Value & pendingCloseView.GroupMask) == 0)
                return;

            closeRequestedThisFrame = true;
            StartCoroutine(CloseCurrentViewNextFrame());
        }

        private IEnumerator CloseCurrentViewNextFrame()
        {
            yield return null;

            closeRequestedThisFrame = false;
            if (pendingCloseView == null || pendingCloseView.RootView)
            {
                pendingCloseView = null;
                yield break;
            }

            if (pendingCloseView.IsActive)
            {
                var closeable = pendingCloseView.GetComponent<ICloseableView>();
                if (closeable == null)
                    closeable = pendingCloseView.GetComponentInParent<ICloseableView>();
                if (closeable == null)
                    closeable = pendingCloseView.GetComponentInChildren<ICloseableView>();

                if (closeable != null)
                    closeable.Close();
                else
                    pendingCloseView.Close();
            }

            pendingCloseView = null;
        }

        private void OnFocusPerformed(InputAction.CallbackContext context) => SentinalViewRouter.TrySelectCurrentView();

        private void OnDestroy()
        {
            SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;
            UnsubscribeFromInputActions();
        }
    }
}
#endif
