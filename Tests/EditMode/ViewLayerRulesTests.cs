using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Tests
{
    /// <summary>
    /// Layer, focus and Back rules that every menu in the game relies on: who hides whom, who gets focus, and what a
    /// Back press does. Breaking these strands players behind the wrong menu.
    /// </summary>
    public class ViewLayerRulesTests
    {
        private const int Dialog = 1;
        private const int Settings = 2;
        private const int Screens = 3;
        private const int Base = 4;
        private const int PlayerMenus = 5;
        private const int Swaps = 6;

        private readonly List<GameObject> created = new();
        private SentinalSettings previousSettings;
        private SentinalSettings settings;

        [SetUp]
        public void SetUp()
        {
            SentinalViewRouter.Reset();
            previousSettings = SentinalSettings.Instance;
            settings = ScriptableObject.CreateInstance<SentinalSettings>();
            var serialized = new SerializedObject(settings);
            SerializedProperty layers = serialized.FindProperty("globalLayers");
            AddLayer(layers, Dialog, LayerOthers.StayVisible, LayerBack.ClosesTheView);
            AddLayer(layers, Settings, LayerOthers.HideUntilThisCloses, LayerBack.ClosesTheView);
            AddLayer(layers, Screens, LayerOthers.HideUntilThisCloses, LayerBack.ClosesTheView);
            AddLayer(layers, Swaps, LayerOthers.Close, LayerBack.ClosesTheView);
            AddLayer(layers, PlayerMenus, LayerOthers.RefuseTheNewOne, LayerBack.ClosesTheView);
            AddLayer(layers, Base, LayerOthers.StayVisible, LayerBack.Ignored);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SentinalSettings.Register(settings);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            created.Clear();
            Object.DestroyImmediate(settings);
            SentinalSettings.Register(previousSettings);
            SentinalViewRouter.Reset();
        }

        [Test]
        public void HideLayerStacksAndRestoresInOrder()
        {
            SentinalView first = CreateView("First", Settings);
            SentinalView second = CreateView("Second", Settings);
            SentinalView third = CreateView("Third", Settings);

            first.Open();
            second.Open();
            third.Open();
            Assert.That(first.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(second.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(third));

            third.Close();
            Assert.That(second.State, Is.EqualTo(ViewState.Open));
            Assert.That(first.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(second));

            second.Close();
            Assert.That(first.State, Is.EqualTo(ViewState.Open));
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(first));
        }

        [Test]
        public void ExplicitlyClosedHiddenViewStaysClosed()
        {
            SentinalView options = CreateView("Options", Settings);
            SentinalView rebind = CreateView("Rebind", Settings);

            options.Open();
            rebind.Open();
            options.Close();
            rebind.Close();

            Assert.That(options.State, Is.EqualTo(ViewState.Closed));
            Assert.That(SentinalViewRouter.CurrentView, Is.Null);
        }

        [Test]
        public void HigherLayerKeepsFocusAndLayersNeverHideEachOther()
        {
            SentinalView dialog = CreateView("Dialog", Dialog);
            SentinalView screen = CreateView("Screen", Screens);
            SentinalView home = CreateView("Home", Base);

            home.Open();
            dialog.Open();
            screen.Open();

            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(dialog));
            Assert.That(screen.State, Is.EqualTo(ViewState.Open));
            Assert.That(home.State, Is.EqualTo(ViewState.Open));
            Assert.That(SentinalViewRouter.WouldTakeFocus(screen), Is.False);
        }

        [Test]
        public void RefuseLayerKeepsTheCurrentView()
        {
            SentinalView customisation = CreateView("Customisation", PlayerMenus);
            SentinalView settingsMenu = CreateView("WorldSettings", PlayerMenus);

            Assert.That(customisation.Open(), Is.True);
            Assert.That(settingsMenu.Open(), Is.False);
            Assert.That(customisation.State, Is.EqualTo(ViewState.Open));
            Assert.That(settingsMenu.State, Is.EqualTo(ViewState.Closed));
            Assert.That(settingsMenu.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CloseLayerClosesThePreviousViewExactlyOnce()
        {
            SentinalView first = CreateView("First", Swaps);
            SentinalView second = CreateView("Second", Swaps);
            int closedCount = 0;
            first.Closed += () => closedCount++;

            first.Open();
            second.Open();
            first.Close();

            Assert.That(first.State, Is.EqualTo(ViewState.Closed));
            Assert.That(closedCount, Is.EqualTo(1));
        }

        [Test]
        public void ClosingAWindowClosesItsNestedViews()
        {
            SentinalView window = CreateView("Window", Screens);
            SentinalView tab = CreateView("Tab", 0, window);
            SentinalView popup = CreateView("Popup", 0, tab);
            int popupClosed = 0;
            popup.Closed += () => popupClosed++;

            window.Open();
            tab.Open();
            popup.Open();
            Assert.That(SentinalViewRouter.CurrentView, Is.EqualTo(popup));

            window.Close();
            Assert.That(tab.State, Is.EqualTo(ViewState.Closed));
            Assert.That(popup.State, Is.EqualTo(ViewState.Closed));
            Assert.That(popupClosed, Is.EqualTo(1));
            Assert.That(popup.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void BackClosesPopupThenPassesThroughTabsToTheWindow()
        {
            SentinalView window = CreateView("Window", Screens);
            SentinalView tab = CreateView("Tab", 0, window);
            tab.IsTab = true;
            SentinalView popup = CreateView("Popup", 0, tab);

            window.Open();
            tab.Open();
            popup.Open();

            Assert.That(SentinalViewRouter.IsFocusWithin(window), Is.False);
            Assert.That(SentinalViewRouter.HandleBack(), Is.True);
            Assert.That(popup.State, Is.EqualTo(ViewState.Closed));
            Assert.That(SentinalViewRouter.IsFocusWithin(window), Is.True);

            Assert.That(SentinalViewRouter.HandleBack(), Is.True);
            Assert.That(window.State, Is.EqualTo(ViewState.Closed));
        }

        [Test]
        public void BackIsIgnoredOnABaseLayer()
        {
            SentinalView home = CreateView("Home", Base);

            home.Open();

            Assert.That(SentinalViewRouter.CanHandleBack(), Is.False);
            Assert.That(SentinalViewRouter.HandleBack(), Is.False);
            Assert.That(home.State, Is.EqualTo(ViewState.Open));
        }

        [Test]
        public void ViewWithoutALayerRefusesToOpen()
        {
            SentinalView view = CreateView("NoLayer", 0);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("expected a layer")
            );
            Assert.That(view.Open(), Is.False);
            Assert.That(view.State, Is.EqualTo(ViewState.Closed));
        }

        private SentinalView CreateView(string name, int layerId, SentinalView parent = null)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            if (parent != null)
                gameObject.transform.SetParent(parent.transform, false);
            else
                created.Add(gameObject);

            SentinalView view = gameObject.AddComponent<SentinalView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("layer").FindPropertyRelative("id").intValue = layerId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void AddLayer(SerializedProperty layers, int id, LayerOthers others, LayerBack back)
        {
            int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("id").intValue = id;
            layer.FindPropertyRelative("name").stringValue = $"Layer {id}";
            layer.FindPropertyRelative("others").enumValueIndex = (int)others;
            layer.FindPropertyRelative("back").enumValueIndex = (int)back;
        }
    }
}
