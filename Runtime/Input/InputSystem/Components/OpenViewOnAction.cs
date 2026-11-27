#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    /// <summary>What pressing the action does while its view is already focused.</summary>
    public enum PressingAgain
    {
        [Tooltip("Pressing the action again keeps the view open.")]
        DoesNothing,

        [Tooltip("Pressing the action again closes the view (and anything open inside it).")]
        ClosesIt,
    }

    /// <summary>
    /// Opens a view when its player presses one of its actions. List the same shortcut once per action map it lives in
    /// (e.g. Player/OtherMenu to open from gameplay and UI/OtherMenu to close from the menu). It only opens the view if the view's layer is at or above the
    /// focused window's layer and no Back press is pending, so one press never both closes one menu and opens another.
    /// One opener per scene may also open its view when the always-on Back has nothing to go back from
    /// (Escape: back out of menus, otherwise open Pause).
    /// </summary>
    [AddComponentMenu("Sentinal/Open View On Action")]
    public sealed class OpenViewOnAction : ViewInputComponent
    {
        [Header("Action")]
        [SerializeField]
        [Tooltip(
            "Any of these opens the view. List the same shortcut once per action map it lives in, since a view that moves its player to UI turns the gameplay map off. Each is resolved on the player's own action asset; one the asset lacks (e.g. another input layout) is simply off."
        )]
        private InputActionReference[] actions;

        [Header("View")]
        [SerializeField]
        [Tooltip("The view to open.")]
        private SentinalView view;

        [SerializeField]
        [Tooltip("What pressing the action does while the view is focused.")]
        private PressingAgain pressingAgain = PressingAgain.DoesNothing;

        [SerializeField]
        [Tooltip(
            "Doesn't open while another Global view that moves everyone to UI is open (e.g. Pause doesn't open over a menu screen)."
        )]
        private bool onlyWhenNoMenuOpen;

        [SerializeField]
        [Tooltip(
            "Also opens the view when the always-on Back (e.g. Escape) has nothing to go back from. One opener per scene."
        )]
        private bool opensOnUnhandledBack;

        private static OpenViewOnAction unhandledBackOpener;
        private readonly List<InputAction> boundActions = new();
        private bool configured;

        public SentinalView View => view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => unhandledBackOpener = null;

        /// <summary>Opens the registered unhandled-Back view for <paramref name="player"/>. Returns true when it opened.</summary>
        internal static bool TryOpenOnUnhandledBack(PlayerInput player) =>
            unhandledBackOpener != null && unhandledBackOpener.Player == player && unhandledBackOpener.TryOpen();

        private void Awake()
        {
            int missingActions = CountMissingActions();
            configured = actions != null && actions.Length > 0 && missingActions == 0 && view != null;
            if (configured)
                return;

            Debug.LogError(
                $"[Sentinal] '{name}' {nameof(OpenViewOnAction)} expected at least one Action, none missing, and a View; actual: Actions={(actions != null ? actions.Length : 0)} ({missingActions} missing), View={(view != null ? view.name : "none")}.",
                this
            );
            enabled = false;
        }

        protected override void Subscribe(PlayerInput player)
        {
            if (!configured)
                return;

            foreach (InputActionReference reference in actions)
            {
                InputAction bound = player.actions.FindAction(reference.action.id);
                if (bound == null || boundActions.Contains(bound))
                    continue;

                bound.performed += OnPerformed;
                boundActions.Add(bound);
            }

            if (opensOnUnhandledBack)
                RegisterUnhandledBackOpener();
        }

        protected override void Unsubscribe()
        {
            foreach (InputAction bound in boundActions)
                bound.performed -= OnPerformed;

            boundActions.Clear();
            if (unhandledBackOpener == this)
                unhandledBackOpener = null;
        }

        private int CountMissingActions()
        {
            if (actions == null)
                return 0;

            int missing = 0;
            foreach (InputActionReference reference in actions)
            {
                if (reference == null || reference.action == null)
                    missing++;
            }

            return missing;
        }

        private void RegisterUnhandledBackOpener()
        {
            if (unhandledBackOpener != null && unhandledBackOpener != this)
            {
                Debug.LogError(
                    $"[Sentinal] '{name}' expected to be the only listening {nameof(OpenViewOnAction)} with Opens On Unhandled Back; actual: '{unhandledBackOpener.name}' already is. '{name}' won't open on Back.",
                    this
                );
                return;
            }

            unhandledBackOpener = this;
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            if (!CanProcessInput(context))
                return;

            if (IsBackPending())
                return;

            // An open view only reacts while focused: a popup or menu above it must be closed first.
            if (view.IsOpen)
            {
                if (pressingAgain == PressingAgain.ClosesIt && SentinalViewRouter.IsFocusWithin(view))
                    view.Close();
                return;
            }

            TryOpen();
        }

        private bool TryOpen()
        {
            if (view.IsOpen || !SentinalViewRouter.WouldTakeFocus(view))
                return false;

            if (onlyWhenNoMenuOpen && SentinalViewRouter.GlobalUIOpen())
                return false;

            return view.IsPlayerView ? view.Open(Player) : view.Open();
        }

        private bool IsBackPending() =>
            SentinalViewRouter.IsBackPendingFor(Player)
            || (Player == SentinalViewRouter.AuthorityPlayer && SentinalViewRouter.IsBackPending);
    }
}
#endif
