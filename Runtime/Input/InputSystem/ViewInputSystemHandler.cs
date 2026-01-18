#if ENABLE_INPUT_SYSTEM
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    [AddComponentMenu("Sentinal/View Input System Handler"), DisallowMultipleComponent]
    public class ViewInputSystemHandler : ViewInputToggleBase, IPlayerInputHandler
    {
        /// <summary>
        /// Event fired when input state changes (enabled/disabled).
        /// </summary>
        public override event Action<bool> OnInputChanged;

        /// <summary>
        /// Event fired when PlayerInput reference changes.
        /// </summary>
        public event Action<PlayerInput> OnPlayerInputChanged;

        [Header("View Input")]
        [SerializeField]
        [Tooltip("Whether input is only enabled when the associated ViewSelector is the current view.")]
        private bool inputOnlyWhenCurrent = true;

        [SerializeField]
        [Tooltip("The ViewSelector to track for input toggling. Only used when inputOnlyWhenCurrent is true.")]
        private ViewSelector viewSelector;

        [Header("Direct Input")]
        [SerializeField]
        [Tooltip("The PlayerInput to use. If null and applyToAllPlayers is false, will attempt to find one by index.")]
        private PlayerInput playerInput;

        [SerializeField]
        [Tooltip("Player index to get PlayerInput from if playerInput is null.")]
        private int playerIndex = 0;

        [Header("Action Maps")]
        [SerializeField]
        [Tooltip(
            "If true, applies action map changes to all PlayerInputs. If false, uses the playerInput field for action map changes."
        )]
        private bool applyToAllPlayers;

        [SerializeField]
        [Tooltip("Action maps to enable when this handler is enabled.")]
        private string[] enableActionMaps = new string[] { "UI" };

        [SerializeField]
        [Tooltip("Action maps to disable when this handler is enabled.")]
        private string[] disableActionMaps = new string[0];

        private bool inputEnabled = true;
        private bool hasAppliedActionMaps = false;

        /// <summary>
        /// Gets or sets whether input is only enabled when the ViewSelector is current.
        /// </summary>
        public bool InputOnlyWhenCurrent
        {
            get => inputOnlyWhenCurrent;
            set => inputOnlyWhenCurrent = value;
        }

        /// <summary>
        /// Gets the associated ViewSelector, if any.
        /// </summary>
        public ViewSelector ViewSelector => viewSelector;

        private void Awake()
        {
            if (playerInput == null)
            {
                playerInput = PlayerInput.GetPlayerByIndex(playerIndex);
                if (playerInput == null)
                    playerInput = PlayerInput.all.FirstOrDefault();
            }

            if (inputOnlyWhenCurrent && viewSelector == null)
                viewSelector = GetComponent<ViewSelector>();
        }

        private void OnEnable()
        {
            if (inputOnlyWhenCurrent && viewSelector != null)
            {
                SentinalManager.OnSwitch += OnViewSwitch;

                if (SentinalManager.Instance != null && SentinalManager.Instance.IsCurrent(viewSelector))
                {
                    EnableInput();
                }
                else
                {
                    DisableInput();
                }
            }
            else if (!inputOnlyWhenCurrent)
            {
                EnableInput();
            }

            if (!HasViewSelectorOnSameObject() && HasActionMapsConfigured() && ActionMapManager.Instance != null)
            {
                ActionMapManager.Instance.ApplyActionMaps(this);
                hasAppliedActionMaps = true;
            }
        }

        private void OnDisable()
        {
            if (inputOnlyWhenCurrent)
                SentinalManager.OnSwitch -= OnViewSwitch;

            if (hasAppliedActionMaps && ActionMapManager.Instance != null)
            {
                ActionMapManager.Instance.RestoreActionMaps(this);
                hasAppliedActionMaps = false;
            }
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            if (!inputOnlyWhenCurrent || viewSelector == null)
                return;

            if (newView == viewSelector)
                EnableInput();
            else if (previousView == viewSelector)
                DisableInput();
        }

        /// <summary>
        /// Sets the ViewSelector reference. Only effective when inputOnlyWhenCurrent is true.
        /// </summary>
        public void SetViewSelector(ViewSelector selector)
        {
            if (viewSelector == selector)
                return;

            if (inputOnlyWhenCurrent && isActiveAndEnabled)
                SentinalManager.OnSwitch -= OnViewSwitch;

            viewSelector = selector;

            if (inputOnlyWhenCurrent && isActiveAndEnabled && viewSelector != null)
            {
                SentinalManager.OnSwitch += OnViewSwitch;

                if (SentinalManager.Instance != null && SentinalManager.Instance.IsCurrent(viewSelector))
                    EnableInput();
                else
                    DisableInput();
            }
        }

        public PlayerInput GetPlayerInput() => playerInput;

        public bool AppliesToAllPlayers() => applyToAllPlayers;

        public int GetPlayerIndex() => playerIndex;

        public string[] GetEnableActionMaps() => enableActionMaps;

        public string[] GetDisableActionMaps() => disableActionMaps;

        /// <summary>
        /// Checks if this handler has any action maps configured.
        /// </summary>
        public bool HasActionMapsConfigured() =>
            (enableActionMaps != null && enableActionMaps.Length > 0)
            || (disableActionMaps != null && disableActionMaps.Length > 0);

        /// <summary>
        /// Checks if there's a ViewSelector on the same GameObject.
        /// When present, ActionMapManager handles action maps via view switching.
        /// </summary>
        private bool HasViewSelectorOnSameObject() => viewSelector != null && viewSelector.gameObject == gameObject;

        public override void EnableInput()
        {
            inputEnabled = true;
            OnInputChanged?.Invoke(true);
        }

        public override void DisableInput()
        {
            inputEnabled = false;
            OnInputChanged?.Invoke(false);
        }

        /// <summary>
        /// Sets the PlayerInput reference. Useful for programmatic setup.
        /// </summary>
        public void SetPlayerInput(PlayerInput input)
        {
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
            SentinalManager.OnSwitch -= OnViewSwitch;
            OnInputChanged = null;
            OnPlayerInputChanged = null;
        }
    }
}
#endif
