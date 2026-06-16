using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    public static class TerminalGUI
    {
        public static void DrawStatusBox(string text, Color stateColor)
        {
            var wrapper = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 6, 6),
                padding = new RectOffset(0, 0, 0, 0)
            };

            using (new EditorGUILayout.VerticalScope(wrapper))
            {
                Rect rect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
                rect.height = 26f;

                Color background = EditorColors.WithAlpha(stateColor, 0.08f);
                EditorGUI.DrawRect(rect, background);

                Rect accent = new Rect(rect.x, rect.y, 3f, rect.height);
                EditorGUI.DrawRect(accent, EditorColors.WithAlpha(stateColor, 0.9f));

                var textStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Bold
                };
                textStyle.normal.textColor = EditorColors.WithAlpha(stateColor, 0.98f);

                Rect labelRect = new Rect(rect.x + 8f, rect.y, rect.width - 10f, rect.height);
                EditorGUI.LabelField(labelRect, text, textStyle);
            }
        }
    }
}
