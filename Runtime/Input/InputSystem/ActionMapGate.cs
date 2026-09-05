#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Per-view component that controls which action maps are active when this view has focus.
    /// Applies explicit map state rules while this view has focus.
    /// <para>
    /// <b>Configured mode</b>: Apply per-map rules from a list (Enable/Disable/Inherit).<br/>
    /// <b>Exclusive mode</b>: Force-enable ONE map and disable all others (e.g. modal UI that must own all input).
    /// </para>
    /// <para>
    /// <b>Restore Previous Action Map State</b> (opt-in): captures each target player's map
    /// state when this gate applies and restores it according to <see cref="RestoreTiming"/>.
    /// <see cref="RestoreTiming.OnDisable"/> (default) holds the snapshot across refocusing and
    /// restores only when the gate is disabled while current, so nested views unwind like a stack.
    /// <see cref="RestoreTiming.OnFocusLost"/> restores as soon as the view loses focus, so the
    /// next view captures a clean base instead of this gate's applied state.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ViewSelector))]
    [AddComponentMenu("Sentinal/Action Map Gate"), DisallowMultipleComponent]
    public class ActionMapGate : MonoBehaviour
    {
        public enum TargetPlayers
        {
            [Tooltip("Use the player registered as Primary in SentinalPlayer.")]
            Primary,

            [Tooltip("Apply to all connected PlayerInputs.")]
            AllPlayers,

            [Tooltip("Use a specific player key from SentinalPlayer.")]
            SpecificKey,
        }

        public enum GateMode
        {
            [Tooltip("Enable/disable maps as configured in the list.")]
            Configured,

            [Tooltip("Force-enable ONE map exclusively, disable all others. Use for modal UIs.")]
            Exclusive,
        }

        public enum RestoreTiming
        {
            [Tooltip("Hold the snapshot across refocusing; restore only when this gate is disabled while its view is current. Use for nested modals that unwind like a stack.")]
            OnDisable,

            [Tooltip("Restore as soon as this view loses focus, so the next view captures a clean base. Use for sibling views that each clean up after themselves.")]
            OnFocusLost,
        }

        [Header("Settings")]
        [SerializeField]
        private TargetPlayers targetPlayers = TargetPlayers.AllPlayers;

        [SerializeField]
        [Tooltip("Player key to use when targetPlayers is SpecificKey.")]
        private int playerKey = SentinalPlayer.PrimaryKey;

        [SerializeField]
        private GateMode mode = GateMode.Configured;

        [SerializeField]
        [Tooltip("Action maps to configure when this view is focused.")]
        private ActionMapConfig[] actionMaps = new[] { new ActionMapConfig("UI", InputWhenCurrentMode.Inherit) };

        [SerializeField]
        [Tooltip("The single action map to force-enable. All others will be disabled.")]
        private string exclusiveMapName = "UI";

        [SerializeField]
        [Tooltip(
            "Captures each target player's action-map state when this gate applies, and restores it according to Restore Timing. OnDisable holds the snapshot across refocusing and restores only when the gate is disabled while current; OnFocusLost restores as soon as the view loses focus."
        )]
        private bool restorePreviousActionMapState;

        [SerializeField]
        [Tooltip("When a captured snapshot is restored. Only used while Restore Previous Action Map State is enabled.")]
        private RestoreTiming restoreTiming = RestoreTiming.OnDisable;

        private ViewSelector view;
        private bool isApplied;
        private readonly Dictionary<PlayerInput, PlayerActionMapSnapshot> appliedSnapshots = new();
        private ReadOnlyCollection<ActionMapConfig> readOnlyActionMaps;

        public TargetPlayers Target
        {
            get => targetPlayers;
            set => Reconfigure(value, playerKey, mode, exclusiveMapName, actionMaps);
        }

        public int PlayerKey
        {
            get => playerKey;
            set => Reconfigure(targetPlayers, value, mode, exclusiveMapName, actionMaps);
        }

        public GateMode Mode
        {
            get => mode;
            set => Reconfigure(targetPlayers, playerKey, value, exclusiveMapName, actionMaps);
        }

        public string ExclusiveMapName
        {
            get => exclusiveMapName;
            set => Reconfigure(targetPlayers, playerKey, mode, value, actionMaps);
        }

        public IReadOnlyList<ActionMapConfig> ActionMaps => readOnlyActionMaps ??= System.Array.AsReadOnly(actionMaps);

        public bool IsApplied => isApplied;

        public bool RestorePreviousActionMapState
        {
            get => restorePreviousActionMapState;
            set => restorePreviousActionMapState = value;
        }

        public RestoreTiming RestoreWhen
        {
            get => restoreTiming;
            set => restoreTiming = value;
        }

        private void Awake() => view = GetComponent<ViewSelector>();

        private void OnEnable()
        {
            SentinalViewRouter.OnSwitch += OnSwitch;

            if (SentinalViewRouter.IsCurrent(view))
                Apply();
            else
                isApplied = false;
        }

        private void OnDisable()
        {
            SentinalViewRouter.OnSwitch -= OnSwitch;

            // Only the current view owns the live map state. A background gate
            // dropping out must never restore over the focused view.
            if (SentinalViewRouter.IsCurrent(view))
                Restore();
            else
                ClearSnapshots();

            isApplied = false;
        }

        private void OnSwitch(ViewSelector prev, ViewSelector next)
        {
            if (next == view)
            {
                Apply();
            }
            else if (prev == view)
            {
                // Eager mode cleans up the moment focus leaves, so the next view
                // captures a clean base. Default mode holds the snapshot while
                // covered so nested views unwind like a stack on disable.
                if (restorePreviousActionMapState && restoreTiming == RestoreTiming.OnFocusLost)
                    Restore();

                isApplied = false;
            }
        }

        private void Apply()
        {
            PruneSnapshots();
            List<PlayerInput> players = GetTargetPlayers();

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

                // Capture once per focus session: refocusing must reuse the original
                // snapshot, and late joiners are captured on their first sighting.
                if (restorePreviousActionMapState && !appliedSnapshots.ContainsKey(player))
                    appliedSnapshots[player] = PlayerActionMapSnapshot.Capture(player);

                if (mode == GateMode.Exclusive)
                {
                    InputActionMap exclusiveMap = player.actions.FindActionMap(exclusiveMapName);
                    if (exclusiveMap != null)
                        player.SwitchCurrentActionMap(exclusiveMapName);
                }
                else
                {
                    foreach (ActionMapConfig config in actionMaps)
                    {
                        if (config == null || string.IsNullOrEmpty(config.ActionMapName))
                            continue;

                        InputActionMap map = player.actions.FindActionMap(config.ActionMapName);
                        if (map == null)
                            continue;

                        switch (config.ApplyMode)
                        {
                            case InputWhenCurrentMode.AlwaysEnabled:
                                if (!map.enabled)
                                    map.Enable();
                                break;
                            case InputWhenCurrentMode.AlwaysDisabled:
                                if (map.enabled)
                                    map.Disable();
                                break;
                            case InputWhenCurrentMode.Inherit:
                            default:
                                break;
                        }
                    }
                }
            }

            isApplied = true;
        }

        public void ReplaceActionMaps(IReadOnlyList<ActionMapConfig> configurations)
        {
            var replacement = new ActionMapConfig[configurations?.Count ?? 0];
            for (int i = 0; i < replacement.Length; i++)
                replacement[i] = configurations[i];

            Reconfigure(targetPlayers, playerKey, mode, exclusiveMapName, replacement);
        }

        public void Reconfigure(
            TargetPlayers targets,
            int specificPlayerKey,
            GateMode gateMode,
            string exclusiveActionMap,
            IReadOnlyList<ActionMapConfig> configurations
        )
        {
            Reconfigure(targets, specificPlayerKey, gateMode, exclusiveActionMap, configurations, restorePreviousActionMapState, restoreTiming);
        }

        public void Reconfigure(
            TargetPlayers targets,
            int specificPlayerKey,
            GateMode gateMode,
            string exclusiveActionMap,
            IReadOnlyList<ActionMapConfig> configurations,
            bool restorePreviousState,
            RestoreTiming restoreWhen
        )
        {
            bool shouldReapply = isActiveAndEnabled && SentinalViewRouter.IsCurrent(view);

            targetPlayers = targets;
            playerKey = specificPlayerKey;
            mode = gateMode;
            exclusiveMapName = exclusiveActionMap;
            restorePreviousActionMapState = restorePreviousState;
            restoreTiming = restoreWhen;

            actionMaps = new ActionMapConfig[configurations?.Count ?? 0];
            for (int i = 0; i < actionMaps.Length; i++)
                actionMaps[i] = configurations[i];

            readOnlyActionMaps = null;
            if (shouldReapply)
                Apply();
        }

        private void Restore()
        {
            foreach (KeyValuePair<PlayerInput, PlayerActionMapSnapshot> pair in appliedSnapshots)
            {
                if (pair.Key != null)
                    pair.Value.Restore(pair.Key);
            }

            ClearSnapshots();
        }

        private void ClearSnapshots() => appliedSnapshots.Clear();

        private void PruneSnapshots()
        {
            List<PlayerInput> dead = null;

            foreach (PlayerInput player in appliedSnapshots.Keys)
            {
                if (player == null)
                    (dead ??= new List<PlayerInput>()).Add(player);
            }

            if (dead == null)
                return;

            for (int i = 0; i < dead.Count; i++)
                appliedSnapshots.Remove(dead[i]);
        }

        private List<PlayerInput> GetTargetPlayers()
        {
            var list = new List<PlayerInput>();

            switch (targetPlayers)
            {
                case TargetPlayers.AllPlayers:
                    list.AddRange(PlayerInput.all);
                    break;

                case TargetPlayers.SpecificKey:
                    PlayerInput specific = SentinalPlayer.GetPlayer(playerKey);
                    if (specific != null)
                        list.Add(specific);
                    break;

                default:
                    PlayerInput primary = SentinalPlayer.PrimaryPlayer;
                    if (primary != null)
                        list.Add(primary);
                    break;
            }

            return list;
        }

        private void Reset()
        {
            if (view == null)
                view = GetComponent<ViewSelector>();
        }

        private sealed class PlayerActionMapSnapshot
        {
            private readonly Dictionary<string, bool> enabledStates = new();
            private string currentMapName;

            public static PlayerActionMapSnapshot Capture(PlayerInput player)
            {
                var snapshot = new PlayerActionMapSnapshot { currentMapName = player.currentActionMap?.name, };

                foreach (InputActionMap map in player.actions.actionMaps)
                    snapshot.enabledStates[map.name] = map.enabled;

                return snapshot;
            }

            public void Restore(PlayerInput player)
            {
                if (player.actions == null)
                    return;

                if (!string.IsNullOrEmpty(currentMapName) && player.actions.FindActionMap(currentMapName) != null)
                    player.SwitchCurrentActionMap(currentMapName);

                foreach (InputActionMap map in player.actions.actionMaps)
                {
                    if (!enabledStates.TryGetValue(map.name, out bool wasEnabled))
                        continue;

                    if (wasEnabled && !map.enabled)
                        map.Enable();
                    else if (!wasEnabled && map.enabled)
                        map.Disable();
                }
            }
        }
    }
}
#endif
