#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Represents an action taken on an action map.
    /// </summary>
    public enum ActionMapAction
    {
        Enable,
        Disable,
        Restore,
    }

    public class ActionMapManager : MonoBehaviour
    {
        public static ActionMapManager Instance { get; private set; }

        private class ActionMapSnapshot
        {
            public PlayerInput playerInput;
            public Dictionary<string, bool> state = new();
        }

        public class ActionMapHistoryEntry
        {
            public float timestamp;
            public int playerIndex;
            public ActionMapAction action;
            public List<string> mapNames = new();
            public string source; // Handler/View name
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

        private readonly List<ActionMapSnapshot> baselineStates = new();
        private readonly Dictionary<ViewInputSystemHandler, List<ActionMapSnapshot>> handlerSnapshots = new();
        private readonly Dictionary<string, ActionMapHistoryEntry> historyBySource = new();
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
            SubscribeToViewEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            CheckAndApplyDefaults();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromViewEvents();
        }

        private void SubscribeToViewEvents()
        {
            SentinalManager.OnSwitch += OnViewSwitch;
            SentinalManager.OnRemove += OnViewRemoved;
            SentinalManager.OnAdd += OnViewAdded;
        }

        private void UnsubscribeFromViewEvents()
        {
            SentinalManager.OnSwitch -= OnViewSwitch;
            SentinalManager.OnRemove -= OnViewRemoved;
            SentinalManager.OnAdd -= OnViewAdded;
        }

        /// <summary>
        /// Re-subscribes to SentinalManager view events. Called after scene load so this
        /// (possibly DontDestroyOnLoad) instance is still listening when the new scene's
        /// SentinalManager fires events, even if the previous manager nulled static events on destroy.
        /// </summary>
        private void ResubscribeToViewEvents()
        {
            UnsubscribeFromViewEvents();
            SubscribeToViewEvents();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearStateForNewScene();
            ResubscribeToViewEvents();
        }

        /// <summary>
        /// Clears cached baselines, handler snapshots, and default snapshots so the manager
        /// does not hold references to destroyed PlayerInput/ViewInputSystemHandlers after a scene load.
        /// Call this (or rely on scene load) when using DontDestroyOnLoad so new scenes get a clean state.
        /// </summary>
        public void ClearStateForNewScene()
        {
            baselineStates.Clear();
            handlerSnapshots.Clear();
            handlerCache.Clear();
            historyBySource.Clear();
            defaultActionMapSnapshots.Clear();
            defaultsApplied = false;

            CheckAndApplyDefaults();
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            // Only modify action maps when the target view explicitly has a handler.
            if (newView != null && TryGetCachedHandler(newView, out ViewInputSystemHandler newHandler))
            {
                if (previousView != null && TryGetCachedHandler(previousView, out ViewInputSystemHandler prevHandler))
                    RestoreHandler(prevHandler);

                ApplyHandler(newHandler);
            }
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

            bool found = view.TryGetComponent(out handler);
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

            // Only consider non-root views that actually have a ViewInputSystemHandler.
            // Views without input management should not influence default action maps.
            bool shouldApplyDefaults = !AnyNonRootViewsWithInputHandlersOpen();

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

            List<string> enabledMaps = new();
            List<string> disabledMaps = new();

            foreach (PlayerInput player in PlayerInput.all)
            {
                if (player == null || player.actions == null)
                    continue;

                ActionMapSnapshot snapshot = new() { playerInput = player };
                defaultActionMapSnapshots.Add(snapshot);

                string firstEnabled = null;

                foreach (ActionMapConfig config in defaultActionMaps)
                {
                    if (string.IsNullOrEmpty(config.actionMapName))
                        continue;

                    InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                    if (map == null)
                        continue;

                    snapshot.state[config.actionMapName] = map.enabled;

                    if (config.enable && !map.enabled)
                    {
                        map.Enable();
                        firstEnabled ??= config.actionMapName;
                        enabledMaps.Add(config.actionMapName);
                    }
                    else if (!config.enable && map.enabled)
                    {
                        map.Disable();
                        disabledMaps.Add(config.actionMapName);
                    }
                }

                if (enabledMaps.Count > 0)
                    AddHistoryEntry(player.playerIndex, ActionMapAction.Enable, enabledMaps, "DefaultActionMaps");
                if (disabledMaps.Count > 0)
                    AddHistoryEntry(player.playerIndex, ActionMapAction.Disable, disabledMaps, "DefaultActionMaps");

                enabledMaps.Clear();
                disabledMaps.Clear();

                if (firstEnabled != null)
                    player.SwitchCurrentActionMap(firstEnabled);
            }

            defaultsApplied = true;
        }

        private void RestoreDefaultActionMaps()
        {
            foreach (var snapshot in defaultActionMapSnapshots)
            {
                if (snapshot.playerInput == null || snapshot.playerInput.actions == null)
                    continue;

                List<string> restoredMaps = new();

                foreach (var mapState in snapshot.state)
                {
                    InputActionMap map = snapshot.playerInput.actions.FindActionMap(mapState.Key);
                    if (map == null)
                        continue;

                    if (mapState.Value && !map.enabled)
                    {
                        map.Enable();
                        restoredMaps.Add(mapState.Key);
                    }
                    else if (!mapState.Value && map.enabled)
                    {
                        map.Disable();
                        restoredMaps.Add(mapState.Key);
                    }
                }

                if (restoredMaps.Count > 0)
                {
                    AddHistoryEntry(
                        snapshot.playerInput.playerIndex,
                        ActionMapAction.Restore,
                        restoredMaps,
                        "DefaultActionMaps"
                    );
                }
            }

            defaultActionMapSnapshots.Clear();
            defaultsApplied = false;
        }

        /// <summary>
        /// Applies action maps for a handler. Call this when the handler becomes active.
        /// </summary>
        public void ApplyActionMaps(ViewInputSystemHandler handler)
        {
            if (handler == null)
                return;

            ApplyHandler(handler);
        }

        /// <summary>
        /// Restores action maps for a handler. Call this when the handler becomes inactive.
        /// </summary>
        public void RestoreActionMaps(ViewInputSystemHandler handler)
        {
            if (handler == null)
                return;

            RestoreHandler(handler);
            handlerSnapshots.Remove(handler);
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

            string sourceName = handler.gameObject.name;
            if (handler.ViewSelector != null)
                sourceName = handler.ViewSelector.name;

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

                if (baselineStates.Find(s => s.playerInput == player) == null)
                    CaptureBaseline(player);

                ActionMapSnapshot snapshot = new() { playerInput = player };
                handlerSnapshots[handler].Add(snapshot);

                string firstEnabled = null;
                List<string> enabledMaps = new();
                List<string> disabledMaps = new();

                foreach (ActionMapConfig config in actionMaps)
                {
                    if (string.IsNullOrEmpty(config.actionMapName))
                        continue;

                    InputActionMap map = player.actions.FindActionMap(config.actionMapName);
                    if (map == null)
                        continue;

                    snapshot.state[config.actionMapName] = map.enabled;

                    if (config.enable && !map.enabled)
                    {
                        map.Enable();
                        firstEnabled ??= config.actionMapName;
                        enabledMaps.Add(config.actionMapName);
                    }
                    else if (!config.enable && map.enabled)
                    {
                        map.Disable();
                        disabledMaps.Add(config.actionMapName);
                    }
                }

                if (enabledMaps.Count > 0)
                    AddHistoryEntry(player.playerIndex, ActionMapAction.Enable, enabledMaps, sourceName);
                if (disabledMaps.Count > 0)
                    AddHistoryEntry(player.playerIndex, ActionMapAction.Disable, disabledMaps, sourceName);

                if (firstEnabled != null)
                    player.SwitchCurrentActionMap(firstEnabled);
            }
        }

        private void RestoreHandler(ViewInputSystemHandler handler)
        {
            string sourceName = handler.gameObject.name;
            if (handler.ViewSelector != null)
                sourceName = handler.ViewSelector.name;

            // First, restore from snapshot if we have one
            if (handlerSnapshots.TryGetValue(handler, out List<ActionMapSnapshot> snapshots))
            {
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.playerInput == null || snapshot.playerInput.actions == null)
                        continue;

                    List<string> restoredMaps = new();

                    foreach (var mapState in snapshot.state)
                    {
                        InputActionMap map = snapshot.playerInput.actions.FindActionMap(mapState.Key);
                        if (map == null)
                            continue;

                        if (mapState.Value && !map.enabled)
                        {
                            map.Enable();
                            restoredMaps.Add(mapState.Key);
                        }
                        else if (!mapState.Value && map.enabled)
                        {
                            map.Disable();
                            restoredMaps.Add(mapState.Key);
                        }
                    }

                    if (restoredMaps.Count > 0)
                    {
                        AddHistoryEntry(
                            snapshot.playerInput.playerIndex,
                            ActionMapAction.Restore,
                            restoredMaps,
                            sourceName
                        );
                    }
                }
            }

            // Then apply onDisabledActionMaps if configured
            ActionMapConfig[] onDisabledMaps = handler.GetOnDisabledActionMaps();
            if (onDisabledMaps == null || onDisabledMaps.Length == 0)
                return;

            List<PlayerInput> players = GetPlayers(handler);

            foreach (PlayerInput player in players)
            {
                if (player == null || player.actions == null)
                    continue;

                List<string> enabledMaps = new();
                List<string> disabledMaps = new();
                string firstEnabled = null;

                foreach (ActionMapConfig config in onDisabledMaps)
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
                        enabledMaps.Add(config.actionMapName);
                    }
                    else if (!config.enable && map.enabled)
                    {
                        map.Disable();
                        disabledMaps.Add(config.actionMapName);
                    }
                }

                if (enabledMaps.Count > 0)
                {
                    AddHistoryEntry(
                        player.playerIndex,
                        ActionMapAction.Enable,
                        enabledMaps,
                        sourceName + " (OnDisabled)"
                    );
                }
                if (disabledMaps.Count > 0)
                {
                    AddHistoryEntry(
                        player.playerIndex,
                        ActionMapAction.Disable,
                        disabledMaps,
                        sourceName + " (OnDisabled)"
                    );
                }

                if (firstEnabled != null)
                    player.SwitchCurrentActionMap(firstEnabled);
            }
        }

        private void CaptureBaseline(PlayerInput player)
        {
            if (player == null || player.actions == null)
                return;

            ActionMapSnapshot snapshot = new() { playerInput = player };
            baselineStates.Add(snapshot);
            foreach (InputActionMap map in player.actions.actionMaps)
                snapshot.state[map.name] = map.enabled;
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

        public void RestoreBaseline(PlayerInput player)
        {
            if (player == null || player.actions == null)
                return;

            ActionMapSnapshot baseline = baselineStates.Find(s => s.playerInput == player);
            if (baseline == null)
                return;

            string firstEnabled = null;
            foreach (var mapState in baseline.state)
            {
                InputActionMap map = player.actions.FindActionMap(mapState.Key);
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

            if (firstEnabled != null)
                player.SwitchCurrentActionMap(firstEnabled);
        }

        private void AddHistoryEntry(int playerIndex, ActionMapAction action, List<string> mapNames, string source)
        {
            var entry = new ActionMapHistoryEntry
            {
                timestamp = Time.time,
                playerIndex = playerIndex,
                action = action,
                mapNames = new List<string>(mapNames),
                source = source,
            };

            historyBySource[source] = entry;
        }

        /// <summary>
        /// Gets the latest action map state per view selector (source). Same selector overwrites the previous entry.
        /// </summary>
        public List<ActionMapHistoryEntry> GetHistory()
        {
            return new List<ActionMapHistoryEntry>(historyBySource.Values);
        }

        /// <summary>
        /// Clears the action map state history.
        /// </summary>
        public void ClearHistory()
        {
            historyBySource.Clear();
        }
    }
}
#endif
