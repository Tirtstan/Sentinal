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
        [SerializeField]
        [Tooltip("Whether to track this view in the view history.")]
        private bool trackView = true;

        [SerializeField]
        [Tooltip("Auto-select on enable.")]
        private bool autoSelectOnEnable = true;

        [SerializeField]
        [Tooltip("Whether to remember the last selected GameObject.")]
        private bool rememberLastSelected = true;
        private GameObject lastSelected;
        private bool isQuitting;

        private void Awake()
        {
            Application.quitting += OnQuit;
        }

        private void OnQuit() => isQuitting = true;

        private void OnEnable()
        {
            if (trackView)
                Sentinal.Instance.Push(this);

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
            {
                Debug.LogWarning($"FirstSelected is not set for {gameObject.name}!");
                return;
            }

            SetSelected(firstSelected);
        }

        public void SelectLastSelected() => SetSelected(lastSelected);

        private void SetSelected(GameObject selected)
        {
            if (selected == null || !selected.activeInHierarchy)
                return;

            EventSystem.current.SetSelectedGameObject(selected);
        }

        private void SaveCurrentSelection()
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
                SaveCurrentSelection();

            if (trackView)
                Sentinal.Instance.Pop(this);
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

        private void OnDestroy()
        {
            Application.quitting -= OnQuit;
        }
    }
}
