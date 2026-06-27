#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
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

            var component = target as ViewInputSystemComponent;

            if (Application.isPlaying)
            {
                var player = component.GetPlayerInput();
                var handler = component.GetViewInputHandler();

                string statusText = "";
                Color color;

                if (handler != null)
                {
                    bool inputEnabled = handler.IsInputEnabled();
                    statusText += inputEnabled ? "HANDLER: ENABLED" : "HANDLER: DISABLED";
                    color = inputEnabled ? EditorColors.Signal : EditorColors.Info;
                }
                else
                {
                    statusText += "HANDLER: NOT FOUND";
                    color = EditorColors.Offline;
                }

                if (player != null)
                {
                    statusText += $" | PlayerIndex: {player.playerIndex} | Map: {player.currentActionMap?.name ?? "NONE"}";
                }
                else
                {
                    statusText += " | WAITING FOR PLAYER INPUT";
                    if (handler != null && handler.IsInputEnabled())
                        color = EditorColors.Caution;
                }

                TerminalGUI.DrawStatusBox(statusText, color);
            }

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.name != "m_Script")
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                } while (iterator.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }
}
#endif
