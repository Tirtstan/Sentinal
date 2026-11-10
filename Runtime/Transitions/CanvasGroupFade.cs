using System.Threading;
using UnityEngine;

namespace Sentinal
{
    /// <summary>Fades a view's <see cref="CanvasGroup"/> in and out, with an optional scale, using unscaled time.</summary>
    [AddComponentMenu("Sentinal/Canvas Group Fade")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CanvasGroupFade : ViewTransition
    {
        [Header("Components")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Header("Configs")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds for a full fade (unscaled time, so it works while the game is paused).")]
        private float duration = 0.15f;

        [SerializeField]
        [Range(0.5f, 1f)]
        [Tooltip("Scale while fully hidden. 1 = no scale animation.")]
        private float hiddenScale = 0.96f;

        private float progress = 1f;

        /// <summary>Current fade position, from fully hidden (0) to fully shown (1).</summary>
        public float Progress => progress;

        public override async Awaitable PlayAsync(bool show, CancellationToken token)
        {
            float target = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;

            while (!Mathf.Approximately(progress, target))
            {
                await Awaitable.NextFrameAsync(token);
                progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / duration);
                Apply();
            }
        }

        public override void Snap(bool show)
        {
            progress = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            Apply();
        }

        private void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            canvasGroup.alpha = eased;
            transform.localScale = Vector3.one * Mathf.Lerp(hiddenScale, 1f, eased);
        }

        private void Awake()
        {
            if (canvasGroup == null)
                Debug.LogError(
                    $"{name} expected {nameof(canvasGroup)} assigned on {nameof(CanvasGroupFade)}; actual: none.",
                    this
                );
        }

        private void Reset() => canvasGroup = GetComponent<CanvasGroup>();
    }
}
