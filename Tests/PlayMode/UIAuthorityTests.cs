#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sentinal.InputSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Sentinal.Tests
{
    /// <summary>
    /// Protects which local player can take global UI, especially shared device-class representatives
    /// and the player-view eligibility boundary. Inputs run through the real Input System callbacks.
    /// </summary>
    public class UIAuthorityTests : InputTestFixture
    {
        private readonly List<GameObject> objects = new();
        private readonly List<InputActionAsset> playerAssets = new();
        private InputActionAsset actions;
        private InputActionReference request;
        private UIAuthority authority;
        private AuthorityTestPrimaryResolver resolver;
        private Keyboard keyboard;
        private Gamepad firstPad;
        private Gamepad secondPad;
        private PlayerInput first;
        private PlayerInput second;
        private PlayerInput third;

        public override void Setup()
        {
            base.Setup();
            keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            firstPad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            secondPad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap ui = actions.AddActionMap("UI");
            ui.AddAction("Navigate", InputActionType.Value, "<Gamepad>/leftStick");
            ui.AddAction("Press", InputActionType.Button, "<Keyboard>/a").AddBinding("<Gamepad>/buttonSouth");
            InputActionMap global = actions.AddActionMap("Global");
            InputAction requestAction = global.AddAction("Request", InputActionType.Button, "<Gamepad>/select");
            requestAction.AddBinding("<Keyboard>/r");
            request = InputActionReference.Create(requestAction);
            actions.AddControlScheme("Keyboard").WithRequiredDevice("<Keyboard>");
            actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");

            first = CreatePlayer(keyboard, "Keyboard");
            second = CreatePlayer(firstPad, "Gamepad");
            third = CreatePlayer(secondPad, "Gamepad");

            GameObject owner = CreateObject("Authority");
            resolver = owner.AddComponent<AuthorityTestPrimaryResolver>();
            authority = owner.AddComponent<UIAuthority>();
            var serialized = new SerializedObject(authority);
            serialized.FindProperty("primaryPlayerResolver").objectReferenceValue = resolver;
            serialized.FindProperty("controlSchemes").objectReferenceValue = actions;
            serialized.FindProperty("requestAction").objectReferenceValue = request;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            authority.Initialize();
        }

        public override void TearDown()
        {
            if (authority != null)
                authority.Shutdown();
            foreach (GameObject owner in objects)
                Object.DestroyImmediate(owner);
            objects.Clear();
            foreach (InputActionAsset asset in playerAssets)
                Object.DestroyImmediate(asset);
            playerAssets.Clear();
            Object.DestroyImmediate(request);
            Object.DestroyImmediate(actions);
            base.TearDown();
        }

        [TestCase(UIAuthorityMode.PrimaryPlayer, false)]
        [TestCase(UIAuthorityMode.DynamicMostRecent, true)]
        [TestCase(UIAuthorityMode.ExplicitAuthorityRequest, false)]
        [TestCase(UIAuthorityMode.DeviceClassPriority, true)]
        public void ModesAdmitOnlyTheirEligibleAuthorityInput(UIAuthorityMode mode, bool ordinaryInputTakesAuthority)
        {
            authority.Mode = mode;
            Press(firstPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(ordinaryInputTakesAuthority ? second : first));

            Press(secondPad.selectButton);
            Assert.That(UIAuthority.Current, Is.SameAs(mode == UIAuthorityMode.PrimaryPlayer ? first : third));
            Assert.That(UIAuthority.Primary, Is.SameAs(first));
        }

        [Test]
        public void RequestReplacesTheDeviceClassRepresentative()
        {
            authority.Mode = UIAuthorityMode.DeviceClassPriority;
            Press(secondPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(first), "The second gamepad is not its class representative.");

            Press(firstPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(second));
            Press(secondPad.selectButton);
            Assert.That(UIAuthority.Current, Is.SameAs(third));

            Press(keyboard.aKey);
            Assert.That(UIAuthority.Current, Is.SameAs(first));
            Release(firstPad.buttonSouth);
            Press(firstPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(first), "The former gamepad representative cannot retake UI.");

            Release(secondPad.buttonSouth);
            Press(secondPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(third));
        }

        [Test]
        public void ActuatedStickTakesAuthorityButReturningToZeroDoesNot()
        {
            authority.Mode = UIAuthorityMode.DeviceClassPriority;
            Set(firstPad.leftStick, new Vector2(0.7f, 0));
            Assert.That(UIAuthority.Current, Is.SameAs(second));
            Press(keyboard.aKey);
            Set(firstPad.leftStick, Vector2.zero);
            Assert.That(UIAuthority.Current, Is.SameAs(first));
        }

        [TestCase(UIAuthorityMode.DynamicMostRecent)]
        [TestCase(UIAuthorityMode.ExplicitAuthorityRequest)]
        [TestCase(UIAuthorityMode.DeviceClassPriority)]
        public void EligibilityRunsBeforeAuthoritySelectionAndRepresentativeReplacement(UIAuthorityMode mode)
        {
            authority.Mode = mode;
            InputActionMap global = second.actions.FindAction(request.action.id).actionMap;
            authority.ActionEligibility = (player, action) => player != second || action.actionMap.id == global.id;
            Press(firstPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(first));
            Press(firstPad.selectButton);
            Assert.That(UIAuthority.Current, Is.SameAs(second));

            authority.ActionEligibility = (player, _) => player != third;
            Press(secondPad.selectButton);
            Assert.That(
                UIAuthority.Current,
                Is.SameAs(second),
                "An ineligible Request must not replace the representative."
            );
        }

        [Test]
        public void MissingOptionalRequestInAPlayersAssetIsOff()
        {
            authority.Mode = UIAuthorityMode.ExplicitAuthorityRequest;
            InputAction action = second.actions.FindAction(request.action.id);
            second.actions.Disable();
            action.RemoveAction();
            second.actions.Enable();
            Press(firstPad.selectButton);
            Press(firstPad.buttonSouth);
            Assert.That(UIAuthority.Current, Is.SameAs(first));
        }

        [Test]
        public void PrimaryRefreshAndExplicitSelectionPublishWithoutInputPolling()
        {
            authority.Mode = UIAuthorityMode.PrimaryPlayer;
            Assert.That(authority.SetPrimaryPlayer(third), Is.True);
            Assert.That(UIAuthority.Primary, Is.SameAs(third));
            Assert.That(UIAuthority.Current, Is.SameAs(third));
            resolver.SessionPrimary = second;
            resolver.Refresh();
            Assert.That(UIAuthority.Primary, Is.SameAs(second));
            Assert.That(UIAuthority.Current, Is.SameAs(second));
        }

        [Test]
        public void RemovingAuthorityPlayerFallsBackBeforeTheNextInput()
        {
            authority.Mode = UIAuthorityMode.DeviceClassPriority;
            Press(firstPad.buttonSouth);
            second.gameObject.SetActive(false);
            UnityEngine.InputSystem.InputSystem.Update();
            Assert.That(UIAuthority.MostRecent, Is.SameAs(first));
            Assert.That(UIAuthority.Current, Is.SameAs(first));
        }

        private GameObject CreateObject(string name)
        {
            var owner = new GameObject(name);
            owner.SetActive(false);
            objects.Add(owner);
            return owner;
        }

        private PlayerInput CreatePlayer(InputDevice device, string scheme)
        {
            GameObject owner = CreateObject(scheme);
            PlayerInput player = owner.AddComponent<PlayerInput>();
            player.actions = Object.Instantiate(actions);
            playerAssets.Add(player.actions);
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            player.neverAutoSwitchControlSchemes = true;
            player.defaultControlScheme = scheme;
            owner.SetActive(true);
            player.SwitchCurrentControlScheme(scheme, device);
            player.actions.Enable();
            return player;
        }
    }

    public sealed class AuthorityTestPrimaryResolver : MonoBehaviour, IPrimaryPlayerResolver
    {
        public event Action Changed;
        public PlayerInput SessionPrimary { get; set; }

        public void Refresh() => Changed?.Invoke();

        public PlayerInput ResolvePrimaryPlayer(int preferredPlayerIndex, Predicate<PlayerInput> isEligible)
        {
            if (SessionPrimary != null && isEligible(SessionPrimary))
                return SessionPrimary;
            PlayerInput preferred = PlayerInput.GetPlayerByIndex(preferredPlayerIndex);
            return preferred != null && isEligible(preferred)
                ? preferred
                : PlayerInput.all.FirstOrDefault(player => isEligible(player));
        }
    }
}
#endif
