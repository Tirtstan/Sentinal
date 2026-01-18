using UnityEngine;
using UnityEngine.UI;

namespace Sentinal.Samples
{
    public class ActivateView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private ViewSelector viewSelector;

        [SerializeField]
        private Button button;

        private void OnEnable() => button.onClick.AddListener(ToggleView);

        private void OnDisable() => button.onClick.RemoveListener(ToggleView);

        private void ToggleView()
        {
            if (viewSelector.IsActive)
                viewSelector.Close();
            else
                viewSelector.Open();
        }
    }
}
