using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(SentinalManager))]
    public class SentinalManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SentinalManager manager = (SentinalManager)target;

            EditorGUILayout.Space();

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField("Sentinal Manager", headerStyle);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Views Open: {manager.ViewCount}");
            EditorGUILayout.LabelField($"Current View: {(manager.CurrentView ? manager.CurrentView.name : "None")}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            if (manager.ViewCount > 0)
            {
                EditorGUILayout.LabelField("View History:", EditorStyles.boldLabel);

                var views = manager.GetViewHistory();
                for (int i = 0; i < views.Length; i++)
                {
                    var view = views[i];
                    if (!view)
                        continue;

                    bool isCurrent = view == manager.CurrentView;

                    EditorGUILayout.BeginVertical("box");

                    string namePrefix = isCurrent ? "► " : $"[{i}] ";
                    string parentName = view.transform.parent ? $" ({view.transform.parent.name})" : "";

                    var titleStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(0.9f, 0.95f, 1f, 1f) },
                    };
                    EditorGUILayout.LabelField($"{namePrefix}{view.name}{parentName}", titleStyle);

                    var propStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };

                    string properties = "";
                    if (view.IsRootView())
                        properties += "Root ";
                    if (view.IsExclusiveView())
                        properties += "Exclusive ";
                    if (view.HasPreventSelection())
                        properties += "No-Selection ";
                    if (string.IsNullOrEmpty(properties))
                        properties = "Default";

                    EditorGUILayout.LabelField($"Type: {properties.Trim()}", propStyle);
                    EditorGUILayout.LabelField($"Active: {view.gameObject.activeInHierarchy}", propStyle);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = manager.CurrentView && !manager.CurrentView.IsRootView();
            if (GUILayout.Button("Close Current", GUILayout.Height(25)))
                manager.CloseCurrentView();

            GUI.enabled = manager.ViewCount > 0;
            if (GUILayout.Button("Close All", GUILayout.Height(25)))
                manager.CloseAllViews();

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                Repaint();
        }
    }
}
