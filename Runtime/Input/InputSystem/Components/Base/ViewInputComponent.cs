#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Sentinal.InputSystem.Components
{
    /// <summary>When an input component inside a view listens to its player.</summary>
    public enum ActiveWhen
    {
        [Tooltip(
            "While focus is on the view or one of its tabs. A focused popup inside it, or another window, stops it."
        )]
        ViewFocused,

        [Tooltip("While the view is open and on screen, even when something else has focus.")]
        ViewOpen,
    }

    /// <summary>
    /// Base for components that react to their player's input actions. The player follows
    /// <see cref="ViewPlayerComponent"/>; listening follows <b>Active when</b> on the nearest view. Outside any view the
    /// component listens whenever it is enabled. A press that started before the component started listening (which
    /// happens whenever its view regains input, its player changes or authority is handed off) never counts, including
    /// its release and any hold it completes: the button must be pressed again.
    /// </summary>
    public abstract class ViewInputComponent : ViewPlayerComponent
    {
        [Header("Input")]
        [SerializeField]
        [Tooltip("When this component listens to its player's input, relative to its nearest parent view.")]
        private ActiveWhen activeWhen = ActiveWhen.ViewFocused;

        private double listeningSince;
        private PlayerInput activePlayer;
        private bool isInputActive;

        public ActiveWhen ActiveWhen
        {
            get => activeWhen;
            set
            {
                if (activeWhen == value)
                    return;

                activeWhen = value;
                if (isActiveAndEnabled)
                    EvaluateInput();
            }
        }

        /// <summary>True while subscribed to the player's actions.</summary>
        public bool IsInputActive => isInputActive;

        protected override void OnDisable()
        {
            Deactivate();
            base.OnDisable();
        }

        protected override void OnRoutingStateChanged() => EvaluateInput();

        /// <summary>Subscribes to the player's actions. Called with a non-null player whose action asset is set.</summary>
        protected abstract void Subscribe(PlayerInput player);

        /// <summary>Removes every subscription made by <see cref="Subscribe"/> and resets any press in progress.</summary>
        protected abstract void Unsubscribe();

        /// <summary>
        /// True when an action callback may act now: the component is listening, the press started after it began
        /// listening, and (for View focused) the view accepts input (layer input delay, fresh-press rule).
        /// </summary>
        protected bool CanProcessInput(InputAction.CallbackContext context)
        {
            if (!isInputActive || context.action == null)
                return false;

            // Always ask the view, even for a carried press: its release is what rearms the view's own rule.
            SentinalView view = ParentView;
            bool viewAccepts =
                view == null || activeWhen == ActiveWhen.ViewOpen || view.CanProcessInput(context.action);
            return viewAccepts && context.startTime >= listeningSince;
        }

        /// <summary>Re-evaluates listening, e.g. after a subclass's own preconditions changed.</summary>
        protected void EvaluateInput()
        {
            PlayerInput player = Player;
            bool shouldListen =
                isActiveAndEnabled && player != null && player.actions != null && IsViewListening(ParentView);

            if (isInputActive && (!shouldListen || activePlayer != player))
                Deactivate();

            if (shouldListen && !isInputActive)
                Activate(player);
        }

        private bool IsViewListening(SentinalView view)
        {
            if (view == null)
                return true;

            if (!view.IsShown || !view.IsActive || SentinalViewRouter.IsSuspended(view))
                return false;

            return activeWhen == ActiveWhen.ViewOpen || SentinalViewRouter.IsFocusWithin(view);
        }

        private void Activate(PlayerInput player)
        {
            listeningSince = InputState.currentTime;
            activePlayer = player;
            isInputActive = true;
            Subscribe(player);
        }

        private void Deactivate()
        {
            if (!isInputActive)
                return;

            isInputActive = false;
            activePlayer = null;
            Unsubscribe();
        }
    }
}
#endif
