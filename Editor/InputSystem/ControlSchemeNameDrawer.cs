#if ENABLE_INPUT_SYSTEM
using System;
using System.Linq;
using Sentinal.InputSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(ControlSchemeNameAttribute))]
    public sealed class ControlSchemeNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var selector = (ControlSchemeNameAttribute)attribute;
            SerializedProperty assetProperty = property.serializedObject.FindProperty(selector.AssetField);
            var asset = assetProperty != null ? assetProperty.objectReferenceValue as InputActionAsset : null;
            if (asset == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(
                    position,
                    label.text,
                    $"Expected InputActionAsset in {selector.AssetField}; actual: {asset}."
                );
                return;
            }

            string[] schemes = asset.controlSchemes.Select(scheme => scheme.name).ToArray();
            int selected = Array.IndexOf(schemes, property.stringValue);
            if (selected < 0)
            {
                // Keep invalid authored data visible until the author deliberately replaces it.
                schemes = new[] { $"Invalid: '{property.stringValue}'" }.Concat(schemes).ToArray();
                selected = 0;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(position, label.text, selected, schemes);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = schemes[next];
            EditorGUI.EndProperty();
        }
    }
}
#endif
