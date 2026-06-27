using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(CreateAssetButtonAttribute))]
    public class CreateAssetButtonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var attr = (CreateAssetButtonAttribute)attribute;
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                if (property.objectReferenceValue != null)
                {
                    EditorGUI.PropertyField(position, property, label, true);
                    return;
                }

                const float buttonWidth = 54f;
                const float spacing = 4f;
                Rect fieldRect = new Rect(
                    position.x,
                    position.y,
                    position.width - buttonWidth - spacing,
                    position.height
                );
                Rect buttonRect = new Rect(fieldRect.xMax + spacing, position.y, buttonWidth, position.height);

                EditorGUI.PropertyField(fieldRect, property, label, true);

                if (!GUI.Button(buttonRect, "+ New", EditorStyles.miniButton))
                    return;

                var fieldType = fieldInfo.FieldType;
                if (!typeof(ScriptableObject).IsAssignableFrom(fieldType))
                {
                    Debug.LogWarning(
                        $"[Sentinal] CreateAssetButton only supports ScriptableObject fields. Field: {fieldInfo.Name}"
                    );
                    return;
                }

                string folderPath = string.IsNullOrWhiteSpace(attr.FolderPath) ? "Assets" : attr.FolderPath;
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string baseName = property.serializedObject.targetObject.name;
                string suffix = string.IsNullOrWhiteSpace(attr.FileNameSuffix) ? "Asset" : attr.FileNameSuffix;
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{baseName}{suffix}.asset");
                var asset = ScriptableObject.CreateInstance(fieldType);
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUIUtility.PingObject(asset);

                property.objectReferenceValue = asset;
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }
        }
    }
}
