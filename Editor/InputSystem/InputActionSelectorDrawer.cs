#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using Sentinal.InputSystem;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(InputActionSelector))]
    public class InputActionSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var useActionNameProp = property.FindPropertyRelative("useActionName");
            var actionNameProp = property.FindPropertyRelative("actionName");
            var actionReferenceProp = property.FindPropertyRelative("actionReference");

            EditorGUI.BeginProperty(position, label, property);

            Rect contentRect = EditorGUI.PrefixLabel(position, label);
            const float buttonWidth = 20f;
            const float gap = 2f;
            float toggleWidth = (buttonWidth * 2f) + gap;

            Rect fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - toggleWidth - 4f, contentRect.height);
            Rect nameBtnRect = new Rect(fieldRect.xMax + 4f, contentRect.y, buttonWidth, contentRect.height);
            Rect refBtnRect = new Rect(nameBtnRect.xMax + gap, contentRect.y, buttonWidth, contentRect.height);

            bool useName = useActionNameProp.boolValue;
            SerializedProperty activeField = useName ? actionNameProp : actionReferenceProp;
            EditorGUI.PropertyField(fieldRect, activeField, GUIContent.none, true);

            bool namePressed = GUI.Toggle(
                nameBtnRect,
                useName,
                new GUIContent("N", "Name: Use action name"),
                EditorStyles.miniButtonLeft
            );
            if (namePressed != useName && namePressed)
            {
                useActionNameProp.boolValue = true;
            }

            bool referencePressed = GUI.Toggle(
                refBtnRect,
                !useName,
                new GUIContent("R", "Reference: Use direct reference"),
                EditorStyles.miniButtonRight
            );
            if (referencePressed == useName && referencePressed)
            {
                useActionNameProp.boolValue = false;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var useActionNameProp = property.FindPropertyRelative("useActionName");
            var actionNameProp = property.FindPropertyRelative("actionName");
            var actionReferenceProp = property.FindPropertyRelative("actionReference");
            SerializedProperty activeField = useActionNameProp.boolValue ? actionNameProp : actionReferenceProp;
            return EditorGUI.GetPropertyHeight(activeField, true);
        }
    }
}
#endif
