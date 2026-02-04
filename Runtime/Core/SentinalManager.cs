using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sentinal
{
    [DefaultExecutionOrder(-5)]
    public class SentinalManager : MonoBehaviour
    {
        public static SentinalManager Instance { get; private set; }

        /// <summary>
        /// Event triggered when a new view is added into the view history.
        /// </summary>
        public static event Action<ViewSelector> OnAdd;

        /// <summary>
        /// Event triggered when a view is removed from the view history.
        /// </summary>
        public static event Action<ViewSelector> OnRemove;

        /// <summary>
        /// Event triggered when switching between views.
        /// Provides the previous view and the new view respectively.
        /// </summary>
        public static event Action<ViewSelector, ViewSelector> OnSwitch;

        private readonly LinkedList<ViewSelector> viewHistory = new();
        private readonly List<ViewSelector> hiddenViews = new();
        private readonly StringBuilder viewInfoBuilder = new();

        /// <summary>
        /// Gets the most recently opened view in the history.
        /// Returns null if no views are open.
        /// </summary>
        public ViewSelector MostRecentView => viewHistory.Count > 0 ? viewHistory.Last.Value : null;

        /// <summary>
        /// Gets the focused view based on priority (highest first) and then recency.
        /// Returns null if no views are open.
        /// </summary>
        public ViewSelector CurrentView => GetCurrentView();

        /// <summary>
        /// Checks if any views are open.
        /// </summary>
        public bool AnyViewsOpen => viewHistory.Count > 0;

        /// <summary>
        /// Checks if any views are open that are not root views.
        /// </summary>
        public bool AnyNonRootViewsOpen => viewHistory.Any(view => !view.RootView);
        public int ViewCount => viewHistory.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Add(ViewSelector view)
        {
            if (view == null || viewHistory.Contains(view))
                return;

            ViewSelector previousFocusedView = CurrentView;
            viewHistory.AddLast(view);
            OnAdd?.Invoke(view);

            ViewSelector newFocusedView = CurrentView;
            if (previousFocusedView != newFocusedView)
                OnSwitch?.Invoke(previousFocusedView, newFocusedView);
        }

        public void Remove(ViewSelector view)
        {
            if (view == null)
                return;

            ViewSelector previousFocusedView = CurrentView;
            bool wasCurrentView = view == previousFocusedView;

            viewHistory.Remove(view);
            OnRemove?.Invoke(view);

            ViewSelector newFocusedView = CurrentView;

            if (wasCurrentView && newFocusedView != null)
                newFocusedView.Select();

            if (previousFocusedView != newFocusedView)
                OnSwitch?.Invoke(previousFocusedView, newFocusedView);
        }

        /// <summary>
        /// Checks if the given view is the current view.
        /// </summary>
        /// <param name="view">The view to check.</param>
        /// <returns>True if the view is the current view, false otherwise.</returns>
        public bool IsCurrent(ViewSelector view) => CurrentView == view;

        /// <summary>
        /// Gets the current view based on priority (highest first) and recency (tie-breaker).
        /// When priorities are tied, the most recently opened view is selected.
        /// </summary>
        /// <returns>The current view.</returns>
        public ViewSelector GetCurrentView()
        {
            if (viewHistory.Count == 0)
                return null;

            ViewSelector focused = null;
            int maxPriority = int.MinValue;

            LinkedListNode<ViewSelector> node = viewHistory.Last;
            while (node != null)
            {
                ViewSelector view = node.Value;
                if (view == null)
                {
                    node = node.Previous;
                    continue;
                }

                int priority = view.Priority;
                if (priority > maxPriority)
                {
                    maxPriority = priority;
                    focused = view;
                }

                node = node.Previous;
            }

            return focused;
        }

        public void CloseCurrentView()
        {
            if (CurrentView == null || CurrentView.RootView)
                return;

            CloseView(CurrentView);
        }

        /// <summary>
        /// Closes all views.
        /// </summary>
        /// <param name="excludeRootViews">If true, root views will not be closed.</param>
        public void CloseAllViews(bool excludeRootViews = false)
        {
            var viewsToClose = new List<ViewSelector>(viewHistory);
            foreach (var view in viewsToClose)
            {
                if (view.RootView && excludeRootViews)
                    continue;

                CloseView(view);
            }
        }

        private void CloseView(ViewSelector view)
        {
            if (view.TryGetComponent(out ICloseableView closeableView))
            {
                closeableView.Close();
            }
            else
            {
                view.Close();
            }
        }

        public void HideAllViews(ViewSelector excludeView)
        {
            hiddenViews.Clear();

            var viewsToHide = new List<ViewSelector>(viewHistory);
            foreach (var view in viewsToHide)
            {
                if (view == excludeView || !view.gameObject.activeInHierarchy)
                    continue;

                hiddenViews.Add(view);
                view.SetBeingHidden(true);
                view.gameObject.SetActive(false);
            }
        }

        public void RestoreHiddenViews()
        {
            foreach (var view in hiddenViews)
            {
                if (view != null)
                {
                    view.SetBeingHidden(false);
                    view.gameObject.SetActive(true);
                }
            }

            hiddenViews.Clear();
        }

        public bool TrySelectCurrentView()
        {
            if (CurrentView == null || CurrentView.PreventSelection)
                return false;

            if (CurrentView.TryGetComponent(out IViewSelector selector))
            {
                selector.Select();
                return true;
            }

            return false;
        }

        public int GetViewIndex(ViewSelector view)
        {
            if (view == null)
                return -1;

            int index = 0;
            foreach (var v in viewHistory)
            {
                if (v == view)
                    return index;

                index++;
            }

            return -1;
        }

        public ViewSelector[] GetViewHistory() => viewHistory.ToArray();

        public override string ToString()
        {
            if (viewHistory.Count == 0)
                return "No open views.";

            viewInfoBuilder.Clear();
            viewInfoBuilder.AppendLine("View Stack (oldest to newest):");

            int index = 0;
            foreach (var view in viewHistory)
            {
                string viewName = view != null ? view.name : "NULL";
                viewInfoBuilder.AppendLine($"[{index}] {viewName}");
                index++;
            }

            return viewInfoBuilder.ToString().TrimEnd();
        }
    }
}
