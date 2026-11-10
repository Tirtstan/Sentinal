using System.Threading;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Optional open/close animation for the <see cref="SentinalView"/> on the same GameObject. The view switches
    /// the GameObject on before showing and off only after hiding finishes, so the animation never decides state.
    /// Implementations animate from their current visual state, so a reversed transition continues from where it is.
    /// </summary>
    [RequireComponent(typeof(SentinalView))]
    public abstract class ViewTransition : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip(
            "Also animate when the view is hidden or shown again by its layer (Hide until this closes). Off: it snaps."
        )]
        private bool animateWhenHidden;

        [SerializeField]
        [Tooltip("Ignore input until the opening transition finishes.")]
        private bool inputWaitsForTransition;

        public bool InputWaitsForTransition => inputWaitsForTransition;

        public bool AnimateWhenHidden => animateWhenHidden;

        /// <summary>
        /// Animates towards shown (<paramref name="show"/> true) or hidden from the current visual state.
        /// Stops where it is when <paramref name="token"/> is cancelled.
        /// </summary>
        public abstract Awaitable PlayAsync(bool show, CancellationToken token);

        /// <summary>Jumps straight to the shown or hidden end state.</summary>
        public abstract void Snap(bool show);
    }
}
