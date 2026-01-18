#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

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

        public class ActionMapHistoryEntry
        {
            public float timestamp;
            public int playerIndex;
            public string action; // "Enable", "Disable", "Restore"
            public List<string> mapNames = new();
            public string source; // Handler/View name
        }

        private readonly List<ActionMapSnapshot> baselineStates = new();
        private readonly Dictionary<ViewInputSystemHandler, List<ActionMapSnapshot>> handlerSnapshots = new();
        private readonly List<ActionMapHistoryEntry> history = new();
        private const int MAX_HISTORY_ENTRIES = 50;

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
        }

        private void OnDestroy()
        {
            SentinalManager.OnSwitch -= OnViewSwitch;
            SentinalManager.OnRemove -= OnViewRemoved;
        }

        private void OnViewSwitch(ViewSelector previousView, ViewSelector newView)
        {
            // Backward compatibility: if views have handlers on the same GameObject, apply/restore them
            if (previousView != null && previousView.TryGetComponent(out ViewInputSystemHandler prevHandler))
                RestoreHandler(prevHandler);

            if (newView != null && newView.TryGetComponent(out ViewInputSystemHandler newHandler))
                ApplyHandler(newHandler);
        }

        private void OnViewRemoved(ViewSelector view)
        {
            if (view == null)
                return;

            if (view.TryGetComponent(out ViewInputSystemHandler handler))
            {
                RestoreHandler(handler);
                handlerSnapshots.Remove(handler);
            }

            if (SentinalManager.Instance != null && SentinalManager.Instance.AnyViewsOpen)
            {
                ViewSelector currentView = SentinalManager.Instance.CurrentView;
                if (currentView != null && currentView.TryGetComponent(out ViewInputSystemHandler currentHandler))
                    ApplyHandler(currentHandler);
            }
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

            string[] enableMaps = handler.GetEnableActionMaps();
            string[] disableMaps = handler.GetDisableActionMaps();

            if ((enableMaps == null || enableMaps.Length == 0) && (disableMaps == null || disableMaps.Length == 0))
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

                if (enableMaps != null)
                {
                    foreach (string mapName in enableMaps)
                    {
                        InputActionMap map = player.actions.FindActionMap(mapName);
                        if (map == null)
                            continue;

                        snapshot.state[mapName] = map.enabled;
                        if (!map.enabled)
                        {
                            map.Enable();
                            firstEnabled ??= mapName;
                            enabledMaps.Add(mapName);
                        }
                    }
                }

                if (disableMaps != null)
                {
                    foreach (string mapName in disableMaps)
                    {
                        InputActionMap map = player.actions.FindActionMap(mapName);
                        if (map == null)
                            continue;

                        snapshot.state[mapName] = map.enabled;
                        if (map.enabled)
                        {
                            map.Disable();
                            disabledMaps.Add(mapName);
                        }
                    }
                }

                // Record history
                if (enabledMaps.Count > 0)
                {
                    AddHistoryEntry(player.playerIndex, "Enable", enabledMaps, sourceName);
                }
                if (disabledMaps.Count > 0)
                {
                    AddHistoryEntry(player.playerIndex, "Disable", disabledMaps, sourceName);
                }

                if (firstEnabled != null)
                    player.SwitchCurrentActionMap(firstEnabled);
            }
        }

        private void RestoreHandler(ViewInputSystemHandler handler)
        {
            if (!handlerSnapshots.TryGetValue(handler, out var snapshots))
                return;

            string sourceName = handler.gameObject.name;
            if (handler.ViewSelector != null)
                sourceName = handler.ViewSelector.name;

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
                    AddHistoryEntry(snapshot.playerInput.playerIndex, "Restore", restoredMaps, sourceName);
                }
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

        private void AddHistoryEntry(int playerIndex, string action, List<string> mapNames, string source)
        {
            var entry = new ActionMapHistoryEntry
            {
                timestamp = Time.time,
                playerIndex = playerIndex,
                action = action,
                mapNames = new List<string>(mapNames),
                source = source
            };

            history.Add(entry);

            // Limit history size
            if (history.Count > MAX_HISTORY_ENTRIES)
            {
                history.RemoveAt(0);
            }
        }

        /// <summary>
        /// Gets the action map change history.
        /// </summary>
        public List<ActionMapHistoryEntry> GetHistory()
        {
            return new List<ActionMapHistoryEntry>(history);
        }

        /// <summary>
        /// Clears the action map change history.
        /// </summary>
        public void ClearHistory()
        {
            history.Clear();
        }
    }
}
#endif
