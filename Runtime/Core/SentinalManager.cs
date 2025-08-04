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
        public event Action<SentinalViewSelector> OnAdd;

        /// <summary>
        /// Event triggered when a view is removed from the view history.
        /// </summary>
        public event Action<SentinalViewSelector> OnRemove;

        /// <summary>
        /// Event triggered when switching between views.
        /// Provides the previous view and the new view respectively.
        /// </summary>
        public event Action<SentinalViewSelector, SentinalViewSelector> OnSwitch;

        private readonly LinkedList<SentinalViewSelector> viewHistory = new();
        private readonly StringBuilder viewInfoBuilder = new();

        /// <summary>
        /// Gets the last view in the history.
        /// Returns null if no views are open.
        /// </summary>
        public SentinalViewSelector CurrentView => viewHistory.Count > 0 ? viewHistory.Last.Value : null;
        public bool AnyViewsOpen => viewHistory.Count > 0;
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

        public void Add(SentinalViewSelector view)
        {
            if (view == null || viewHistory.Contains(view))
                return;

            SentinalViewSelector previousView = CurrentView;
            viewHistory.AddLast(view);
            OnAdd?.Invoke(view);
            OnSwitch?.Invoke(previousView, view);
        }

        public void Remove(SentinalViewSelector view)
        {
            if (view == null)
                return;

            bool wasCurrentView = view == CurrentView;
            viewHistory.Remove(view);
            OnRemove?.Invoke(view);

            if (wasCurrentView && CurrentView != null)
            {
                if (CurrentView.TryGetComponent(out ISentinalSelector selector))
                    selector.Select();
            }

            OnSwitch?.Invoke(view, CurrentView);
        }

        public void CloseCurrentView()
        {
            if (CurrentView == null || CurrentView.IsRootView())
                return;

            if (CurrentView.TryGetComponent(out ICloseableView closeableView))
                closeableView.Close();
            else
                CurrentView.gameObject.SetActive(false);
        }

        public void CloseAllViews(bool includeRoots = false)
        {
            var viewsToClose = new List<SentinalViewSelector>(viewHistory);
            foreach (var view in viewsToClose)
            {
                if (view.IsRootView() && !includeRoots)
                    continue;

                if (view.TryGetComponent(out ICloseableView closeableView))
                    closeableView.Close();
                else
                    view.gameObject.SetActive(false);
            }
        }

        public bool TrySelectCurrentView()
        {
            if (CurrentView == null || CurrentView.HasPreventSelection())
                return false;

            if (CurrentView.TryGetComponent(out ISentinalSelector selector))
            {
                selector.Select();
                return true;
            }

            return false;
        }

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

        public int GetViewIndex(SentinalViewSelector view)
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

        public SentinalViewSelector[] GetViewHistory() => viewHistory.ToArray();
    }
}
