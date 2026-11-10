#if ENABLE_INPUT_SYSTEM
using Sentinal.InputSystem.Components;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    /// <summary>Shows the nearest-view/player rule for every component that follows it.</summary>
    [CustomEditor(typeof(ViewPlayerComponent), true), CanEditMultipleObjects]
    public sealed class ViewPlayerComponentEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = SentinalEditorUI.Root();
            VisualElement strip = SentinalEditorUI.Status(root);
            var state = new StatusFact(strip, "Input", showTone: true);
            var owner = new StatusFact(strip, "View");
            var player = new StatusFact(strip, "Player");
            var tab = new StatusFact(strip, "Tab");
            SentinalEditorUI.DefaultFields(root, serializedObject);

            void Refresh()
            {
                if (target == null)
                    return;
                if (serializedObject.isEditingMultipleObjects)
                {
                    state.Set($"{targets.Length} selected");
                    owner.AddToClassList("sentinal-hidden");
                    player.AddToClassList("sentinal-hidden");
                    tab.AddToClassList("sentinal-hidden");
                    return;
                }

                var component = (ViewPlayerComponent)target;
                SentinalView view = component.ParentView;
                string source = component.HasExplicitPlayer
                    ? "pinned player"
                    : view != null
                        ? "view's player"
                        : "UI authority";
                owner.Set(view != null ? null : "Outside views", target: view);
                player.Set(Application.isPlaying ? SentinalEditorUI.PlayerName(component.Player) : source);
                player.tooltip = $"Player source: {source}";

                if (component is ViewInputComponent input)
                {
                    string rule =
                        view == null
                            ? "While enabled"
                            : input.ActiveWhen == ActiveWhen.ViewOpen
                                ? "While open"
                                : "While focused";
                    state.Set(
                        Application.isPlaying
                            ? input.IsInputActive
                                ? "Listening"
                                : "Not listening"
                            : rule,
                        Application.isPlaying && input.IsInputActive ? SentinalTone.Open : SentinalTone.Idle
                    );
                    state.tooltip = rule;
                }
                else
                {
                    bool active = Application.isPlaying ? component.isActiveAndEnabled : component.enabled;
                    state.Set(active ? "Enabled" : "Disabled", active ? SentinalTone.Open : SentinalTone.Idle);
                }

                tab.EnableInClassList("sentinal-hidden", component is not TabbedView);
                if (component is TabbedView tabs)
                {
                    int index = Application.isPlaying ? tabs.CurrentTabIndex : tabs.DefaultTabIndex;
                    SentinalView panel = index >= 0 && index < tabs.GroupPanels.Count ? tabs.GroupPanels[index] : null;
                    if (panel != null)
                        tab.Set($"{index + 1}/{tabs.GroupPanels.Count}", target: panel);
                    else
                        tab.Set(tabs.GroupPanels.Count == 0 ? "No tabs" : "Missing panel", SentinalTone.Warning);
                }
            }

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.schedule.Execute(Refresh).Every(100);
            Refresh();
            return root;
        }
    }
}
#endif
