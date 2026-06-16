#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
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

        [Header("Target")]
        [SerializeField]
        private TargetPlayers targetPlayers = TargetPlayers.AllPlayers;

        [SerializeField]
        [Tooltip("Player key to use when targetPlayers is SpecificKey.")]
        private int playerKey = SentinalPlayer.PrimaryKey;

        [Header("Mode")]
        [SerializeField]
        private GateMode mode = GateMode.Configured;

        [Header("Configured Mode")]
        [SerializeField]
        [Tooltip("Action maps to configure when this view is focused.")]
        private ActionMapConfig[] actionMaps = new[] { new ActionMapConfig("UI", InputWhenCurrentMode.Inherit) };

        [Header("Exclusive Mode")]
        [SerializeField]
        [Tooltip("The single action map to force-enable. All others will be disabled.")]
        private string exclusiveMapName = "UI";

        private ViewSelector view;
        private bool isApplied;

        public TargetPlayers Target => targetPlayers;
        public GateMode Mode => mode;
        public string ExclusiveMapName => exclusiveMapName;
        public ActionMapConfig[] ActionMaps => actionMaps;
        public bool IsApplied => isApplied;

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
            isApplied = false;
        }

        private void OnSwitch(ViewSelector prev, ViewSelector next)
        {
            if (next == view)
                Apply();
            else if (prev == view)
                isApplied = false;
        }

        private void Apply()
        {
            List<PlayerInput> players = GetTargetPlayers();

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

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
                        if (string.IsNullOrEmpty(config.actionMapName))
                            continue;

                        InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                        if (map == null)
                            continue;

                        switch (config.applyMode)
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
    }
}
#endif
