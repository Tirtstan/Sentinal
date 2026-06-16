using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    /// <summary>
    /// Custom property drawer for displaying a group mask selection UI.
    /// </summary>
    [CustomPropertyDrawer(typeof(ViewGroupMaskAttribute))]
    public class ViewGroupMaskDrawer : PropertyDrawer
    {
        private ViewGroupConfig config;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "Use ViewGroupMaskAttribute with an int field.");
                return;
            }

            if (config == null)
            {
                config = ViewGroupConfig.LoadShared();
                if (config == null)
                {
                    // Draw a button to create the config if it's missing
                    Rect warningRect = new Rect(position.x, position.y, position.width - 100, position.height);
                    Rect btnRect = new Rect(position.xMax - 95, position.y, 95, position.height);

                    EditorGUI.LabelField(warningRect, label.text, "Config Missing");
                    if (GUI.Button(btnRect, "Create Config"))
                    {
                        ViewGroupConfig.EnsureSharedInProject();
                        config = ViewGroupConfig.LoadShared();
                    }
                    return;
                }
            }

            int groupMask = property.intValue;
            int groupCount = config.Groups.Count;

            string[] groupNames = new string[groupCount];
            for (int i = 0; i < groupCount; i++)
                groupNames[i] = config.GetGroupName(i) ?? $"Group {i}";

            EditorGUI.BeginProperty(position, label, property);

            int newMask = EditorGUI.MaskField(position, label, groupMask, groupNames);
            if (newMask != groupMask)
                property.intValue = newMask;

            EditorGUI.EndProperty();
        }
    }
}
