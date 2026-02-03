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
        private SerializedProperty onEnabledActionMapsProperty;
        private SerializedProperty onDisabledActionMapsProperty;

        private void OnEnable()
        {
            inputOnlyWhenCurrentProperty = serializedObject.FindProperty("inputOnlyWhenCurrent");
            viewSelectorProperty = serializedObject.FindProperty("viewSelector");
            playerInputProperty = serializedObject.FindProperty("playerInput");
            playerIndexProperty = serializedObject.FindProperty("playerIndex");
            applyToAllPlayersProperty = serializedObject.FindProperty("applyToAllPlayers");
            onEnabledActionMapsProperty = serializedObject.FindProperty("onEnabledActionMaps");
            onDisabledActionMapsProperty = serializedObject.FindProperty("onDisabledActionMaps");
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
            EditorGUILayout.PropertyField(onEnabledActionMapsProperty, true);
            EditorGUILayout.PropertyField(onDisabledActionMapsProperty, true);

            serializedObject.ApplyModifiedProperties();

            ViewInputSystemHandler handler = target as ViewInputSystemHandler;

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space(4);

            bool inputEnabled = handler.IsInputEnabled();
            PlayerInput playerInput = handler.GetPlayerInput();

            DrawInfoBox(() =>
            {
                var onEnabledMaps = handler.GetOnEnabledActionMaps();
                var onDisabledMaps = handler.GetOnDisabledActionMaps();
                bool hasConfig =
                    (onEnabledMaps != null && onEnabledMaps.Length > 0)
                    || (onDisabledMaps != null && onDisabledMaps.Length > 0);

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

                    if (onEnabledMaps != null && onEnabledMaps.Length > 0)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        foreach (var cfg in onEnabledMaps)
                        {
                            if (cfg == null || string.IsNullOrEmpty(cfg.actionMapName))
                                continue;

                            parts.Add($"{cfg.actionMapName} ({(cfg.enable ? "Enable" : "Disable")})");
                        }

                        if (parts.Count > 0)
                        {
                            configInfo += $"On Enabled: {string.Join(", ", parts)}";
                        }
                    }

                    if (onDisabledMaps != null && onDisabledMaps.Length > 0)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        foreach (var cfg in onDisabledMaps)
                        {
                            if (cfg == null || string.IsNullOrEmpty(cfg.actionMapName))
                                continue;

                            parts.Add($"{cfg.actionMapName} ({(cfg.enable ? "Enable" : "Disable")})");
                        }

                        if (parts.Count > 0)
                        {
                            if (configInfo.Length > 0)
                                configInfo += "  |  ";

                            configInfo += $"On Disabled: {string.Join(", ", parts)}";
                        }
                    }

                    if (!string.IsNullOrEmpty(configInfo))
                        infoLine += $"  |  Config: {configInfo}";
                }

                // Apply color style to the whole line if input is enabled
                var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                if (inputEnabled)
                {
                    statusStyle.normal.textColor = SentinalEditorColors.AccentColor;
                }

                EditorGUILayout.LabelField(infoLine, statusStyle);
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
