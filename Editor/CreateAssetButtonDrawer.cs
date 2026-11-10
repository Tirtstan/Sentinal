using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(CreateAssetButtonAttribute))]
    public sealed class CreateAssetButtonDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // The field and a compact "+ New" button share one line; the button only shows while the field is empty.
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            var field = new ObjectField(property.displayName)
            {
                objectType = fieldInfo.FieldType,
                allowSceneObjects = false,
                tooltip = property.tooltip,
            };
            field.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.BindProperty(property);
            root.Add(field);
            var create = new Button(() =>
            {
                var config = (CreateAssetButtonAttribute)attribute;
                if (!typeof(ScriptableObject).IsAssignableFrom(fieldInfo.FieldType))
                {
                    Debug.LogError(
                        $"[Sentinal] Expected ScriptableObject field for CreateAssetButton; actual: {fieldInfo.FieldType}."
                    );
                    return;
                }
                string folder = string.IsNullOrWhiteSpace(config.FolderPath) ? "Assets" : config.FolderPath;
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{folder}/{property.serializedObject.targetObject.name}{config.FileNameSuffix}.asset"
                );
                ScriptableObject asset = ScriptableObject.CreateInstance(fieldInfo.FieldType);
                AssetDatabase.CreateAsset(asset, path);
                Undo.RegisterCreatedObjectUndo(asset, "Create Sentinal asset");
                property.objectReferenceValue = asset;
                property.serializedObject.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(asset);
            })
            {
                text = "+ New",
                tooltip = "Create a new asset for this field and assign it.",
            };
            create.style.width = 54;
            create.style.flexShrink = 0;
            create.style.marginLeft = 4;
            create.style.marginRight = 3;
            root.Add(create);
            void Refresh() =>
                create.style.display =
                    !property.hasMultipleDifferentValues && property.objectReferenceValue == null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            root.TrackPropertyValue(property, _ => Refresh());
            Refresh();
            return root;
        }
    }
}
