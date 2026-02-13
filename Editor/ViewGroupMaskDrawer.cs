using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    /// <summary>
    /// Custom property drawer for displaying a group mask selection UI similar to Unity's LayerMask.
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
                    config = ViewGroupConfig.EnsureSharedInProject();

                if (config == null)
                {
                    Debug.LogWarning(
                        "Failing to create/load ViewGroupConfig asset. ViewGroupMask cannot be displayed."
                    );
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
