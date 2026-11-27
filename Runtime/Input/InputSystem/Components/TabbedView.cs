#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinal.InputSystem.Components
{
    /// <summary>
    /// Switches between tab panels inside a view. Panels are views nested under the view that owns this component
    /// (the nearest <see cref="SentinalView"/> at or above it); opening one tab closes the others, and Back on a
    /// tab passes through to the owner. The next/previous tab actions are its owner's player's, and work while the
    /// owner is focused (focus on it or one of its tabs; a focused popup inside it stops them).
    /// A tab whose toggle GameObject is inactive is unavailable: it is skipped by the tab actions and the default tab,
    /// and cannot be selected. Use <see cref="SetTabAvailable(SentinalView, bool)"/> to hide one at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class TabbedView : ViewInputComponent
    {
        [Header("Components")]
        [SerializeField]
        private Toggle[] groupToggles = Array.Empty<Toggle>();

        [SerializeField]
        [Tooltip("One panel per toggle. Each must be a view nested directly under this component's view.")]
        private SentinalView[] groupPanels = Array.Empty<SentinalView>();

        [Header("Configs")]
        [SerializeField]
        [Tooltip("The tab shown the first time the view opens.")]
        private int defaultTabIndex;

        [SerializeField]
        [Tooltip("Reopening the view returns to the tab it was on. Off: it starts on the default tab every time.")]
        private bool rememberLastTab = true;

        [Header("Tab Actions")]
        [SerializeField]
        [Tooltip(
            "Optional. Moves to the next tab when pressed (positive value). Resolved on the player's own action asset; absent there = off."
        )]
        private InputActionReference nextTabAction;

        [SerializeField]
        [Tooltip(
            "Optional. Moves to the previous tab when pressed (positive value). When it is the same 1D axis action as Next Tab, its negative half moves to the previous tab instead."
        )]
        private InputActionReference previousTabAction;

        [SerializeField]
        [Tooltip("Tab actions wrap from the last tab to the first and vice versa.")]
        private bool wrapTabs = true;

        private int currentTabIndex;
        private SentinalView owner;
        private InputAction nextAction;
        private InputAction previousAction;
        private ToggleGroup toggleGroup;
        private UnityAction<bool>[] toggleListeners = Array.Empty<UnityAction<bool>>();
        private ReadOnlyCollection<Toggle> readOnlyToggles;
        private ReadOnlyCollection<SentinalView> readOnlyPanels;

        public IReadOnlyList<Toggle> GroupToggles => readOnlyToggles ??= Array.AsReadOnly(groupToggles);

        public IReadOnlyList<SentinalView> GroupPanels => readOnlyPanels ??= Array.AsReadOnly(groupPanels);

        public int CurrentTabIndex => currentTabIndex;

        public SentinalView Owner => owner;

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
            owner = ParentView;
            ValidatePanels();
            foreach (SentinalView panel in groupPanels)
                panel.IsTab = true;

            TryGetComponent(out toggleGroup);
            SetupToggleGroup();
            SubscribeToToggles();
            currentTabIndex = ClampedDefault();
            owner.Opened += OnOwnerOpened;
        }

        protected override void OnEnable()
        {
            if (groupToggles.Length > 0)
            {
                if (!IsTabAvailable(currentTabIndex))
                    currentTabIndex = ClampedDefault();
                SelectTab(currentTabIndex);
            }

            base.OnEnable();
        }

        protected override void Subscribe(PlayerInput player)
        {
            nextAction = Resolve(nextTabAction, player);
            previousAction = Resolve(previousTabAction, player);
            if (nextAction != null)
                nextAction.performed += OnNextPerformed;
            if (previousAction != null && previousAction != nextAction)
                previousAction.performed += OnPreviousPerformed;
        }

        protected override void Unsubscribe()
        {
            if (nextAction != null)
                nextAction.performed -= OnNextPerformed;
            if (previousAction != null)
                previousAction.performed -= OnPreviousPerformed;

            nextAction = null;
            previousAction = null;
        }

        private static InputAction Resolve(InputActionReference reference, PlayerInput player) =>
            reference != null && reference.action != null ? player.actions.FindAction(reference.action.id) : null;

        private void OnNextPerformed(InputAction.CallbackContext context)
        {
            if (!CanProcessInput(context))
                return;

            float value = context.ReadValue<float>();
            if (value > 0f)
                Next(wrapTabs);
            else if (value < 0f && previousAction == nextAction)
                Previous(wrapTabs);
        }

        private void OnPreviousPerformed(InputAction.CallbackContext context)
        {
            if (CanProcessInput(context) && context.ReadValue<float>() > 0f)
                Previous(wrapTabs);
        }

        private void OnValidate()
        {
            SentinalView validationOwner = GetComponentInParent<SentinalView>(true);
            if (validationOwner == null || groupPanels.Length == 0)
                return;

            owner = validationOwner;
            try
            {
                ValidatePanels();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message, this);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromToggles();
            if (owner != null)
                owner.Opened -= OnOwnerOpened;
        }

        private void OnOwnerOpened()
        {
            if (!rememberLastTab)
                currentTabIndex = ClampedDefault();
        }

        /// <summary>The default tab, or the first available tab after it when the default is unavailable.</summary>
        private int ClampedDefault()
        {
            if (groupToggles.Length == 0)
                return 0;

            int start = Mathf.Clamp(defaultTabIndex, 0, groupToggles.Length - 1);
            int available = FindAvailable(start, 1, wrap: true);
            return available >= 0 ? available : start;
        }

        /// <summary>Whether a tab can be selected: its toggle GameObject is active.</summary>
        public bool IsTabAvailable(int index) =>
            index >= 0
            && index < groupToggles.Length
            && groupToggles[index] != null
            && groupToggles[index].gameObject.activeSelf;

        /// <summary>
        /// Shows or hides a tab's toggle. A hidden tab is skipped by the tab actions; when it is the current tab, the
        /// next available tab opens instead. Safe to call before or after this component's Awake.
        /// </summary>
        public void SetTabAvailable(SentinalView panel, bool available)
        {
            int index = IndexOfPanel(panel);
            groupToggles[index].gameObject.SetActive(available);
            if (available || index != currentTabIndex)
                return;

            int replacement = FindAvailable(index, 1, wrap: true);
            if (replacement < 0)
            {
                groupPanels[index].Close();
                return;
            }

            if (isActiveAndEnabled)
                SelectTab(replacement);
            else
                currentTabIndex = replacement;
        }

        /// <summary>First available index from start (inclusive) stepping by direction, or -1 when none.</summary>
        private int FindAvailable(int start, int direction, bool wrap)
        {
            int count = groupToggles.Length;
            for (int step = 0; step < count; step++)
            {
                int index = start + step * direction;
                if (index < 0 || index >= count)
                {
                    if (!wrap)
                        return -1;
                    index = (index % count + count) % count;
                }

                if (IsTabAvailable(index))
                    return index;
            }

            return -1;
        }

        private void ValidatePanels()
        {
            if (owner == null)
                throw new InvalidOperationException(
                    $"{name} expected a {nameof(SentinalView)} on or above it to own its tabs; actual: none."
                );

            if (groupPanels.Length != groupToggles.Length)
                throw new InvalidOperationException(
                    $"{name} expected one toggle per panel; actual: {groupToggles.Length} toggles, {groupPanels.Length} panels."
                );

            var seen = new HashSet<SentinalView>();
            for (int i = 0; i < groupPanels.Length; i++)
            {
                SentinalView panel = groupPanels[i];
                if (panel == null || groupToggles[i] == null || !seen.Add(panel))
                    throw new InvalidOperationException(
                        $"{name} expected a unique panel and toggle at index {i}; actual: panel={(panel != null ? panel.name : "missing")}, toggle={(groupToggles[i] != null ? groupToggles[i].name : "missing")}."
                    );

                if (panel.Parent != owner)
                    throw new InvalidOperationException(
                        $"{name} expected tab panel '{panel.name}' nested under '{owner.name}' in the hierarchy; actual: its parent view is {(panel.Parent != null ? $"'{panel.Parent.name}'" : "none (top-level)")}."
                    );
            }
        }

        /// <summary>Selects the next available tab. Without wrap, stays put at the last available tab.</summary>
        public void Next(bool wrap)
        {
            int nextIndex = FindAvailable(currentTabIndex + 1, 1, wrap);
            if (nextIndex >= 0)
                SelectTab(nextIndex);
        }

        /// <summary>Selects the previous available tab. Without wrap, stays put at the first available tab.</summary>
        public void Previous(bool wrap)
        {
            int previousIndex = FindAvailable(currentTabIndex - 1, -1, wrap);
            if (previousIndex >= 0)
                SelectTab(previousIndex);
        }

        /// <summary>Selects an authored panel. Can be wired directly to a Button's onClick event.</summary>
        public void SelectTab(SentinalView panel)
        {
            SelectTab(IndexOfPanel(panel));
        }

        /// <summary>Selects an available tab by index. Returns false for an out-of-range or unavailable tab.</summary>
        public bool SelectTab(int index)
        {
            if (!IsTabAvailable(index))
                return false;

            currentTabIndex = index;
            Toggle toggle = groupToggles[index];
            if (toggle != null && !toggle.isOn)
                toggle.isOn = true;

            for (int i = 0; i < groupPanels.Length; i++)
                if (i != index)
                    groupPanels[i].Close();

            groupPanels[index].Open();
            return true;
        }

        private int IndexOfPanel(SentinalView panel)
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

            return index;
        }

        private void OnTabToggle(int tabIndex, bool isOn)
        {
            if (isOn && tabIndex != currentTabIndex)
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

        private void Reset()
        {
            TryGetComponent(out toggleGroup);
            SetupToggleGroup();
        }
    }
}
#endif
