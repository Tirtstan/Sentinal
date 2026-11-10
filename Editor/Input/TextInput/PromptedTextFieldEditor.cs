using Sentinal.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor.Input
{
    [CustomEditor(typeof(PromptedTextField)), CanEditMultipleObjects]
    public sealed class PromptedTextFieldEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = SentinalEditorUI.Root();
            VisualElement strip = SentinalEditorUI.Status(root);
            var state = new StatusFact(strip, "Prompt", showTone: true);
            var activity = new StatusFact(strip, "Presenter");
            SentinalEditorUI.DefaultFields(root, serializedObject);

            void Refresh()
            {
                if (target == null)
                    return;
                if (serializedObject.isEditingMultipleObjects)
                {
                    state.Set($"{targets.Length} selected");
                    activity.AddToClassList("sentinal-hidden");
                    return;
                }
                if (!Application.isPlaying)
                {
                    state.Set("Opens on click");
                    activity.Set("Registered at runtime");
                    return;
                }
                if (!TextInputGateway.IsAvailable)
                {
                    state.Set("Unavailable", SentinalTone.Warning);
                    activity.Set("None registered", SentinalTone.Warning);
                    activity.tooltip =
                        "Expected a registered text input presenter; actual: none. Register one at runtime.";
                    return;
                }

                bool waiting = ((PromptedTextField)target).IsPromptOpen;
                state.Set(waiting ? "Prompt open" : "Ready", waiting ? SentinalTone.Open : SentinalTone.Idle);
                activity.Set(
                    waiting
                        ? "Editing this field"
                        : TextInputGateway.IsShowing
                            ? "Editing another field"
                            : "Available"
                );
            }

            root.schedule.Execute(Refresh).Every(100);
            Refresh();
            return root;
        }

        [MenuItem("CONTEXT/Button/Add Prompted Text Field")]
        private static void AddPromptedTextFieldFromButton(MenuCommand command)
        {
            if (command.context is Component component && !component.TryGetComponent(out PromptedTextField _))
                Undo.AddComponent<PromptedTextField>(component.gameObject);
        }
    }
}
