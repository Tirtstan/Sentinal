using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sentinal
{
    /// <summary>
    /// Owns which views are open, which one has focus, and what Back does. Top-level views follow their layer's
    /// rules (Project Settings › Sentinal); nested views belong to their window. Focus goes to the newest shown
    /// window on the highest layer, then down through its newest open nested views.
    /// </summary>
    public static class SentinalViewRouter
    {
        /// <summary>Reports a change of the focused view (previous, current). Refresh can report the same view twice.</summary>
        public static event Action<SentinalView, SentinalView> OnSwitch;

        /// <summary>A closed view started opening.</summary>
        public static event Action<SentinalView> ViewOpened;

        /// <summary>A view finished closing (exactly once per open).</summary>
        public static event Action<SentinalView> ViewClosed;

        /// <summary>One notification after a complete layer/hierarchy change, including player focus and suspension.</summary>
        public static event Action Changed;

        private static readonly List<SentinalView> openViews = new();
        private static readonly Dictionary<PlayerInput, SentinalView> playerFocus = new();
        private static readonly Dictionary<PlayerInput, int> playerBackPending = new();
        private static readonly Dictionary<SentinalView, SentinalView> hiddenBy = new();
        private static readonly Dictionary<SentinalView, List<SentinalView>> closedWithParent = new();
        private static readonly HashSet<SentinalView> reportedMissingLayer = new();
        private static readonly StringBuilder debugBuilder = new();
        private static SentinalView currentWindow;
        private static SentinalView currentView;
        private static int backPendingUntilFrame = -1;
        private static int batchDepth;
        private static bool focusDirty;

        /// <summary>The focused view: the newest open nested view inside the focused window.</summary>
        public static SentinalView CurrentView => currentView;

        /// <summary>The top-level view that owns focus.</summary>
        public static SentinalView CurrentWindow => currentWindow;

        public static PlayerInput AuthorityPlayer { get; private set; }

        public static void SetAuthorityPlayer(PlayerInput player)
        {
            if (AuthorityPlayer == player)
                return;

            AuthorityPlayer = player;
            Refresh();
        }

        public static SentinalView GetPlayerView(PlayerInput player) =>
            player != null && playerFocus.TryGetValue(player, out SentinalView view) ? view : null;

        public static bool HasPlayerView(PlayerInput player) => GetPlayerView(player) != null;

        public static bool IsSuspended(SentinalView view) => view.IsPlayerView && GlobalUIOpen();

        /// <summary>The four Back-routing rules. Suspended non-authority menus receive no Back.</summary>
        public static SentinalView GetBackView(PlayerInput player)
        {
            if (player == null)
                return null;
            if (GlobalUIOpen() && player == AuthorityPlayer)
                return currentView;
            if (!GlobalUIOpen() && GetPlayerView(player) != null)
                return GetPlayerView(player);
            return player == AuthorityPlayer ? currentView : null;
        }

        public static bool IsBackPendingFor(PlayerInput player) =>
            player != null && playerBackPending.TryGetValue(player, out int frame) && Time.frameCount <= frame;

        public static void NotifyBackPending(PlayerInput player)
        {
            if (player == null)
                return;
            playerBackPending[player] = Time.frameCount + 1;
            if (player == AuthorityPlayer)
                NotifyBackPending();
        }

        /// <summary>
        /// True while a Back press has been claimed and not handled yet. Input openers ignore presses during this
        /// window so one press never both closes and opens.
        /// </summary>
        public static bool IsBackPending => Time.frameCount <= backPendingUntilFrame;

        /// <summary>Marks the current Back press as claimed until the end of the next frame.</summary>
        public static void NotifyBackPending() => backPendingUntilFrame = Time.frameCount + 1;

        public static bool IsCurrent(SentinalView view) =>
            view != null && (view.IsPlayerView ? GetPlayerView(view.Owner) == view : currentView == view);

        /// <summary>True when focus is on <paramref name="view"/> or one of its tabs. A focused nested popup interrupts this.</summary>
        public static bool IsFocusWithin(SentinalView view)
        {
            if (view == null)
                return false;

            for (
                SentinalView focused = view.IsPlayerView ? GetPlayerView(view.Owner) : currentView;
                focused != null;
                focused = focused.Parent
            )
            {
                if (focused == view)
                    return true;

                if (!focused.IsTab)
                    return false;
            }

            return false;
        }

        /// <summary>True when opening <paramref name="view"/> would focus it: its window's layer is at or above the focused window's.</summary>
        public static bool WouldTakeFocus(SentinalView view)
        {
            if (view == null)
                return false;

            SentinalView window = view.Window;
            SentinalView focusedWindow = view.IsPlayerView ? GetPlayerView(view.Owner) : currentWindow;
            if (focusedWindow != null)
                focusedWindow = focusedWindow.Window;
            if (focusedWindow == null || focusedWindow == window)
                return true;

            return TryGetLayer(window, out _, out int order)
                && TryGetLayer(focusedWindow, out _, out int currentOrder)
                && order >= currentOrder;
        }

        /// <summary>True when any top-level view is on screen.</summary>
        public static bool AnyViewsOpen()
        {
            foreach (SentinalView view in openViews)
                if (view.IsTopLevel && view.IsShown && view.IsActive)
                    return true;

            return false;
        }

        /// <summary>True while a shown Global layer requests Everyone to UI, including layers that ignore Back.</summary>
        public static bool GlobalUIOpen()
        {
            foreach (SentinalView view in openViews)
                if (
                    view.IsTopLevel
                    && !view.IsPlayerView
                    && view.IsShown
                    && view.IsActive
                    && TryGetLayer(view, out ViewLayer layer, out _)
                    && layer.InputWhileOpen == LayerInput.EveryoneToUI
                )
                    return true;
            return false;
        }

        /// <summary>Checks a pooled menu's layer before acquiring an instance. Closing views no longer occupy it.</summary>
        public static bool CanOpen(ViewLayerRef reference, PlayerInput owner)
        {
            SentinalSettings settings = SentinalSettings.Instance;
            if (settings == null || !settings.TryGetLayer(reference, out ViewLayer layer, out _))
                return false;
            bool playerLayer = settings.IsPlayerLayer(reference);
            if (playerLayer && owner == null)
                return false;
            foreach (SentinalView open in openViews)
                if (
                    layer.Others == LayerOthers.RefuseTheNewOne
                    && open.IsTopLevel
                    && open.IsOpen
                    && open.Layer == reference
                    && (!playerLayer || open.Owner == owner)
                )
                    return false;
            return true;
        }

        public static bool Open(SentinalView view, PlayerInput owner)
        {
            if (view == null)
                return false;
            if (!view.IsPlayerView)
            {
                Debug.LogError(
                    $"[Sentinal] '{view.name}' expected a Player layer for Open(owner); actual: Global layer.",
                    view
                );
                return false;
            }
            if (owner == null)
                return Open(view);
            if (view.Owner != owner && !CanOpen(view.Window.Layer, owner))
                return false;
            view.Window.SetOwner(owner);
            return OpenCore(view, ownerBound: true);
        }

        public static bool Open(SentinalView view) => OpenCore(view, ownerBound: false);

        private static bool OpenCore(SentinalView view, bool ownerBound)
        {
            if (view == null)
                return false;

            view.MarkSeen();
            if (view.IsPlayerView && (view.Owner == null || (view.IsTopLevel && !ownerBound)))
            {
                Debug.LogError(
                    $"[Sentinal] '{view.name}' expected Open(PlayerInput owner) on its Player layer; actual: no owner.",
                    view
                );
                view.EnsureInactive();
                return false;
            }
            if (
                view.Parent != null
                && SentinalSettings.Instance != null
                && SentinalSettings.Instance.IsPlayerLayer(view.Layer)
                && !view.Parent.IsPlayerView
            )
            {
                Debug.LogError(
                    $"[Sentinal] '{view.name}' expected a Player-layer parent or a top-level Player view; actual: nested under Global view '{view.Parent.name}'.",
                    view
                );
                view.EnsureInactive();
                return false;
            }
            if (view.IsShown)
                return true;

            ViewLayer layer = null;
            if (view.IsTopLevel && !TryGetLayer(view, out layer, out _))
            {
                view.EnsureInactive();
                return false;
            }

            BeginBatch();
            try
            {
                if (layer != null && !ApplyLayerRule(view, layer))
                {
                    view.EnsureInactive();
                    return false;
                }

                if (view.State == ViewState.Hidden)
                {
                    hiddenBy.Remove(view);
                    MoveToNewest(view);
                    view.Unhide();
                    return true;
                }

                bool wasClosed = view.State == ViewState.Closed;
                MoveToNewest(view);
                view.BeginOpen();
                if (wasClosed)
                    ViewOpened?.Invoke(view);
                else
                    ReopenChildrenClosedWith(view);

                return true;
            }
            finally
            {
                focusDirty = true;
                EndBatch();
            }
        }

        public static void Close(SentinalView view)
        {
            if (view == null)
                return;

            view.MarkSeen();
            if (view.State == ViewState.Closed)
            {
                view.EnsureInactive();
                return;
            }

            if (view.State == ViewState.Closing)
                return;

            BeginBatch();
            try
            {
                openViews.Remove(view);
                hiddenBy.Remove(view);

                bool animating = view.WillAnimateClose;
                var children = new List<SentinalView>();
                foreach (SentinalView open in openViews.ToArray())
                {
                    if (open == view || !IsDescendantOf(open, view))
                        continue;

                    openViews.Remove(open);
                    children.Add(open);
                }

                if (animating && children.Count > 0)
                    closedWithParent[view] = children;

                foreach (SentinalView child in children)
                    child.CloseWithParent(animating);

                RestoreViewsHiddenBy(view);
                view.BeginClose();
            }
            finally
            {
                focusDirty = true;
                EndBatch();
            }
        }

        /// <summary>Opens the view if needed and makes it the newest on its layer.</summary>
        public static void BringToFront(SentinalView view)
        {
            if (view == null)
                return;

            if (!view.IsShown)
            {
                if (view.IsPlayerView)
                    Open(view, view.Owner);
                else
                    Open(view);
                return;
            }

            MoveToNewest(view);
            RecomputeFocus(force: false);
        }

        /// <summary>
        /// Handles a Back press for the focused view: each view's <see cref="IBackHandler"/> first, walking up;
        /// otherwise a tab passes Back to its window, another nested view closes, and a window follows its layer.
        /// Returns true when the press was consumed.
        /// </summary>
        public static bool HandleBack()
        {
            return HandleBackChain(currentView);
        }

        public static bool HandleBack(PlayerInput player) => HandleBackChain(GetBackView(player));

        private static bool HandleBackChain(SentinalView focused)
        {
            for (SentinalView view = focused; view != null; view = view.Parent)
            {
                foreach (IBackHandler handler in view.GetComponents<IBackHandler>())
                    if ((handler is not Behaviour behaviour || behaviour.isActiveAndEnabled) && handler.TryHandleBack())
                        return true;

                if (view.IsTopLevel)
                {
                    if (!TryGetLayer(view, out ViewLayer layer, out _) || layer.Back == LayerBack.Ignored)
                        return false;

                    view.Close();
                    return true;
                }

                if (view.IsTab)
                    continue;

                view.Close();
                return true;
            }

            return false;
        }

        /// <summary>True when <see cref="HandleBack"/> could do something for the focused view.</summary>
        public static bool CanHandleBack() => PreviewBack().Outcome is BackOutcome.Closes or BackOutcome.Handler;

        public static bool CanHandleBack(PlayerInput player) =>
            PreviewBack(player).Outcome is BackOutcome.Closes or BackOutcome.Handler;

        /// <summary>What a global Back press would do right now, without running any handler.</summary>
        public static BackPreview PreviewBack() => PreviewBackChain(currentView);

        /// <summary>What <paramref name="player"/>'s Back press would do right now, without running any handler.</summary>
        public static BackPreview PreviewBack(PlayerInput player) => PreviewBackChain(GetBackView(player));

        /// <summary>Mirrors <see cref="HandleBackChain"/>; a custom handler is reported, never invoked.</summary>
        private static BackPreview PreviewBackChain(SentinalView focused)
        {
            for (SentinalView view = focused; view != null; view = view.Parent)
            {
                foreach (IBackHandler handler in view.GetComponents<IBackHandler>())
                    if (handler is not Behaviour behaviour || behaviour.isActiveAndEnabled)
                        return new BackPreview(BackOutcome.Handler, view);

                if (view.IsTopLevel)
                {
                    if (!TryGetLayer(view, out ViewLayer layer, out _))
                        return new BackPreview(BackOutcome.Nothing, view);
                    return new BackPreview(
                        layer.Back == LayerBack.ClosesTheView ? BackOutcome.Closes : BackOutcome.Ignored,
                        view
                    );
                }

                if (!view.IsTab)
                    return new BackPreview(BackOutcome.Closes, view);
            }

            return new BackPreview(BackOutcome.Nothing, null);
        }

        /// <summary>Re-reports the focused view through <see cref="OnSwitch"/> (e.g. after a player change).</summary>
        public static void Refresh()
        {
            RecomputeFocus(force: true);
            Changed?.Invoke();
        }

        public static bool TrySelectCurrentView()
        {
            if (currentView == null)
                return false;

            currentView.Select();
            return true;
        }

        /// <summary>Opens a view by its address, instantiating its direct fallback prefab if needed.</summary>
        public static SentinalView OpenView(ViewAddress address)
        {
            if (address == null)
                return null;

            SentinalView view = ViewAddressRegistry.Resolve(address);
            if (view != null)
                view.Open();

            return view;
        }

        /// <summary>Opens a view by address, asynchronously instantiating an Addressable prefab when configured.</summary>
        public static async Task<SentinalView> OpenViewAsync(ViewAddress address)
        {
            SentinalView view = await ViewAddressRegistry.ResolveAsync(address);
            if (view != null)
                view.Open();

            return view;
        }

        /// <summary>Open views (opening, open or hidden), oldest first.</summary>
        public static SentinalView[] GetOpenViews() => openViews.ToArray();

        /// <summary>The view that hid <paramref name="view"/> through its layer, or null.</summary>
        public static SentinalView GetHiddenBy(SentinalView view) =>
            view != null && hiddenBy.TryGetValue(view, out SentinalView hider) ? hider : null;

        /// <summary>Finds a view's window layer and stacking order, reporting a missing layer once per view.</summary>
        public static bool TryGetLayer(SentinalView view, out ViewLayer layer, out int order)
        {
            layer = null;
            order = -1;
            if (view == null)
                return false;

            SentinalView window = view.Window;
            SentinalSettings settings = SentinalSettings.Instance;
            if (settings != null && settings.TryGetLayer(window.Layer, out layer, out order))
                return true;

            if (settings != null && reportedMissingLayer.Add(window))
                Debug.LogError(
                    $"[Sentinal] '{window.name}' expected a layer from Project Settings › Sentinal; actual: {(window.Layer.IsSet ? $"unknown layer id {window.Layer.Id}" : "none")}. It can't open until it has one.",
                    window
                );

            return false;
        }

        public static string GetDebugString()
        {
            if (openViews.Count == 0)
                return "No open views.";

            debugBuilder.Clear();
            debugBuilder.AppendLine("Open views (oldest to newest):");
            for (int i = 0; i < openViews.Count; i++)
            {
                SentinalView view = openViews[i];
                string marker = view == currentView ? " *" : "";
                string owner = view.Parent != null ? view.Parent.name : LayerName(view);
                debugBuilder.AppendLine($"  [{i}] {view.name} ({view.State}, {owner}){marker}");
            }

            return debugBuilder.ToString().TrimEnd();
        }

        internal static void NotifyClosed(SentinalView view)
        {
            BeginBatch();
            try
            {
                openViews.Remove(view);
                hiddenBy.Remove(view);
                closedWithParent.Remove(view);
                RestoreViewsHiddenBy(view);
                ViewClosed?.Invoke(view);
            }
            finally
            {
                focusDirty = true;
                EndBatch();
            }
        }

        /// <summary>A view or one of its parents was switched on or off outside Sentinal; focus skips inactive views.</summary>
        internal static void NotifyHierarchyChanged()
        {
            focusDirty = true;
            if (batchDepth == 0)
                EndBatchWork();
        }

        internal static void Forget(SentinalView view)
        {
            openViews.Remove(view);
            hiddenBy.Remove(view);
            closedWithParent.Remove(view);
            reportedMissingLayer.Remove(view);
            if (currentView == view || currentWindow == view)
                NotifyHierarchyChanged();
        }

        private static bool ApplyLayerRule(SentinalView view, ViewLayer layer)
        {
            var others = new List<SentinalView>();
            foreach (SentinalView open in openViews)
                if (
                    open != view
                    && open.IsTopLevel
                    && open.Layer == view.Layer
                    && (!view.IsPlayerView || open.Owner == view.Owner)
                )
                    others.Add(open);

            switch (layer.Others)
            {
                case LayerOthers.RefuseTheNewOne:
                    return others.Count == 0;
                case LayerOthers.HideUntilThisCloses:
                    foreach (SentinalView other in others)
                    {
                        if (!other.IsShown)
                            continue;

                        hiddenBy[other] = view;
                        other.Hide();
                    }

                    return true;
                case LayerOthers.Close:
                    foreach (SentinalView other in others)
                        Close(other);

                    return true;
                default:
                    return true;
            }
        }

        private static void RestoreViewsHiddenBy(SentinalView hider)
        {
            var restored = new List<SentinalView>();
            foreach (KeyValuePair<SentinalView, SentinalView> entry in hiddenBy)
                if (entry.Value == hider)
                    restored.Add(entry.Key);

            foreach (SentinalView view in restored)
            {
                hiddenBy.Remove(view);
                if (view != null && view.State == ViewState.Hidden)
                    view.Unhide();
            }
        }

        private static void ReopenChildrenClosedWith(SentinalView view)
        {
            if (!closedWithParent.Remove(view, out List<SentinalView> children))
                return;

            foreach (SentinalView child in children)
            {
                if (child == null || child.State != ViewState.Closed || !child.gameObject.activeSelf)
                    continue;

                MoveToNewest(child);
                child.BeginOpen();
                ViewOpened?.Invoke(child);
            }
        }

        private static void MoveToNewest(SentinalView view)
        {
            openViews.Remove(view);
            openViews.Add(view);
        }

        private static bool IsDescendantOf(SentinalView view, SentinalView ancestor)
        {
            for (SentinalView parent = view.Parent; parent != null; parent = parent.Parent)
                if (parent == ancestor)
                    return true;

            return false;
        }

        private static void BeginBatch() => batchDepth++;

        private static void EndBatch()
        {
            batchDepth--;
            if (batchDepth == 0)
                EndBatchWork();
        }

        private static void EndBatchWork()
        {
            if (focusDirty)
            {
                focusDirty = false;
                RecomputeFocus(force: false);
                Changed?.Invoke();
            }
        }

        private static void RecomputeFocus(bool force)
        {
            SentinalView previousView = currentView;
            SentinalView previousWindow = currentWindow;

            currentWindow = null;
            playerFocus.Clear();
            int bestOrder = int.MinValue;
            foreach (SentinalView view in openViews)
            {
                if (!view.IsTopLevel || !view.IsShown || !view.IsActive)
                    continue;

                if (!TryGetLayer(view, out _, out int order))
                    continue;

                if (view.IsPlayerView)
                {
                    PlayerInput owner = view.Owner;
                    if (owner == null)
                        continue;
                    if (
                        !playerFocus.TryGetValue(owner, out SentinalView previous)
                        || !TryGetLayer(previous, out _, out int previousOrder)
                        || order >= previousOrder
                    )
                        playerFocus[owner] = view;
                    continue;
                }

                if (order < bestOrder)
                    continue;
                bestOrder = order;
                currentWindow = view;
            }

            currentView = currentWindow;
            while (currentView != null && TryFindFocusedChild(currentView, out SentinalView child))
                currentView = child;

            foreach (PlayerInput player in new List<PlayerInput>(playerFocus.Keys))
            {
                SentinalView focused = playerFocus[player];
                while (TryFindFocusedChild(focused, out SentinalView child))
                    focused = child;
                playerFocus[player] = focused;
            }

            foreach (SentinalView view in openViews.ToArray())
                view.RefreshInputState();

            if (!force && previousView == currentView && previousWindow == currentWindow)
                return;

            if (previousView != null && previousView != currentView)
                previousView.SaveLastSelection();

            OnSwitch?.Invoke(previousView, currentView);
            TrySelectCurrentView();
        }

        private static bool TryFindFocusedChild(SentinalView parent, out SentinalView child)
        {
            for (int i = openViews.Count - 1; i >= 0; i--)
            {
                SentinalView view = openViews[i];
                if (view.Parent == parent && view.IsShown && view.IsActive)
                {
                    child = view;
                    return true;
                }
            }

            child = null;
            return false;
        }

        private static string LayerName(SentinalView view) =>
            TryGetLayer(view, out ViewLayer layer, out _) ? layer.Name : "no layer";

        /// <summary>Each scene brings its own EventSystem with nothing selected, so reapply the focused view's selection.</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySelectCurrentView();

        /// <summary>Clears all router state. Runs on Play Mode entry (domain reload is disabled) and between tests.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            openViews.Clear();
            playerFocus.Clear();
            playerBackPending.Clear();
            AuthorityPlayer = null;
            hiddenBy.Clear();
            closedWithParent.Clear();
            reportedMissingLayer.Clear();
            currentWindow = null;
            currentView = null;
            backPendingUntilFrame = -1;
            batchDepth = 0;
            focusDirty = false;
            OnSwitch = null;
            ViewOpened = null;
            ViewClosed = null;
            Changed = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }
}
