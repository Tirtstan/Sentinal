using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    [CustomPropertyDrawer(typeof(ViewLayerRef))]
    public sealed class ViewLayerRefDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            SerializedProperty id = property.FindPropertyRelative("id");
            string layerSignature = null;
            string Signature()
            {
                SentinalSettings settings = SentinalSettingsRegistration.Active;
                return settings != null
                    ? settings.GetInstanceID()
                        + ":"
                        + string.Join(
                            "|",
                            settings
                                .GlobalLayers.Concat(settings.PlayerLayers)
                                .Select(layer => $"{layer.Id}:{layer.Name}")
                        )
                    : "No settings";
            }
            void Build()
            {
                layerSignature = Signature();
                root.Clear();
                SentinalSettings settings = SentinalSettingsRegistration.Active;
                if (settings == null)
                {
                    root.Add(
                        new HelpBox("Expected registered Sentinal settings; actual: none.", HelpBoxMessageType.Warning)
                    );
                    root.Add(
                        new Button(() => SettingsService.OpenProjectSettings("Project/Sentinal"))
                        {
                            text = "Open Sentinal settings"
                        }
                    );
                    return;
                }
                var ids = new List<int> { 0 };
                var names = new List<string> { "None" };
                foreach (ViewLayer layer in settings.GlobalLayers)
                {
                    ids.Add(layer.Id);
                    names.Add("Global / " + layer.Name);
                }
                foreach (ViewLayer layer in settings.PlayerLayers)
                {
                    ids.Add(layer.Id);
                    names.Add("Player / " + layer.Name);
                }
                int current = ids.IndexOf(id.intValue);
                if (current < 0)
                {
                    ids.Add(id.intValue);
                    names.Add($"Missing layer ({id.intValue})");
                    current = ids.Count - 1;
                }
                var dropdown = new PopupField<int>(
                    property.displayName,
                    ids,
                    current,
                    value => names[ids.IndexOf(value)],
                    value => names[ids.IndexOf(value)]
                )
                {
                    tooltip = property.tooltip,
                    showMixedValue = id.hasMultipleDifferentValues,
                };
                dropdown.AddToClassList(BaseField<int>.alignedFieldUssClassName);
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    id.intValue = evt.newValue;
                    id.serializedObject.ApplyModifiedProperties();
                });
                root.Add(dropdown);
            }
            root.TrackPropertyValue(id, _ => Build());
            root.schedule.Execute(() =>
                {
                    if (Signature() != layerSignature)
                        Build();
                })
                .Every(500);
            Build();
            return root;
        }
    }
}
