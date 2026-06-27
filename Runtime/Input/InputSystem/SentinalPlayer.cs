#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Tracks named player roles as integer keys.
    /// Key 0 is the primary player.
    /// </summary>
    public static class SentinalPlayer
    {
        public const int PrimaryKey = 0;

        /// <summary>
        /// Event raised when a player is changed.
        /// </summary>
        public static event Action<int, PlayerInput> OnPlayerChanged;

        /// <summary>
        /// Dictionary of players by key.
        /// </summary>
        private static readonly Dictionary<int, PlayerInput> players = new();

        /// <summary>
        /// Gets the primary player. Primary key is 0.
        /// </summary>
        public static PlayerInput PrimaryPlayer => GetPlayer(PrimaryKey);

        public static PlayerInput GetPlayerByIndex(int playerIndex) =>
            playerIndex >= 0 && playerIndex < PlayerInput.all.Count ? PlayerInput.all[playerIndex] : null;

        /// <summary>
        /// Sets the player for a given key. If the player is already set, it will be replaced.
        /// Note: key 0 is the primary player.
        /// </summary>
        /// <param name="key">The key to set the player for. Key 0 is the primary player.</param>
        /// <param name="player">The player to set.</param>
        public static void SetPlayer(int key, PlayerInput player)
        {
            if (players.TryGetValue(key, out var existing) && existing == player)
                return;

            players[key] = player;
            OnPlayerChanged?.Invoke(key, player);
        }

        /// <summary>
        /// Gets the player for a given key. If the player is not set, it will return the primary player (key 0).
        /// </summary>
        /// <param name="key">The key to get the player for. Key 0 is the primary player.</param>
        /// <returns>The player for the given key.</returns>
        public static PlayerInput GetPlayer(int key = PrimaryKey)
        {
            if (players.TryGetValue(key, out var p) && p != null)
                return p;

            if (key != PrimaryKey && players.TryGetValue(PrimaryKey, out p) && p != null)
                return p;

            return PlayerInput.all.Count > 0 ? PlayerInput.all[0] : null;
        }

        /// <summary>
        /// Sets the primary player.
        /// Note: key 0 is the primary player.
        /// </summary>
        /// <param name="player">The player to set as the primary player.</param>
        public static void SetPrimaryPlayer(PlayerInput player) => SetPlayer(PrimaryKey, player);

        public static bool TryGetPlayer(int key, out PlayerInput player) =>
            players.TryGetValue(key, out player) && player != null;

        /// <summary>
        /// Removes the player for a given key.
        /// </summary>
        /// <param name="key">The key to remove the player for. Key 0 is the primary player.</param>
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
