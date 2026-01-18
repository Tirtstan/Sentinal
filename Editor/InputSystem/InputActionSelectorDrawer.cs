#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using Sentinal.InputSystem;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(InputActionSelector))]
    public class InputActionSelectorDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 20f;
        private const float ButtonSpacing = 2f;
        private const float ButtonPadding = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty useActionNameProp = property.FindPropertyRelative("useActionName");
            SerializedProperty actionNameProp = property.FindPropertyRelative("actionName");
            SerializedProperty actionReferenceProp = property.FindPropertyRelative("actionReference");

            float buttonsWidth = (ButtonWidth * 2) + ButtonSpacing;

            Rect fieldPosition = position;
            fieldPosition.width -= buttonsWidth + ButtonPadding;

            var nameButtonRect = new Rect(
                position.xMax - buttonsWidth,
                position.y,
                ButtonWidth,
                EditorGUIUtility.singleLineHeight
            );
            var refButtonRect = new Rect(
                position.xMax - ButtonWidth,
                position.y,
                ButtonWidth,
                EditorGUIUtility.singleLineHeight
            );

            bool useName = useActionNameProp.boolValue;

            var nameContent = new GUIContent(
                "N",
                "Name: Use action name to find actions across specific/all action maps (e.g., 'Submit', 'Cancel', 'UI/Cancel')."
            );
            var refContent = new GUIContent(
                "R",
                "Reference: Use direct reference to a specific action in a specific action map."
            );

            if (GUI.Button(nameButtonRect, nameContent, EditorStyles.miniButtonLeft))
            {
                if (!useName)
                    useActionNameProp.boolValue = true;
            }

            if (GUI.Button(refButtonRect, refContent, EditorStyles.miniButtonRight))
            {
                if (useName)
                    useActionNameProp.boolValue = false;
            }

            if (useName)
            {
                EditorGUI.DrawRect(nameButtonRect, new Color(0.3f, 0.5f, 0.9f, 0.15f));
            }
            else
            {
                EditorGUI.DrawRect(refButtonRect, new Color(0.3f, 0.5f, 0.9f, 0.15f));
            }

            SerializedProperty fieldToShow = useActionNameProp.boolValue ? actionNameProp : actionReferenceProp;
            EditorGUI.PropertyField(fieldPosition, fieldToShow, label, true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty useActionNameProp = property.FindPropertyRelative("useActionName");
            SerializedProperty actionNameProp = property.FindPropertyRelative("actionName");
            SerializedProperty actionReferenceProp = property.FindPropertyRelative("actionReference");

            return EditorGUI.GetPropertyHeight(
                useActionNameProp.boolValue ? actionNameProp : actionReferenceProp,
                label,
                true
            );
        }
    }
}
#endif
