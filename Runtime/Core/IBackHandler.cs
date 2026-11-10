namespace Sentinal
{
    /// <summary>
    /// Custom Back for the <see cref="SentinalView"/> on the same GameObject. Back asks the focused view's handler
    /// first, then each parent's on the way up; a view without a handler (or whose handler declines) falls back to
    /// its default: a tab passes Back to its window, another nested view closes, and a top-level view follows its
    /// layer's Back setting. Programmatic <see cref="SentinalView.Close"/> never calls this.
    /// </summary>
    public interface IBackHandler
    {
        /// <summary>Handles a Back press. Returns true when the press was consumed.</summary>
        bool TryHandleBack();
    }
}
