#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using Sentinal.InputSystem;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewInputSystemHandler))]
    public class ViewInputSystemHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var handler = target as ViewInputSystemHandler;

            if (Application.isPlaying)
            {
                bool enabled = handler.IsInputEnabled();
                string statusText = enabled ? "INPUT ENABLED" : "INPUT DISABLED";
                Color color = enabled ? EditorColors.Signal : EditorColors.Info;

                var playerInput = handler.GetPlayerInput();
                if (playerInput != null)
                {
                    statusText +=
                        $" | PlayerIndex: {playerInput.playerIndex} | Name: {handler.GetTrackingPlayerInputName()} | Map: {playerInput.currentActionMap?.name ?? "NONE"}";
                }
                else
                {
                    statusText +=
                        handler.GetInputSource() == PlayerInputSource.SentinalPlayerRole
                            ? $" | WAITING FOR ROLE: {handler.GetPlayerKey()}"
                            : handler.GetInputSource() == PlayerInputSource.PlayerInputIndex
                                ? $" | WAITING FOR INDEX: {handler.GetPlayerInputIndex()}"
                                : " | WAITING FOR DIRECT PLAYER";
                    if (enabled)
                        color = EditorColors.Caution;
                }

                TerminalGUI.DrawStatusBox(statusText, color);
            }

            SerializedProperty inputSourceProp = serializedObject.FindProperty("inputSource");
            SerializedProperty playerKeyProp = serializedObject.FindProperty("playerKey");
            SerializedProperty directPlayerInputProp = serializedObject.FindProperty("directPlayerInput");
            SerializedProperty playerInputIndexProp = serializedObject.FindProperty("playerInputIndex");

            EditorGUILayout.PropertyField(inputSourceProp);
            if ((PlayerInputSource)inputSourceProp.enumValueIndex == PlayerInputSource.SentinalPlayerRole)
                EditorGUILayout.PropertyField(playerKeyProp);
            else if ((PlayerInputSource)inputSourceProp.enumValueIndex == PlayerInputSource.PlayerInputIndex)
                EditorGUILayout.PropertyField(playerInputIndexProp);
            else
                EditorGUILayout.PropertyField(directPlayerInputProp);

            DrawRemainingFields("m_Script", "inputSource", "playerKey", "directPlayerInput", "playerInputIndex");

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawRemainingFields(params string[] excluded)
        {
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (System.Array.IndexOf(excluded, iterator.name) >= 0)
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }
        }
    }
}
#endif
