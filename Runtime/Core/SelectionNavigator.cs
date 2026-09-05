using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sentinal
{
    /// <summary>
    /// Resolves uGUI selection moves using spatial search, masks, and authored preferred targets.
    /// </summary>
    [AddComponentMenu("Sentinal/Selection Navigator")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Selectable))]
    public sealed class SelectionNavigator : MonoBehaviour, IMoveHandler
    {
        internal const float DefaultSearchAngleValue = 60f;
        private const float DefaultDiagonalThresholdValue = 0.65f;
        private const float ScoreTieTolerance = 0.0001f;

        private static readonly List<SelectionNavigator> activeNavigators = new();
        private static readonly ReadOnlyCollection<SelectionNavigator> readOnlyActiveNavigators =
            activeNavigators.AsReadOnly();

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("Only navigators with at least one overlapping group can select each other.")]
        private ViewGroupMask navigationMask = 1;

        [SerializeField]
        [Tooltip("Directions handled by this navigator. Cardinal directions are enabled by default.")]
        private SelectionNavigationDirection allowedDirections = SelectionNavigationDirection.Cardinal;

        [SerializeField]
        [Tooltip(
            "Opposite end of the sibling list when nothing is found in these directions. Only applies along a list's long axis."
        )]
        private SelectionNavigationDirection wrapDirections = SelectionNavigationDirection.None;

        [SerializeField]
        [Tooltip("Higher-priority targets win when spatial scores are nearly equal.")]
        private int priority;

        [Header("Search")]
        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Maximum angle in degrees from the requested direction for automatic candidates.")]
        private float defaultSearchAngle = DefaultSearchAngleValue;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Minimum minor-axis ratio required to interpret input as diagonal.")]
        private float diagonalThreshold = DefaultDiagonalThresholdValue;

        [Header("Directions")]
        [SerializeField]
        private SelectionDirectionSettings up = new();

        [SerializeField]
        private SelectionDirectionSettings down = new();

        [SerializeField]
        private SelectionDirectionSettings left = new();

        [SerializeField]
        private SelectionDirectionSettings right = new();

        [SerializeField]
        private SelectionDirectionSettings upLeft = new();

        [SerializeField]
        private SelectionDirectionSettings upRight = new();

        [SerializeField]
        private SelectionDirectionSettings downLeft = new();

        [SerializeField]
        private SelectionDirectionSettings downRight = new();

        private readonly Vector3[] worldCorners = new Vector3[4];
        private Selectable selectable;
        private RectTransform rectTransform;
        private Canvas canvas;
        private bool canvasCached;
        private bool warnedNavigationMode;
        private bool warnedMissingEventSystem;

        public ViewGroupMask NavigationMask
        {
            get => navigationMask;
            set => navigationMask = value;
        }

        public SelectionNavigationDirection AllowedDirections
        {
            get => allowedDirections;
            set => allowedDirections = value & SelectionNavigationDirection.All;
        }

        public SelectionNavigationDirection WrapDirections
        {
            get => wrapDirections;
            set => wrapDirections = value & SelectionNavigationDirection.All;
        }

        public int Priority
        {
            get => priority;
            set => priority = value;
        }

        public float DefaultSearchAngle
        {
            get => defaultSearchAngle;
            set => defaultSearchAngle = Mathf.Clamp(value, 0f, 180f);
        }

        public float DiagonalThreshold
        {
            get => diagonalThreshold;
            set => diagonalThreshold = Mathf.Clamp01(value);
        }

        public Selectable Selectable
        {
            get
            {
                CacheComponents();
                return selectable;
            }
        }

        public static IReadOnlyList<SelectionNavigator> ActiveNavigators => readOnlyActiveNavigators;

        private void Awake()
        {
            CacheComponents();
            EnforceSelectableNavigation(warn: false);
        }

        private void OnEnable()
        {
            CacheComponents();
            EnforceSelectableNavigation(warn: false);
            Register(this);
        }

        private void OnDisable() => Unregister(this);

        private void OnDestroy() => Unregister(this);

        private void OnTransformParentChanged() => CacheCanvas();

        private void Reset()
        {
            navigationMask = 1;
            allowedDirections = SelectionNavigationDirection.Cardinal;
            wrapDirections = SelectionNavigationDirection.None;
            defaultSearchAngle = DefaultSearchAngleValue;
            diagonalThreshold = DefaultDiagonalThresholdValue;

            CacheComponents();
            EnforceSelectableNavigation(warn: false);
        }

        private void OnValidate()
        {
            allowedDirections &= SelectionNavigationDirection.All;
            wrapDirections &= SelectionNavigationDirection.All;
            defaultSearchAngle = Mathf.Clamp(defaultSearchAngle, 0f, 180f);
            diagonalThreshold = Mathf.Clamp01(diagonalThreshold);

            up.Normalize();
            down.Normalize();
            left.Normalize();
            right.Normalize();
            upLeft.Normalize();
            upRight.Normalize();
            downLeft.Normalize();
            downRight.Normalize();

            CacheComponents();
            EnforceSelectableNavigation(warn: false);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));

            EnforceSelectableNavigation(warn: true);

            if (!TryResolveDirection(eventData.moveVector, out SelectionNavigationDirection direction))
                return;

            SelectionNavigator target = FindTarget(direction);
            if (target == null)
                return;

            EventSystem eventSystem = ResolveEventSystem(eventData);
            if (eventSystem == null)
            {
                if (!warnedMissingEventSystem)
                {
                    Debug.LogWarning(
                        $"[{nameof(SelectionNavigator)}] '{name}' received a move without an EventSystem. Selection was not changed.",
                        this
                    );
                    warnedMissingEventSystem = true;
                }

                return;
            }

            eventSystem.SetSelectedGameObject(target.gameObject, eventData);
            eventData.Use();
        }

        private static EventSystem ResolveEventSystem(AxisEventData eventData)
        {
            BaseInputModule inputModule = eventData.currentInputModule;
            return inputModule != null ? inputModule.GetComponent<EventSystem>() : null;
        }

        public bool TryResolveDirection(Vector2 moveVector, out SelectionNavigationDirection direction)
        {
            direction = SelectionNavigationDirection.None;
            float absoluteX = Mathf.Abs(moveVector.x);
            float absoluteY = Mathf.Abs(moveVector.y);

            if (absoluteX <= Mathf.Epsilon && absoluteY <= Mathf.Epsilon)
                return false;

            SelectionNavigationDirection horizontal =
                moveVector.x < 0f ? SelectionNavigationDirection.Left : SelectionNavigationDirection.Right;
            SelectionNavigationDirection vertical =
                moveVector.y < 0f ? SelectionNavigationDirection.Down : SelectionNavigationDirection.Up;

            float major = Mathf.Max(absoluteX, absoluteY);
            float minor = Mathf.Min(absoluteX, absoluteY);
            bool diagonalIntent = major > Mathf.Epsilon && minor / major >= diagonalThreshold;

            if (diagonalIntent)
            {
                SelectionNavigationDirection diagonal = CombineDirections(vertical, horizontal);
                if (IsDirectionAllowed(diagonal))
                {
                    direction = diagonal;
                    return true;
                }
            }

            SelectionNavigationDirection primary = absoluteY >= absoluteX ? vertical : horizontal;

            if (IsDirectionAllowed(primary))
            {
                direction = primary;
                return true;
            }

            return false;
        }

        public SelectionNavigator FindTarget(SelectionNavigationDirection direction)
        {
            if (!IsSingleDirection(direction) || !IsDirectionAllowed(direction))
                return null;

            SelectionDirectionSettings settings = GetDirectionSettings(direction);
            SelectionNavigator preferredTarget = FindPreferredTarget(settings);
            if (preferredTarget != null)
                return preferredTarget;

            SelectionNavigator automaticTarget = FindAutomaticTarget(direction);
            if (automaticTarget != null)
                return automaticTarget;

            return FindWrapTarget(direction);
        }

        public bool IsWrapEnabled(SelectionNavigationDirection direction) =>
            (wrapDirections & direction) != SelectionNavigationDirection.None;

        public SelectionDirectionSettings GetDirectionSettings(SelectionNavigationDirection direction)
        {
            return direction switch
            {
                SelectionNavigationDirection.Up => up,
                SelectionNavigationDirection.Down => down,
                SelectionNavigationDirection.Left => left,
                SelectionNavigationDirection.Right => right,
                SelectionNavigationDirection.UpLeft => upLeft,
                SelectionNavigationDirection.UpRight => upRight,
                SelectionNavigationDirection.DownLeft => downLeft,
                SelectionNavigationDirection.DownRight => downRight,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Expected one direction."),
            };
        }

        public void ReplacePreferredTargets(
            SelectionNavigationDirection direction,
            IReadOnlyList<SelectionNavigator> targets
        ) => GetDirectionSettings(direction).ReplacePreferredTargets(targets);

        /// <summary>
        /// Returns the effective search angle for one direction.
        /// </summary>
        public float GetSearchAngle(SelectionNavigationDirection direction)
        {
            SelectionDirectionSettings settings = GetDirectionSettings(direction);
            return settings.OverridesSearchAngle ? settings.SearchAngle : defaultSearchAngle;
        }

        public void SetSearchAngle(SelectionNavigationDirection direction, float angle) =>
            GetDirectionSettings(direction).SetSearchAngle(angle);

        public void ClearSearchAngle(SelectionNavigationDirection direction) =>
            GetDirectionSettings(direction).ClearSearchAngle();

        public bool IsValidTarget(SelectionNavigator target)
        {
            if (target == null || target == this || !target.isActiveAndEnabled)
                return false;

            target.CacheComponents();
            if (!target.gameObject.activeInHierarchy || target.selectable == null)
                return false;

            if (!target.selectable.enabled || !target.selectable.IsInteractable())
                return false;

            return (navigationMask & target.navigationMask) != ViewGroupMask.Nothing;
        }

        private SelectionNavigator FindAutomaticTarget(SelectionNavigationDirection direction) =>
            FindAutomaticTarget(direction, activeNavigators);

        /// <summary>
        /// Finds the best spatial target from an explicit navigator set.
        /// </summary>
        public SelectionNavigator FindAutomaticTarget(
            SelectionNavigationDirection direction,
            IReadOnlyList<SelectionNavigator> candidates
        )
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (!IsSingleDirection(direction) || !IsDirectionAllowed(direction))
                return null;

            CacheComponents();
            Vector2 directionVector = GetDirectionVector(direction);
            GetScreenRect(out Vector2 sourceMin, out Vector2 sourceMax);
            Vector2 sourceEdgeCenter = GetFacingEdgeCenter(directionVector, sourceMin, sourceMax);
            float minimumDot = Mathf.Cos(GetSearchAngle(direction) * Mathf.Deg2Rad);
            SelectionNavigator bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                SelectionNavigator candidate = candidates[i];
                if (!IsValidTarget(candidate))
                    continue;

                candidate.GetScreenRect(out Vector2 targetMin, out Vector2 targetMax);
                Vector2 toTarget = (targetMin + targetMax) * 0.5f - sourceEdgeCenter;

                float distance = toTarget.magnitude;
                if (distance <= Mathf.Epsilon)
                    continue;

                float alignment = Vector2.Dot(directionVector, toTarget / distance);
                if (alignment < minimumDot)
                    continue;

                if (IsBetterAutomaticTarget(candidate, distance, bestTarget, bestDistance))
                {
                    bestTarget = candidate;
                    bestDistance = distance;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Finds the wrap target at the opposite end of the sibling list.
        /// Only applies along the sibling list's long axis, so rows wrap
        /// horizontally, columns wrap vertically, and grids do not wrap.
        /// </summary>
        public SelectionNavigator FindWrapTarget(SelectionNavigationDirection direction) =>
            FindWrapTarget(direction, activeNavigators);

        /// <summary>
        /// Finds the wrap target at the opposite end of the sibling list
        /// from an explicit navigator set.
        /// </summary>
        public SelectionNavigator FindWrapTarget(
            SelectionNavigationDirection direction,
            IReadOnlyList<SelectionNavigator> candidates
        )
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (!IsSingleDirection(direction) || !IsDirectionAllowed(direction) || !IsWrapEnabled(direction))
                return null;

            CacheComponents();
            Vector2 directionVector = GetDirectionVector(direction);
            Vector2 crossVector = new Vector2(-directionVector.y, directionVector.x);
            Transform parent = transform.parent;
            GetScreenRect(out Vector2 sourceMin, out Vector2 sourceMax);
            Vector2 ownCenter = (sourceMin + sourceMax) * 0.5f;

            float minAlong = Vector2.Dot(ownCenter, directionVector);
            float maxAlong = minAlong;
            float minCross = Vector2.Dot(ownCenter, crossVector);
            float maxCross = minCross;

            SelectionNavigator bestTarget = null;
            float bestAlong = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                SelectionNavigator candidate = candidates[i];
                if (candidate == null || candidate == this)
                    continue;

                if (candidate.transform.parent != parent)
                    continue;

                candidate.GetScreenRect(out Vector2 targetMin, out Vector2 targetMax);
                Vector2 targetCenter = (targetMin + targetMax) * 0.5f;
                float along = Vector2.Dot(targetCenter, directionVector);
                float cross = Vector2.Dot(targetCenter, crossVector);
                minAlong = Mathf.Min(minAlong, along);
                maxAlong = Mathf.Max(maxAlong, along);
                minCross = Mathf.Min(minCross, cross);
                maxCross = Mathf.Max(maxCross, cross);

                if (!IsValidTarget(candidate))
                    continue;

                if (along < bestAlong)
                {
                    bestTarget = candidate;
                    bestAlong = along;
                }
            }

            if (bestTarget == null || maxAlong - minAlong <= maxCross - minCross)
                return null;

            return bestTarget;
        }

        private SelectionNavigator FindPreferredTarget(SelectionDirectionSettings settings)
        {
            List<SelectionNavigator> preferredTargets = settings.GetMutablePreferredTargets();
            for (int i = 0; i < preferredTargets.Count; i++)
            {
                SelectionNavigator candidate = preferredTargets[i];
                if (IsValidTarget(candidate))
                    return candidate;
            }

            return null;
        }

        private bool IsBetterAutomaticTarget(
            SelectionNavigator candidate,
            float distance,
            SelectionNavigator bestTarget,
            float bestDistance
        )
        {
            if (bestTarget == null || distance < bestDistance - ScoreTieTolerance)
                return true;

            return Mathf.Abs(distance - bestDistance) <= ScoreTieTolerance && candidate.priority > bestTarget.priority;
        }

        private void GetScreenRect(out Vector2 min, out Vector2 max)
        {
            CacheComponents();
            rectTransform.GetWorldCorners(worldCorners);
            Camera eventCamera = GetEventCamera();
            min = max = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[0]);

            for (int i = 1; i < worldCorners.Length; i++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[i]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }

        private static Vector2 GetFacingEdgeCenter(Vector2 direction, Vector2 min, Vector2 max)
        {
            Vector2 center = (min + max) * 0.5f;
            float x =
                direction.x > 0f
                    ? max.x
                    : direction.x < 0f
                        ? min.x
                        : center.x;
            float y =
                direction.y > 0f
                    ? max.y
                    : direction.y < 0f
                        ? min.y
                        : center.y;
            return new Vector2(x, y);
        }

        private Camera GetEventCamera()
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        private void CacheComponents()
        {
            if (selectable == null)
                TryGetComponent(out selectable);

            if (rectTransform == null)
                TryGetComponent(out rectTransform);

            if (!canvasCached)
                CacheCanvas();
        }

        private void CacheCanvas()
        {
            canvas = GetComponentInParent<Canvas>();
            canvasCached = true;
        }

        private void EnforceSelectableNavigation(bool warn)
        {
            if (selectable == null)
                return;

            Navigation navigation = selectable.navigation;
            if (navigation.mode == Navigation.Mode.None)
                return;

            if (warn && !warnedNavigationMode)
            {
                Debug.LogWarning(
                    $"[{nameof(SelectionNavigator)}] '{name}' expected Selectable.navigation.mode to be None but found {navigation.mode}. Sentinal restored None.",
                    this
                );
                warnedNavigationMode = true;
            }

            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        private bool IsDirectionAllowed(SelectionNavigationDirection direction) => (allowedDirections & direction) != 0;

        private static bool IsSingleDirection(SelectionNavigationDirection direction) =>
            direction != SelectionNavigationDirection.None
            && (direction & SelectionNavigationDirection.All) == direction
            && ((int)direction & ((int)direction - 1)) == 0;

        private static SelectionNavigationDirection CombineDirections(
            SelectionNavigationDirection vertical,
            SelectionNavigationDirection horizontal
        )
        {
            if (vertical == SelectionNavigationDirection.Up)
            {
                return horizontal == SelectionNavigationDirection.Left
                    ? SelectionNavigationDirection.UpLeft
                    : SelectionNavigationDirection.UpRight;
            }

            return horizontal == SelectionNavigationDirection.Left
                ? SelectionNavigationDirection.DownLeft
                : SelectionNavigationDirection.DownRight;
        }

        public static Vector2 GetDirectionVector(SelectionNavigationDirection direction)
        {
            return direction switch
            {
                SelectionNavigationDirection.Up => Vector2.up,
                SelectionNavigationDirection.Down => Vector2.down,
                SelectionNavigationDirection.Left => Vector2.left,
                SelectionNavigationDirection.Right => Vector2.right,
                SelectionNavigationDirection.UpLeft => new Vector2(-1f, 1f).normalized,
                SelectionNavigationDirection.UpRight => new Vector2(1f, 1f).normalized,
                SelectionNavigationDirection.DownLeft => new Vector2(-1f, -1f).normalized,
                SelectionNavigationDirection.DownRight => new Vector2(1f, -1f).normalized,
                _ => Vector2.zero,
            };
        }

        private static void Register(SelectionNavigator navigator)
        {
            if (navigator != null && !activeNavigators.Contains(navigator))
                activeNavigators.Add(navigator);
        }

        private static void Unregister(SelectionNavigator navigator)
        {
            if (navigator != null)
                activeNavigators.Remove(navigator);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeNavigators.Clear();
    }
}
