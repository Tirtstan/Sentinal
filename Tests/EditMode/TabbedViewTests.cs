using NUnit.Framework;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class TabbedViewTests
    {
        private GameObject tabbedViewObject;
        private GameObject firstToggleObject;
        private GameObject secondToggleObject;

        [SetUp]
        public void SetUp()
        {
            tabbedViewObject = new GameObject("Tabbed View", typeof(ToggleGroup), typeof(TabbedView));
            firstToggleObject = new GameObject("First Toggle", typeof(Toggle));
            secondToggleObject = new GameObject("Second Toggle", typeof(Toggle));
        }

        [TearDown]
        public void TearDown()
        {
            if (tabbedViewObject != null)
                Object.DestroyImmediate(tabbedViewObject);

            if (firstToggleObject != null)
                Object.DestroyImmediate(firstToggleObject);

            if (secondToggleObject != null)
                Object.DestroyImmediate(secondToggleObject);
        }

        [Test]
        public void SelectTabNotifiesToggleListeners()
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            Toggle firstToggle = firstToggleObject.GetComponent<Toggle>();
            Toggle secondToggle = secondToggleObject.GetComponent<Toggle>();
            bool didNotifySecondToggle = false;

            secondToggle.onValueChanged.AddListener(isOn => didNotifySecondToggle = isOn);
            tabbedView.ReplaceTabs(new[] { firstToggle, secondToggle }, System.Array.Empty<ViewSelector>());

            tabbedView.SelectTab(1);

            Assert.That(secondToggle.isOn, Is.True);
            Assert.That(didNotifySecondToggle, Is.True);
        }
    }
}
