using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    public static class EditorColors
    {
        public static Color PanelBg =>
            EditorGUIUtility.isProSkin ? new Color(0.14f, 0.15f, 0.17f, 1f) : new Color(0.92f, 0.93f, 0.95f, 1f);

        public static readonly Color Signal = new Color(0.47f, 0.71f, 0.88f, 1f);
        public static readonly Color Caution = new Color(0.86f, 0.66f, 0.34f, 1f);
        public static readonly Color Connected = new Color(0.43f, 0.72f, 0.56f, 1f);
        public static readonly Color Offline = new Color(0.80f, 0.48f, 0.48f, 1f);
        public static readonly Color Info = new Color(0.63f, 0.68f, 0.75f, 1f);

        public static readonly Color Wire = new Color(0.42f, 0.46f, 0.52f, 0.45f);
        public static readonly Color RowBg = new Color(1f, 1f, 1f, 0.03f);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
