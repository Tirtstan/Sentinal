using UnityEditor;
using UnityEngine;

namespace MenuNavigation.Editor
{
    [CustomEditor(typeof(MenuNavigator))]
    public class MenuNavigatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
                return;

            if (MenuNavigatorManager.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "MenuNavigatorManager is not initialized. Ensure it is present in the scene.",
                    MessageType.Warning
                );
                return;
            }

            MenuNavigator menuNavigator = target as MenuNavigator;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Info", EditorStyles.boldLabel);

            int index = MenuNavigatorManager.Instance.GetMenuIndex(menuNavigator);
            string indexString = index >= 0 ? index.ToString() : "Not in history";

            EditorGUILayout.LabelField("Menu Index", indexString);
            EditorGUILayout.LabelField(
                "Is Current Menu?",
                (MenuNavigatorManager.Instance.CurrentMenu == menuNavigator).ToString()
            );
        }
    }
}
