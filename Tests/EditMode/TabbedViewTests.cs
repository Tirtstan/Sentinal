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

        [Test]
        public void PanelsWithoutAnAuthoredOwnerAreRejectedBeforeReplacingTabs()
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            Toggle firstToggle = firstToggleObject.GetComponent<Toggle>();
            Toggle secondToggle = secondToggleObject.GetComponent<Toggle>();
            ViewSelector panel = secondToggleObject.AddComponent<ViewSelector>();
            tabbedView.ReplaceTabs(new[] { firstToggle }, System.Array.Empty<ViewSelector>());

            Assert.Throws<System.InvalidOperationException>(
                () => tabbedView.ReplaceTabs(new[] { secondToggle }, new[] { panel })
            );
            Assert.That(tabbedView.GroupToggles[0], Is.EqualTo(firstToggle));
            Assert.That(tabbedView.GroupPanels, Is.Empty);
            Assert.That(panel.FocusParent, Is.Null);
        }

        [Test]
        public void SelectPanelUsesItsRegisteredToggleAndOwnership()
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            ViewSelector owner = tabbedViewObject.AddComponent<ViewSelector>();
            ViewSelector firstPanel = firstToggleObject.AddComponent<ViewSelector>();
            ViewSelector secondPanel = secondToggleObject.AddComponent<ViewSelector>();
            Toggle firstToggle = firstToggleObject.GetComponent<Toggle>();
            Toggle secondToggle = secondToggleObject.GetComponent<Toggle>();
            tabbedView.FocusOwner = owner;
            tabbedView.ReplaceTabs(new[] { firstToggle, secondToggle }, new[] { firstPanel, secondPanel });

            tabbedView.SelectTab(secondPanel);

            Assert.That(tabbedView.CurrentTabIndex, Is.EqualTo(1));
            Assert.That(secondToggle.isOn, Is.True);
            Assert.That(firstPanel.gameObject.activeSelf, Is.False);
            Assert.That(secondPanel.gameObject.activeSelf, Is.True);
            Assert.That(secondPanel.FocusParent, Is.EqualTo(owner));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InvalidPanelSelectionDoesNotChangeTheSelectedTab(bool missing)
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            ViewSelector owner = tabbedViewObject.AddComponent<ViewSelector>();
            ViewSelector panel = firstToggleObject.AddComponent<ViewSelector>();
            Toggle toggle = firstToggleObject.GetComponent<Toggle>();
            tabbedView.FocusOwner = owner;
            tabbedView.ReplaceTabs(new[] { toggle }, new[] { panel });

            Assert.That(
                () => tabbedView.SelectTab(missing ? null : owner),
                Throws.InstanceOf<System.ArgumentException>()
            );

            Assert.That(tabbedView.CurrentTabIndex, Is.EqualTo(0));
            Assert.That(toggle.isOn, Is.True);
            Assert.That(panel.gameObject.activeSelf, Is.True);
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void NextUsesCallerWrapPolicy(bool wrap, int expectedIndex)
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            Toggle firstToggle = firstToggleObject.GetComponent<Toggle>();
            Toggle secondToggle = secondToggleObject.GetComponent<Toggle>();
            tabbedView.ReplaceTabs(new[] { firstToggle, secondToggle }, System.Array.Empty<ViewSelector>());
            tabbedView.SelectTab(1);

            tabbedView.Next(wrap);

            Assert.That(tabbedView.CurrentTabIndex, Is.EqualTo(expectedIndex));
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void PreviousUsesCallerWrapPolicy(bool wrap, int expectedIndex)
        {
            TabbedView tabbedView = tabbedViewObject.GetComponent<TabbedView>();
            Toggle firstToggle = firstToggleObject.GetComponent<Toggle>();
            Toggle secondToggle = secondToggleObject.GetComponent<Toggle>();
            tabbedView.ReplaceTabs(new[] { firstToggle, secondToggle }, System.Array.Empty<ViewSelector>());
            tabbedView.SelectTab(0);

            tabbedView.Previous(wrap);

            Assert.That(tabbedView.CurrentTabIndex, Is.EqualTo(expectedIndex));
        }
    }
}
