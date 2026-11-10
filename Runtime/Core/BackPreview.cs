namespace Sentinal
{
    /// <summary>What a Back press would do if it were pressed now.</summary>
    public enum BackOutcome
    {
        /// <summary>Nothing has focus, or the focused window has no layer.</summary>
        Nothing,

        /// <summary>A custom Back handler on <see cref="BackPreview.View"/> gets the press first and may consume it.</summary>
        Handler,

        /// <summary><see cref="BackPreview.View"/> closes.</summary>
        Closes,

        /// <summary><see cref="BackPreview.View"/>'s layer ignores Back.</summary>
        Ignored,
    }

    /// <summary>Result of <see cref="SentinalViewRouter.PreviewBack()"/>: the outcome and the view it applies to.</summary>
    public readonly struct BackPreview
    {
        public readonly BackOutcome Outcome;
        public readonly SentinalView View;

        public BackPreview(BackOutcome outcome, SentinalView view)
        {
            Outcome = outcome;
            View = view;
        }
    }
}
