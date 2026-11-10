#if ENABLE_INPUT_SYSTEM
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Owns Back for every local player. Cancel works while the UI map is on; the optional always-on Back also works in
    /// gameplay and, when there is nothing to go back from, opens the <see cref="OpenViewOnAction"/> marked to open on
    /// unhandled Back (e.g. Escape opens Pause). A control bound to both is handled once, by the always-on Back.
    /// </summary>
    [AddComponentMenu("Sentinal/View Dismissal Input Handler"), DisallowMultipleComponent]
    public sealed class ViewDismissalInputHandler : MonoBehaviour
    {
        [Header("Actions")]
        [SerializeField]
        private InputActionSelector cancelAction = new("UI/Cancel");

        [SerializeField]
        [Tooltip(
            "Back in an always-on map. With nothing to go back from, it opens the view whose Open View On Action has 'Opens On Unhandled Back'. An asset without this action has it off."
        )]
        private InputActionSelector alwaysOnBackAction = new("Global/Back");

        [SerializeField]
        private InputActionSelector focusAction = new("UI/Focus");

        [SerializeField]
        private bool cancelActionOnRelease;

        private sealed class Binding
        {
            public PlayerInput Player;
            public InputAction Cancel;
            public InputAction AlwaysOnBack;
            public InputAction Focus;
            public System.Action<InputAction.CallbackContext> CancelPressed;
            public System.Action<InputAction.CallbackContext> CancelReleased;
            public System.Action<InputAction.CallbackContext> BackPressed;
            public System.Action<InputAction.CallbackContext> BackReleased;
            public System.Action<InputAction.CallbackContext> Focused;
            public bool PressAccepted;
            public bool PressIsAlwaysOn;
            public bool Pending;

            /// <summary>True when the always-on Back also hears this control, so Cancel leaves the press to it.</summary>
            public bool AlwaysOnBackOwns(InputControl control)
            {
                if (AlwaysOnBack == null)
                    return false;

                foreach (InputControl bound in AlwaysOnBack.controls)
                    if (bound == control)
                        return true;

                return false;
            }
        }

        private readonly Dictionary<PlayerInput, Binding> bindings = new();

        private void OnEnable()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += RefreshPlayers;
            RefreshPlayers();
        }

        private void OnDisable()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= RefreshPlayers;
            StopAllCoroutines();
            foreach (Binding binding in bindings.Values.ToArray())
                Unbind(binding);
            bindings.Clear();
        }

        private void RefreshPlayers()
        {
            foreach (Binding binding in bindings.Values.ToArray())
                if (binding.Player == null || !PlayerInput.all.Contains(binding.Player))
                {
                    Unbind(binding);
                    bindings.Remove(binding.Player);
                }
            foreach (PlayerInput player in PlayerInput.all)
            {
                if (bindings.ContainsKey(player) || player.actions == null)
                    continue;
                var binding = new Binding
                {
                    Player = player,
                    Cancel = cancelAction.FindAction(player.actions),
                    AlwaysOnBack = alwaysOnBackAction.FindAction(player.actions),
                    Focus = focusAction.FindAction(player.actions),
                };
                binding.CancelPressed = context =>
                {
                    if (!binding.AlwaysOnBackOwns(context.control))
                        OnPressed(binding, binding.Cancel, alwaysOn: false);
                };
                binding.CancelReleased = context =>
                {
                    if (!binding.AlwaysOnBackOwns(context.control))
                        OnReleased(binding);
                };
                binding.BackPressed = context => OnPressed(binding, binding.AlwaysOnBack, alwaysOn: true);
                binding.BackReleased = context => OnReleased(binding);
                binding.Focused = context =>
                {
                    if (binding.Player == UIAuthority.Current)
                        SentinalViewRouter.TrySelectCurrentView();
                };
                if (binding.Cancel != null)
                {
                    binding.Cancel.performed += binding.CancelPressed;
                    binding.Cancel.canceled += binding.CancelReleased;
                }
                if (binding.AlwaysOnBack != null)
                {
                    binding.AlwaysOnBack.performed += binding.BackPressed;
                    binding.AlwaysOnBack.canceled += binding.BackReleased;
                }
                if (binding.Focus != null)
                    binding.Focus.performed += binding.Focused;
                bindings.Add(player, binding);
            }
        }

        private void OnPressed(Binding binding, InputAction action, bool alwaysOn)
        {
            SentinalView target = SentinalViewRouter.GetBackView(binding.Player);
            // With nothing focused only the always-on Back has work to do: opening the unhandled-Back view.
            bool targetAccepts = target != null ? target.CanProcessInput(action) : alwaysOn;
            binding.PressAccepted = !binding.Pending && targetAccepts;
            binding.PressIsAlwaysOn = alwaysOn;
            if (!cancelActionOnRelease && binding.PressAccepted)
                RequestBack(binding);
        }

        private void OnReleased(Binding binding)
        {
            if (cancelActionOnRelease && binding.PressAccepted)
                RequestBack(binding);
            binding.PressAccepted = false;
        }

        private void RequestBack(Binding binding)
        {
            if (binding.Pending)
                return;

            if (!SentinalViewRouter.CanHandleBack(binding.Player))
            {
                if (binding.PressIsAlwaysOn)
                    OpenViewOnAction.TryOpenOnUnhandledBack(binding.Player);
                return;
            }

            SentinalView target = SentinalViewRouter.GetBackView(binding.Player);
            binding.Pending = true;
            SentinalViewRouter.NotifyBackPending(binding.Player);
            StartCoroutine(BackNextFrame(binding, target));
        }

        private IEnumerator BackNextFrame(Binding binding, SentinalView target)
        {
            yield return null;
            binding.Pending = false;
            if (!bindings.ContainsKey(binding.Player) || SentinalViewRouter.GetBackView(binding.Player) != target)
                yield break;

            if (target != null && target.IsShown)
                SentinalViewRouter.HandleBack(binding.Player);
        }

        private static void Unbind(Binding binding)
        {
            if (binding.Cancel != null)
            {
                binding.Cancel.performed -= binding.CancelPressed;
                binding.Cancel.canceled -= binding.CancelReleased;
            }
            if (binding.AlwaysOnBack != null)
            {
                binding.AlwaysOnBack.performed -= binding.BackPressed;
                binding.AlwaysOnBack.canceled -= binding.BackReleased;
            }
            if (binding.Focus != null)
                binding.Focus.performed -= binding.Focused;
        }
    }
}
#endif
