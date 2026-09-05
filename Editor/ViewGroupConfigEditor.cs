using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(ViewGroupConfig))]
    public sealed class ViewGroupConfigEditor : UnityEditor.Editor
    {
        private const int MaxGroups = 32;

        private ViewGroupConfig Config => (ViewGroupConfig)target;

        private VisualElement groupList;
        private TextField newGroupField;
        private Button addButton;
        private Label countLabel;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(
                new HelpBox(
                    "Default always uses bit 0. Groups can be added and renamed, but never reordered, "
                        + "so existing masks keep their meaning. Only the last group can be removed, and only while unused.",
                    HelpBoxMessageType.Info
                )
            );

            groupList = new VisualElement { style = { marginTop = 6f } };
            root.Add(groupList);

            root.Add(CreateAddRow());

            countLabel = new Label
            {
                style =
                {
                    marginTop = 2f,
                    alignSelf = Align.FlexEnd,
                    fontSize = 10f,
                    color = new Color(0.6f, 0.6f, 0.6f, 1f),
                },
            };
            root.Add(countLabel);

            RebuildGroupList();

            root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);

            return root;
        }

        private VisualElement CreateAddRow()
        {
            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8f }, };

            newGroupField = new TextField { value = "New Group", style = { flexGrow = 1f } };
            newGroupField.RegisterValueChangedCallback(_ => RefreshAddButton());
            newGroupField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return)
                    AddGroup();
            });
            addRow.Add(newGroupField);

            addButton = new Button(AddGroup) { text = "Add", style = { width = 64f } };
            addRow.Add(addButton);

            return addRow;
        }

        private void RebuildGroupList()
        {
            groupList.Clear();

            groupList.Add(CreateGroupRow(0, "Default"));
            for (int i = 0; i < Config.Groups.Count; i++)
                groupList.Add(CreateGroupRow(i + 1, Config.Groups[i]));

            countLabel.text = $"{Config.GroupCount} / {MaxGroups} groups";
            RefreshAddButton();
        }

        private VisualElement CreateGroupRow(int groupIndex, string groupName)
        {
            bool isDefault = groupIndex == 0;
            bool isLast = groupIndex == Config.GroupCount - 1;

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2f,
                },
            };

            row.Add(CreateBitBadge(groupIndex, isDefault));

            var nameField = new TextField
            {
                value = groupName,
                isDelayed = true,
                style = { flexGrow = 1f }
            };
            nameField.SetEnabled(!isDefault);
            if (!isDefault)
            {
                int capturedIndex = groupIndex;
                nameField.RegisterValueChangedCallback(evt => RenameGroup(capturedIndex, evt.newValue));
            }
            row.Add(nameField);

            var removeButton = new Button { text = "\u2212", tooltip = "Remove the last group if its bit is unused." };
            removeButton.style.width = 22f;
            removeButton.SetEnabled(!isDefault && isLast);
            if (!isDefault && isLast)
            {
                int capturedIndex = groupIndex;
                string capturedName = groupName;
                removeButton.clicked += () => TryRemoveLastGroup(capturedIndex, capturedName);
            }
            row.Add(removeButton);

            return row;
        }

        private static VisualElement CreateBitBadge(int groupIndex, bool isDefault)
        {
            var badge = new Label(groupIndex.ToString())
            {
                tooltip = $"Mask bit {groupIndex}",
                style =
                {
                    width = 26f,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginRight = 4f,
                    paddingTop = 1f,
                    paddingBottom = 1f,
                    borderTopLeftRadius = 3f,
                    borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f,
                    borderBottomRightRadius = 3f,
                    backgroundColor = isDefault
                        ? new Color(0.22f, 0.42f, 0.28f, 1f)
                        : new Color(0.25f, 0.25f, 0.25f, 1f),
                    color = new Color(0.85f, 0.85f, 0.85f, 1f),
                    fontSize = 10f,
                },
            };

            return badge;
        }

        private void RefreshAddButton()
        {
            bool full = Config.GroupCount >= MaxGroups;
            addButton.SetEnabled(!full && !string.IsNullOrWhiteSpace(newGroupField.value));
            addButton.tooltip = full ? "ViewGroupMask supports Default plus 31 custom groups." : string.Empty;
        }

        private void AddGroup()
        {
            Undo.RecordObject(Config, "Add Sentinal View Group");
            if (!Config.TryAddGroup(newGroupField.value, out _))
            {
                Debug.LogWarning(
                    $"[Sentinal] Could not add group '{newGroupField.value}'. Names must be unique, non-empty, and cannot be Default.",
                    Config
                );
                return;
            }

            newGroupField.SetValueWithoutNotify("New Group");
            SaveConfig();
            RebuildGroupList();
        }

        private void RenameGroup(int groupIndex, string groupName)
        {
            Undo.RecordObject(Config, "Rename Sentinal View Group");
            if (!Config.RenameGroup(groupIndex, groupName))
            {
                Debug.LogWarning(
                    $"[Sentinal] Could not rename group {groupIndex} to '{groupName}'. Names must be unique, non-empty, and cannot be Default.",
                    Config
                );
                RebuildGroupList();
                return;
            }

            SaveConfig();
            RebuildGroupList();
        }

        private void TryRemoveLastGroup(int groupIndex, string groupName)
        {
            if (ViewGroupUsageScanner.IsGroupBitUsed(groupIndex, out string firstUsage))
            {
                EditorUtility.DisplayDialog(
                    "Group is in use",
                    $"'{groupName}' cannot be removed because bit {groupIndex} is used by {firstUsage}.",
                    "OK"
                );
                return;
            }

            Undo.RecordObject(Config, "Remove Sentinal View Group");
            if (!Config.RemoveLastGroupInEditor())
                return;

            SaveConfig();
            RebuildGroupList();
        }

        private void OnUndoRedo()
        {
            if (Config == null)
                return;

            SaveConfig();
            RebuildGroupList();
        }

        private void SaveConfig()
        {
            EditorUtility.SetDirty(Config);
            AssetDatabase.SaveAssetIfDirty(Config);
            serializedObject.Update();
        }
    }
}
