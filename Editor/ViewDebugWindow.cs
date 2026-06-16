using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    public class ViewDebugWindow : EditorWindow
    {
        [MenuItem("Window/Sentinal/Debug")]
        public static void ShowWindow()
        {
            var window = GetWindow<ViewDebugWindow>("Sentinal Debug");
            window.minSize = new Vector2(520, 320);
        }

        private VisualElement root;
        private VisualElement viewStackContainer;
        private VisualElement hiddenStackContainer;
        private VisualElement playerAuthorityContainer;
        private Label statusLabel;
        private ViewGroupConfig groupConfig;
        private readonly StringBuilder groupBuilder = new();

        private void CreateGUI()
        {
            root = rootVisualElement;
            groupConfig = ViewGroupConfig.LoadShared();

            DrawHeader();
            DrawActionBar();

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            viewStackContainer = CreateSection("View Stack");
            scrollView.Add(viewStackContainer);

            hiddenStackContainer = CreateSection("Hidden Stack");
            scrollView.Add(hiddenStackContainer);

#if ENABLE_INPUT_SYSTEM
            playerAuthorityContainer = CreateSection("Player Authority");
            scrollView.Add(playerAuthorityContainer);
#endif

            root.schedule.Execute(Refresh).Every(100);
        }

        private void DrawHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.height = 28;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = EditorColors.Wire;

            var title = new Label("Sentinal Debug")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.12f, 0.12f, 0.12f)
                }
            };
            header.Add(title);

            statusLabel = new Label("Initializing...")
            {
                style =
                {
                    color = EditorGUIUtility.isProSkin
                        ? new Color(0.70f, 0.70f, 0.70f)
                        : new Color(0.25f, 0.25f, 0.25f),
                    unityTextAlign = TextAnchor.MiddleRight,
                    fontSize = 10
                }
            };
            header.Add(statusLabel);

            root.Add(header);
        }

        private void DrawActionBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 30;
            bar.style.alignItems = Align.Center;
            bar.style.paddingTop = 3;
            bar.style.paddingBottom = 3;
            bar.style.paddingLeft = 6;
            bar.style.paddingRight = 6;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = EditorColors.Wire;

            var closeCurrent = new Button(() => SentinalViewRouter.CloseCurrentView()) { text = "Close Current" };
            var closeAll = new Button(() => SentinalViewRouter.CloseAllViews()) { text = "Close All" };
            var restoreHidden = new Button(() => SentinalViewRouter.RestoreHiddenViews())
            {
                text = "Restore Top Hidden"
            };
            var dumpAddressRegistry = new Button(() => ViewAddressRegistry.DumpRegistry())
            {
                text = "Log Address Registry"
            };
            StyleActionButton(closeCurrent);
            StyleActionButton(closeAll);
            StyleActionButton(restoreHidden);
            StyleActionButton(dumpAddressRegistry);

            bar.Add(closeCurrent);
            bar.Add(closeAll);
            bar.Add(restoreHidden);
            bar.Add(dumpAddressRegistry);

            root.Add(bar);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginTop = 6;
            section.style.marginBottom = 6;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;

            var header = new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = EditorGUIUtility.isProSkin
                        ? new Color(0.86f, 0.86f, 0.86f)
                        : new Color(0.15f, 0.15f, 0.15f),
                    fontSize = 10,
                    borderBottomWidth = 1,
                    borderBottomColor = EditorGUIUtility.isProSkin
                        ? new Color(0.26f, 0.26f, 0.26f)
                        : new Color(0.78f, 0.78f, 0.78f),
                    paddingBottom = 3,
                    marginBottom = 3
                }
            };
            section.Add(header);

            var content = new VisualElement();
            section.Add(content);
            return section;
        }

        private void Refresh()
        {
            if (!Application.isPlaying)
            {
                statusLabel.text = "Offline - enter Play Mode";
                statusLabel.style.color = new Color(0.86f, 0.43f, 0.43f);
                ClearContainers();
                return;
            }

            statusLabel.text = $"Connected | Views: {SentinalViewRouter.ViewCount}";
            statusLabel.style.color = new Color(0.39f, 0.76f, 0.45f);

            RefreshViewStack();
            RefreshHiddenStack();
#if ENABLE_INPUT_SYSTEM
            RefreshPlayerAuthority();
#endif
        }

        private void ClearContainers()
        {
            viewStackContainer?.ElementAt(1).Clear();
            hiddenStackContainer?.ElementAt(1).Clear();
            playerAuthorityContainer?.ElementAt(1).Clear();
        }

        private void RefreshViewStack()
        {
            var content = viewStackContainer.ElementAt(1);
            content.Clear();

            var history = SentinalViewRouter.GetViewHistory();
            if (history.Length == 0)
            {
                content.Add(
                    new Label("No views open.")
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f),
                            unityFontStyleAndWeight = FontStyle.Italic
                        }
                    }
                );
                return;
            }

            int topPriority = int.MinValue;
            for (int i = 0; i < history.Length; i++)
            {
                if (history[i] != null && history[i].Priority > topPriority)
                    topPriority = history[i].Priority;
            }

            for (int i = history.Length - 1; i >= 0; i--)
            {
                var view = history[i];
                if (view == null)
                    continue;

                var card = new VisualElement();
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.FlexStart;
                card.style.justifyContent = Justify.SpaceBetween;
                card.style.paddingTop = 4;
                card.style.paddingBottom = 4;
                card.style.paddingLeft = 5;
                card.style.paddingRight = 4;
                card.style.marginBottom = 3;
                card.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.03f)
                    : new Color(0f, 0f, 0f, 0.02f);
                card.style.borderLeftWidth = 2;

                bool isCurrent = SentinalViewRouter.IsCurrent(view);
                bool isTopPriority = view.Priority == topPriority;
                card.style.borderLeftColor = isCurrent
                    ? new Color(0.36f, 0.68f, 1f)
                    : isTopPriority
                        ? new Color(1f, 0.70f, 0.28f)
                        : EditorGUIUtility.isProSkin
                            ? new Color(0.30f, 0.30f, 0.30f)
                            : new Color(0.72f, 0.72f, 0.72f);
                RegisterPick(card, view.gameObject);

                var dot = new VisualElement();
                dot.style.width = 6;
                dot.style.height = 6;
                dot.style.borderTopLeftRadius = 3;
                dot.style.borderTopRightRadius = 3;
                dot.style.borderBottomLeftRadius = 3;
                dot.style.borderBottomRightRadius = 3;
                dot.style.marginRight = 6;
                dot.style.backgroundColor = isCurrent ? new Color(0.39f, 0.76f, 0.45f) : new Color(0.58f, 0.58f, 0.58f);
                card.Add(dot);

                var body = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.FlexStart,
                        flexGrow = 1
                    }
                };

                var titleRow = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        flexWrap = Wrap.Wrap
                    }
                };
                titleRow.Add(
                    new Label(view.name)
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.93f, 0.93f, 0.93f)
                                : new Color(0.10f, 0.10f, 0.10f)
                        }
                    }
                );

                if (isCurrent)
                    titleRow.Add(CreateBadge("Current Focus", new Color(0.36f, 0.68f, 1f)));
                if (isTopPriority)
                    titleRow.Add(CreateBadge($"Priority: {view.Priority}", new Color(1f, 0.70f, 0.28f)));
                if (view.RootView)
                    titleRow.Add(CreateBadge("Root View", new Color(0.62f, 0.62f, 0.62f)));
                body.Add(titleRow);

                string details = $"Group: {GetGroupMaskDisplayName(view.GroupMask)}";
