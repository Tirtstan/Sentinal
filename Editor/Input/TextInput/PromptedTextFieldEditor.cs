using Sentinal.Input;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor.Input
{
    [CustomEditor(typeof(PromptedTextField))]
    [CanEditMultipleObjects]
    public class PromptedTextFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var field = (PromptedTextField)target;

            if (Application.isPlaying)
            {
                string gatewayStatus = TextInputGateway.IsAvailable ? "GATEWAY: ONLINE" : "GATEWAY: OFFLINE";
                Color gatewayColor = TextInputGateway.IsAvailable ? EditorColors.Connected : EditorColors.Offline;

                if (TextInputGateway.IsAvailable && TextInputGateway.IsShowing)
                {
                    gatewayStatus += " | PROMPT OPEN";
                    gatewayColor = EditorColors.Caution;
                }

                TerminalGUI.DrawStatusBox(gatewayStatus, gatewayColor);

                string valueStatus = field.IsPromptOpen
                    ? "FIELD: WAITING FOR INPUT"
                    : $"FIELD: {Truncate(field.GetEffectiveValue(), 48)}";
                TerminalGUI.DrawStatusBox(valueStatus, field.IsPromptOpen ? EditorColors.Caution : EditorColors.Info);
            }
            else if (!TextInputGateway.IsAvailable)
            {
                TerminalGUI.DrawStatusBox("GATEWAY: Register a presenter at runtime.", EditorColors.Info);
            }

            DrawDefaultInspector();

            if (Application.isPlaying && GUILayout.Button("Open Prompt"))
                field.OpenPrompt();

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
                Repaint();
        }

        [MenuItem("CONTEXT/Button/Add Prompted Text Field")]
        private static void AddPromptedTextFieldFromButton(MenuCommand command)
        {
            if (command.context is not Component component)
                return;

            var gameObject = component.gameObject;
            if (gameObject.GetComponent<PromptedTextField>() != null)
                return;

            Undo.AddComponent<PromptedTextField>(gameObject);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "(empty)";

            return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
        }
    }
}
