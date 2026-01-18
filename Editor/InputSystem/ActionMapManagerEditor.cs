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

        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime information will be displayed when playing.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);

            DrawInfoBox(() =>
            {
                EditorGUILayout.LabelField($"Players: {PlayerInput.all.Count}", EditorStyles.miniLabel);
            });

            EditorGUILayout.Space(4);

            // History section
            ActionMapManager manager = target as ActionMapManager;
            var history = manager.GetHistory();

            EditorGUILayout.BeginHorizontal();
            showHistoryFoldout = EditorGUILayout.Foldout(
                showHistoryFoldout,
                $"History ({history.Count})",
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
                    EditorGUILayout.HelpBox("No action map changes recorded yet.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.Space(2);
                    // Show newest first
                    for (int i = history.Count - 1; i >= 0; i--)
                    {
                        var entry = history[i];
                        DrawInfoBox(() =>
                        {
                            float timeSince = Time.time - entry.timestamp;
                            string timeStr = timeSince < 1f ? $"{timeSince:F2}s" : $"{timeSince:F1}s";

                            var actionStyle = new GUIStyle(EditorStyles.miniLabel);
                            if (entry.action == "Enable")
                            {
                                actionStyle.normal.textColor = SentinalEditorColors.AccentColor;
                            }

                            EditorGUILayout.LabelField(
                                $"[{timeStr} ago]  Player {entry.playerIndex}  |  {entry.action}: {string.Join(", ", entry.mapNames)}  |  Source: {entry.source}",
                                actionStyle
                            );
                        });

                        if (i > 0)
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
