using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(ActionMapRef))]
    public sealed class ActionMapRefDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            CreateField(property, preferredLabel ?? property.displayName);

        /// <summary>
        /// One line: the action asset, then a map dropdown resolved by GUID. Without a label the field fits a table cell.
        /// </summary>
        public static VisualElement CreateField(SerializedProperty property, string label)
        {
            SerializedProperty asset = property.FindPropertyRelative("asset");
            SerializedProperty id = property.FindPropertyRelative("id");
            var field = new ObjectField(label) { objectType = typeof(InputActionAsset), allowSceneObjects = false };
            if (!string.IsNullOrEmpty(label))
                field.AddToClassList(ObjectField.alignedFieldUssClassName);
            field.BindProperty(asset);

            var ids = new List<string>();
            var map = new PopupField<string>(new List<string> { "None" }, 0)
            {
                tooltip = "Action map, resolved by ID in each player's own copy of the asset.",
            };
            map.style.width = 130;
            map.style.flexShrink = 0;
            map.style.marginLeft = 6;
            map.style.marginRight = 0;
            map.style.marginTop = 0;
            map.style.marginBottom = 0;
            field.Add(map);

            void Refresh()
            {
                ids.Clear();
                var choices = new List<string>();
                var actions = asset.objectReferenceValue as InputActionAsset;
                ids.Add("");
                choices.Add(actions != null ? "None" : "No asset");
                if (actions != null)
                    foreach (InputActionMap actionMap in actions.actionMaps)
                    {
                        ids.Add(actionMap.id.ToString());
                        choices.Add(actionMap.name);
                    }
                int current = ids.IndexOf(id.stringValue);
                if (current < 0)
                {
                    ids.Add(id.stringValue);
                    choices.Add("Missing map");
                    current = choices.Count - 1;
                }
                map.choices = choices;
                map.SetValueWithoutNotify(choices[current]);
                map.SetEnabled(actions != null);
            }

            map.RegisterValueChangedCallback(_ =>
            {
                if (map.index < 0 || map.index >= ids.Count)
                    return;
                id.serializedObject.Update();
                id.stringValue = ids[map.index];
                id.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(asset, _ => Refresh());
            field.TrackPropertyValue(id, _ => Refresh());
            Refresh();
            return field;
        }
    }
}
