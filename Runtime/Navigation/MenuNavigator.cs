using UnityEngine;
using UnityEngine.EventSystems;

namespace MenuNavigation
{
    [AddComponentMenu("Menu Navigation/Menu Navigator")]
    public class MenuNavigator : MonoBehaviour, INavigableMenu
    {
        public bool IsOpen => gameObject.activeInHierarchy;
        public GameObject FirstSelected => firstSelected;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("The first selected GameObject when active.")]
        private GameObject firstSelected;

        [SerializeField]
        [Tooltip("Auto-select saved GameObject on enable.")]
        private bool autoSelectOnEnable = true;

        [SerializeField]
        [Tooltip("Whether to remember the last selected GameObject when disabled.")]
        private bool rememberLastSelected = true;
        private GameObject lastSelected;
        private bool isQuitting;

        private void Awake()
        {
            Application.quitting += OnQuitting;
        }

        private void OnQuitting() => isQuitting = true;

        private void OnEnable()
        {
            if (autoSelectOnEnable)
                Select();

            MenuNavigatorManager.Instance.RegisterMenuOpen(this);
        }

        private void OnDisable()
        {
            if (isQuitting) // avoids console errors when exiting play
                return;

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && IsSelectablePartOfThisMenu(currentSelected))
                lastSelected = currentSelected;

            MenuNavigatorManager.Instance.RegisterMenuClose(this);
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

        private bool IsSelectablePartOfThisMenu(GameObject selectable)
        {
            if (selectable == null || !rememberLastSelected)
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
            Application.quitting -= OnQuitting;
        }
    }
}
