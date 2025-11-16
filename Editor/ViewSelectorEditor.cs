using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewSelector))]
    public class ViewSelectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
                return;

            if (SentinalManager.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "Sentinal is not initialized. Ensure it is present in the scene.",
                    MessageType.Warning
                );
                return;
            }

            ViewSelector sentinalView = target as ViewSelector;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);

            int index = SentinalManager.Instance.GetViewIndex(sentinalView);
            string indexString = index >= 0 ? index.ToString() : "Not in history";

            EditorGUILayout.LabelField("View Index", indexString);
            EditorGUILayout.LabelField(
                "Is Current View?",
                (SentinalManager.Instance.CurrentView == sentinalView).ToString()
            );
        }
    }
}
