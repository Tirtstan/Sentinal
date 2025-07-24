using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(SentinalViewSelector))]
    public class SentinalEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
                return;

            if (Sentinal.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "Sentinal is not initialized. Ensure it is present in the scene.",
                    MessageType.Warning
                );
                return;
            }

            SentinalViewSelector sentinalView = target as SentinalViewSelector;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);

            int index = Sentinal.Instance.GetViewIndex(sentinalView);
            string indexString = index >= 0 ? index.ToString() : "Not in history";

            EditorGUILayout.LabelField("View Index", indexString);
            EditorGUILayout.LabelField("Is Current View?", (Sentinal.Instance.CurrentView == sentinalView).ToString());
        }
    }
}
