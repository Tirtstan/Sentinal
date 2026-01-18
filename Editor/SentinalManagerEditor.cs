using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using Sentinal.InputSystem;
#endif

namespace Sentinal.Editor
{
    [CustomEditor(typeof(SentinalManager))]
    public class SentinalManagerEditor : UnityEditor.Editor
    {
        private bool viewHistoryFoldout = true;

        public override void OnInspectorGUI()
        {
            SentinalManager manager = (SentinalManager)target;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Sentinal Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawInfoBox(() =>
            {
                EditorGUILayout.LabelField($"Views Open: {manager.ViewCount}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Current: {(manager.CurrentView ? manager.CurrentView.name : "None")}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Most Recent: {(manager.MostRecentView ? manager.MostRecentView.name : "None")}",
                    EditorStyles.miniLabel
                );
            });

            if (manager.ViewCount > 0)
            {
                EditorGUILayout.Space(4);
                viewHistoryFoldout = EditorGUILayout.Foldout(
                    viewHistoryFoldout,
                    "View History",
                    true,
                    EditorStyles.foldoutHeader
                );

                if (viewHistoryFoldout)
                {
                    var views = manager.GetViewHistory();
                    for (int i = 0; i < views.Length; i++)
                    {
                        var view = views[i];
                        if (!view)
                            continue;

                        bool isCurrent = view == manager.CurrentView;

                        EditorGUILayout.Space(2);
                        DrawInfoBox(() =>
                        {
                            string index = $"[{i}] ";
                            string namePrefix = isCurrent ? "► " + index : index;
                            string parentName = view.transform.parent ? $" (P: {view.transform.parent.name})" : "";

                            var titleStyle = new GUIStyle(EditorStyles.miniLabel);
                            if (isCurrent)
                            {
                                titleStyle.normal.textColor = SentinalEditorColors.AccentColor;
                                titleStyle.fontStyle = FontStyle.Bold;
                            }
                            EditorGUILayout.LabelField($"{namePrefix}{view.name}{parentName}", titleStyle);

                            string properties = "";
                            if (view.PreventDismissal)
                                properties += "Dismissal-Protected ";
                            if (view.ExclusiveView)
                                properties += "Exclusive ";
                            if (view.PreventSelection)
                                properties += "No-Selection ";
                            if (string.IsNullOrEmpty(properties))
                                properties = "Default";

                            EditorGUILayout.LabelField(
                                $"Type: {properties.Trim()}  |  Priority: {view.Priority}  |  Active: {view.gameObject.activeInHierarchy}",
                                EditorStyles.miniLabel
                            );

                            IViewInputToggle inputHandler = null;
#if ENABLE_INPUT_SYSTEM
                            if (view.TryGetComponent(out ViewInputSystemHandler systemHandler))
                                inputHandler = systemHandler;
                            else
#endif
                                inputHandler = view.GetComponent<ViewInputToggleBase>();

                            if (inputHandler != null)
                            {
                                bool inputEnabled = inputHandler.IsInputEnabled();
                                var inputStyle = new GUIStyle(EditorStyles.miniLabel);
                                if (inputEnabled)
                                {
                                    inputStyle.normal.textColor = SentinalEditorColors.AccentColor;
                                }
                                EditorGUILayout.LabelField(
                                    $"Input: {(inputEnabled ? "Enabled" : "Disabled")}",
                                    inputStyle
                                );
                            }
                        });
                    }
                }
            }

#if ENABLE_INPUT_SYSTEM
            ViewDismissalInputHandler viewDismissalInputHandler = manager.GetComponent<ViewDismissalInputHandler>();
            ActionMapManager actionMapManager = manager.GetComponent<ActionMapManager>();

            if (viewDismissalInputHandler == null || actionMapManager == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Some input system components are recommended. Add them below if needed.",
                    MessageType.Info
                );

                BeginResponsiveHorizontal(400f);

                if (viewDismissalInputHandler == null)
                {
                    if (GUILayout.Button("Add View Dismissal Handler", GUILayout.Height(24)))
                    {
                        manager.gameObject.AddComponent<ViewDismissalInputHandler>();
                        EditorUtility.SetDirty(manager);
                    }
                }

                if (actionMapManager == null)
                {
                    if (GUILayout.Button("Add Action Map Manager", GUILayout.Height(24)))
                    {
                        manager.gameObject.AddComponent<ActionMapManager>();
                        EditorUtility.SetDirty(manager);
                    }
                }

                EndResponsiveHorizontal(400f);
            }
#endif

            EditorGUILayout.Space(4);
            BeginResponsiveHorizontal(350f);

            GUI.enabled = manager.CurrentView && !manager.CurrentView.PreventDismissal;
            if (GUILayout.Button("Close Current", GUILayout.Height(24)))
                manager.CloseCurrentView();

            GUI.enabled = manager.ViewCount > 0;
            if (GUILayout.Button("Close All", GUILayout.Height(24)))
                manager.CloseAllViews();

            GUI.enabled = true;

            EndResponsiveHorizontal(350f);

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

        private void BeginResponsiveHorizontal(float minWidth = 300f)
        {
            if (EditorGUIUtility.currentViewWidth < minWidth)
                EditorGUILayout.BeginVertical();
            else
                EditorGUILayout.BeginHorizontal();
        }

        private void EndResponsiveHorizontal(float minWidth = 300f)
        {
            if (EditorGUIUtility.currentViewWidth < minWidth)
                EditorGUILayout.EndVertical();
            else
                EditorGUILayout.EndHorizontal();
        }
    }
}
