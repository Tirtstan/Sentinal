#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using Sentinal.InputSystem;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewDismissalInputHandler))]
    public class ViewDismissalInputHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var handler = target as ViewDismissalInputHandler;

            if (Application.isPlaying)
            {
                if (handler.GetPlayerInput() != null)
                {
                    TerminalGUI.DrawStatusBox(
                        $"CONNECTED | Name: {handler.GetTrackingPlayerInputName()}",
                        EditorColors.Connected
                    );
                }
                else
                {
                    string waitText =
                        handler.GetInputSource() == PlayerInputSource.SentinalPlayerRole
                            ? $"WAITING | NO PLAYER FOR ROLE {handler.GetPlayerKey()}"
                            : handler.GetInputSource() == PlayerInputSource.PlayerInputIndex
                                ? $"WAITING | NO PLAYER AT INDEX {handler.GetPlayerInputIndex()}"
                                : "WAITING | NO DIRECT PLAYER";
                    TerminalGUI.DrawStatusBox(waitText, EditorColors.Caution);
                }
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
