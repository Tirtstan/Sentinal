using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using Sentinal.InputSystem;
#endif

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewSelector))]
    public class ViewSelectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");

            ViewSelector sentinalView = target as ViewSelector;

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;

            if (SentinalManager.Instance == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Sentinal is not initialized. Ensure it is present in the scene.",
                    MessageType.Warning
                );
                return;
            }

            int index = SentinalManager.Instance.GetViewIndex(sentinalView);
            string indexString = index >= 0 ? index.ToString() : "Not in history";
            bool isCurrentView = SentinalManager.Instance.CurrentView == sentinalView;
            bool isMostRecentView = SentinalManager.Instance.MostRecentView == sentinalView;
            bool isActiveButNotTracked = sentinalView.IsActive && !sentinalView.TrackView;

            EditorGUILayout.Space(4);

            DrawInfoBox(() =>
            {
                var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                if (isCurrentView)
                {
                    statusStyle.normal.textColor = SentinalEditorColors.AccentColor;
                }
                else if (isActiveButNotTracked)
                {
                    statusStyle.normal.textColor = SentinalEditorColors.WarningColor;
                }
                EditorGUILayout.LabelField(
                    $"Index: {indexString}  |  Current: {(isCurrentView ? "Yes" : "No")}  |  Recent: {(isMostRecentView ? "Yes" : "No")}",
                    statusStyle
                );
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
