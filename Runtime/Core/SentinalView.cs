using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Sentinal
{
    /// <summary>Where a view is in its lifecycle. Sentinal owns this; the GameObject's active state follows it.</summary>
    public enum ViewState
    {
        Closed,
        Opening,
        Open,
        Closing,

        /// <summary>Logically open but switched off by another view on its layer (Hide until this closes).</summary>
        Hidden,
    }

    /// <summary>
    /// A menu, screen, popup or panel. A top-level view follows the rules of its layer (Project Settings › Sentinal);
    /// a view nested inside another view belongs to that view's window. Open and close it with
    /// <see cref="Open"/>/<see cref="Close"/>, never by toggling the GameObject.
    /// </summary>
    [AddComponentMenu("Sentinal/Sentinal View"), DisallowMultipleComponent]
    public sealed class SentinalView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField]
        [CreateAssetButton("Assets/Resources/Sentinal View Addresses", "Address")]
        [Tooltip("Optional address for finding this view from other prefabs via ViewAddressRegistry.")]
        private ViewAddress address;

        [Header("Layer")]
        [SerializeField]
        [Tooltip(
            "Top-level views only: the layer whose rules this view follows. Views nested inside another view belong to that view's window."
        )]
        private ViewLayerRef layer;

        [Header("Input")]
        [SerializeField, Min(0f)]
        [Tooltip(
            "Top-level views only: seconds of unscaled time to ignore input each time this view, or a view inside it, gets input back. Use it for views that can pop up while players are still pressing buttons."
        )]
        private float inputDelay;

        [Header("Selection")]
        [SerializeField]
        [Tooltip("Selected when the view gains focus. Empty = nothing is selected.")]
        private GameObject firstSelected;

        [SerializeField]
        [Tooltip("Return to the last selected element instead of First Selected when the view regains focus.")]
        private bool rememberLastSelected = true;

        public event Action Opened;

        /// <summary>Fires exactly once per open, whatever closed the view (Back, its layer, a parent, code, teardown).</summary>
        public event Action Closed;

        /// <summary>Changes whenever input is suspended or rearmed, including a new owner/authority.</summary>
        public event Action InputChanged;

        private readonly List<AwaitableCompletionSource<bool>> openWaiters = new();
        private readonly List<AwaitableCompletionSource> closeWaiters = new();
        private ViewState state;
        private SentinalView parent;
        private bool parentResolved;
        private bool seen;
        private bool changingActivation;
        private bool selectNothing;
        private bool isQuitting;
        private bool reportedDirectToggle;
        private GameObject lastSelected;
        private ViewTransition transition;
        private CancellationTokenSource transitionCancellation;
        private PlayerInput owner;
        private PlayerInput inputPlayer;
        private bool hadInput;
        private float inputReadyAt;
        private readonly HashSet<InputAction> blockedActions = new();

        public bool IsPlayerView =>
            SentinalSettings.Instance != null && SentinalSettings.Instance.IsPlayerLayer(Window.Layer);
        public PlayerInput Owner => Window.owner;
        public PlayerInput InputPlayer => IsPlayerView ? Owner : SentinalViewRouter.AuthorityPlayer;
        public int InputEpoch { get; private set; }
        public bool IsInputReady =>
            hadInput
            && IsShown
            && IsActive
            && !SentinalViewRouter.IsSuspended(this)
            && Time.unscaledTime >= inputReadyAt;

        /// <summary>Rejects held buttons/vectors until neutral, then accepts the next press after the window's input delay.</summary>
        public bool CanProcessInput(InputAction action)
        {
            RefreshInputState();
            if (!IsInputReady || action == null)
                return false;
            if (!blockedActions.Contains(action))
                return true;
            if (!IsActuated(action))
                blockedActions.Remove(action);
            return false;
        }

        public static bool IsActuated(InputAction action)
        {
            if (action == null)
                return false;
            foreach (InputControl control in action.controls)
                if (control.EvaluateMagnitude() > 0.2f)
                    return true;
            return false;
        }

        /// <summary>Absolute pointer position has no neutral position; pointer buttons still need a fresh press.</summary>
        public static bool NeedsFreshPress(InputAction action)
        {
            foreach (InputControl control in action.controls)
                if (control.device is not Pointer || control is ButtonControl)
                    return true;
            return false;
        }

        internal void SetOwner(PlayerInput player)
        {
            if (owner == player)
                return;
            owner = player;
            SentinalViewRouter.Refresh();
        }

        /// <summary>Called before map/module application, so held controls are read before actions are disabled.</summary>
        public void RefreshInputState()
        {
            PlayerInput nextPlayer = InputPlayer;
            bool available =
                IsShown
                && IsActive
                && !SentinalViewRouter.IsSuspended(this)
                && SentinalViewRouter.IsFocusWithin(this)
                && (transition == null || !transition.InputWaitsForTransition || state == ViewState.Open);
            if (available == hadInput && inputPlayer == nextPlayer)
                return;
            hadInput = available;
            inputPlayer = nextPlayer;
            blockedActions.Clear();
            InputEpoch++;
            if (available)
            {
                inputReadyAt = Time.unscaledTime + Window.inputDelay;
                SentinalSettings settings = SentinalSettings.Instance;
                InputActionMap map =
                    settings != null && nextPlayer != null ? settings.UIActionMap.Resolve(nextPlayer.actions) : null;
                if (map != null)
                    foreach (InputAction action in map.actions)
                        if (NeedsFreshPress(action) && IsActuated(action))
                            blockedActions.Add(action);
            }
            InputChanged?.Invoke();
        }

        public ViewState State => state;

        /// <summary>Opening, open or hidden: the view counts as open even while its layer hides it.</summary>
        public bool IsOpen => state is ViewState.Opening or ViewState.Open or ViewState.Hidden;

        /// <summary>Opening or open: on screen and able to take focus.</summary>
        public bool IsShown => state is ViewState.Opening or ViewState.Open;

        public bool IsActive => gameObject.activeInHierarchy;

        /// <summary>True when closing now would play an out-animation (nested views stay visible inside it).</summary>
        internal bool WillAnimateClose => state != ViewState.Hidden && transition != null && isActiveAndEnabled;

        /// <summary>The nearest parent view in the hierarchy, or null for a top-level view.</summary>
        public SentinalView Parent
        {
            get
            {
                if (!parentResolved)
                {
                    parent =
                        transform.parent != null ? transform.parent.GetComponentInParent<SentinalView>(true) : null;
                    parentResolved = true;
                }

                return parent;
            }
        }

        /// <summary>The top-level view this view belongs to (itself when top-level).</summary>
        public SentinalView Window
        {
            get
            {
                SentinalView window = this;
                while (window.Parent != null)
                    window = window.Parent;

                return window;
            }
        }

        public bool IsTopLevel => Parent == null;

        public ViewLayerRef Layer => layer;

        /// <summary>Seconds of unscaled time to ignore input whenever this view regains it. Nested views use their window's.</summary>
        public float InputDelay
        {
            get => inputDelay;
            set => inputDelay = Mathf.Max(0f, value);
        }

        /// <summary>True for a panel managed by a TabbedView: Back passes through it to its window.</summary>
        public bool IsTab { get; internal set; }

        public ViewAddress Address
        {
            get => address;
            set
            {
                if (address == value)
                    return;

                if (isActiveAndEnabled && address != null)
                    ViewAddressRegistry.Unregister(address, this);

                address = value;

                if (isActiveAndEnabled && address != null)
                    ViewAddressRegistry.Register(address, this);
            }
        }

        public GameObject FirstSelected
        {
            get => firstSelected;
            set
            {
                firstSelected = value;
                if (IsCurrent() && (!rememberLastSelected || lastSelected == null))
                    Select();
            }
        }

        public GameObject LastSelected => lastSelected;

        public bool RememberLastSelected
        {
            get => rememberLastSelected;
            set => rememberLastSelected = value;
        }

        /// <summary>Keeps the view focused (Back and input actions still work) while nothing is selected.</summary>
        public bool SelectNothing
        {
            get => selectNothing;
            set
            {
                if (selectNothing == value)
                    return;

                selectNothing = value;
                if (IsCurrent())
                    Select();
            }
        }

        /// <summary>Requests the view to open. Returns false when its layer refuses it or it is misconfigured.</summary>
        public bool Open() => SentinalViewRouter.Open(this);

        /// <summary>Opens or transfers a Player-layer view to an explicit local input owner.</summary>
        public bool Open(PlayerInput owner) => SentinalViewRouter.Open(this, owner);

        /// <summary>Requests the view to close. Closing continues if already closing.</summary>
        public void Close() => SentinalViewRouter.Close(this);

        /// <summary>Opens the view and completes once it is fully open. Cancelled if closed before then.</summary>
        public async Awaitable<bool> OpenAsync()
        {
            if (!Open())
                return false;

            if (state != ViewState.Opening)
                return true;

            var completion = new AwaitableCompletionSource<bool>();
            openWaiters.Add(completion);
            return await completion.Awaitable;
        }

        public async Awaitable<bool> OpenAsync(PlayerInput owner)
        {
            if (!Open(owner))
                return false;
            if (state != ViewState.Opening)
                return true;
            var completion = new AwaitableCompletionSource<bool>();
            openWaiters.Add(completion);
            return await completion.Awaitable;
        }

        /// <summary>Closes the view and completes once its close animation finishes. Cancelled if reopened first.</summary>
        public async Awaitable CloseAsync()
        {
            Close();
            if (state != ViewState.Closing)
                return;

            var completion = new AwaitableCompletionSource();
            closeWaiters.Add(completion);
            await completion.Awaitable;
        }

        /// <summary>Opens the view if needed and makes it the newest on its layer, so it takes focus unless a higher layer is open.</summary>
        public void BringToFront() => SentinalViewRouter.BringToFront(this);

        /// <summary>Checks if this view is the focused view.</summary>
        public bool IsCurrent() => SentinalViewRouter.IsCurrent(this);

        public void SetFirstSelected(GameObject target) => FirstSelected = target;

        /// <summary>Updates the selection target, optionally keeping the current EventSystem selection.</summary>
        public void SetFirstSelected(GameObject target, bool selectImmediately)
        {
            if (selectImmediately)
                SetFirstSelected(target);
            else
                firstSelected = target;
        }

        /// <summary>Applies this view's selection: the remembered element, First Selected, or nothing.</summary>
        public void Select()
        {
            if (IsPlayerView)
                return;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            GameObject target =
                rememberLastSelected && lastSelected != null && lastSelected.activeInHierarchy
                    ? lastSelected
                    : firstSelected;

            if (selectNothing || target == null)
            {
                eventSystem.SetSelectedGameObject(null);
                return;
            }

            if (target.activeInHierarchy && eventSystem.currentSelectedGameObject != target)
                eventSystem.SetSelectedGameObject(target);
        }

        internal void SaveLastSelection()
        {
            if (IsPlayerView)
                return;
            if (!rememberLastSelected || EventSystem.current == null)
                return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                lastSelected = selected;
        }

        internal void MarkSeen() => seen = true;

        internal void BeginOpen()
        {
            bool wasClosed = state == ViewState.Closed;
            seen = true;
            CancelTransition();
            CancelCloseWaiters();
            state = ViewState.Opening;
            if (wasClosed)
                Opened?.Invoke();

            SetActiveFromSentinal(true);

            if (transition == null || !isActiveAndEnabled)
            {
                if (transition != null)
                    transition.Snap(true);

                FinishOpen();
                return;
            }

            if (wasClosed)
                transition.Snap(false);

            RunTransition(show: true, FinishOpen);
        }

        internal void BeginClose()
        {
            CancelTransition();
            CancelOpenWaiters();

            if (state == ViewState.Hidden || transition == null || !isActiveAndEnabled)
            {
                state = ViewState.Closing;
                FinishClose();
                return;
            }

            state = ViewState.Closing;
            RunTransition(show: false, FinishClose);
        }

        /// <summary>Closes a nested view together with its closing parent. It stays visible inside the parent's out-animation.</summary>
        internal void CloseWithParent(bool parentAnimating)
        {
            CancelTransition();
            CancelOpenWaiters();
            state = ViewState.Closed;
            if (!parentAnimating)
                SetActiveFromSentinal(false);

            NotifyClosed();
        }

        internal void Hide()
        {
            CancelTransition();
            CompleteOpenWaiters();
            state = ViewState.Hidden;

            if (transition != null && transition.AnimateWhenHidden && isActiveAndEnabled)
                RunTransition(show: false, () => SetActiveFromSentinal(false));
            else
                SetActiveFromSentinal(false);
        }

        internal void Unhide()
        {
            CancelTransition();
            state = ViewState.Open;
            SetActiveFromSentinal(true);
            if (transition == null)
                return;

            if (transition.AnimateWhenHidden && isActiveAndEnabled)
                RunTransition(show: true, null);
            else
                transition.Snap(true);
        }

        /// <summary>Switches off a closed view whose GameObject is still on (e.g. authored active).</summary>
        internal void EnsureInactive()
        {
            if (state == ViewState.Closed && gameObject.activeSelf)
                SetActiveFromSentinal(false);
        }

        private void FinishOpen()
        {
            if (state != ViewState.Opening)
                return;

            state = ViewState.Open;
            CompleteOpenWaiters();
            SentinalViewRouter.NotifyHierarchyChanged();
        }

        private void FinishClose()
        {
            if (state != ViewState.Closing)
                return;

            state = ViewState.Closed;
            SetActiveFromSentinal(false);
            foreach (SentinalView child in GetComponentsInChildren<SentinalView>(true))
                if (child != this)
                    child.EnsureInactive();

            NotifyClosed();
        }

        private void NotifyClosed()
        {
            SentinalViewRouter.NotifyClosed(this);
            foreach (AwaitableCompletionSource waiter in closeWaiters.ToArray())
                waiter.TrySetResult();

            closeWaiters.Clear();
            Closed?.Invoke();
        }

        private async void RunTransition(bool show, Action onFinished)
        {
            transitionCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCancellation.Token;
            try
            {
                await transition.PlayAsync(show, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!token.IsCancellationRequested)
                onFinished?.Invoke();
        }

        private void CancelTransition()
        {
            if (transitionCancellation == null)
                return;

            transitionCancellation.Cancel();
            transitionCancellation.Dispose();
            transitionCancellation = null;
        }

        private void CompleteOpenWaiters()
        {
            foreach (AwaitableCompletionSource<bool> waiter in openWaiters.ToArray())
                waiter.TrySetResult(true);

            openWaiters.Clear();
        }

        private void CancelOpenWaiters()
        {
            foreach (AwaitableCompletionSource<bool> waiter in openWaiters.ToArray())
                waiter.TrySetCanceled();

            openWaiters.Clear();
        }

        private void CancelCloseWaiters()
        {
            foreach (AwaitableCompletionSource waiter in closeWaiters.ToArray())
                waiter.TrySetCanceled();

            closeWaiters.Clear();
        }

        private void SetActiveFromSentinal(bool active)
        {
            if (gameObject.activeSelf == active)
                return;

            changingActivation = true;
            try
            {
                gameObject.SetActive(active);
            }
            finally
            {
                changingActivation = false;
            }
        }

        private void Awake() => TryGetComponent(out transition);

        private void OnEnable()
        {
            if (address != null)
                ViewAddressRegistry.Register(address, this);

            if (changingActivation)
                return;

            if (state != ViewState.Closed)
            {
                // A parent was switched back on; this view's state is unchanged.
                SentinalViewRouter.NotifyHierarchyChanged();
                return;
            }

            if (seen)
                ReportDirectToggle("switched on");

            // A view that is active when it first loads starts open.
            seen = true;
            SentinalViewRouter.Open(this);
        }

        private void OnDisable()
        {
            if (address != null)
                ViewAddressRegistry.Unregister(address, this);

            if (changingActivation || isQuitting)
                return;

            if (gameObject.activeSelf || state == ViewState.Closed)
            {
                // A parent was switched off (or the view is being destroyed); focus skips inactive views.
                SentinalViewRouter.NotifyHierarchyChanged();
                return;
            }

            ReportDirectToggle("switched off");
            SentinalViewRouter.Close(this);
        }

        private void ReportDirectToggle(string change)
        {
            if (reportedDirectToggle)
                return;

            reportedDirectToggle = true;
            Debug.LogWarning(
                $"[Sentinal] '{name}' was {change} directly. Expected {nameof(SentinalView)}.{nameof(Open)}()/{nameof(Close)}(); Sentinal treated it as a request.",
                this
            );
        }

        private void OnTransformParentChanged() => parentResolved = false;

        private void OnApplicationQuit() => isQuitting = true;

        private void OnDestroy()
        {
            CancelTransition();
            if (IsOpen || state == ViewState.Closing)
            {
                state = ViewState.Closed;
                NotifyClosed();
            }

            SentinalViewRouter.Forget(this);
        }
    }
}