#if ENABLE_INPUT_SYSTEM
                if (view.TryGetComponent<Sentinal.InputSystem.ActionMapGate>(out var gate))
                {
                    details += gate.IsApplied ? " | Input Gate: Active" : " | Input Gate: Inactive";
                    titleRow.Add(
                        CreateBadge(
                            gate.IsApplied ? "Input Enabled" : "Input Disabled",
                            gate.IsApplied ? new Color(0.39f, 0.76f, 0.45f) : new Color(0.58f, 0.58f, 0.58f)
                        )
                    );
                }
#endif
                body.Add(
                    new Label(details)
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f),
                            fontSize = 10
                        }
                    }
                );
                card.Add(body);

                var btn = new Button(() => SentinalViewRouter.Remove(view)) { text = "X" };
                btn.style.height = 18;
                btn.style.minWidth = 20;
                btn.style.maxWidth = 20;
                btn.style.fontSize = 10;
                btn.style.unityTextAlign = TextAnchor.MiddleCenter;
                btn.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.4f, 0.4f, 0.12f)
                    : new Color(0.7f, 0.1f, 0.1f, 0.10f);
                btn.style.color = new Color(0.86f, 0.43f, 0.43f);
                card.Add(btn);

                content.Add(card);
            }
        }

        private void RefreshHiddenStack()
        {
            var content = hiddenStackContainer.ElementAt(1);
            content.Clear();

            var field = typeof(SentinalViewRouter).GetField(
                "hiddenViewStack",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );
            if (field == null)
                return;

            var stack = field.GetValue(null) as Stack<(ViewSelector owner, List<ViewSelector> views)>;
            if (stack == null || stack.Count == 0)
            {
                content.Add(
                    new Label("No hidden views.")
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f),
                            unityFontStyleAndWeight = FontStyle.Italic
                        }
                    }
                );
                return;
            }

            foreach (var entry in stack)
            {
                var card = new VisualElement();
                card.style.paddingTop = 3;
                card.style.paddingBottom = 3;
                card.style.paddingLeft = 5;
                card.style.paddingRight = 5;
                card.style.marginBottom = 2;
                card.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.03f)
                    : new Color(0f, 0f, 0f, 0.02f);
                card.style.borderLeftWidth = 2;
                card.style.borderLeftColor = new Color(1f, 0.70f, 0.28f);

                var ownerName = entry.owner != null ? entry.owner.name : "Null Owner";
                var ownerGroup = entry.owner != null ? GetGroupMaskDisplayName(entry.owner.GroupMask) : "None";
                var ownerLabel = new Label($"Owner: {ownerName} | Group: {ownerGroup}")
                {
                    style = { color = new Color(1f, 0.70f, 0.28f), unityFontStyleAndWeight = FontStyle.Bold }
                };
                card.Add(ownerLabel);
                if (entry.owner != null)
                    RegisterPick(ownerLabel, entry.owner.gameObject);

                foreach (var hidden in entry.views)
                {
                    var hName = hidden != null ? hidden.name : "Null";
                    var hiddenGroup = hidden != null ? GetGroupMaskDisplayName(hidden.GroupMask) : "None";
                    var hiddenLabel = new Label($"  - View: {hName} | Group: {hiddenGroup}")
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f)
                        }
                    };
                    card.Add(hiddenLabel);
                    if (hidden != null)
                        RegisterPick(hiddenLabel, hidden.gameObject);
                }

                content.Add(card);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void RefreshPlayerAuthority()
        {
            var content = playerAuthorityContainer.ElementAt(1);
            content.Clear();

            var players = Sentinal.InputSystem.SentinalPlayer.GetAllPlayers();
            if (players.Count == 0)
            {
                content.Add(
                    new Label("No players registered.")
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f),
                            unityFontStyleAndWeight = FontStyle.Italic
                        }
                    }
                );
                return;
            }

            foreach (var kvp in players)
            {
                var key = kvp.Key;
                var player = kvp.Value;

                var card = new VisualElement();
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.FlexStart;
                card.style.justifyContent = Justify.SpaceBetween;
                card.style.paddingTop = 4;
                card.style.paddingBottom = 4;
                card.style.paddingLeft = 5;
                card.style.paddingRight = 5;
                card.style.marginBottom = 3;
                card.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.03f)
                    : new Color(0f, 0f, 0f, 0.02f);
                card.style.borderLeftWidth = 2;
                card.style.borderLeftColor = new Color(0.39f, 0.76f, 0.45f);

                var body = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.FlexStart,
                        flexGrow = 1
                    }
                };

                string roleName = key == Sentinal.InputSystem.SentinalPlayer.PrimaryKey ? "Primary" : $"Role {key}";
                string pName = player != null ? $"Player Index: {player.playerIndex}" : "No PlayerInput";
                string mapName =
                    player != null && player.currentActionMap != null ? player.currentActionMap.name : "None";

                var titleRow = new VisualElement()
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
                };
                titleRow.Add(
                    new Label($"Role: {roleName} (Key: {key})")
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.93f, 0.93f, 0.93f)
                                : new Color(0.10f, 0.10f, 0.10f),
                            marginRight = 8
                        }
                    }
                );
                if (key == Sentinal.InputSystem.SentinalPlayer.PrimaryKey)
                    titleRow.Add(CreateBadge("Primary Role", new Color(0.36f, 0.68f, 1f)));
                body.Add(titleRow);

                body.Add(
                    new Label($"Input: {pName} | Action Map: {mapName}")
                    {
                        style =
                        {
                            color = EditorGUIUtility.isProSkin
                                ? new Color(0.72f, 0.72f, 0.72f)
                                : new Color(0.30f, 0.30f, 0.30f),
                            fontSize = 10
                        }
                    }
                );

                card.Add(body);
                if (player != null)
                    RegisterPick(card, player.gameObject);
                content.Add(card);
            }
        }
