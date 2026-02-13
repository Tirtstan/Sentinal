using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Interface for view selectors.
    /// </summary>
    public interface IViewSelector
    {
        /// <summary>
        /// The first selected GameObject when selecting.
        /// </summary>
        public GameObject FirstSelected { get; }

        /// <summary>
        /// The last selected GameObject when selecting.
        /// </summary>
        public GameObject LastSelected { get; }

        /// <summary>
        /// Priority for focus selection. Higher priority views get focus first. Equal priority uses recency.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Whether this view is a root view (not auto-closed, special permissions around being closed; can still be hidden).
        /// </summary>
        public bool RootView { get; }

        /// <summary>
        /// The group mask for this view. Views with overlapping group masks share groups.
        /// A mask of 0 means no groups are assigned (affects all views for backward compatibility).
        /// </summary>
        public int GroupMask { get; }

        /// <summary>
        /// Whether this view is exclusive. If true, it will close all other views (except root views) when opened.
        /// Only affects views in the same group(s) if grouping is configured.
        /// </summary>
        public bool ExclusiveView { get; }

        /// <summary>
        /// Whether this view hides all other views when opened. Unlike exclusive, this only hides them temporarily.
        /// Only affects views in the same group(s) if grouping is configured.
        /// </summary>
        public bool HideOtherViews { get; }

        /// <summary>
        /// Whether to track this view in the view history.
        /// </summary>
        public bool TrackView { get; }

        /// <summary>
        /// Whether to prevent selection of this view. This is useful for views that interact through only input actions.
        /// </summary>
        public bool PreventSelection { get; }

        /// <summary>
        /// Whether to automatically select the first selected GameObject on enable.
        /// </summary>
        public bool AutoSelectOnEnable { get; }

        /// <summary>
        /// Whether to remember the last selected GameObject.
        /// </summary>
        public bool RememberLastSelected { get; }

        /// <summary>
        /// Whether this view is active.
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// Selects the first selected GameObject.
        /// </summary>
        public void Select();

        /// <summary>
        /// Opens this view.
        /// </summary>
        public void Open();

        /// <summary>
        /// Closes this view.
        /// </summary>
        public void Close();
    }
}
