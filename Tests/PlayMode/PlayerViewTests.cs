#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sentinal.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Sentinal.Tests
{
    /// <summary>
    /// Protects independent local-player menus: per-player stacks, Back ownership, held input across focus changes,
    /// and the enabled-action contract that PlayerInputMaps keeps across map changes.
    /// </summary>
    public class PlayerViewTests : SentinalInputFixture
    {
        [Test]
        public void RefuseAndHideApplyToEachPlayersOwnStack()
        {
            SentinalView a = CreateView(Menus);
            SentinalView b = CreateView(Menus);
            SentinalView c = CreateView(Menus);
            Assert.That(a.Open(First), Is.True);
            Assert.That(b.Open(Second), Is.True, "another player's open menu never refuses mine");
            Assert.That(c.Open(First), Is.False);
            Assert.That(SentinalViewRouter.CanOpen(c.Layer, First), Is.False);
            Assert.That(SentinalViewRouter.GetPlayerView(First), Is.SameAs(a));
            Assert.That(SentinalViewRouter.GetPlayerView(Second), Is.SameAs(b));
            Assert.That(SentinalViewRouter.CurrentView, Is.Null, "player views never take global focus");
            a.Close();
            Assert.That(c.Open(First), Is.True);

            SentinalView under = CreateView(Stacked);
            SentinalView over = CreateView(Stacked);
            SentinalView other = CreateView(Stacked);
            under.Open(First);
            other.Open(Second);
            over.Open(First);
            Assert.That(under.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(other.State, Is.EqualTo(ViewState.Open), "hiding only touches the opening player's stack");
            over.Close();
            Assert.That(under.State, Is.EqualTo(ViewState.Open));
        }

        [Test]
        public void PlayerLayerRequiresOwnerAndCannotNestUnderGlobal()
        {
            SentinalView menu = CreateView(Menus);
            LogAssert.Expect(LogType.Error, new Regex(@"expected Open\(PlayerInput owner\)"));
            Assert.That(menu.Open(), Is.False);

            SentinalView global = CreateView(Low);
            SentinalView nested = CreateView(Menus, global);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"expected a Player layer for Open\(owner\); actual: Global layer")
            );
            Assert.That(nested.Open(First), Is.False);
        }

        [Test]
        public void BackUsesGlobalThenOwnChainAndNeverClosesAnotherPlayersMenu()
        {
            SentinalView a = CreateView(Menus);
            SentinalView b = CreateView(Menus);
            SentinalView pause = CreateView(Low);
            a.Open(First);
            b.Open(Second);
            pause.Open();
            Assert.That(SentinalViewRouter.GetBackView(First), Is.SameAs(pause));
            Assert.That(SentinalViewRouter.GetBackView(Second), Is.Null);
            Assert.That(SentinalViewRouter.HandleBack(Second), Is.False);
            Assert.That(b.IsInputReady, Is.False, "global UI suspends player menus");
            Assert.That(b.State, Is.EqualTo(ViewState.Open));

            Assert.That(SentinalViewRouter.HandleBack(First), Is.True);
            Assert.That(SentinalViewRouter.GetBackView(First), Is.SameAs(a));
            Assert.That(SentinalViewRouter.GetBackView(Second), Is.SameAs(b));
            SentinalViewRouter.HandleBack(Second);
            Assert.That(a.IsOpen, Is.True);
            Assert.That(b.IsOpen, Is.False);

            a.Close();
            SentinalView floor = CreateView(Floor);
            floor.Open();
            Assert.That(SentinalViewRouter.GetBackView(First), Is.SameAs(floor));
            Assert.That(SentinalViewRouter.GetBackView(Second), Is.Null);
        }

        [Test]
        public void MissingBackBindingsAreSupportedWithoutDiagnostics()
        {
            SentinalView menu = CreateView(Menus);
            menu.Open(Keys);
            CreateBackHandler();
            Press(Keyboard.aKey);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Keys.actions.FindAction("UI/Cancel").controls, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AlwaysOnBackBacksOutOneLevelOrOpensTheUnhandledBackView()
        {
            Keys.actions.FindAction("UI/Cancel").AddBinding("<Keyboard>/escape");
            Keys.actions.FindAction("Global/Back").AddBinding("<Keyboard>/escape");
            SentinalViewRouter.SetAuthorityPlayer(Keys);
            PlayerInputMaps.Register(Keys);
            SentinalView pause = CreateView(Low);
            SentinalView popup = CreateView(0, pause);
            CreateOpener(pause, onlyWhenNoMenuOpen: true, opensOnUnhandledBack: true);
            CreateBackHandler();
            PlayerInputMaps.Reconcile();
            yield return null;

            PressAndRelease(Keyboard.escapeKey);
            yield return null;
            Assert.That(pause.IsOpen, Is.True, "Back with nothing to go back from opens the view");

            popup.Open();
            PlayerInputMaps.Reconcile();
            yield return null;
            PressAndRelease(Keyboard.escapeKey);
            yield return null;
            yield return null;
            Assert.That(popup.IsOpen, Is.False);
            Assert.That(pause.IsOpen, Is.True, "Cancel and the always-on Back share Escape but back out once");

            PressAndRelease(Keyboard.escapeKey);
            yield return null;
            yield return null;
            Assert.That(pause.IsOpen, Is.False, "Back closes the view rather than reopening it");
        }

        [Test]
        public void MapRecomputeBatchesCloseOpenAndLateRegistrationNeverAppliesBaseFirst()
        {
            PlayerInputMaps.Register(First);
            SentinalView pause = CreateView(Low);
            SentinalView dialog = CreateView(Low);
            pause.Open();
            PlayerInputMaps.Reconcile();
            AssertMaps(First, "UI");

            var applied = new List<string>();
            Action<PlayerInput, InputActionMap, InputActionMap> observe = (player, previousMap, nextMap) =>
                applied.Add(nextMap.name);
            PlayerInputMaps.BeforeApply += observe;
            try
            {
                pause.Close();
                dialog.Open();
                PlayerInputMaps.Register(Second);
                PlayerInputMaps.Reconcile();
                Assert.That(applied, Does.Not.Contain("Player"));
                AssertMaps(First, "UI");
                AssertMaps(Second, "UI");
                dialog.Close();
                PlayerInputMaps.Reconcile();
                AssertMaps(First, "Player");
                AssertMaps(Second, "Player");
            }
            finally
            {
                PlayerInputMaps.BeforeApply -= observe;
            }
        }

        [Test]
        public void HeldButtonAndVectorMustReturnNeutralOnOpenUnsuspendOwnerAndAuthorityChange()
        {
            SentinalView menu = CreateView(Menus);
            First.actions.Enable();
            Press(FirstPad.buttonSouth);
            Set(FirstPad.leftStick, Vector2.right);
            menu.Open(First);
            InputAction charge = First.actions.FindAction("UI/Charge");
            InputAction navigate = First.actions.FindAction("UI/Navigate");
            Assert.That(menu.CanProcessInput(charge), Is.False);
            Assert.That(menu.CanProcessInput(navigate), Is.False);

            Release(FirstPad.buttonSouth);
            Set(FirstPad.leftStick, Vector2.zero);
            menu.CanProcessInput(charge);
            menu.CanProcessInput(navigate);
            Press(FirstPad.buttonSouth);
            Assert.That(menu.CanProcessInput(charge), Is.True);

            SentinalView pause = CreateView(Low);
            pause.Open();
            pause.Close();
            Assert.That(menu.CanProcessInput(charge), Is.False, "unsuspending needs a fresh press");

            Second.actions.Enable();
            Press(SecondPad.buttonSouth);
            menu.Open(Second);
            Assert.That(menu.CanProcessInput(Second.actions.FindAction("UI/Charge")), Is.False);
            pause.Open();
            SentinalViewRouter.SetAuthorityPlayer(Second);
            Assert.That(pause.CanProcessInput(Second.actions.FindAction("UI/Charge")), Is.False);
        }

        [Test]
        public void PlayerMenuActionsCannotTakeAuthorityButGlobalActionsCan()
        {
            SentinalView menu = CreateView(Menus);
            menu.Open(Second);
            Assert.That(
                PlayerInputMaps.IsAuthorityActionEligible(Second, Second.actions.FindAction("UI/Charge")),
                Is.False
            );
            Assert.That(
                PlayerInputMaps.IsAuthorityActionEligible(Second, Second.actions.FindAction("Global/Pause")),
                Is.True
            );
            Assert.That(
                PlayerInputMaps.IsAuthorityActionEligible(First, First.actions.FindAction("UI/Charge")),
                Is.True
            );
        }

        private static void AssertMaps(PlayerInput player, string name)
        {
            Assert.That(player.currentActionMap.name, Is.EqualTo(name));
            foreach (InputActionMap map in player.actions.actionMaps)
            foreach (InputAction action in map.actions)
                Assert.That(action.enabled, Is.EqualTo(map.name == name || map.name == "Global"), action.ToString());
        }
    }
}
#endif