#endif

        private static Label CreateBadge(string text, Color color)
        {
            Color background = new Color(color.r, color.g, color.b, EditorGUIUtility.isProSkin ? 0.22f : 0.16f);
            return new Label(text)
            {
                style =
                {
                    backgroundColor = background,
                    color = color,
                    fontSize = 9,
                    paddingLeft = 3,
                    paddingRight = 3,
                    marginLeft = 6,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
        }

        private static void RegisterPick(VisualElement element, GameObject target)
        {
            element.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || target == null)
                    return;

                Selection.activeGameObject = target;
                EditorGUIUtility.PingObject(target);
            });
        }

        private static void StyleActionButton(Button button)
        {
            button.style.flexGrow = 1;
            button.style.height = 22;
            button.style.fontSize = 10;
            button.style.marginRight = 6;
        }

        private string GetGroupMaskDisplayName(int groupMask)
        {
            if (groupMask == 0)
                return "None";

            if (groupConfig == null || groupConfig.Groups == null || groupConfig.Groups.Count == 0)
                return $"Mask: {groupMask}";

            groupBuilder.Clear();
            for (int i = 0; i < groupConfig.Groups.Count; i++)
            {
                int bit = 1 << i;
                if ((groupMask & bit) == 0)
                    continue;

                if (groupBuilder.Length > 0)
                    groupBuilder.Append(", ");

                string groupName = groupConfig.GetGroupName(i);
                groupBuilder.Append(string.IsNullOrEmpty(groupName) ? $"Group {i}" : groupName);
            }

            return groupBuilder.Length == 0 ? $"Mask: {groupMask}" : groupBuilder.ToString();
        }
    }
}
