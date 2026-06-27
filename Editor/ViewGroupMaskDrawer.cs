using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    /// <summary>
    /// Custom property drawer for displaying a group mask selection UI.
    /// </summary>
    [CustomPropertyDrawer(typeof(ViewGroupMask))]
    public class ViewGroupMaskDrawer : PropertyDrawer
    {
        private ViewGroupConfig config;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            SerializedProperty valueProp = property.FindPropertyRelative("value");
            if (valueProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Invalid ViewGroupMask structure.");
                EditorGUI.EndProperty();
                return;
            }

            if (config == null)
            {
                config = ViewGroupConfig.LoadShared();
                if (config == null)
                {
                    // Draw standard prefix label aligned with all inspector fields
                    Rect controlRect = EditorGUI.PrefixLabel(position, label);

                    GUIContent btnContent = new GUIContent(
                        " Config Missing (Click to Create)",
                        EditorGUIUtility.IconContent("console.warnicon.sml")?.image,
                        "ViewGroupConfig asset not found in Resources. Click to create SentinalViewGroups asset."
                    );

                    if (GUI.Button(controlRect, btnContent, EditorStyles.miniButton))
                    {
                        ViewGroupConfig.EnsureSharedInProject();
                        config = ViewGroupConfig.LoadShared();
                        if (config != null)
                        {
                            EditorGUIUtility.PingObject(config);
                        }
                        GUI.changed = true;
                    }

                    EditorGUI.EndProperty();
                    return;
                }
            }

            int groupMask = valueProp.intValue;
            int groupCount = config.GroupCount;

            string[] groupNames = new string[groupCount];
            for (int i = 0; i < groupCount; i++)
                groupNames[i] = config.GetGroupName(i) ?? $"Group {i}";

            int newMask = EditorGUI.MaskField(position, label, groupMask, groupNames);
            if (newMask != groupMask)
                valueProp.intValue = newMask;

            EditorGUI.EndProperty();
        }
    }
}
