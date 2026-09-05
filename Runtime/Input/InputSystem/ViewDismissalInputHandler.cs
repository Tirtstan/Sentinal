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
        private InputActionSelector cancelAction = new("UI/Cancel");

        [SerializeField]
        [Tooltip("Action to refocus the last selected element (e.g., 'Focus').")]
        private InputActionSelector focusAction = new("UI/Focus");

        [SerializeField]
        [Tooltip("Fire on action release (canceled) instead of action press (performed).")]
        private bool cancelActionOnRelease;

        [SerializeField]
        [Tooltip(
            "Requires Cancel to be released before dismissing after the current view changes, preventing the same press from closing a newly focused view."
        )]
        private bool requireFreshPress = true;

        [Header("Grouping")]
        [SerializeField]
        [Tooltip("Optional group mask to filter which views can be dismissed by this handler. Defaults to Everything.")]
        private ViewGroupMask groupMask = ViewGroupMask.Everything;

        public ViewGroupMask GroupMask
        {
            get => groupMask;
            set => groupMask = value;
        }

        public PlayerInputSource InputSource => inputSource;

        public int PlayerKey => playerKey;

        public PlayerInput DirectPlayerInput => directPlayerInput;

        public int PlayerInputIndex => playerInputIndex;

        public InputActionSelector CancelAction
        {
            get => cancelAction;
            set => ReplaceActions(value, focusAction);
        }

        public InputActionSelector FocusAction
        {
            get => focusAction;
            set => ReplaceActions(cancelAction, value);
        }

        public bool CancelActionOnRelease
        {
            get => cancelActionOnRelease;
            set
            {
                if (cancelActionOnRelease == value)
                    return;

                bool resubscribe = isSubscribed;
                if (resubscribe)
                    UnsubscribeFromInputActions();

                cancelActionOnRelease = value;

                if (resubscribe)
                    SubscribeToInputActions();
            }
        }

        public bool RequireFreshPress
        {
            get => requireFreshPress;
            set
            {
                requireFreshPress = value;
                if (cancelInputAction != null)
                    cancelFreshPressGate.Arm(cancelInputAction, requireFreshPress);
            }
        }

        private PlayerInput playerInput;
        private InputAction cancelInputAction;
        private InputAction focusInputAction;
        private bool isSubscribed;
        private bool closeRequestedThisFrame;
        private ViewSelector pendingCloseView;
        private InputFreshPressGate cancelFreshPressGate;

        private void Awake()
        {
            playerInput = ResolvePlayerInput();

            if (playerInput != null)
                SubscribeToInputActions();
        }

        private void OnEnable()
        {
            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged += OnPlayerRoleChanged;

            SentinalViewRouter.OnSwitch += OnViewSwitch;

            if (playerInput != null && !isSubscribed)
                SubscribeToInputActions();
        }

        private void OnDisable()
        {
            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;

            SentinalViewRouter.OnSwitch -= OnViewSwitch;
            UnsubscribeFromInputActions();
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector nextView)
        {
            if (!requireFreshPress || cancelInputAction == null)
                return;

            cancelFreshPressGate.Arm(cancelInputAction, requireFreshPress);
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

        private PlayerInput ResolvePlayerInput()
        {
            return inputSource switch
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
                SetResolvedPlayerInput(ResolvePlayerInput());
        }

        public void ConfigurePlayerInputSource(
            PlayerInputSource source,
            int roleKey,
            PlayerInput directInput,
            int inputIndex
        )
        {
            if (isActiveAndEnabled && inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;

            inputSource = source;
            playerKey = roleKey;
            directPlayerInput = directInput;
            playerInputIndex = Mathf.Max(0, inputIndex);

            if (isActiveAndEnabled && inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged += OnPlayerRoleChanged;

            SetResolvedPlayerInput(ResolvePlayerInput());
        }

        public void ReplaceActions(InputActionSelector cancelSelector, InputActionSelector focusSelector)
        {
            bool resubscribe = isSubscribed;
            if (resubscribe)
                UnsubscribeFromInputActions();

            cancelAction = cancelSelector;
            focusAction = focusSelector;

            if (resubscribe)
                SubscribeToInputActions();
        }

        private void SetResolvedPlayerInput(PlayerInput resolvedPlayerInput)
        {
            if (playerInput == resolvedPlayerInput)
                return;

            UnsubscribeFromInputActions();
            playerInput = resolvedPlayerInput;

            if (isActiveAndEnabled && playerInput != null)
                SubscribeToInputActions();
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
                    cancelFreshPressGate.Arm(cancelInputAction, requireFreshPress);

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

            cancelFreshPressGate.Disarm();

            if (focusInputAction != null)
            {
                focusInputAction.performed -= OnFocusPerformed;
                focusInputAction = null;
            }

            isSubscribed = false;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!cancelFreshPressGate.ShouldProcess(cancelInputAction, isCancelEvent: false, requireFreshPress))
                return;

            RequestCloseCurrentView();
        }

        private void OnCancelCanceled(InputAction.CallbackContext context)
        {
            if (!cancelFreshPressGate.ShouldProcess(cancelInputAction, isCancelEvent: true, requireFreshPress))
                return;

            RequestCloseCurrentView();
        }

        private void RequestCloseCurrentView()
        {
            if (closeRequestedThisFrame)
                return;

            pendingCloseView = SentinalViewRouter.CurrentView;
            if (pendingCloseView == null || pendingCloseView.RootView)
                return;

            if (
                groupMask != ViewGroupMask.Everything
                && (groupMask & pendingCloseView.GroupMask) == ViewGroupMask.Nothing
            )
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
                if (!pendingCloseView.TryGetComponent(out ICloseableView closeable))
                    closeable = pendingCloseView.GetComponentInParent<ICloseableView>();
                closeable ??= pendingCloseView.GetComponentInChildren<ICloseableView>();

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
