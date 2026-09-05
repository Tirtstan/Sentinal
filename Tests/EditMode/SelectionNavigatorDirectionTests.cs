using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class SelectionNavigatorDirectionTests
    {
        private GameObject gameObject;
        private SelectionNavigator navigator;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Navigator", typeof(RectTransform), typeof(Button));
            navigator = gameObject.AddComponent<SelectionNavigator>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void DefaultsToCardinalDirections()
        {
            Assert.That(navigator.AllowedDirections, Is.EqualTo(SelectionNavigationDirection.Cardinal));
        }

        [Test]
        public void DiagonalInputUsesEnabledDiagonal()
        {
            navigator.AllowedDirections = SelectionNavigationDirection.Cardinal | SelectionNavigationDirection.UpRight;

            bool resolved = navigator.TryResolveDirection(new Vector2(1f, 1f), out var direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(SelectionNavigationDirection.UpRight));
        }

        [Test]
        public void DisabledDiagonalFallsBackToVerticalOnTie()
        {
            bool resolved = navigator.TryResolveDirection(new Vector2(1f, 1f), out var direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(SelectionNavigationDirection.Up));
        }

        [Test]
        public void DisabledPrimaryAxisDoesNotFallBackToSecondaryAxis()
        {
            navigator.AllowedDirections = SelectionNavigationDirection.Right;

            bool resolved = navigator.TryResolveDirection(new Vector2(0.5f, 1f), out var direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(SelectionNavigationDirection.None));
        }

        [Test]
        public void DisabledCardinalInputDoesNotResolveToEnabledAxis()
        {
            navigator.AllowedDirections = SelectionNavigationDirection.Up | SelectionNavigationDirection.Down;

            bool resolved = navigator.TryResolveDirection(Vector2.right, out var direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(SelectionNavigationDirection.None));
        }

        [Test]
        public void DisabledAxesDoNotResolve()
        {
            navigator.AllowedDirections = SelectionNavigationDirection.Left;

            bool resolved = navigator.TryResolveDirection(new Vector2(1f, 1f), out var direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(SelectionNavigationDirection.None));
        }

        [Test]
        public void ValidPreferredTargetIsChosenRegardlessOfGeometry()
        {
            var overrideObject = new GameObject("Override", typeof(RectTransform), typeof(Button));
            try
            {
                var preferredTarget = overrideObject.AddComponent<SelectionNavigator>();
                overrideObject.transform.position = new Vector3(-100f, 0f, 0f);

                navigator.ReplacePreferredTargets(SelectionNavigationDirection.Right, new[] { preferredTarget });

                Assert.That(navigator.FindTarget(SelectionNavigationDirection.Right), Is.EqualTo(preferredTarget));
            }
            finally
            {
                Object.DestroyImmediate(overrideObject);
            }
        }

        [Test]
        public void InvalidPreferredTargetFallsThroughToTheNextPreferredTarget()
        {
            var overrideObject = new GameObject("Override", typeof(RectTransform), typeof(Button));
            var fallbackObject = new GameObject("Fallback", typeof(RectTransform), typeof(Button));
            try
            {
                var invalidPreferredTarget = overrideObject.AddComponent<SelectionNavigator>();
                var validPreferredTarget = fallbackObject.AddComponent<SelectionNavigator>();
                overrideObject.SetActive(false);

                navigator.ReplacePreferredTargets(
                    SelectionNavigationDirection.Right,
                    new[] { invalidPreferredTarget, validPreferredTarget }
                );

                Assert.That(navigator.FindTarget(SelectionNavigationDirection.Right), Is.EqualTo(validPreferredTarget));
            }
            finally
            {
                Object.DestroyImmediate(overrideObject);
                Object.DestroyImmediate(fallbackObject);
            }
        }

        [Test]
        public void AutomaticSearchCanUseExplicitCandidates()
        {
            var targetObject = new GameObject("Target", typeof(RectTransform), typeof(Button));
            try
            {
                var target = targetObject.AddComponent<SelectionNavigator>();
                targetObject.transform.position = new Vector3(100f, 0f, 0f);

                SelectionNavigator resolved = navigator.FindAutomaticTarget(
                    SelectionNavigationDirection.Right,
                    new[] { target }
                );

                Assert.That(resolved, Is.EqualTo(target));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void AutomaticSearchPrefersNearerOffsetTargetOverFarAlignedTarget()
        {
            SizeNavigator(gameObject);
            var nearObject = CreateNavigatorObject("Near", new Vector2(40f, 80f));
            var farObject = CreateNavigatorObject("Far", new Vector2(0f, 400f));
            try
            {
                var nearTarget = nearObject.GetComponent<SelectionNavigator>();
                var farTarget = farObject.GetComponent<SelectionNavigator>();

                SelectionNavigator resolved = navigator.FindAutomaticTarget(
                    SelectionNavigationDirection.Up,
                    new[] { farTarget, nearTarget }
                );

                Assert.That(resolved, Is.EqualTo(nearTarget));
            }
            finally
            {
                Object.DestroyImmediate(nearObject);
                Object.DestroyImmediate(farObject);
            }
        }

        [Test]
        public void AutomaticSearchFindsOverlappingAdjacentTarget()
        {
            // Regression: PauseMenu Resume (649x144) sits 8px above Restart (458x113).
            // Slight layout rotation expands screen AABBs until they overlap, and the
            // previous edge-to-edge metric then compared the adjacent target's
            // center-distance against the farther candidate's edge-distance,
            // skipping down to the Options button instead of Restart.
            GameObject sourceObject = CreateNavigatorObject(
                "Source",
                new Vector2(-603.79f, 137.44f),
                new Vector2(649.27f, 144.46f)
            );
            GameObject adjacentObject = CreateNavigatorObject(
                "Adjacent",
                new Vector2(-701.45f, 21f),
                new Vector2(458.71f, 113.52f)
            );
            GameObject fartherObject = CreateNavigatorObject(
                "Farther",
                new Vector2(-700.82f, -119.41f),
                new Vector2(464.21f, 113.52f)
            );
            try
            {
                var source = sourceObject.GetComponent<SelectionNavigator>();
                source.SetSearchAngle(SelectionNavigationDirection.Down, 90f);
                var adjacent = adjacentObject.GetComponent<SelectionNavigator>();
                var farther = fartherObject.GetComponent<SelectionNavigator>();

                SelectionNavigator resolved = source.FindAutomaticTarget(
                    SelectionNavigationDirection.Down,
                    new[] { farther, adjacent }
                );

                Assert.That(resolved, Is.EqualTo(adjacent));
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(adjacentObject);
                Object.DestroyImmediate(fartherObject);
            }
        }

        [Test]
        public void WrapFindsOppositeEndWhenNothingFound()
        {
            GameObject topObject = CreateNavigatorObject("Top", new Vector2(0f, 200f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(0f, 100f));
            GameObject bottomObject = CreateNavigatorObject("Bottom", new Vector2(0f, 0f));
            try
            {
                var source = topObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Up;
                var top = topObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var bottom = bottomObject.GetComponent<SelectionNavigator>();

                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Up, new[] { top, middle, bottom }),
                    Is.EqualTo(bottom)
                );
            }
            finally
            {
                Object.DestroyImmediate(topObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(bottomObject);
            }
        }

        [Test]
        public void AutomaticSearchTakesPrecedenceOverWrap()
        {
            GameObject topObject = CreateNavigatorObject("Top", new Vector2(0f, 200f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(0f, 100f));
            GameObject bottomObject = CreateNavigatorObject("Bottom", new Vector2(0f, 0f));
            try
            {
                var source = middleObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Up;
                var top = topObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var bottom = bottomObject.GetComponent<SelectionNavigator>();

                // FindTarget consults automatic search before wrap targets.
                Assert.That(
                    source.FindAutomaticTarget(SelectionNavigationDirection.Up, new[] { top, middle, bottom }),
                    Is.EqualTo(top)
                );
                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Up, new[] { top, middle, bottom }),
                    Is.EqualTo(bottom)
                );
            }
            finally
            {
                Object.DestroyImmediate(topObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(bottomObject);
            }
        }

        [Test]
        public void PreferredTargetsTakePrecedenceOverWrap()
        {
            GameObject topObject = CreateNavigatorObject("Top", new Vector2(0f, 200f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(0f, 100f));
            GameObject bottomObject = CreateNavigatorObject("Bottom", new Vector2(0f, 0f));
            try
            {
                var source = topObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Up;
                var middle = middleObject.GetComponent<SelectionNavigator>();
                source.ReplacePreferredTargets(SelectionNavigationDirection.Up, new[] { middle });

                Assert.That(source.FindTarget(SelectionNavigationDirection.Up), Is.EqualTo(middle));
            }
            finally
            {
                Object.DestroyImmediate(topObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(bottomObject);
            }
        }

        [Test]
        public void NoWrapAlongShortAxis()
        {
            GameObject leftObject = CreateNavigatorObject("Left", new Vector2(0f, 300f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(100f, 300f));
            GameObject rightObject = CreateNavigatorObject("Right", new Vector2(200f, 300f));
            try
            {
                var source = middleObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Down;
                var left = leftObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var right = rightObject.GetComponent<SelectionNavigator>();

                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Down, new[] { left, middle, right }),
                    Is.Null
                );
            }
            finally
            {
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        [Test]
        public void WrapDisabledByDefault()
        {
            GameObject topObject = CreateNavigatorObject("Top", new Vector2(0f, 200f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(0f, 100f));
            GameObject bottomObject = CreateNavigatorObject("Bottom", new Vector2(0f, 0f));
            try
            {
                var source = topObject.GetComponent<SelectionNavigator>();
                var top = topObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var bottom = bottomObject.GetComponent<SelectionNavigator>();

                Assert.That(source.WrapDirections, Is.EqualTo(SelectionNavigationDirection.None));
                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Up, new[] { top, middle, bottom }),
                    Is.Null
                );
            }
            finally
            {
                Object.DestroyImmediate(topObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(bottomObject);
            }
        }

        [Test]
        public void WrapSkipsInvalidTargets()
        {
            GameObject topObject = CreateNavigatorObject("Top", new Vector2(0f, 200f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(0f, 100f));
            GameObject bottomObject = CreateNavigatorObject("Bottom", new Vector2(0f, 0f));
            try
            {
                var source = topObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Up;
                var top = topObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var bottom = bottomObject.GetComponent<SelectionNavigator>();
                bottomObject.SetActive(false);

                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Up, new[] { top, middle, bottom }),
                    Is.EqualTo(middle)
                );
            }
            finally
            {
                Object.DestroyImmediate(topObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(bottomObject);
            }
        }

        [Test]
        public void WrapFindsOppositeEndInRow()
        {
            GameObject leftObject = CreateNavigatorObject("Left", new Vector2(0f, 300f));
            GameObject middleObject = CreateNavigatorObject("Middle", new Vector2(100f, 300f));
            GameObject rightObject = CreateNavigatorObject("Right", new Vector2(200f, 300f));
            try
            {
                var source = leftObject.GetComponent<SelectionNavigator>();
                source.WrapDirections = SelectionNavigationDirection.Left;
                var left = leftObject.GetComponent<SelectionNavigator>();
                var middle = middleObject.GetComponent<SelectionNavigator>();
                var right = rightObject.GetComponent<SelectionNavigator>();

                Assert.That(
                    source.FindWrapTarget(SelectionNavigationDirection.Left, new[] { left, middle, right }),
                    Is.EqualTo(right)
                );
            }
            finally
            {
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(middleObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        private static void SizeNavigator(GameObject target)
        {
            var rectTransform = target.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 50f);
        }

        private static GameObject CreateNavigatorObject(string name, Vector2 anchoredPosition)
        {
            return CreateNavigatorObject(name, anchoredPosition, new Vector2(100f, 50f));
        }

        private static GameObject CreateNavigatorObject(string name, Vector2 anchoredPosition, Vector2 size)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(Button));
            target.GetComponent<RectTransform>().sizeDelta = size;
            target.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
            target.AddComponent<SelectionNavigator>();
            return target;
        }
    }
}
