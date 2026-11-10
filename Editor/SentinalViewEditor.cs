using System.Collections.Generic;
using System.Text;
using Sentinal.InputSystem.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    /// <summary>
    /// Leads with compact inline facts (live state, what Back does, where input goes), then one sentence about
    /// the layer, the live Open/Close/Focus actions, and the serialized fields. The layer answers and the sentence
    /// open that layer in Project Settings.
    /// </summary>
    [CustomEditor(typeof(SentinalView)), CanEditMultipleObjects]
    public sealed class SentinalViewEditor : UnityEditor.Editor
    {
        /// <summary>One cell of the strip: a small key over <see cref="Text"/>, an optional link, and a softer note.</summary>
        private readonly struct Answer
        {
            public readonly string Key;
            public readonly string Text;
            public readonly Object Link;
            public readonly string Note;
            public readonly bool FromLayer;

            public Answer(string key, string text, Object link = null, string note = null, bool fromLayer = false)
            {
                Key = key;
                Text = text;
                Link = link;
                Note = note;
                FromLayer = fromLayer;
            }
        }

        /// <summary>Everything the inspector says about a view that does not change every frame.</summary>
        private sealed class Summary
        {
            public readonly List<Answer> Answers = new();
            public string Note;
            public bool Warning;

            /// <summary>The layer the answers come from, or null when the view has none.</summary>
            public ViewLayer Layer;
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = SentinalEditorUI.Root();

            var panel = new VisualElement();
            panel.AddToClassList("sentinal-status-panel");
            root.Add(panel);
            VisualElement strip = SentinalEditorUI.Status(panel);
            var state = new StatusFact(strip, "State", showTone: true);

            var note = new Label();
            note.AddToClassList("sentinal-answers__note");
            panel.Add(note);
            int noteLayerId = 0;
            note.RegisterCallback<ClickEvent>(_ =>
            {
                if (noteLayerId != 0)
                    SentinalSettingsProvider.Reveal(noteLayerId);
            });

            var actionRow = new VisualElement();
            actionRow.AddToClassList("sentinal-actions");
            root.Add(actionRow);
            var actions = new VisualElement();
            actions.AddToClassList("sentinal-segment");
            actionRow.Add(actions);
            Button open = Action(actions, "Open", "Open this view.", view => view.Open());
            Button close = Action(actions, "Close", "Close this view.", view => view.Close());
            Button focus = Action(
                actions,
                "Focus",
                "Bring this view to the front. A view on a higher layer keeps focus.",
                view => view.BringToFront()
            );

            open.AddToClassList("sentinal-first");
            focus.AddToClassList("sentinal-last");

            var fields = new VisualElement();
            fields.AddToClassList("sentinal-view-fields");
            root.Add(fields);
            SentinalEditorUI.Field(fields, serializedObject, "address");
            PropertyField layer = SentinalEditorUI.Field(fields, serializedObject, "layer");
            PropertyField inputDelay = SentinalEditorUI.Field(fields, serializedObject, "inputDelay");
            SentinalEditorUI.Field(fields, serializedObject, "firstSelected");
            SentinalEditorUI.Field(fields, serializedObject, "rememberLastSelected");

            string summaryKey = null;
            int answerCount = 0;
            void Refresh()
            {
                if (target == null)
                    return;

                bool single = !serializedObject.isEditingMultipleObjects;
                bool allTopLevel = true;
                foreach (Object item in targets)
                    allTopLevel &= ((SentinalView)item).IsTopLevel;
                layer.EnableInClassList("sentinal-hidden", !allTopLevel);
                inputDelay.EnableInClassList("sentinal-hidden", !allTopLevel);

                Summary summary = single ? Summarise((SentinalView)target) : Selection(targets.Length);
                string key = Key(summary);
                if (key != summaryKey)
                {
                    summaryKey = key;
                    answerCount = summary.Answers.Count;
                    DrawAnswers(strip, summary.Answers, summary.Layer);
                    note.text = summary.Note;
                    note.EnableInClassList("sentinal-answers__note--warning", summary.Warning);
                    noteLayerId = summary.Layer?.Id ?? 0;
                    note.EnableInClassList("sentinal-answers__note--layer", noteLayerId != 0);
                    note.tooltip = summary.Layer != null ? LayerTooltip(summary.Layer) : null;
                }

                bool live = Application.isPlaying && single;
                state.EnableInClassList("sentinal-hidden", !live);
                actionRow.EnableInClassList("sentinal-hidden", !live);
                strip.EnableInClassList("sentinal-hidden", !live && answerCount == 0);
                if (!live)
                    return;

                var view = (SentinalView)target;
                state.Set(
                    SentinalEditorUI.State(view),
                    SentinalEditorUI.ToneOf(view),
                    note: SentinalEditorUI.Focus(view)
                );
                open.SetEnabled(view.State == ViewState.Closed);
                close.SetEnabled(view.IsOpen);
                focus.SetEnabled(view.IsShown && !SentinalViewRouter.IsFocusWithin(view));
            }

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.schedule.Execute(Refresh).Every(100);
            Refresh();
            return root;
        }

        private Button Action(VisualElement parent, string text, string tooltip, System.Action<SentinalView> run)
        {
            var button = new Button(() =>
            {
                if (target != null)
                    run((SentinalView)target);
            })
            {
                text = text,
                tooltip = tooltip,
            };
            parent.Add(button);
            return button;
        }

        private static Summary Selection(int count) => new() { Note = $"{count} views selected." };

        private static Summary Summarise(SentinalView view)
        {
            var summary = new Summary();
            SentinalView window = view.Window;
            SentinalSettings settings = SentinalSettingsRegistration.Active;
            ViewLayer layer = null;
            bool hasLayer = settings != null && settings.TryGetLayer(window.Layer, out layer, out _);

            if (view.Parent != null)
            {
                bool tab = IsTabOf(view, view.Parent);
                summary.Answers.Add(new Answer("Lives in", tab ? "Tab of" : "Inside", view.Parent));
                summary.Answers.Add(
                    tab ? new Answer("Back", "Passes to", view.Parent) : new Answer("Back", "Closes this")
                );
            }

            if (!hasLayer)
            {
                bool isolatedPrefab =
                    PrefabUtility.IsPartOfPrefabAsset(window)
                    || EditorSceneManager.IsPreviewSceneObject(window.gameObject);
                if (!window.Layer.IsSet && isolatedPrefab)
                {
                    summary.Note =
                        "No layer assigned in this prefab. Nest it inside a view to inherit its layer, or assign a layer to use it as a window.";
                    return summary;
                }

                string actual = window.Layer.IsSet ? $"unknown layer ID {window.Layer.Id}" : "none";
                summary.Note = $"Expected a layer from Project Settings › Sentinal on {window.name}; actual: {actual}.";
                summary.Warning = true;
                return summary;
            }

            summary.Layer = layer;
            if (view.Parent != null)
            {
                summary.Note = $"{layer.Name} layer, from {window.name}.";
                return summary;
            }

            summary.Answers.Add(
                new Answer("Back", layer.Back == LayerBack.ClosesTheView ? "Closes this" : "Ignored", fromLayer: true)
            );
            string input = layer.InputWhileOpen switch
            {
                LayerInput.EveryoneToUI => "Everyone to UI",
                LayerInput.OwnerToUI => "Owner to UI",
                _ => "No change",
            };
            summary.Answers.Add(
                new Answer(
                    "Input",
                    input,
                    note: window.InputDelay > 0f ? $"after {window.InputDelay:0.##} s" : null,
                    fromLayer: true
                )
            );

            string others = layer.Others switch
            {
                LayerOthers.HideUntilThisCloses => "hide until this closes",
                LayerOthers.Close => "close when this opens",
                LayerOthers.RefuseTheNewOne => "can't open while this is open",
                _ => "stay visible",
            };
            summary.Note = $"{layer.Name} layer. Others on it {others}.";
            return summary;
        }

        private static bool IsTabOf(SentinalView view, SentinalView parent)
        {
            foreach (TabbedView tabs in parent.GetComponentsInChildren<TabbedView>(true))
            foreach (SentinalView panel in tabs.GroupPanels)
                if (panel == view)
                    return true;
            return false;
        }

        private static string Key(Summary summary)
        {
            var key = new StringBuilder();
            foreach (Answer answer in summary.Answers)
            {
                key.Append(answer.Key).Append('\u001f').Append(answer.Text).Append('\u001f');
                key.Append(answer.Link != null ? answer.Link.GetInstanceID() : 0).Append('\u001f');
                key.Append(answer.Note).Append('\u001e');
            }
            key.Append(summary.Note).Append('\u001f').Append(summary.Warning).Append('\u001f');
            key.Append(summary.Layer?.Id ?? 0);
            return key.ToString();
        }

        /// <summary>Replaces every cell after the live State cell, which stays first.</summary>
        private static void DrawAnswers(VisualElement strip, List<Answer> answers, ViewLayer layer)
        {
            while (strip.childCount > 1)
                strip.RemoveAt(strip.childCount - 1);

            foreach (Answer answer in answers)
            {
                var cell = new StatusFact(strip, answer.Key);
                cell.Set(answer.Text, target: answer.Link, note: answer.Note);
                if (answer.FromLayer && layer != null)
                {
                    int layerId = layer.Id;
                    cell.AddToClassList("sentinal-answer--layer");
                    cell.tooltip = LayerTooltip(layer);
                    cell.RegisterCallback<ClickEvent>(_ => SentinalSettingsProvider.Reveal(layerId));
                }
            }
        }

        private static string LayerTooltip(ViewLayer layer) =>
            $"Set by the {layer.Name} layer. Click to edit it in Project Settings › Sentinal.";

#if ENABLE_INPUT_SYSTEM
        [MenuItem("CONTEXT/SentinalView/Add Dismissal Input Handler")]
        private static void AddDismissalHandler(MenuCommand command) =>
            Undo.AddComponent<InputSystem.ViewDismissalInputHandler>(((SentinalView)command.context).gameObject);

        [MenuItem("CONTEXT/SentinalView/Add Dismissal Input Handler", true)]
        private static bool ValidateAddDismissalHandler(MenuCommand command) =>
            command.context is SentinalView view && !view.TryGetComponent(out InputSystem.ViewDismissalInputHandler _);
#endif
    }
}
