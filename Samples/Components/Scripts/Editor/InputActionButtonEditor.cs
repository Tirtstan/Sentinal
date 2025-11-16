using UnityEditor;
using UnityEngine;

namespace Sentinal.Samples.Editor
{
    [CustomEditor(typeof(InputActionButton))]
    public class InputActionButtonEditor : UnityEditor.Editor
    {
        private SerializedProperty buttonProperty;
        private SerializedProperty inputActionReferenceProperty;
        private SerializedProperty inputWhenFocusProperty;
        private SerializedProperty viewSelectorProperty;
        private SerializedProperty sendPointerEventsProperty;
        private SerializedProperty triggerOnReleaseProperty;

        private void OnEnable()
        {
            buttonProperty = serializedObject.FindProperty("button");
            inputActionReferenceProperty = serializedObject.FindProperty("inputActionReference");
            inputWhenFocusProperty = serializedObject.FindProperty("inputWhenFocus");
            viewSelectorProperty = serializedObject.FindProperty("viewSelector");
            sendPointerEventsProperty = serializedObject.FindProperty("sendPointerEvents");
            triggerOnReleaseProperty = serializedObject.FindProperty("triggerOnRelease");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(buttonProperty);
            EditorGUILayout.PropertyField(inputActionReferenceProperty);
            EditorGUILayout.PropertyField(inputWhenFocusProperty);

            bool inputWhenFocus = inputWhenFocusProperty.boolValue;

            if (inputWhenFocus)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(viewSelectorProperty);
                EditorGUI.indentLevel--;

                if (viewSelectorProperty.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "ViewSelector is required when 'Input When Focus' is enabled. Please assign a ViewSelector reference.",
                        MessageType.Warning
                    );
                }
            }

            EditorGUILayout.PropertyField(sendPointerEventsProperty);
            EditorGUILayout.PropertyField(triggerOnReleaseProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
