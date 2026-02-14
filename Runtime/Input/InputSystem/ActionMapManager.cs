#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    public class ActionMapManager : MonoBehaviour
    {
        public static ActionMapManager Instance { get; private set; }

        private class ActionMapSnapshot
        {
            public PlayerInput playerInput;
            public Dictionary<string, bool> state = new();
        }

        [Header("Default Action Maps")]
        [SerializeField]
        [Tooltip(
            "If true, applies defaultActionMaps when no non-root views are open. If false, uses current in-memory state."
        )]
        private bool useDefaultActionMaps = true;

        [SerializeField]
        [Tooltip("Action maps to apply when no non-root views are open.")]
        private ActionMapConfig[] defaultActionMaps = Array.Empty<ActionMapConfig>();

        private readonly Dictionary<ViewInputSystemHandler, List<ActionMapSnapshot>> handlerSnapshots = new();
        private readonly List<ActionMapSnapshot> defaultActionMapSnapshots = new();
        private readonly Dictionary<ViewSelector, ViewInputSystemHandler> handlerCache = new();
        private bool defaultsApplied = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SentinalManager.OnSwitch += OnViewSwitch;
            SentinalManager.OnRemove += OnViewRemoved;
            SentinalManager.OnAdd += OnViewAdded;
        }

        private void Start()
        {
            CheckAndApplyDefaults();
        }

        private void OnDestroy()
        {
            SentinalManager.OnSwitch -= OnViewSwitch;
            SentinalManager.OnRemove -= OnViewRemoved;
            SentinalManager.OnAdd -= OnViewAdded;
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            // Only modify action maps when the target view explicitly has a handler on the same GameObject.
            if (newView != null && TryGetCachedHandler(newView, out ViewInputSystemHandler newHandler))
            {
                if (previousView != null && TryGetCachedHandler(previousView, out ViewInputSystemHandler prevHandler))
                    RestoreHandler(prevHandler);

                ApplyHandler(newHandler);
            }

            CheckAndApplyDefaults();
        }

        private bool TryGetCachedHandler(ViewSelector view, out ViewInputSystemHandler handler)
        {
            if (view == null)
            {
                handler = null;
                return false;
            }

            if (handlerCache.TryGetValue(view, out handler))
                return handler != null;

            // Only cache handlers that are on the same GameObject as the ViewSelector
            bool found = view.TryGetComponent(out handler) && handler != null;
            handlerCache[view] = found ? handler : null;
            return found;
        }

        /// <summary>
        /// Checks if there are any non-root views in the cache that manage input.
        /// Views without a handler are never cached, so this is a fast dictionary iteration.
        /// </summary>
        private bool AnyNonRootViewsWithInputHandlersOpen()
        {
            foreach (var kvp in handlerCache)
            {
                if (kvp.Key != null && !kvp.Key.RootView && kvp.Value != null)
                    return true;
            }

            return false;
        }

        private void OnViewAdded(ViewSelector view)
        {
            if (view != null)
                TryGetCachedHandler(view, out _);

            CheckAndApplyDefaults();
        }

        private void OnViewRemoved(ViewSelector view)
        {
            if (view == null)
                return;

            if (TryGetCachedHandler(view, out ViewInputSystemHandler handler))
            {
                RestoreHandler(handler);
                handlerSnapshots.Remove(handler);
            }

            handlerCache.Remove(view);

            if (SentinalManager.Instance != null && SentinalManager.Instance.AnyViewsOpen)
            {
                ViewSelector currentView = SentinalManager.Instance.CurrentView;
                if (currentView != null && TryGetCachedHandler(currentView, out ViewInputSystemHandler currentHandler))
                    ApplyHandler(currentHandler);
            }

            CheckAndApplyDefaults();
        }

        private void CheckAndApplyDefaults()
        {
            if (SentinalManager.Instance == null)
                return;

            if (!useDefaultActionMaps)
            {
                if (defaultsApplied)
                    RestoreDefaultActionMaps();

                return;
            }

            // Decide whether defaults should be active:
            // - If Sentinal reports no non-root views open at all, always apply defaults.
            // - Otherwise, only consider non-root views that actually have a ViewInputSystemHandler;
            //   views without input management should not influence default action maps.
            bool shouldApplyDefaults =
                !SentinalManager.Instance.AnyNonRootViewsOpen || !AnyNonRootViewsWithInputHandlersOpen();

            if (shouldApplyDefaults && !defaultsApplied)
            {
                ApplyDefaultActionMaps();
            }
            else if (!shouldApplyDefaults && defaultsApplied)
            {
                RestoreDefaultActionMaps();
            }
        }

        private void ApplyDefaultActionMaps()
        {
            if (defaultActionMaps == null || defaultActionMaps.Length == 0)
                return;

            defaultActionMapSnapshots.Clear();

            foreach (PlayerInput player in PlayerInput.all)
            {
                if (player == null || player.actions == null)
                    continue;

                ActionMapSnapshot snapshot = new() { playerInput = player };
                defaultActionMapSnapshots.Add(snapshot);

                foreach (ActionMapConfig config in defaultActionMaps)
                {
                    if (string.IsNullOrEmpty(config.actionMapName))
                        continue;

                    InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                    if (map != null)
                        snapshot.state[config.actionMapName] = map.enabled;
                }

                ApplyActionMapsToPlayer(player, defaultActionMaps);
            }

            defaultsApplied = true;
        }

        private void RestoreDefaultActionMaps()
        {
            foreach (var snapshot in defaultActionMapSnapshots)
            {
                if (snapshot.playerInput == null || snapshot.playerInput.actions == null)
                    continue;

                RestoreSnapshotToPlayer(snapshot);
            }

            defaultActionMapSnapshots.Clear();
            defaultsApplied = false;
        }

        private void ApplyHandler(ViewInputSystemHandler handler)
        {
            if (handler == null)
                return;

            ActionMapConfig[] actionMaps = handler.GetOnEnabledActionMaps();

            if (actionMaps == null || actionMaps.Length == 0)
                return;

            List<PlayerInput> players = GetPlayers(handler);
            if (!handlerSnapshots.ContainsKey(handler))
                handlerSnapshots[handler] = new();

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

                ActionMapSnapshot snapshot = new() { playerInput = player };
                handlerSnapshots[handler].Add(snapshot);

                foreach (ActionMapConfig config in actionMaps)
                {
                    if (string.IsNullOrEmpty(config.actionMapName))
                        continue;

                    InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                    if (map != null)
                        snapshot.state[config.actionMapName] = map.enabled;
                }

                ApplyActionMapsToPlayer(player, actionMaps);
            }
        }

        private void RestoreHandler(ViewInputSystemHandler handler)
        {
            if (handlerSnapshots.TryGetValue(handler, out List<ActionMapSnapshot> snapshots))
            {
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.playerInput == null || snapshot.playerInput.actions == null)
                        continue;

                    RestoreSnapshotToPlayer(snapshot);
                }
            }

            ActionMapConfig[] onDisabledMaps = handler.GetOnDisabledActionMaps();
            if (onDisabledMaps == null || onDisabledMaps.Length == 0)
                return;

            List<PlayerInput> players = GetPlayers(handler);

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

                ApplyActionMapsToPlayer(player, onDisabledMaps);
            }
        }

        /// <summary>
        /// Applies action maps to a player. Uses SwitchCurrentActionMap when only one map is enabled,
        /// otherwise uses Enable/Disable for multiple maps.
        /// </summary>
        private void ApplyActionMapsToPlayer(PlayerInput player, ActionMapConfig[] configs)
        {
            if (player == null || player.actions == null || configs == null || configs.Length == 0)
                return;

            int enabledCount = configs.Count(c => c.enable && !string.IsNullOrEmpty(c.actionMapName));

            if (enabledCount == 1)
            {
                string mapName = configs.First(c => c.enable && !string.IsNullOrEmpty(c.actionMapName)).actionMapName;
                InputActionMap map = player.actions.FindActionMap(mapName);
                if (map != null)
                    player.SwitchCurrentActionMap(mapName);
            }
            else
            {
                string firstEnabled = null;
                foreach (ActionMapConfig config in configs)
                {
                    if (string.IsNullOrEmpty(config.actionMapName))
                        continue;

                    InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                    if (map == null)
                        continue;

                    if (config.enable && !map.enabled)
                    {
                        map.Enable();
                        firstEnabled ??= config.actionMapName;
                    }
                    else if (!config.enable && map.enabled)
                    {
                        map.Disable();
                    }
                }

                // Switch to first enabled map if any
                if (firstEnabled != null)
                    player.SwitchCurrentActionMap(firstEnabled);
            }
        }

        /// <summary>
        /// Restores action map state from a snapshot.
        /// </summary>
        private void RestoreSnapshotToPlayer(ActionMapSnapshot snapshot)
        {
            if (snapshot.playerInput == null || snapshot.playerInput.actions == null)
                return;

            string firstEnabled = null;

            foreach (var mapState in snapshot.state)
            {
                InputActionMap map = snapshot.playerInput.actions.FindActionMap(mapState.Key);
                if (map == null)
                    continue;

                if (mapState.Value && !map.enabled)
                {
                    map.Enable();
                    firstEnabled ??= mapState.Key;
                }
                else if (!mapState.Value && map.enabled)
                {
                    map.Disable();
                }
            }

            // Switch to first enabled map if any
            if (firstEnabled != null)
                snapshot.playerInput.SwitchCurrentActionMap(firstEnabled);
        }

        private List<PlayerInput> GetPlayers(ViewInputSystemHandler handler)
        {
            List<PlayerInput> players = new();
            if (handler.AppliesToAllPlayers())
            {
                players.AddRange(PlayerInput.all);
            }
            else if (handler.GetPlayerInput() != null)
            {
                players.Add(handler.GetPlayerInput());
            }

            return players;
        }
    }
}
#endif
