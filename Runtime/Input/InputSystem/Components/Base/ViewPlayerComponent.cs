#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Base for components that act for one player. The player comes from the nearest <see cref="SentinalView"/> on
    /// or above this object (its <see cref="SentinalView.InputPlayer"/>: the owner of a player view, the UI authority
    /// player for a global view). Outside any view it is the UI authority player
    /// (<see cref="SentinalViewRouter.AuthorityPlayer"/>, published by <see cref="UIAuthority"/>). A player set with
    /// <see cref="SetPlayer"/> (e.g. by a spawner) is kept and never follows the view or authority again.
    /// The parent view is cached and refreshed when the transform's parent changes; player changes arrive as events.
    /// </summary>
    public abstract class ViewPlayerComponent : MonoBehaviour
    {
        private SentinalView parentView;
        private bool parentViewResolved;
        private SentinalView subscribedView;
        private PlayerInput player;
        private PlayerInput explicitPlayer;
        private bool hasExplicitPlayer;

        /// <summary>The nearest view on or above this object, or null when it sits outside every view.</summary>
        public SentinalView ParentView
        {
            get
            {
                if (!parentViewResolved)
                {
                    parentView = GetComponentInParent<SentinalView>(true);
                    parentViewResolved = true;
                }

                return parentView;
            }
        }

        /// <summary>The player this component currently acts for. Null until one is available.</summary>
        public PlayerInput Player => player;

        /// <summary>True once <see cref="SetPlayer"/> has pinned the player.</summary>
        public bool HasExplicitPlayer => hasExplicitPlayer;

        /// <summary>
        /// Pins the player (null pins "no player"). From then on the view and the UI authority no longer change it.
        /// Used by spawners that show one specific player's input.
        /// </summary>
        public void SetPlayer(PlayerInput explicitPlayerInput)
        {
            hasExplicitPlayer = true;
            explicitPlayer = explicitPlayerInput;
            RefreshPlayer();
        }

        protected virtual void OnEnable()
        {
            SentinalViewRouter.Changed += OnRoutingChanged;
            BindParentView();
            RefreshPlayer();
        }

        protected virtual void OnDisable()
        {
            SentinalViewRouter.Changed -= OnRoutingChanged;
            UnbindParentView();
        }

        protected virtual void OnTransformParentChanged()
        {
            parentViewResolved = false;
            if (!isActiveAndEnabled)
                return;

            BindParentView();
            RefreshPlayer();
        }

        /// <summary>Called whenever <see cref="Player"/> changes, including to or from null.</summary>
        protected virtual void OnPlayerChanged(PlayerInput previous, PlayerInput current) { }

        /// <summary>Called after any routing change (focus, open/close, suspension, authority hand-off) while enabled.</summary>
        protected virtual void OnRoutingStateChanged() { }

        private PlayerInput ResolvePlayer()
        {
            if (hasExplicitPlayer)
                return explicitPlayer;

            SentinalView view = ParentView;
            return view != null ? view.InputPlayer : SentinalViewRouter.AuthorityPlayer;
        }

        protected void RefreshPlayer()
        {
            PlayerInput next = ResolvePlayer();
            if (player != next)
            {
                PlayerInput previous = player;
                player = next;
                OnPlayerChanged(previous, next);
            }

            if (isActiveAndEnabled)
                OnRoutingStateChanged();
        }

        private void BindParentView()
        {
            SentinalView view = ParentView;
            if (subscribedView == view)
                return;

            UnbindParentView();
            subscribedView = view;
            if (subscribedView != null)
                subscribedView.InputChanged += OnRoutingChanged;
        }

        private void UnbindParentView()
        {
            if (subscribedView != null)
                subscribedView.InputChanged -= OnRoutingChanged;

            subscribedView = null;
        }

        private void OnRoutingChanged() => RefreshPlayer();
    }
}
#endif
