using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sentinal
{
    [DefaultExecutionOrder(-1)]
    public class Sentinal : MonoBehaviour
    {
        public static Sentinal Instance { get; private set; }

        /// <summary>
        /// Event triggered when a new view is pushed onto the view history.
        /// </summary>
        public event Action<SentinalViewSelector> OnPush;

        /// <summary>
        /// Event triggered when a view is popped from the view history.
        /// </summary>
        public event Action<SentinalViewSelector> OnPop;

        /// <summary>
        /// Event triggered when switching between views.
        /// Provides the previous view and the new view respectively.
        /// </summary>
        public event Action<SentinalViewSelector, SentinalViewSelector> OnSwitch;

        private readonly LinkedList<SentinalViewSelector> viewHistory = new();
        private readonly StringBuilder viewInfoBuilder = new();

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

        public void Push(SentinalViewSelector view)
        {
            if (view == null || viewHistory.Contains(view))
                return;

            SentinalViewSelector previousView = CurrentView;
            viewHistory.AddLast(view);
            OnPush?.Invoke(view);
            OnSwitch?.Invoke(previousView, view);
        }

        public void Pop(SentinalViewSelector view)
        {
            if (view == null)
                return;

            bool wasCurrentView = view == CurrentView;
            viewHistory.Remove(view);
            OnPop?.Invoke(view);

            if (wasCurrentView && CurrentView != null)
            {
                if (CurrentView.TryGetComponent(out SentinalViewSelector selector))
                    selector.Select();
            }

            OnSwitch?.Invoke(view, CurrentView);
        }

        public void CloseCurrentView()
        {
            if (CurrentView == null)
                return;

            if (CurrentView.TryGetComponent(out ICloseableView closeableView))
                closeableView.Close();
            else
                CurrentView.gameObject.SetActive(false);
        }

        public void CloseAllViews()
        {
            List<SentinalViewSelector> viewsToClose = new(viewHistory);
            foreach (var view in viewsToClose)
            {
                if (view.TryGetComponent(out ICloseableView closeableView))
                    closeableView.Close();
                else
                    view.gameObject.SetActive(false);
            }
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
    }
}
