#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Sentinal.InputSystem
{
    public enum UIAuthorityMode
    {
        PrimaryPlayer,
        DynamicMostRecent,
        ExplicitAuthorityRequest,
        DeviceClassPriority,
    }

    /// <summary>
    /// Authored, local UI identity owner. The first enabled instance owns the statics; duplicates
    /// report both scene paths and disable themselves, except a scene copy of a persistent instance,
    /// which yields silently because the scene load destroys it. Disabling/destroying the owner clears
    /// identity and notifies consumers. No replacement instance or player is auto-created.
    /// </summary>
    [AddComponentMenu("Sentinal/UI Authority"), DisallowMultipleComponent]
    public sealed class UIAuthority : MonoBehaviour
    {
        private const string KeyboardAuthorityGroup = "Keyboard";
        private const string GamepadAuthorityGroup = "Gamepad";
        private const string JoystickAuthorityGroup = "Joystick";

        [Header("Authority")]
        [SerializeField]
        private UIAuthorityMode mode = UIAuthorityMode.DeviceClassPriority;

        [SerializeField, Tooltip("Required game component implementing IPrimaryPlayerResolver.")]
        private MonoBehaviour primaryPlayerResolver;

        [SerializeField, Tooltip("Optional request action. Resolved by GUID on each player's own action asset.")]
        private InputActionReference requestAction;

        [Header("Control schemes")]
        [SerializeField, Tooltip("Asset whose control schemes are offered by the exclusion dropdowns.")]
        private InputActionAsset controlSchemes;

        [SerializeField, ControlSchemeName(nameof(controlSchemes))]
        [Tooltip("Schemes that the game resolver cannot choose as Primary. Independent of authority tracking.")]
        private string[] primaryExcludedSchemes = Array.Empty<string>();

        [SerializeField, ControlSchemeName(nameof(controlSchemes))]
        [Tooltip("Schemes whose input cannot take authority. Dynamic mode can still admit paired mouse clicks/scroll.")]
        private string[] authorityExcludedSchemes = Array.Empty<string>();

        [Header("Dynamic most recent")]
        [SerializeField, Tooltip("Treat players using the same control scheme as one candidate in DynamicMostRecent.")]
        private bool collapseDuplicateControlSchemesForUi;

        [SerializeField, Tooltip("Allow mouse clicks and scroll, never mouse motion, to take keyboard authority.")]
        private bool trackMouseInputAsRecent = true;

        public static UIAuthority Instance { get; private set; }
        public static PlayerInput Current => Instance != null ? Instance.current : null;
        public static PlayerInput Primary => Instance != null ? Instance.primary : null;
        public static PlayerInput MostRecent => Instance != null ? Instance.mostRecent : null;
        public static event Action<PlayerInput> CurrentChanged;

        /// <summary>Any identity or retained-input roster change, for typed recipient sources.</summary>
        public static event Action PlayersChanged;

        /// <summary>Presentation only; performed pointer/non-pointer input drives hover feedback.</summary>
        public static event Action<bool> PointerInputChanged;

        public UIAuthorityMode Mode
        {
            get => mode;
            set
            {
                if (mode == value)
                    return;
                mode = value;
                PublishCurrent();
                PlayersChanged?.Invoke();
            }
        }

        public string CurrentReason =>
            mode == UIAuthorityMode.PrimaryPlayer
                ? "primary player policy"
                : mostRecent != null
                    ? recentReason
                    : "primary player fallback";

        /// <summary>
        /// Allows only always-on actions while a player controls an unsuspended player view.
        /// Evaluated before mode/device-class processing, including Request.
        /// </summary>
        internal Func<PlayerInput, InputAction, bool> ActionEligibility { get; set; }

        private IPrimaryPlayerResolver resolver;
        private PlayerInput primary;
        private PlayerInput mostRecent;
        private PlayerInput current;
        private int primaryPlayerIndex;
        private string recentReason = "primary player fallback";
        private readonly Dictionary<PlayerInput, Action<InputAction.CallbackContext>> playerActionHandlers = new();
        private readonly Dictionary<string, PlayerInput> authorityPlayersByDeviceClass =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PlayerInput> removedPlayers = new();

        private void OnEnable() => Initialize();

        internal void Initialize()
        {
            if (Instance == this)
                return;
            if (Instance != null && Instance != this)
            {
                // Every scene may author this component while the surviving instance is persistent.
                // That scene copy is destroyed by the same load, so it yields without an error.
                if (IsPersistent(Instance) && !IsPersistent(this))
                {
                    enabled = false;
                    return;
                }

                Debug.LogError(
                    $"[Sentinal] Expected one authored UIAuthority; actual: '{ObjectPath(Instance)}' and '{ObjectPath(this)}'. Keeping '{ObjectPath(Instance)}'; disabling '{ObjectPath(this)}'.",
                    this
                );
                enabled = false;
                return;
            }
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            Instance = this;
            ActionEligibility = PlayerInputMaps.IsAuthorityActionEligible;
            resolver = (IPrimaryPlayerResolver)primaryPlayerResolver;
            resolver.Changed += RefreshPrimary;
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += RefreshPlayers;
            RefreshPlayers();
        }

        private static bool IsPersistent(Component component) => component.gameObject.scene.name == "DontDestroyOnLoad";

        private static string ObjectPath(Component component)
        {
            string path = component.name;
            for (Transform parent = component.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return component.gameObject.scene.path + "/" + path;
        }

        private void OnDisable() => Shutdown();

        private void OnDestroy() => Shutdown();

        internal void Shutdown()
        {
            if (Instance != this)
                return;
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= RefreshPlayers;
            if (resolver != null)
                resolver.Changed -= RefreshPrimary;
            foreach (PlayerInput player in playerActionHandlers.Keys.ToArray())
                Unregister(player);
            authorityPlayersByDeviceClass.Clear();
            ActionEligibility = null;
            primary = mostRecent = current = null;
            Instance = null;
            SentinalViewRouter.SetAuthorityPlayer(null);
            CurrentChanged?.Invoke(null);
            PlayersChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (Instance != null)
                Instance.Shutdown();
            Instance = null;
            CurrentChanged = null;
            PlayersChanged = null;
            PointerInputChanged = null;
        }

        private bool ValidateConfiguration()
        {
            if (primaryPlayerResolver == null || primaryPlayerResolver is not IPrimaryPlayerResolver)
            {
                Debug.LogError(
                    $"[Sentinal] '{name}' expected {nameof(primaryPlayerResolver)} implementing IPrimaryPlayerResolver; actual: '{primaryPlayerResolver}'.",
                    this
                );
                return false;
            }
            if (controlSchemes == null)
            {
                Debug.LogError(
                    $"[Sentinal] '{name}' expected {nameof(controlSchemes)} InputActionAsset; actual: null.",
                    this
                );
                return false;
            }
            if (requestAction != null && requestAction.action == null)
            {
                Debug.LogError(
                    $"[Sentinal] '{name}' expected a valid optional {nameof(requestAction)}; actual: reference has no action.",
                    this
                );
                return false;
            }
            return ValidateSchemes(primaryExcludedSchemes, nameof(primaryExcludedSchemes))
                & ValidateSchemes(authorityExcludedSchemes, nameof(authorityExcludedSchemes));
        }

        private bool ValidateSchemes(string[] schemes, string field)
        {
            bool valid = true;
            foreach (string scheme in schemes)
            {
                if (controlSchemes.controlSchemes.Any(candidate => candidate.name == scheme))
                    continue;
                Debug.LogError(
                    $"[Sentinal] '{name}' expected {field} entry from '{controlSchemes.name}' schemes [{string.Join(", ", controlSchemes.controlSchemes.Select(candidate => candidate.name))}]; actual: '{scheme}'.",
                    this
                );
                valid = false;
            }
            return valid;
        }

        private void OnValidate()
        {
            if (Application.isPlaying && Instance == this && ValidateConfiguration())
            {
                RefreshPrimary();
                PlayersChanged?.Invoke();
            }
        }

        private void RefreshPlayers()
        {
            bool changed = false;
            removedPlayers.Clear();
            foreach (PlayerInput player in playerActionHandlers.Keys)
            {
                if (player != null && PlayerInput.all.Contains(player))
                    continue;
                removedPlayers.Add(player);
            }
            foreach (PlayerInput player in removedPlayers)
            {
                Unregister(player);
                changed = true;
            }
            foreach (PlayerInput player in PlayerInput.all)
            {
                if (playerActionHandlers.ContainsKey(player))
                    continue;
                if (player.actions == null || player.notificationBehavior != PlayerNotifications.InvokeCSharpEvents)
                {
                    // Register the diagnostic once; never change authored notification behavior.
                    Debug.LogError(
                        $"[Sentinal] '{player.name}' expected an action asset and notificationBehavior InvokeCSharpEvents; actual: actions '{player.actions}', behavior {player.notificationBehavior}.",
                        player
                    );
                    playerActionHandlers.Add(player, null);
                    continue;
                }
                Action<InputAction.CallbackContext> handler = context => HandlePlayerActionTriggered(player, context);
                playerActionHandlers.Add(player, handler);
                player.onActionTriggered += handler;
                player.onControlsChanged += OnControlsChanged;
                changed = true;
            }
            if (changed)
            {
                RefreshPrimary();
                PlayersChanged?.Invoke();
            }
        }

        private void Unregister(PlayerInput player)
        {
            Action<InputAction.CallbackContext> handler = playerActionHandlers[player];
            if (player != null)
            {
                player.onActionTriggered -= handler;
                player.onControlsChanged -= OnControlsChanged;
            }
            playerActionHandlers.Remove(player);
            RemoveAuthorityPlayer(player);
        }

        private void OnControlsChanged(PlayerInput player) => RefreshPrimary();

        /// <summary>Refreshes the primary policy after a game-owned session change.</summary>
        public void RefreshPrimary()
        {
            PlayerInput resolved = resolver.ResolvePrimaryPlayer(primaryPlayerIndex, IsPrimaryEligible);
            if (resolved != null && (!PlayerInput.all.Contains(resolved) || !IsPrimaryEligible(resolved)))
            {
                Debug.LogError(
                    $"[Sentinal] '{primaryPlayerResolver.name}' expected a registered, primary-eligible PlayerInput; actual: '{resolved.name}', scheme '{resolved.currentControlScheme}'.",
                    primaryPlayerResolver
                );
                return;
            }
            bool changed = primary != resolved;
            primary = resolved;
            if (mostRecent == null || !PlayerInput.all.Contains(mostRecent))
            {
                mostRecent = primary != null ? primary : PlayerInput.all.FirstOrDefault();
                recentReason = "primary player fallback";
                changed = true;
            }
            PublishCurrent();
            if (changed)
                PlayersChanged?.Invoke();
        }

        private bool IsPrimaryEligible(PlayerInput player) =>
            player != null
            && !Array.Exists(
                primaryExcludedSchemes,
                scheme => string.Equals(scheme, player.currentControlScheme, StringComparison.OrdinalIgnoreCase)
            );

        /// <summary>Records the explicit main-menu choice, then resolves the session policy and publishes immediately.</summary>
        public bool SetPrimaryPlayer(PlayerInput player)
        {
            if (player == null || !PlayerInput.all.Contains(player) || !IsPrimaryEligible(player))
                return false;
            primaryPlayerIndex = player.playerIndex;
            RefreshPrimary();
            return true;
        }

        private void SetMostRecent(PlayerInput player, string reason)
        {
            if (player == null)
                return;
            recentReason = reason;
            if (mostRecent == player)
                return;
            mostRecent = player;
            PublishCurrent();
            PlayersChanged?.Invoke();
        }

        private void PublishCurrent()
        {
            PlayerInput next = mode == UIAuthorityMode.PrimaryPlayer || mostRecent == null ? primary : mostRecent;
            if (ReferenceEquals(current, next))
                return;
            current = next;
            SentinalViewRouter.SetAuthorityPlayer(current);
            CurrentChanged?.Invoke(current);
        }

        private void HandlePlayerActionTriggered(PlayerInput playerInput, InputAction.CallbackContext context)
        {
            if (context.performed && TryGetInputDevice(context, out InputDevice sourceDevice))
                PointerInputChanged?.Invoke(sourceDevice is Pointer && sourceDevice is not Touchscreen);

            if (ActionEligibility != null && !ActionEligibility(playerInput, context.action))
                return;

            if (mode == UIAuthorityMode.PrimaryPlayer)
                return;

            if (mode == UIAuthorityMode.ExplicitAuthorityRequest)
            {
                if (ShouldRequestAuthority(playerInput, context))
                    SetMostRecent(playerInput, "pressed Request");

                return;
            }

            if (mode == UIAuthorityMode.DeviceClassPriority)
            {
                HandleDeviceClassPriorityInput(playerInput, context);
                return;
            }

            if (ShouldTrackRecentInput(playerInput, context, out InputDevice device))
                SetMostRecent(
                    ResolveMostRecentRepresentative(playerInput, device, collapseDuplicateControlSchemesForUi),
                    "most recent eligible input"
                );
        }

        private void HandleDeviceClassPriorityInput(PlayerInput playerInput, InputAction.CallbackContext context)
        {
            if (!context.performed || playerInput == null || IsControlSchemeExcluded(playerInput.currentControlScheme))
                return;

            if (!TryGetInputDevice(context, out InputDevice device) || device is Touchscreen)
                return;

            if (device is Mouse)
            {
                if (!CanPointerTakeKeyboardAuthority(playerInput, context.control))
                    return;

                authorityPlayersByDeviceClass[KeyboardAuthorityGroup] = playerInput;
                SetMostRecent(playerInput, "keyboard representative, mouse click or scroll");
                return;
            }

            if (device is Pointer)
                return;

            string authorityGroup = GetAuthorityGroup(device, playerInput.currentControlScheme);
            if (string.IsNullOrEmpty(authorityGroup))
                return;

            if (IsAuthorityRequest(playerInput, context.action))
            {
                authorityPlayersByDeviceClass[authorityGroup] = playerInput;
                SetMostRecent(playerInput, $"{authorityGroup} representative, pressed Request");
                return;
            }

            // Button interactions can perform on release, while axes returning to zero are not new authority input.
            if (context.control is not ButtonControl && !context.control.IsActuated())
                return;

            PlayerInput authorityPlayer = GetOrCreateAuthorityPlayer(authorityGroup);
            if (authorityPlayer == null || authorityPlayer != playerInput)
                return;

            SetMostRecent(authorityPlayer, $"{authorityGroup} representative, {context.action.name}");
        }

        private PlayerInput GetOrCreateAuthorityPlayer(string authorityGroup)
        {
            if (
                authorityPlayersByDeviceClass.TryGetValue(authorityGroup, out PlayerInput authorityPlayer)
                && IsValidAuthorityPlayer(authorityPlayer, authorityGroup)
            )
            {
                return authorityPlayer;
            }

            authorityPlayersByDeviceClass.Remove(authorityGroup);
            authorityPlayer = null;

            int lowestPlayerIndex = int.MaxValue;
            foreach (PlayerInput candidate in PlayerInput.all)
            {
                if (
                    candidate == null
                    || candidate.playerIndex >= lowestPlayerIndex
                    || IsControlSchemeExcluded(candidate.currentControlScheme)
                    || !UsesAuthorityGroup(candidate, authorityGroup)
                )
                {
                    continue;
                }

                authorityPlayer = candidate;
                lowestPlayerIndex = candidate.playerIndex;
            }

            if (authorityPlayer != null)
                authorityPlayersByDeviceClass[authorityGroup] = authorityPlayer;

            return authorityPlayer;
        }

        private bool IsValidAuthorityPlayer(PlayerInput playerInput, string authorityGroup)
        {
            return playerInput != null
                && IsJoinedPlayer(playerInput)
                && !IsControlSchemeExcluded(playerInput.currentControlScheme)
                && UsesAuthorityGroup(playerInput, authorityGroup);
        }

        private static bool IsJoinedPlayer(PlayerInput playerInput)
        {
            foreach (PlayerInput joinedPlayer in PlayerInput.all)
            {
                if (joinedPlayer == playerInput)
                    return true;
            }

            return false;
        }

        private static bool UsesAuthorityGroup(PlayerInput playerInput, string authorityGroup)
        {
            foreach (InputDevice device in playerInput.devices)
            {
                string candidateGroup = GetAuthorityGroup(device, playerInput.currentControlScheme);
                if (string.Equals(candidateGroup, authorityGroup, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetAuthorityGroup(InputDevice device, string controlScheme)
        {
            if (device is Keyboard)
                return KeyboardAuthorityGroup;
            if (device is Gamepad)
                return GamepadAuthorityGroup;
            if (device is Joystick)
                return JoystickAuthorityGroup;

            return !string.IsNullOrEmpty(device.layout) ? device.layout : controlScheme;
        }

        private void RemoveAuthorityPlayer(PlayerInput playerInput)
        {
            List<string> groupsToRemove = new();
            foreach (KeyValuePair<string, PlayerInput> entry in authorityPlayersByDeviceClass)
            {
                if (entry.Value == playerInput)
                    groupsToRemove.Add(entry.Key);
            }

            foreach (string authorityGroup in groupsToRemove)
                authorityPlayersByDeviceClass.Remove(authorityGroup);
        }

        private bool ShouldRequestAuthority(PlayerInput playerInput, InputAction.CallbackContext context)
        {
            if (!context.performed || playerInput == null)
                return false;

            if (!IsAuthorityRequest(playerInput, context.action))
                return false;

            return !IsControlSchemeExcluded(playerInput.currentControlScheme);
        }

        private bool IsAuthorityRequest(PlayerInput player, InputAction action)
        {
            if (requestAction == null || requestAction.action == null || player.actions == null)
                return false;

            // Missing in an input layout (e.g. Arcade) means this optional feature is off.
            return player.actions.FindAction(requestAction.action.id) == action;
        }

        private static bool TryGetInputDevice(InputAction.CallbackContext context, out InputDevice device)
        {
            device = null;

            InputControl control;
            try
            {
                control = context.control;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }

            if (control == null)
                return false;

            device = control.device;
            return device != null;
        }

        private bool ShouldTrackRecentInput(
            PlayerInput playerInput,
            InputAction.CallbackContext context,
            out InputDevice device
        )
        {
            device = null;

            if (!context.performed || playerInput == null)
                return false;

            if (!TryGetInputDevice(context, out device) || device is Touchscreen)
                return false;

            InputControl control = context.control;

            bool isPointer = device is Mouse or Pointer;
            bool controlSchemeExcluded = IsControlSchemeExcluded(playerInput.currentControlScheme);
            if (isPointer)
                return (!controlSchemeExcluded || HasMouse(playerInput)) && ShouldTrackPointerAction(control);

            return !controlSchemeExcluded;
        }

        private bool ShouldTrackPointerAction(InputControl control)
        {
            if (!trackMouseInputAsRecent)
                return false;

            if (control is ButtonControl)
                return control.IsActuated();

            string path = control.path;
            return !string.IsNullOrEmpty(path)
                && path.Contains("scroll", StringComparison.OrdinalIgnoreCase)
                && control.IsActuated();
        }

        private bool CanPointerTakeKeyboardAuthority(PlayerInput playerInput, InputControl control)
        {
            if (!ShouldTrackPointerAction(control))
                return false;

            foreach (InputDevice device in playerInput.devices)
            {
                if (device is Mouse)
                    return true;
            }

            return false;
        }

        private static bool HasMouse(PlayerInput player)
        {
            foreach (InputDevice device in player.devices)
                if (device is Mouse)
                    return true;
            return false;
        }

        private bool IsControlSchemeExcluded(string controlScheme)
        {
            if (string.IsNullOrEmpty(controlScheme))
                return false;

            return Array.Exists(
                authorityExcludedSchemes,
                x => string.Equals(x, controlScheme, StringComparison.OrdinalIgnoreCase)
            );
        }

        private int GetPrimaryDeviceId(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.devices.Count == 0)
                return -1;
            return playerInput.devices[0].deviceId;
        }

        private PlayerInput ResolveMostRecentRepresentative(
            PlayerInput playerInput,
            InputDevice sourceDevice,
            bool collapseDuplicateControlSchemes
        )
        {
            if (collapseDuplicateControlSchemes)
            {
                PlayerInput schemeRepresentative = FindControlSchemeRepresentative(playerInput.currentControlScheme);
                if (schemeRepresentative != null)
                    return schemeRepresentative;
            }

            int deviceId = sourceDevice != null ? sourceDevice.deviceId : GetPrimaryDeviceId(playerInput);
            if (deviceId == -1)
                return playerInput;

            PlayerInput representative = FindDeviceRepresentative(deviceId);
            return representative != null ? representative : playerInput;
        }

        private PlayerInput FindDeviceRepresentative(int deviceId)
        {
            PlayerInput lowestIndexPlayer = null;
            int lowestIndex = int.MaxValue;

            foreach (PlayerInput player in PlayerInput.all)
            {
                if (GetPrimaryDeviceId(player) == deviceId && player.playerIndex < lowestIndex)
                {
                    lowestIndex = player.playerIndex;
                    lowestIndexPlayer = player;
                }
            }

            return lowestIndexPlayer;
        }

        private PlayerInput FindControlSchemeRepresentative(string controlScheme)
        {
            if (string.IsNullOrEmpty(controlScheme))
                return null;

            PlayerInput lowestIndexPlayer = null;
            int lowestIndex = int.MaxValue;

            foreach (PlayerInput player in PlayerInput.all)
            {
                if (player == null || player.playerIndex >= lowestIndex)
                    continue;

                if (!string.Equals(player.currentControlScheme, controlScheme, StringComparison.OrdinalIgnoreCase))
                    continue;

                lowestIndex = player.playerIndex;
                lowestIndexPlayer = player;
            }

            return lowestIndexPlayer;
        }
    }
}
#endif
