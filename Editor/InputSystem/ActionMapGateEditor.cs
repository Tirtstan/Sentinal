#if ENABLE_INPUT_SYSTEM
using Sentinal.InputSystem;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ActionMapGate))]
    public class ActionMapGateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var gate = target as ActionMapGate;

            if (Application.isPlaying)
            {
                if (gate.IsApplied)
                {
                    string modeStr =
                        gate.Mode == ActionMapGate.GateMode.Exclusive
                            ? $"EXCLUSIVE MAP: {gate.ExclusiveMapName}"
                            : "CONFIG MODE";
                    TerminalGUI.DrawStatusBox($"GATE ACTIVE | {modeStr}", EditorColors.Signal);
                }
                else
                {
                    TerminalGUI.DrawStatusBox("GATE IDLE | WAITING FOR FOCUS", EditorColors.Info);
                }
            }

            var targetPlayersProp = serializedObject.FindProperty("targetPlayers");
            var playerKeyProp = serializedObject.FindProperty("playerKey");
            var modeProp = serializedObject.FindProperty("mode");
            var actionMapsProp = serializedObject.FindProperty("actionMaps");
            var exclusiveMapNameProp = serializedObject.FindProperty("exclusiveMapName");

            EditorGUILayout.PropertyField(targetPlayersProp);

            if (targetPlayersProp.enumValueIndex == (int)ActionMapGate.TargetPlayers.SpecificKey)
            {
                EditorGUILayout.PropertyField(playerKeyProp);
            }

            EditorGUILayout.PropertyField(modeProp);

            if (modeProp.enumValueIndex == (int)ActionMapGate.GateMode.Exclusive)
            {
                EditorGUILayout.PropertyField(exclusiveMapNameProp);
            }
            else
            {
                EditorGUILayout.PropertyField(actionMapsProp, true);
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
