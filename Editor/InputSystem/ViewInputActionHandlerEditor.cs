#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewInputActionHandler), true)]
    [CanEditMultipleObjects]
    public class ViewInputActionHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;

            ViewInputActionHandler handler = target as ViewInputActionHandler;
            if (handler == null)
                return;

            EditorGUILayout.Space(4);

            DrawInfoBox(() =>
            {
                PlayerInput playerInput = handler.GetPlayerInput();
                ViewInputSystemHandler viewHandler = handler.GetViewInputHandler();

                // Player Input status
                if (playerInput != null)
                {
                    var playerStyle = new GUIStyle(EditorStyles.miniLabel);
                    playerStyle.normal.textColor = SentinalEditorColors.AccentColor;
                    EditorGUILayout.LabelField($"Player Input: Assigned (P{playerInput.playerIndex})", playerStyle);
                    EditorGUILayout.LabelField(
                        $"Current Map: {playerInput.currentActionMap?.name ?? "None"}",
                        EditorStyles.miniLabel
                    );
                }
                else
                {
                    EditorGUILayout.LabelField("Player Input: Not Assigned", EditorStyles.miniLabel);
                }

                // Handler input status
                if (viewHandler != null)
                {
                    bool handlerInputEnabled = viewHandler.IsInputEnabled();
                    var handlerStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (handlerInputEnabled)
                    {
                        handlerStyle.normal.textColor = SentinalEditorColors.AccentColor;
                    }
                    EditorGUILayout.LabelField(
                        $"Handler Input: {(handlerInputEnabled ? "Enabled" : "Disabled")}",
                        handlerStyle
                    );
                }

                // Should Subscribe status
                bool shouldSubscribe = handler.ShouldSubscribe();
                var subscribeStyle = new GUIStyle(EditorStyles.miniLabel);
                if (shouldSubscribe)
                {
                    subscribeStyle.normal.textColor = SentinalEditorColors.AccentColor;
                    subscribeStyle.fontStyle = FontStyle.Bold;
                }
                EditorGUILayout.LabelField($"Input Subscribed: {(shouldSubscribe ? "Yes" : "No")}", subscribeStyle);

                // Input mode
                string modeText = handler.InputWhenCurrentMode switch
                {
                    InputWhenCurrentMode.Inherit => "Inherit from Handler",
                    InputWhenCurrentMode.AlwaysEnabled => "Always Enabled",
                    InputWhenCurrentMode.AlwaysDisabled => "Always Disabled",
                    _ => "Unknown",
                };
                EditorGUILayout.LabelField($"Mode: {modeText}", EditorStyles.miniLabel);
            });

            if (Application.isPlaying)
                Repaint();
        }

        private void DrawInfoBox(System.Action content)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = SentinalEditorColors.BoxColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;

            content?.Invoke();

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
