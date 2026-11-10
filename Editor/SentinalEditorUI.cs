using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sentinal.Editor
{
    /// <summary>How a state reads at a glance: colour of status pills and Monitor dots.</summary>
    public enum SentinalTone
    {
        Open,
        Transition,
        Hidden,
        Idle,
        Focus,
        Warning,
    }

    /// <summary>A compact read-only fact shared by every component inspector.</summary>
    public sealed class StatusFact : VisualElement
    {
        public readonly Label Text;
        public readonly Label Note;
        private readonly VisualElement square;
        private readonly Label link;
        private Object linkTarget;

        public StatusFact(VisualElement parent, string key, bool showTone = false)
        {
            AddToClassList("sentinal-fact");
            parent.Add(this);
            SentinalEditorUI.Clip(this, key, "sentinal-fact__key");
            square = SentinalEditorUI.Square(this, SentinalTone.Idle);
            square.EnableInClassList("sentinal-hidden", !showTone);
            Text = SentinalEditorUI.Clip(this, "", "sentinal-fact__text");
            link = SentinalEditorUI.Clip(this, "", "sentinal-link");
            link.AddToClassList("sentinal-hidden");
            link.RegisterCallback<MouseDownEvent>(evt => SentinalEditorUI.Ping(evt, linkTarget));
            Note = SentinalEditorUI.Clip(this, "", "sentinal-fact__note");
            Note.AddToClassList("sentinal-hidden");
        }

        public void Set(string text, SentinalTone tone = SentinalTone.Idle, Object target = null, string note = null)
        {
            Text.text = text;
            Text.tooltip = text;
            Text.EnableInClassList("sentinal-hidden", string.IsNullOrEmpty(text));
            SentinalEditorUI.SetTone(Text, tone);
            SentinalEditorUI.SetTone(square, tone);
            linkTarget = target;
            link.text = target != null ? target.name : "";
            link.tooltip = target != null ? $"Select and ping {target.name}" : null;
            link.EnableInClassList("sentinal-hidden", target == null);
            Note.text = note ?? "";
            Note.tooltip = note;
            Note.EnableInClassList("sentinal-hidden", string.IsNullOrEmpty(note));
        }
    }

    /// <summary>Shared layout and read-only runtime presentation for the package's editor tools.</summary>
    public static class SentinalEditorUI
    {
        private static readonly string[] ToneClasses =
        {
            "sentinal-tone--open",
            "sentinal-tone--transition",
            "sentinal-tone--hidden",
            "sentinal-tone--idle",
            "sentinal-tone--focus",
            "sentinal-tone--warning",
        };

        /// <summary>Root element with the package style sheet and the current skin; <paramref name="variant"/> picks the layout.</summary>
        public static VisualElement Root(string variant = null)
        {
            var root = new VisualElement();
            root.AddToClassList(EditorGUIUtility.isProSkin ? "sentinal-dark" : "sentinal-light");
            if (!string.IsNullOrEmpty(variant))
                root.AddToClassList(variant);
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.tirt.sentinal/Editor/SentinalEditor.uss"
            );
            if (sheet != null)
                root.styleSheets.Add(sheet);
            else
                Debug.LogError("[Sentinal] Expected Editor/SentinalEditor.uss; actual: stylesheet not found.");
            return root;
        }

        /// <summary>A wrapping strip of inline facts, followed by the component's authored fields.</summary>
        public static VisualElement Status(VisualElement parent)
        {
            var strip = new VisualElement();
            strip.AddToClassList("sentinal-status");
            parent.Add(strip);
            return strip;
        }

        public static void SetTone(VisualElement element, SentinalTone tone)
        {
            for (int i = 0; i < ToneClasses.Length; i++)
                element.EnableInClassList(ToneClasses[i], i == (int)tone);
        }

        public static Label Muted(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("sentinal-muted");
            parent.Add(label);
            return label;
        }

        /// <summary>A small coloured square that shows a state's tone.</summary>
        public static VisualElement Square(VisualElement parent, SentinalTone tone)
        {
            var square = new VisualElement();
            square.AddToClassList("sentinal-square");
            SetTone(square, tone);
            parent.Add(square);
            return square;
        }

        /// <summary>A single-line label that shrinks with an ellipsis.</summary>
        public static Label Clip(VisualElement parent, string text, string className = null)
        {
            var label = new Label(text);
            label.AddToClassList("sentinal-clip");
            if (className != null)
                label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        /// <summary>Link-styled label that selects and pings <paramref name="target"/>.</summary>
        public static Label Link(VisualElement parent, Object target)
        {
            var link = new Label(target != null ? target.name : "None") { tooltip = "Select and ping" };
            link.AddToClassList("sentinal-link");
            link.RegisterCallback<MouseDownEvent>(evt => Ping(evt, target));
            parent.Add(link);
            return link;
        }

        public static void Ping(MouseDownEvent evt, Object target)
        {
            if (evt.button != 0 || target == null)
                return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        /// <summary>A hoverable row that selects and pings <paramref name="target"/> when clicked.</summary>
        public static VisualElement LinkRow(VisualElement parent, Object target)
        {
            var row = new VisualElement();
            row.AddToClassList("sentinal-link-row");
            row.RegisterCallback<MouseDownEvent>(evt => Ping(evt, target));
            parent.Add(row);
            return row;
        }

        public static PropertyField Field(
            VisualElement parent,
            SerializedObject owner,
            string path,
            string label = null
        )
        {
            var field = new PropertyField(owner.FindProperty(path), label);
            parent.Add(field);
            return field;
        }

        public static void DefaultFields(VisualElement parent, SerializedObject owner)
        {
            SerializedProperty property = owner.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                children = false;
                if (property.propertyPath == "m_Script")
                    continue;
                parent.Add(new PropertyField(property.Copy()));
            }
        }

        public static string PlayerName(PlayerInput player) =>
            player != null ? $"P{player.playerIndex + 1} ({player.currentControlScheme})" : "No player";

        /// <summary>
        /// The menu object holding a top-level view, or null. Menus often keep their script on a parent that stays
        /// active and put the view on a child, so the parent's name is what identifies it. A plain Canvas parent is
        /// layout, not a menu, so it is skipped.
        /// </summary>
        public static string Holder(SentinalView view)
        {
            Transform parent = view.transform.parent;
            if (!view.IsTopLevel || parent == null || parent.TryGetComponent(out Canvas _))
                return null;
            return parent.name;
        }

        /// <summary>The view's name, prefixed with its holder when it has one: "QuitDialog › View".</summary>
        public static string Name(SentinalView view)
        {
            string holder = Holder(view);
            return holder != null ? $"{holder} › {view.name}" : view.name;
        }

        public static SentinalTone ToneOf(SentinalView view)
        {
            if (SentinalViewRouter.IsSuspended(view))
                return SentinalTone.Hidden;
            return view.State switch
            {
                ViewState.Open when SentinalViewRouter.IsCurrent(view) => SentinalTone.Focus,
                ViewState.Open => SentinalTone.Open,
                ViewState.Opening or ViewState.Closing => SentinalTone.Transition,
                ViewState.Hidden => SentinalTone.Hidden,
                _ => SentinalTone.Idle,
            };
        }

        /// <summary>The view's state on its own: "Open", "Opening 62%", "Covered by X" or "Suspended".</summary>
        public static string State(SentinalView view)
        {
            if (SentinalViewRouter.IsSuspended(view))
                return "Suspended by global UI";

            SentinalView hider = SentinalViewRouter.GetHiddenBy(view);
            if (view.State == ViewState.Hidden && hider != null)
                return $"Covered by {Name(hider)}";

            string text = view.State.ToString();
            if (view.State is ViewState.Opening or ViewState.Closing && view.TryGetComponent(out CanvasGroupFade fade))
            {
                float progress = view.State == ViewState.Opening ? fade.Progress : 1f - fade.Progress;
                text += $" {Mathf.RoundToInt(progress * 100f)}%";
            }
            return text;
        }

        /// <summary>Where focus is relative to the view, or null when it has nothing to do with it.</summary>
        public static string Focus(SentinalView view)
        {
            if (SentinalViewRouter.IsCurrent(view))
                return "focused";
            if (SentinalViewRouter.IsFocusWithin(view))
                return "focus within";
            return null;
        }

        /// <summary>What a Back press would do, for buttons and tooltips. Never runs a handler.</summary>
        public static string Describe(BackPreview preview) =>
            preview.Outcome switch
            {
                BackOutcome.Closes => $"closes {Name(preview.View)}",
                BackOutcome.Handler => $"{Name(preview.View)} handles it",
                BackOutcome.Ignored => $"ignored by {Name(preview.View)}'s layer",
                _ => "does nothing",
            };
    }
}
