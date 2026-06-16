#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Tracks named player roles as integer keys.
    /// Key 0 is conventionally the primary player.
    /// </summary>
    public static class SentinalPlayer
    {
        public const int PrimaryKey = 0;
        public static event Action<int, PlayerInput> OnPlayerChanged;

        private static readonly Dictionary<int, PlayerInput> players = new();

        public static PlayerInput PrimaryPlayer => GetPlayer(PrimaryKey);

        public static PlayerInput GetPlayerByIndex(int playerIndex) =>
            playerIndex >= 0 && playerIndex < PlayerInput.all.Count ? PlayerInput.all[playerIndex] : null;

        public static void SetPlayer(int key, PlayerInput player)
        {
            if (players.TryGetValue(key, out var existing) && existing == player)
                return;

            players[key] = player;
            OnPlayerChanged?.Invoke(key, player);
        }

        public static PlayerInput GetPlayer(int key = PrimaryKey)
        {
            if (players.TryGetValue(key, out var p) && p != null)
                return p;

            if (key != PrimaryKey && players.TryGetValue(PrimaryKey, out p) && p != null)
                return p;

            return PlayerInput.all.Count > 0 ? PlayerInput.all[0] : null;
        }

        public static void SetPrimaryPlayer(PlayerInput player) => SetPlayer(PrimaryKey, player);

        public static bool TryGetPlayer(int key, out PlayerInput player) =>
            players.TryGetValue(key, out player) && player != null;

        public static void RemovePlayer(int key)
        {
            if (players.Remove(key))
                OnPlayerChanged?.Invoke(key, null);
        }

        public static IReadOnlyDictionary<int, PlayerInput> GetAllPlayers() => players;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            players.Clear();
            OnPlayerChanged = null;
        }
    }
}
#endif
