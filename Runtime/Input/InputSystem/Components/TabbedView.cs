using UnityEngine;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    [DisallowMultipleComponent]
    public class TabbedView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private Toggle[] groupToggles;

        [SerializeField]
        private ViewSelector[] groupPanels;

        [Header("Configs")]
        [SerializeField]
        [Tooltip("The index of the tab to set as active by default.")]
        private int defaultTabIndex = 0;

        [SerializeField]
        [Tooltip("Whether tab switching wraps around from last to first tab and vice versa")]
        private bool wrapTabs = true;

        private int currentTabIndex = 0;
        private ToggleGroup toggleGroup;

        private void Awake()
        {
            TryGetComponent(out toggleGroup);

            if (groupToggles != null && groupToggles.Length > 0)
            {
                for (int i = 0; i < groupToggles.Length; i++)
                {
                    int index = i;
                    groupToggles[i].onValueChanged.AddListener(isOn => OnTabToggle(index, isOn));
                }

                groupToggles[defaultTabIndex % groupToggles.Length].isOn = true;
                SetActiveTab(defaultTabIndex % groupToggles.Length);
            }
        }

        private void SetupToggleGroup()
        {
            if (toggleGroup == null)
                return;

            if (groupToggles == null || groupToggles.Length == 0)
                return;

            for (int i = 0; i < groupToggles.Length; i++)
            {
                if (groupToggles[i] == null)
                    continue;

                if (groupToggles[i].group == null)
                {
                    groupToggles[i].group = toggleGroup;
                    Debug.LogWarning(
                        $"{GetType().Name}: Assigned ToggleGroup to {groupToggles[i].name}.",
                        groupToggles[i]
                    );
                }
            }
        }

        private void OnTabToggle(int tabIndex, bool isOn)
        {
            if (!isOn)
                return;

            SetActiveTab(tabIndex);
        }

        public void Next()
        {
            if (groupToggles == null || groupToggles.Length == 0)
                return;

            int nextIndex = currentTabIndex + 1;
            if (nextIndex >= groupToggles.Length)
            {
                if (wrapTabs)
                    nextIndex = 0;
                else
                    nextIndex = groupToggles.Length - 1;
            }

            groupToggles[nextIndex].isOn = true;
            SetActiveTab(nextIndex);
        }

        public void Previous()
        {
            if (groupToggles == null || groupToggles.Length == 0)
                return;

            int prevIndex = currentTabIndex - 1;
            if (prevIndex < 0)
            {
                if (wrapTabs)
                    prevIndex = groupToggles.Length - 1;
                else
                    prevIndex = 0;
            }

            groupToggles[prevIndex].isOn = true;
            SetActiveTab(prevIndex);
        }

        private void SetActiveTab(int index)
        {
            currentTabIndex = index;

            if (groupPanels != null)
            {
                for (int i = 0; i < groupPanels.Length; i++)
                {
                    if (groupPanels[i] != null)
                        groupPanels[i].gameObject.SetActive(false);
                }

                if (index < groupPanels.Length && groupPanels[index] != null)
                    groupPanels[index].gameObject.SetActive(true);
            }
        }

        private void Reset()
        {
            SetupToggleGroup();
        }
    }
}
