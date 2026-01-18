using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    /// <summary>
    /// Centralized color palette for Sentinal editor UI.
    /// Provides consistent colors across all custom inspectors.
    /// </summary>
    public static class SentinalEditorColors
    {
        public static Color BoxColor =>
            EditorGUIUtility.isProSkin ? new Color(0.5f, 0.5f, 0.5f, 0.15f) : new Color(0, 0, 0, 0.08f);

        public static readonly Color AccentColor = new Color(0.4f, 0.7f, 0.9f, 1f);
        public static readonly Color WarningColor = new Color(0.95f, 0.8f, 0.2f, 1f);
    }
}
