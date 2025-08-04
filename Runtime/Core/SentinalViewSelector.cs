using UnityEngine;
using UnityEngine.EventSystems;

namespace Sentinal
{
    [AddComponentMenu("Sentinal/View Selector"), DisallowMultipleComponent]
    public class SentinalViewSelector : MonoBehaviour, ISentinalSelector
    {
        public GameObject FirstSelected => firstSelected;
        public GameObject LastSelected => lastSelected;

        [Header("Components")]
        [SerializeField]
        [Tooltip("The first selected GameObject when selecting.")]
        private GameObject firstSelected;

        [Header("Configs")]
        [Header("View")]
        [SerializeField]
        [Tooltip(
            "Treat this as the root view. This view is added to the view history, but is not closed automatically."
        )]
        private bool rootView;

        [SerializeField]
        [Tooltip(
            "Whether this view is exclusive. If true, it will close all other views (except root views) when opened."
        )]
        private bool exclusiveView;

        [SerializeField]
        [Tooltip("Whether to track this view in the view history.")]
        private bool trackView = true;

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
        private GameObject lastSelected;
        private bool isQuitting;

        private void Awake()
        {
            Application.quitting += OnQuit;
            SentinalManager.Instance.OnSwitch += OnSwitch;
        }

        private void OnSwitch(SentinalViewSelector selector1, SentinalViewSelector selector2)
        {
            if (selector1 == this && rememberLastSelected)
                SaveLastSelection();
        }

        private void OnQuit() => isQuitting = true;

        private void OnEnable()
        {
            if (exclusiveView)
                SentinalManager.Instance.CloseAllViews();

            if (trackView)
                SentinalManager.Instance.Add(this);

            if (autoSelectOnEnable)
                Select();
        }

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

        public void SelectFirstSelected()
        {
            if (firstSelected == null)
                return;

            SetSelected(firstSelected);
        }

        public void SelectLastSelected() => SetSelected(lastSelected);

        private void SetSelected(GameObject selected)
        {
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
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && IsSelectablePartOfThis(currentSelected))
                lastSelected = currentSelected;
        }

        private void OnDisable()
        {
            if (isQuitting)
                return;

            if (rememberLastSelected)
                SaveLastSelection();

            if (trackView)
                SentinalManager.Instance.Remove(this);
        }

        /// <summary>
        /// Checks if this view is the root view.
        /// The root view does not close automatically.
        /// </summary>
        /// <returns>If this is a root view.</returns>
        public bool IsRootView() => rootView;

        /// <summary>
        /// Checks if this view is exclusive.
        /// </summary>
        /// <returns>If this is an exclusive view.</returns>
        public bool IsExclusiveView() => exclusiveView;

        /// <summary>
        /// Checks if this view prevents selection.
        /// </summary>
        /// <returns>If this view prevents selection.</returns>
        public bool HasPreventSelection() => preventSelection;

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

        private void OnDestroy()
        {
            Application.quitting -= OnQuit;
            SentinalManager.Instance.OnSwitch -= OnSwitch;
        }
    }
}
