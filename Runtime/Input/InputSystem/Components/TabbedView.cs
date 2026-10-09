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
        [Tooltip("The tracked view owning these panels. Focus ownership is independent of Transform parenting.")]
        private ViewSelector focusOwner;

        [SerializeField]
        private Toggle[] groupToggles = Array.Empty<Toggle>();

        [SerializeField]
        private ViewSelector[] groupPanels = Array.Empty<ViewSelector>();

        [Header("Configs")]
        [SerializeField]
        [Tooltip("The index of the tab to set as active by default.")]
        private int defaultTabIndex;

        private int currentTabIndex;
        private ToggleGroup toggleGroup;
        private UnityAction<bool>[] toggleListeners = Array.Empty<UnityAction<bool>>();
        private ReadOnlyCollection<Toggle> readOnlyToggles;
        private ReadOnlyCollection<ViewSelector> readOnlyPanels;

        public event Action TabsChanged;

        public IReadOnlyList<Toggle> GroupToggles => readOnlyToggles ??= Array.AsReadOnly(groupToggles);

        public IReadOnlyList<ViewSelector> GroupPanels => readOnlyPanels ??= Array.AsReadOnly(groupPanels);

        public int CurrentTabIndex => currentTabIndex;

        public ViewSelector FocusOwner
        {
            get => focusOwner;
            set
            {
                if (focusOwner == value)
                    return;
                ValidatePanels(value, groupToggles, groupPanels);
                ReleasePanels();
                focusOwner = value;
                BindPanels();
            }
        }

        public int DefaultTabIndex
        {
            get => defaultTabIndex;
            set => defaultTabIndex = Mathf.Max(0, value);
        }

        private void Awake()
        {
            ValidatePanels(focusOwner, groupToggles, groupPanels);
            BindPanels();
            TryGetComponent(out toggleGroup);
            SetupToggleGroup();
            SubscribeToToggles();

            if (groupToggles.Length > 0)
                SelectTab(Mathf.Clamp(defaultTabIndex, 0, groupToggles.Length - 1));
        }

        private void OnEnable()
        {
            if (groupToggles.Length > 0)
                SetPanelsActive(currentTabIndex);
        }

        private void OnDisable() => SetPanelsActive(-1);

        private void OnValidate()
        {
            try
            {
                ValidatePanels(focusOwner, groupToggles, groupPanels);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message, this);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromToggles();
            SetPanelsActive(-1);
            ReleasePanels();
        }

        public void ReplaceTabs(IReadOnlyList<Toggle> toggles, IReadOnlyList<ViewSelector> panels)
        {
            Toggle[] replacementToggles = Copy(toggles);
            ViewSelector[] replacementPanels = Copy(panels);
            ValidatePanels(focusOwner, replacementToggles, replacementPanels);

            UnsubscribeFromToggles();
            SetPanelsActive(-1);
            ReleasePanels();

            groupToggles = replacementToggles;
            groupPanels = replacementPanels;
            readOnlyToggles = null;
            readOnlyPanels = null;

            BindPanels();

            SetupToggleGroup();
            SubscribeToToggles();

            if (groupToggles.Length == 0)
            {
                currentTabIndex = 0;
                SetPanelsActive(-1);
                TabsChanged?.Invoke();
                return;
            }

            SelectTab(Mathf.Clamp(currentTabIndex, 0, groupToggles.Length - 1));
            TabsChanged?.Invoke();
        }

        private void BindPanels()
        {
            foreach (ViewSelector panel in groupPanels)
                panel.FocusParent = focusOwner;
        }

        private void ReleasePanels()
        {
            foreach (ViewSelector panel in groupPanels)
                if (panel != null && panel.FocusParent == focusOwner)
                    panel.FocusParent = null;
        }

        private void ValidatePanels(ViewSelector owner, Toggle[] toggles, ViewSelector[] panels)
        {
            if (panels.Length == 0)
                return;
            if (owner == null || !owner.TrackView)
                throw new InvalidOperationException(
                    $"{name} expected a tracked Focus Owner for {panels.Length} panels; actual: {(owner != null ? owner.name + " is untracked" : "missing owner")}."
                );
            if (panels.Length != toggles.Length)
                throw new InvalidOperationException(
                    $"{name} expected one toggle per panel; actual: {toggles.Length} toggles, {panels.Length} panels."
                );

            var seen = new HashSet<ViewSelector>();
            for (int i = 0; i < panels.Length; i++)
            {
                ViewSelector panel = panels[i];
                if (panel == null || toggles[i] == null || !panel.TrackView || panel == owner || !seen.Add(panel))
                    throw new InvalidOperationException(
                        $"{name} expected a unique tracked panel and toggle at index {i}, distinct from owner '{owner.name}'; actual: panel={(panel != null ? panel.name : "missing")}, toggle={(toggles[i] != null ? toggles[i].name : "missing")}."
                    );
                if (panel.FocusParent != null && panel.FocusParent != owner && panel.FocusParent != focusOwner)
                    throw new InvalidOperationException(
                        $"{name} expected panel '{panel.name}' to belong to '{owner.name}'; actual: already owned by '{panel.FocusParent.name}'."
                    );
                for (ViewSelector parent = owner; parent != null; parent = parent.FocusParent)
                    if (parent == panel)
                        throw new InvalidOperationException(
                            $"{name} expected panel '{panel.name}' outside the owner chain of '{owner.name}'; actual: ownership would form a cycle."
                        );
            }
        }

        public void Next(bool wrap)
        {
            if (groupToggles.Length == 0)
                return;

            int nextIndex = currentTabIndex + 1;
            if (nextIndex >= groupToggles.Length)
                nextIndex = wrap ? 0 : groupToggles.Length - 1;

            SelectTab(nextIndex);
        }

        public void Previous(bool wrap)
        {
            if (groupToggles.Length == 0)
                return;

            int previousIndex = currentTabIndex - 1;
            if (previousIndex < 0)
                previousIndex = wrap ? groupToggles.Length - 1 : 0;

            SelectTab(previousIndex);
        }

        /// <summary>Selects an authored panel. Can be wired directly to a Button's onClick event.</summary>
        public void SelectTab(ViewSelector panel)
        {
            if (panel == null)
                throw new ArgumentNullException(
                    nameof(panel),
                    $"{name} expected an authored tab panel; actual: missing panel."
                );

            int index = Array.IndexOf(groupPanels, panel);
            if (index < 0)
                throw new ArgumentException(
                    $"{name} expected panel '{panel.name}' in its {groupPanels.Length} registered panels; actual: panel is not registered.",
                    nameof(panel)
                );

            if (!SelectTab(index))
                throw new InvalidOperationException(
                    $"{name} expected a toggle for panel '{panel.name}' at index {index}; actual: selection rejected with {groupToggles.Length} toggles."
                );
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
            if (SentinalViewRouter.IsFocusWithin(focusOwner))
                SentinalViewRouter.TrySelectCurrentView();
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
                if (i != activeIndex && groupPanels[i] != null)
                    groupPanels[i].gameObject.SetActive(false);
            }

            if (activeIndex >= 0 && activeIndex < groupPanels.Length && groupPanels[activeIndex] != null)
                groupPanels[activeIndex].gameObject.SetActive(true);
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
