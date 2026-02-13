using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Sentinal
{
    [AddComponentMenu("Sentinal/View Selector"), DisallowMultipleComponent]
    public class ViewSelector : MonoBehaviour, IViewSelector
    {
        [Header("Components")]
        [SerializeField]
        [Tooltip("The first selected GameObject when selecting.")]
        private GameObject firstSelected;

        [Header("View")]
        [SerializeField]
        [Tooltip("Priority for focus selection. Higher priority views get focus first. Equal priority uses recency.")]
        private int priority = 0;

        [SerializeField]
        [Tooltip("Whether to track this view in the view history.")]
        private bool trackView = true;

        [SerializeField]
        [Tooltip(
            "Root view: does not get auto-closed and has special permissions around being closed (e.g. by dismissal input). Can still be hidden. Use for main screens like main menu or HUD."
        )]
        [FormerlySerializedAs("preventDismissal")]
        private bool rootView;

        [Header("Grouping")]
        [SerializeField]
        [ViewGroupMask]
        [Tooltip(
            "Selected groups for this view. Exclusive and hide behaviors will only affect views in the same group(s)."
        )]
        private int groupMask;

        [SerializeField]
        [Tooltip(
            "Whether this view is exclusive. If true, it will close all other views (except root views) when opened. Only affects views in the same group(s) if grouping is configured."
        )]
        private bool exclusiveView;

        [SerializeField]
        [Tooltip(
            "Whether this view hides all other views when opened. Unlike exclusive, this only hides them temporarily. Only affects views in the same group(s) if grouping is configured."
        )]
        private bool hideOtherViews;

        [Header("Selection")]
        [SerializeField]
        [Tooltip(
            "Whether to prevent selection of this view. This is useful for views that interact through only input actions."
        )]
        private bool preventSelection;

        [SerializeField]
        [Tooltip("Whether to automatically select the first selected GameObject on enable.")]
        private bool autoSelectOnEnable = true;

        [SerializeField]
        [Tooltip("Whether to remember the last selected GameObject.")]
        private bool rememberLastSelected = true;

        public GameObject FirstSelected => firstSelected;
        public GameObject LastSelected => lastSelected;
        public int Priority => priority;
        public bool RootView => rootView;
        public int GroupMask => groupMask;
        public bool ExclusiveView => exclusiveView;
        public bool HideOtherViews => hideOtherViews;
        public bool TrackView => trackView;
        public bool PreventSelection => preventSelection;
        public bool AutoSelectOnEnable => autoSelectOnEnable;
        public bool RememberLastSelected => rememberLastSelected;
        public bool IsActive => gameObject.activeInHierarchy;

        private GameObject lastSelected;
        private bool isQuitting;
        private bool isBeingHidden;

        private void Awake()
        {
            Application.quitting += OnQuit;
            SentinalManager.OnSwitch += OnSwitch;
        }

        private void OnEnable()
        {
            if (exclusiveView)
                SentinalManager.Instance.CloseAllViews(GroupMask);
            else if (hideOtherViews)
                SentinalManager.Instance.HideAllViews(GroupMask, this);

            if (trackView)
                SentinalManager.Instance.Add(this);

            if (autoSelectOnEnable)
                Select();
        }

        public void Open() => gameObject.SetActive(true);

        public void Close() => gameObject.SetActive(false);

        private void OnSwitch(ViewSelector selector1, ViewSelector selector2)
        {
            if (selector1 == this && rememberLastSelected)
                SaveLastSelection();
        }

        private void OnQuit() => isQuitting = true;

        public void Select()
        {
            if (rememberLastSelected && lastSelected != null && lastSelected.activeInHierarchy)
            {
                SelectLastSelected();
            }
            else
            {
                SelectFirstSelected();
            }
        }

        public void SetFirstSelected(GameObject firstSelected) => this.firstSelected = firstSelected;

        public void SelectFirstSelected()
        {
            if (firstSelected == null)
                return;

            SetSelected(firstSelected);
        }

        public void SelectLastSelected() => SetSelected(lastSelected);

        private void SetSelected(GameObject selected)
        {
            if (EventSystem.current == null)
                return;

            if (preventSelection)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            if (selected == null || !selected.activeInHierarchy)
                return;

            EventSystem.current.SetSelectedGameObject(selected);
        }

        private void SaveLastSelection()
        {
            if (EventSystem.current == null)
                return;

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && IsSelectablePartOfThis(currentSelected))
                lastSelected = currentSelected;
        }

        private void OnDisable()
        {
            if (isQuitting || isBeingHidden)
                return;

            if (rememberLastSelected)
                SaveLastSelection();

            if (trackView)
                SentinalManager.Instance.Remove(this);

            if (hideOtherViews)
                SentinalManager.Instance.RestoreHiddenViews();
        }

        /// <summary>
        /// Sets whether this view is being hidden temporarily.
        /// When true, prevents removal from view history on disable.
        /// </summary>
        /// <param name="hidden">Whether the view is being hidden.</param>
        public void SetBeingHidden(bool hidden) => isBeingHidden = hidden;

        /// <summary>
        /// Checks if this view is currently focused.
        /// </summary>
        public bool IsCurrent() => SentinalManager.Instance != null && SentinalManager.Instance.IsCurrent(this);

        /// <summary>
        /// Checks if this view shares at least one group with another view.
        /// </summary>
        /// <param name="other">The other view to check.</param>
        /// <returns>True if both views share at least one group.</returns>
        public bool SharesGroupWith(ViewSelector other)
        {
            if (other == null)
                return false;

            if (groupMask == 0 || other.groupMask == 0)
                return false;

            return (groupMask & other.groupMask) != 0;
        }

        /// <summary>
        /// Checks if the selectable GameObject is part of this selector's hierarchy.
        /// </summary>
        /// <param name="selectable">The GameObject to check.</param>
        /// <returns>True if the selectable is part of this selector's hierarchy, false otherwise.</returns>
        private bool IsSelectablePartOfThis(GameObject selectable)
        {
            if (selectable == null)
                return false;

            Transform current = selectable.transform;
            while (current != null)
            {
                if (current.gameObject == gameObject)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void Reset()
        {
            groupMask = 1;
        }

        private void OnDestroy()
        {
            Application.quitting -= OnQuit;
            SentinalManager.OnSwitch -= OnSwitch;
        }
    }
}
