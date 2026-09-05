using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    [DisallowMultipleComponent]
    public class TabbedView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private Toggle[] groupToggles = Array.Empty<Toggle>();

        [SerializeField]
        private ViewSelector[] groupPanels = Array.Empty<ViewSelector>();

        [Header("Configs")]
        [SerializeField]
        [Tooltip("The index of the tab to set as active by default.")]
        private int defaultTabIndex;

        [SerializeField]
        [Tooltip("Whether tab switching wraps around from last to first tab and vice versa.")]
        private bool wrapTabs = true;

        private int currentTabIndex;
        private ToggleGroup toggleGroup;
        private UnityAction<bool>[] toggleListeners = Array.Empty<UnityAction<bool>>();
        private ReadOnlyCollection<Toggle> readOnlyToggles;
        private ReadOnlyCollection<ViewSelector> readOnlyPanels;

        public IReadOnlyList<Toggle> GroupToggles => readOnlyToggles ??= Array.AsReadOnly(groupToggles);

        public IReadOnlyList<ViewSelector> GroupPanels => readOnlyPanels ??= Array.AsReadOnly(groupPanels);

        public int CurrentTabIndex => currentTabIndex;

        public int DefaultTabIndex
        {
            get => defaultTabIndex;
            set => defaultTabIndex = Mathf.Max(0, value);
        }

        public bool WrapTabs
        {
            get => wrapTabs;
            set => wrapTabs = value;
        }

        private void Awake()
        {
            TryGetComponent(out toggleGroup);
            SetupToggleGroup();
            SubscribeToToggles();

            if (groupToggles.Length > 0)
                SelectTab(Mathf.Clamp(defaultTabIndex, 0, groupToggles.Length - 1));
        }

        private void OnDestroy() => UnsubscribeFromToggles();

        public void ReplaceTabs(IReadOnlyList<Toggle> toggles, IReadOnlyList<ViewSelector> panels)
        {
            UnsubscribeFromToggles();

            groupToggles = Copy(toggles);
            groupPanels = Copy(panels);
            readOnlyToggles = null;
            readOnlyPanels = null;

            SetupToggleGroup();
            SubscribeToToggles();

            if (groupToggles.Length == 0)
            {
                currentTabIndex = 0;
                SetPanelsActive(-1);
                return;
            }

            SelectTab(Mathf.Clamp(currentTabIndex, 0, groupToggles.Length - 1));
        }

        public void Next()
        {
            if (groupToggles.Length == 0)
                return;

            int nextIndex = currentTabIndex + 1;
            if (nextIndex >= groupToggles.Length)
                nextIndex = wrapTabs ? 0 : groupToggles.Length - 1;

            SelectTab(nextIndex);
        }

        public void Previous()
        {
            if (groupToggles.Length == 0)
                return;

            int previousIndex = currentTabIndex - 1;
            if (previousIndex < 0)
                previousIndex = wrapTabs ? groupToggles.Length - 1 : 0;

            SelectTab(previousIndex);
        }

        public bool SelectTab(int index)
        {
            if (index < 0 || index >= groupToggles.Length)
                return false;

            currentTabIndex = index;
            Toggle toggle = groupToggles[index];
            if (toggle != null && !toggle.isOn)
                toggle.isOn = true;

            SetPanelsActive(index);
            return true;
        }

        private void OnTabToggle(int tabIndex, bool isOn)
        {
            if (isOn)
                SelectTab(tabIndex);
        }

        private void SubscribeToToggles()
        {
            toggleListeners = new UnityAction<bool>[groupToggles.Length];
            for (int i = 0; i < groupToggles.Length; i++)
            {
                Toggle toggle = groupToggles[i];
                if (toggle == null)
                    continue;

                int index = i;
                UnityAction<bool> listener = isOn => OnTabToggle(index, isOn);
                toggleListeners[i] = listener;
                toggle.onValueChanged.AddListener(listener);
            }
        }

        private void UnsubscribeFromToggles()
        {
            int count = Mathf.Min(groupToggles.Length, toggleListeners.Length);
            for (int i = 0; i < count; i++)
            {
                if (groupToggles[i] != null && toggleListeners[i] != null)
                    groupToggles[i].onValueChanged.RemoveListener(toggleListeners[i]);
            }

            toggleListeners = Array.Empty<UnityAction<bool>>();
        }

        private void SetupToggleGroup()
        {
            if (toggleGroup == null)
                return;

            for (int i = 0; i < groupToggles.Length; i++)
            {
                if (groupToggles[i] != null)
                    groupToggles[i].group = toggleGroup;
            }
        }

        private void SetPanelsActive(int activeIndex)
        {
            for (int i = 0; i < groupPanels.Length; i++)
            {
                if (groupPanels[i] != null)
                    groupPanels[i].gameObject.SetActive(i == activeIndex);
            }
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
                return Array.Empty<T>();

            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];

            return copy;
        }

        private void Reset()
        {
            TryGetComponent(out toggleGroup);
            SetupToggleGroup();
        }
    }
}
