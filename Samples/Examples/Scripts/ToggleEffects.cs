using UnityEngine;
using UnityEngine.UI;

namespace Sentinal.Samples
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleEffects : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField]
        private Color onColor = Color.red;
        private Color offColor = Color.white;
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            offColor = toggle.image.color;
        }

        private void OnEnable()
        {
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
            OnToggleValueChanged(toggle.isOn);
        }

        private void OnToggleValueChanged(bool isOn) => toggle.image.color = isOn ? onColor : offColor;

        private void OnDisable()
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }
}
