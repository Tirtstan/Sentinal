#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewInputSystemComponent), true)]
    [CanEditMultipleObjects]
    public class ViewInputSystemComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;

            ViewInputSystemComponent component = target as ViewInputSystemComponent;
            if (component == null)
                return;

            EditorGUILayout.Space(4);

            DrawInfoBox(() =>
            {
                PlayerInput playerInput = component.GetPlayerInput();
                ViewInputSystemHandler handler = component.GetViewInputHandler();

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

                if (handler != null)
                {
                    bool inputEnabled = handler.IsInputEnabled();
                    var handlerStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (inputEnabled)
                    {
                        handlerStyle.normal.textColor = SentinalEditorColors.AccentColor;
                    }
                    EditorGUILayout.LabelField(
                        $"Handler Input: {(inputEnabled ? "Enabled" : "Disabled")}",
                        handlerStyle
                    );
                }
                else
                {
                    EditorGUILayout.LabelField("Handler: Not Assigned", EditorStyles.miniLabel);
                }
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
