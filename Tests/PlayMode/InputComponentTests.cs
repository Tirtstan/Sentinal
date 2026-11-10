#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    /// <summary>
    /// Protects the input-component contract: Active when follows focus-within (tabs yes, popups no), presses held
    /// before a view regains input never fire, pinned players survive authority hand-off, OpenViewOnAction respects
    /// layers, focus and pending Back, and action buttons only click an interactable Button.
    /// </summary>
    public class InputComponentTests : SentinalInputFixture
    {
        [Test]
        public void ViewFocusedFollowsTabsButStopsForNestedPopupsWhileViewOpenKeepsListening()
        {
            SentinalView window = CreateView(Low);
            SentinalView tab = CreateView(0, window);
            SentinalView popup = CreateView(0, window);
            tab.IsTab = true;
            Probe focused = CreateProbe(window, ActiveWhen.ViewFocused);
            Probe open = CreateProbe(window, ActiveWhen.ViewOpen);

            window.Open();
            tab.Open();
            Assert.That(focused.IsInputActive, Is.True, "focus on a tab counts as focus on its window");
            Assert.That(open.IsInputActive, Is.True);

            popup.Open();
            Assert.That(focused.IsInputActive, Is.False, "a focused popup interrupts its parent");
            Assert.That(open.IsInputActive, Is.True);

            popup.Close();
            Assert.That(focused.IsInputActive, Is.True);

            window.Close();
            Assert.That(focused.IsInputActive, Is.False);
            Assert.That(open.IsInputActive, Is.False);
        }

        [UnityTest]
        public IEnumerator InputDelayBelongsToEachWindowAndRestartsForNestedViewsWhilePaused()
        {
            const float delay = 0.15f;
            SentinalView delayed = CreateView(Low);
            delayed.InputDelay = delay;
            SentinalView nested = CreateView(0, delayed);
            nested.InputDelay = 10f;
            SentinalView immediate = CreateView(Low);
            float previousTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 0f;
                delayed.Open();
                nested.Open();
                Assert.That(nested.IsInputReady, Is.False, "nested views inherit the window's delay");

                yield return new WaitForSecondsRealtime(delay + 0.05f);
                Assert.That(nested.IsInputReady, Is.True, "the window's unscaled delay expires while paused");

                immediate.Open();
                Assert.That(immediate.IsInputReady, Is.True, "another view on the same layer has its own zero delay");

                immediate.Close();
                Assert.That(nested.IsInputReady, Is.False, "regaining focus restarts the window's delay");
                yield return new WaitForSecondsRealtime(delay + 0.05f);
                Assert.That(nested.IsInputReady, Is.True);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [Test]
        public void PressHeldBeforeTheViewRegainsInputNeverFires()
        {
            First.actions.Enable();
            SentinalView window = CreateView(Low);
            SentinalView popup = CreateView(0, window);
            Probe probe = CreateProbe(window, ActiveWhen.ViewFocused);
            window.Open();
            popup.Open();

            Press(FirstPad.buttonSouth);
            currentTime += 0.1;
            popup.Close();
            Assert.That(probe.IsInputActive, Is.True);

            currentTime += 1.0;
            UnityEngine.InputSystem.InputSystem.Update();
            Release(FirstPad.buttonSouth);
            Assert.That(probe.Accepted, Is.Empty, "a held hold must not complete or release inside the regained view");

            currentTime += 0.1;
            Press(FirstPad.buttonSouth);
            currentTime += 1.0;
            UnityEngine.InputSystem.InputSystem.Update();
            Assert.That(probe.Accepted, Does.Contain(InputActionPhase.Performed), "a fresh press works");
        }

        [Test]
        public void ExplicitPlayerSurvivesAuthorityHandOffWhileOthersFollowIt()
        {
            SentinalView window = CreateView(Low);
            window.Open();
            Probe inView = CreateProbe(window, ActiveWhen.ViewFocused);
            Probe outside = CreateProbe(null, ActiveWhen.ViewFocused);
            Probe pinnedInView = CreateProbe(window, ActiveWhen.ViewFocused);
            Probe pinnedOutside = CreateProbe(null, ActiveWhen.ViewFocused);
            pinnedInView.SetPlayer(Second);
            pinnedOutside.SetPlayer(Second);
            Assert.That(inView.Player, Is.SameAs(First));
            Assert.That(outside.Player, Is.SameAs(First));

            SentinalViewRouter.SetAuthorityPlayer(Keys);

            Assert.That(inView.Player, Is.SameAs(Keys));
            Assert.That(outside.Player, Is.SameAs(Keys));
            Assert.That(pinnedInView.Player, Is.SameAs(Second));
            Assert.That(pinnedOutside.Player, Is.SameAs(Second));
        }

        [Test]
        public void OpenViewOnActionRespectsLayerFocusAndPendingBack()
        {
            SentinalViewRouter.SetAuthorityPlayer(Keys);
            Keys.actions.Enable();
            SentinalView target = CreateView(Low);
            SentinalView popup = CreateView(0, target);
            SentinalView above = CreateView(High);
            CreateOpener(target);

            above.Open();
            PressAndRelease(Keyboard.pKey);
            Assert.That(target.IsOpen, Is.False, "never opens below the focused window's layer");
            above.Close();

            SentinalView worldMenu = CreateView(World);
            worldMenu.Open(Keys);
            Assert.That(SentinalViewRouter.CanHandleBack(Keys), Is.False);
            PressAndRelease(Keyboard.pKey);
            Assert.That(target.IsOpen, Is.True, "opens over a player's world menu");
            Assert.That(worldMenu.IsOpen, Is.True, "the world menu stays open underneath");

            popup.Open();
            PressAndRelease(Keyboard.pKey);
            Assert.That(popup.IsOpen, Is.True, "a popup above the view must be closed first");
            Assert.That(target.IsOpen, Is.True);
            popup.Close();
            PressAndRelease(Keyboard.pKey);
            Assert.That(target.IsOpen, Is.False, "pressing again closes it once it has focus");

            SentinalViewRouter.NotifyBackPending(Keys);
            PressAndRelease(Keyboard.pKey);
            Assert.That(target.IsOpen, Is.False, "a pending Back press owns this frame");
        }

        [UnityTest]
        public IEnumerator InputActionButtonOnlyClicksAnInteractableButton()
        {
            GameObject item = CreateObject("Input Button");
            item.AddComponent<RectTransform>();
            Button button = item.AddComponent<Button>();
            TestInputActionButton inputButton = item.AddComponent<TestInputActionButton>();
            inputButton.SendPointerEvents = false;
            item.SetActive(true);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);

            inputButton.DeferExecution = false;
            button.interactable = false;
            inputButton.TriggerClick();
            Assert.That(clicks, Is.Zero);
            button.interactable = true;
            inputButton.TriggerClick();
            Assert.That(clicks, Is.EqualTo(1));
            button.enabled = false;
            inputButton.TriggerClick();
            Assert.That(clicks, Is.EqualTo(1));

            button.enabled = true;
            inputButton.DeferExecution = true;
            inputButton.TriggerClick();
            button.interactable = false;
            yield return null;
            Assert.That(clicks, Is.EqualTo(1), "a deferred click stops when the button becomes non-interactable");
        }

        private Probe CreateProbe(SentinalView view, ActiveWhen activeWhen)
        {
            GameObject item = CreateObject("Probe", view != null ? view.transform : null);
            Probe probe = item.AddComponent<Probe>();
            probe.ActiveWhen = activeWhen;
            item.SetActive(true);
            return probe;
        }

        /// <summary>Records which phases of the UI/Hold action it accepted.</summary>
        public class Probe : ViewInputComponent
        {
            public readonly List<InputActionPhase> Accepted = new();
            private InputAction hold;

            protected override void Subscribe(PlayerInput player)
            {
                hold = player.actions.FindAction("UI/Hold");
                hold.started += Record;
                hold.performed += Record;
                hold.canceled += Record;
            }

            protected override void Unsubscribe()
            {
                hold.started -= Record;
                hold.performed -= Record;
                hold.canceled -= Record;
                hold = null;
            }

            private void Record(InputAction.CallbackContext context)
            {
                if (CanProcessInput(context))
                    Accepted.Add(context.phase);
            }
        }

        public class TestInputActionButton : InputActionButton
        {
            public void TriggerClick() => Click();
        }
    }
}
#endif
