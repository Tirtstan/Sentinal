using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sentinal.Editor
{
    [CustomEditor(typeof(SelectionNavigator))]
    [CanEditMultipleObjects]
    public sealed class SelectionNavigatorEditor : UnityEditor.Editor
    {
        private const float DirectionButtonHeight = 24f;
        private const float WrapStripHeight = 18f;
        private const float DirectionGroupPadding = 3f;
        private const float PresetButtonHeight = 20f;
        private const float PresetSeparation = 5f;

        private static readonly Color upColor = new(0.2f, 0.9f, 0.42f, 1f);
        private static readonly Color downColor = new(1f, 0.82f, 0.18f, 1f);
        private static readonly Color leftColor = new(0.32f, 0.48f, 1f, 1f);
        private static readonly Color rightColor = new(1f, 0.3f, 0.3f, 1f);
        private static readonly Color wrapColor = new(0.35f, 0.85f, 1f, 1f);
        private static readonly Color wrapToggleTint = new(0.62f, 0.9f, 1f, 1f);
        private static readonly Color invalidLinkColor = new(1f, 0.3f, 0.3f, 1f);
        private static readonly Color outlineColor = new(0f, 0f, 0f, 0.8f);
        private static readonly Vector3[] worldCorners = new Vector3[4];

        private enum LinkKind
        {
            Preferred,
            Automatic,
            Wrap,
        }

        private static readonly SelectionNavigationDirection[] allDirections =
        {
            SelectionNavigationDirection.Up,
            SelectionNavigationDirection.Down,
            SelectionNavigationDirection.Left,
            SelectionNavigationDirection.Right,
            SelectionNavigationDirection.UpLeft,
            SelectionNavigationDirection.UpRight,
            SelectionNavigationDirection.DownLeft,
            SelectionNavigationDirection.DownRight,
        };

        private SerializedProperty navigationMask;
        private SerializedProperty navigationMaskValue;
        private SerializedProperty allowedDirections;
        private SerializedProperty wrapDirections;
        private SerializedProperty priority;
        private SerializedProperty defaultSearchAngle;
        private SerializedProperty diagonalThreshold;
        private readonly Dictionary<SelectionNavigationDirection, SerializedProperty> directionSettings = new();
        private GUIStyle linkLabelStyle;
        private Texture2D labelBackground;

        private void OnEnable()
        {
            labelBackground = CreateLabelBackground();
            navigationMask = serializedObject.FindProperty("navigationMask");
            navigationMaskValue = navigationMask?.FindPropertyRelative("value");
            allowedDirections = serializedObject.FindProperty("allowedDirections");
            wrapDirections = serializedObject.FindProperty("wrapDirections");
            priority = serializedObject.FindProperty("priority");
            defaultSearchAngle = serializedObject.FindProperty("defaultSearchAngle");
            diagonalThreshold = serializedObject.FindProperty("diagonalThreshold");

            directionSettings[SelectionNavigationDirection.Up] = serializedObject.FindProperty("up");
            directionSettings[SelectionNavigationDirection.Down] = serializedObject.FindProperty("down");
            directionSettings[SelectionNavigationDirection.Left] = serializedObject.FindProperty("left");
            directionSettings[SelectionNavigationDirection.Right] = serializedObject.FindProperty("right");
            directionSettings[SelectionNavigationDirection.UpLeft] = serializedObject.FindProperty("upLeft");
            directionSettings[SelectionNavigationDirection.UpRight] = serializedObject.FindProperty("upRight");
            directionSettings[SelectionNavigationDirection.DownLeft] = serializedObject.FindProperty("downLeft");
            directionSettings[SelectionNavigationDirection.DownRight] = serializedObject.FindProperty("downRight");
        }

        private void OnDisable()
        {
            if (labelBackground != null)
                DestroyImmediate(labelBackground);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(navigationMask);
            EditorGUILayout.PropertyField(priority);

            DrawDirections();

            DrawDiagnostics();

            EditorGUILayout.PropertyField(defaultSearchAngle);
            if (HasDiagonalDirections())
                EditorGUILayout.PropertyField(diagonalThreshold);

            DrawDirectionSettings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDirections()
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Directions", EditorStyles.boldLabel);

            GUILayout.Space(2f);

            DrawCombinedRow(
                SelectionNavigationDirection.UpLeft,
                "↖ UpLeft",
                SelectionNavigationDirection.Up,
                "↑ Up",
                SelectionNavigationDirection.UpRight,
                "↗ UpRight"
            );

            GUILayout.Space(4f);

            DrawCombinedRow(
                SelectionNavigationDirection.Left,
                "← Left",
                null,
                "•",
                SelectionNavigationDirection.Right,
                "Right →"
            );

            GUILayout.Space(4f);

            DrawCombinedRow(
                SelectionNavigationDirection.DownLeft,
                "↙ DnLeft",
                SelectionNavigationDirection.Down,
                "↓ Down",
                SelectionNavigationDirection.DownRight,
                "↘ DnRight"
            );

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            GUILayout.Space(2f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(DirectionGroupPadding);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(DirectionGroupPadding);

                    DrawPresetsRow();

                    GUILayout.Space(DirectionGroupPadding);
                }

                GUILayout.Space(DirectionGroupPadding);
            }

            EditorGUILayout.Space(2);
        }

        private void DrawCombinedRow(
            SelectionNavigationDirection leftDir,
            string leftLabel,
            SelectionNavigationDirection? midDir,
            string midLabel,
            SelectionNavigationDirection rightDir,
            string rightLabel
        )
        {
            const float spacing = 2f;
            float rowHeight = DirectionGroupPadding * 2f + DirectionButtonHeight + WrapStripHeight;
            Rect rect = EditorGUILayout.GetControlRect(false, rowHeight);
            float width = (rect.width - spacing * 2f) / 3f;

            Rect leftRect = new(rect.x, rect.y, width, rowHeight);
            Rect midRect = new(rect.x + width + spacing, rect.y, width, rowHeight);
            Rect rightRect =
                new(rect.x + (width + spacing) * 2f, rect.y, rect.width - (width + spacing) * 2f, rowHeight);

            DrawCombinedCell(leftRect, leftDir, leftLabel);

            if (midDir.HasValue)
            {
                DrawCombinedCell(midRect, midDir.Value, midLabel);
            }
            else
            {
                GUI.Box(midRect, GUIContent.none, EditorStyles.helpBox);
                Rect placeholderRect =
                    new(
                        midRect.x + DirectionGroupPadding,
                        midRect.y + DirectionGroupPadding,
                        midRect.width - DirectionGroupPadding * 2f,
                        midRect.height - DirectionGroupPadding * 2f
                    );
                GUI.Label(placeholderRect, midLabel, EditorStyles.centeredGreyMiniLabel);
            }

            DrawCombinedCell(rightRect, rightDir, rightLabel);
        }

        private void DrawCombinedCell(Rect rect, SelectionNavigationDirection direction, string label)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect directionRect =
                new(
                    rect.x + DirectionGroupPadding,
                    rect.y + DirectionGroupPadding,
                    rect.width - DirectionGroupPadding * 2f,
                    DirectionButtonHeight
                );
            Rect wrapRect = new(directionRect.x, directionRect.yMax, directionRect.width, WrapStripHeight);

            string name = DirectionDisplayName(direction);
            string kind = (direction & SelectionNavigationDirection.Diagonal) != 0 ? "diagonal" : "cardinal";

            bool isAllowed = IsDirectionEnabled(
                allowedDirections,
                static nav => nav.AllowedDirections,
                direction,
                out bool allowedMixed
            );
            EditorGUI.showMixedValue = allowedMixed;
            bool pressed = GUI.Toggle(
                directionRect,
                isAllowed,
                new GUIContent(label, $"Allow {name} {kind} navigation."),
                EditorStyles.miniButton
            );
            EditorGUI.showMixedValue = false;

            if (pressed != isAllowed)
                ToggleDirection("allowedDirections", "Toggle Allowed Direction", direction, pressed);

            bool isWrap = IsDirectionEnabled(
                wrapDirections,
                static nav => nav.WrapDirections,
                direction,
                out bool wrapMixed
            );
            EditorGUI.showMixedValue = wrapMixed;
            Color previousBackground = GUI.backgroundColor;
            if (isWrap)
                GUI.backgroundColor = wrapToggleTint;

            bool wrapPressed;
            using (new EditorGUI.DisabledScope(!allowedMixed && !isAllowed))
            {
                wrapPressed = GUI.Toggle(
                    wrapRect,
                    isWrap,
                    new GUIContent("↺ Wrap", $"Opposite end of the sibling list when {name} finds nothing."),
                    EditorStyles.miniButton
                );
            }

            GUI.backgroundColor = previousBackground;
            EditorGUI.showMixedValue = false;

            if (wrapPressed != isWrap)
                ToggleDirection("wrapDirections", "Toggle Wrap Direction", direction, wrapPressed);
        }

        private static string DirectionDisplayName(SelectionNavigationDirection direction)
        {
            return direction switch
            {
                SelectionNavigationDirection.UpLeft => "Up-Left",
                SelectionNavigationDirection.UpRight => "Up-Right",
                SelectionNavigationDirection.DownLeft => "Down-Left",
                SelectionNavigationDirection.DownRight => "Down-Right",
                _ => direction.ToString(),
            };
        }

        private void DrawPresetsRow()
        {
            bool isMixed = allowedDirections.hasMultipleDifferentValues;
            int currentMask = isMixed ? -1 : (allowedDirections.intValue & (int)SelectionNavigationDirection.All);

            bool isCardinal = currentMask == (int)SelectionNavigationDirection.Cardinal;
            bool isDiagonal = currentMask == (int)SelectionNavigationDirection.Diagonal;
            bool isAll = currentMask == (int)SelectionNavigationDirection.All;
            bool isNone = currentMask == (int)SelectionNavigationDirection.None;

            Rect rect = EditorGUILayout.GetControlRect(false, PresetButtonHeight);
            float width = Mathf.Floor(rect.width / 4f);

            Rect r0 = new(rect.x, rect.y, width, rect.height);
            Rect r1 = new(rect.x + width, rect.y, width, rect.height);
            Rect r2 = new(rect.x + width * 2f, rect.y, width, rect.height);
            Rect r3 = new(rect.x + width * 3f, rect.y, rect.width - width * 3f, rect.height);

            bool newCardinal = GUI.Toggle(r0, isCardinal, "Cardinal", EditorStyles.miniButtonLeft);
            if (newCardinal != isCardinal)
                SetDirections(SelectionNavigationDirection.Cardinal);

            bool newDiagonal = GUI.Toggle(r1, isDiagonal, "Diagonal", EditorStyles.miniButtonMid);
            if (newDiagonal != isDiagonal)
                SetDirections(SelectionNavigationDirection.Diagonal);

            bool newAll = GUI.Toggle(r2, isAll, "All", EditorStyles.miniButtonMid);
            if (newAll != isAll)
                SetDirections(SelectionNavigationDirection.All);

            bool newNone = GUI.Toggle(r3, isNone, "None", EditorStyles.miniButtonRight);
            if (newNone != isNone)
                SetDirections(SelectionNavigationDirection.None);
        }

        private bool IsDirectionEnabled(
            SerializedProperty maskProp,
            Func<SelectionNavigator, SelectionNavigationDirection> maskGetter,
            SelectionNavigationDirection direction,
            out bool isMixed
        )
        {
            int mask = (int)direction;
            int first = maskProp.intValue & mask;
            isMixed = false;

            if (serializedObject.isEditingMultipleObjects)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    var nav = (SelectionNavigator)targets[i];
                    int current = (int)maskGetter(nav) & mask;
                    if (current != first)
                    {
                        isMixed = true;
                        break;
                    }
                }
            }

            return first != 0;
        }

        private void ToggleDirection(
            string propertyName,
            string toggleUndoLabel,
            SelectionNavigationDirection direction,
            bool enabled
        )
        {
            int mask = (int)direction;
            Undo.RecordObjects(targets, toggleUndoLabel);
            foreach (var targetObj in serializedObject.targetObjects)
            {
                var so = new SerializedObject(targetObj);
                var prop = so.FindProperty(propertyName);
                if (prop != null)
                {
                    prop.intValue = enabled ? (prop.intValue | mask) : (prop.intValue & ~mask);
                    prop.serializedObject.ApplyModifiedProperties();
                }
            }
            serializedObject.Update();
        }

        private void SetDirections(SelectionNavigationDirection directions)
        {
            Undo.RecordObjects(targets, "Set Allowed Directions");
            foreach (var targetObj in serializedObject.targetObjects)
            {
                var so = new SerializedObject(targetObj);
                var prop = so.FindProperty("allowedDirections");
                if (prop != null)
                {
                    prop.intValue = (int)(directions & SelectionNavigationDirection.All);
                    prop.serializedObject.ApplyModifiedProperties();
                }
            }
            serializedObject.Update();
        }

        private void DrawDiagnostics()
        {
            if (
                navigationMaskValue != null
                && !navigationMaskValue.hasMultipleDifferentValues
                && navigationMaskValue.intValue == 0
            )
            {
                EditorGUILayout.HelpBox(
                    "Navigation Mask is Nothing. This navigator cannot select or be selected by another navigator.",
                    MessageType.Warning
                );
            }

            if (targets.Length != 1)
                return;

            var navigator = (SelectionNavigator)target;
            if (
                navigator.Selectable != null
                && navigator.Selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None
            )
            {
                EditorGUILayout.HelpBox(
                    "The attached Selectable must use Navigation Mode None. Sentinal will restore it automatically.",
                    MessageType.Warning
                );
            }

            SelectionNavigationDirection directions = (SelectionNavigationDirection)allowedDirections.intValue;
            for (int i = 0; i < allDirections.Length; i++)
            {
                SelectionNavigationDirection direction = allDirections[i];
                if ((directions & direction) == 0)
                    continue;

                DrawDirectionDiagnostics(navigator, direction);
            }
        }

        private static void DrawDirectionDiagnostics(
            SelectionNavigator navigator,
            SelectionNavigationDirection direction
        )
        {
            SelectionDirectionSettings settings = navigator.GetDirectionSettings(direction);
            var seen = new HashSet<SelectionNavigator>();

            for (int i = 0; i < settings.PreferredTargets.Count; i++)
            {
                SelectionNavigator preferredTarget = settings.PreferredTargets[i];
                if (preferredTarget == null)
                    continue;

                if (preferredTarget == navigator)
                {
                    EditorGUILayout.HelpBox(
                        $"{direction} preferred target {i} references itself.",
                        MessageType.Warning
                    );
                    continue;
                }

                if (!seen.Add(preferredTarget))
                {
                    EditorGUILayout.HelpBox(
                        $"{direction} contains duplicate preferred target '{preferredTarget.name}'.",
                        MessageType.Warning
                    );
                }

                if (!MasksOverlap(navigator, preferredTarget))
                {
                    EditorGUILayout.HelpBox(
                        $"{direction} preferred target '{preferredTarget.name}' does not share a Navigation Mask.",
                        MessageType.Warning
                    );
                }
            }
        }

        private void DrawDirectionSettings()
        {
            SelectionNavigationDirection directions = (SelectionNavigationDirection)allowedDirections.intValue;

            for (int i = 0; i < allDirections.Length; i++)
            {
                SelectionNavigationDirection direction = allDirections[i];
                if ((directions & direction) == 0)
                    continue;

                DrawDirectionSection(directionSettings[direction], direction);
            }
        }

        private static void DrawDirectionSection(SerializedProperty property, SelectionNavigationDirection direction)
        {
            if (property == null)
                return;

            property.isExpanded = EditorGUILayout.Foldout(
                property.isExpanded,
                ObjectNames.NicifyVariableName(direction.ToString()),
                toggleOnLabelClick: true
            );
            if (!property.isExpanded)
                return;

            SerializedProperty overrideSearchAngle = property.FindPropertyRelative("overrideSearchAngle");
            SerializedProperty searchAngle = property.FindPropertyRelative("searchAngle");
            SerializedProperty preferredTargets = property.FindPropertyRelative("preferredTargets");

            using (new EditorGUI.IndentLevelScope())
            {
                if (overrideSearchAngle != null)
                {
                    EditorGUILayout.PropertyField(overrideSearchAngle);
                    if (
                        searchAngle != null
                        && (overrideSearchAngle.boolValue || overrideSearchAngle.hasMultipleDifferentValues)
                    )
                        EditorGUILayout.PropertyField(searchAngle);
                }

                if (preferredTargets != null)
                    EditorGUILayout.PropertyField(preferredTargets);
            }
        }

        private bool HasDiagonalDirections() =>
            ((SelectionNavigationDirection)allowedDirections.intValue & SelectionNavigationDirection.Diagonal) != 0;

        private static bool MasksOverlap(SelectionNavigator source, SelectionNavigator target) =>
            (source.NavigationMask & target.NavigationMask) != ViewGroupMask.Nothing;

        private void OnSceneGUI()
        {
            var navigator = (SelectionNavigator)target;
            if (navigator == null)
                return;

            SelectionNavigationDirection directions = navigator.AllowedDirections;
            IReadOnlyList<SelectionNavigator> navigators = FindNavigatorsInContext(navigator);

            for (int i = 0; i < allDirections.Length; i++)
            {
                SelectionNavigationDirection direction = allDirections[i];
                if ((directions & direction) == 0)
                    continue;

                SelectionDirectionSettings settings = navigator.GetDirectionSettings(direction);
                bool hasValidPreferredTarget = false;
                for (int preferredIndex = 0; preferredIndex < settings.PreferredTargets.Count; preferredIndex++)
                {
                    SelectionNavigator preferredTarget = settings.PreferredTargets[preferredIndex];
                    if (preferredTarget != null)
                        DrawLink(navigator, preferredTarget, direction, LinkKind.Preferred);

                    if (navigator.IsValidTarget(preferredTarget))
                    {
                        hasValidPreferredTarget = true;
                        break;
                    }
                }

                if (hasValidPreferredTarget)
                    continue;

                DrawAutomaticSearchCone(navigator, direction);

                SelectionNavigator automaticTarget = navigator.FindAutomaticTarget(direction, navigators);
                if (automaticTarget != null)
                {
                    DrawLink(navigator, automaticTarget, direction, LinkKind.Automatic);
                    continue;
                }

                SelectionNavigator wrapTarget = navigator.FindWrapTarget(direction, navigators);
                if (wrapTarget != null)
                    DrawLink(navigator, wrapTarget, direction, LinkKind.Wrap);
            }
        }

        private static IReadOnlyList<SelectionNavigator> FindNavigatorsInContext(SelectionNavigator navigator)
        {
            var navigators = new List<SelectionNavigator>(
                UnityEngine.Object.FindObjectsByType<SelectionNavigator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            );

            // Preview scenes (e.g. an open prefab stage) are invisible to FindObjectsByType,
            // so the selected navigator would otherwise be matched against unrelated scenes.
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(navigator.gameObject);
            if (stage != null)
            {
                foreach (GameObject root in stage.scene.GetRootGameObjects())
                    root.GetComponentsInChildren(true, navigators);
            }

            return navigators;
        }

        private void DrawLink(
            SelectionNavigator source,
            SelectionNavigator target,
            SelectionNavigationDirection direction,
            LinkKind linkKind
        )
        {
            if (source == null || target == null)
                return;

            Vector2 directionVector = SelectionNavigator.GetDirectionVector(direction);
            Vector3 sourcePosition = GetDirectionalPoint(source, directionVector);
            Vector3 targetPosition = GetDirectionalPoint(target, -directionVector);
            Vector3 line = targetPosition - sourcePosition;
            if (line.sqrMagnitude <= Mathf.Epsilon)
                return;

            bool isValid = source.IsValidTarget(target);
            Color directionColor = GetDirectionColor(direction);
            Color linkColor =
                linkKind == LinkKind.Wrap && isValid
                    ? wrapColor
                    : isValid
                        ? directionColor
                        : invalidLinkColor;
            float sourceHandleSize = HandleUtility.GetHandleSize(sourcePosition);
            float targetHandleSize = HandleUtility.GetHandleSize(targetPosition);
            Vector3 labelPosition = targetPosition - GetSceneCameraUp() * targetHandleSize * 0.28f;

            Handles.color = outlineColor;
            if (linkKind == LinkKind.Preferred)
                Handles.DrawAAPolyLine(7f, sourcePosition, targetPosition);
            else
                Handles.DrawDottedLine(sourcePosition, targetPosition, 5f);

            Handles.color = linkColor;
            if (linkKind == LinkKind.Preferred)
                Handles.DrawAAPolyLine(4f, sourcePosition, targetPosition);
            else
                Handles.DrawDottedLine(sourcePosition, targetPosition, 3f);

            DrawEndpoint(sourcePosition, sourceHandleSize, linkColor);
            DrawEndpoint(targetPosition, targetHandleSize, linkColor);

            Handles.Label(
                labelPosition,
                linkKind == LinkKind.Wrap ? target.name + " (wrap)" : target.name,
                LinkLabelStyle
            );
        }

        private static void DrawAutomaticSearchCone(
            SelectionNavigator navigator,
            SelectionNavigationDirection direction
        )
        {
            float searchAngle = navigator.GetSearchAngle(direction);
            if (searchAngle <= Mathf.Epsilon)
                return;

            Vector2 screenDirection = SelectionNavigator.GetDirectionVector(direction);
            Vector3 origin = GetDirectionalPoint(navigator, screenDirection);
            var rectTransform = (RectTransform)navigator.transform;
            Vector3 planeNormal = Vector3.Cross(rectTransform.right, rectTransform.up).normalized;
            Vector3 worldDirection = (
                rectTransform.right * screenDirection.x + rectTransform.up * screenDirection.y
            ).normalized;
            float radius = HandleUtility.GetHandleSize(origin) * 0.7f;
            float totalAngle = searchAngle * 2f;
            Vector3 arcStart = Quaternion.AngleAxis(-searchAngle, planeNormal) * worldDirection;
            Vector3 arcEnd = Quaternion.AngleAxis(searchAngle, planeNormal) * worldDirection;
            Color color = GetDirectionColor(direction);

            Handles.color = new Color(color.r, color.g, color.b, 0.18f);
            Handles.DrawSolidArc(origin, planeNormal, arcStart, totalAngle, radius);

            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawWireArc(origin, planeNormal, arcStart, totalAngle, radius);
            Handles.DrawAAPolyLine(2f, origin, origin + arcStart * radius);
            Handles.DrawAAPolyLine(2f, origin, origin + arcEnd * radius);
        }

        private GUIStyle LinkLabelStyle
        {
            get
            {
                if (linkLabelStyle != null)
                    return linkLabelStyle;

                linkLabelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(3, 3, 1, 1),
                };
                linkLabelStyle.normal.background = labelBackground;
                linkLabelStyle.normal.textColor = Color.white;
                return linkLabelStyle;
            }
        }

        private static Texture2D CreateLabelBackground()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, };
            texture.SetPixel(0, 0, new Color(0.04f, 0.14f, 0.24f, 1f));
            texture.Apply();
            return texture;
        }

        private static Vector3 GetDirectionalPoint(SelectionNavigator navigator, Vector2 direction)
        {
            var rectTransform = (RectTransform)navigator.transform;
            rectTransform.GetWorldCorners(worldCorners);

            Canvas canvas = navigator.GetComponentInParent<Canvas>();
            Camera eventCamera =
                canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[0]);
            Vector2 maximum = minimum;

            for (int i = 1; i < worldCorners.Length; i++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[i]);
                minimum = Vector2.Min(minimum, corner);
                maximum = Vector2.Max(maximum, corner);
            }

            Vector2 screenPoint =
                new(
                    direction.x > 0f
                        ? maximum.x
                        : direction.x < 0f
                            ? minimum.x
                            : (minimum.x + maximum.x) * 0.5f,
                    direction.y > 0f
                        ? maximum.y
                        : direction.y < 0f
                            ? minimum.y
                            : (minimum.y + maximum.y) * 0.5f
                );

            return RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out Vector3 worldPoint
            )
                ? worldPoint
                : navigator.transform.position;
        }

        private static void DrawEndpoint(Vector3 position, float handleSize, Color color)
        {
            Vector3 cameraForward = GetSceneCameraForward();

            Handles.color = outlineColor;
            Handles.DrawSolidDisc(position, cameraForward, handleSize * 0.085f);

            Handles.color = color;
            Handles.DrawSolidDisc(position, cameraForward, handleSize * 0.05f);
        }

        private static Color GetDirectionColor(SelectionNavigationDirection direction)
        {
            return direction switch
            {
                SelectionNavigationDirection.Up => upColor,
                SelectionNavigationDirection.Down => downColor,
                SelectionNavigationDirection.Left => leftColor,
                SelectionNavigationDirection.Right => rightColor,
                SelectionNavigationDirection.UpLeft => Color.Lerp(upColor, leftColor, 0.5f),
                SelectionNavigationDirection.UpRight => Color.Lerp(upColor, rightColor, 0.5f),
                SelectionNavigationDirection.DownLeft => Color.Lerp(downColor, leftColor, 0.5f),
                SelectionNavigationDirection.DownRight => Color.Lerp(downColor, rightColor, 0.5f),
                _ => Color.white,
            };
        }

        private static Vector3 GetSceneCameraForward()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView;
            return sceneView != null && sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward;
        }

        private static Vector3 GetSceneCameraUp()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView;
            return sceneView != null && sceneView.camera != null ? sceneView.camera.transform.up : Vector3.up;
        }
    }
}
