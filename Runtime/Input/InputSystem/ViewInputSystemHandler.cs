#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    public enum PlayerInputSource
    {
        [Tooltip("Resolve the player through SentinalPlayer role keys.")]
        SentinalPlayerRole,

        [Tooltip("Use a direct PlayerInput reference.")]
        DirectReference,

        [Tooltip("Use a PlayerInput from PlayerInput.all by index.")]
        PlayerInputIndex
    }

    public enum HandlerInputMode
    {
        [Tooltip("Input is enabled only when the associated View is the global current view in the ViewRouter.")]
        GlobalFocus,

        [Tooltip(
            "Input is enabled whenever this View's GameObject is active in the hierarchy, even if it's untracked or not globally focused. Useful for sub-views or tabs."
        )]
        LocalActive,

        [Tooltip("Input is always enabled as long as this component is enabled.")]
        Always
    }

    [AddComponentMenu("Sentinal/View Input System Handler"), DisallowMultipleComponent]
    public class ViewInputSystemHandler : ViewInputToggleBase, IPlayerInputHandler
    {
        public override event Action<bool> OnInputChanged;
        public event Action<PlayerInput> OnPlayerInputChanged;

        [Header("View Input")]
        [SerializeField]
        [Tooltip("How this handler decides when to enable input.")]
        private HandlerInputMode inputMode = HandlerInputMode.GlobalFocus;

        [SerializeField]
        [Tooltip("The ViewSelector to track for input toggling. Only used when mode is GlobalFocus or LocalActive.")]
        private ViewSelector viewSelector;

        [Header("Input Source")]
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

        private PlayerInput playerInput;
        private bool inputEnabled = true;

        public HandlerInputMode InputMode
        {
            get => inputMode;
            set
            {
                if (inputMode != value)
                {
                    inputMode = value;
                    EvaluateInputState();
                }
            }
        }

        public ViewSelector ViewSelector => viewSelector;

        private void Awake()
        {
            ResolvePlayerInput();

            if (inputMode != HandlerInputMode.Always && viewSelector == null)
                viewSelector = GetComponent<ViewSelector>();
        }

        private void OnEnable()
        {
            PlayerInput previousPlayerInput = playerInput;
            ResolvePlayerInput();

            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged += OnPlayerRoleChanged;

            if (previousPlayerInput != playerInput)
                OnPlayerInputChanged?.Invoke(playerInput);

            SentinalViewRouter.OnSwitch += OnViewSwitch;

            EvaluateInputState();
        }

        private void OnDisable()
        {
            if (inputSource == PlayerInputSource.SentinalPlayerRole)
                SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;

            SentinalViewRouter.OnSwitch -= OnViewSwitch;

            if (inputMode == HandlerInputMode.LocalActive)
                DisableInput();
        }

        private void OnPlayerRoleChanged(int key, PlayerInput newPlayer)
        {
            if (key != playerKey)
                return;

            if (playerInput != newPlayer)
            {
                playerInput = newPlayer;
                OnPlayerInputChanged?.Invoke(playerInput);
            }
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

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            if (inputMode == HandlerInputMode.GlobalFocus)
                EvaluateInputState();
        }

        public void EvaluateInputState()
        {
            if (!isActiveAndEnabled)
            {
                DisableInput();
                return;
            }

            switch (inputMode)
            {
                case HandlerInputMode.GlobalFocus:
                    if (viewSelector != null && SentinalViewRouter.IsCurrent(viewSelector))
                        EnableInput();
                    else
                        DisableInput();
                    break;
                case HandlerInputMode.LocalActive:
                    if (viewSelector != null && viewSelector.IsActive)
                        EnableInput();
                    else
                        DisableInput();
                    break;
                case HandlerInputMode.Always:
                    EnableInput();
                    break;
            }
        }

        public void SetViewSelector(ViewSelector selector)
        {
            if (viewSelector == selector)
                return;

            viewSelector = selector;
            EvaluateInputState();
        }

        public PlayerInput GetPlayerInput() => playerInput;

        public string GetTrackingPlayerInputName() => playerInput != null ? playerInput.name : "None";

        public int GetPlayerKey() => playerKey;

        public PlayerInputSource GetInputSource() => inputSource;

        public int GetPlayerInputIndex() => playerInputIndex;

        public override void EnableInput()
        {
            if (!inputEnabled)
            {
                inputEnabled = true;
                OnInputChanged?.Invoke(true);
            }
        }

        public override void DisableInput()
        {
            if (inputEnabled)
            {
                inputEnabled = false;
                OnInputChanged?.Invoke(false);
            }
        }

        public void SetPlayerInput(PlayerInput input)
        {
            directPlayerInput = input;
            if (inputSource == PlayerInputSource.DirectReference)
                ResolvePlayerInput();

            if (playerInput != input)
            {
                playerInput = input;
                OnPlayerInputChanged?.Invoke(playerInput);
            }
        }

        public override bool IsInputEnabled() => inputEnabled;

        private void Reset()
        {
            if (viewSelector == null)
                viewSelector = GetComponent<ViewSelector>();
        }

        private void OnDestroy()
        {
            SentinalPlayer.OnPlayerChanged -= OnPlayerRoleChanged;
            SentinalViewRouter.OnSwitch -= OnViewSwitch;
            OnInputChanged = null;
            OnPlayerInputChanged = null;
        }
    }
}
#endif
