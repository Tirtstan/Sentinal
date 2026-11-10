#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using System.Linq;
using Sentinal.InputSystem;
using Sentinal.InputSystem.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sentinal.Tests
{
    /// <summary>
    /// Shared Play Mode setup: registered test settings with Global and Player layers, an action asset with Player,
    /// UI and Global maps, and three players (two gamepads and a keyboard). Player one starts with UI authority.
    /// </summary>
    public abstract class SentinalInputFixture : InputTestFixture
    {
        // Global layers, top to bottom.
        protected const int High = 101;
        protected const int Low = 102;
        protected const int Floor = 103;

        // Player layers, top to bottom.
        protected const int Menus = 201;
        protected const int Stacked = 202;
        protected const int World = 203;

        private readonly List<Object> objects = new();
        private SentinalSettings previous;

        protected InputActionAsset Actions { get; private set; }
        protected Gamepad FirstPad { get; private set; }
        protected Gamepad SecondPad { get; private set; }
        protected Keyboard Keyboard { get; private set; }
        protected PlayerInput First { get; private set; }
        protected PlayerInput Second { get; private set; }
        protected PlayerInput Keys { get; private set; }

        public override void Setup()
        {
            base.Setup();
            SentinalViewRouter.Reset();
            previous = SentinalSettings.Instance;

            Actions = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            InputActionMap gameplay = Actions.AddActionMap("Player");
            gameplay.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            gameplay.AddAction("Charge", InputActionType.Button, "<Gamepad>/buttonSouth");
            InputActionMap ui = Actions.AddActionMap("UI");
            ui.AddAction("Charge", InputActionType.Button, "<Gamepad>/buttonSouth");
            ui.AddAction("Hold", InputActionType.Button, "<Gamepad>/buttonSouth", interactions: "hold(duration=0.5)");
            ui.AddAction("Cancel", InputActionType.Button, "<Gamepad>/buttonEast");
            ui.AddAction("Navigate", InputActionType.PassThrough, "<Gamepad>/leftStick");
            ui.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonSouth");
            InputActionMap global = Actions.AddActionMap("Global");
            global.AddAction("Pause", InputActionType.Button, "<Gamepad>/start").AddBinding("<Keyboard>/p");
            global.AddAction("Back", InputActionType.Button);
            Actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            Actions.AddControlScheme("Keyboard").WithRequiredDevice("<Keyboard>");

            SentinalSettings settings = Track(ScriptableObject.CreateInstance<SentinalSettings>());
            var data = new SerializedObject(settings);
            SerializedProperty globalLayers = data.FindProperty("globalLayers");
            AddLayer(globalLayers, High, LayerOthers.StayVisible, LayerInput.EveryoneToUI);
            AddLayer(globalLayers, Low, LayerOthers.StayVisible, LayerInput.EveryoneToUI);
            AddLayer(globalLayers, Floor, LayerOthers.StayVisible, LayerInput.NoChange, LayerBack.Ignored);
            SerializedProperty playerLayers = data.FindProperty("playerLayers");
            AddLayer(playerLayers, Menus, LayerOthers.RefuseTheNewOne, LayerInput.OwnerToUI);
            AddLayer(playerLayers, Stacked, LayerOthers.HideUntilThisCloses, LayerInput.OwnerToUI);
            AddLayer(playerLayers, World, LayerOthers.StayVisible, LayerInput.OwnerToUI, LayerBack.Ignored);
            SetMap(data.FindProperty("uiActionMap"), ui);
            SerializedProperty alwaysOn = data.FindProperty("alwaysOnActionMaps");
            alwaysOn.arraySize = 1;
            SetMap(alwaysOn.GetArrayElementAtIndex(0), global);
            SerializedProperty scenes = data.FindProperty("sceneBaseMaps");
            scenes.arraySize = 1;
            SerializedProperty scene = scenes.GetArrayElementAtIndex(0);
            scene.FindPropertyRelative("scenePath").stringValue = SceneManager.GetActiveScene().path;
            SetMap(scene.FindPropertyRelative("map"), gameplay);
            data.ApplyModifiedPropertiesWithoutUndo();
            SentinalSettings.Register(settings);

            FirstPad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            SecondPad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            Keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            First = CreatePlayer(FirstPad, "Gamepad");
            Second = CreatePlayer(SecondPad, "Gamepad");
            Keys = CreatePlayer(Keyboard, "Keyboard");
            SentinalViewRouter.SetAuthorityPlayer(First);
        }

        public override void TearDown()
        {
            foreach (PlayerInput player in PlayerInputMaps.Players.ToArray())
                PlayerInputMaps.Unregister(player);
            foreach (Object item in objects.AsEnumerable().Reverse())
                if (item != null)
                    Object.DestroyImmediate(item);
            objects.Clear();
            SentinalSettings.Register(previous);
            SentinalViewRouter.Reset();
            base.TearDown();
        }

        protected T Track<T>(T item)
            where T : Object
        {
            objects.Add(item);
            return item;
        }

        /// <summary>Creates an inactive GameObject that is destroyed on tear-down.</summary>
        protected GameObject CreateObject(string name, Transform parent = null)
        {
            GameObject item = Track(new GameObject(name));
            item.SetActive(false);
            if (parent != null)
                item.transform.SetParent(parent, false);
            return item;
        }

        /// <summary>Creates a closed view. Layer 0 leaves it unlayered, as a nested view.</summary>
        protected SentinalView CreateView(int layerId, SentinalView parent = null)
        {
            GameObject item = CreateObject("View " + layerId, parent != null ? parent.transform : null);
            SentinalView view = item.AddComponent<SentinalView>();
            var data = new SerializedObject(view);
            data.FindProperty("layer").FindPropertyRelative("id").intValue = layerId;
            data.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>Creates an active <see cref="OpenViewOnAction"/> bound to Global/Pause that toggles its view.</summary>
        protected OpenViewOnAction CreateOpener(
            SentinalView view,
            bool onlyWhenNoMenuOpen = false,
            bool opensOnUnhandledBack = false
        )
        {
            GameObject item = CreateObject("Opener");
            OpenViewOnAction opener = item.AddComponent<OpenViewOnAction>();
            var data = new SerializedObject(opener);
            data.FindProperty("action").objectReferenceValue = Track(
                InputActionReference.Create(Actions.FindAction("Global/Pause"))
            );
            data.FindProperty("view").objectReferenceValue = view;
            data.FindProperty("pressingAgain").intValue = (int)PressingAgain.ClosesIt;
            data.FindProperty("onlyWhenNoMenuOpen").boolValue = onlyWhenNoMenuOpen;
            data.FindProperty("opensOnUnhandledBack").boolValue = opensOnUnhandledBack;
            data.ApplyModifiedPropertiesWithoutUndo();
            item.SetActive(true);
            return opener;
        }

        protected void CreateBackHandler()
        {
            GameObject item = CreateObject("Back router");
            item.AddComponent<ViewDismissalInputHandler>();
            item.SetActive(true);
        }

        protected void PressAndRelease(ButtonControl button)
        {
            Press(button);
            Release(button);
        }

        private PlayerInput CreatePlayer(InputDevice device, string scheme)
        {
            GameObject item = CreateObject(scheme);
            PlayerInput player = item.AddComponent<PlayerInput>();
            player.actions = Track(Object.Instantiate(Actions));
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            player.neverAutoSwitchControlSchemes = true;
            player.defaultControlScheme = scheme;
            item.SetActive(true);
            player.SwitchCurrentControlScheme(scheme, device);
            return player;
        }

        private static void SetMap(SerializedProperty property, InputActionMap map)
        {
            property.FindPropertyRelative("asset").objectReferenceValue = map.asset;
            property.FindPropertyRelative("id").stringValue = map.id.ToString();
        }

        private static void AddLayer(
            SerializedProperty layers,
            int id,
            LayerOthers others,
            LayerInput input,
            LayerBack back = LayerBack.ClosesTheView
        )
        {
            int index = layers.arraySize++;
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("id").intValue = id;
            layer.FindPropertyRelative("name").stringValue = "Layer " + id;
            layer.FindPropertyRelative("others").intValue = (int)others;
            layer.FindPropertyRelative("back").intValue = (int)back;
            layer.FindPropertyRelative("inputWhileOpen").intValue = (int)input;
        }
    }
}
#endif
