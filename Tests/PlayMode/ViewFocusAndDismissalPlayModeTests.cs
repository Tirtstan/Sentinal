using NUnit.Framework;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class ViewFocusAndDismissalPlayModeTests
    {
        private GameObject menuObject;
        private GameObject overlayObject;

        [TearDown]
        public void TearDown()
        {
            if (overlayObject != null)
                Object.DestroyImmediate(overlayObject);
            if (menuObject != null)
                Object.DestroyImmediate(menuObject);
        }

        [Test]
        public void CloseCurrentViewUsesParentCloseHandlerForFocusedTab()
        {
            menuObject = new GameObject("Menu");
            menuObject.SetActive(false);
            menuObject.AddComponent<ViewSelector>();
            TestParentCloseHandler closeHandler = menuObject.AddComponent<TestParentCloseHandler>();

            GameObject tabObject = new GameObject("Tab");
            tabObject.SetActive(false);
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();

            menuObject.SetActive(true);
            tabObject.SetActive(true);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));

            SentinalViewRouter.CloseCurrentView();

            Assert.That(closeHandler.CloseCount, Is.EqualTo(1));
            Assert.That(menuObject.activeSelf, Is.False);
        }

        [Test]
        public void TabSwitchInputFollowsCurrentTabAndStopsForOverlayOrReplacedTabs()
        {
            menuObject = new GameObject("Menu");
            menuObject.SetActive(false);
            ViewSelector menuView = menuObject.AddComponent<ViewSelector>();
            ViewInputSystemHandler inputHandler = menuObject.AddComponent<ViewInputSystemHandler>();
            inputHandler.ViewSelector = menuView;
            TabbedView tabbedView = menuObject.AddComponent<TabbedView>();
            TestTabbedViewInputHandler tabInput = menuObject.AddComponent<TestTabbedViewInputHandler>();
            tabInput.SetViewInputHandler(inputHandler);
            tabInput.TabbedView = tabbedView;
            tabInput.IncludeTabPanelsInFocus = true;

            GameObject tabObject = new GameObject("Tab");
            tabObject.SetActive(false);
            tabObject.transform.SetParent(menuObject.transform);
            ViewSelector tabView = tabObject.AddComponent<ViewSelector>();
            GameObject toggleObject = new GameObject("Toggle", typeof(Toggle));
            toggleObject.transform.SetParent(menuObject.transform);

            menuObject.SetActive(true);
            tabbedView.ReplaceTabs(new[] { toggleObject.GetComponent<Toggle>() }, new[] { tabView });
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(inputHandler.IsInputEnabled(), Is.False);
            Assert.That(tabInput.Subscribed, Is.True);

            overlayObject = new GameObject("Overlay");
            ViewSelector overlayView = overlayObject.AddComponent<ViewSelector>();
            overlayView.Priority = 20;
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(overlayView));
            Assert.That(tabInput.Subscribed, Is.False);

            overlayObject.SetActive(false);
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(tabView));
            Assert.That(tabInput.Subscribed, Is.True);

            tabbedView.ReplaceTabs(System.Array.Empty<Toggle>(), System.Array.Empty<ViewSelector>());
            Assert.That(tabInput.Subscribed, Is.False);
        }
    }

    public class TestParentCloseHandler : MonoBehaviour, ICloseableView
    {
        public int CloseCount { get; private set; }

        public void Close()
        {
            CloseCount++;
            gameObject.SetActive(false);
        }
    }

    public class TestTabbedViewInputHandler : TabbedViewInputHandler
    {
        public bool Subscribed => isSubscribed;

        protected override void Subscribe() => isSubscribed = true;

        protected override void Unsubscribe() => isSubscribed = false;
    }
}
