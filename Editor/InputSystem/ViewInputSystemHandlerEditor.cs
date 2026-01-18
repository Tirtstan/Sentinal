#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Sentinal.InputSystem;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewInputSystemHandler))]
    public class ViewInputSystemHandlerEditor : UnityEditor.Editor
    {
        private SerializedProperty inputOnlyWhenCurrentProperty;
        private SerializedProperty viewSelectorProperty;
        private SerializedProperty playerInputProperty;
        private SerializedProperty playerIndexProperty;
        private SerializedProperty applyToAllPlayersProperty;
        private SerializedProperty enableActionMapsProperty;
        private SerializedProperty disableActionMapsProperty;
        private bool showActionMapsFoldout = false;

        private void OnEnable()
        {
            inputOnlyWhenCurrentProperty = serializedObject.FindProperty("inputOnlyWhenCurrent");
            viewSelectorProperty = serializedObject.FindProperty("viewSelector");
            playerInputProperty = serializedObject.FindProperty("playerInput");
            playerIndexProperty = serializedObject.FindProperty("playerIndex");
            applyToAllPlayersProperty = serializedObject.FindProperty("applyToAllPlayers");
            enableActionMapsProperty = serializedObject.FindProperty("enableActionMaps");
            disableActionMapsProperty = serializedObject.FindProperty("disableActionMaps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(inputOnlyWhenCurrentProperty);
            if (inputOnlyWhenCurrentProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(viewSelectorProperty);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(playerInputProperty);
            EditorGUILayout.PropertyField(playerIndexProperty);

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(applyToAllPlayersProperty);
            EditorGUILayout.PropertyField(enableActionMapsProperty);
            EditorGUILayout.PropertyField(disableActionMapsProperty);

            serializedObject.ApplyModifiedProperties();

            ViewInputSystemHandler handler = target as ViewInputSystemHandler;

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space(4);

            bool inputEnabled = handler.IsInputEnabled();
            PlayerInput playerInput = handler.GetPlayerInput();

            DrawInfoBox(() =>
            {
                var enableMaps = handler.GetEnableActionMaps();
                var disableMaps = handler.GetDisableActionMaps();
                bool hasConfig =
                    (enableMaps != null && enableMaps.Length > 0) || (disableMaps != null && disableMaps.Length > 0);

                string infoLine = "";

                // Input status
                infoLine += $"Input: {(inputEnabled ? "Enabled" : "Disabled")}";

                // Player info
                if (playerInput != null)
                {
                    infoLine +=
                        $"  |  Player {playerInput.playerIndex}  |  Map: {playerInput.currentActionMap?.name ?? "None"}";
                }
                else if (handler.AppliesToAllPlayers())
                {
                    infoLine += $"  |  Target: All Players ({PlayerInput.all.Count})";
                }

                // Config info
                if (hasConfig)
                {
                    string configInfo = "";
                    if (enableMaps != null && enableMaps.Length > 0)
                        configInfo += $"Enable: {string.Join(", ", enableMaps)}";
                    if (disableMaps != null && disableMaps.Length > 0)
                    {
                        if (configInfo.Length > 0)
                            configInfo += ", ";
                        configInfo += $"Disable: {string.Join(", ", disableMaps)}";
                    }
                    infoLine += $"  |  Config: {configInfo}";
                }

                EditorGUILayout.BeginHorizontal();

                // Apply color style to the whole line if input is enabled
                var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                if (inputEnabled)
                {
                    statusStyle.normal.textColor = SentinalEditorColors.AccentColor;
                }
                EditorGUILayout.LabelField(infoLine, statusStyle);

                // Action maps foldout (only show if we have a player input and config)
                if (playerInput != null && hasConfig && playerInput.actions != null)
                {
                    showActionMapsFoldout = EditorGUILayout.Foldout(
                        showActionMapsFoldout,
                        "",
                        true,
                        new GUIStyle { fixedWidth = 15 }
                    );
                }
                EditorGUILayout.EndHorizontal();

                // Expanded action maps list
                if (showActionMapsFoldout && playerInput != null && playerInput.actions != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUI.indentLevel++;
                    foreach (var map in playerInput.actions.actionMaps)
                    {
                        var mapStyle = new GUIStyle(EditorStyles.miniLabel);
                        if (map.enabled)
                        {
                            mapStyle.normal.textColor = SentinalEditorColors.AccentColor;
                        }
                        EditorGUILayout.LabelField($"{(map.enabled ? "✓" : "○")} {map.name}", mapStyle);
                    }
                    EditorGUI.indentLevel--;
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
