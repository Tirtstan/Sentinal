using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Static view router that replaces the old SentinalManager singleton.
    /// Tracks view history, handles switching, hiding, and restoring.
    /// No scene object required — views self-register on enable.
    /// </summary>
    public static class SentinalViewRouter
    {
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

        /// <summary>
        /// Event triggered when the presence of open non-root views changes.
        /// True: at least one non-root view is open. False: only root/no views remain.
        /// </summary>
        public static event Action<bool> OnNonRootViewPresenceChanged;

        private static readonly LinkedList<ViewSelector> viewHistory = new();
        private static readonly Stack<(ViewSelector owner, List<ViewSelector> views)> hiddenViewStack = new();
        private static readonly StringBuilder viewInfoBuilder = new();
        private static bool lastNonRootViewPresence;

        /// <summary>
        /// Gets the most recently opened view in the history.
        /// Returns null if no views are open.
        /// </summary>
        public static ViewSelector MostRecentView => viewHistory.Count > 0 ? viewHistory.Last.Value : null;

        /// <summary>
        /// Gets the focused view based on priority (highest first) and then recency.
        /// Returns null if no views are open.
        /// </summary>
        public static ViewSelector CurrentView => GetCurrentView();

        /// <summary>
        /// Checks if any views are open, optionally filtered by a group mask.
        /// </summary>
        public static bool AnyViewsOpen(int groupMask = -1)
        {
            foreach (var view in viewHistory)
            {
                if (view != null && view.IsActive)
                {
                    if (groupMask >= 0 && (groupMask & view.GroupMask) == 0)
                        continue;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if any views are open that are not root views, optionally filtered by a group mask.
        /// </summary>
        public static bool AnyNonRootViewsOpen(int groupMask = -1)
        {
            foreach (var view in viewHistory)
            {
                if (view != null && view.IsActive && !view.RootView)
                {
                    if (groupMask >= 0 && (groupMask & view.GroupMask) == 0)
                        continue;

                    return true;
                }
            }

            return false;
        }

        public static int ViewCount => viewHistory.Count;

        public static void Add(ViewSelector view)
        {
            if (view == null || viewHistory.Contains(view))
                return;

            ViewSelector previousFocusedView = CurrentView;
            viewHistory.AddLast(view);
            OnAdd?.Invoke(view);
            NotifyFocusChanged(previousFocusedView, selectCurrentView: true);
            NotifyNonRootViewPresenceChangedIfNeeded();
        }

        public static void Remove(ViewSelector view)
        {
            if (view == null)
                return;

            ViewSelector previousFocusedView = CurrentView;

            viewHistory.Remove(view);
            OnRemove?.Invoke(view);
            NotifyFocusChanged(previousFocusedView, selectCurrentView: true);
            NotifyNonRootViewPresenceChangedIfNeeded();
        }

        /// <summary>
        /// Checks if the given view is the current view.
        /// </summary>
        public static bool IsCurrent(ViewSelector view) => CurrentView == view;

        /// <summary>
        /// Gets the current view based on priority (highest first) and recency (tie-breaker).
        /// </summary>
        public static ViewSelector GetCurrentView()
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

        public static void CloseCurrentView()
        {
            if (CurrentView == null || CurrentView.RootView)
                return;

            CloseView(CurrentView);
        }

        /// <summary>
        /// Forces a refresh of the current view, triggering <see cref="OnSwitch"/> with the current view.
        /// Useful for systems like <see cref="ActionMapGate"/> to re-apply rules (e.g. for late-joining players).
        /// </summary>
        public static void Refresh()
        {
            if (CurrentView != null)
            {
                OnSwitch?.Invoke(CurrentView, CurrentView);
                TrySelectCurrentView();
            }
        }

        /// <summary>
        /// Opens a view by its address. Resolves the address via the registry, instantiating it if necessary, and activates it.
        /// </summary>
        public static ViewSelector OpenView(ViewAddress address)
        {
            if (address == null)
                return null;

            var view = ViewAddressRegistry.Resolve(address);
            if (view != null && !view.gameObject.activeInHierarchy)
                view.Open();

            return view;
        }

        /// <summary>
        /// Closes all views.
        /// </summary>
        /// <param name="excludeRootViews">If true, root views will not be closed.</param>
        public static void CloseAllViews(bool excludeRootViews = false) => CloseAllViews(-1, excludeRootViews);

        /// <summary>
        /// Closes all views that match the given group mask.
        /// </summary>
        public static void CloseAllViews(int groupMask, bool excludeRootViews = false)
        {
            var viewsToClose = new List<ViewSelector>(viewHistory);
            foreach (var view in viewsToClose)
            {
                if (view.RootView && excludeRootViews)
                    continue;

                if (groupMask >= 0 && (groupMask & view.GroupMask) == 0)
                    continue;

                CloseView(view);
            }
        }

        private static void CloseView(ViewSelector view)
        {
            if (view.TryGetComponent(out ICloseableView closeableView))
                closeableView.Close();
            else
                view.Close();
        }

        /// <summary>
        /// Hides all views that match the given group mask, excluding a specific view.
        /// Uses hardened two-pass approach: marks all targets as hidden BEFORE disabling any,
        /// preventing same-frame race conditions.
        /// </summary>
        public static void HideAllViews(int groupMask, ViewSelector excludeView)
        {
            var targets = new List<ViewSelector>();

            var snapshot = new List<ViewSelector>(viewHistory);
            foreach (var view in snapshot)
            {
                if (view == excludeView || !view.gameObject.activeInHierarchy)
                    continue;

                if (groupMask >= 0 && (groupMask & view.GroupMask) == 0)
                    continue;

                targets.Add(view);
            }

            if (targets.Count == 0)
                return;

            RemoveLatestHiddenEntry(excludeView);

            // Pass 1: Mark ALL targets as hidden BEFORE any SetActive(false).
            // This prevents the race where OnDisable fires before isBeingHidden is set.
            foreach (var view in targets)
                view.SetBeingHidden(true);

            // Pass 2: Now safe to disable — OnDisable checks isBeingHidden and skips removal.
            foreach (var view in targets)
                view.gameObject.SetActive(false);

            hiddenViewStack.Push((excludeView, targets));
        }

        /// <summary>
        /// Restores the most recent set of hidden views owned by the given view.
        /// </summary>
        public static void RestoreHiddenViews(ViewSelector owner)
        {
            if (hiddenViewStack.Count == 0 || owner == null)
                return;

            if (!TryPopHiddenEntry(owner, out var entry))
                return;

            RestoreHiddenEntry(entry);
        }

        /// <summary>
        /// Restores the topmost set of hidden views.
        /// </summary>
        public static void RestoreHiddenViews()
        {
            if (hiddenViewStack.Count == 0)
                return;

            var entry = hiddenViewStack.Pop();
            RestoreHiddenEntry(entry);
        }

        private static void RestoreHiddenEntry((ViewSelector owner, List<ViewSelector> views) entry)
        {
            if (entry.views == null || entry.views.Count == 0)
                return;

            ViewSelector previousFocusedView = CurrentView;
            foreach (var view in entry.views)
            {
                if (view == null)
                    continue;

                view.SetBeingHidden(false);
                view.gameObject.SetActive(true);
            }

            NotifyFocusChanged(previousFocusedView, selectCurrentView: true);
            NotifyNonRootViewPresenceChangedIfNeeded();
        }

        private static bool TryPopHiddenEntry(
            ViewSelector owner,
            out (ViewSelector owner, List<ViewSelector> views) matchingEntry
        )
        {
            var tempStack = new Stack<(ViewSelector owner, List<ViewSelector> views)>();
            matchingEntry = default;
            bool found = false;

            while (hiddenViewStack.Count > 0)
            {
                var entry = hiddenViewStack.Pop();
                if (!found && entry.owner == owner)
                {
                    matchingEntry = entry;
                    found = true;
                    continue;
                }

                tempStack.Push(entry);
            }

            while (tempStack.Count > 0)
                hiddenViewStack.Push(tempStack.Pop());

            return found;
        }

        private static void RemoveLatestHiddenEntry(ViewSelector owner)
        {
            if (hiddenViewStack.Count == 0 || owner == null)
                return;

            var tempStack = new Stack<(ViewSelector owner, List<ViewSelector> views)>();
            bool removed = false;

            while (hiddenViewStack.Count > 0)
            {
                var entry = hiddenViewStack.Pop();
                if (!removed && entry.owner == owner)
                {
                    removed = true;
                    continue;
                }

                tempStack.Push(entry);
            }

            while (tempStack.Count > 0)
                hiddenViewStack.Push(tempStack.Pop());
        }

        private static void NotifyFocusChanged(ViewSelector previousFocusedView, bool selectCurrentView)
        {
            ViewSelector newFocusedView = CurrentView;
            if (previousFocusedView != newFocusedView)
                OnSwitch?.Invoke(previousFocusedView, newFocusedView);

            if (selectCurrentView)
                TrySelectCurrentView();
        }

        private static void NotifyNonRootViewPresenceChangedIfNeeded()
        {
            bool hasNonRootViews = AnyNonRootViewsOpen();
            if (hasNonRootViews == lastNonRootViewPresence)
                return;

            lastNonRootViewPresence = hasNonRootViews;
            OnNonRootViewPresenceChanged?.Invoke(hasNonRootViews);
        }

        public static bool TrySelectCurrentView()
        {
            if (CurrentView == null)
                return false;

            if (CurrentView.TryGetComponent(out IViewSelector selector))
            {
                selector.Select();
                return true;
            }

            return false;
        }

        public static int GetViewIndex(ViewSelector view)
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

        public static ViewSelector[] GetViewHistory() => new List<ViewSelector>(viewHistory).ToArray();

        public static string GetDebugString()
        {
            if (viewHistory.Count == 0)
                return "No open views.";

            ViewSelector current = CurrentView;

            viewInfoBuilder.Clear();
            viewInfoBuilder.AppendLine("View Stack (oldest to newest):");

            int index = 0;
            foreach (var view in viewHistory)
            {
                if (view == null)
                {
                    viewInfoBuilder.AppendLine($"  [{index}] NULL");
                }
                else
                {
                    string marker = view == current ? " *" : "";
                    viewInfoBuilder.AppendLine($"  [{index}] {view.name} (P:{view.Priority}){marker}");
                }

                index++;
            }

            return viewInfoBuilder.ToString().TrimEnd();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            viewHistory.Clear();
            hiddenViewStack.Clear();
            OnAdd = null;
            OnRemove = null;
            OnSwitch = null;
            OnNonRootViewPresenceChanged = null;
            lastNonRootViewPresence = false;
        }
    }
}
