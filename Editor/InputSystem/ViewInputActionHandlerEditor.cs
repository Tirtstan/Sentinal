#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
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

            var handler = target as ViewInputActionHandler;

            if (Application.isPlaying)
            {
                bool shouldSubscribe = handler.ShouldSubscribe();
                string statusText = shouldSubscribe ? "SUBSCRIBED" : "NOT SUBSCRIBED";
                Color color = shouldSubscribe ? EditorColors.Signal : EditorColors.Info;

                var player = handler.GetPlayerInput();
                if (player != null)
                {
                    statusText += $" | PlayerIndex: {player.playerIndex} | Map: {player.currentActionMap?.name ?? "NONE"}";
                    if (!shouldSubscribe)
                        color = EditorColors.Caution;
                }
                else
                {
                    statusText += " | NO PLAYER INPUT ASSIGNED";
                    if (shouldSubscribe)
                        color = EditorColors.Caution;
                }

                statusText += $" | MODE: {handler.InputWhenCurrentMode}";
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
