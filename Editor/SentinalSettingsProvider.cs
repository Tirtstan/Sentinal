using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    /// <summary>
    /// Registers the project's <see cref="SentinalSettings"/> for the editor and builds (Preloaded Assets),
    /// and draws Project Settings › Sentinal.
    /// </summary>
    [InitializeOnLoad]
    public static class SentinalSettingsRegistration
    {
        private const string ConfigKey = "com.tirt.sentinal.settings";

        static SentinalSettingsRegistration() => EditorApplication.delayCall += RegisterActive;

        public static SentinalSettings Active
        {
            get
            {
                EditorBuildSettings.TryGetConfigObject(ConfigKey, out SentinalSettings settings);
                return settings;
            }
        }

        public static void RegisterActive()
        {
            SentinalSettings settings = Active;
            if (settings == null)
                return;

            SentinalSettings.Register(settings);
            EnsurePreloaded(settings);
        }

        public static SentinalSettings Create()
        {
            string directory = Path.GetDirectoryName(SentinalSettings.DefaultAssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                Directory.CreateDirectory(directory);

            var settings = ScriptableObject.CreateInstance<SentinalSettings>();
            AssetDatabase.CreateAsset(settings, SentinalSettings.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.AddConfigObject(ConfigKey, settings, true);
            RegisterActive();
            return settings;
        }

        private static void EnsurePreloaded(SentinalSettings settings)
        {
            List<Object> preloaded = PlayerSettings.GetPreloadedAssets().Where(asset => asset != null).ToList();
            if (preloaded.Contains(settings))
                return;

            preloaded.RemoveAll(asset => asset is SentinalSettings);
            preloaded.Add(settings);
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        }
    }

    internal sealed class SentinalSettingsProvider : SettingsProvider
    {
        private delegate void RowBuilder(VisualElement row, VisualElement below, SerializedProperty item);

        private static SentinalSettingsProvider active;
        private static int revealLayerId;
        private SerializedObject serializedSettings;
        private VisualElement host;
        private ScrollView scroll;

        private SentinalSettingsProvider()
            : base("Project/Sentinal", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() =>
            new SentinalSettingsProvider { keywords = new[] { "Sentinal", "Layer", "View", "Menu", "Action map" } };

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            OnDeactivate();
            host = rootElement;
            active = this;
            Build();
            Undo.undoRedoPerformed += Build;
        }

        public override void OnDeactivate()
        {
            Undo.undoRedoPerformed -= Build;
            if (active == this)
                active = null;
            if (host != null)
                host.Unbind();
            serializedSettings?.Dispose();
            serializedSettings = null;
            host = null;
        }

        private void Build()
        {
            if (host == null)
                return;
            host.Unbind();
            host.Clear();
            serializedSettings?.Dispose();
            serializedSettings = null;

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            host.Add(scroll);
            VisualElement root = SentinalEditorUI.Root("sentinal-settings");
            scroll.Add(root);
            var title = new Label("Sentinal");
            title.AddToClassList("sentinal-title");
            root.Add(title);

            SentinalSettings settings = SentinalSettingsRegistration.Active;
            if (settings == null)
            {
                root.Add(
                    new HelpBox(
                        $"Expected registered Sentinal settings; actual: none. Create them at '{SentinalSettings.DefaultAssetPath}'.",
                        HelpBoxMessageType.Warning
                    )
                );
                root.Add(
                    new Button(() =>
                    {
                        SentinalSettingsRegistration.Create();
                        Build();
                    })
                    {
                        text = "Create Sentinal Settings",
                    }
                );
                return;
            }

            serializedSettings = new SerializedObject(settings);
            CreateLayerTable(
                Section(
                    root,
                    "Global layers",
                    "Top to bottom as they stack on screen: a view on a higher layer always takes focus. Expand a row to find the views that use it."
                ),
                "globalLayers",
                false
            );
            CreateLayerTable(
                Section(
                    root,
                    "Player layers",
                    "Every local player gets their own copy of these layers, so one player's menus never block another's."
                ),
                "playerLayers",
                true
            );

            VisualElement maps = Section(
                root,
                "Action maps",
                "The map menus move players to, and the maps that stay on beside every player's current map."
            );
            VisualElement uiMap = ActionMapRefDrawer.CreateField(
                serializedSettings.FindProperty("uiActionMap"),
                "UI action map"
            );
            // Match the right edge of the bordered tables below.
            uiMap.style.marginRight = 10;
            maps.Add(uiMap);
            var alwaysOn = new Label("Always-on maps");
            alwaysOn.AddToClassList("sentinal-section__title");
            alwaysOn.style.marginTop = 6;
            maps.Add(alwaysOn);
            CreateTable(
                maps,
                "alwaysOnActionMaps",
                new[] { ("Actions asset", "sentinal-col--grow"), ("Map", "sentinal-col--map") },
                (row, _, item) =>
                {
                    VisualElement field = ActionMapRefDrawer.CreateField(item, null);
                    field.AddToClassList("sentinal-col--grow");
                    row.Add(field);
                },
                ClearMap,
                null
            );

            CreateTable(
                Section(root, "Scene base maps", "The map each player uses in a scene while no menu changes it."),
                "sceneBaseMaps",
                new[]
                {
                    ("Scene", "sentinal-col--grow"),
                    ("Actions asset", "sentinal-col--asset"),
                    ("Map", "sentinal-col--map"),
                },
                BuildSceneRow,
                item =>
                {
                    item.FindPropertyRelative("scenePath").stringValue = "";
                    ClearMap(item.FindPropertyRelative("map"));
                },
                null
            );
        }

        private static VisualElement Section(VisualElement parent, string title, string help)
        {
            var section = new VisualElement();
            section.AddToClassList("sentinal-section");
            var heading = new Label(title);
            heading.AddToClassList("sentinal-section__title");
            section.Add(heading);
            SentinalEditorUI.Muted(section, help);
            parent.Add(section);
            return section;
        }

        private void CreateTable(
            VisualElement parent,
            string path,
            (string Label, string Column)[] columns,
            RowBuilder buildRow,
            System.Action<SerializedProperty> initialize,
            System.Func<SerializedProperty, bool> canRemove
        )
        {
            var header = new VisualElement();
            header.AddToClassList("sentinal-table__header");
            foreach ((string text, string column) in columns)
            {
                var label = new Label(text);
                label.AddToClassList("sentinal-col");
                label.AddToClassList(column);
                header.Add(label);
            }
            parent.Add(header);

            var indices = Enumerable.Range(0, serializedSettings.FindProperty(path).arraySize).ToList();
            var list = new ListView
            {
                itemsSource = indices,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                reorderable = true,
                reorderMode = ListViewReorderMode.Simple,
                selectionType = SelectionType.Single,
                showBorder = true,
                showAddRemoveFooter = true,
                makeItem = () => new VisualElement(),
            };

            void Reload()
            {
                indices.Clear();
                indices.AddRange(Enumerable.Range(0, serializedSettings.FindProperty(path).arraySize));
                list.ClearSelection();
                list.Rebuild();
            }

            list.bindItem = (element, index) =>
            {
                element.Unbind();
                element.Clear();
                SerializedProperty array = serializedSettings.FindProperty(path);
                if (index >= array.arraySize)
                    return;
                var row = new VisualElement();
                row.AddToClassList("sentinal-table__row");
                element.Add(row);
                var below = new VisualElement();
                below.AddToClassList("sentinal-table__below");
                below.AddToClassList("sentinal-hidden");
                element.Add(below);
                buildRow(row, below, array.GetArrayElementAtIndex(index));
            };
            list.unbindItem = (element, _) => element.Unbind();
            list.itemIndexChanged += (from, to) =>
            {
                serializedSettings.Update();
                serializedSettings.FindProperty(path).MoveArrayElement(from, to);
                serializedSettings.ApplyModifiedProperties();
                Reload();
            };
            list.onAdd = _ =>
            {
                serializedSettings.Update();
                SerializedProperty array = serializedSettings.FindProperty(path);
                array.arraySize++;
                initialize(array.GetArrayElementAtIndex(array.arraySize - 1));
                serializedSettings.ApplyModifiedProperties();
                Reload();
            };
            list.onRemove = _ =>
            {
                serializedSettings.Update();
                SerializedProperty array = serializedSettings.FindProperty(path);
                int index = list.selectedIndex >= 0 ? list.selectedIndex : array.arraySize - 1;
                if (index < 0 || index >= array.arraySize)
                    return;
                if (canRemove != null && !canRemove(array.GetArrayElementAtIndex(index)))
                    return;
                array.DeleteArrayElementAtIndex(index);
                serializedSettings.ApplyModifiedProperties();
                Reload();
            };
            parent.Add(list);
        }

        private void CreateLayerTable(VisualElement parent, string path, bool player)
        {
            CreateTable(
                parent,
                path,
                new[]
                {
                    ("", "sentinal-col--toggle"),
                    ("Name", "sentinal-col--grow"),
                    ("Others on this layer", "sentinal-col--others"),
                    ("Back", "sentinal-col--back"),
                    ("Input while open", "sentinal-col--input"),
                },
                (row, below, layer) => BuildLayerRow(row, below, layer, player),
                InitializeLayer,
                CanRemoveLayer
            );
        }

        /// <summary>Opens Project Settings › Sentinal with <paramref name="layerId"/>'s row expanded and in view.</summary>
        public static void Reveal(int layerId)
        {
            revealLayerId = layerId;
            SettingsService.OpenProjectSettings("Project/Sentinal");
            // An already-open page doesn't activate again, so rebuild it to apply the reveal.
            active?.Build();
        }

        private void BuildLayerRow(VisualElement row, VisualElement below, SerializedProperty layer, bool player)
        {
            int id = layer.FindPropertyRelative("id").intValue;
            var expand = new Foldout { value = false, tooltip = "Find the views that use this layer." };
            expand.AddToClassList("sentinal-col");
            expand.AddToClassList("sentinal-col--toggle");
            row.Add(expand);
            AddCell(row, new TextField(), layer.FindPropertyRelative("name"), "sentinal-col--grow");
            AddCell(row, new EnumField(), layer.FindPropertyRelative("others"), "sentinal-col--others");
            AddCell(row, new EnumField(), layer.FindPropertyRelative("back"), "sentinal-col--back");

            SerializedProperty input = layer.FindPropertyRelative("inputWhileOpen");
            var values = new List<int>
            {
                (int)LayerInput.NoChange,
                (int)(player ? LayerInput.OwnerToUI : LayerInput.EveryoneToUI),
            };
            var choices = new List<string> { "No change", player ? "Owner to UI" : "Everyone to UI" };
            if (!values.Contains(input.intValue))
            {
                values.Add(input.intValue);
                choices.Add($"Invalid: {(LayerInput)input.intValue}");
            }
            var dropdown = new PopupField<string>(choices, choices[values.IndexOf(input.intValue)])
            {
                tooltip = player
                    ? "Move only the owner's map to UI while the view is open."
                    : "Move everyone's map to UI and pause player menus while the view is open.",
            };
            dropdown.AddToClassList("sentinal-col");
            dropdown.AddToClassList("sentinal-col--input");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                input.serializedObject.Update();
                input.intValue = values[choices.IndexOf(evt.newValue)];
                input.serializedObject.ApplyModifiedProperties();
            });
            row.Add(dropdown);

            expand.RegisterValueChangedCallback(evt =>
            {
                below.EnableInClassList("sentinal-hidden", !evt.newValue);
                below.Clear();
                if (evt.newValue)
                    ListLayerUsers(below, id);
            });

            if (id != 0 && id == revealLayerId)
            {
                revealLayerId = 0;
                row.AddToClassList("sentinal-table__row--revealed");
                expand.value = true;
                ScrollView page = scroll;
                row.schedule.Execute(() => page?.ScrollTo(row)).StartingIn(50);
            }
        }

        private static void ListLayerUsers(VisualElement parent, int id)
        {
            List<SentinalLayerUsage.Usage> users;
            try
            {
                users = SentinalLayerUsage.FindAssignments(id);
            }
            catch (System.Exception exception)
            {
                parent.Add(new HelpBox($"Scanning for views failed: {exception.Message}", HelpBoxMessageType.Error));
                return;
            }

            SentinalEditorUI.Muted(
                parent,
                users.Count == 0
                    ? "No views use this layer."
                    : $"{users.Count} layer assignment{(users.Count == 1 ? "" : "s")}. Inherited views share their source assignment."
            );
            foreach (SentinalLayerUsage.Usage user in users)
            {
                VisualElement row = SentinalEditorUI.LinkRow(parent, user.Target);
                row.tooltip = user.Description;
                SentinalEditorUI.Clip(
                    row,
                    user.Kind == SentinalLayerUsage.UsageKind.SceneAssignment
                        ? $"{user.Target.name} (scene)"
                        : user.Target.name
                );
            }
        }

        private void InitializeLayer(SerializedProperty layer)
        {
            SerializedProperty nextId = serializedSettings.FindProperty("nextLayerId");
            layer.FindPropertyRelative("id").intValue = nextId.intValue;
            nextId.intValue++;
            layer.FindPropertyRelative("name").stringValue = "New layer";
            layer.FindPropertyRelative("others").intValue = (int)LayerOthers.StayVisible;
            layer.FindPropertyRelative("back").intValue = (int)LayerBack.ClosesTheView;
            layer.FindPropertyRelative("inputWhileOpen").intValue = (int)LayerInput.NoChange;
        }

        private static bool CanRemoveLayer(SerializedProperty layer)
        {
            int id = layer.FindPropertyRelative("id").intValue;
            string name = layer.FindPropertyRelative("name").stringValue;
            List<SentinalLayerUsage.Usage> users;
            try
            {
                users = SentinalLayerUsage.Scan(id);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[Sentinal] Layer '{name}' was not removed because scanning for its views failed: {exception.Message}"
                );
                return false;
            }
            if (users.Count == 0)
                return true;
            Debug.LogError(
                $"[Sentinal] Expected no views on layer '{name}' before removing it; actual: {users.Count}: {string.Join(", ", users.Select(user => user.Description))}."
            );
            return false;
        }

        private static void BuildSceneRow(VisualElement row, VisualElement below, SerializedProperty item)
        {
            SerializedProperty path = item.FindPropertyRelative("scenePath");
            var scene = new ObjectField { objectType = typeof(SceneAsset), allowSceneObjects = false };
            scene.AddToClassList("sentinal-col");
            scene.AddToClassList("sentinal-col--grow");
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue);
            scene.SetValueWithoutNotify(sceneAsset);
            if (sceneAsset == null && !string.IsNullOrEmpty(path.stringValue))
            {
                below.RemoveFromClassList("sentinal-hidden");
                SentinalEditorUI.Muted(below, $"Missing scene: expected an asset at '{path.stringValue}'.");
            }
            scene.RegisterValueChangedCallback(evt =>
            {
                path.serializedObject.Update();
                path.stringValue = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : "";
                path.serializedObject.ApplyModifiedProperties();
                below.Clear();
                below.AddToClassList("sentinal-hidden");
            });
            row.Add(scene);
            VisualElement map = ActionMapRefDrawer.CreateField(item.FindPropertyRelative("map"), null);
            // Asset column + gap + map column, so the cell lines up with both headers.
            map.style.width = 190 + 6 + 130;
            map.style.flexShrink = 0;
            row.Add(map);
        }

        private static void ClearMap(SerializedProperty map) => map.FindPropertyRelative("id").stringValue = "";

        private static void AddCell<T>(
            VisualElement row,
            BaseField<T> field,
            SerializedProperty property,
            string column
        )
        {
            field.AddToClassList("sentinal-col");
            field.AddToClassList(column);
            field.BindProperty(property);
            row.Add(field);
        }
    }
}
