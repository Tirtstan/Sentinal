#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Sentinal.InputSystem;
using System.Collections.Generic;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ActionMapManager))]
    public class ActionMapManagerEditor : UnityEditor.Editor
    {
        private readonly Dictionary<int, bool> playerFoldouts = new();
        private bool showHistoryFoldout = false;
        private SerializedProperty useDefaultActionMapsProperty;
        private SerializedProperty defaultActionMapsProperty;

        private void OnEnable()
        {
            useDefaultActionMapsProperty = serializedObject.FindProperty("useDefaultActionMaps");
            defaultActionMapsProperty = serializedObject.FindProperty("defaultActionMaps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(useDefaultActionMapsProperty);
            EditorGUILayout.PropertyField(defaultActionMapsProperty, true);

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Runtime information will be displayed when playing.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);

            DrawInfoBox(() =>
            {
                EditorGUILayout.LabelField($"Players: {PlayerInput.all.Count}", EditorStyles.miniLabel);
            });

            EditorGUILayout.Space(4);

            // History section (one entry per view selector / source; same selector overwrites)
            ActionMapManager manager = target as ActionMapManager;
            var history = manager.GetHistory();
            history.Sort((a, b) => string.Compare(a.source, b.source, System.StringComparison.Ordinal));

            EditorGUILayout.BeginHorizontal();
            showHistoryFoldout = EditorGUILayout.Foldout(
                showHistoryFoldout,
                $"Action maps per view ({history.Count})",
                true,
                EditorStyles.foldoutHeader
            );

            if (history.Count > 0 && GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                manager.ClearHistory();
            }
            EditorGUILayout.EndHorizontal();

            if (showHistoryFoldout)
            {
                if (history.Count == 0)
                {
                    EditorGUILayout.HelpBox("No action map state recorded yet.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.Space(2);
                    foreach (var entry in history)
                    {
                        DrawInfoBox(() =>
                        {
                            var actionStyle = new GUIStyle(EditorStyles.miniLabel);
                            if (entry.action == ActionMapAction.Enable)
                                actionStyle.normal.textColor = SentinalEditorColors.AccentColor;

                            EditorGUILayout.LabelField(
                                $"{entry.source}  |  Player {entry.playerIndex}  {entry.action}: {string.Join(", ", entry.mapNames)}",
                                actionStyle
                            );
                        });

                        EditorGUILayout.Space(2);
                    }
                }
            }

            EditorGUILayout.Space(4);

            // Player details
            foreach (var player in PlayerInput.all)
            {
                if (player == null)
                    continue;

                int playerId = player.playerIndex;
                if (!playerFoldouts.ContainsKey(playerId))
                    playerFoldouts[playerId] = false;

                DrawInfoBox(() =>
                {
                    EditorGUILayout.BeginHorizontal();

                    string currentMap = player.currentActionMap?.name ?? "None";
                    EditorGUILayout.LabelField($"Player {playerId}  |  Map: {currentMap}", EditorStyles.miniLabel);

                    playerFoldouts[playerId] = EditorGUILayout.Foldout(
                        playerFoldouts[playerId],
                        "",
                        true,
                        new GUIStyle { fixedWidth = 15 }
                    );
                    EditorGUILayout.EndHorizontal();

                    if (playerFoldouts[playerId] && player.actions != null)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Action Maps:", EditorStyles.miniBoldLabel);

                        EditorGUI.indentLevel++;
                        foreach (var map in player.actions.actionMaps)
                        {
                            var style = new GUIStyle(EditorStyles.miniLabel);
                            if (map.enabled)
                            {
                                style.normal.textColor = SentinalEditorColors.AccentColor;
                            }
                            EditorGUILayout.LabelField($"{(map.enabled ? "✓" : "○")} {map.name}", style);
                        }
                        EditorGUI.indentLevel--;
                    }
                });

                EditorGUILayout.Space(2);
            }

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
