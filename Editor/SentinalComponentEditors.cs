using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using Sentinal.InputSystem;
#endif

namespace Sentinal.Editor
{
    /// <summary>Owns refresh and multi-selection presentation for the smaller component inspectors.</summary>
    public abstract class SentinalComponentEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = SentinalEditorUI.Root();
            VisualElement strip = SentinalEditorUI.Status(root);
            var state = new StatusFact(strip, "State", showTone: true);
            Action refresh = CreateStatus(strip, state);
            SentinalEditorUI.DefaultFields(root, serializedObject);

            void Refresh()
            {
                if (target == null)
                    return;
                if (serializedObject.isEditingMultipleObjects)
                {
                    state.Set($"{targets.Length} selected");
                    foreach (VisualElement fact in strip.Children())
                        if (fact != state)
                            fact.AddToClassList("sentinal-hidden");
                    return;
                }

                var component = (MonoBehaviour)target;
                bool active = Application.isPlaying ? component.isActiveAndEnabled : component.enabled;
                state.Set(active ? "Enabled" : "Disabled", active ? SentinalTone.Open : SentinalTone.Idle);
                refresh();
            }

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.schedule.Execute(Refresh).Every(100);
            Refresh();
            return root;
        }

        internal abstract Action CreateStatus(VisualElement strip, StatusFact state);
    }

    [CustomEditor(typeof(ViewLink), true), CanEditMultipleObjects]
    public sealed class ViewLinkEditor : SentinalComponentEditor
    {
        internal override Action CreateStatus(VisualElement strip, StatusFact state)
        {
            var address = new StatusFact(strip, "Opens");
            return () =>
            {
                var link = (ViewLink)target;
                address.Set(link.TargetAddress != null ? null : "No address", target: link.TargetAddress);
                if (link.TargetAddress == null)
                {
                    state.Set("Missing address", SentinalTone.Warning);
                    address.tooltip = "Expected a target view address; actual: none. Assign Target Address below.";
                }
            };
        }
    }

    [CustomEditor(typeof(ViewTransition), true), CanEditMultipleObjects]
    public sealed class ViewTransitionEditor : SentinalComponentEditor
    {
        internal override Action CreateStatus(VisualElement strip, StatusFact state)
        {
            var view = new StatusFact(strip, "View");
            var input = new StatusFact(strip, "Input");
            var hidden = new StatusFact(strip, "Covered");
            return () =>
            {
                var transition = (ViewTransition)target;
                transition.TryGetComponent(out SentinalView owner);
                view.Set(owner != null ? null : "Missing", target: owner);
                input.Set(transition.InputWaitsForTransition ? "Waits for animation" : "No animation wait");
                hidden.Set(transition.AnimateWhenHidden ? "Animates" : "Snaps");
                if (owner == null)
                {
                    state.Set("Missing view", SentinalTone.Warning);
                    view.tooltip = "Expected a Sentinal View on this object; actual: none.";
                }
                else if (Application.isPlaying)
                {
                    state.Set(SentinalEditorUI.State(owner), SentinalEditorUI.ToneOf(owner));
                }
            };
        }
    }

#if ENABLE_INPUT_SYSTEM
    [CustomEditor(typeof(UIAuthority)), CanEditMultipleObjects]
    public sealed class UIAuthorityEditor : SentinalComponentEditor
    {
        internal override Action CreateStatus(VisualElement strip, StatusFact state)
        {
            var policy = new StatusFact(strip, "Policy");
            var player = new StatusFact(strip, "Player");
            return () =>
            {
                var authority = (UIAuthority)target;
                policy.Set(ObjectNames.NicifyVariableName(authority.Mode.ToString()));
                player.EnableInClassList("sentinal-hidden", !Application.isPlaying);
                if (!Application.isPlaying)
                    return;
                if (UIAuthority.Instance != authority)
                {
                    state.Set("Inactive", SentinalTone.Idle);
                    player.Set("Owned elsewhere", target: UIAuthority.Instance);
                    if (UIAuthority.Instance == null)
                        player.Set("No active authority", SentinalTone.Warning);
                    return;
                }

                state.Set("Active", SentinalTone.Focus);
                player.Set(SentinalEditorUI.PlayerName(UIAuthority.Current));
                player.tooltip = authority.CurrentReason;
            };
        }
    }

    [CustomEditor(typeof(ViewDismissalInputHandler)), CanEditMultipleObjects]
    public sealed class ViewDismissalInputHandlerEditor : SentinalComponentEditor
    {
        internal override Action CreateStatus(VisualElement strip, StatusFact state)
        {
            var scope = new StatusFact(strip, "Scope");
            var back = new StatusFact(strip, "Back");
            return () =>
            {
                scope.Set("All local players");
                var handler = (ViewDismissalInputHandler)target;
                if (!Application.isPlaying)
                {
                    back.Set(
                        serializedObject.FindProperty("cancelActionOnRelease").boolValue ? "On release" : "On press"
                    );
                    return;
                }
                if (!handler.isActiveAndEnabled)
                {
                    back.Set("Not listening");
                    return;
                }

                back.Set(SentinalEditorUI.Describe(SentinalViewRouter.PreviewBack()));
                back.tooltip =
                    "Back preview for the UI authority player. Each local player follows their own view stack.";
            };
        }
    }
#endif
}
