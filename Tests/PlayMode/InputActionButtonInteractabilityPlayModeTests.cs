using System.Collections;
using NUnit.Framework;
using Sentinal.InputSystem.Components;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sentinal.Tests
{
    public class InputActionButtonInteractabilityPlayModeTests
    {
        private GameObject buttonObject;

        [TearDown]
        public void TearDown()
        {
            if (buttonObject != null)
                Object.DestroyImmediate(buttonObject);
        }

        [Test]
        public void DirectInputClickRequiresAnInteractableButton()
        {
            (Button button, TestInputActionButton inputButton) = CreateButton();
            inputButton.DeferExecution = false;

            int clickCount = 0;
            button.onClick.AddListener(() => clickCount++);

            button.interactable = false;
            inputButton.TriggerClick();
            Assert.That(clickCount, Is.Zero);

            button.interactable = true;
            inputButton.TriggerClick();
            Assert.That(clickCount, Is.EqualTo(1));

            button.enabled = false;
            inputButton.TriggerClick();
            Assert.That(clickCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeferredInputClickStopsWhenButtonBecomesNonInteractable()
        {
            (Button button, TestInputActionButton inputButton) = CreateButton();
            inputButton.DeferExecution = true;

            int clickCount = 0;
            button.onClick.AddListener(() => clickCount++);

            inputButton.TriggerClick();
            button.interactable = false;
            yield return null;

            Assert.That(clickCount, Is.Zero);
        }

        private (Button, TestInputActionButton) CreateButton()
        {
            buttonObject = new GameObject("Input Button", typeof(RectTransform));
            buttonObject.SetActive(false);

            Button button = buttonObject.AddComponent<Button>();
            TestInputActionButton inputButton = buttonObject.AddComponent<TestInputActionButton>();
            inputButton.SendPointerEvents = false;
            buttonObject.SetActive(true);

            return (button, inputButton);
        }
    }

    public class TestInputActionButton : InputActionButton
    {
        public void TriggerClick() => Click();
    }
}
