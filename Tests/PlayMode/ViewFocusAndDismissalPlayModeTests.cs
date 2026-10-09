using System.Collections;
using NUnit.Framework;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class ViewFocusAndDismissalPlayModeTests
    {
        private GameObject menuObject;
        private GameObject overlayObject;
        private GameObject eventSystemObject;
        private GameObject playerObject;
        private GameObject externalPanelObject;
        private InputActionAsset actions;

        [TearDown]
        public void TearDown()
        {
            if (overlayObject != null)
                Object.DestroyImmediate(overlayObject);
            if (menuObject != null)
                Object.DestroyImmediate(menuObject);
            if (eventSystemObject != null)
                Object.DestroyImmediate(eventSystemObject);
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
            if (actions != null)
                Object.DestroyImmediate(actions);
            if (externalPanelObject != null)
                Object.DestroyImmediate(externalPanelObject);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TabKeepsFocusAndInputRegardlessOfParentRegistrationOrder(bool childFirst)
        {
            menuObject = new GameObject("Menu");
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            GameObject tabObject = new GameObject("Tab");
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();
            tabView.FocusParent = menuView;
            ViewInputSystemHandler tabInput = tabObject.AddComponent<ViewInputSystemHandler>();
            tabInput.ViewSelector = tabView;

            for (int opening = 0; opening < 3; opening++)
            {
                SentinalViewRouter.Remove(tabView);
                SentinalViewRouter.Remove(menuView);
                SentinalViewRouter.Add(childFirst ? tabView : menuView);
                SentinalViewRouter.Add(childFirst ? menuView : tabView);

                Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
                Assert.That(tabInput.IsInputEnabled(), Is.True);

                overlayObject = new GameObject("Modal");
                ViewSelector overlayView = overlayObject.AddComponent<ViewSelector>();
                Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(overlayView));
                Assert.That(tabInput.IsInputEnabled(), Is.False);
                Object.DestroyImmediate(overlayObject);
                Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
                Assert.That(tabInput.IsInputEnabled(), Is.True);
            }

            menuView.Priority = 20;
            Assert.That(SentinalViewRouter.CurrentWindow, Is.EqualTo(menuView));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
        }

        [TestCase(ActionMapGate.RestoreTiming.OnDisable)]
        [TestCase(ActionMapGate.RestoreTiming.OnFocusLost)]
        public void ParentGateEnablesUiForChildFocusAndRestoresAfterModalAndWindowClose(
            ActionMapGate.RestoreTiming timing
        )
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            actions.AddActionMap("Player").AddAction("Move");
            actions.AddActionMap("UI").AddAction("Confirm");
            playerObject = new GameObject("Player Input");
            playerObject.SetActive(false);
            PlayerInput player = playerObject.AddComponent<PlayerInput>();
            player.actions = actions;
            player.defaultActionMap = "Player";
            playerObject.SetActive(true);

            menuObject = new GameObject("Menu");
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            GameObject tabObject = new GameObject("Tab");
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();
            tabView.FocusParent = menuView;
            SentinalViewRouter.Remove(menuView);

            ActionMapGate gate = menuObject.AddComponent<ActionMapGate>();
            gate.RestorePreviousActionMapState = true;
            gate.RestoreWhen = timing;
            gate.Mode = ActionMapGate.GateMode.Exclusive;
            SentinalViewRouter.Add(menuView);

            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(gate.IsApplied, Is.True);
            Assert.That(player.currentActionMap.name, Is.EqualTo("UI"));
            Assert.That(player.actions.FindActionMap("Player").enabled, Is.False);

            overlayObject = new GameObject("Modal");
            overlayObject.SetActive(false);
            overlayObject.transform.SetParent(menuObject.transform);
            ViewSelector overlayView = overlayObject.AddComponent<ViewSelector>();
            overlayView.Priority = 20;
            ActionMapGate modalGate = overlayObject.AddComponent<ActionMapGate>();
            modalGate.RestorePreviousActionMapState = true;
            modalGate.Mode = ActionMapGate.GateMode.Exclusive;
            modalGate.ExclusiveMapName = "Player";
            overlayObject.SetActive(true);

            Assert.That(gate.IsApplied, Is.False);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));
            overlayObject.SetActive(false);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(gate.IsApplied, Is.True);
            Assert.That(player.currentActionMap.name, Is.EqualTo("UI"));

            menuObject.SetActive(false);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));
            Assert.That(player.actions.FindActionMap("UI").enabled, Is.False);
        }

        [Test]
        public void ExplicitOwnershipWorksOutsideTransformTreeAndKeepsPanelPriorityLocal()
        {
            menuObject = new GameObject("Window");
            ViewSelector window = menuObject.AddComponent<ViewSelector>();
            ViewInputSystemHandler sharedInput = menuObject.AddComponent<ViewInputSystemHandler>();
            sharedInput.ViewSelector = window;
            sharedInput.InputMode = HandlerInputMode.FocusWithin;

            externalPanelObject = new GameObject("Panel outside window hierarchy");
            ViewSelector panel = externalPanelObject.AddComponent<ViewSelector>();
            panel.FocusParent = window;
            panel.Priority = 100;
            ViewInputSystemHandler panelInput = externalPanelObject.AddComponent<ViewInputSystemHandler>();
            panelInput.ViewSelector = panel;

            GameObject detailObject = new GameObject("Owned detail");
            detailObject.transform.SetParent(externalPanelObject.transform);
            ViewSelector detail = detailObject.AddComponent<ViewSelector>();
            detail.FocusParent = panel;
            Assert.That(SentinalViewRouter.CurrentWindow, Is.EqualTo(window));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(detail));
            Assert.That(sharedInput.IsInputEnabled(), Is.True);
            Assert.That(panelInput.IsInputEnabled(), Is.False);
            Assert.That(SentinalViewRouter.IsFocusWithin(panel), Is.True);

            overlayObject = new GameObject("Independent modal inside window hierarchy");
            overlayObject.transform.SetParent(menuObject.transform);
            ViewSelector modal = overlayObject.AddComponent<ViewSelector>();
            modal.Priority = 1;
            Assert.That(SentinalViewRouter.CurrentWindow, Is.EqualTo(modal));
            Assert.That(sharedInput.IsInputEnabled(), Is.False);
            Assert.That(SentinalViewRouter.IsFocusWithin(window), Is.False);

            SentinalViewRouter.CloseCurrentView();
            Assert.That(menuObject.activeSelf, Is.True);
            Assert.That(overlayObject.activeSelf, Is.False);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(detail));
            Assert.That(sharedInput.IsInputEnabled(), Is.True);

            Object.DestroyImmediate(menuObject);
            Assert.That(SentinalViewRouter.CurrentWindow, Is.Null);
            Assert.That(sharedInput == null, Is.True);
            Assert.That(SentinalViewRouter.AnyNonRootViewsOpen(), Is.False);
            Assert.That(panelInput.IsInputEnabled(), Is.False);
        }

        [Test]
        public void OwnershipCyclesAreRejectedWithoutChangingFocus()
        {
            menuObject = new GameObject("Window");
            ViewSelector window = menuObject.AddComponent<ViewSelector>();
            GameObject tabObject = new GameObject("Panel");
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector panel = tabObject.AddComponent<ViewSelector>();
            panel.FocusParent = window;

            Assert.Throws<System.InvalidOperationException>(() => window.FocusParent = panel);
            Assert.That(window.FocusParent, Is.Null);
            Assert.That(SentinalViewRouter.CurrentWindow, Is.EqualTo(window));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(panel));
        }

        [Test]
        public void EnablingBackgroundGateAfterModalDoesNotOverwriteModalMaps()
        {
            PlayerInput player = CreatePlayerInput();
            overlayObject = new GameObject("Modal");
            overlayObject.SetActive(false);
            ViewSelector modal = overlayObject.AddComponent<ViewSelector>();
            modal.Priority = 20;
            ActionMapGate modalGate = overlayObject.AddComponent<ActionMapGate>();
            modalGate.RestorePreviousActionMapState = true;
            modalGate.Mode = ActionMapGate.GateMode.Exclusive;
            modalGate.ExclusiveMapName = "Player";
            overlayObject.SetActive(true);

            menuObject = new GameObject("Background window");
            menuObject.SetActive(false);
            menuObject.AddComponent<ViewSelector>();
            ActionMapGate windowGate = menuObject.AddComponent<ActionMapGate>();
            windowGate.RestorePreviousActionMapState = true;
            windowGate.Mode = ActionMapGate.GateMode.Exclusive;
            menuObject.SetActive(true);
            Assert.That(ActionMapGate.Current, Is.EqualTo(modalGate));
            Assert.That(windowGate.IsApplied, Is.False);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));

            overlayObject.SetActive(false);
            Assert.That(ActionMapGate.Current, Is.EqualTo(windowGate));
            Assert.That(player.currentActionMap.name, Is.EqualTo("UI"));
            menuObject.SetActive(false);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OwnedPanelGateUnwindsOnDisableOrWholeWindowClose(bool disablePanelGate)
        {
            PlayerInput player = CreatePlayerInput();
            menuObject = new GameObject("Window");
            ViewSelector window = menuObject.AddComponent<ViewSelector>();
            ActionMapGate windowGate = menuObject.AddComponent<ActionMapGate>();
            windowGate.RestorePreviousActionMapState = true;
            windowGate.Mode = ActionMapGate.GateMode.Exclusive;

            GameObject tabObject = new GameObject("Owned panel");
            tabObject.SetActive(false);
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector panel = tabObject.AddComponent<ViewSelector>();
            panel.FocusParent = window;
            ActionMapGate panelGate = tabObject.AddComponent<ActionMapGate>();
            panelGate.RestorePreviousActionMapState = true;
            panelGate.Mode = ActionMapGate.GateMode.Exclusive;
            panelGate.ExclusiveMapName = "Player";
            tabObject.SetActive(true);
            Assert.That(ActionMapGate.Current, Is.EqualTo(panelGate));
            Assert.That(windowGate.IsApplied, Is.False);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));

            if (disablePanelGate)
            {
                panelGate.enabled = false;
                Assert.That(ActionMapGate.Current, Is.EqualTo(windowGate));
                Assert.That(player.currentActionMap.name, Is.EqualTo("UI"));
            }
            menuObject.SetActive(false);
            Assert.That(player.currentActionMap.name, Is.EqualTo("Player"));
        }

        private PlayerInput CreatePlayerInput()
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            actions.AddActionMap("Player").AddAction("Move");
            actions.AddActionMap("UI").AddAction("Confirm");
            playerObject = new GameObject("Player Input");
            playerObject.SetActive(false);
            PlayerInput player = playerObject.AddComponent<PlayerInput>();
            player.actions = actions;
            player.defaultActionMap = "Player";
            playerObject.SetActive(true);
            return player;
        }

        [UnityTest]
        public IEnumerator LowerPriorityViewEnablingKeepsModalSelectionUntilDismissed()
        {
            eventSystemObject = new GameObject("Event System", typeof(EventSystem));

            overlayObject = new GameObject("Confirmation");
            overlayObject.SetActive(false);
            ViewSelector overlayView = overlayObject.AddComponent<ViewSelector>();
            overlayView.Priority = 20;
            GameObject confirmButton = new GameObject("OK", typeof(RectTransform), typeof(Button));
            confirmButton.transform.SetParent(overlayObject.transform);
            overlayView.FirstSelected = confirmButton;
            overlayObject.SetActive(true);

            menuObject = new GameObject("Main Menu");
            menuObject.SetActive(false);
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            GameObject menuButton = new GameObject("Start", typeof(RectTransform), typeof(Button));
            menuButton.transform.SetParent(menuObject.transform);
            menuView.FirstSelected = menuButton;

            for (int i = 0; i < 3; i++)
            {
                menuObject.SetActive(true);
                yield return null;
                yield return null;

                Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(overlayView));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(confirmButton));
                menuObject.SetActive(false);
            }

            menuObject.SetActive(true);
            yield return null;
            yield return null;

            overlayObject.SetActive(false);

            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(menuView));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menuButton));
        }

        [Test]
        public void CloseCurrentViewUsesParentCloseHandlerForFocusedTab()
        {
            menuObject = new GameObject("Menu");
            menuObject.SetActive(false);
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            TestParentCloseHandler closeHandler = menuObject.AddComponent<TestParentCloseHandler>();

            GameObject tabObject = new GameObject("Tab");
            tabObject.SetActive(false);
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();
            tabView.FocusParent = menuView;

            menuObject.SetActive(true);
            tabObject.SetActive(true);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));

            SentinalViewRouter.CloseCurrentView();

            Assert.That(closeHandler.CloseCount, Is.EqualTo(1));
            Assert.That(menuObject.activeSelf, Is.False);
        }

        [Test]
        public void TabSwitchInputFollowsCurrentTabAndStopsForOverlayOrReplacedTabs()
        {
            menuObject = new GameObject("Menu");
            menuObject.SetActive(false);
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            ViewInputSystemHandler inputHandler = menuObject.AddComponent<ViewInputSystemHandler>();
            inputHandler.ViewSelector = menuView;
            inputHandler.InputMode = HandlerInputMode.FocusWithin;
            TabbedView tabbedView = menuObject.AddComponent<TabbedView>();
            tabbedView.FocusOwner = menuView;
            TestTabbedViewInputHandler tabInput = menuObject.AddComponent<TestTabbedViewInputHandler>();
            tabInput.SetViewInputHandler(inputHandler);
            tabInput.TabbedView = tabbedView;

            GameObject tabObject = new GameObject("Tab");
            tabObject.SetActive(false);
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();
            GameObject toggleObject = new GameObject("Toggle", typeof(Toggle));
            toggleObject.transform.SetParent(menuObject.transform);

            menuObject.SetActive(true);
            tabbedView.ReplaceTabs(new[] { toggleObject.GetComponent<Toggle>() }, new[] { tabView });
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(inputHandler.IsInputEnabled(), Is.True);
            Assert.That(tabInput.Subscribed, Is.True);

            overlayObject = new GameObject("Overlay");
            ViewSelector overlayView = overlayObject.AddComponent<ViewSelector>();
            overlayView.Priority = 20;
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(overlayView));
            Assert.That(tabInput.Subscribed, Is.False);

            overlayObject.SetActive(false);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(tabInput.Subscribed, Is.True);

            tabbedView.ReplaceTabs(System.Array.Empty<Toggle>(), System.Array.Empty<ViewSelector>());
            Assert.That(tabInput.Subscribed, Is.False);
        }
    }

    public class TestParentCloseHandler : MonoBehaviour, ICloseableView
    {
        public int CloseCount { get; private set; }

        public void Close()
        {
            CloseCount++;
            gameObject.SetActive(false);
        }
    }

    public class TestTabbedViewInputHandler : TabbedViewInputHandler
    {
        public bool Subscribed => isSubscribed;

        protected override void Subscribe() => isSubscribed = true;

        protected override void Unsubscribe() => isSubscribed = false;
    }
}
