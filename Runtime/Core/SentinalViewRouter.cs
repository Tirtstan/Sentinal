using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Static view router that replaces the old SentinalManager singleton.
    /// Tracks view history, handles switching, hiding, and restoring.
    /// Independent windows compete globally; explicitly owned panels resolve within their window.
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
        /// Reports a change to the focused view or its ownership path.
        /// Refresh/configuration notifications can report the same view twice.
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
        private static bool isProcessing;
        private static readonly Queue<Action> deferredActions = new();
        private static ViewSelector currentWindow;
        private static ViewSelector currentView;

        /// <summary>
        /// Gets the most recently opened view in the history.
        /// Returns null if no views are open.
        /// </summary>
        public static ViewSelector MostRecentView => viewHistory.Count > 0 ? viewHistory.Last.Value : null;

        /// <summary>
        /// Gets the focused leaf inside the current window. Null when no window is focused.
        /// </summary>
        public static ViewSelector CurrentView => currentView;

        /// <summary>The independent window owning the focused panel, chosen by priority and open order.</summary>
        public static ViewSelector CurrentWindow => currentWindow;

        /// <summary>
        /// Checks if any views are open, optionally filtered by a group mask.
        /// </summary>
        public static bool AnyViewsOpen(ViewGroupMask? groupMask = null)
        {
            int mask = groupMask?.Value ?? -1;
            foreach (var view in viewHistory)
            {
                if (view != null && view.IsActive)
                {
                    if (!MatchesGroupMask(mask, view.GroupMask))
                        continue;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if any views are open that are not root views, optionally filtered by a group mask.
        /// </summary>
        public static bool AnyNonRootViewsOpen(ViewGroupMask? groupMask = null)
        {
            int mask = groupMask?.Value ?? -1;
            foreach (var view in viewHistory)
            {
                if (view != null && ReferenceEquals(view.FocusParent, null) && view.IsActive && !view.RootView)
                {
                    if (!MatchesGroupMask(mask, view.GroupMask))
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

            if (isProcessing)
            {
                deferredActions.Enqueue(() => Add(view));
                return;
            }

            isProcessing = true;
            try
            {
                viewHistory.AddLast(view);
                OnAdd?.Invoke(view);
                NotifyFocusChanged(selectCurrentView: true);
                NotifyNonRootViewPresenceChangedIfNeeded();
            }
            finally
            {
                isProcessing = false;
                ProcessDeferredActions();
            }
        }

        public static void Remove(ViewSelector view)
        {
            if (view == null)
                return;

            if (isProcessing)
            {
                deferredActions.Enqueue(() => Remove(view));
                return;
            }

            isProcessing = true;
            try
            {
                viewHistory.Remove(view);
                OnRemove?.Invoke(view);
                NotifyFocusChanged(selectCurrentView: true);
                NotifyNonRootViewPresenceChangedIfNeeded();
            }
            finally
            {
                isProcessing = false;
                ProcessDeferredActions();
            }
        }

        private static void ProcessDeferredActions()
        {
            while (deferredActions.Count > 0)
            {
                var action = deferredActions.Dequeue();
                action?.Invoke();
            }
        }

        /// <summary>
        /// Checks if the given view is the current view.
        /// </summary>
        public static bool IsCurrent(ViewSelector view) => CurrentView == view;

        /// <summary>
        /// Gets the focused leaf of the current window's explicit ownership chain.
        /// </summary>
        public static ViewSelector GetCurrentView() => CurrentView;

        /// <summary>True for the focused view and its explicit focus owners. Independent modals interrupt this scope.</summary>
        public static bool IsFocusWithin(ViewSelector view)
        {
            if (view == null)
                return false;

            for (ViewSelector focused = CurrentView; focused != null; focused = focused.FocusParent)
                if (focused == view)
                    return true;

            return false;
        }

        private static ViewSelector FindFocusedChild(ViewSelector parent)
        {
            ViewSelector focused = null;
            int maxPriority = int.MinValue;

            LinkedListNode<ViewSelector> node = viewHistory.Last;
            while (node != null)
            {
                ViewSelector view = node.Value;
                if (view == null || !ReferenceEquals(view.FocusParent, parent) || !view.enabled || !view.IsActive)
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

        /// <summary>
        /// Closes the focused non-root view through its nearest available close handler.
        /// Parent handlers can own dismissal for child views such as tabs.
        /// </summary>
        public static void CloseCurrentView()
        {
            ViewSelector focused = CurrentView;
            if (focused == null || CurrentWindow == null || CurrentWindow.RootView)
                return;

            for (ViewSelector owner = focused; owner != null; owner = owner.FocusParent)
                if (owner.TryGetComponent(out ICloseableView closeableView))
                {
                    closeableView.Close();
                    return;
                }

            CurrentWindow.Close();
        }

        /// <summary>
        /// Forces a refresh of the current view, triggering <see cref="OnSwitch"/> with the current view.
        /// Useful for systems like <see cref="ActionMapGate"/> to re-apply rules (e.g. for late-joining players).
        /// </summary>
        public static void Refresh()
        {
            NotifyConfigurationChanged();
        }

        internal static void NotifyConfigurationChanged()
        {
            if (isProcessing)
            {
                deferredActions.Enqueue(NotifyConfigurationChanged);
                return;
            }

            isProcessing = true;
            try
            {
                NotifyFocusChanged(selectCurrentView: true, forceNotification: true);
                NotifyNonRootViewPresenceChangedIfNeeded();
            }
            finally
            {
                isProcessing = false;
                ProcessDeferredActions();
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
        /// Opens a view by address, asynchronously instantiating an Addressable prefab when configured.
        /// </summary>
        public static async Task<ViewSelector> OpenViewAsync(ViewAddress address)
        {
            ViewSelector view = await ViewAddressRegistry.ResolveAsync(address);
            if (view != null && !view.gameObject.activeInHierarchy)
                view.Open();

            return view;
        }

        /// <summary>        /// Closes all views.
        /// </summary>
        /// <param name="excludeRootViews">If true, root views will not be closed.</param>
        public static void CloseAllViews(bool excludeRootViews = false) => CloseAllViews(null, excludeRootViews);

        /// <summary>
        /// Closes all views that match the given group mask.
        /// </summary>
        public static void CloseAllViews(ViewGroupMask? groupMask, bool excludeRootViews = false) =>
            CloseAllViews(groupMask, excludeRootViews, null);

        internal static void CloseAllViews(ViewGroupMask? groupMask, bool excludeRootViews, ViewSelector excludeView)
        {
            int mask = groupMask?.Value ?? -1;
            var viewsToClose = new List<ViewSelector>(viewHistory);
            foreach (var view in viewsToClose)
            {
                if (
                    view == null
                    || !ReferenceEquals(view.FocusParent, null)
                    || (excludeView != null && view == excludeView.FocusWindow)
                )
                    continue;

                if (view.RootView && excludeRootViews)
                    continue;

                if (!MatchesGroupMask(mask, view.GroupMask))
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
        public static void HideAllViews(ViewGroupMask? groupMask, ViewSelector excludeView)
        {
            int mask = groupMask?.Value ?? -1;
            var targets = new List<ViewSelector>();

            var snapshot = new List<ViewSelector>(viewHistory);
            foreach (var view in snapshot)
            {
                if (
                    view == null
                    || !ReferenceEquals(view.FocusParent, null)
                    || (excludeView != null && view == excludeView.FocusWindow)
                    || !view.IsActive
                )
                    continue;

                if (!MatchesGroupMask(mask, view.GroupMask))
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
            NotifyFocusChanged(selectCurrentView: true);
            NotifyNonRootViewPresenceChangedIfNeeded();
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

            foreach (var view in entry.views)
            {
                if (view == null)
                    continue;

                view.SetBeingHidden(false);
                view.gameObject.SetActive(true);
            }

            NotifyFocusChanged(selectCurrentView: true);
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

        private static void NotifyFocusChanged(bool selectCurrentView, bool forceNotification = false)
        {
            ViewSelector previousFocusedView = currentView;
            ViewSelector previousWindow = currentWindow;
            currentWindow = FindFocusedChild(null);
            currentView = currentWindow;
            ViewSelector child;
            while (currentView != null && (child = FindFocusedChild(currentView)) != null)
                currentView = child;

            if (
                previousFocusedView != currentView
                || previousWindow != currentWindow
                || (forceNotification && currentView != null)
            )
                OnSwitch?.Invoke(previousFocusedView, currentView);

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

        private static bool MatchesGroupMask(int filterMask, ViewGroupMask viewMask) =>
            filterMask == ViewGroupMask.Everything.Value || (filterMask & viewMask.Value) != 0;

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
                    string parentName = view.FocusParent != null ? view.FocusParent.name : "Window";
                    viewInfoBuilder.AppendLine(
                        $"  [{index}] {view.name} (Focus owner: {parentName}, P:{view.Priority}){marker}"
                    );
                }

                index++;
            }

            return viewInfoBuilder.ToString().TrimEnd();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            viewHistory.Clear();
            currentWindow = null;
            currentView = null;
            hiddenViewStack.Clear();
            OnAdd = null;
            OnRemove = null;
            OnSwitch = null;
            OnNonRootViewPresenceChanged = null;
            lastNonRootViewPresence = false;
            isProcessing = false;
            deferredActions.Clear();
        }
    }
}
