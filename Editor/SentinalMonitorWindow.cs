using System;
using System.Collections.Generic;
using System.Text;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    /// <summary>
    /// Live overview for verifying menus while playing: who holds UI authority, what is open on every layer,
    /// and each local player's Back chain and action map. Compact enough to stay docked.
    /// </summary>
    /// <remarks>
    /// Windows are drawn as a stack, front of the screen first, and facts are pinned to the view they are about.
    /// The body is rebuilt only when the set of open views or players changes; every other tick updates the
    /// existing rows in place so hover and pressed states survive.
    /// </remarks>
    public sealed class SentinalMonitorWindow : EditorWindow
    {
        private const float NarrowWidth = 380f;
        private const int IndentPerDepth = 16;

        private readonly List<Action> updaters = new();
        private readonly List<PlayerInput> players = new();
        private readonly StringBuilder structureBuilder = new();
        private VisualElement body;
        private VisualElement authoritySquare;
        private Label authorityWho;
        private Label authorityWhy;
        private Button backButton;
        private BackPreview globalBack;
        private string structure;

        [MenuItem("Window/Sentinal/Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<SentinalMonitorWindow>();
            window.titleContent = new GUIContent("Sentinal Monitor");
            window.minSize = new Vector2(280, 100);
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent("Sentinal Monitor");
            rootVisualElement.Clear();
            VisualElement root = SentinalEditorUI.Root("sentinal-monitor");
            root.style.flexGrow = 1;
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                root.EnableInClassList("sentinal-monitor--narrow", evt.newRect.width < NarrowWidth)
            );
            rootVisualElement.Add(root);

            CreateToolbar(root);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            body = new VisualElement();
            scroll.Add(body);

            structure = null;
            Refresh();
            root.schedule.Execute(Refresh).Every(100);
        }

        private void CreateToolbar(VisualElement root)
        {
            var bar = new VisualElement();
            bar.AddToClassList("sentinal-bar");
            root.Add(bar);

            var authority = new VisualElement();
            authority.AddToClassList("sentinal-bar__authority");
            bar.Add(authority);
            authoritySquare = SentinalEditorUI.Square(authority, SentinalTone.Idle);
            authorityWho = SentinalEditorUI.Clip(authority, "", "sentinal-bar__who");
            authorityWhy = SentinalEditorUI.Clip(authority, "", "sentinal-bar__why");

            backButton = new Button(() => SentinalViewRouter.HandleBack()) { text = "Send Back" };
            backButton.AddToClassList("sentinal-bar__button");
            bar.Add(backButton);

            var addresses = new Button(() => ViewAddressRegistry.DumpRegistry())
            {
                text = "Addresses",
                tooltip = "Log every registered view address to the Console.",
            };
            addresses.AddToClassList("sentinal-bar__button");
            bar.Add(addresses);
        }

        private void Refresh()
        {
            string current = Structure();
            if (current != structure)
            {
                structure = current;
                Rebuild();
            }

            globalBack = Application.isPlaying ? SentinalViewRouter.PreviewBack() : default;
            UpdateToolbar();
            foreach (Action update in updaters)
                update();
        }

        private void UpdateToolbar()
        {
            if (!Application.isPlaying)
            {
                SetAuthority(SentinalTone.Idle, "Not playing", "");
                backButton.SetEnabled(false);
                backButton.tooltip = "Enter Play Mode to send Back.";
                return;
            }

            UIAuthority authority = UIAuthority.Instance;
            if (authority == null)
                SetAuthority(SentinalTone.Warning, "No authority", "expected a UIAuthority component; actual: none");
            else if (UIAuthority.Current == null)
                SetAuthority(SentinalTone.Warning, "No authority", authority.CurrentReason);
            else
                SetAuthority(
                    SentinalTone.Open,
                    SentinalEditorUI.PlayerName(UIAuthority.Current),
                    $"has the UI. {authority.CurrentReason}"
                );

            backButton.SetEnabled(globalBack.Outcome is BackOutcome.Closes or BackOutcome.Handler);
            backButton.tooltip = $"Send one global Back press. Right now it {SentinalEditorUI.Describe(globalBack)}.";
        }

        private void SetAuthority(SentinalTone tone, string who, string why)
        {
            SentinalEditorUI.SetTone(authoritySquare, tone);
            authorityWho.text = who;
            authorityWhy.text = why;
            authorityWhy.tooltip = why;
        }

        private void Rebuild()
        {
            body.Clear();
            updaters.Clear();
            players.Clear();

            if (!Application.isPlaying)
            {
                Empty("Enter Play Mode to see open views, UI authority and each player's input.");
                return;
            }

            SentinalSettings settings = SentinalSettingsRegistration.Active;
            if (settings == null)
            {
                body.Add(new HelpBox("Expected registered Sentinal settings; actual: none.", HelpBoxMessageType.Error));
                return;
            }

            players.AddRange(PlayerInput.all);
            SentinalView[] views = SentinalViewRouter.GetOpenViews();
            if (DrawStack(settings.GlobalLayers, views, null) == 0)
                Empty("No global views open.");

            foreach (PlayerInput player in players)
                DrawPlayer(player, settings, views);
        }

        private void Empty(string text)
        {
            Label label = SentinalEditorUI.Muted(body, text);
            label.AddToClassList("sentinal-monitor__empty");
        }

        /// <summary>
        /// One line for a local player: who they are, which action map they are on and why, and a Back button.
        /// Their own views, when they have any, stack underneath.
        /// </summary>
        private void DrawPlayer(PlayerInput player, SentinalSettings settings, SentinalView[] views)
        {
            var strip = new VisualElement();
            strip.AddToClassList("sentinal-player");
            body.Add(strip);

            VisualElement square = SentinalEditorUI.Square(strip, SentinalTone.Idle);
            SentinalEditorUI.Clip(strip, PlayerLabel(player), "sentinal-player__name");
            Label detail = SentinalEditorUI.Clip(strip, "", "sentinal-player__detail");
            var back = new Button(() => SentinalViewRouter.HandleBack(player))
            {
                text = $"Back as {PlayerLabel(player)}",
            };
            strip.Add(back);

            bool hasOwnViews = DrawStack(settings.PlayerLayers, views, player) > 0;
            var chain = new List<string>();
            updaters.Add(() =>
            {
                if (player == null)
                    return;

                bool hasAuthority = UIAuthority.Current == player;
                SentinalEditorUI.SetTone(square, hasAuthority ? SentinalTone.Open : SentinalTone.Idle);
                square.tooltip = hasAuthority ? "Has the UI." : "Does not have the UI.";

                SentinalView source = PlayerInputMaps.GetMapSource(player);
                string map = player.currentActionMap != null ? player.currentActionMap.name : "none";
                string mapWhy = source != null ? $"from {SentinalEditorUI.Name(source)}" : "the scene base map";
                detail.text =
                    $"{player.currentControlScheme}. Map {map}, {mapWhy}."
                    + (hasOwnViews ? "" : " No views of their own.");

                chain.Clear();
                for (SentinalView view = SentinalViewRouter.GetBackView(player); view != null; view = view.Parent)
                    chain.Add(SentinalEditorUI.Name(view));
                detail.tooltip =
                    chain.Count > 0
                        ? $"{detail.text}\nBack asks each view in this order until one handles or closes: {string.Join("  ›  ", chain)}"
                        : $"{detail.text}\nNothing to go back from.";

                BackPreview preview = SentinalViewRouter.PreviewBack(player);
                back.SetEnabled(preview.Outcome is BackOutcome.Closes or BackOutcome.Handler);
                back.tooltip =
                    $"Send one Back press as this player. Right now it {SentinalEditorUI.Describe(preview)}.";
            });
        }

        private static string PlayerLabel(PlayerInput player) => $"P{player.playerIndex + 1}";

        /// <summary>
        /// Draws the windows on <paramref name="layers"/> as one stack, front first, with a footnote naming the
        /// empty layers. Adds nothing and returns 0 when no window is open.
        /// </summary>
        private int DrawStack(IReadOnlyList<ViewLayer> layers, SentinalView[] views, PlayerInput owner)
        {
            var slabs = new VisualElement();
            slabs.AddToClassList("sentinal-slabs");

            int windows = 0;
            var ordered = new List<SentinalView>();
            var empty = new List<string>();
            foreach (ViewLayer layer in layers)
            {
                ordered.Clear();
                for (int i = views.Length - 1; i >= 0; i--)
                {
                    // A covered window is placed by the window covering it, not by its own age.
                    SentinalView view = views[i];
                    bool covered = IsWindowOn(SentinalViewRouter.GetHiddenBy(view), layer, owner);
                    if (IsWindowOn(view, layer, owner) && !covered)
                        AddWithCovered(ordered, view, views, layer, owner);
                }

                if (ordered.Count == 0)
                    empty.Add(layer.Name);

                foreach (SentinalView window in ordered)
                    DrawSlab(slabs, window, layer.Name, views);
                windows += ordered.Count;
            }

            if (windows == 0)
                return 0;

            if (empty.Count > 0)
            {
                string text = empty.Count == 1 ? $"{empty[0]} layer is empty" : $"{empty.Count} empty layers";
                Label footnote = SentinalEditorUI.Muted(slabs, text);
                footnote.AddToClassList("sentinal-footnote");
                footnote.tooltip = string.Join(", ", empty);
            }

            var stack = new VisualElement();
            stack.AddToClassList("sentinal-stack");
            body.Add(stack);

            var rail = new VisualElement();
            rail.AddToClassList("sentinal-rail");
            rail.tooltip = "Windows are listed in screen order: the one in front is at the top.";
            stack.Add(rail);
            rail.Add(new Label("Front"));
            var line = new VisualElement();
            line.AddToClassList("sentinal-rail__line");
            rail.Add(line);
            rail.Add(new Label("Behind"));

            stack.Add(slabs);
            return windows;
        }

        private static bool IsWindowOn(SentinalView view, ViewLayer layer, PlayerInput owner) =>
            view != null
            && view.IsTopLevel
            && view.Layer.Id == layer.Id
            && (owner != null ? view.IsPlayerView && view.Owner == owner : !view.IsPlayerView);

        /// <summary>Adds <paramref name="window"/>, then every window it covers on the same layer, newest first.</summary>
        private static void AddWithCovered(
            List<SentinalView> ordered,
            SentinalView window,
            SentinalView[] views,
            ViewLayer layer,
            PlayerInput owner
        )
        {
            ordered.Add(window);
            for (int i = views.Length - 1; i >= 0; i--)
            {
                SentinalView candidate = views[i];
                bool coveredByWindow =
                    IsWindowOn(candidate, layer, owner) && SentinalViewRouter.GetHiddenBy(candidate) == window;
                if (coveredByWindow && !ordered.Contains(candidate))
                    AddWithCovered(ordered, candidate, views, layer, owner);
            }
        }

        /// <summary>One window: its own row, tags and tabs, then its nested views.</summary>
        private void DrawSlab(VisualElement slabs, SentinalView window, string layerName, SentinalView[] views)
        {
            var slab = new VisualElement();
            slab.AddToClassList("sentinal-slab");
            slabs.Add(slab);
            DrawTree(slab, window, views, 0, layerName);

            updaters.Add(() =>
            {
                if (window == null)
                    return;

                SentinalTone tone = SentinalEditorUI.ToneOf(window);
                slab.EnableInClassList("sentinal-slab--focus", SentinalViewRouter.IsFocusWithin(window));
                slab.EnableInClassList("sentinal-slab--covered", tone == SentinalTone.Hidden);
                slab.EnableInClassList("sentinal-slab--moving", tone == SentinalTone.Transition);
            });
        }

        /// <summary>A view's row and tags, its tab strips, then its nested views. Tabs live in the strip, not as rows.</summary>
        private void DrawTree(VisualElement rows, SentinalView view, SentinalView[] views, int depth, string layerName)
        {
            DrawView(rows, view, depth, layerName);
            foreach (TabbedView tabs in view.GetComponentsInChildren<TabbedView>(true))
                if (tabs.Owner == view)
                    DrawTabs(rows, tabs, view, depth);
            DrawChildren(rows, view, views, depth);
        }

        private void DrawChildren(VisualElement rows, SentinalView parent, SentinalView[] views, int depth)
        {
            foreach (SentinalView child in views)
            {
                if (child == null || child.Parent != parent)
                    continue;

                if (child.IsTab)
                    DrawChildren(rows, child, views, depth);
                else
                    DrawTree(rows, child, views, depth + 1, null);
            }
        }

        private void DrawView(VisualElement rows, SentinalView view, int depth, string layerName)
        {
            var row = new VisualElement();
            row.AddToClassList("sentinal-view");
            row.style.marginLeft = depth * IndentPerDepth;
            row.RegisterCallback<MouseDownEvent>(evt => SentinalEditorUI.Ping(evt, view.gameObject));
            rows.Add(row);

            string holder = SentinalEditorUI.Holder(view);
            if (holder != null)
                SentinalEditorUI.Clip(row, $"{holder} ›", "sentinal-view__holder");
            SentinalEditorUI.Clip(row, view.name, "sentinal-view__name");
            Label state = SentinalEditorUI.Clip(row, "", "sentinal-view__state");
            if (layerName != null)
            {
                Label layer = SentinalEditorUI.Clip(row, layerName, "sentinal-view__layer");
                layer.tooltip = $"On the {layerName} layer.";
            }

            var close = new Button(view.Close) { text = "×", tooltip = $"Close {SentinalEditorUI.Name(view)}" };
            close.AddToClassList("sentinal-view__close");
            row.Add(close);

            var tags = new VisualElement();
            tags.AddToClassList("sentinal-tags");
            tags.style.marginLeft = depth * IndentPerDepth;
            rows.Add(tags);
            Label focusTag = Tag(tags);
            Label backTag = Tag(tags);
            var mapTags = new Label[players.Count];
            var playerBackTags = new Label[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                mapTags[i] = Tag(tags);
                playerBackTags[i] = Tag(tags);
            }

            updaters.Add(() =>
            {
                if (view == null)
                    return;

                SentinalTone tone = SentinalEditorUI.ToneOf(view);
                string text = SentinalEditorUI.State(view);
                string focus = SentinalEditorUI.Focus(view);
                state.text = tone is SentinalTone.Open or SentinalTone.Focus ? "" : text;
                state.tooltip = text;
                SentinalEditorUI.SetTone(state, tone);
                row.tooltip = $"{SentinalEditorUI.Name(view)}: {text}" + (focus != null ? $", {focus}" : "");

                bool globalBackHere = globalBack.View == view;
                bool any = SetTag(focusTag, SentinalViewRouter.IsCurrent(view) ? "Focus" : null);
                any |= SetTag(backTag, globalBackHere ? $"Back {BackEffect(globalBack.Outcome)}" : null);
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerInput player = players[i];
                    if (player == null)
                        continue;

                    bool setsMap = player.currentActionMap != null && PlayerInputMaps.GetMapSource(player) == view;
                    string mapText = setsMap ? $"{PlayerLabel(player)} map: {player.currentActionMap.name}" : null;
                    any |= SetTag(mapTags[i], mapText);

                    // A player's Back is only worth a tag when it lands somewhere the global Back does not.
                    BackPreview preview = SentinalViewRouter.PreviewBack(player);
                    bool playerBackHere = !globalBackHere && preview.View == view;
                    string backText = playerBackHere
                        ? $"{PlayerLabel(player)} Back {BackEffect(preview.Outcome)}"
                        : null;
                    any |= SetTag(playerBackTags[i], backText);
                }

                tags.EnableInClassList("sentinal-hidden", !any);
            });
        }

        private static Label Tag(VisualElement tags)
        {
            var tag = new Label();
            tag.AddToClassList("sentinal-tag");
            tag.AddToClassList("sentinal-hidden");
            tags.Add(tag);
            return tag;
        }

        /// <summary>Shows the tag with <paramref name="text"/>, or hides it when text is null. Returns whether it shows.</summary>
        private static bool SetTag(Label tag, string text)
        {
            tag.EnableInClassList("sentinal-hidden", text == null);
            tag.text = text ?? "";
            return text != null;
        }

        /// <summary>What Back does to the view carrying the tag: "Back closes this".</summary>
        private static string BackEffect(BackOutcome outcome) =>
            outcome switch
            {
                BackOutcome.Closes => "closes this",
                BackOutcome.Handler => "is handled here",
                BackOutcome.Ignored => "is ignored here",
                _ => "does nothing",
            };

        /// <summary>
        /// Every tab while <paramref name="view"/> is on screen; only the selected one and a count while it is covered.
        /// </summary>
        private void DrawTabs(VisualElement rows, TabbedView tabs, SentinalView view, int depth)
        {
            var strip = new VisualElement();
            strip.AddToClassList("sentinal-tabs");
            strip.style.marginLeft = depth * IndentPerDepth;
            rows.Add(strip);

            IReadOnlyList<SentinalView> panels = tabs.GroupPanels;
            var labels = new Label[panels.Count];
            for (int i = 0; i < panels.Count; i++)
            {
                SentinalView panel = panels[i];
                var tab = new Label(panel != null ? panel.name : "Missing panel");
                tab.AddToClassList("sentinal-tab");
                tab.RegisterCallback<MouseDownEvent>(evt => SentinalEditorUI.Ping(evt, panel));
                strip.Add(tab);
                labels[i] = tab;
            }

            var more = new Label();
            more.AddToClassList("sentinal-tab");
            more.AddToClassList("sentinal-tab--more");
            strip.Add(more);

            updaters.Add(() =>
            {
                if (tabs == null || view == null)
                    return;

                bool collapsed = SentinalEditorUI.ToneOf(view) == SentinalTone.Hidden;
                int folded = 0;
                for (int i = 0; i < labels.Length; i++)
                {
                    SentinalView panel = panels[i];
                    Label tab = labels[i];
                    bool selected = tabs.CurrentTabIndex == i;
                    bool focused = panel != null && SentinalViewRouter.IsFocusWithin(panel);
                    bool visible = !collapsed || selected;
                    if (!visible)
                        folded++;

                    tab.EnableInClassList("sentinal-hidden", !visible);
                    tab.EnableInClassList("sentinal-tab--selected", selected);
                    tab.EnableInClassList("sentinal-tab--focused", focused);
                    if (panel == null)
                        continue;

                    string focusText = "";
                    if (focused)
                        focusText = ", focused";
                    else if (selected)
                        focusText = ", selected, focus is elsewhere";
                    tab.tooltip = $"{panel.name}: {SentinalEditorUI.State(panel)}{focusText}";
                }

                more.EnableInClassList("sentinal-hidden", folded == 0);
                more.text = folded == 1 ? "1 more tab" : $"{folded} more tabs";
            });
        }

        /// <summary>
        /// Identity of everything the body lays out: open views, their nesting, what covers what, and the players.
        /// </summary>
        private string Structure()
        {
            StringBuilder key = structureBuilder.Clear();
            key.Append(Application.isPlaying);
            if (!Application.isPlaying)
                return key.ToString();

            SentinalSettings settings = SentinalSettingsRegistration.Active;
            key.Append('|').Append(settings != null ? settings.GetInstanceID() : 0);
            foreach (SentinalView view in SentinalViewRouter.GetOpenViews())
            {
                if (view == null)
                    continue;

                SentinalView coveredBy = SentinalViewRouter.GetHiddenBy(view);
                key.Append('|').Append(view.GetInstanceID()).Append(':').Append(view.Layer.Id);
                key.Append(':').Append(view.Parent != null ? view.Parent.GetInstanceID() : 0);
                key.Append(':').Append(view.IsTab).Append(':').Append(view.IsPlayerView);
                key.Append(':').Append(view.Owner != null ? view.Owner.GetInstanceID() : 0);
                key.Append(':').Append(coveredBy != null ? coveredBy.GetInstanceID() : 0);
            }

            foreach (PlayerInput player in PlayerInput.all)
                key.Append("|p").Append(player.GetInstanceID());
            return key.ToString();
        }
    }
}
