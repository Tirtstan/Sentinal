using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sentinal
{
    [AddComponentMenu("Sentinal/View Selector"), DisallowMultipleComponent]
    public class ViewSelector : MonoBehaviour, IViewSelector
    {
        [Header("Identity")]
        [SerializeField]
        [CreateAssetButton("Assets/Resources/Sentinal View Addresses", "Address")]
        [Tooltip("Optional SO address for cross-prefab discovery via ViewAddressRegistry.")]
        private ViewAddress address;

        [Header("View")]
        [SerializeField]
        [Tooltip("Priority for focus selection. Higher priority views get focus first. Equal priority uses recency.")]
        private int priority = 0;

        [SerializeField]
        [Tooltip("Whether to track this view in the view history.")]
        private bool trackView = true;

        [SerializeField]
        [Tooltip(
            "Root view: does not get auto-closed and has special permissions around being closed (e.g. by dismissal input). Can still be hidden. Use for main screens like main menu or HUD (overlay)."
        )]
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
        [Tooltip("The first selected GameObject when selecting.")]
        private GameObject firstSelected;

        [SerializeField]
        [Tooltip(
            "Whether to prevent selection of this view. This is useful for views that interact through only input actions."
        )]
        private bool preventSelection;

        [SerializeField]
        [Tooltip("Whether to remember the last selected GameObject.")]
        private bool rememberLastSelected = true;

        [SerializeField]
        [Tooltip(
            "Automatically selects this view when it becomes active/enabled. Useful for non-tracked/tabbed views."
        )]
        private bool selectOnEnable = true;

        public GameObject FirstSelected => firstSelected;
        public GameObject LastSelected => lastSelected;
        public ViewAddress Address => address;
        public int Priority => priority;
        public bool RootView => rootView;
        public int GroupMask => groupMask;
        public bool ExclusiveView => exclusiveView;
        public bool HideOtherViews => hideOtherViews;
        public bool TrackView => trackView;
        public bool PreventSelection => preventSelection;
        public bool RememberLastSelected => rememberLastSelected;
        public bool SelectOnEnable => selectOnEnable;
        public bool IsActive => gameObject.activeInHierarchy;

        private GameObject lastSelected;
        private bool isQuitting;
        private bool isBeingHidden;
        private Coroutine selectOnEnableCoroutine;

        private void Awake()
        {
            SentinalViewRouter.OnSwitch += OnSwitch;
        }

        private void OnEnable()
        {
            if (address != null)
                ViewAddressRegistry.Register(address, this);

            if (exclusiveView)
                SentinalViewRouter.CloseAllViews(GroupMask);
            else if (hideOtherViews)
                SentinalViewRouter.HideAllViews(GroupMask, this);

            if (trackView)
                SentinalViewRouter.Add(this);

            if (selectOnEnable)
                QueueSelectOnEnable();
        }

        public void Open() => gameObject.SetActive(true);

        public void Close() => gameObject.SetActive(false);

        private void OnSwitch(ViewSelector previousView, ViewSelector newView)
        {
            if (previousView == this && rememberLastSelected)
                SaveLastSelection();

            if (newView == this)
                Select();
        }

        public void Select()
        {
            if (rememberLastSelected && lastSelected != null && lastSelected.activeInHierarchy)
                SelectLastSelected();
            else
                SelectFirstSelected();
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

            if (selectOnEnableCoroutine != null)
            {
                StopCoroutine(selectOnEnableCoroutine);
                selectOnEnableCoroutine = null;
            }

            if (rememberLastSelected)
                SaveLastSelection();

            if (trackView)
                SentinalViewRouter.Remove(this);

            if (hideOtherViews)
                SentinalViewRouter.RestoreHiddenViews(this);

            if (address != null)
                ViewAddressRegistry.Unregister(address);
        }

        /// <summary>
        /// Sets whether this view is being hidden temporarily.
        /// When true, prevents removal from view history on disable.
        /// </summary>
        public void SetBeingHidden(bool hidden) => isBeingHidden = hidden;

        /// <summary>
        /// Checks if this view is currently focused.
        /// </summary>
        public bool IsCurrent() => SentinalViewRouter.IsCurrent(this);

        /// <summary>
        /// Checks if this view shares at least one group with another view.
        /// </summary>
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

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            if (selectOnEnableCoroutine != null)
            {
                StopCoroutine(selectOnEnableCoroutine);
                selectOnEnableCoroutine = null;
            }

            // If the object is destroyed while hidden (or during scene unload),
            // OnDisable may have skipped cleanup. Ensure statics don't retain stale refs.
            if (trackView)
                SentinalViewRouter.Remove(this);

            if (address != null)
                ViewAddressRegistry.Unregister(address);

            SentinalViewRouter.OnSwitch -= OnSwitch;
        }

        private void QueueSelectOnEnable()
        {
            if (!isActiveAndEnabled)
                return;

            if (selectOnEnableCoroutine != null)
                StopCoroutine(selectOnEnableCoroutine);

            selectOnEnableCoroutine = StartCoroutine(SelectOnEnableNextFrame());
        }

        private IEnumerator SelectOnEnableNextFrame()
        {
            yield return null;

            selectOnEnableCoroutine = null;
            if (!isActiveAndEnabled)
                yield break;

            Select();
        }
    }
}
