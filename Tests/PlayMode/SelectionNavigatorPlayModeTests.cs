using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class SelectionNavigatorPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeNavigatorsRegisterAndUnregister()
        {
            SelectionNavigator navigator = CreateNavigator("Runtime", Vector2.zero);
            yield return null;

            Assert.That(SelectionNavigator.ActiveNavigators.Contains(navigator), Is.True);

            navigator.enabled = false;
            Assert.That(SelectionNavigator.ActiveNavigators.Contains(navigator), Is.False);

            navigator.enabled = true;
            Assert.That(SelectionNavigator.ActiveNavigators.Contains(navigator), Is.True);

            Object.Destroy(navigator.gameObject);
            yield return null;

            Assert.That(SelectionNavigator.ActiveNavigators.Contains(navigator), Is.False);
        }

        [UnityTest]
        public IEnumerator SuccessfulMoveUsesEventDatasEventSystem()
        {
            var firstEventSystemObject = new GameObject(
                "First EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            var secondEventSystemObject = new GameObject(
                "Second EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            EventSystem firstEventSystem = firstEventSystemObject.GetComponent<EventSystem>();
            EventSystem secondEventSystem = secondEventSystemObject.GetComponent<EventSystem>();
            SelectionNavigator source = CreateNavigator("Source", Vector2.zero);
            SelectionNavigator target = CreateNavigator("Target", new Vector2(200f, 0f));
            yield return null;

            secondEventSystem.SetSelectedGameObject(source.gameObject);
            var eventData = new AxisEventData(firstEventSystem)
            {
                moveVector = Vector2.right,
                moveDir = MoveDirection.Right,
            };

            source.OnMove(eventData);

            Assert.That(firstEventSystem.currentSelectedGameObject, Is.EqualTo(target.gameObject));
            Assert.That(eventData.used, Is.True);

            Object.Destroy(source.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(firstEventSystemObject);
            Object.Destroy(secondEventSystemObject);
        }

        [UnityTest]
        public IEnumerator PreferredTargetsUseAuthoredOrder()
        {
            SelectionNavigator source = CreateNavigator("Source", Vector2.zero);
            SelectionNavigator farFallback = CreateNavigator("Far", new Vector2(-300f, 0f));
            SelectionNavigator nearFallback = CreateNavigator("Near", new Vector2(-100f, 0f));
            source.ReplacePreferredTargets(SelectionNavigationDirection.Right, new[] { farFallback, nearFallback });
            yield return null;

            SelectionNavigator target = source.FindTarget(SelectionNavigationDirection.Right);

            Assert.That(target, Is.EqualTo(farFallback));

            Object.Destroy(source.gameObject);
            Object.Destroy(farFallback.gameObject);
            Object.Destroy(nearFallback.gameObject);
        }

        private static SelectionNavigator CreateNavigator(string name, Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 50f);
            rectTransform.anchoredPosition = anchoredPosition;
            return gameObject.AddComponent<SelectionNavigator>();
        }
    }
}
