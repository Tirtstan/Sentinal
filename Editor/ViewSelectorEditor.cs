using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewSelector))]
    public class ViewSelectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var view = target as ViewSelector;

            if (Application.isPlaying)
            {
                int index = SentinalViewRouter.GetViewIndex(view);
                bool isCurrent = SentinalViewRouter.IsCurrent(view);
                bool isTracked = view.TrackView;

                string statusText = "";
                Color color = EditorColors.Info;

                if (isCurrent)
                {
                    statusText += "CURRENT VIEW";
                    color = EditorColors.Signal;
                }
                else if (index >= 0)
                {
                    statusText += "OPEN (BACKGROUND)";
                    color = EditorColors.Connected;
                }
                else if (view.IsActive && !isTracked)
                {
                    statusText += "ACTIVE (UNTRACKED)";
                    color = EditorColors.Caution;
                }
                else
                {
                    statusText += "CLOSED";
                }

                statusText += $" | INDEX: {(index >= 0 ? index.ToString() : "N/A")}";

                if (view.RootView)
                    statusText += " | ROOT";
                if (view.ExclusiveView)
                    statusText += " | EXCLUSIVE";
                if (view.HideOtherViews)
                    statusText += " | HIDES OTHERS";

                TerminalGUI.DrawStatusBox(statusText, color);
            }

            DrawDefaultInspectorFields();

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawDefaultInspectorFields()
        {
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

#if ENABLE_INPUT_SYSTEM
        [MenuItem("CONTEXT/ViewSelector/Add Action Map Gate")]
        private static void AddActionMapGate(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            Undo.AddComponent<InputSystem.ActionMapGate>(view.gameObject);
        }

        [MenuItem("CONTEXT/ViewSelector/Add Action Map Gate", true)]
        private static bool ValidateAddActionMapGate(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            return view != null && !view.TryGetComponent(out InputSystem.ActionMapGate _);
        }

        [MenuItem("CONTEXT/ViewSelector/Add View Input System Handler")]
        private static void AddInputSystemHandler(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            Undo.AddComponent<InputSystem.ViewInputSystemHandler>(view.gameObject);
        }

        [MenuItem("CONTEXT/ViewSelector/Add View Input System Handler", true)]
        private static bool ValidateAddInputSystemHandler(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            return view != null && !view.TryGetComponent(out InputSystem.ViewInputSystemHandler _);
        }

        [MenuItem("CONTEXT/ViewSelector/Add Dismissal Input Handler")]
        private static void AddDismissalHandler(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            Undo.AddComponent<InputSystem.ViewDismissalInputHandler>(view.gameObject);
        }

        [MenuItem("CONTEXT/ViewSelector/Add Dismissal Input Handler", true)]
        private static bool ValidateAddDismissalHandler(MenuCommand command)
        {
            var view = command.context as ViewSelector;
            return view != null && !view.TryGetComponent(out InputSystem.ViewDismissalInputHandler _);
        }
#endif
    }
}
