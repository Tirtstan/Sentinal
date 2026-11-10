#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// The sole writer of retained players' maps and the scene UI module's actions. Recomputes from live
    /// layers and scene policy; never restores a snapshot. View changes batch until the pre-input pass.
    /// </summary>
    public static class PlayerInputMaps
    {
        public static event Action<PlayerInput, InputActionMap, InputActionMap> BeforeApply;
        private static readonly HashSet<PlayerInput> players = new();
        private static readonly HashSet<string> reported = new();
        private static readonly Dictionary<PlayerInput, SentinalView> sources = new();
        private static InputSystemUIInputModule currentModule;
        private static bool dirty;
        private static bool applying;

        public static IReadOnlyCollection<PlayerInput> Players => players;

        public static void Register(PlayerInput player)
        {
            if (player != null && players.Add(player))
                dirty = true;
        }

        public static void Unregister(PlayerInput player)
        {
            foreach (SentinalView view in SentinalViewRouter.GetOpenViews())
                if (view.IsTopLevel && view.IsPlayerView && view.Owner == player)
                    view.Close();
            players.Remove(player);
            sources.Remove(player);
            dirty = true;
        }

        public static SentinalView GetMapSource(PlayerInput player) =>
            player != null && sources.TryGetValue(player, out SentinalView source) ? source : null;

        public static bool IsAlwaysOn(InputActionMap map)
        {
            SentinalSettings settings = SentinalSettings.Instance;
            return map != null
                && settings != null
                && settings.AlwaysOnActionMaps.Any(reference => reference.Resolve(map.asset) == map);
        }

        internal static bool IsAuthorityActionEligible(PlayerInput player, InputAction action) =>
            !SentinalViewRouter.HasPlayerView(player)
            || SentinalViewRouter.GlobalUIOpen()
            || IsAlwaysOn(action.actionMap);

        private static void MarkDirty() => dirty = true;

        private static void SceneChanged(Scene previous, Scene current) => MarkDirty();

        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => MarkDirty();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= Reconcile;
            SentinalViewRouter.Changed -= MarkDirty;
            UIAuthority.CurrentChanged -= AuthorityChanged;
            SceneManager.activeSceneChanged -= SceneChanged;
            SceneManager.sceneLoaded -= SceneLoaded;
            players.Clear();
            sources.Clear();
            reported.Clear();
            currentModule = null;
            BeforeApply = null;
            applying = false;
            dirty = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += Reconcile;
            SentinalViewRouter.Changed += MarkDirty;
            UIAuthority.CurrentChanged += AuthorityChanged;
            SceneManager.activeSceneChanged += SceneChanged;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private static void AuthorityChanged(PlayerInput player) => MarkDirty();

        /// <summary>Applies one complete state before processing the next input update. Also used by runtime checks.</summary>
        public static void Reconcile()
        {
            if (applying || !Application.isPlaying)
                return;
            SentinalSettings settings = SentinalSettings.Instance;
            if (settings == null || players.Count == 0)
                return;

            applying = true;
            try
            {
                foreach (PlayerInput player in players.ToArray())
                    if (player == null || !player.isActiveAndEnabled)
                        Unregister(player);

                foreach (SentinalView view in SentinalViewRouter.GetOpenViews())
                    view.RefreshInputState();

                InputSystemUIInputModule module =
                    EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
                PlayerInput authority = UIAuthority.Current;
                bool moduleChanged =
                    module != currentModule
                    || (module != null && authority != null && module.actionsAsset != authority.actions);
                currentModule = module;

                string scene = SceneManager.GetActiveScene().path;
                if (!settings.TryGetBaseMap(scene, out ActionMapRef baseMap))
                {
                    Report(
                        $"scene:{scene}",
                        $"Expected a scene base-map policy for active build scene '{scene}'; actual: no matching entry in '{SentinalSettings.DefaultAssetPath}'."
                    );
                    return;
                }

                // Notify every affected player before module assignment or any map disabling loses held state.
                var desired = new Dictionary<PlayerInput, InputActionMap>();
                foreach (PlayerInput player in players)
                {
                    SentinalView source = FindMapSource(player);
                    sources[player] = source;
                    InputActionMap map = (source != null ? settings.UIActionMap : baseMap).Resolve(player.actions);
                    if (map == null)
                    {
                        Report(
                            $"map:{player.GetInstanceID()}:{scene}",
                            $"PlayerInput '{player.name}' expected map GUID '{(source != null ? settings.UIActionMap : baseMap).Id}' in its action asset; actual: '{player.actions}' has no matching map.",
                            player
                        );
                        continue;
                    }
                    desired.Add(player, map);
                    if (dirty || moduleChanged || player.currentActionMap != map)
                        BeforeApply?.Invoke(player, player.currentActionMap, map);
                }

                if (module != null && authority != null && module.actionsAsset != authority.actions)
                    module.actionsAsset = authority.actions;

                foreach (KeyValuePair<PlayerInput, InputActionMap> entry in desired)
                {
                    PlayerInput player = entry.Key;
                    InputActionMap map = entry.Value;
                    if (player.currentActionMap != map)
                        player.SwitchCurrentActionMap(map.id.ToString());

                    SentinalView focused = SentinalViewRouter.GlobalUIOpen()
                        ? SentinalViewRouter.CurrentView
                        : SentinalViewRouter.GetPlayerView(player);
                    if (focused == null && player == authority)
                        focused = SentinalViewRouter.CurrentView;
                    foreach (ActionMapRef reference in settings.AlwaysOnActionMaps)
                        if (reference.Resolve(player.actions) == null)
                            Report(
                                $"always:{player.GetInstanceID()}:{reference.Id}",
                                $"PlayerInput '{player.name}' expected always-on map GUID '{reference.Id}'; actual: no matching map in '{player.actions}'.",
                                player
                            );

                    // Assigning a UI module can enable individual actions on an otherwise disabled map.
                    // Reconcile actual actions every pass, including when the current map did not change.
                    foreach (InputActionMap candidate in player.actions.actionMaps)
                    {
                        bool allowed = candidate == map || IsAlwaysOn(candidate);
                        foreach (InputAction action in candidate.actions)
                        {
                            bool enabled = allowed;
                            if (enabled && candidate == settings.UIActionMap.Resolve(player.actions) && focused != null)
                                enabled = !SentinalView.NeedsFreshPress(action)
                                    ? focused.IsInputReady
                                    : focused.CanProcessInput(action);
                            if (enabled && !action.enabled)
                                action.Enable();
                            else if (!enabled && action.enabled)
                                action.Disable();
                        }
                    }
                }
                dirty = false;
                if (moduleChanged)
                    SentinalViewRouter.TrySelectCurrentView();
            }
            finally
            {
                applying = false;
            }
        }

        private static SentinalView FindMapSource(PlayerInput player)
        {
            SentinalView bestGlobal = null;
            SentinalView bestPlayer = null;
            int globalOrder = -1;
            int playerOrder = -1;
            foreach (SentinalView view in SentinalViewRouter.GetOpenViews())
            {
                if (
                    !view.IsTopLevel
                    || !view.IsShown
                    || !view.IsActive
                    || !SentinalViewRouter.TryGetLayer(view, out ViewLayer layer, out int order)
                )
                    continue;
                if (!view.IsPlayerView && layer.InputWhileOpen == LayerInput.EveryoneToUI && order >= globalOrder)
                {
                    bestGlobal = view;
                    globalOrder = order;
                }
                else if (
                    view.IsPlayerView
                    && view.Owner == player
                    && layer.InputWhileOpen == LayerInput.OwnerToUI
                    && order >= playerOrder
                )
                {
                    bestPlayer = view;
                    playerOrder = order;
                }
            }
            return bestGlobal != null ? bestGlobal : bestPlayer;
        }

        private static void Report(string key, string message, UnityEngine.Object context = null)
        {
            if (reported.Add(key))
                Debug.LogError($"[Sentinal] {message}", context);
        }
    }
}
#endif
