using Sentinal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal.Samples
{
    public sealed class SampleMenus : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SentinalSettings settings;

        [SerializeField]
        private SentinalView screen;

        [SerializeField]
        private SentinalView dialog;

        [SerializeField]
        private SentinalView tabs;

        [SerializeField]
        private SentinalView[] playerMenus;

        [SerializeField]
        private PlayerInput[] players;
        private SentinalSettings previous;

        private void Awake()
        {
            previous = SentinalSettings.Instance;
            SentinalSettings.Register(settings);
        }

        private void Start() => screen.Open();

        public void OpenDialog() => dialog.Open();

        public void OpenTabs() => tabs.Open();

        public void CloseDialog() => dialog.Close();

        public void CloseTabs() => tabs.Close();

        public void OpenFirstPlayer() => playerMenus[0].Open(players[0]);

        public void OpenSecondPlayer() => playerMenus[1].Open(players[1]);

        private void OnDestroy() => SentinalSettings.Register(previous);
    }
}
