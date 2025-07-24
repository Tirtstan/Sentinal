using TMPro;
using UnityEngine;

namespace Sentinal.Samples
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuLogText : MonoBehaviour
    {
        private TextMeshProUGUI menuLogText;

        private void Awake()
        {
            menuLogText = GetComponent<TextMeshProUGUI>();
            Sentinal.Instance.OnSwitch += OnSwitch;
            UpdateText();
        }

        private void OnSwitch(SentinalViewSelector view1, SentinalViewSelector view2) => UpdateText();

        private void UpdateText() => menuLogText.SetText(Sentinal.Instance.ToString());

        private void OnDestroy()
        {
            Sentinal.Instance.OnSwitch -= OnSwitch;
        }
    }
}
